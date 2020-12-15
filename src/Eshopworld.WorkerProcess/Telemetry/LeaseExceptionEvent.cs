using System;

namespace EShopworld.WorkerProcess.Telemetry
{
    /// <summary>
    /// An event that is raised when an exception occurs when an exception occurs during the leasing process
    /// </summary>
    public class LeaseExceptionEvent : BaseLeaseEvent
    {
        /// <summary>
        ///     Create instance of <see cref="LeaseExceptionEvent" />
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="workerType"></param>
        /// <param name="priority"></param>
        /// <param name="reason"></param>
        public LeaseExceptionEvent(Guid instanceId, string workerType, int priority, string reason) : base(instanceId,workerType,priority,reason)
        {
            
        }

    }
}