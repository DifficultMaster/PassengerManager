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

        public string? CurrentRouteId { get; private set; }

        public string? CurrentTripId { get; private set; }

        public void Login(DriverLoginResponse response)
        {
            Token = response.Token;
            DisplayName = response.DriverName;
            CurrentShiftId = response.ShiftId;
            LoginTimeUtc = DateTime.UtcNow;

            InvokeStateChanged();
        }

        public void SetActiveTrip(string? routeId, string? tripId)
        {
            CurrentRouteId = routeId;
            CurrentTripId = tripId;

            InvokeStateChanged();
        }

        public override void Logout()
        {
            CurrentShiftId = 0;
            LoginTimeUtc = DateTime.MinValue;

            CurrentRouteId = null;
            CurrentTripId = null;

            base.Logout();
        }
    }
}
