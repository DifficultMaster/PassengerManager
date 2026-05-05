using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PassengerManager.Client.Core.Services.Interfaces;
using PassengerManager.Client.Core.Stores;
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
    /// </summary>
    public class HeartbeatBackgroundService : IDisposable
    {
        private readonly ITelemetryService _telemetryService;
        private readonly HardwareAccountStore _hardwareStore;
        private readonly DriverAccountStore _driverAccountStore;
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

        // Mock GPS location (would be replaced with actual GPS in production)
        private double _latitude = 50.4501;
        private double _longitude = 30.5234;
        private double _bearing = 0.0;
        private double _speed = 0.0;
        private double _odometer = 0.0;

        public HeartbeatBackgroundService(
            ITelemetryService telemetryService,
            HardwareAccountStore hardwareStore,
            DriverAccountStore driverAccountStore,
            IConfiguration configuration,
            ILogger<HeartbeatBackgroundService> logger)
        {
            _telemetryService = telemetryService;
            _hardwareStore = hardwareStore;
            _driverAccountStore = driverAccountStore;
            _configuration = configuration;
            _logger = logger;

            LoadConfiguration();
        }

        /// <summary>
        /// Loads heartbeat configuration from appsettings.json
        /// </summary>
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

        /// <summary>
        /// Starts the background heartbeat service.
        /// Must be called from UI thread (OnStartup).
        /// </summary>
        public void Start()
        {
            if (_heartbeatTask != null)
            {
                _logger.LogWarning("Heartbeat service already started");
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _heartbeatTask = HeartbeatLoop(_cancellationTokenSource.Token);

            _logger.LogInformation("Heartbeat background service started");
        }

        /// <summary>
        /// Stops the background heartbeat service gracefully.
        /// Must be called from UI thread (OnExit).
        /// </summary>
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

            _heartbeatTask = null;
        }

        /// <summary>
        /// Main heartbeat loop that runs continuously.
        /// Sends heartbeats whenever hardware is logged in (always-on telemetry).
        /// Uses different intervals based on whether driver is also logged in.
        /// </summary>
        private async Task HeartbeatLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Always send heartbeat if hardware is logged in
                    if (_hardwareStore.IsLoggedIn)
                    {
                        // Determine interval based on driver login status
                        bool isDriverLoggedIn = _driverAccountStore.IsLoggedIn;
                        bool isAppInForeground = IsApplicationInForeground();

                        int intervalSeconds = isDriverLoggedIn
                            ? _activeHeartbeatIntervalSeconds 
                            : _idleHeartbeatIntervalSeconds;

                        // Send heartbeat
                        var request = new SendHeartbeatRequest
                        {
                            Latitude = _latitude,
                            Longitude = _longitude,
                            Bearing = _bearing,
                            Odometer = _odometer,
                            Speed = _speed,
                            IsAppInForeground = isAppInForeground
                        };

                        var response = await _telemetryService.SendHeartbeatAsync(request);

                        if (response.Success)
                        {
                            _logger.LogDebug(
                                "Heartbeat sent - Location: ({Latitude}, {Longitude}), Speed: {Speed}, Driver: {DriverLoggedIn}, Foreground: {IsForeground}",
                                _latitude,
                                _longitude,
                                _speed,
                                isDriverLoggedIn,
                                isAppInForeground);
                        }
                        else
                        {
                            _logger.LogWarning("Heartbeat failed to send");
                        }

                        // Wait for the next interval
                        await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);
                    }
                    else
                    {
                        // Hardware not logged in, wait longer before checking again
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
                    // Continue on error, wait a bit before retrying
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
        }

        /// <summary>
        /// Checks if the application window is in the foreground.
        /// </summary>
        private bool IsApplicationInForeground()
        {
            try
            {
                return Application.Current?.MainWindow?.IsActive ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Updates mock GPS location (in production, would integrate with actual GPS provider).
        /// </summary>
        public void UpdateLocation(double latitude, double longitude, double bearing, double speed, double odometer)
        {
            _latitude = latitude;
            _longitude = longitude;
            _bearing = bearing;
            _speed = speed;
            _odometer = odometer;
        }

        /// <summary>
        /// For testing: Updates location with a slight random variation to simulate movement.
        /// </summary>
        public void SimulateLocationChange()
        {
            Random random = new Random();
            _latitude += (random.NextDouble() - 0.5) * 0.001;  // ~100 meters variation
            _longitude += (random.NextDouble() - 0.5) * 0.001; // ~100 meters variation
            _bearing = (random.NextDouble() * 360);
            _speed = random.NextDouble() * 50; // 0-50 km/h
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
