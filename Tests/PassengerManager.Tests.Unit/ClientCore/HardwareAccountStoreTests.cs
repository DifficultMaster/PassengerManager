using PassengerManager.Client.Core.Stores;
using PassengerManager.Shared.Protos;

namespace PassengerManager.Tests.Unit.ClientCore;

public class HardwareAccountStoreTests
{
    [Test]
    public void LoginSetsTokenVehicleAndDisplayName()
    {
        HardwareAccountStore store = new();
        HardwareLoginResponse response = new()
        {
            Token = "token"
        };

        store.Login(response, "Vehicle42");

        Assert.Multiple(() =>
        {
            Assert.That(store.Token, Is.EqualTo("token"));
            Assert.That(store.VehicleId, Is.EqualTo("Vehicle42"));
            Assert.That(store.DisplayName, Is.EqualTo("Hardware_Vehicle42"));
        });
    }

    [Test]
    public void LogoutClearsVehicleId()
    {
        HardwareAccountStore store = new();
        store.Login(new HardwareLoginResponse { Token = "token" }, "Vehicle1");

        store.Logout();

        Assert.That(store.VehicleId, Is.Empty);
    }
}
