using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using PassengerManager.Client.Core.Services.Interfaces;
using PassengerManager.Client.Core.Stores;
using PassengerManager.Shared.Protos;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace PassengerManager.Client.Core.ViewModels
{
    public partial class DriverLoginViewModel : BaseViewModel
    {
        private readonly IAuthService _authService;
        private readonly IAuthErrorTranslator _authErrorTranslator;
        private readonly string _vehicleId;

        private readonly int _idLength;
        private readonly int _pinLength;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentInputLength))]
        private string _driverId = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentInputLength))]
        private string _pin = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentInputLength))]
        private bool _isEnteringPin = false;

        [ObservableProperty]
        private bool _isLoading = false;

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

        public int CurrentInputLength
        {
            get
            {
                return IsEnteringPin ? Pin.Length : DriverId.Length;
            }
        }

        public bool HasError
        {
            get
            {
                return !string.IsNullOrEmpty(ErrorMessage);
            }
        }

        [RelayCommand]
        private async Task AddDigitAsync(string digit)
        {
            if (IsLoading) return;

            ErrorMessage = string.Empty;

            if (!IsEnteringPin)
            {
                if (DriverId.Length < _idLength)
                {
                    DriverId += digit;
                }

                if (DriverId.Length == _idLength)
                {
                    IsEnteringPin = true;
                }
            }
            else
            {
                if (Pin.Length < _pinLength)
                {
                    Pin += digit;
                }

                if (Pin.Length == _pinLength)
                {
                    await PerformLoginAsync();
                }
            }
        }

        [RelayCommand]
        private async Task RemoveDigitAsync()
        {
            if (IsLoading) return;

            ErrorMessage = string.Empty;

            if (!IsEnteringPin)
            {
                if (DriverId.Length > 0)
                {
                    DriverId = DriverId.Substring(0, DriverId.Length - 1);
                }                
            }
            else
            {
                if (Pin.Length > 0)
                {
                    Pin = Pin.Substring(0, Pin.Length - 1);
                }
                else
                {
                    IsEnteringPin = false;
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

                    //NavigationService.NavigateTo<DriverDashboardViewModel>();
                }
                else
                {
                    ErrorMessage = response.Message;

                    DriverId = string.Empty;
                    Pin = string.Empty;
                    IsEnteringPin = false;
                }
            }
            catch
            {
                ErrorMessage = "Network error. Canot reach the server.";

                DriverId = string.Empty;
                Pin = string.Empty;
                IsEnteringPin = false;
            }

            IsLoading = false;
        }
    }
}
