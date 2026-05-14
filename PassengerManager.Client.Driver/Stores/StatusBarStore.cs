using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PassengerManager.Client.Driver.Stores
{
    public partial class StatusBarStore : ObservableObject
    {
        [ObservableProperty]
        private ConnectionLevel _connectionLevel = ConnectionLevel.None;

        [ObservableProperty]
        private DrivingHoursState _drivingHours = DrivingHoursState.Prohibited;

        [ObservableProperty]
        private int _drivingMinutesLeft = 0;

        [ObservableProperty]
        private bool _isMicrophoneOn = false;

        [ObservableProperty] 
        private bool _isTrackerOn = false;

        [ObservableProperty]
        private bool _isTrackingAvailable = false;
    }
}
