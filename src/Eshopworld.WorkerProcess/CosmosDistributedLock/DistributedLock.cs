﻿using System;
using System.Threading.Tasks;
using EShopworld.WorkerProcess.Configuration;
using Microsoft.Extensions.Options;

namespace EShopworld.WorkerProcess.CosmosDistributedLock
{
    public class DistributedLock : IDistributedLock
    {
        private readonly ICosmosDistributedLockStore _cosmosDistributedLockStore;
        private readonly IOptions<CosmosDataStoreOptions> _options;
        private CosmosDistributedLockClaim _cosmosDistributedLockClaim;

        public DistributedLock(ICosmosDistributedLockStore cosmosDistributedLockStore, IOptions<CosmosDataStoreOptions> options)
        {
            _cosmosDistributedLockStore = cosmosDistributedLockStore;
            _options = options;
        }

        public async Task<IDisposable> Acquire(string lockName)
        {
            if (string.IsNullOrEmpty(lockName))
                throw new ArgumentNullException(nameof(lockName));

            try
            {
                await _cosmosDistributedLockStore.InitialiseAsync();
                _cosmosDistributedLockClaim = new CosmosDistributedLockClaim(lockName);
                var result = await _cosmosDistributedLockStore.TryClaimLockAsync(_cosmosDistributedLockClaim);

                if (result)
                    return this;

                _cosmosDistributedLockClaim = null;
                throw new DistributedLockNotAcquiredException(lockName, _options.Value.DistributedLocksCollection, null);
            }
            catch (Exception ex) when (!(ex is DistributedLockNotAcquiredException))
            {
                throw new DistributedLockNotAcquiredException(lockName, _options.Value.DistributedLocksCollection, ex);
            }
        }

        public async void Dispose()
        {
            if (string.IsNullOrEmpty(_cosmosDistributedLockClaim?.Id))
                return;

            await _cosmosDistributedLockStore.ReleaseLockAsync(_cosmosDistributedLockClaim);
        }
    }
}