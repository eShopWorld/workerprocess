using System;
using System.Fabric;
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
        private readonly ISlottedInterval _slottedInterval;
        private readonly IBigBrother _telemetry;

        public LeaseAllocator(
            IBigBrother telemetry,
            ILeaseStore leaseStore,
            ISlottedInterval slottedInterval,
            IAllocationDelay allocationDelay,
            IOptions<WorkerLeaseOptions> options)
        {
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _leaseStore = leaseStore ?? throw new ArgumentNullException(nameof(leaseStore));
            _slottedInterval = slottedInterval ?? throw new ArgumentNullException(nameof(slottedInterval));
            _allocationDelay = allocationDelay ?? throw new ArgumentNullException(nameof(allocationDelay));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc />
        public async Task<ILease> AllocateLeaseAsync(Guid instanceId)
        {
            //Lease allocation following design documented here : https://eshopworld.atlassian.net/wiki/spaces/EPLT/pages/2345435290/Worker+Process
            _telemetry.Publish(new LeaseAcquisitionEvent(instanceId, _options.Value.WorkerType,
                _options.Value.Priority, $"Lease allocation started for instance {instanceId}"));
           
            var lease = await _leaseStore.ReadByLeaseTypeAsync(_options.Value.WorkerType).ConfigureAwait(false);
            if (lease?.LeasedUntil != null && lease.LeasedUntil.Value > ServerDateTime.UtcNow)
                return null;
            
            await _leaseStore.AddLeaseRequestAsync(_options.Value.WorkerType,_options.Value.Priority,instanceId).ConfigureAwait(false);

            // backoff to allow other workers to add their lease request
            var delay = _allocationDelay.Calculate(_options.Value.Priority, _options.Value.LeaseInterval);
            await Task.Delay(delay);

            _telemetry.Publish(new LeaseAcquisitionEvent(instanceId, _options.Value.WorkerType,
                _options.Value.Priority, $"Leader election starting"));

            var winnerInstanceId = await _leaseStore.SelectWinnerRequestAsync(_options.Value.WorkerType);

            if (!winnerInstanceId.HasValue || winnerInstanceId != instanceId)
            {
                var reason = !winnerInstanceId.HasValue
                    ? "There are no LeaseRequests to evaluate"
                    : $"Lease failed to be acquired after election. Winner instance is {winnerInstanceId.Value}.";
                _telemetry.Publish(new LeaseAcquisitionEvent(instanceId, _options.Value.WorkerType,
                    _options.Value.Priority, reason));
                return null;
            }

            if (lease == null)
            {
                var createTime = ServerDateTime.UtcNow;
                var interval = _slottedInterval.Calculate(createTime, _options.Value.LeaseInterval);
                lease = new CosmosDbLease
                {
                    InstanceId = instanceId,
                    Interval = interval,
                    LeasedUntil = createTime.Add(interval),
                    Priority = _options.Value.Priority,
                    LeaseType = _options.Value.WorkerType
                };
                var createResult = await TryAcquireLease(lease, instanceId, _leaseStore.TryCreateLeaseAsync).ConfigureAwait(false);
                if (createResult.Result || createResult.Lease==null)
                {
                    return createResult.Lease;
                }

                lease = createResult.Lease;
            }


            lease.Priority = _options.Value.Priority;
            lease.InstanceId = instanceId;
            var updateTime = ServerDateTime.UtcNow;
            lease.Interval = _slottedInterval.Calculate(updateTime, _options.Value.LeaseInterval);
            lease.LeasedUntil = updateTime.Add(lease.Interval.Value);
            var updateResult = await TryAcquireLease(lease, instanceId, _leaseStore.TryUpdateLeaseAsync).ConfigureAwait(false);

            return updateResult.Lease;
           
        }

        private async Task<LeaseStoreResult> TryAcquireLease(ILease lease, Guid instanceId, Func<ILease,Task<LeaseStoreResult>> leaseAcquirer)
        {
            var leaseResult = await leaseAcquirer(lease).ConfigureAwait(false);

            if (leaseResult.Result)
            {
                _telemetry.Publish(new LeaseAcquisitionEvent(instanceId, _options.Value.WorkerType,
                    _options.Value.Priority,
                    $"Lease acquired. {GenerateLeaseInfo(leaseResult.Lease)}"));
                return leaseResult;
            }

            // Read the lease for event
            lease = await _leaseStore.ReadByLeaseTypeAsync(_options.Value.WorkerType).ConfigureAwait(false);

            if (lease.Priority <= _options.Value.Priority)
            {
                // if we get here, means that there was a higher priority wp claiming to be the winner and acquiring the lease previously
                // this happens if not all worker processes see the same winner, despite of the delay. Give up in this case

                _telemetry.Publish(new LeaseAcquisitionEvent(instanceId, _options.Value.WorkerType,
                    _options.Value.Priority,
                    $"Lease acquisition failed lease already acquired. {GenerateLeaseInfo(lease)}"));
                return new LeaseStoreResult(null,false);
            }
            // if we get here, means that there was a lower priority wp claiming to be the winner and acquiring the lease previously
            // this happens if not all worker processes see the same winner, despite of the delay. Current Lease not valid and should be updated in this case

            return new LeaseStoreResult(lease ,false);
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