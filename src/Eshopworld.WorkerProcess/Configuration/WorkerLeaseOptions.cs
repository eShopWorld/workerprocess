using System;

namespace EShopworld.WorkerProcess.Configuration
{
    public class WorkerLeaseOptions
    {
        public const int MaxPriority = 8;
        private int _priority;

        /// <summary>
        ///     This is a category type of a worker that acquires leases.
        /// </summary>
        /// <remarks>
        ///     This is a domain specific value, for example it can be returnsprocessor etc
        /// </remarks>
        public string WorkerType { get; set; }

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
        public TimeSpan LeaseInterval { get; set; } = new TimeSpan(0,5,0);
        
        /// <summary>
        /// Default delay used en lease election process
        /// </summary>
        public TimeSpan ElectionDelay { get;  set; } = new TimeSpan(0, 0, 5);
    }
}