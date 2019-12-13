using System;
using Eshopworld.Core;

namespace EShopworld.WorkerProcess.Telemetry
{
    /// <summary>
    /// An event that is raised when a cosmos retry event occurs
    /// </summary>
    internal class CosmosRetryEvent : TelemetryEvent
    {
        public CosmosRetryEvent(TimeSpan retryAfter, int count)
        {
            RetryAfter = retryAfter;
            Count = count;
        }
        /// <summary>
        /// The amount of time to wait before retry
        /// </summary>
        public TimeSpan RetryAfter { get; }
        /// <summary>
        /// The current retry count
        /// </summary>
        public int Count { get; }
    }
}
