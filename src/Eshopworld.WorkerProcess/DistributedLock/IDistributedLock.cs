using System;
using System.Threading.Tasks;

namespace EShopworld.WorkerProcess.DistributedLock
{
    public interface IDistributedLock : IDisposable
    {
        public Task<IDisposable> Acquire(string requestJobName);
    }
}