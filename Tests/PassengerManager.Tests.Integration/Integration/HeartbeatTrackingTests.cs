using PassengerManager.Client.Driver.Services;
using PassengerManager.Client.Driver.Stores;
using PassengerManager.Client.Core.Services.Interfaces;
using PassengerManager.Client.Core.Stores;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace PassengerManager.Tests.Integration.Integration;

public class HeartbeatTrackingTests
{
    [Test]
    public async Task TrackingAvailabilityChangesWhenServiceStartsAndStops()
    {
        TestTelemetryService telemetryService = new();
        HardwareAccountStore hardwareStore = new();
        DriverAccountStore driverStore = new();
        SideBarStore sideBarStore = new();
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TerminalSettings:VehicleId"] = "Vehicle42",
            ["TerminalSettings:ActiveHeartbeatIntervalSeconds"] = "1",
            ["TerminalSettings:IdleHeartbeatIntervalSeconds"] = "1",
            ["TerminalSettings:EmergencyHeartbeatIntervalSeconds"] = "1"
        }).Build();

        HeartbeatBackgroundService service = new(
            telemetryService,
            hardwareStore,
            driverStore,
            sideBarStore,
            configuration,
            NullLogger<HeartbeatBackgroundService>.Instance);

        bool tracking = false;
        service.TrackingAvailabilityChanged += value => tracking = value;

        hardwareStore.Login(new PassengerManager.Shared.Protos.HardwareLoginResponse { Token = "token" }, "Vehicle42");

        service.Start();
        await Task.Delay(50);

        Assert.That(tracking, Is.True);

        await service.StopAsync();

        Assert.That(tracking, Is.False);
    }

    private sealed class TestTelemetryService : ITelemetryService
    {
        public Task<PassengerManager.Shared.Protos.SendHeartbeatResponse> SendHeartbeatAsync(PassengerManager.Shared.Protos.SendHeartbeatRequest request)
        {
            return Task.FromResult(new PassengerManager.Shared.Protos.SendHeartbeatResponse { Success = true });
        }

        public Task<PassengerManager.Shared.Protos.SendStatusResponse> SendStatusAsync(PassengerManager.Shared.Protos.SendStatusRequest request)
        {
            return Task.FromResult(new PassengerManager.Shared.Protos.SendStatusResponse { Success = true });
        }
    }
}
