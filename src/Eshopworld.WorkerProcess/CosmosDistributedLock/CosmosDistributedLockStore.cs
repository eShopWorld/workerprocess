using System;
using System.Collections.ObjectModel;
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

namespace EShopworld.WorkerProcess.CosmosDistributedLock
{
    public class CosmosDistributedLockStore : ICosmosDistributedLockStore
    {
        private readonly Lazy<IDocumentClient> _documentClient;
        private readonly IOptions<CosmosDataStoreOptions> _options;
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly IBigBrother _telemetry;

        public CosmosDistributedLockStore(IDocumentClient documentClient, IOptions<CosmosDataStoreOptions> options, IBigBrother telemetry)
        {
            _options = options;
            _telemetry = telemetry;
            _retryPolicy = CreateRetryPolicy();
            _documentClient = new Lazy<IDocumentClient>(() =>
                InitialiseAsync(documentClient, _options.Value)
                    .ConfigureAwait(false).GetAwaiter().GetResult());
        }

        public async Task<bool> TryClaimLockAsync(IDistributedLockClaim claim)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                try
                {
                    var response = await _documentClient.Value.CreateDocumentAsync(
                        UriFactory.CreateDocumentCollectionUri(_options.Value.Database, _options.Value.DistributedLocksCollection),
                        claim,
                        new RequestOptions
                        {
                            ConsistencyLevel = _options.Value.ConsistencyLevel,
                            PartitionKey = new PartitionKey(claim.Id)
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
                var response = await _documentClient.Value.DeleteDocumentAsync(
                    UriFactory.CreateDocumentUri(
                        _options.Value.Database, 
                        _options.Value.DistributedLocksCollection,
                        claim.Id),
                    new RequestOptions
                    {
                        PartitionKey = new PartitionKey(claim.Id)
                    });
                return response.StatusCode == HttpStatusCode.OK;
            }).ConfigureAwait(false);
        }

        internal async Task<IDocumentClient> InitialiseAsync(IDocumentClient documentClient,
            CosmosDataStoreOptions cosmosDataStoreOptions)
        {
            await documentClient.CreateDatabaseIfNotExistsAsync(new Database { Id = cosmosDataStoreOptions.Database });
            await CreateLockClaimsCollection(documentClient, cosmosDataStoreOptions);
            return documentClient;
        }

        private async Task CreateLockClaimsCollection(IDocumentClient documentClient,
            CosmosDataStoreOptions cosmosDataStoreOptions)
        {
            var collectionDefinition = new DocumentCollection
            {
                Id = cosmosDataStoreOptions.DistributedLocksCollection,
                PartitionKey = new PartitionKeyDefinition
                {
                    Paths = new Collection<string> { "/id" }
                }
            };

            await documentClient.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(cosmosDataStoreOptions.Database),
                collectionDefinition,
                new RequestOptions { OfferThroughput = cosmosDataStoreOptions.OfferThroughput }).ConfigureAwait(false);
        }

        private AsyncRetryPolicy CreateRetryPolicy()
        {
            return Policy
                .Handle<DocumentClientException>(e => e.RetryAfter > TimeSpan.Zero)
                .WaitAndRetryAsync(5,
                    (count, exception, context) => ((DocumentClientException)exception).RetryAfter,
                    (exception, timeSpan, count, context) =>
                    {
                        _telemetry.Publish(new CosmosRetryEvent(timeSpan, count));
                        return Task.CompletedTask;
                    }
                );
        }
    }
}
