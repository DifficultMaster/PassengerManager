using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassengerManager.Client.Core.Services.Interfaces;
using PassengerManager.Client.Core.Stores;
using PassengerManager.Client.Core.ViewModels;
using PassengerManager.Shared.Protos;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using PassengerManager.Client.Driver.Stores;

namespace PassengerManager.Client.Driver.ViewModels.Dashboard
{
    public partial class RouteSelectionViewModel : BaseViewModel
    {
        private readonly DriverOpsService.DriverOpsServiceClient _driverOpsClient;
        private readonly ManifestStore _manifestStore;

        [ObservableProperty]
        private ObservableCollection<RouteOption> _routes = new();

        [ObservableProperty]
        private bool _isLoading;

        public RouteSelectionViewModel(
            DriverOpsService.DriverOpsServiceClient driverOpsClient,
            INavigationService navigationService,
            DriverAccountStore driverAccountStore,
            ManifestStore manifestStore) : base(navigationService, driverAccountStore)
        {
            _driverOpsClient = driverOpsClient;
            _manifestStore = manifestStore;
           
            _ = LoadManifestAsync();
        }

        private async Task LoadManifestAsync()
        {
            IsLoading = true;
            try
            {
                var response = await _driverOpsClient.GetManifestAsync(new GetManifestRequest());
                if (response.Success)
                {
                    _manifestStore.AvailableRoutes = new List<RouteOption>(response.Routes.OrderBy(r => r.ShortName));
                    Routes = new ObservableCollection<RouteOption>(_manifestStore.AvailableRoutes);
                }
                else throw new AccessViolationException("Failed to retrieve manifest.");
            }
            catch (Exception ex)
            {
                if (MessageBox.Show("Failed to retrieve manifest.", "Error", MessageBoxButton.RetryCancel, MessageBoxImage.Error) == 
                    MessageBoxResult.Retry)
                {
                    await LoadManifestAsync();
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void SelectRoute(RouteOption selectedRoute)
        {
            if (selectedRoute == null)
                return;

            _manifestStore.SelectedRoute = selectedRoute;
            NavigationService.NavigateTo<TripSelectionViewModel>();
        }
    }
}
