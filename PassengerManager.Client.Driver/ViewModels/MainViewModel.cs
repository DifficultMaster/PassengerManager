using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassengerManager.Client.Core.Stores;
using Microsoft.Extensions.Logging;
using System;
using PassengerManager.Client.Core.ViewModels;

namespace PassengerManager.Client.Driver.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly NavigationStore _navigationStore;
        private readonly ILogger<MainViewModel> _logger;
        private ToolbarMode _currentToolbarMode;

        public BaseViewModel? CurrentViewModel => _navigationStore.CurrentViewModel;

        public MainViewModel(NavigationStore navigationStore, ILogger<MainViewModel> logger)
        {
            _navigationStore = navigationStore;
            _logger = logger;
            _navigationStore.CurrentViewModelChanged += OnCurrentViewModelChanged;
            _currentToolbarMode = ToolbarMode.Floating;
        }

        public enum ToolbarMode
        {
            Floating,
            Docked
        }

        public ToolbarMode CurrentToolbarMode
        {
            get => _currentToolbarMode;
            set
            {
                _currentToolbarMode = value;
                OnPropertyChanged(nameof(CurrentToolbarMode));
            }
        }

        [RelayCommand]
        private void EmergencySos()
        {
            _logger.LogWarning("SOS button pressed - initiating emergency protocol");
            // TODO: Implement emergency SOS logic
            // - Send emergency signal to server
            // - Show emergency UI overlay
            // - Trigger emergency broadcast notification
        }

        private void OnCurrentViewModelChanged()
        {
            OnPropertyChanged(nameof(CurrentViewModel));
        }
    }
}