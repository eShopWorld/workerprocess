using System;
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
        /// Initialise the <see cref="IWorkerLease"/> instance
        /// </summary>
        /// <returns></returns>
        Task InitialiseAsync();
        /// <summary>
        /// Start Leasing
        /// </summary>
        void StartLeasing();
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
