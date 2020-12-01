using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Eshopworld.Core;
using Eshopworld.Tests.Core;
using EShopworld.WorkerProcess.Configuration;
using EShopworld.WorkerProcess.Stores;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EShopworld.WorkerProcess.UnitTests.Stores
{
    public class CosmosDbLeaseStoreTests
    {
        private readonly Mock<IDocumentClient> _mockDocumentClient;
        private readonly Mock<IBigBrother> _mockTelemetry;
        private readonly CosmosDataStoreOptions _options;
        private readonly CosmosDbLeaseStore _store;

        public CosmosDbLeaseStoreTests()
        {
            _mockDocumentClient = new Mock<IDocumentClient>();
            _mockTelemetry = new Mock<IBigBrother>();

            _options = new CosmosDataStoreOptions
            {
                LeasesCollection = "collection1",
                RequestsCollection = "collection2",
                Database = "database"
            };

            _store = new CosmosDbLeaseStore(_mockDocumentClient.Object, _mockTelemetry.Object,
                Options.Create(_options));
        }

        public interface IMockDocumentQuery<T> : IDocumentQuery<T>, IOrderedQueryable<T>
        {
        }
        
        [Fact, IsUnit]
        public async Task InitialiseAsync_CreateDatabaseAndCreateCollectionIsCalled()
        {
            // Act
            await _store.InitialiseAsync();

            // Assert
            using (new AssertionScope())
            {
                _mockDocumentClient.Verify(
                client => client.CreateDatabaseIfNotExistsAsync(
                    It.Is<Database>(db => db.Id.Equals(_options.Database)),
                    null),
                Times.Once);

            _mockDocumentClient.Verify(
                client => client.CreateDocumentCollectionIfNotExistsAsync(
                    It.IsAny<Uri>(),
                    It.Is<DocumentCollection>(col => col.Id.Equals(_options.LeasesCollection)),
                    It.IsAny<RequestOptions>()), Times.Once);

            _mockDocumentClient.Verify(
                client => client.CreateDocumentCollectionIfNotExistsAsync(
                    It.IsAny<Uri>(),
                    It.Is<DocumentCollection>(col => col.Id.Equals(_options.RequestsCollection)),
                    It.IsAny<RequestOptions>()), Times.Once);
            }
        }

        [Fact, IsUnit]
        public async Task InitialiseAsync_LeasesCollectionIsCreatedWithConstraintsAndPartition()
        {
            // Act
            await _store.InitialiseAsync();

            // Assert
            _mockDocumentClient.Verify(
                client => client.CreateDocumentCollectionIfNotExistsAsync(
                    It.IsAny<Uri>(),
                    It.Is<DocumentCollection>(collection =>
                        collection.Id.Equals(_options.LeasesCollection) &&
                        collection.UniqueKeyPolicy.UniqueKeys.Count == 1 && collection.PartitionKey.Paths.Count == 1),
                    It.IsAny<RequestOptions>()), Times.Once);
        }

        [Fact, IsUnit]
        public async Task TestTryUpdateLeaseAsyncSuccess()
        {
            // Arrange
            CosmosDbLease lease = new CosmosDbLease
            {
                Id = Guid.NewGuid().ToString()
            };

            _mockDocumentClient.Setup(m => m.ReplaceDocumentAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<object>(),
                    It.IsAny<RequestOptions>(),
                    CancellationToken.None))
                .ReturnsAsync(new Document().ToResourceResponse(HttpStatusCode.OK));

            // Act
            var result = await _store.TryUpdateLeaseAsync(lease);

            // Assert
            result.Result.Should().BeTrue();
        }

        [Fact, IsUnit]
        public async Task TestTryUpdateLeaseAsyncFailed()
        {
            // Arrange
            CosmosDbLease lease = new CosmosDbLease
            {
                Id = Guid.NewGuid().ToString()
            };

            _mockDocumentClient.Setup(m => m.ReplaceDocumentAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<object>(),
                    It.IsAny<RequestOptions>(),
                    CancellationToken.None))
                .ThrowsAsync(CreateDocumentClientExceptionForTesting(string.Empty, new Exception(),
                    HttpStatusCode.Conflict));

            // Act
            var result = await _store.TryUpdateLeaseAsync(lease);

            // Assert
            result.Result.Should().BeFalse();
        }

        [Fact, IsUnit]
        public async Task TestReadByLeaseTypeAsync()
        {
            // Arrange
            var lease = new CosmosDbLease
            {
                Id = Guid.NewGuid().ToString(),
                LeaseType = "leasetype"
            };

            var dataSource = new List<CosmosDbLease>
            {
                lease
            }.AsQueryable();

            Expression<Func<CosmosDbLease, bool>> predicate = t => t.Id == lease.Id;
            var expected = dataSource.AsEnumerable().Where(predicate.Compile());
            var response = new FeedResponse<CosmosDbLease>(expected);

            var mockDocumentQuery = new Mock<IMockDocumentQuery<CosmosDbLease>>();

            mockDocumentQuery
                .SetupSequence(m => m.HasMoreResults)
                .Returns(true)
                .Returns(false);

            mockDocumentQuery
                .Setup(_ => _.ExecuteNextAsync<CosmosDbLease>(It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var provider = new Mock<IQueryProvider>();
            provider
                .Setup(_ => _.CreateQuery<CosmosDbLease>(It.IsAny<Expression>()))
                .Returns(mockDocumentQuery.Object);

            mockDocumentQuery.As<IQueryable<CosmosDbLease>>().Setup(x => x.Provider).Returns(provider.Object);
            mockDocumentQuery.As<IQueryable<CosmosDbLease>>().Setup(x => x.Expression).Returns(dataSource.Expression);
            mockDocumentQuery.As<IQueryable<CosmosDbLease>>().Setup(x => x.ElementType).Returns(dataSource.ElementType);
            mockDocumentQuery.As<IQueryable<CosmosDbLease>>().Setup(x => x.GetEnumerator())
                .Returns(() => dataSource.GetEnumerator());

            _mockDocumentClient.Setup(m => m.CreateDocumentQuery<CosmosDbLease>(
                It.Is<Uri>(u => u == UriFactory.CreateDocumentCollectionUri(_options.Database, _options.LeasesCollection)),
                It.IsAny<FeedOptions>())).Returns(mockDocumentQuery.Object);

            // Act
            var result = await _store.ReadByLeaseTypeAsync(lease.LeaseType);

            // Assert
            result.Should().NotBeNull();
        }

        [Fact, IsUnit]
        public async Task TestTryCreateLeaseAsyncCreated()
        {
            // Arrange
#warning Workaround for lack of mocking of ResourceResponse https://github.com/Azure/azure-cosmos-dotnet-v2/issues/393
            _store.ResourceMappingFunc = response => new CosmosDbLease();

            _mockDocumentClient.Setup(m => m.CreateDocumentAsync(
                It.Is<Uri>(u => u == UriFactory.CreateDocumentCollectionUri(_options.Database, _options.LeasesCollection)),
                It.IsAny<object>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Document().ToResourceResponse(HttpStatusCode.Created));

            // Act
            var result = await _store.TryCreateLeaseAsync(new CosmosDbLease
            {
                LeaseType = "worktype",
                Priority = 1,
                InstanceId = Guid.NewGuid()
            });

            // Assert
            result.Should().NotBeNull();
            result.Lease.Should().NotBeNull();
            result.Result.Should().BeTrue();
        }

        [Fact, IsUnit]
        public async Task TestTryCreateLeaseAsyncConflict()
        {
            // Arrange
#warning Workaround for lack of mocking of ResourceResponse https://github.com/Azure/azure-cosmos-dotnet-v2/issues/393
            _store.ResourceMappingFunc = response => new CosmosDbLease();

            _mockDocumentClient.Setup(m => m.CreateDocumentAsync(
                    It.Is<Uri>(u =>
                        u == UriFactory.CreateDocumentCollectionUri(_options.Database, _options.LeasesCollection)),
                    It.IsAny<object>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(CreateDocumentClientExceptionForTesting(new Error(), HttpStatusCode.Conflict));

            // Act
            var result = await _store.TryCreateLeaseAsync(new CosmosDbLease
            {
                LeaseType = "worktype",
                Priority = 1,
                InstanceId = Guid.NewGuid()
            });

            // Assert
            result.Should().NotBeNull();
            result.Lease.Should().BeNull();
            result.Result.Should().BeFalse();
        }

        [Fact, IsUnit]
        public async Task TestAddLeaseRequestAsync_WhenAddingLeaseRequest_ShouldReturnTrue()
        {
            // Arrange
            _mockDocumentClient.Setup(m => m.CreateDocumentAsync(
                    It.Is<Uri>(u => u == UriFactory.CreateDocumentCollectionUri(_options.Database, _options.RequestsCollection)),
                    It.IsAny<object>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Document().ToResourceResponse(HttpStatusCode.Created));

            // Act
            var instanceId = Guid.NewGuid();
            var result = await _store.AddLeaseRequestAsync("test", 1, instanceId);

            // Assert
            result.Should().BeTrue();
            _mockDocumentClient.Verify(m => m.CreateDocumentAsync(
                It.Is<Uri>(u => u == UriFactory.CreateDocumentCollectionUri(_options.Database, _options.RequestsCollection)),
              It.Is<CosmoDbLeaseRequest>(req=>req.TimeToLive==_options.LeaseRequestTimeToLive && req.Priority==1 && req.InstanceId==instanceId && req.LeaseType=="test"),
                It.IsAny<RequestOptions>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),Times.Once);
            _mockTelemetry.Verify(tel=>tel.Publish(It.IsAny<ExceptionEvent>(),It.IsAny<string>(),
                It.IsAny<string>(),It.IsAny<int>()),Times.Never);

        }

        [Fact, IsUnit]
        public async Task TestAddLeaseRequestAsync_WhenAddingLeaseRequestWithConflict_ShouldReturnFalse()
        {
            // Arrange
            _mockDocumentClient.Setup(m => m.CreateDocumentAsync(
                    It.Is<Uri>(u => u == UriFactory.CreateDocumentCollectionUri(_options.Database, _options.RequestsCollection)),
                    It.IsAny<object>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(CreateDocumentClientExceptionForTesting(new Error(), HttpStatusCode.Conflict));

            // Act
            var instanceId = Guid.NewGuid();
            var result = await _store.AddLeaseRequestAsync("test", 1, instanceId);

            // Assert
            result.Should().BeFalse();
            _mockDocumentClient.Verify(m => m.CreateDocumentAsync(
                It.Is<Uri>(u => u == UriFactory.CreateDocumentCollectionUri(_options.Database, _options.RequestsCollection)),
                It.Is<CosmoDbLeaseRequest>(req => req.TimeToLive == _options.LeaseRequestTimeToLive && req.Priority == 1 && req.InstanceId == instanceId && req.LeaseType == "test"),
                It.IsAny<RequestOptions>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()), Times.Once);
            _mockTelemetry.Verify(tel => tel.Publish(It.IsAny<ExceptionEvent>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>()), Times.Once);

        }

        [Fact, IsUnit]
        public async Task SelectWinnerRequestAsync_WhenMultipleRequests_ShouldReturnTheOneWithHighestPriority()
        {
            // Arrange
            var winnerLeaseRequest = new CosmoDbLeaseRequest
            {
                Id = Guid.NewGuid().ToString(),
                InstanceId = Guid.NewGuid(),
                LeaseType = "leasetype",
                Priority = 0
            };
            var dataSource = new List<CosmoDbLeaseRequest>
            {
                new CosmoDbLeaseRequest
                {
                    Id = Guid.NewGuid().ToString(),
                    InstanceId = Guid.NewGuid(),
                    LeaseType = "leasetype",
                    Priority = 1
                },
                winnerLeaseRequest
            }.AsQueryable();

            Expression<Func<CosmoDbLeaseRequest, bool>> predicate = t => t.Id == winnerLeaseRequest.Id;
            var expected = dataSource.AsEnumerable().Where(predicate.Compile());
            var response = new FeedResponse<CosmoDbLeaseRequest>(expected);

            var mockDocumentQuery = new Mock<IMockDocumentQuery<CosmoDbLeaseRequest>>();

            mockDocumentQuery
                .SetupSequence(m => m.HasMoreResults)
                .Returns(true)
                .Returns(false);

            mockDocumentQuery
                .Setup(_ => _.ExecuteNextAsync<CosmoDbLeaseRequest>(It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var provider = new Mock<IQueryProvider>();
            provider
                .Setup(_ => _.CreateQuery<CosmoDbLeaseRequest>(It.IsAny<Expression>()))
                .Returns(mockDocumentQuery.Object);

            mockDocumentQuery.As<IQueryable<CosmoDbLeaseRequest>>().Setup(x => x.Provider).Returns(provider.Object);
            mockDocumentQuery.As<IQueryable<CosmoDbLeaseRequest>>().Setup(x => x.Expression).Returns(dataSource.Expression);
            mockDocumentQuery.As<IQueryable<CosmoDbLeaseRequest>>().Setup(x => x.ElementType).Returns(dataSource.ElementType);
            mockDocumentQuery.As<IQueryable<CosmoDbLeaseRequest>>().Setup(x => x.GetEnumerator())
                .Returns(() => dataSource.GetEnumerator());

            _mockDocumentClient.Setup(m => m.CreateDocumentQuery<CosmoDbLeaseRequest>(
                It.Is<Uri>(u => u == UriFactory.CreateDocumentCollectionUri(_options.Database, _options.RequestsCollection)),
                It.IsAny<FeedOptions>())).Returns(mockDocumentQuery.Object);

            // Act
            var result = await _store.SelectWinnerRequestAsync("leasetype");

            // Assert
            result.Should().Be(winnerLeaseRequest.InstanceId);
        }

        private static DocumentClientException CreateDocumentClientExceptionForTesting(
            Error error, HttpStatusCode httpStatusCode)
        {
            var type = typeof(DocumentClientException);

            // we are using the overload with 3 parameters (error, responseheaders, statuscode)
            // use any one appropriate for you.

            var documentClientExceptionInstance = type.Assembly.CreateInstance(type.FullName,
                false, BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { error, null, httpStatusCode }, null, null);

            return (DocumentClientException)documentClientExceptionInstance;
        }

        private DocumentClientException CreateDocumentClientExceptionForTesting(
            string error,
            Exception exception,
            HttpStatusCode httpStatusCode)
        {
            var type = typeof(DocumentClientException);

            var documentClientExceptionInstance = type.Assembly.CreateInstance(
                type.FullName,
                false,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] {error, exception, (HttpStatusCode?) httpStatusCode, null, null},
                null,
                null);

            return (DocumentClientException) documentClientExceptionInstance;
        }
    }
}