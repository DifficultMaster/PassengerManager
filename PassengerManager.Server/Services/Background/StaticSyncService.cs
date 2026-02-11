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
using System.Linq.Expressions;

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

        private async Task ImportBulkUpsert<TEntity, TMap>(ZipArchive archive, string fileName, PassengerManagerContext context,
            Expression<Func<TEntity, string>> keySelector)
            where TEntity : class
            where TMap : ClassMap<TEntity>
        {
            ZipArchiveEntry? entry = archive.GetEntry(fileName);
            if (entry == null) return;

            using StreamReader reader = new StreamReader(entry.Open());
            using CsvReader csv = new CsvReader(reader, culture: CultureInfo.InvariantCulture);

            csv.Context.RegisterClassMap<TMap>();

            List<TEntity> csvRecords = csv.GetRecords<TEntity>().ToList();
            if (!csvRecords.Any()) return;

            Func<TEntity, string> getCsvKey = keySelector.Compile();

            List<string> existingKeys = await context.Set<TEntity>()
                .AsNoTracking()
                .Select(keySelector)
                .ToListAsync();

            HashSet<string> existingKeySet = new HashSet<string>(existingKeys);

            List<TEntity> toInsert = new List<TEntity>();
            List<TEntity> toUpdate = new List<TEntity>();

            foreach (TEntity record in csvRecords)
            {
                if (existingKeySet.Contains(getCsvKey(record)))
                {
                    toUpdate.Add(record);
                }
                else
                {
                    toInsert.Add(record);
                }
            }

            if (toInsert.Any())
            {
                await context.Set<TEntity>().AddRangeAsync(toInsert);
            }

            foreach (TEntity record in toUpdate)
            {
                string key = getCsvKey(record);
                TEntity? entity = await context.Set<TEntity>().FindAsync(key);

                if (entity != null)
                {
                    context.Entry(entity).CurrentValues.SetValues(record);
                }
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

            List<ShapePoint> allPoints = csv.GetRecords<ShapePoint>().ToList(); // Test RAM performance
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

            const int BatchSize = 2000; //
            for (int i = 0; i < distinctShapeIds.Count; i += BatchSize)
            {
                List<string> batchIds = distinctShapeIds.Skip(i).Take(BatchSize).ToList();
                var oldPoints = await context.ShapePoints
                    .Where(p => batchIds.Contains(p.ShapeId))
                    .ToListAsync();

                if (oldPoints.Any())
                {
                    context.ShapePoints.RemoveRange(oldPoints);
                }    
            }        

            await context.ShapePoints.AddRangeAsync(allPoints);
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

                await Task.Delay(TimeSpan
                    .FromHours(_configuration.GetValue<int>("GtfsSettings:SyncIntervalHours", AppDefaults.Sync.StaticIntervalHours)), token);
            }
        }

        public async Task RunSync()
        {
            _logger.LogInformation("Starting GTFS static data sync...");

            ZipArchive? archive = null;
            Stream? stream = null;

            int attempts = 0;
            bool downloadSuccess = false;

            while (!downloadSuccess && attempts < TimeoutDefaults.StaticData.MaxRetries)

            try
            {
                attempts++;

                using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutDefaults.StaticData.DownloadTimeoutSeconds));

                string url = _configuration.GetValue<string>("GtfsSettings:StaticDataUrl") ?? string.Empty;
                if (string.IsNullOrEmpty(url))
                {
                    _logger.LogError("GtfsSettings:StaticDataUrl is missing.");
                    return;
                }

                stream = await _http.GetStreamAsync(url, cts.Token);
                archive = new ZipArchive(stream);

                downloadSuccess = true;
            }
            catch (Exception ex)
            {
                stream?.Dispose();
                stream = null;

                if (attempts >= TimeoutDefaults.StaticData.MaxRetries)
                {
                    _logger.LogError(ex, $"Failed to download GTFS static data after {attempts} attempts. Aborting sync.");
                    return;
                }

                _logger.LogError(ex, $"Failed to download GTFS static data. Retrying in {TimeoutDefaults.StaticData.RetryTimeoutSeconds} seconds...");
                await Task.Delay(TimeSpan.FromSeconds(TimeoutDefaults.StaticData.RetryTimeoutSeconds));
            }

            using IServiceScope scope = _serviceProvider.CreateScope();
            PassengerManagerContext context = scope.ServiceProvider.GetRequiredService<PassengerManagerContext>();

            var strategy = context.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await context.Database.BeginTransactionAsync();

                try
                {
                    await ImportBulkUpsert<Shared.Models.Agency, Server.Services.Maps.AgencyMap>(archive, "agency.txt", context, a => a.AgencyId);
                    await ImportBulkUpsert<Shared.Models.Stop, Server.Services.Maps.StopMap>(archive, "stops.txt", context, s => s.StopId);
                    await ImportBulkUpsert<Shared.Models.Route, Server.Services.Maps.RouteMap>(archive, "routes.txt", context, r => r.RouteId);
                    await ImportBulkUpsert<Shared.Models.Trip, Server.Services.Maps.TripMap>(archive, "trips.txt", context, t => t.TripId);
                    await ProcessShapes(archive, context);

                    await transaction.CommitAsync();
                    _logger.LogInformation("Successful GTFS static data sync complete.");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Failed to sync GTFS static data.");
                }
            });

            archive?.Dispose();
            stream?.Dispose();
        }            
    }
}
