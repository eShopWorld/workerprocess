using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

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

        private readonly CancellationTokenSource _cancellationTokenSource;

        public SystemTimer()
        {
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <inheritdoc />
        public async Task ExecutePeriodicallyIn(TimeSpan interval,Func<Task<TimeSpan>> executor)
        {
            await Task.Delay(interval).ContinueWith(async t=>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var newInterval = await executor();

                    await Task.Delay(newInterval, _cancellationTokenSource.Token);
                }
            }).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public void Stop()
        {
            _cancellationTokenSource.Cancel();
        }

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _cancellationTokenSource.Dispose();
            _disposed = true;
        }
    }
}