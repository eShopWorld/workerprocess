using System;
using System.Threading.Tasks;
using EShopworld.WorkerProcess.Stores;

namespace EShopworld.WorkerProcess
{
    public interface ILeaseAllocator
    {
        /// <summary>
        /// Allocate a <see cref="ILease"/> to an instance
        /// </summary>
        /// <param name="instanceId">The instance id to allocate the <see cref="ILease"/></param>
        /// <returns>A <see cref="ILease"/></returns>
        Task<ILease> AllocateLeaseAsync(Guid instanceId);
        /// <summary>
        /// Release a <see cref="ILease"/> instance
        /// </summary>
        /// <param name="lease">The <see cref="ILease"/> instance to release</param>
        /// <returns></returns>
        Task ReleaseLeaseAsync(ILease lease);
    }
}
