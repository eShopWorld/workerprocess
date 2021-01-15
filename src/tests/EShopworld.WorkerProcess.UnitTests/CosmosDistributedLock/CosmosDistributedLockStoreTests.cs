using System;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Eshopworld.Core;
using Eshopworld.Tests.Core;
using EShopworld.WorkerProcess.Configuration;
using EShopworld.WorkerProcess.CosmosDistributedLock;
using EShopworld.WorkerProcess.UnitTests.Stores;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EShopworld.WorkerProcess.UnitTests.CosmosDistributedLock
{
    public class CosmosDistributedLockStoreTests
    {
        private readonly Mock<IDocumentClient> _mockDocumentClient;
        private readonly CosmosDataStoreOptions _options;
        private readonly CosmosDistributedLockStore _store;

        public CosmosDistributedLockStoreTests()
        {
            _mockDocumentClient = new Mock<IDocumentClient>();
            var mockTelemetry = new Mock<IBigBrother>();

            _options = new CosmosDataStoreOptions
            {
                DistributedLocksCollection = "lockCollection",
                Database = "database"
            };

            _store = new CosmosDistributedLockStore(_mockDocumentClient.Object,
                Options.Create(_options), mockTelemetry.Object);
        }

        [Fact, IsUnit]
        public async Task InitialiseAsync_CreateDatabaseAndCreateCollectionIsCalled()
        {
            // Act
            await _store.InitialiseAsync(_mockDocumentClient.Object, _options);

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
                        It.Is<DocumentCollection>(col => col.Id.Equals(_options.DistributedLocksCollection)),
                        It.IsAny<RequestOptions>()), Times.Once);
            }
        }

        [Fact, IsUnit]
        public async Task InitialiseAsync_DistributedLocksCollectionIsCreatedWithPartition()
        {
            // Act
            await _store.InitialiseAsync(_mockDocumentClient.Object, _options);

            // Assert
            _mockDocumentClient.Verify(
                client => client.CreateDocumentCollectionIfNotExistsAsync(
                    It.IsAny<Uri>(),
                    It.Is<DocumentCollection>(collection =>
                        collection.Id.Equals(_options.DistributedLocksCollection) && collection.PartitionKey.Paths.Count == 1),
                    It.IsAny<RequestOptions>()), Times.Once);
        }

        [Fact, IsUnit]
        public async Task TryClaimLockAsync_WhenLockCreated_ReturnsTrue()
        {
            // Arrange
            var lease = new CosmosDistributedLockClaim("lock-name");

            _mockDocumentClient.Setup(m => m.CreateDocumentAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<object>(),
                    It.IsAny<RequestOptions>(), false, default))
                .ReturnsAsync(new Document().ToResourceResponse(HttpStatusCode.Created));

            // Act
            var result = await _store.TryClaimLockAsync(lease);

            // Assert
            result.Should().BeTrue();

        }

        [Fact, IsUnit]
        public async Task TryClaimLockAsync_WhenLockConflicted_ReturnsFalse()
        {
            // Arrange
            var lease = new CosmosDistributedLockClaim("lock-name");

            _mockDocumentClient.Setup(m => m.CreateDocumentAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<object>(),
                    It.IsAny<RequestOptions>(), false, default))
                .ReturnsAsync(new Document().ToResourceResponse(HttpStatusCode.Conflict));

            // Act
            var result = await _store.TryClaimLockAsync(lease);

            // Assert
            result.Should().BeFalse();

        }

        [Fact, IsUnit]
        public void TryClaimLockAsync_OnDocumentClientException_ThrowsException()
        {
            // Arrange
            var lease = new CosmosDistributedLockClaim("lock-name");

            _mockDocumentClient.Setup(m => m.CreateDocumentAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<object>(),
                    It.IsAny<RequestOptions>(),
                    false,
                    default))
                .Throws(new Exception("http error"));

            // Act
            Func<Task> act = async () => await _store.TryClaimLockAsync(lease);

            // Assert
            act.Should().Throw<Exception>()
                .WithMessage("http error");

        }

        [Fact, IsUnit]
        public void ReleaseLockAsync_OnDocumentClientSuccess_ThrowsNoException()
        {
            // Arrange
            var lease = new CosmosDistributedLockClaim("lock-name");

            _mockDocumentClient.Setup(m => m.DeleteDocumentAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<RequestOptions>(), default))
                .ReturnsAsync(new Document().ToResourceResponse(HttpStatusCode.OK));

            // Act
            Func<Task> act = async () => await _store.ReleaseLockAsync(lease);

            // Assert
            act.Should().NotThrow();
        }

        [Fact, IsUnit]
        public void ReleaseLockAsync_OnDocumentClientFailure_ThrowsException()
        {
            // Arrange
            var lease = new CosmosDistributedLockClaim("lock-name");
            _mockDocumentClient.Setup(m => m.DeleteDocumentAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<RequestOptions>(), default))
                .Throws(CreateDocumentClientExceptionForTesting("http error", null, HttpStatusCode.RequestTimeout));

            // Act
            Func<Task> act = async () => await _store.ReleaseLockAsync(lease);

            // Assert
            act.Should().Throw<DocumentClientException>()
                .WithMessage("http error*");
        }
        
        private DocumentClientException CreateDocumentClientExceptionForTesting(
            string error,
            Exception exception,
            HttpStatusCode httpStatusCode)
        {
            var type = typeof(DocumentClientException);

            var documentClientExceptionInstance = type.Assembly.CreateInstance(
                type.FullName!,
                false,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { error, exception, (HttpStatusCode?)httpStatusCode, null, null },
                null,
                null);

            return (DocumentClientException)documentClientExceptionInstance;
        }
    }
}
