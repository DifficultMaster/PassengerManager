using Microsoft.Extensions.DependencyInjection;
using PassengerManager.Client.Core.Services.Interfaces;
using PassengerManager.Client.Core.Stores;
using PassengerManager.Client.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace PassengerManager.Client.Core.Services
{
    public class AppNavigationService : INavigationService
    {
        private readonly NavigationStore _navigationStore;
        private readonly IServiceProvider _serviceProvider;

        public AppNavigationService(NavigationStore navigationStore, IServiceProvider serviceProvider)
        {
            _navigationStore = navigationStore;
            _serviceProvider = serviceProvider;
        }

        public void NavigateTo<TViewModel>() where TViewModel : class
        {
            TViewModel viewModel = _serviceProvider.GetRequiredService<TViewModel>();
            _navigationStore.CurrentViewModel = viewModel as BaseViewModel;
        }
    }
}
