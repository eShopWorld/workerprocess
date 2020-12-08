using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using EShopworld.WorkerProcess.Exceptions;

namespace EShopworld.WorkerProcess.Infrastructure
{
    /// <summary>
    /// An implementation of the <see cref="ITimer"/> instance
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class SystemTimer : ITimer
    {
        // To detect redundant calls
        private bool _disposed = false;

        private CancellationTokenSource _cancellationTokenSource;

        /// <inheritdoc />
        public async Task ExecuteIn(TimeSpan interval,Func<Task> executor)
        {
            await Task.Delay(interval).ContinueWith(async t=>
            {
                if (!_cancellationTokenSource.IsCancellationRequested)
                {
                    await executor();
                }
            });
        }

        public void Start()
        {
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <inheritdoc />
        public void Stop()
        {
            if(_cancellationTokenSource==null)
                throw new WorkerLeaseException("Timer was not started!");
            _cancellationTokenSource.Cancel();
        }

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
        {
            if (_disposed || _cancellationTokenSource==null)
            {
                return;
            }

            _cancellationTokenSource.Dispose();
            _disposed = true;
        }
    }
}