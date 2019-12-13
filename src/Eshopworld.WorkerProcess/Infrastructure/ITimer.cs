using System;
using System.Timers;

namespace EShopworld.WorkerProcess.Infrastructure
{
    /// <summary>
    /// An abstraction for a system timer
    /// </summary>
    public interface ITimer : IDisposable
    {
        /// <summary>
        /// The timer interval
        /// </summary>
        double Interval { get; set; }

        /// <summary>
        /// The event that is fired when the timer is elapsed
        /// </summary>
        event EventHandler<ElapsedEventArgs> Elapsed;

        /// <summary>
        /// Start the timer
        /// </summary>
        void Start();

        /// <summary>
        /// Stop the timer
        /// </summary>
        void Stop();
    }
}