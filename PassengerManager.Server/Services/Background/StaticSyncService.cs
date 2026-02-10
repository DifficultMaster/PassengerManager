using CsvHelper;
using CsvHelper.Configuration;
using PassengerManager.Server.Models;
using PassengerManager.Shared.Models;
using System.Globalization;
using System.IO.Compression;

namespace PassengerManager.Server.Services.Background
{
    public class StaticSyncService : BackgroundService
    {
        private const int DEFAULT_STATIC_SYNC_INTERVAL_HOURS = 24;
        //private const int DEFAULT_VEHICLE_SYNC_INTERVAL_SECONDS = 5;
        //private const int DEFAULT_TRIP_SYNC_INTERVAL_SECONDS = 15;

        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<StaticSyncService> _logger;
        private readonly HttpClient _http;

        public StaticSyncService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<StaticSyncService> logger, IHttpClientFactory httpClientFactory)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
            _http = new HttpClient();
        }

        private async Task ProcessCsv<TEntity, TMap>(ZipArchive archive, string fileName, PassengerManagerContext context,
            Func<TEntity, PassengerManagerContext, Task> addLogic)
            where TEntity : class
            where TMap : ClassMap<TEntity>
        {
            ZipArchiveEntry? entry = archive.GetEntry(fileName);
            if (entry == null) return;

            using CsvReader csv = new CsvReader(new StreamReader(entry.Open()), culture: CultureInfo.InvariantCulture);
            csv.Context.RegisterClassMap<TMap>();
            IEnumerable<TEntity> records = csv.GetRecords<TEntity>();

            foreach (var r in records)
            {
                await addLogic(r, context);
            }

            await context.SaveChangesAsync();
        }

        protected override async Task ExecuteAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_configuration.GetValue<bool>("GtfsSettings:AutoSyncEnabled", false))
                {
                    await RunSync();
                }

                await Task.Delay(TimeSpan.FromHours(_configuration.GetValue<int>("GtfsSettings:SyncIntervalHours", DEFAULT_STATIC_SYNC_INTERVAL_HOURS)), token);
            }
        }

        public async Task RunSync()
        {
            _logger.LogInformation("Starting GTFS static data sync...");

            try
            {
                using Stream stream = await _http.GetStreamAsync(_configuration.GetValue<string>("GtfsSettings:StaticDataUrl"));
                using ZipArchive archive = new ZipArchive(stream);

                using IServiceScope scope = _serviceProvider.CreateScope();
                PassengerManagerContext db = scope.ServiceProvider.GetRequiredService<PassengerManagerContext>();

                using var transaction = await db.Database.BeginTransactionAsync();

                ZipArchiveEntry? agencyEntry = archive.GetEntry("agency.txt");
                if (agencyEntry != null)
                {
                    using CsvReader reader = new CsvReader(new StreamReader(agencyEntry.Open()), culture: CultureInfo.InvariantCulture);
                    List<Agency> records = reader.GetRecords<Shared.Models.Agency>().ToList();
                    foreach (Shared.Models.Agency a in records)
                    {
                        if (await db.Agencies.FindAsync(a.AgencyId) == null)
                        {
                            db.Agencies.Add(new Agency);
                        }
                    }

                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex) 
            {
                
            }
        }            
    }
}
