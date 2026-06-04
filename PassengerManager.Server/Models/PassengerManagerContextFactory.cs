using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace PassengerManager.Server.Models
{
    public class PassengerManagerContextFactory : IDesignTimeDbContextFactory<PassengerManagerContext>
    {
        public PassengerManagerContext CreateDbContext(string[] args)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddUserSecrets<Program>()
                .AddEnvironmentVariables()
                .Build();

            string? connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");
            }

            DbContextOptionsBuilder<PassengerManagerContext> optionsBuilder = new DbContextOptionsBuilder<PassengerManagerContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new PassengerManagerContext(optionsBuilder.Options);
        }
    }
}
