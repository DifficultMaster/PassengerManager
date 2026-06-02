using PassengerManager.Client.Core.Stores;
using PassengerManager.Shared.Protos;

namespace PassengerManager.Tests.Unit.ClientCore;

public class DriverAccountStoreTests
{
    [Test]
    public void LoginSetsTokenDisplayNameShiftIdAndLoginTime()
    {
        DriverAccountStore store = new();
        DriverLoginResponse response = new()
        {
            Token = "token",
            DriverName = "driver",
            ShiftId = 42
        };

        store.Login(response);

        Assert.Multiple(() =>
        {
            Assert.That(store.Token, Is.EqualTo("token"));
            Assert.That(store.DisplayName, Is.EqualTo("driver"));
            Assert.That(store.CurrentShiftId, Is.EqualTo(42));
            Assert.That(store.LoginTimeUtc, Is.Not.EqualTo(DateTime.MinValue));
        });
    }

    [Test]
    public void LogoutClearsShiftState()
    {
        DriverAccountStore store = new();
        store.Login(new DriverLoginResponse { Token = "token", DriverName = "driver", ShiftId = 10 });

        store.Logout();

        Assert.Multiple(() =>
        {
            Assert.That(store.CurrentShiftId, Is.EqualTo(0));
            Assert.That(store.LoginTimeUtc, Is.EqualTo(DateTime.MinValue));
            Assert.That(store.Token, Is.Empty);
        });
    }
}
