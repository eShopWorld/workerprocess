using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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

namespace EShopworld.WorkerProcess.Stores
{
    public class CosmosDbLeaseStore : ILeaseStore
    {
        private readonly IOptions<CosmosDataStoreOptions> _options;
        private readonly IDocumentClient _documentClient;
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly IBigBrother _telemetry;

        internal Func<ResourceResponse<Document>, ILease> ResourceMappingFunc;

        public CosmosDbLeaseStore(IDocumentClient documentClient, IBigBrother telemetry,
            IOptions<CosmosDataStoreOptions> options)
        {
            _documentClient = documentClient ?? throw new ArgumentNullException(nameof(documentClient));
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _options = options ?? throw new ArgumentNullException(nameof(options));

            _retryPolicy = CreateRetryPolicy();

            ResourceMappingFunc = MapResource;
        }

        public async Task InitialiseAsync()
        {
            await _documentClient.CreateDatabaseIfNotExistsAsync(new Database {Id = _options.Value.Database})
                    .ConfigureAwait(false);
            
            DocumentCollection collectionDefinition = new DocumentCollection
            {
                Id = _options.Value.Collection
            };

            collectionDefinition.PartitionKey.Paths.Add("/leaseType");

            var leaseConstraint = new UniqueKey
            {
                Paths = new Collection<string> { "/leaseType" }
            };

            collectionDefinition.UniqueKeyPolicy.UniqueKeys.Add(leaseConstraint);

            await _documentClient.CreateDocumentCollectionIfNotExistsAsync(
                UriFactory.CreateDatabaseUri(_options.Value.Database),
                collectionDefinition,
                new RequestOptions {OfferThroughput = _options.Value.OfferThroughput}).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<LeaseStoreResult> TryUpdateLeaseAsync(ILease lease)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var cosmosLease = (CosmosDbLease) lease;

                if (cosmosLease == null)
                    throw new ArgumentException("Invalid lease type");

                try
                {
                    var response = await _documentClient.ReplaceDocumentAsync(
                        UriFactory.CreateDocumentUri(_options.Value.Database, _options.Value.Collection,
                            cosmosLease.Id),
                        cosmosLease,
                        new RequestOptions
                        {
                            ConsistencyLevel = _options.Value.ConsistencyLevel,
                            AccessCondition = new AccessCondition
                            {
                                Condition = cosmosLease.ETag,
                                Type = AccessConditionType.IfMatch
                            }
                        }).ConfigureAwait(false);

                    return new LeaseStoreResult(MapResource(response),
                        response.StatusCode == HttpStatusCode.OK);
                }
                catch (DocumentClientException ex) when (ex.StatusCode == HttpStatusCode.Conflict || ex.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    return new LeaseStoreResult(null, false);
                }
            }).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<ILease> ReadByLeaseTypeAsync(string leaseType)
        {
#pragma warning disable 1998
            // There is no async document query
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                return (ILease) _documentClient.CreateDocumentQuery<CosmosDbLease>(
                        UriFactory.CreateDocumentCollectionUri(_options.Value.Database, _options.Value.Collection),
                        new FeedOptions
                        {
                            ConsistencyLevel = _options.Value.ConsistencyLevel,
                            EnableCrossPartitionQuery = true
                        })
                    .Where(so => so.LeaseType == leaseType)
                    .AsEnumerable()
                    .FirstOrDefault();
            }).ConfigureAwait(false);
#pragma warning disable 1998
        }

        /// <inheritdoc />
        public async Task<LeaseStoreResult> TryCreateLeaseAsync(ILease lease)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                try
                {
                    var cosmosDbLease = new CosmosDbLease
                    {
                        InstanceId = lease.InstanceId,
                        Interval = lease.Interval,
                        LeasedUntil = lease.LeasedUntil,
                        Priority = lease.Priority,
                        LeaseType = lease.LeaseType
                    };
                    var response = await _documentClient.CreateDocumentAsync(
                        UriFactory.CreateDocumentCollectionUri(_options.Value.Database, _options.Value.Collection),
                        cosmosDbLease,
                        new RequestOptions
                        {
                            ConsistencyLevel = _options.Value.ConsistencyLevel
                        }).ConfigureAwait(false);

                    if (response.StatusCode == HttpStatusCode.Created)
                        return new LeaseStoreResult(ResourceMappingFunc(response), true);
                }
                catch (DocumentClientException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
                {
                    // document was created before, we did not get the lease
                    // do not throw exception
                }

                return new LeaseStoreResult(null, false);
            }).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> AddLeaseRequestAsync(string leaseType, int priority, Guid instanceId)
        {
            var leaseRequest = new CosmoDbLeaseRequest
            {
                InstanceId = instanceId,
                Priority = priority,
                LeaseType = leaseType,
                TimeToLive = _options.Value.LeaseRequestTimeToLive
            };
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                try
                {
                    var response = await _documentClient.CreateDocumentAsync(
                        UriFactory.CreateDocumentCollectionUri(_options.Value.Database, _options.Value.RequestsCollection),
                        leaseRequest,
                        new RequestOptions
                        {

                            ConsistencyLevel = _options.Value.ConsistencyLevel,
                        }).ConfigureAwait(false);

                    if (response.StatusCode == HttpStatusCode.Created)
                        return true;
                }
                catch (DocumentClientException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
                {
                    // document was created before, we did not get the lease
                    // do not throw exception
                    _telemetry.Publish(ex.ToExceptionEvent());
                }

                return false;
            }).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<Guid?> SelectWinnerRequestAsync(string workerType)
        {

#pragma warning disable 1998
            // There is no async document query
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                try
                {
                    var query = _documentClient.CreateDocumentQuery<CosmoDbLeaseRequest>(
                        UriFactory.CreateDocumentCollectionUri(_options.Value.Database, _options.Value.RequestsCollection),
                        new FeedOptions
                        {
                            ConsistencyLevel = _options.Value.ConsistencyLevel,
                            EnableCrossPartitionQuery = true
                        });
                        
                   var result=query.Where(so => so.LeaseType == workerType)
                        .OrderBy(req => req.Priority)
                        .ThenBy(req => req.Timestamp)
                        .AsEnumerable()
                        .FirstOrDefault();
                    return result?.InstanceId;
                }
                catch (Exception e)
                {
                    _telemetry.Publish(e.ToExceptionEvent());
                    throw;
                }


            }).ConfigureAwait(false);
#pragma warning disable 1998
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

        [ExcludeFromCodeCoverage]
        private ILease MapResource(ResourceResponse<Document> response)
        {
            return (CosmosDbLease)(dynamic)response.Resource;
        }

        
    }
}