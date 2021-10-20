using System;
using System.Threading;
using System.Threading.Tasks;

namespace EShopworld.WorkerProcess.Infrastructure
{
    /// <summary>
    /// An implementation of the <see cref="ITimer"/> instance
    /// </summary>
    public class SystemTimer : ITimer
    {
        // To detect redundant calls
        private bool _disposed = false;

        private  CancellationTokenSource _cancellationTokenSource;

       
        /// <inheritdoc />
        public async Task ExecutePeriodicallyIn(TimeSpan interval,Func<CancellationToken,Task<TimeSpan>> executor, CancellationToken cancellationToken = default)
        {
            try
            {
                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _cancellationTokenSource.Token.ThrowIfCancellationRequested();
                await Task.Delay(interval, _cancellationTokenSource.Token).ConfigureAwait(false);
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    var newInterval = await executor(_cancellationTokenSource.Token).ConfigureAwait(false);

                    await Task.Delay(newInterval, _cancellationTokenSource.Token).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException)
            {
                throw;
            }
        }

        /// <inheritdoc />
        public void Stop()
        {
            if (_disposed)
            {
                return;
            }
            _cancellationTokenSource?.Cancel();
        }

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _cancellationTokenSource?.Dispose();
            _disposed = true;
        }

    }
}