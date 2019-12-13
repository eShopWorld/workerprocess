using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Timers;
using Eshopworld.Core;
using EShopworld.WorkerProcess.Configuration;
using EShopworld.WorkerProcess.Exceptions;
using EShopworld.WorkerProcess.Infrastructure;
using EShopworld.WorkerProcess.Stores;
using EShopworld.WorkerProcess.Telemetry;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;

namespace EShopworld.WorkerProcess
{
    public class WorkerLease : IWorkerLease
    {
        private readonly IOptions<WorkerLeaseOptions> _options;
        private readonly ILeaseAllocator _leaseAllocator;
        private readonly IBigBrother _telemetry;
        private readonly ITimer _timer;

        internal ILease CurrentLease;

        public WorkerLease(
            IBigBrother telemetry,
            ILeaseAllocator leaseAllocator,
            ITimer timer,
            IOptions<WorkerLeaseOptions> options)
        {
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _leaseAllocator = leaseAllocator ?? throw new ArgumentNullException(nameof(leaseAllocator));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _timer = timer ?? throw new ArgumentNullException(nameof(timer));

            InstanceId = Guid.NewGuid();
        }

        /// <inheritdoc />
        public Guid InstanceId { get; }

        public async Task InitialiseAsync()
        {
            await OperationTelemetryHandlerAsync(async () => { await _leaseAllocator.InitialiseAsync().ConfigureAwait(false); });
        }

        /// <inheritdoc />
        public void StartLeasing()
        {
            OperationTelemetryHandler(() =>
            {
                _timer.Elapsed += TimerElapsed;

                _timer.Interval = CalculateTimerInterval(ServerDateTime.UtcNow, _options.Value.LeaseInterval);
                _timer.Start();
            });
        }

        /// <inheritdoc />
        public void StopLeasing()
        {
            OperationTelemetryHandler(() =>
            {
                _timer.Stop();

                _timer.Elapsed -= TimerElapsed;
            });
        }

        /// <inheritdoc />
        public event EventHandler<LeaseAllocatedEventArgs> LeaseAllocated;

        /// <inheritdoc />
        public event EventHandler<EventArgs> LeaseExpired;

        internal async Task LeaseAsync()
        {
            _timer.Stop();

            try
            {
                if (CheckLeaseExpired(CurrentLease))
                {
                    await _leaseAllocator.ReleaseLeaseAsync(CurrentLease).ConfigureAwait(false);

                    OnLeaseExpired();
                }

                CurrentLease = await _leaseAllocator.AllocateLeaseAsync(InstanceId).ConfigureAwait(false);

                if (CurrentLease != null)
                    OnLeaseAllocated(CurrentLease.LeasedUntil.GetValueOrDefault());

                _timer.Interval =
                    CalculateTimerInterval(
                        CurrentLease?.LeasedUntil ?? ServerDateTime.UtcNow,
                        _options.Value.LeaseInterval);
            }
            finally
            {
                _timer.Start();
            }
        }

        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            OperationTelemetryHandler(() =>
            {
                AsyncContext.Run(async () => { await LeaseAsync().ConfigureAwait(false); });
            });
        }

        private bool CheckLeaseExpired(ILease lease)
        {
            return lease != null && lease.InstanceId == InstanceId && lease.LeasedUntil.HasValue &&
                   lease.LeasedUntil.Value < ServerDateTime.UtcNow;
        }

        private protected void OnLeaseExpired()
        {
            LeaseExpired?.Invoke(this, EventArgs.Empty);
        }

        private protected void OnLeaseAllocated(DateTime leaseExpiry)
        {
            LeaseAllocated?.Invoke(this, new LeaseAllocatedEventArgs(leaseExpiry));
        }

        private static double CalculateTimerInterval(DateTime time, TimeSpan interval)
        {
            return interval.TotalMilliseconds -
                   time.TimeOfDay.TotalMilliseconds / interval.TotalMilliseconds % 1 *
                   interval.TotalMilliseconds;
        }

        [DebuggerStepThrough]
        [ExcludeFromCodeCoverage]
        private void OperationTelemetryHandler(
            Action operation,
            [CallerMemberName] string memberName = "")
        {
            try
            {
                var operationEvent = new OperationTelemetryEvent(memberName);

                operation();

                _telemetry.Publish(operationEvent);
            }
            catch (Exception ex)
            {
                _telemetry.Publish(new LeaseExceptionEvent(ex));

                throw new WorkerLeaseException(
                    $"An unhandled exception occured executing operation [{memberName}]", ex);
            }
        }

        [DebuggerStepThrough]
        [ExcludeFromCodeCoverage]
        private async Task OperationTelemetryHandlerAsync(
            Func<Task> operation,
            [CallerMemberName] string memberName = "")
        {
            try
            {
                var operationEvent = new OperationTelemetryEvent(memberName);

                await operation();

                _telemetry.Publish(operationEvent);
            }
            catch (Exception ex)
            {
                _telemetry.Publish(new LeaseExceptionEvent(ex));

                throw new WorkerLeaseException(
                    $"An unhandled exception occured executing operation [{memberName}]", ex);
            }
        }
    }
}