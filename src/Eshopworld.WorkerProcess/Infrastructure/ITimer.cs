using System;
using System.Threading.Tasks;

namespace EShopworld.WorkerProcess.Infrastructure
{
    /// <summary>
    /// An abstraction for a system timer
    /// </summary>
    public interface ITimer : IDisposable
    {
        /// <summary>
        /// Execute a func after some delay
        /// </summary>
        /// <param name="interval"></param>
        /// <param name="executor"></param>
        /// <returns></returns>
        Task ExecuteIn(TimeSpan interval, Func<Task> executor);

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