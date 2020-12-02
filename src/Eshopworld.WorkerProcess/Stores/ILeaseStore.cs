using EShopworld.WorkerProcess.Model;
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
        /// <param name="lease"></param>
        /// <returns></returns>
        Task<LeaseStoreResult> TryCreateLeaseAsync(ILease lease);

        /// <summary>
        /// Add a new LeaseRequest
        /// </summary>
        /// <param name="leaseRequest"></param>
        /// <returns>if request was successfully</returns>
        Task<bool> AddLeaseRequestAsync(LeaseRequest leaseRequest);
        
        /// <summary>
        /// Return Winner LeaseRequest
        /// </summary>
        /// <param name="workerType"></param>
        /// <returns>winner instance Id</returns>
        Task<Guid?> SelectWinnerRequestAsync(string workerType);
    }
}