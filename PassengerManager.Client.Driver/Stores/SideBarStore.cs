using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PassengerManager.Client.Driver.Stores
{
    public partial class SideBarStore : ObservableObject
    {
        [ObservableProperty]
        private bool _isEmergency = false;

        [ObservableProperty]
        private bool _isOverlay = false;

        [ObservableProperty]
        private SideBarButtonState _navigationButtonState = SideBarButtonState.Disabled;

        [ObservableProperty]
        private SideBarButtonState _reportButtonState = SideBarButtonState.Disabled;

        [ObservableProperty]
        private SideBarButtonState _phoneButtonState = SideBarButtonState.Disabled;

        [ObservableProperty]
        private CallStatus _callStatus = CallStatus.None;

        [ObservableProperty]
        private string _callId = string.Empty;

        [ObservableProperty]
        private SideBarButtonState _settingsButtonState = SideBarButtonState.Disabled;
    }
}
