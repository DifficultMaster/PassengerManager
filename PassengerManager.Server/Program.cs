

using Microsoft.EntityFrameworkCore;
using PassengerManager.Server.Services;
using PassengerManager.Server.Services.Background;
using System.Text;

namespace PassengerManager.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            var builder = WebApplication.CreateBuilder(args);

            // Add DB context
            builder.Services.AddDbContext<PassengerManager.Server.Models.PassengerManagerContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Add services to the container.
            builder.Services.AddHttpClient();

            builder.Services.AddHostedService<StaticSyncService>();
            builder.Services.AddHostedService<VehicleSyncService>();
            builder.Services.AddHostedService<TripSyncService>();

            builder.Services.AddGrpc();
            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddGrpcReflection();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapGrpcReflectionService();
            }

            app.UseAuthorization();

            app.MapGrpcService<TransportService>();
            app.MapControllers();

            app.Run();
        }
    }
}
