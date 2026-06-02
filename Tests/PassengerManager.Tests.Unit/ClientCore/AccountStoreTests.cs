using PassengerManager.Client.Core.Stores;

namespace PassengerManager.Tests.Unit.ClientCore;

public class AccountStoreTests
{
    [Test]
    public void LogoutClearsTokenAndDisplayName()
    {
        TestAccountStore store = new();
        store.SetAuth("token", "display");

        store.Logout();

        Assert.That(store.Token, Is.Empty);
        Assert.That(store.DisplayName, Is.Empty);
    }

    [Test]
    public void LogoutRaisesStateChanged()
    {
        TestAccountStore store = new();
        int changeCount = 0;
        store.StateChanged += () => changeCount++;

        store.Logout();

        Assert.That(changeCount, Is.EqualTo(1));
    }

    private sealed class TestAccountStore : AccountStore
    {
        public void SetAuth(string token, string displayName)
        {
            Token = token;
            DisplayName = displayName;
        }
    }
}
