using System;
using System.Collections.Generic;
using System.Text;

namespace PassengerManager.Shared.DTOs
{
    public record RouteOptionDto(
        string RouteId,
        string ShortName,
        string? LongName,
        List<TripOptionDto> Trips
    );

    public record TripOptionDto(
        string TripId,
        string Headsign,
        int DirectionId
    );
}
