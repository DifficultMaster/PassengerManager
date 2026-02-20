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

        public IReadOnlyList<string> AvailableRoutes { get; private set; } = new List<string>();

        public void Login(DriverLoginResponse response)
        {
            Token = response.Token;
            DisplayName = response.DriverName;

            CurrentShiftId = response.ShiftId;
            AvailableRoutes = response.AvailableRoutes.ToList();

            InvokeStateChanged();
        }

        public override void Logout()
        {
            CurrentShiftId = 0;
            AvailableRoutes = new List<string>();
            base.Logout();
        }
    }
}
