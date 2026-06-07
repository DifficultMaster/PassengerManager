using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassengerManager.Client.Core.Services.Interfaces;
using PassengerManager.Client.Core.Stores;
using PassengerManager.Client.Driver.Stores;
using PassengerManager.Shared.Protos;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Navigation;
using PassengerManager.Client.Core.ViewModels;
using PassengerManager.Client.Driver.Resources;

namespace PassengerManager.Client.Driver.ViewModels.Dashboard
{
    public partial class TripSelectionViewModel : BaseViewModel, IDisposable
    {
        private readonly DriverAccountStore _driverAccountStore;
        private readonly ManifestStore _manifestStore;

        [ObservableProperty]
        private ObservableCollection<TripOption> _trips = new();

        [ObservableProperty]
        private string _routeTitle = string.Empty;

        public string GoBackButtonText
        {
            get => UIStrings.ButtonGoBackToRouteSelection;
        }

        public TripSelectionViewModel(           
            INavigationService navigationService,
            DriverAccountStore driverAccountStore,
            ManifestStore manifestStore) : base(navigationService, driverAccountStore)
        {
            _driverAccountStore = driverAccountStore;
            _manifestStore = manifestStore;

            LoadTrips();

            _manifestStore.OnSelectedRouteChanged += LoadTrips;
        }

        public void Dispose()
        {
            _manifestStore.OnSelectedRouteChanged -= LoadTrips;
        }

        private void LoadTrips()
        {
            if (_manifestStore.SelectedRoute != null)
            {
                RouteTitle = _manifestStore.SelectedRoute.ShortName;
                Trips = new ObservableCollection<TripOption>(_manifestStore.SelectedRoute.Trips.OrderBy(t => t.Headsign));
            }          
        }

        [RelayCommand]
        private void SelectTrip(TripOption selectedTrip)
        {
            if (selectedTrip == null || _manifestStore.SelectedRoute == null)
                return;

            _driverAccountStore.SetActiveTrip(
                _manifestStore.SelectedRoute.RouteId,
                selectedTrip.TripId);

            NavigationService.NavigateTo<NavigationMapViewModel>();
        }

        [RelayCommand]
        private void GoBack()
        {
            NavigationService.NavigateTo<RouteSelectionViewModel>();
        }
    }
}
