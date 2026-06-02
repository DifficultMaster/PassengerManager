using PassengerManager.Client.Driver.Stores;

namespace PassengerManager.Tests.Integration.Integration;

public class StatusBarConnectionTests
{
    [Test]
    public void ConnectionLevelCanBeUpdated()
    {
        StatusBarStore store = new();

        store.ConnectionLevel = ConnectionLevel.High;

        Assert.That(store.ConnectionLevel, Is.EqualTo(ConnectionLevel.High));
    }
}
