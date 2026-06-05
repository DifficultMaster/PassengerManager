using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using PassengerManager.Client.Core.Services.Interfaces;
using PassengerManager.Client.Core.Stores;
using PassengerManager.Client.Core.ViewModels;
using PassengerManager.Shared.Protos;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using PassengerManager.Client.Driver.Resources;
using PassengerManager.Client.Core.Resources;
using PassengerManager.Client.Driver.ViewModels.Dashboard;
using PassengerManager.Client.Driver.Views.Dashboard;

namespace PassengerManager.Client.Driver.ViewModels
{
    public partial class DriverLoginViewModel : BaseViewModel
    {
        private readonly IAuthService _authService;
        private readonly IAuthErrorTranslator _authErrorTranslator;
        private readonly string _vehicleId;

        private readonly int _idLength;
        private readonly int _pinLength;

        private string _tempToken = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentInputLength))]
        [NotifyPropertyChangedFor(nameof(InputCircles))]
        [NotifyPropertyChangedFor(nameof(PromptText))]
        private DriverLoginState _currentState = DriverLoginState.EnteringId;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentInputLength))]
        [NotifyPropertyChangedFor(nameof(InputCircles))]
        private string _driverId = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentInputLength))]
        [NotifyPropertyChangedFor(nameof(InputCircles))]
        private string _pin = string.Empty;
      
        private string _oldPin = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentInputLength))]
        [NotifyPropertyChangedFor(nameof(InputCircles))]
        private string _newPin = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentInputLength))]
        [NotifyPropertyChangedFor(nameof(InputCircles))]
        private string _confirmPin = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasError))]
        [NotifyPropertyChangedFor(nameof(IsNumpadEnabled))]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNumpadEnabled))]
        private bool _isLoading = false;

        public enum DriverLoginState
        { 
            EnteringId,
            EnteringPin,
            EnteringNewPin,
            ConfirmingNewPin
        }

        public DriverLoginViewModel(
            INavigationService navigationService, 
            DriverAccountStore accountStore,
            IAuthService authService,
            IConfiguration config,
            IAuthErrorTranslator authErrorTranslator) : base(navigationService, accountStore)
        {
            _authService = authService;
            _authErrorTranslator = authErrorTranslator;
            _vehicleId = config["TerminalSettings:VehicleId"] ?? "UNKNOWN";

            if (int.TryParse(config["AuthDefaults:Terminal:DefaultIdLength"] ?? "4", out int idLength))
                _idLength = idLength;
            else
                _idLength = 4;

            if (int.TryParse(config["AuthDefaults:Terminal:DefaultPasswordLength"] ?? "8", out int pinLength))
                _pinLength = pinLength;
            else
                _pinLength = 8;               
        }

        public IEnumerable<bool> InputCircles
        {
            get
            {
                int targetLength = CurrentState == DriverLoginState.EnteringId ? _idLength : _pinLength;
                int currentLength = CurrentInputLength;

                List<bool> circles = new List<bool>();
                for (int i = 0; i < targetLength; i++)
                {
                    circles.Add(i < currentLength);
                }

                return circles;
            }
        }

        public int CurrentInputLength
        {
            get
            {
                switch (CurrentState)
                {
                    case DriverLoginState.EnteringId:
                        return DriverId.Length;

                    case DriverLoginState.EnteringPin:
                        return Pin.Length;                   

                    case DriverLoginState.EnteringNewPin:
                        return NewPin.Length;

                    case DriverLoginState.ConfirmingNewPin:
                        return ConfirmPin.Length;

                    default:
                        return 0;
                }
            }
        }

        public string PromptText
        {
            get
            {
                switch (CurrentState)
                {
                    case DriverLoginState.EnteringId:
                        return UIStrings.PromptEnterId;

                    case DriverLoginState.EnteringPin:
                        return UIStrings.PromptEnterPin;                    

                    case DriverLoginState.EnteringNewPin:
                        return UIStrings.PromptEnterNewPin;

                    case DriverLoginState.ConfirmingNewPin:
                        return UIStrings.PromptConfirmNewPin;

                    default:
                        return string.Empty;
                }
            }
        }

        public bool HasError
        {
            get
            {
                return !string.IsNullOrEmpty(ErrorMessage);
            }
        }

        public bool IsNumpadEnabled
        {
            get
            {
                return (!HasError && !IsLoading);
            }
        }                 

        [RelayCommand]
        private async Task AddDigitAsync(string digit)
        {
            if (IsLoading || HasError) return;

            ErrorMessage = string.Empty;

            switch (CurrentState)
            {
                case DriverLoginState.EnteringId:
                    {
                        if (DriverId.Length < _idLength)
                            DriverId += digit;

                        if (DriverId.Length == _idLength)
                            CurrentState = DriverLoginState.EnteringPin; 
                        
                        break;
                    }

                case DriverLoginState.EnteringPin:
                    {
                        if (Pin.Length < _pinLength)
                            Pin += digit;

                        if (Pin.Length == _pinLength)
                            await PerformLoginAsync();

                        break;
                    }               

                case DriverLoginState.EnteringNewPin:
                    {
                        if (NewPin.Length < _pinLength)
                            NewPin += digit;

                        if (NewPin.Length == _pinLength)
                            CurrentState = DriverLoginState.ConfirmingNewPin;

                        break;
                    }

                case DriverLoginState.ConfirmingNewPin:
                    {
                        if (ConfirmPin.Length < _pinLength)
                            ConfirmPin += digit;

                        if (ConfirmPin.Length == _pinLength)
                        {
                            if (NewPin == ConfirmPin)
                                await PerformPasswordChangeAsync();
                            else
                            {
                                ErrorMessage = AuthErrors.InvalidNewPassword;

                                NewPin = string.Empty;
                                ConfirmPin = string.Empty;
                                CurrentState = DriverLoginState.EnteringNewPin;
                            }
                        }

                        break;
                    }
            }
        }

        [RelayCommand]
        private async Task ClearDigitsAsync()
        {
            if (IsLoading || HasError) return;

            ErrorMessage = string.Empty;

            switch (CurrentState)
            {
                case DriverLoginState.EnteringId:
                    {
                        if (DriverId.Length > 0)
                            DriverId = string.Empty;

                        break;
                    }

                case DriverLoginState.EnteringPin:
                    {
                        if (Pin.Length > 0)
                            Pin = string.Empty;

                        break;
                    }

                case DriverLoginState.EnteringNewPin:
                    {
                        if (NewPin.Length > 0)
                            NewPin = string.Empty;

                        break;
                    }

                case DriverLoginState.ConfirmingNewPin:
                    {
                        if (ConfirmPin.Length > 0)
                            ConfirmPin = string.Empty;

                        break;
                    }
            }
        }

        [RelayCommand]
        private async Task RemoveDigitAsync()
        {
            if (IsLoading || HasError) return;

            ErrorMessage = string.Empty;

            switch (CurrentState)
            {
                case DriverLoginState.EnteringId:
                    {
                        if (DriverId.Length > 0)
                            DriverId = DriverId.Substring(0, DriverId.Length - 1);

                        break;
                    }

                case DriverLoginState.EnteringPin:
                    {
                        if (Pin.Length > 0)
                            Pin = Pin.Substring(0, Pin.Length - 1);
                        else
                            CurrentState = DriverLoginState.EnteringId;

                        break;
                    }                

                case DriverLoginState.EnteringNewPin:
                    {
                        if (NewPin.Length > 0)
                            NewPin = NewPin.Substring(0, NewPin.Length - 1);
                        else
                        {
                            CurrentState = DriverLoginState.EnteringId;

                            DriverId = string.Empty;
                            Pin = string.Empty;
                            _tempToken = string.Empty;
                        }

                        break;
                    }

                case DriverLoginState.ConfirmingNewPin:
                    {
                        if (ConfirmPin.Length > 0)
                            ConfirmPin = ConfirmPin.Substring(0, ConfirmPin.Length - 1);
                        else
                            CurrentState = DriverLoginState.EnteringNewPin;

                        break;
                    }
            }
        }

        [RelayCommand]
        private void DismissError()
        {
            ErrorMessage = string.Empty;
        }

        private async Task PerformLoginAsync()
        {
            IsLoading = true;

            DriverLoginRequest request = new DriverLoginRequest
            {
                UserId = DriverId,
                Pin = Pin,
                VehicleId = _vehicleId
            };

            try
            {
                DriverLoginResponse response = await _authService.AuthenticateDriverAsync(request);

                if (response.Success)
                {
                    DriverAccountStore driverStore = (DriverAccountStore)AccountStore;
                    driverStore.Login(response);

                    NavigationService.NavigateTo<RouteSelectionViewModel>();
                }
                else if (response.Code == AuthResultCode.CredentialOverdue)
                {
                    _oldPin = Pin;
                    _tempToken = response.Token;

                    ErrorMessage = _authErrorTranslator.Translate(response.Code) + ".";
                    CurrentState = DriverLoginState.EnteringNewPin;

                    Pin = string.Empty;
                    NewPin = string.Empty;
                    ConfirmPin = string.Empty;
                }
                else
                {
                    ErrorMessage = _authErrorTranslator.Translate(response.Code) + ".";
                    CurrentState = DriverLoginState.EnteringId;

                    DriverId = string.Empty;
                    Pin = string.Empty;
                }
            }
            catch (Exception e)
            {
                ErrorMessage = $"Network error. Cannot reach the server. Error: {e.Message}";
                CurrentState = DriverLoginState.EnteringId;

                DriverId = string.Empty;
                Pin = string.Empty;               
            }

            IsLoading = false;
        }

        private async Task PerformPasswordChangeAsync()
        {
            IsLoading = true;

            PasswordChangeRequest request = new PasswordChangeRequest
            {
                CurrentPassword = _oldPin,
                NewPassword = ConfirmPin,
            };

            try
            {
                PasswordChangeResponse response = await _authService.ChangeDriverPasswordAsync(request, _tempToken);

                if (response.Success)
                {
                    _tempToken = string.Empty;

                    Pin = ConfirmPin;
                    _oldPin = string.Empty;
                    NewPin = string.Empty;
                    ConfirmPin = string.Empty;

                    CurrentState = DriverLoginState.EnteringPin;
                    await PerformLoginAsync();
                }
                else
                {
                    ErrorMessage = _authErrorTranslator.Translate(response.Code);
                    CurrentState = DriverLoginState.EnteringNewPin;

                    NewPin = string.Empty;
                    ConfirmPin = string.Empty;
                }
            }
            catch
            {
                ErrorMessage = "Network error. Canot reach the server.";
                CurrentState = DriverLoginState.EnteringNewPin;

                NewPin = string.Empty;
                ConfirmPin = string.Empty;
            }

            IsLoading = false;
        }
    }
}
