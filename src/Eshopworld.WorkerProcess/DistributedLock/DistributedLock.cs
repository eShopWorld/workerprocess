using System;
using System.Threading.Tasks;

namespace EShopworld.WorkerProcess.DistributedLock
{
    public class DistributedLock : IDistributedLock
    {
        private readonly IDistributedLockStore _distributedLockStore;
        private DistributedLockClaim _distributedLockClaim;

        public DistributedLock(IDistributedLockStore distributedLockStore)
        {
            _distributedLockStore = distributedLockStore;
        }
        
        public async Task<IDisposable> Acquire(string requestJobName)
        {
            try
            {
                await _distributedLockStore.InitialiseAsync();
                _distributedLockClaim = new DistributedLockClaim(requestJobName);
                await _distributedLockStore.TryClaimLockAsync(_distributedLockClaim);
            }
            catch (Exception ex)
            {
                throw new DistributedLockNotAcquiredException(requestJobName, ex);
            }

            return this;
        }

        public async void Dispose()
        {
            await _distributedLockStore.ReleaseLockAsync(_distributedLockClaim);
        }
    }
}