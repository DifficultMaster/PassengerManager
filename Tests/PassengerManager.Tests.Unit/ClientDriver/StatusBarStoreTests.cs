using PassengerManager.Client.Driver.Stores;

namespace PassengerManager.Tests.Unit.ClientDriver;

public class StatusBarStoreTests
{
    [Test]
    public void DefaultsMatchExpectedState()
    {
        StatusBarStore store = new();

        Assert.Multiple(() =>
        {
            Assert.That(store.ConnectionLevel, Is.EqualTo(ConnectionLevel.None));
            Assert.That(store.DrivingHours, Is.EqualTo(DrivingHoursState.Prohibited));
            Assert.That(store.DrivingMinutesLeft, Is.EqualTo(0));
            Assert.That(store.IsMicrophoneOn, Is.False);
            Assert.That(store.IsTrackerOn, Is.False);
            Assert.That(store.IsTrackingAvailable, Is.False);
        });
    }
}
