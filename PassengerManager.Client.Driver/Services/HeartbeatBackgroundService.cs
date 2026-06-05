using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PassengerManager.Client.Core.Services.Interfaces;
using PassengerManager.Client.Core.Stores;
using PassengerManager.Client.Driver.Stores;
using PassengerManager.Shared.Protos;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PassengerManager.Client.Driver.Services
{
    /// <summary>
    /// Background service that sends periodic heartbeats to the server with vehicle telemetry data.
    /// Runs continuously from app startup to shutdown, respecting different intervals based on login status.
    /// 
    /// Heartbeat states:
    /// - Active (driver logged in): Sends at ActiveHeartbeatIntervalSeconds (e.g., 5s)
    /// - Idle (hardware logged in only): Sends at IdleHeartbeatIntervalSeconds (e.g., 30s)
    /// - Emergency: Sends at EmergencyHeartbeatIntervalSeconds (e.g., 1s)
    /// </summary>
    public class HeartbeatBackgroundService
    {
        private readonly ITelemetryService _telemetryService;
        private readonly ILocationProvider _locationProvider;
        private readonly HardwareAccountStore _hardwareStore;
        private readonly DriverAccountStore _driverAccountStore;
        private readonly SideBarStore _sideBarStore;
        private readonly IConfiguration _configuration;
        private readonly ILogger<HeartbeatBackgroundService> _logger;

        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _heartbeatTask;
        private bool _disposed;

        // Configuration values from appsettings
        private int _activeHeartbeatIntervalSeconds;
        private int _idleHeartbeatIntervalSeconds;
        private int _emergencyHeartbeatIntervalSeconds;
        private string _vehicleId;

        public event Action<bool>? TrackingAvailabilityChanged;

        public HeartbeatBackgroundService(
            ITelemetryService telemetryService,
            ILocationProvider locationProvider,
            HardwareAccountStore hardwareStore,
            DriverAccountStore driverAccountStore,
            SideBarStore sideBarStore,
            IConfiguration configuration,
            ILogger<HeartbeatBackgroundService> logger)
        {
            _telemetryService = telemetryService;
            _locationProvider = locationProvider;
            _hardwareStore = hardwareStore;
            _driverAccountStore = driverAccountStore;
            _sideBarStore = sideBarStore;
            _configuration = configuration;
            _logger = logger;

            LoadConfiguration();
        }

        private void LoadConfiguration()
        {
            var terminalSettings = _configuration.GetSection("TerminalSettings");

            _vehicleId = terminalSettings["VehicleId"] ?? "UNKNOWN";
            _activeHeartbeatIntervalSeconds = int.Parse(terminalSettings["ActiveHeartbeatIntervalSeconds"] ?? "5");
            _idleHeartbeatIntervalSeconds = int.Parse(terminalSettings["IdleHeartbeatIntervalSeconds"] ?? "30");
            _emergencyHeartbeatIntervalSeconds = int.Parse(terminalSettings["EmergencyHeartbeatIntervalSeconds"] ?? "1");

            _logger.LogInformation(
                "Heartbeat configured - Active: {ActiveInterval}s, Idle: {IdleInterval}s, Emergency: {EmergencyInterval}s, Vehicle: {VehicleId}",
                _activeHeartbeatIntervalSeconds,
                _idleHeartbeatIntervalSeconds,
                _emergencyHeartbeatIntervalSeconds,
                _vehicleId);
        }

        public void Start()
        {
            if (_heartbeatTask != null)
            {
                _logger.LogWarning("Heartbeat service already started");
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            TrackingAvailabilityChanged?.Invoke(true);
            _heartbeatTask = HeartbeatLoop(_cancellationTokenSource.Token);

            _logger.LogInformation("Heartbeat background service started");
        }

        public async Task StopAsync()
        {
            if (_heartbeatTask == null)
            {
                return;
            }

            _logger.LogInformation("Stopping heartbeat background service");

            _cancellationTokenSource?.Cancel();

            try
            {
                await (_heartbeatTask ?? Task.CompletedTask);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during heartbeat service shutdown");
            }
            finally
            {
                TrackingAvailabilityChanged?.Invoke(false);
            }

            _heartbeatTask = null;
        }

        private async Task HeartbeatLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    TrackingAvailabilityChanged?.Invoke(true);

                    if (_hardwareStore.IsLoggedIn)
                    {
                        bool isDriverLoggedIn = _driverAccountStore.IsLoggedIn;
                        bool isAppInForeground = IsApplicationInForeground();
                        int intervalSeconds;

                        if (_sideBarStore.IsEmergency)
                            intervalSeconds = _emergencyHeartbeatIntervalSeconds;
                        else if (isDriverLoggedIn)
                            intervalSeconds = _activeHeartbeatIntervalSeconds;
                        else
                            intervalSeconds = _idleHeartbeatIntervalSeconds;

                        // Fetch live or mocked coordinates from the provider
                        var location = await _locationProvider.GetCurrentLocationAsync();

                        // Map coordinates directly to the gRPC request
                        var request = new SendHeartbeatRequest
                        {
                            Latitude = location.Latitude,
                            Longitude = location.Longitude,
                            Bearing = location.Bearing,
                            Odometer = location.Odometer,
                            Speed = location.Speed, // already in km/h
                            IsAppInForeground = isAppInForeground
                        };

                        // Safely attach optional Protobuf strings ONLY if they exist
                        if (_driverAccountStore.IsLoggedIn)
                        {
                            if (!string.IsNullOrEmpty(_driverAccountStore.CurrentRouteId))
                                request.RouteId = _driverAccountStore.CurrentRouteId;

                            if (!string.IsNullOrEmpty(_driverAccountStore.CurrentTripId))
                                request.TripId = _driverAccountStore.CurrentTripId;
                        }
                        var response = await _telemetryService.SendHeartbeatAsync(request);

                        if (response.Success)
                        {
                            _logger.LogDebug(
                                "Heartbeat sent - Location: ({Latitude}, {Longitude}), Speed: {Speed} km/h, Trip: {RouteId} - {TripId}, Driver: {DriverLoggedIn}",
                                location.Latitude,
                                location.Longitude,
                                location.Speed,
                                request.RouteId,
                                request.TripId,
                                isDriverLoggedIn);
                        }
                        else
                        {
                            _logger.LogWarning("Heartbeat failed to send");
                        }

                        await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Heartbeat loop cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in heartbeat loop");
                    TrackingAvailabilityChanged?.Invoke(false);
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
        }

        private bool IsApplicationInForeground()
        {
            try
            {
                if (Application.Current == null) return false;

                // Safely marshal the UI thread check
                return Application.Current.Dispatcher.Invoke(() =>
                    Application.Current.MainWindow?.IsActive ?? false);
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _cancellationTokenSource?.Dispose();
            _disposed = true;
        }
    }
}