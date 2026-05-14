using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore.Metadata;
using PassengerManager.Client.Core.Services.Interfaces;
using PassengerManager.Client.Core.Stores;
using PassengerManager.Client.Core.ViewModels;
using PassengerManager.Client.Driver.Stores;
using PassengerManager.Shared.Protos;

namespace PassengerManager.Client.Driver.ViewModels.Overlay
{
    public partial class SideBarViewModel : ObservableObject
    {
        private readonly INavigationService _navigationService;
        private readonly ICommunicationService _communicationService;
        private readonly SideBarStore _store;

        public SideBarStore Store { get => _store; }

        public SideBarViewModel(
            SideBarStore store,
            INavigationService navigationService,
            ICommunicationService communicationService)
        {
            _navigationService = navigationService;
            _store = store;
            _communicationService = communicationService;
        }

        [RelayCommand]
        private async Task InitiateEmergency()
        {
            _store.IsEmergency = true;
            _store.CallStatus = CallStatus.Outgoing;
            _store.CallId = string.Empty;

            InitiateCallRequest request = new InitiateCallRequest
            {
                CallType = InitiateCallRequest.Types.CallType.Emergency
            };

            InitiateCallResponse response = await _communicationService.InitiateCallAsync(request);

            if (response.Success)
            {
                _store.CallStatus = CallStatus.Live;
                _store.CallId = response.CallId;
            }
            else
            {
                _store.CallStatus = CallStatus.None;
                _store.CallId = string.Empty;
            }
        }

        // explicit over implicit arrays for UI here
        [RelayCommand]
        private async Task NavigateToMap()
        {
            _store.NavigationButtonState = SideBarButtonState.Selected;

            if (_store.ReportButtonState != SideBarButtonState.Disabled)
                _store.ReportButtonState = SideBarButtonState.Enabled;

            if (_store.PhoneButtonState != SideBarButtonState.Disabled)
                _store.PhoneButtonState = SideBarButtonState.Enabled;

            if (_store.SettingsButtonState != SideBarButtonState.Disabled)
                _store.SettingsButtonState = SideBarButtonState.Enabled;

            //_navigationService.NavigateTo<>();
        }

        [RelayCommand]
        private async Task NavigateToReport()
        {
            _store.ReportButtonState = SideBarButtonState.Selected;

            if (_store.NavigationButtonState != SideBarButtonState.Disabled)
                _store.NavigationButtonState = SideBarButtonState.Enabled;

            if (_store.PhoneButtonState != SideBarButtonState.Disabled)
                _store.PhoneButtonState = SideBarButtonState.Enabled;

            if (_store.SettingsButtonState != SideBarButtonState.Disabled)
                _store.SettingsButtonState = SideBarButtonState.Enabled;

            //_navigationService.NavigateTo<>();
        }

        [RelayCommand]
        private async Task CallDispatch()
        {
            if (_store.CallStatus == CallStatus.Live)
            {
                if (!_store.IsEmergency)
                {
                    await EndCallAsync();
                }

                return;
            }

            _store.IsEmergency = false;
            _store.CallStatus = CallStatus.Outgoing;
            _store.CallId = string.Empty;

            InitiateCallRequest request = new InitiateCallRequest
            {
                CallType = InitiateCallRequest.Types.CallType.Standard
            };

            InitiateCallResponse response = await _communicationService.InitiateCallAsync(request);

            if (response.Success)
            {
                _store.CallStatus = CallStatus.Live;
                _store.CallId = response.CallId;
            }
            else
            {
                _store.CallStatus = CallStatus.None;
                _store.CallId = string.Empty;
            }
        }

        private async Task EndCallAsync()
        {
            if (string.IsNullOrWhiteSpace(_store.CallId))
            {
                _store.CallStatus = CallStatus.None;
                return;
            }

            EndCallResponse response = await _communicationService.EndCallAsync(new EndCallRequest
            {
                CallId = _store.CallId,
                CallType = InitiateCallRequest.Types.CallType.Standard
            });

            if (response.Success)
            {
                _store.CallStatus = CallStatus.None;
                _store.CallId = string.Empty;
            }
        }

        [RelayCommand]
        private async Task NavigateToSettings()
        {
            _store.SettingsButtonState = SideBarButtonState.Selected;
            
            if (_store.NavigationButtonState != SideBarButtonState.Disabled)
                _store.NavigationButtonState = SideBarButtonState.Enabled;

            if (_store.ReportButtonState != SideBarButtonState.Disabled)
                _store.ReportButtonState = SideBarButtonState.Enabled;

            if (_store.PhoneButtonState != SideBarButtonState.Disabled)
                _store.PhoneButtonState = SideBarButtonState.Enabled;

            //_navigationService.NavigateTo<>();
        }
    }
}
