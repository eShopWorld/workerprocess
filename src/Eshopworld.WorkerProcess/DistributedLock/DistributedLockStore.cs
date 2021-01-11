using System;
using System.Net;
using System.Threading.Tasks;
using Eshopworld.Core;
using EShopworld.WorkerProcess.Configuration;
using EShopworld.WorkerProcess.Telemetry;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace EShopworld.WorkerProcess.DistributedLock
{
    public class DistributedLockStore : IDistributedLockStore
    {
        private readonly IDocumentClient _documentClient;
        private readonly IOptions<CosmosDataStoreOptions> _options;
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly IBigBrother _telemetry;

        public DistributedLockStore(IDocumentClient documentClient, IOptions<CosmosDataStoreOptions> options, IBigBrother telemetry)
        {
            _documentClient = documentClient;
            _options = options;
            _telemetry = telemetry;
            _retryPolicy = CreateRetryPolicy();
        }

        public async Task InitialiseAsync()
        {
            await _documentClient.CreateDatabaseIfNotExistsAsync(new Database { Id = _options.Value.Database })
                .ConfigureAwait(false);

            await CreateLockClaimsCollection();
        }

        private async Task CreateLockClaimsCollection()
        {
            var collectionDefinition = new DocumentCollection
            {
                Id = _options.Value.DistributedLocksCollection
            };

            await _documentClient.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(_options.Value.Database),
                collectionDefinition,
                new RequestOptions { OfferThroughput = _options.Value.OfferThroughput }).ConfigureAwait(false);
        }

        public async Task TryClaimLockAsync(IDistributedLockClaim claim)
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                try
                {
                    var response = await _documentClient.CreateDocumentAsync(
                        UriFactory.CreateDocumentCollectionUri(_options.Value.Database, _options.Value.DistributedLocksCollection),
                        claim,
                        new RequestOptions
                        {
                            ConsistencyLevel = _options.Value.ConsistencyLevel
                        }).ConfigureAwait(false);

                    return response.StatusCode == HttpStatusCode.Created;
                }
                catch (DocumentClientException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
                {
                    // document was created before, lock acquisition failed
                    return false;
                }
            }).ConfigureAwait(false);
        }

        public async Task ReleaseLockAsync(IDistributedLockClaim claim)
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                try
                {
                    var response = await _documentClient.DeleteDocumentAsync(
                        UriFactory.CreateDocumentUri(_options.Value.Database, _options.Value.DistributedLocksCollection,
                            claim.Id));
                    return response.StatusCode == HttpStatusCode.OK;
                }
                catch (DocumentClientException ex)
                {
                    Console.WriteLine(ex);
                    throw;
                }
            }).ConfigureAwait(false);
        }

        private AsyncRetryPolicy CreateRetryPolicy()
        {
            return Policy
                .Handle<DocumentClientException>(e => e.RetryAfter > TimeSpan.Zero)
                .WaitAndRetryForeverAsync(
                    (count, exception, context) => ((DocumentClientException)exception).RetryAfter,
                    (exception, count, timeSpan, context) =>
                    {
                        _telemetry.Publish(new CosmosRetryEvent(timeSpan, count));
                        return Task.CompletedTask;
                    }
                );
        }
    }

    public interface IDistributedLockStore
    {
        Task InitialiseAsync();
        Task TryClaimLockAsync(IDistributedLockClaim claim);
        Task ReleaseLockAsync(IDistributedLockClaim claim);
    }
}
