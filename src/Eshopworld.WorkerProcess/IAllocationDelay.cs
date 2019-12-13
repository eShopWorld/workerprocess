using System;
using EShopworld.WorkerProcess.Stores;

namespace EShopworld.WorkerProcess
{
    /// <summary>
    /// Delay the allocation of a <see cref="ILease"/> instance
    /// </summary>
    public interface IAllocationDelay
    {
        /// <summary>
        /// Delay the allocation task
        /// </summary>
        /// <param name="priority">The priority</param>
        /// <param name="leaseInterval">The lease interval</param>
        TimeSpan Calculate(int priority, TimeSpan leaseInterval);
    }
}
