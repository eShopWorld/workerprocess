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
            if (string.IsNullOrEmpty(lockName))
                throw new ArgumentNullException(nameof(lockName));

            try
            {
                await _cosmosDistributedLockStore.InitialiseAsync();
                _cosmosDistributedLockClaim = new CosmosDistributedLockClaim(lockName);
                var result = await _cosmosDistributedLockStore.TryClaimLockAsync(_cosmosDistributedLockClaim);

                if (result)
                    return this;

                _cosmosDistributedLockClaim = null;
                throw new DistributedLockNotAcquiredException(lockName, null);
            }
            catch (Exception ex) when (!(ex is DistributedLockNotAcquiredException))
            {
                throw new DistributedLockNotAcquiredException(lockName, ex);
            }
        }

        public async void Dispose()
        {
            if (string.IsNullOrEmpty(_cosmosDistributedLockClaim?.Id))
                return;

            await _cosmosDistributedLockStore.ReleaseLockAsync(_cosmosDistributedLockClaim);
        }
    }
}