using PassengerManager.Shared.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace PassengerManager.Client.Core.Stores
{
    public class AccountStore
    {
        public User? CurrentUser { get; private set; }

        public event Action? CurrentUserChanged;

        public void Login(User user)
        {
            CurrentUser = user;
            CurrentUserChanged?.Invoke();
        }

        public void Logout()
        {
            CurrentUser = null;
            CurrentUserChanged?.Invoke();
        }

        public bool IsLoggedIn => CurrentUser != null;
    }
}
