using System;
using System.Threading.Tasks;
using Eshopworld.Core;
using EShopworld.WorkerProcess.Configuration;
using EShopworld.WorkerProcess.Infrastructure;
using EShopworld.WorkerProcess.Model;
using EShopworld.WorkerProcess.Stores;
using EShopworld.WorkerProcess.Telemetry;
using Microsoft.Extensions.Options;

namespace EShopworld.WorkerProcess
{
    public class LeaseAllocator : ILeaseAllocator
    {
        private readonly IOptions<WorkerLeaseOptions> _options;
        private readonly ILeaseStore _leaseStore;
        private readonly ISlottedInterval _slottedInterval;
        private readonly IBigBrother _telemetry;

        public LeaseAllocator(
            IBigBrother telemetry,
            ILeaseStore leaseStore,
            ISlottedInterval slottedInterval,
            IOptions<WorkerLeaseOptions> options)
        {
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _leaseStore = leaseStore ?? throw new ArgumentNullException(nameof(leaseStore));
            _slottedInterval = slottedInterval ?? throw new ArgumentNullException(nameof(slottedInterval));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc />
        public async Task<ILease> AllocateLeaseAsync(Guid instanceId)
        {
            _telemetry.Publish(new LeaseAcquisitionEvent(instanceId, _options.Value.WorkerType,
                _options.Value.Priority, $"Lease allocation started for instance {instanceId}"));

            var lease = await _leaseStore.ReadByLeaseTypeAsync(_options.Value.WorkerType).ConfigureAwait(false);
            if (lease?.LeasedUntil != null && lease.LeasedUntil.Value > ServerDateTime.UtcNow)
                return lease.InstanceId == instanceId ? lease : null;
            
            await _leaseStore.AddLeaseRequestAsync(new LeaseRequest
            {
                LeaseType = _options.Value.WorkerType, 
                Priority = _options.Value.Priority,
                InstanceId = instanceId,
                TimeToLive = 2 * _options.Value.ElectionDelay.Seconds
            }).ConfigureAwait(false);

            // backoff to allow other workers to add their lease request
            await Task.Delay(_options.Value.ElectionDelay);

            _telemetry.Publish(new LeaseAcquisitionEvent(instanceId, _options.Value.WorkerType,
                _options.Value.Priority, $"Leader election starting"));

            var winnerInstanceId = await _leaseStore.SelectWinnerRequestAsync(_options.Value.WorkerType);

            if (!winnerInstanceId.HasValue)
            {
                //This should not happen normally
                _telemetry.Publish(new WinnerNotExistingEvent(instanceId, _options.Value.WorkerType));
                return null;
            }

            if (winnerInstanceId != instanceId)
            {
                _telemetry.Publish(new LeaseAcquisitionEvent(instanceId, _options.Value.WorkerType,
                    _options.Value.Priority, $"Lease failed to be acquired after election. Winner instance is {winnerInstanceId.Value}."));
                return null;
            }

            if (lease == null)
            {
                lease = new Lease
                {
                    LeaseType = _options.Value.WorkerType
                };
            }

            var now = ServerDateTime.UtcNow;
            lease.InstanceId = instanceId;
            lease.Priority = _options.Value.Priority;
            lease.Interval = _slottedInterval.Calculate(now, _options.Value.LeaseInterval);
            lease.LeasedUntil = now.Add(lease.Interval.Value);
            var persistResult = await TryPersistLease(lease).ConfigureAwait(false);
            return persistResult.Lease;

        }

        private async Task<LeaseStoreResult> TryPersistLease(ILease lease)
        {
            var instanceId = lease.InstanceId.Value;
            var leaseResult = string.IsNullOrEmpty(lease.Id) 
                ? await _leaseStore.TryCreateLeaseAsync(lease).ConfigureAwait(false)
                : await _leaseStore.TryUpdateLeaseAsync(lease).ConfigureAwait(false);

            if (leaseResult.Result)
            {
                _telemetry.Publish(new LeaseAcquisitionEvent(instanceId, _options.Value.WorkerType,
                    _options.Value.Priority,
                    $"Lease successfully persisted. {GenerateLeaseInfo(leaseResult.Lease)}"));
                return leaseResult;
            }

            // Read the lease for event
            lease = await _leaseStore.ReadByLeaseTypeAsync(_options.Value.WorkerType).ConfigureAwait(false);
            
            // if requested by the same group (instanceId) return the lease
            if (lease?.InstanceId == instanceId)
                return new LeaseStoreResult(lease, false);

            _telemetry.Publish(new LeaseAcquisitionEvent(instanceId, _options.Value.WorkerType,
                _options.Value.Priority,
                $"Lease could not be persisted. Current stored lease is: {GenerateLeaseInfo(lease)}"));
            return new LeaseStoreResult(null, false);
        }

        /// <inheritdoc />
        public async Task ReleaseLeaseAsync(ILease lease)
        {
            var instanceId = lease.InstanceId.GetValueOrDefault();

            lease.Priority = -1;
            lease.LeasedUntil = null;
            lease.Interval = null;
            lease.InstanceId = null;

            var updateResult = await _leaseStore.TryUpdateLeaseAsync(lease).ConfigureAwait(false);

            if (!updateResult.Result)
            {
                _telemetry.Publish(new LeaseReleaseEvent(instanceId, _options.Value.WorkerType,
                    _options.Value.Priority,
                    $"Lease release failed. Lease Id: [{instanceId}]"));
            }
        }

        private static string GenerateLeaseInfo(ILease lease)
        {
            return
                $"Lease Id: [{lease.Id}] LeaseType: [{lease.LeaseType}] InstanceId: [{lease.InstanceId}] Priority: [{lease.Priority}] LeasedUntil: [{(lease.LeasedUntil.HasValue ? lease.LeasedUntil.Value.ToString() : "null")}]";
        }
    }
}