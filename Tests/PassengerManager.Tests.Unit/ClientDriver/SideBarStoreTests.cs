using PassengerManager.Client.Driver.Stores;

namespace PassengerManager.Tests.Unit.ClientDriver;

public class SideBarStoreTests
{
    [Test]
    public void DefaultsMatchExpectedState()
    {
        SideBarStore store = new();

        Assert.Multiple(() =>
        {
            Assert.That(store.IsEmergency, Is.False);
            Assert.That(store.IsOverlay, Is.False);
            Assert.That(store.NavigationButtonState, Is.EqualTo(SideBarButtonState.Disabled));
            Assert.That(store.ReportButtonState, Is.EqualTo(SideBarButtonState.Disabled));
            Assert.That(store.PhoneButtonState, Is.EqualTo(SideBarButtonState.Disabled));
            Assert.That(store.CallStatus, Is.EqualTo(CallStatus.None));
            Assert.That(store.CallId, Is.Empty);
            Assert.That(store.SettingsButtonState, Is.EqualTo(SideBarButtonState.Disabled));
        });
    }
}
