using PassengerManager.Shared.Protos;

namespace PassengerManager.Client.Core.Stores
{
    /// <summary>
    /// Manages the hardware device authentication state.
    /// This is separate from driver login and is used for continuous telemetry.
    /// </summary>
    public class HardwareAccountStore : AccountStore
    {
        public string VehicleId { get; private set; } = string.Empty;

        /// <summary>
        /// Updates the hardware login state with a successful authentication.
        /// </summary>
        public void Login(HardwareLoginResponse response, string vehicleId)
        {
            Token = response.Token;
            VehicleId = vehicleId;
            DisplayName = $"Hardware_{vehicleId}";
            InvokeStateChanged();
        }

        public override void Logout()
        {
            VehicleId = string.Empty;
            base.Logout();
        }
    }
}
