using System;
using System.Threading;
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
        Task<ILease> AllocateLeaseAsync(Guid instanceId, CancellationToken token);
        /// <summary>
        /// Release a <see cref="ILease"/> instance
        /// </summary>
        /// <param name="lease">The <see cref="ILease"/> instance to release</param>
        /// <returns></returns>
        Task ReleaseLeaseAsync(ILease lease);

        /// <summary>
        /// Attempts to reacquire an existing valid <see cref="ILease"/>. This is useful if an application is restarted.
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<ILease> TryReacquireLease(Guid instanceId, CancellationToken token);
    }
}
