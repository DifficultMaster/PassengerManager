using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PassengerManager.Client.Core.Stores;
using System;
using PassengerManager.Client.Core.ViewModels;
using PassengerManager.Client.Driver.Stores;
using PassengerManager.Client.Driver.ViewModels.Overlay;

namespace PassengerManager.Client.Driver.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly NavigationStore _navigationStore;
        private readonly ILogger<MainViewModel> _logger;

        public BaseViewModel? CurrentViewModel => _navigationStore.CurrentViewModel;

        public SideBarStore SideBarStore { get; }
        public SideBarViewModel SideBarViewModel { get; }
        public StatusBarViewModel StatusBarViewModel { get; }

        public MainViewModel(
            NavigationStore navigationStore,
            SideBarStore sideBarStore,
            SideBarViewModel sideBarViewModel,
            StatusBarViewModel statusBarViewModel,
            ILogger<MainViewModel> logger)
        {
            _navigationStore = navigationStore;
            SideBarStore = sideBarStore;
            SideBarViewModel = sideBarViewModel;
            StatusBarViewModel = statusBarViewModel;
            _logger = logger;

            _navigationStore.CurrentViewModelChanged += OnCurrentViewModelChanged;
        }

        private void OnCurrentViewModelChanged()
        {
            OnPropertyChanged(nameof(CurrentViewModel));
        }
    }
}