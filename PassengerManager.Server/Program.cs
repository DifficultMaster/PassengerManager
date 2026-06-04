using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using MassTransit;
using PassengerManager.Server.Hubs;
using PassengerManager.Server.Services;
using PassengerManager.Server.Services.Background;
using PassengerManager.Server.Services.Interfaces;
using PassengerManager.Server.Services.Security;
using PassengerManager.Server.Services.Static;
using PassengerManager.Shared.Protos;
using PassengerManager.Shared.Models;
using System;
using System.Net;
using System.Text;
using PassengerManager.Server.Models;
using System.Runtime.CompilerServices;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using PassengerManager.Shared.DTOs;

namespace PassengerManager.Server
{
    public class Program
    {
        private static void SetDefaultDatabaseObjects(WebApplication app)
        {
            using (IServiceScope scope = app.Services.CreateScope())
            {
                Models.PassengerManagerContext context = scope.ServiceProvider.GetRequiredService<Models.PassengerManagerContext>();

                // Apply pending migrations to ensure all tables exist
                try
                {
                    context.Database.Migrate();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error applying migrations: {ex.Message}");
                    throw;
                }

                if (!context.Users.Any())
                {
                    List<Shared.Models.User> users = new List<User>
                    {
                        new User
                        {
                            Username = "admin_user",
                            FullName = "Administrator",
                            PasswordHash = PasswordHandler.GetHashedPassword("admin_user"),
                            RoleId = 1,
                            CreatedAt = DateTime.UtcNow
                        },

                        new User
                        {
                            Username = "dispatcher_user",
                            FullName = "Dispatcher",
                            PasswordHash = PasswordHandler.GetHashedPassword("dispatcher_user"),
                            RoleId = 2,
                            CreatedAt = DateTime.UtcNow
                        },

                        new User
                        {
                            Username = "driver_user",
                            FullName = "Driver",
                            PasswordHash = PasswordHandler.GetHashedPassword("01234567"),
                            RoleId = 3,
                            CreatedAt = DateTime.UtcNow
                        }
                    };

                    // for debug testing only, remove on production
                    if (!context.Vehicles.Any())
                    {
                        Shared.Models.Vehicle vehicle = new Vehicle
                        {
                            VehicleId = "1099",
                            HardwareHash = "DEFAULT_HARDWARE_HASH"
                        };

                        context.Vehicles.Add(vehicle);
                    }
                    //

                    context.Users.AddRange(users);
                    context.SaveChanges();                    
                }
            }
        }

        public static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            // Configure settings (appsettings.json)
            AppDefaults.Configure(builder.Configuration);
            TimeoutDefaults.Configure(builder.Configuration);
            MapConstraints.Configure(builder.Configuration);
            AuthDefaults.Configure(builder.Configuration);

            // Add DB context
            builder.Services.AddDbContext<PassengerManager.Server.Models.PassengerManagerContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Add services to the container.
            builder.Services.AddHttpClient("GtfsClient", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("HttpSettings:GtfsClient:TimeoutSeconds", 10));
                client.DefaultRequestHeaders.ConnectionClose = true;
                client.DefaultRequestHeaders.UserAgent.ParseAdd(builder.Configuration.GetValue<string>("HttpSettings:GtfsClient:UserAgent", "PassengerManager/1.0"));

