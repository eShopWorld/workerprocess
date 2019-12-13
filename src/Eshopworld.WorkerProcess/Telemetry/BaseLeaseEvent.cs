using System;
using Eshopworld.Core;

namespace EShopworld.WorkerProcess.Telemetry
{
    /// <summary>
    /// A base event for lease events
    /// </summary>
    public abstract class BaseLeaseEvent : TelemetryEvent
    {
        protected BaseLeaseEvent(Guid instanceId, string leaseId, int priority, string reason)
        {
            InstanceId = instanceId;
            LeaseId = leaseId;
            Priority = priority;
            Reason = reason;
        }

        public Guid InstanceId { get; }
        public string LeaseId { get; }
        public int Priority { get; }
        public string Reason { get; }
    }
}