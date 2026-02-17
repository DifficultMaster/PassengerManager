using Microsoft.EntityFrameworkCore;
using PassengerManager.Server.Hubs;
using PassengerManager.Server.Services;
using PassengerManager.Server.Services.Background;
using PassengerManager.Server.Services.Security;
using PassengerManager.Server.Services.Static;
using PassengerManager.Shared.Protos;
using System.Net;
using System.Text;

namespace PassengerManager.Server
{
    public class Program
    {
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
                client.DefaultRequestHeaders.UserAgent.ParseAdd(builder.Configuration.GetValue<string>("HttpSettings:GtfsClient:UserAgent", "PassegnerManager/1.0"));

                client.DefaultRequestVersion = HttpVersion.Version11;
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(builder.Configuration.GetValue<int>("HttpSettings:GtfsClient:DnsRefreshMinutes", 5)),
                UseProxy = false
            });

            builder.Services.AddSignalR();
            builder.Services.AddScoped<ITokenService, JwtTokenService>();

            builder.Services.AddHostedService<StaticSyncService>();
            builder.Services.AddHostedService<VehicleSyncService>();
            builder.Services.AddHostedService<TripSyncService>();

            builder.Services.AddGrpc();
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddGrpcReflection();

            WebApplication app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapGrpcReflectionService();
            }

            app.UseAuthorization();
            app.MapControllers();

            // Map gRPC services
            app.MapGrpcService<PassengerManager.Server.Services.AuthService>();
            app.MapGrpcService<PassengerManager.Server.Services.DriverOpsService>();
            // app.MapGrpcService<PassengerManager.Server.Services.DispatcherOpsService>();
            // app.MapGrpcService<PassengerManager.Server.Services.AdminOpsService>();

            // Map SignalR Hubs
            app.MapHub<DispatcherHub>("/dispatcherHub");
            app.MapHub<DriverHub>("/driverHub");
            // app.MapHub<AdminHub>("/adminHub");

            app.Run();
        }
    }
}
