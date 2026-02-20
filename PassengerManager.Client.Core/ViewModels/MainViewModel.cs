using CommunityToolkit.Mvvm.ComponentModel;
using PassengerManager.Client.Core.Stores;
using System;
using System.Collections.Generic;
using System.Text;

namespace PassengerManager.Client.Core.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly NavigationStore _navigationStore;

        public BaseViewModel? CurrentViewModel => _navigationStore.CurrentViewModel;

        public MainViewModel(NavigationStore navigationStore)
        {
            _navigationStore = navigationStore;
            _navigationStore.CurrentViewModelChanged += OnCurrentViewModelChanged;
        }

        private void OnCurrentViewModelChanged()
        {
            OnPropertyChanged(nameof(CurrentViewModel)); 
        }
    }
}
