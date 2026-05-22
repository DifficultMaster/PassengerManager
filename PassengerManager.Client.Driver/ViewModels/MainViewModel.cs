using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassengerManager.Client.Core.Stores;
using Microsoft.Extensions.Logging;
using System;
using PassengerManager.Client.Core.ViewModels;
using PassengerManager.Client.Driver.Stores;

namespace PassengerManager.Client.Driver.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly NavigationStore _navigationStore;
        private readonly ILogger<MainViewModel> _logger;

        public BaseViewModel? CurrentViewModel => _navigationStore.CurrentViewModel;

        public SideBarStore SideBarStore { get; }

        public MainViewModel(NavigationStore navigationStore, SideBarStore sideBarStore, ILogger<MainViewModel> logger)
        {
            _navigationStore = navigationStore;
            SideBarStore = sideBarStore;
            _logger = logger;

            _navigationStore.CurrentViewModelChanged += OnCurrentViewModelChanged;
        }

        private void OnCurrentViewModelChanged()
        {
            OnPropertyChanged(nameof(CurrentViewModel));
        }
    }
}