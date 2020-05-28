using System;
using System.Diagnostics.CodeAnalysis;
using System.Timers;

namespace EShopworld.WorkerProcess.Infrastructure
{
    /// <summary>
    /// An implementation of the <see cref="ITimer"/> instance
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class SystemTimer : ITimer
    {
        private readonly Timer _timer;

        public SystemTimer()
        {
            // As we explicitly stop/start in TimerElapsed, no need for auto-reset, which is on by default.
            _timer = new Timer() {AutoReset = false};
            _timer.Elapsed += TimerElapsed;
        }

        /// <inheritdoc />
        public double Interval
        {
            get => _timer.Interval;
            set => _timer.Interval = value;
        }

        /// <inheritdoc />
        public event EventHandler<ElapsedEventArgs> Elapsed;

        /// <inheritdoc />
        public void Start()
        {
            _timer.Start();
        }

        /// <inheritdoc />
        public void Stop()
        {
            _timer.Stop();
        }

        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            Elapsed?.Invoke(this, e);
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}