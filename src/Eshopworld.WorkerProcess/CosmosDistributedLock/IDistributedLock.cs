using System;
using System.Threading.Tasks;

namespace EShopworld.WorkerProcess.CosmosDistributedLock
{
    public interface IDistributedLock : IDisposable
    {
        public Task<IDisposable> Acquire(string lockName);
    }
}