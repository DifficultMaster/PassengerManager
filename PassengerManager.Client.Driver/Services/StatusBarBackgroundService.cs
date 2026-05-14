using Microsoft.Extensions.Configuration;
using PassengerManager.Client.Core.Stores;
using PassengerManager.Client.Driver.Stores;
using System.Windows.Threading;

namespace PassengerManager.Client.Driver.Services
{
    public class StatusBarBackgroundService : IDisposable
    {
        private readonly StatusBarStore _store;
        private readonly SideBarStore _sideBarStore;
        private readonly DriverAccountStore _driverAccountStore;
        private readonly HeartbeatBackgroundService _heartbeatService;
        private readonly DispatcherTimer _drivingHoursTimer;
        private readonly int _maxDrivingMinutes;
        private readonly int _warningMinutes;
        private bool _disposed;

        public StatusBarBackgroundService(
            StatusBarStore store,
            SideBarStore sideBarStore,
            DriverAccountStore driverAccountStore,
            HeartbeatBackgroundService heartbeatService,
            IConfiguration configuration)
        {
            _store = store;
            _sideBarStore = sideBarStore;
            _driverAccountStore = driverAccountStore;
            _heartbeatService = heartbeatService;

            IConfigurationSection drivingHours = configuration.GetSection("DrivingHours");
            _maxDrivingMinutes = drivingHours.GetValue<int?>("MaxMinutes") ?? 540;
            _warningMinutes = drivingHours.GetValue<int?>("WarningMinutes") ?? 30;

            _drivingHoursTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(1)
            };

            _drivingHoursTimer.Tick += (_, _) => UpdateDrivingHours();
            _drivingHoursTimer.Start();

            _sideBarStore.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(SideBarStore.CallStatus))
                    UpdateMicrophoneState();
            };

            _driverAccountStore.StateChanged += () => UpdateDrivingHours();
            _heartbeatService.TrackingAvailabilityChanged += UpdateTrackingAvailability;

            UpdateMicrophoneState();
            UpdateTrackingAvailability(_driverAccountStore.IsLoggedIn);
            UpdateDrivingHours();
        }

        private void UpdateMicrophoneState()
        {
            _store.IsMicrophoneOn = _sideBarStore.CallStatus == CallStatus.Live;
        }

        private void UpdateTrackingAvailability(bool isAvailable)
        {
            _store.IsTrackingAvailable = isAvailable;
            _store.IsTrackerOn = isAvailable;
        }

        private void UpdateDrivingHours()
        {
            if (!_driverAccountStore.IsLoggedIn)
            {
                _store.DrivingHours = DrivingHoursState.Prohibited;
                _store.DrivingMinutesLeft = 0;
                return;
            }

            int elapsedMinutes = (int)Math.Floor((DateTime.UtcNow - _driverAccountStore.LoginTimeUtc).TotalMinutes);
            int minutesLeft = Math.Max(0, _maxDrivingMinutes - elapsedMinutes);

            _store.DrivingMinutesLeft = minutesLeft;

            if (minutesLeft <= 0)
                _store.DrivingHours = DrivingHoursState.Prohibited;
            else if (minutesLeft <= _warningMinutes)
                _store.DrivingHours = DrivingHoursState.Warning;
            else
                _store.DrivingHours = DrivingHoursState.Normal;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _drivingHoursTimer.Stop();
            _disposed = true;
        }
    }
}
