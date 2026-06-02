using Microsoft.Extensions.Configuration;
using PassengerManager.Server.Services.Static;

namespace PassengerManager.Tests.Unit.Server;

public class MapConstraintsTests
{
    [Test]
    public void ConfigureOverridesDefaultsWhenValuesProvided()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MapConstraints:MaxKeyLength"] = "256",
                ["MapConstraints:Agency:MaxTimezoneLength"] = "60",
                ["MapConstraints:Route:MaxColorLength"] = "12",
                ["MapConstraints:Stop:MaxCodeLength"] = "80"
            })
            .Build();

        MapConstraints.Configure(configuration);

        Assert.Multiple(() =>
        {
            Assert.That(MapConstraints.MaxKeyLength, Is.EqualTo(256));
            Assert.That(MapConstraints.Agency.MaxTimezoneLength, Is.EqualTo(60));
            Assert.That(MapConstraints.Route.MaxColorLength, Is.EqualTo(12));
            Assert.That(MapConstraints.Stop.MaxCodeLength, Is.EqualTo(80));
        });
    }
}
