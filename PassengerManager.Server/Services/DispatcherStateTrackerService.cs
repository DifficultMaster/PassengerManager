using StackExchange.Redis;

namespace PassengerManager.Server.Services
{
    public class DispatcherStateTrackerService
    {
        private readonly IDatabase _db;
        private const string DispatcherSetKeyTemplate = "agency:{0}:online_dispatchers";

        public DispatcherStateTrackerService(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }

        private string GetDispatcherSetKey(string agencyId)
        {
            return string.Format(DispatcherSetKeyTemplate, agencyId);
        }

        public async Task MarkDispatcherOnlineAsync(string dispatcherId, string agencyId)
        {
            string key = GetDispatcherSetKey(agencyId);
            await _db.SortedSetAddAsync(key, dispatcherId, 0);
        }

        public async Task MarkDispatcherOfflineAsync(string dispatcherId, string agencyId)
        {
            string key = GetDispatcherSetKey(agencyId);
            await _db.SortedSetRemoveAsync(key, dispatcherId);
        }

        public async Task IncrementDispatcherLoadAsync(string dispatcherId, string agencyId)
        {
            string key = GetDispatcherSetKey(agencyId);
            await _db.SortedSetIncrementAsync(key, dispatcherId, 1);
        }

        public async Task DecrementDispatcherLoadAsync(string dispatcherId, string agencyId)
        {
            string key = GetDispatcherSetKey(agencyId);
            double? currentScore = await _db.SortedSetScoreAsync(key, dispatcherId);
            if (currentScore.HasValue && currentScore.Value > 0)
            {
                await _db.SortedSetDecrementAsync(key, dispatcherId, 1);
            }
        }

        public async Task<string?> GetLeastBusyDispatcherIdAsync(string agencyId)
        {
            string key = GetDispatcherSetKey(agencyId);
            RedisValue[] results = await _db.SortedSetRangeByRankAsync(key, 0, 0);
            return results.Length > 0 ? results[0].ToString() : null;
        }
    }
}
