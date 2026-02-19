using System;
using System.Collections.Generic;
using System.Text;

namespace PassengerManager.Client.Core.Services.Interfaces
{
    public interface INavigationService
    {
        void NavigateTo<TViewModel>() where TViewModel : class;
    }
}
