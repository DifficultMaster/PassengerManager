using PassengerManager.Shared.Models;
using PassengerManager.Shared.Protos;
using System;
using System.Collections.Generic;
using System.Text;

namespace PassengerManager.Client.Core.Stores
{
    public class DriverAccountStore : AccountStore
    {
        public long CurrentShiftId { get; private set; }

        public DateTime LoginTimeUtc { get; private set; } = DateTime.MinValue;

        public void Login(DriverLoginResponse response)
        {
            Token = response.Token;
            DisplayName = response.DriverName;
            CurrentShiftId = response.ShiftId;
            LoginTimeUtc = DateTime.UtcNow;

            InvokeStateChanged();
        }

        public override void Logout()
        {
            CurrentShiftId = 0;
            LoginTimeUtc = DateTime.MinValue;
            base.Logout();
        }
    }
}
