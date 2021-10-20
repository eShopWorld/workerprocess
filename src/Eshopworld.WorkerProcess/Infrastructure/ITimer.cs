using System;
using System.Threading;
using System.Threading.Tasks;

namespace EShopworld.WorkerProcess.Infrastructure
{
    /// <summary>
    /// An abstraction for a system timer
    /// </summary>
    public interface ITimer : IDisposable
    {
        /// <summary>
        /// Execute a func periodically after some delay
        /// </summary>
        /// <param name="interval"></param>
        /// <param name="executor"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task ExecutePeriodicallyIn(TimeSpan interval, Func<CancellationToken, Task<TimeSpan>> executor, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stop the timer
        /// </summary>
        void Stop();
    }
}