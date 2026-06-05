using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using PassengerManager.Client.Core.DTOs;
using PassengerManager.Client.Core.Services.Interfaces;
using PassengerManager.Client.Core.Stores;
using PassengerManager.Client.Driver.Services;
using PassengerManager.Client.Driver.Stores;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PassengerManager.Tests.Stress.Stress;

public class HeartbeatStressTests
{
    [Test]
    [Timeout(5000)]
    public async Task HeartbeatLoopHandlesRapidTicks()
    {
        TestTelemetryService telemetryService = new();
        TestLocationProvider locationProvider = new();
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
            locationProvider,
            hardwareStore,
            driverStore,
            sideBarStore,
            configuration,
            NullLogger<HeartbeatBackgroundService>.Instance);

        hardwareStore.Login(new PassengerManager.Shared.Protos.HardwareLoginResponse { Token = "token" }, "Vehicle42");

        service.Start();
        await Task.Delay(1500);
        await service.StopAsync();

        Assert.That(telemetryService.HeartbeatCount, Is.GreaterThanOrEqualTo(1));
    }

    // Mock Location Provider for the test
    private sealed class TestLocationProvider : ILocationProvider
    {
        public Task<GeoLocation> GetCurrentLocationAsync()
        {
            // Returns a static dummy location instantly to prevent I/O blocking during the stress test
            return Task.FromResult(new GeoLocation(50.4501, 30.5234, 40.0, 0.0, 15000.0));
        }
    }

    private sealed class TestTelemetryService : ITelemetryService
    {
        public int HeartbeatCount { get; private set; }

        public Task<PassengerManager.Shared.Protos.SendHeartbeatResponse> SendHeartbeatAsync(PassengerManager.Shared.Protos.SendHeartbeatRequest request)
        {
            HeartbeatCount++;
            return Task.FromResult(new PassengerManager.Shared.Protos.SendHeartbeatResponse { Success = true });
        }

        public Task<PassengerManager.Shared.Protos.SendStatusResponse> SendStatusAsync(PassengerManager.Shared.Protos.SendStatusRequest request)
        {
            return Task.FromResult(new PassengerManager.Shared.Protos.SendStatusResponse { Success = true });
        }
    }
}