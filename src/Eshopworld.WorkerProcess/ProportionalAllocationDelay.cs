using System;
using EShopworld.WorkerProcess.Configuration;

namespace EShopworld.WorkerProcess
{
    /// <summary>
    ///     An implementation of a <see cref="IAllocationDelay" /> that implements an proportional delay based on
    ///     priority of the worker lease
    /// </summary>
    public class ProportionalAllocationDelay : IAllocationDelay
    {
        /// <inheritdoc />
        public TimeSpan Calculate(int priority, TimeSpan leaseInterval)
        {
            var adjustedPriority = (WorkerLeaseOptions.MaxPriority - priority) + 1;

            return TimeSpan.FromTicks((leaseInterval.Ticks / 4) / adjustedPriority);
        }
    }
}