using PassengerManager.Server.Models;
using PassengerManager.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace PassengerManager.Server.Services
{
    public class TransportService : PassengerManager.Shared.Protos.TransportService.TransportServiceBase
    {
        private readonly ILogger<TransportService> _logger;
        private readonly PassengerManagerContext _context;

        public TransportService(ILogger<TransportService> logger, PassengerManagerContext context)
        {
            _logger = logger;
            _context = context;
        }

       
    }
}
