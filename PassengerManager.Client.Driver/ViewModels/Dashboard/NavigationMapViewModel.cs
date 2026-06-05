using System;
using System.Collections.Generic;
using System.Text;
using PassengerManager.Client.Core.Services.Interfaces;
using PassengerManager.Client.Core.Stores;
using PassengerManager.Client.Core.ViewModels;

namespace PassengerManager.Client.Driver.ViewModels.Dashboard
{
    public partial class NavigationMapViewModel : BaseViewModel
    {
        private readonly DriverAccountStore _driverAccountStore;

        public NavigationMapViewModel(INavigationService navigationService, DriverAccountStore driverAccountStore) : base(navigationService, driverAccountStore)
        {
            _driverAccountStore = driverAccountStore;
            //_driverAccountStore.StateChanged += OnDriverStateChanged;
        }


    }
}