                client.DefaultRequestVersion = HttpVersion.Version11;
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(builder.Configuration.GetValue<int>("HttpSettings:GtfsClient:DnsRefreshMinutes", 5)),
                UseProxy = false
            });

            // Add JWT Token handling
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
                    ValidAudience = builder.Configuration["JwtSettings:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Key"]!))
                };
            });

            builder.Services.AddSignalR();
            builder.Services.AddScoped<ITokenService, JwtTokenService>();
            
            // Add MassTransit for event publishing with RabbitMQ
            builder.Services.AddMassTransit(x =>
            {
                x.SetKebabCaseEndpointNameFormatter();
                
                x.UsingRabbitMq((context, cfg) =>
                {
                    string rabbitMqHost = builder.Configuration.GetConnectionString("RabbitMQ") ?? "localhost";
                    cfg.Host(rabbitMqHost);
                    cfg.ConfigureEndpoints(context);
                });
            });
            
            builder.Services.AddScoped<IMessageService, MessageService>();
            builder.Services.AddScoped<INotificationService, NotificationService>();
            builder.Services.AddSingleton<DispatcherStateTrackerService>();

            // Add Redis handling
            GtfsScaleSettings defaultScaleSettings = (builder.Configuration.GetValue<string>("SystemProfile") ?? "Small").ToLower() switch
            {
                "large" => new GtfsScaleSettings(1_500_000, 10_000, 60, 30),
                "medium" => new GtfsScaleSettings(500_000, 2_500, 60, 45),
                _ => new GtfsScaleSettings(100_000, 500, 60, 60)
            };
            GtfsScaleSettings finalScaleSettings = new GtfsScaleSettings(
                builder.Configuration.GetValue<int>("GtfsSettings:ChannelCapacity", defaultScaleSettings.ChannelCapacity),
                builder.Configuration.GetValue<int>("GtfsSettings:ArchiverBatchSize", defaultScaleSettings.ArchiverBatchSize),
                builder.Configuration.GetValue<int>("GtfsSettings:ArchiverMaxWaitSeconds", defaultScaleSettings.ArchiverMaxWaitSeconds),
                builder.Configuration.GetValue<int>("GtfsSettings:RedisTtlSeconds", defaultScaleSettings.RedisTtlSeconds)
                );
            builder.Services.AddSingleton(finalScaleSettings);
            builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                IConfiguration configuration = sp.GetRequiredService<IConfiguration>();
                string baseConnection = configuration.GetConnectionString("Redis") ?? "localhost:6379";

                ConfigurationOptions options = ConfigurationOptions.Parse(baseConnection);
                options.AbortOnConnectFail = false;

                return ConnectionMultiplexer.Connect(options);
            });
            builder.Services.AddSingleton<TelemetryChannels>();
            builder.Services.AddHostedService<StaticSyncService>();
            builder.Services.AddHostedService<VehicleSyncService>();
            builder.Services.AddHostedService<TripSyncService>();
            builder.Services.AddHostedService<GtfsArchiveWorker>();

            // Configure Swagger UI
            builder.Services.AddControllers();
            builder.Services.AddGrpc().AddJsonTranscoding();
            builder.Services.AddGrpcSwagger();            
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = "PassengerManager API",
                    Version = "v1"
                });

                c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Description = "Please enter your JWT token",
                    Name = "Authorization",
                    In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                    Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT"
                });

                c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        new List<string>()
                    }
                });
            });
            builder.Services.AddGrpcReflection();

            try
            {
                WebApplication app = builder.Build();

                // Configure the HTTP request pipeline.
                if (app.Environment.IsDevelopment())
                {
                    app.UseSwagger();
                    app.UseSwaggerUI();

                    app.MapGrpcReflectionService();
                }

                app.UseAuthentication();
                app.UseAuthorization();
                app.MapControllers();

                // Map gRPC services
                app.MapGrpcService<PassengerManager.Server.Services.AuthService>();
                app.MapGrpcService<PassengerManager.Server.Services.DriverOpsService>();
                app.MapGrpcService<PassengerManager.Server.Services.CommunicationService>();
                app.MapGrpcService<PassengerManager.Server.Services.TelemetryService>();
                // app.MapGrpcService<PassengerManager.Server.Services.DispatcherOpsService>();
                // app.MapGrpcService<PassengerManager.Server.Services.AdminOpsService>();

                // Map SignalR Hubs
                app.MapHub<DispatcherHub>("/dispatcherHub");
                app.MapHub<DriverHub>("/driverHub");
                // app.MapHub<AdminHub>("/adminHub");

                SetDefaultDatabaseObjects(app);
                app.Run();
            }
            catch (System.Reflection.ReflectionTypeLoadException ex)
            {
                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine("CRITICAL ERROR: MISSING DEPENDENCY");

                foreach (Exception? loaderEx in ex.LoaderExceptions)
                {
                    Console.WriteLine($" -> {loaderEx?.Message}");
                }
                Console.WriteLine("--------------------------------------------------");

                throw;
            }
        }
    }
}

