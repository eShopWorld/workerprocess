using System;
using System.Threading.Tasks;
using Eshopworld.Tests.Core;
using EShopworld.WorkerProcess.Configuration;
using EShopworld.WorkerProcess.CosmosDistributedLock;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EShopworld.WorkerProcess.UnitTests.CosmosDistributedLock
{
    public class DistributedLockTests
    {
        private readonly Mock<ICosmosDistributedLockStore> _cosmosDbLockStore;
        private readonly DistributedLock _distributedLock;

        public DistributedLockTests()
        {
            var options = Options.Create(new CosmosDataStoreOptions
            {
                DistributedLocksCollection = "test-collection"
            });
            _cosmosDbLockStore = new Mock<ICosmosDistributedLockStore>();
            _distributedLock = new DistributedLock(_cosmosDbLockStore.Object, options);

        }

        [Theory, IsUnit]
        [InlineData("")]
        [InlineData(null)]
        public void Acquire_WhenInvalidLockName_ThrowsException(string lockName)
        {
            // Arrange
            Func<Task> act = async () => await _distributedLock.AcquireAsync(lockName);
            
            // Act - Assert
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact, IsUnit]
        public async Task Acquire_WhenTryClaimLockAsyncSucceeds_DistributedLockObjectIsReturned()
        {
            // Arrange
            _cosmosDbLockStore.Setup(_ => _.TryClaimLockAsync(It.IsAny<IDistributedLockClaim>()))
                .Returns(Task.FromResult(true));

            // Act
            await using (var result = await _distributedLock.AcquireAsync("blah"))
            {
                // Assert
                result.Should().BeSameAs(_distributedLock);
                _cosmosDbLockStore.Verify(x => x.TryClaimLockAsync(It.IsAny<IDistributedLockClaim>()), Times.Once);
            }

            _cosmosDbLockStore.Verify(x => x.ReleaseLockAsync(It.IsAny<IDistributedLockClaim>()), Times.Once);
        }

        [Fact, IsUnit]
        public void Acquire_WhenTryClaimLockAsyncReturnsFalse_ExceptionIsThrown()
        {
            // Arrange
            _cosmosDbLockStore.Setup(_ => _.TryClaimLockAsync(It.IsAny<IDistributedLockClaim>()))
                .Returns(Task.FromResult(false));

            Func<Task> act = async () => await _distributedLock.AcquireAsync("blah");

            // Act - Assert
            act.Should().Throw<DistributedLockNotAcquiredException>()
                .WithMessage("Distributed Lock for document with id: 'blah' in collection 'test-collection' could not be acquired.");

            _cosmosDbLockStore.Verify(x => x.TryClaimLockAsync(It.IsAny<IDistributedLockClaim>()), Times.Once);
            _cosmosDbLockStore.Verify(x => x.ReleaseLockAsync(It.IsAny<IDistributedLockClaim>()), Times.Never);
        }

        [Fact, IsUnit]
        public void Acquire_WhenTryClaimLockAsyncFails_ExceptionIsThrown()
        {
            // Arrange
            _cosmosDbLockStore.Setup(_ => _.TryClaimLockAsync(It.IsAny<IDistributedLockClaim>()))
                .Throws<Exception>();

            Func<Task> act = async () => await _distributedLock.AcquireAsync("blah");

            // Act - Assert
            act.Should().Throw<DistributedLockNotAcquiredException>()
                .WithMessage("Distributed Lock for document with id: 'blah' in collection 'test-collection' could not be acquired.")
                .WithInnerException<Exception>()
                .WithMessage("Exception of type 'System.Exception' was thrown.");

            _cosmosDbLockStore.Verify(x=>x.TryClaimLockAsync(It.IsAny<IDistributedLockClaim>()), Times.Once);
            _cosmosDbLockStore.Verify(x => x.ReleaseLockAsync(It.IsAny<IDistributedLockClaim>()), Times.Never);
        }

        [Fact, IsUnit]
        public async Task Acquire_WhenLockAlreadyAcquired_ThrowsException()
        {
            // Arrange
            _cosmosDbLockStore.Setup(_ => _.TryClaimLockAsync(It.IsAny<IDistributedLockClaim>()))
                .Returns(Task.FromResult(true));
            
            await _distributedLock.AcquireAsync("blah");

            Func<Task> act = async () => await _distributedLock.AcquireAsync("blah");

            // Act - Assert
            act.Should().Throw<DistributedLockAlreadyAcquiredException>()
                .WithMessage("Distributed Lock with name 'blah' was already acquired on the current object.");

            _cosmosDbLockStore.Verify(x => x.TryClaimLockAsync(It.IsAny<IDistributedLockClaim>()), Times.Once);
            _cosmosDbLockStore.Verify(x => x.ReleaseLockAsync(It.IsAny<IDistributedLockClaim>()), Times.Never);
        }
    }
}
