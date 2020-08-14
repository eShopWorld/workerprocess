using System;
using System.Threading.Tasks;
using Eshopworld.Core;
using EShopworld.WorkerProcess.Configuration;
using EShopworld.WorkerProcess.Infrastructure;
using EShopworld.WorkerProcess.Stores;
using EShopworld.WorkerProcess.Telemetry;
using Microsoft.Extensions.Options;

namespace EShopworld.WorkerProcess
{
    public class LeaseAllocator : ILeaseAllocator
    {
        private readonly IOptions<WorkerLeaseOptions> _options;
        private readonly IAllocationDelay _allocationDelay;
        private readonly ILeaseStore _leaseStore;
        private readonly IBigBrother _telemetry;

        public LeaseAllocator(IBigBrother telemetry, ILeaseStore leaseStore, IAllocationDelay allocationDelay,
            IOptions<WorkerLeaseOptions> options)
        {
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _leaseStore = leaseStore ?? throw new ArgumentNullException(nameof(leaseStore));
            _allocationDelay = allocationDelay ?? throw new ArgumentNullException(nameof(allocationDelay));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc />
        public async Task<ILease> AllocateLeaseAsync(Guid instanceId)
        {
            var lease = await _leaseStore.ReadByLeaseTypeAsync(_options.Value.WorkerType).ConfigureAwait(false);

            if (lease == null)
            {
                var leaseResult = await _leaseStore.TryCreateLeaseAsync(_options.Value.WorkerType,
                    _options.Value.Priority, instanceId).ConfigureAwait(false);

                if (leaseResult.Result)
                {
                    lease = leaseResult.Lease;
                }
                else
                {
                    // another worker lease created the lease before this worker read the created lease
                    lease = await _leaseStore.ReadByLeaseTypeAsync(_options.Value.WorkerType).ConfigureAwait(false);
                }
            }

            // check lease priority is less than current worker priority and lease is not acquired
            if (lease.Priority <= _options.Value.Priority && lease.LeasedUntil == null ||
                lease.LeasedUntil.HasValue && lease.LeasedUntil.Value < ServerDateTime.UtcNow)
            {
                lease.Priority = _options.Value.Priority;
                lease.InstanceId = instanceId;

                var updateResult = await _leaseStore.TryUpdateLeaseAsync(lease).ConfigureAwait(false);

                if (updateResult.Result)
                {
                    lease = updateResult.Lease;

                    // backoff to allow other worker lease instances to update the lease
                    var delay = _allocationDelay.Calculate(_options.Value.Priority, _options.Value.LeaseInterval);

                    _telemetry.Publish(new LeaseAcquisitionEvent(instanceId, _options.Value.WorkerType,
                        _options.Value.Priority,
                        $"Lease activation delayed [{delay}]. Allocated Lease Id: [{lease.Id}]"));

                    await Task.Delay(delay).ConfigureAwait(false);

                    // activate
                    lease.LeasedUntil = GetLeaseUntil(_options.Value.LeaseInterval);

                    updateResult = await _leaseStore.TryUpdateLeaseAsync(lease).ConfigureAwait(false);

                    if (updateResult.Result)
                    {
                        _telemetry.Publish(new LeaseAcquisitionEvent(instanceId, _options.Value.WorkerType,
                            _options.Value.Priority,
                            $"Lease acquired. {GenerateLeaseInfo(updateResult.Lease)}"));

                        return updateResult.Lease;
                    }

                    LogLeaseAlreadyAcquiredEvent(instanceId, lease);
                }

                // Read the lease for event
                lease = await _leaseStore.ReadByLeaseTypeAsync(_options.Value.WorkerType).ConfigureAwait(false);

                if(lease.InstanceId == instanceId)
                {
                    return lease;
                }

                LogLeaseAlreadyAcquiredEvent(instanceId, lease);
            }
            else
            {
                // lease already acquired
                LogLeaseAlreadyAcquiredEvent(instanceId, lease);
            }

            return null;
        }

        private void LogLeaseAlreadyAcquiredEvent(Guid instanceId, ILease lease)
        {
            _telemetry.Publish(new LeaseAcquisitionEvent(instanceId, _options.Value.WorkerType,
                    _options.Value.Priority,
                    $"Lease acquisition failed lease already acquired. {GenerateLeaseInfo(lease)}"));
        }

        /// <inheritdoc />
        public async Task ReleaseLeaseAsync(ILease lease)
        {
            var instanceId = lease.InstanceId.GetValueOrDefault();

            lease.Priority = -1;
            lease.LeasedUntil = null;
            lease.InstanceId = null;

            var updateResult = await _leaseStore.TryUpdateLeaseAsync(lease).ConfigureAwait(false);

            if (!updateResult.Result)
            {
                _telemetry.Publish(new LeaseReleaseEvent(instanceId, _options.Value.WorkerType,
                    _options.Value.Priority,
                    $"Lease release failed. Lease Id: [{instanceId}]"));
            }
        }

        private static DateTime GetLeaseUntil(TimeSpan interval)
        {
            var now = ServerDateTime.UtcNow;

            return now.AddMilliseconds(interval.TotalMilliseconds -
                                       now.TimeOfDay.TotalMilliseconds / interval.TotalMilliseconds % 1 *
                                       interval.TotalMilliseconds);
        }

        private static string GenerateLeaseInfo(ILease lease)
        {
            return
                $"Lease Id: [{lease.Id}] LeaseType: [{lease.LeaseType}] InstanceId: [{lease.InstanceId}] Priority: [{lease.Priority}] LeasedUntil: [{(lease.LeasedUntil.HasValue ? lease.LeasedUntil.Value.ToString() : "null")}]";
        }
    }
}