﻿using System;
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
        public void StartLeasing()
        {
            OperationTelemetryHandler(() =>
            {
              
                var interval = _slottedInterval.Calculate(ServerDateTime.UtcNow, _options.Value.LeaseInterval);
                Task.Delay(interval).ContinueWith((t)=>LeaseAsync());
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
           
            while(true)
            {
                if (CheckLeaseExpired(CurrentLease))
                {
                    await _leaseAllocator.ReleaseLeaseAsync(CurrentLease).ConfigureAwait(false);

                    OnLeaseExpired();
                }

                CurrentLease = await _leaseAllocator.AllocateLeaseAsync(InstanceId).ConfigureAwait(false);

                if (CurrentLease != null)
                    OnLeaseAllocated(CurrentLease.LeasedUntil.GetValueOrDefault());

                var interval = _slottedInterval.Calculate(
                    CurrentLease?.LeasedUntil ?? ServerDateTime.UtcNow,
                    _options.Value.LeaseInterval);

                if (CurrentLease?.LeasedUntil.HasValue ?? false)
                { 
                    interval = CurrentLease.Interval.Value;
                }

                await Task.Delay(interval);
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
    }
}