using System;
using System.Collections.Generic;
using System.Text;
using Eshopworld.Core;

namespace EShopworld.WorkerProcess.Telemetry
{
    /// <summary>
    /// Event raised in the exceptional case where no winner is found in the lease election
    /// </summary>
    public class WinnerNotExistingEvent : TelemetryEvent
    {
        public WinnerNotExistingEvent(Guid instanceId, string workerType)
        {
            InstanceId = instanceId;
            WorkerType = workerType;
        }

        public Guid InstanceId { get; }
        public string WorkerType { get; }
    }
}
