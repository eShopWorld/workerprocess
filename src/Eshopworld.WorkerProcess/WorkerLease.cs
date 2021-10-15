using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.PlatformServices;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Eshopworld.Core;
using EShopworld.WorkerProcess.Configuration;
using EShopworld.WorkerProcess.Exceptions;
using EShopworld.WorkerProcess.Infrastructure;
using EShopworld.WorkerProcess.Model;
using EShopworld.WorkerProcess.Stores;
using EShopworld.WorkerProcess.Telemetry;
using Microsoft.Extensions.Options;

namespace EShopworld.WorkerProcess
{
    public class WorkerLease : IWorkerLease
    {
        private readonly IOptions<WorkerLeaseOptions> _options;
        private readonly ILeaseAllocator _leaseAllocator;
        private readonly IBigBrother _telemetry;
        private readonly ITimer _timer;
        private readonly ISlottedInterval _slottedInterval;
        
        internal ILease CurrentLease;

        public WorkerLease(
            IBigBrother telemetry,
            ILeaseAllocator leaseAllocator,
            ITimer timer,
            ISlottedInterval slottedInterval,
            IOptions<WorkerLeaseOptions> options)
        {
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _leaseAllocator = leaseAllocator ?? throw new ArgumentNullException(nameof(leaseAllocator));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _timer = timer ?? throw new ArgumentNullException(nameof(timer));
            _slottedInterval = slottedInterval ?? throw new ArgumentNullException(nameof(slottedInterval));

            InstanceId = _options.Value.InstanceId ?? Guid.NewGuid();

            if (string.IsNullOrWhiteSpace(_options.Value.WorkerType))
            {
                throw new ArgumentOutOfRangeException($"{nameof(options.Value.WorkerType)} cannot be null or empty");
            }
        }

        /// <inheritdoc />
        public Guid InstanceId { get; }

        /// <inheritdoc />
        public Task StartLeasingAsync(CancellationToken cancellationToken)
        {
            try
            {
                Task.Run(async () => await HandleExistingLeaseAsync(cancellationToken).ConfigureAwait(false), cancellationToken);

                return Task.Run(async () =>
                   await _timer.ExecutePeriodicallyIn(_slottedInterval.Calculate(ServerDateTime.UtcNow, _options.Value.LeaseInterval),
                        (token)=> OperationTelemetryHandler(LeaseAsync,token)).ConfigureAwait(false), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return Task.CompletedTask;
            }
            
        }

        /// <inheritdoc />
        public void StopLeasing()
        {
            _timer.Stop();
        }

        /// <inheritdoc />
        public event EventHandler<LeaseAllocatedEventArgs> LeaseAllocated;

        /// <inheritdoc />
        public event EventHandler<EventArgs> LeaseExpired;

        internal async Task HandleExistingLeaseAsync(CancellationToken token)
        {
            CurrentLease = await _leaseAllocator.TryReacquireLease(InstanceId, token).ConfigureAwait(false);

            if (CurrentLease != null)
                OnLeaseAllocated(CurrentLease.LeasedUntil.GetValueOrDefault());
        }

        internal async Task<TimeSpan> LeaseAsync(CancellationToken token)
        {

            if (CheckLeaseExpired(CurrentLease))
            {
                OnLeaseExpired();
            }

            CurrentLease = await _leaseAllocator.AllocateLeaseAsync(InstanceId,token).ConfigureAwait(false);

            if (CurrentLease != null)
                OnLeaseAllocated(CurrentLease.LeasedUntil.GetValueOrDefault());

            if (CurrentLease?.LeasedUntil.HasValue ?? false)
            {
                return CurrentLease.LeasedUntil.Value.Subtract(ServerDateTime.UtcNow);
            }
            return _slottedInterval.Calculate(
                CurrentLease?.LeasedUntil ?? ServerDateTime.UtcNow,
                _options.Value.LeaseInterval);
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


        [DebuggerStepThrough]
        [ExcludeFromCodeCoverage]
        private async Task<TimeSpan> OperationTelemetryHandler(Func<CancellationToken, Task<TimeSpan>> operation, CancellationToken token, [CallerMemberName] string memberName = "")
        {
            try
            {
                var operationEvent = new OperationTelemetryEvent(memberName);

                var result = await operation(token).ConfigureAwait(false);

                _telemetry.Publish(operationEvent);

                return result;
            }
            catch (OperationCanceledException _)
            {
                _telemetry.Publish(new LeaseStoppedEvent(InstanceId, _options.Value.WorkerType, _options.Value.Priority));

                throw;
            }
            catch (Exception ex)
            {
                _timer.Dispose();

                _telemetry.Publish(new LeaseExceptionEvent(InstanceId, _options.Value.WorkerType, _options.Value.Priority,
                    ex.Message));

                throw new WorkerLeaseException(
                    $"An unhandled exception occurred executing operation [{memberName}]", ex);
            }
        }

    }
}