using System;
using System.Collections.Generic;
using System.Text;

namespace PassengerManager.Shared.DTOs
{
    public class ServiceAlertDto
    {
        public string AlertId { get; set; } = string.Empty;
        public string? AgencyId { get; set; }
        public string? RouteId { get; set; }
        public string? StopId { get; set; }
        public string? HeaderText { get; set; }
        public string? DescriptionText { get; set; }
        public int? Cause { get; set; }
        public int? Effect { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool IsActive { get; set; }
    }
}
