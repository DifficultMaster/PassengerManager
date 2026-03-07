using PassengerManager.Shared.DTOs;
using System.Threading.Channels;

namespace PassengerManager.Server.Services.Background
{
    public class TelemetryChannels
    {
        public Channel<VehiclePositionDto> VehicleChannel { get; }

        public Channel<TripUpdateDto> TripChannel { get; }

        public Channel<ServiceAlertDto> AlertChannel { get; }

        public TelemetryChannels(GtfsScaleSettings settings)
        {
            BoundedChannelOptions options = new BoundedChannelOptions(settings.ChannelCapacity)
            { 
                FullMode = BoundedChannelFullMode.DropOldest
            };

            VehicleChannel = Channel.CreateBounded<VehiclePositionDto>(options);
            TripChannel = Channel.CreateBounded<TripUpdateDto>(options);
            AlertChannel = Channel.CreateBounded<ServiceAlertDto>(options);
        }
    }
}
