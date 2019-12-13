using System;

namespace EShopworld.WorkerProcess.Configuration
{
    public class WorkerLeaseOptions
    {
        public const int MaxPriority = 8;
        private int _priority;

        /// <summary>
        ///     The Lease type
        /// </summary>
        /// <remarks>
        ///     This is an type of the class of worker that acquired the lease.
        ///     This is a domain specific value, for example it can be returnsprocessor etc
        /// </remarks>
        public string LeaseType { get; set; }

        /// <summary>
        ///     The worker process priority.
        /// </summary>
        /// <remarks>0 is the highest priority</remarks>
        public int Priority
        {
            get => _priority;
            set
            {
                if (_priority >= MaxPriority)
                    throw new ArgumentOutOfRangeException(
                        $"{nameof(Priority)} value must be less than [{MaxPriority}]");

                _priority = value;
            }
        }

        /// <summary>
        ///     The amount of time in minutes that the lease interval executes
        /// </summary>
        public TimeSpan LeaseInterval { get; set; }
    }
}