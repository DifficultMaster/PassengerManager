using System;
using System.Collections.Generic;
using System.Text;

namespace PassengerManager.Client.Driver.Stores
{
    public enum ConnectionLevel
    {
        High,
        Medium,
        Low,
        None
    }

    public enum DrivingHoursState
    {
        Normal,
        Warning,
        Prohibited
    }

    public enum CallStatus
    {
        Live,
        Outgoing,
        None
    }

    public enum SideBarButtonState
    {
        Selected,
        Enabled,
        Disabled
    }
}
