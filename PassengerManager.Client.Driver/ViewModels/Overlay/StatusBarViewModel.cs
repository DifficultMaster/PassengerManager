using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using PassengerManager.Client.Driver.Stores;

namespace PassengerManager.Client.Driver.ViewModels.Overlay
{
    public partial class StatusBarViewModel : ObservableObject
    {
        private readonly StatusBarStore _store;

        public StatusBarStore Store
        {
            get => _store;
        }

        public StatusBarViewModel(StatusBarStore store)
        {
            _store = store;
        }
    }
}
