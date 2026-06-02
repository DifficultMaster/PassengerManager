using PassengerManager.Shared.Models;

namespace PassengerManager.Tests.Unit.Shared;

public class ModelDefaultsTests
{
    [Test]
    public void VehicleDefaultsAreInitialized()
    {
        Vehicle vehicle = new();

        Assert.Multiple(() =>
        {
            Assert.That(vehicle.VehicleId, Is.Not.Null);
            Assert.That(vehicle.VehicleId, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void UserDefaultsAreInitialized()
    {
        User user = new();

        Assert.Multiple(() =>
        {
            Assert.That(user.Username, Is.Not.Null);
            Assert.That(user.Username, Is.EqualTo(string.Empty));
        });
    }
}
