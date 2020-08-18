using System;

namespace EShopworld.WorkerProcess.Stores
{
    public interface ILease
    {
        /// <summary>
        ///     The unique identifier for the lease instance
        /// </summary>
        string Id { get; set; }

        /// <summary>
        /// The priority of the instance that acquired the lease
        /// </summary>
        int Priority { get; set; }

        /// <summary>
        /// The instance id of the worker that acquired the lease
        /// </summary>
        Guid? InstanceId { get; set; }

        /// <summary>
        /// The time until the lease has been acquired
        /// </summary>
        DateTime? LeasedUntil { get; set; }

        /// <summary>
        /// The interval of the lease
        /// </summary>
        TimeSpan? Interval { get; set; }

        /// <summary>
        /// The lease type
        /// </summary>
        string LeaseType { get; set; }
    }
}