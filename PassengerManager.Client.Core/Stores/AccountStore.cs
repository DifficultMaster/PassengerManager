using PassengerManager.Shared.Models;
using PassengerManager.Shared.Protos;
using System;
using System.Collections.Generic;
using System.Text;

namespace PassengerManager.Client.Core.Stores
{
    public abstract class AccountStore
    {
        public string Token { get; protected set; } = string.Empty;

        public string DisplayName { get; protected set; } = string.Empty;        

        public bool IsLoggedIn => !string.IsNullOrEmpty(Token);

        public event Action? StateChanged;

        public virtual void Logout()
        {
            Token = string.Empty;
            DisplayName = string.Empty;
            InvokeStateChanged();
        }

        protected void InvokeStateChanged() => StateChanged?.Invoke();
    }
}
