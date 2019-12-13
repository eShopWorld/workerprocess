using Eshopworld.Core;

namespace EShopworld.WorkerProcess.Telemetry
{
    /// <summary>
    /// An event that monitors the execution of a method
    /// </summary>
    internal class OperationTelemetryEvent : TimedTelemetryEvent
    {
        public OperationTelemetryEvent(string operationName)
        {
            OperationName = operationName;
        }

        /// <summary>
        ///     The operation being timed
        /// </summary>
        public string OperationName { get; }
    }
}
