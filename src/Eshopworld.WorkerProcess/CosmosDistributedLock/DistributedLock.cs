using System;
using System.Threading.Tasks;

namespace EShopworld.WorkerProcess.CosmosDistributedLock
{
    public class DistributedLock : IDistributedLock
    {
        private readonly ICosmosDistributedLockStore _cosmosDistributedLockStore;
        private CosmosDistributedLockClaim _cosmosDistributedLockClaim;

        public DistributedLock(ICosmosDistributedLockStore cosmosDistributedLockStore)
        {
            _cosmosDistributedLockStore = cosmosDistributedLockStore;
        }

        public async Task<IDisposable> Acquire(string lockName)
        {
            try
            {
                await _cosmosDistributedLockStore.InitialiseAsync();
                _cosmosDistributedLockClaim = new CosmosDistributedLockClaim(lockName);
                var result = await _cosmosDistributedLockStore.TryClaimLockAsync(_cosmosDistributedLockClaim);
                return result ? this : throw new DistributedLockNotAcquiredException(lockName, null);

            }
            catch (Exception ex)
            {
                throw new DistributedLockNotAcquiredException(lockName, ex);
            }
        }

        public async void Dispose()
        {
            await _cosmosDistributedLockStore.ReleaseLockAsync(_cosmosDistributedLockClaim);
        }
    }
}