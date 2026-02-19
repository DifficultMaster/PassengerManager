using CommunityToolkit.Mvvm.ComponentModel;
using PassengerManager.Client.Core.Services.Interfaces;
using PassengerManager.Client.Core.Stores;
using System;
using System.Collections.Generic;
using System.Text;

namespace PassengerManager.Client.Core.ViewModels
{
    public abstract class BaseViewModel : ObservableObject
    {
        protected readonly INavigationService NavigationService;
        protected readonly AccountStore AccountStore;

        protected BaseViewModel(
            INavigationService navigationService, 
            AccountStore accountStore)
        {
            NavigationService = navigationService;
            AccountStore = accountStore;
        }
    }
}
