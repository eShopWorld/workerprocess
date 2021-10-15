using System;
using System.Threading;
using System.Threading.Tasks;

namespace EShopworld.WorkerProcess
{
    public interface IWorkerLease
    {
        /// <summary>
        /// The instance id
        /// </summary>
        Guid InstanceId { get; }
        /// <summary>
        /// Start Leasing
        /// </summary>
        Task StartLeasingAsync(CancellationToken cancellationToken);
        /// <summary>
        /// Stop Leasing
        /// </summary>
        void StopLeasing();
        /// <summary>
        /// Event fired when lease is allocated
        /// </summary>
        event EventHandler<LeaseAllocatedEventArgs> LeaseAllocated;
        /// <summary>
        /// Event fired when lease is expired
        /// </summary>
        event EventHandler<EventArgs> LeaseExpired;
    }
}
