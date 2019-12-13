using System;
using System.Threading.Tasks;

namespace EShopworld.WorkerProcess.Stores
{
    public interface ILeaseStore
    {
        /// <summary>
        /// Initialise the 
        /// </summary>
        Task InitialiseAsync();
        /// <summary>
        /// Update a <see cref="ILease"/> instance
        /// </summary>
        /// <param name="lease">The lease to update</param>
        /// <returns>The <see cref="LeaseStoreResult"/></returns>
        Task<LeaseStoreResult> TryUpdateLeaseAsync(ILease lease);
        /// <summary>
        /// Read a <see cref="ILease"/> instance by type
        /// </summary>
        /// <param name="leaseType"></param>
        /// <returns></returns>
        Task<ILease> ReadByLeaseTypeAsync(string leaseType);
        /// <summary>
        /// Try create an <see cref="ILease"/> instance
        /// </summary>
        /// <param name="leaseType">The lease type</param>
        /// <param name="priority">The priority of the lease</param>
        /// <param name="instanceId">The instance of the worker that acquired the lease</param>
        /// <returns>The <see cref="LeaseStoreResult"/></returns>
        Task<LeaseStoreResult> TryCreateLeaseAsync(string leaseType, int priority, Guid instanceId);
    }
}