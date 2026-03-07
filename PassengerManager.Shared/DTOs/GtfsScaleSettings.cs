using System;
using System.Collections.Generic;
using System.Text;

namespace PassengerManager.Shared.DTOs
{
    public record GtfsScaleSettings(
        int ChannelCapacity,
        int ArchiverBatchSize,
        int ArchiverMaxWaitSeconds, // overrides profile default
        int RedisTtlSeconds // overrides profile default
        );
}
