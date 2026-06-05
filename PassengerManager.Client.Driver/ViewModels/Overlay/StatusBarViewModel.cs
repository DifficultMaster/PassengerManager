using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using PassengerManager.Client.Core.Stores;
using PassengerManager.Client.Driver.Stores;

namespace PassengerManager.Client.Driver.ViewModels.Overlay
{
    public partial class StatusBarViewModel : ObservableObject, IDisposable
    {
        private readonly StatusBarStore _store;
        private readonly DriverAccountStore _driverAccountStore;
        private readonly DispatcherTimer _timer;

        [ObservableProperty]
        private string _currentTime = string.Empty;

        [ObservableProperty]
        private string _currentDate = string.Empty;

        [ObservableProperty]
        private string _currentDelay = string.Empty; // proper support to be added when fixed scheduling is a supported feature

        [ObservableProperty]
        private bool _isLoggedIn;

        public StatusBarStore Store
        {
            get => _store;
        }

        public StatusBarViewModel(StatusBarStore store, DriverAccountStore driverAccountStore)
        {
            _store = store;
            _driverAccountStore = driverAccountStore;

            IsLoggedIn = _driverAccountStore.IsLoggedIn;

            _driverAccountStore.StateChanged += OnDriverStateChanged;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += TimerTick;
            _timer.Start();

            UpdateDateTime();
        }

        private void OnDriverStateChanged()
        {
            IsLoggedIn = _driverAccountStore.IsLoggedIn;
        }

        private void TimerTick(object? sender, EventArgs e)
        {
            UpdateDateTime();
        }

        public void UpdateDateTime()
        {
            DateTime now = DateTime.Now;
            CurrentTime = now.ToString("HH:mm:ss");
            CurrentDate = now.ToString("dd.MM.yy ddd");
        }

        public void Dispose()
        {
            _driverAccountStore.StateChanged -= OnDriverStateChanged;
            _timer.Stop();
        }
    }
}
