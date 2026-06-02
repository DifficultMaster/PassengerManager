using Microsoft.Extensions.Configuration;
using PassengerManager.Server.Services.Static;

namespace PassengerManager.Tests.Unit.Server;

public class AuthDefaultsTests
{
    [Test]
    public void ConfigureOverridesDefaultsWhenValuesProvided()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthDefaults:Staff:MinPasswordLength"] = "12",
                ["AuthDefaults:Terminal:MaxFailedAttempts"] = "2"
            })
            .Build();

        AuthDefaults.Configure(configuration);

        Assert.Multiple(() =>
        {
            Assert.That(AuthDefaults.Staff.MinPasswordLength, Is.EqualTo(12));
            Assert.That(AuthDefaults.Terminal.MaxFailedAttempts, Is.EqualTo(2));
        });
    }
}
