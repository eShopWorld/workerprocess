using System;
using System.Threading.Tasks;

namespace EShopworld.WorkerProcess.CosmosDistributedLock
{
    public interface IDistributedLock : IAsyncDisposable
    {
        public Task<IAsyncDisposable> AcquireAsync(string lockName);
    }
}