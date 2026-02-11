using CsvHelper;
using CsvHelper.Configuration;
using PassengerManager.Server.Models;
using static PassengerManager.Server.Services.Static.AppDefaults;
using PassengerManager.Shared.Models;
using System.Globalization;
using System.IO.Compression;
using PassengerManager.Server.Services.Static;
using PassengerManager.Server.Services.Maps;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace PassengerManager.Server.Services.Background
{
    public class StaticSyncService : BackgroundService
    {       
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<StaticSyncService> _logger;
        private readonly HttpClient _http;

        public StaticSyncService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<StaticSyncService> logger, IHttpClientFactory httpClientFactory)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
            _http = httpClientFactory.CreateClient();
        }

        private async Task ProcessCsvBulk<TEntity, TMap>(ZipArchive archive, string fileName, PassengerManagerContext context,
            Func<TEntity, PassengerManagerContext, Task> upsertLogic)
            where TEntity : class
            where TMap : ClassMap<TEntity>
        {
            ZipArchiveEntry? entry = archive.GetEntry(fileName);
            if (entry == null) return;

            using StreamReader reader = new StreamReader(entry.Open());
            using CsvReader csv = new CsvReader(reader, culture: CultureInfo.InvariantCulture);

            csv.Context.RegisterClassMap<TMap>();

            IEnumerable<TEntity> records = csv.GetRecords<TEntity>();
            foreach (TEntity r in records)
            {
                await upsertLogic(r, context);
            }

            await context.SaveChangesAsync();
        }

        private async Task ProcessShapes(ZipArchive archive, PassengerManagerContext context)
        {
            ZipArchiveEntry? entry = archive.GetEntry("shapes.txt");
            if (entry == null) return;

            using var reader = new StreamReader(entry.Open());
            using CsvReader csv = new CsvReader(reader, culture: CultureInfo.InvariantCulture);

            csv.Context.RegisterClassMap<ShapePointMap>();

            List<ShapePoint> allPoints = csv.GetRecords<ShapePoint>().ToList(); // RAM constraints?
            if (!allPoints.Any()) return;

            List<string> distinctShapeIds = allPoints.Select(p => p.ShapeId).Distinct().ToList();
            List<string> existingHeaders = await context.ShapeHeaders
                .Where(h => distinctShapeIds.Contains(h.ShapeId))
                .Select(h => h.ShapeId)
                .ToListAsync();

            HashSet<string> existingHeaderSet = new HashSet<string>(existingHeaders);
            List<ShapeHeader> newHeaders = new List<ShapeHeader>();

            foreach (string shapeId in distinctShapeIds)
            {
                if (!existingHeaderSet.Contains(shapeId))
                {
                    newHeaders.Add(new ShapeHeader
                    {
                        ShapeId = shapeId
                    });
                }
            }

            if (newHeaders.Any())
            {
                await context.ShapeHeaders.AddRangeAsync(newHeaders);
                await context.SaveChangesAsync();
            }

            HashSet<(string, int)> existingSignatures = new HashSet<(string, int)>();

            const int BatchSize = 1000;
            for (int i = 0; i < distinctShapeIds.Count; i += BatchSize)
            {
                List<string> batchIds = distinctShapeIds.Skip(i).Take(BatchSize).ToList();
                var batchSpecificExisting = await context.ShapePoints
                    .AsNoTracking()
                    .Where(p => batchIds.Contains(p.ShapeId))
                    .Select(p => new
                    {
                        p.ShapeId,
                        p.Sequence
                    })
                    .ToListAsync();

                foreach (var signature in batchSpecificExisting)
                {
                    existingSignatures.Add((signature.ShapeId, signature.Sequence));
                }
            }

            var newPoints = allPoints
                .Where(p => !existingSignatures.Contains((p.ShapeId, p.Sequence)))
                .ToList();

            if (newPoints.Any())
            {
                await context.ShapePoints.AddRangeAsync(newPoints);
                await context.SaveChangesAsync();
            }
        }

        protected override async Task ExecuteAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_configuration.GetValue<bool>("GtfsSettings:AutoSyncEnabled", false))
                {
                    await RunSync();
                }

                await Task.Delay(TimeSpan
                    .FromHours(_configuration.GetValue<int>("GtfsSettings:SyncIntervalHours", AppDefaults.Sync.StaticIntervalHours)), token);
            }
        }

        public async Task RunSync()
        {
            _logger.LogInformation("Starting GTFS static data sync...");

            ZipArchive? archive = null;
            Stream? stream = null;

            try
            {
                string url = _configuration.GetValue<string>("GtfsSettings:StaticDataUrl") ?? string.Empty;
                if (string.IsNullOrEmpty(url))
                {
                    _logger.LogError("GtfsSettings:StaticDataUrl is missing.");
                    return;
                }

                stream = await _http.GetStreamAsync(url);
                archive = new ZipArchive(stream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download GTFS zip. Aborting sync.");
                return;
            }

            using IServiceScope scope = _serviceProvider.CreateScope();
            PassengerManagerContext context = scope.ServiceProvider.GetRequiredService<PassengerManagerContext>();

            var strategy = context.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await context.Database.BeginTransactionAsync();

                try
                {
                    await ProcessCsvBulk<Agency, AgencyMap>(archive, "agency.txt", context, async (record, context) =>
                    {
                        if (await context.Agencies.FindAsync(record.AgencyId) == null)
                            context.Agencies.Add(record);
                    });
                }
                catch ()
                {

                }
            });
        }            
    }
}
