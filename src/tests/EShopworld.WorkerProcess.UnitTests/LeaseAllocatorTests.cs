using System;
using System.Threading.Tasks;
using Eshopworld.Core;
using Eshopworld.Tests.Core;
using EShopworld.WorkerProcess.Configuration;
using EShopworld.WorkerProcess.Infrastructure;
using EShopworld.WorkerProcess.Stores;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EShopworld.WorkerProcess.UnitTests
{
    public class LeaseAllocatorTests
    {
        private readonly Mock<IAllocationDelay> _mockAllocationDelay;
        private readonly Mock<ISlottedInterval> _mockSlottedInterval;
        private readonly Mock<IBigBrother> _mockTelemetry;
        private readonly Mock<ILeaseStore> _mockStore;
        private readonly WorkerLeaseOptions _options;
        private readonly LeaseAllocator _leaseAllocator;

        public LeaseAllocatorTests()
        {
            _mockAllocationDelay = new Mock<IAllocationDelay>();
            _mockSlottedInterval = new Mock<ISlottedInterval>();
            _mockTelemetry = new Mock<IBigBrother>();
            _mockStore = new Mock<ILeaseStore>();

            _options = new WorkerLeaseOptions
            {
                LeaseInterval = new TimeSpan(0, 2, 0),
                Priority = 1,
                WorkerType = "workertype"
            };

            _leaseAllocator = new LeaseAllocator(_mockTelemetry.Object, _mockStore.Object, _mockSlottedInterval.Object, _mockAllocationDelay.Object,
                Options.Create(_options));
        }

        [Fact, IsUnit]
        public async Task TestAllocateLeaseAsyncExistingActiveLease()
        {
            // Arrange
            DateTime currentDateTime = new DateTime(2000, 1, 1, 12, 0, 0);
            ServerDateTime.UtcNowFunc = () => currentDateTime;

            _mockStore.Setup(m => m.ReadByLeaseTypeAsync(It.IsAny<string>())).ReturnsAsync(new TestLease
            {
                InstanceId = Guid.NewGuid(),
                LeasedUntil = currentDateTime.Add(new TimeSpan(1, 0, 0)),
                Priority = _options.Priority + 1,
                LeaseType = _options.WorkerType
            });

            // Act
            var result = await _leaseAllocator.AllocateLeaseAsync(Guid.NewGuid());

            // Assert
            result.Should().BeNull();
        }

        [Fact, IsUnit]
        public async Task TestAllocateLeaseAsyncExistingExpiredLeaseLeaseCreated()
        {
            // Arrange
            DateTime currentDateTime = new DateTime(2000, 1, 1, 12, 0, 0);
            ServerDateTime.UtcNowFunc = () => currentDateTime;

            var lease = new TestLease
            {
                InstanceId = Guid.NewGuid(),
                LeasedUntil = currentDateTime.Subtract(new TimeSpan(1, 0, 0)),
                Priority = _options.Priority + 1,
                Interval = TimeSpan.FromMinutes(2),
                LeaseType = _options.WorkerType
            };

            _mockStore.Setup(m => m.ReadByLeaseTypeAsync(It.IsAny<string>())).ReturnsAsync(lease);

            _mockStore.Setup(m =>
                    m.TryCreateLeaseAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>()))
                .ReturnsAsync(new LeaseStoreResult(lease, true));

            _mockStore.SetupSequence(m => m.TryUpdateLeaseAsync(It.IsAny<ILease>()))
                .ReturnsAsync(new LeaseStoreResult(lease, true))
                .ReturnsAsync(new LeaseStoreResult(lease, true));

            _mockSlottedInterval.Setup(m => m.Calculate(It.IsAny<DateTime>(), It.IsAny<TimeSpan>()))
                .Returns(TimeSpan.FromMinutes(2));

            // Act
            var result = await _leaseAllocator.AllocateLeaseAsync(Guid.NewGuid());

            // Assert
            result.Should().NotBeNull();
            result.Priority.Should().Be(_options.Priority);
            result.LeasedUntil.Should().NotBeNull();
            result.LeasedUntil.GetValueOrDefault().Should().Be(currentDateTime.Add(_options.LeaseInterval));
            _mockAllocationDelay.Verify(m => m.Calculate(It.Is<int>(i => i == _options.Priority),
                It.Is<TimeSpan>(t => t == _options.LeaseInterval)));
        }

        [Fact, IsUnit]
        public async Task TestAllocateLeaseAsyncExistingHigherPriorityLease()
        {
            // Arrange
            DateTime currentDateTime = new DateTime(2000, 1, 1, 12, 0, 0);
            ServerDateTime.UtcNowFunc = () => currentDateTime;

            _mockStore.Setup(m => m.ReadByLeaseTypeAsync(It.IsAny<string>())).ReturnsAsync(new TestLease
            {
                InstanceId = Guid.NewGuid(),
                LeasedUntil = currentDateTime.Add(new TimeSpan(1, 0, 0)),
                Priority = 0,
                LeaseType = _options.WorkerType
            });

            // Act
            var result = await _leaseAllocator.AllocateLeaseAsync(Guid.NewGuid());

            // Assert
            result.Should().BeNull();
        }

        [Fact, IsUnit]
        public async Task TestAllocateLeaseAsyncExistingLowerPriorityLease()
        {
            // Arrange
            DateTime currentDateTime = new DateTime(2000, 1, 1, 12, 0, 0);
            ServerDateTime.UtcNowFunc = () => currentDateTime;

            _mockStore.Setup(m => m.ReadByLeaseTypeAsync(It.IsAny<string>())).ReturnsAsync(new TestLease
            {
                InstanceId = Guid.NewGuid(),
                LeasedUntil = null,
                Priority = _options.Priority + 1,
                LeaseType = _options.WorkerType
            });

            // Act
            var result = await _leaseAllocator.AllocateLeaseAsync(Guid.NewGuid());

            // Assert
            result.Should().BeNull();
        }

        [Fact, IsUnit]
        public async Task TestAllocateLeaseAsyncNoExistingLeaseCreateLease()
        {
            // Arrange
            DateTime currentDateTime = new DateTime(2000, 1, 1, 12, 0, 0);
            ServerDateTime.UtcNowFunc = () => currentDateTime;

            ILease lease = new TestLease
            {
                Priority = 0
            };

            _mockStore.Setup(m => m.ReadByLeaseTypeAsync(It.IsAny<string>())).ReturnsAsync((TestLease) null);

            _mockStore.Setup(m =>
                    m.TryCreateLeaseAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>()))
                .ReturnsAsync(new LeaseStoreResult(lease, true));

            _mockStore.SetupSequence(m => m.TryUpdateLeaseAsync(It.IsAny<ILease>()))
                .ReturnsAsync(new LeaseStoreResult(lease, true))
                .ReturnsAsync(new LeaseStoreResult(lease, true));

            _mockSlottedInterval.Setup(m => m.Calculate(It.IsAny<DateTime>(), It.IsAny<TimeSpan>()))
                .Returns(TimeSpan.FromMinutes(2));

            // Act
            var result = await _leaseAllocator.AllocateLeaseAsync(Guid.NewGuid());

            // Assert
            result.Should().NotBeNull();
            result.Priority.Should().Be(_options.Priority);
            result.LeasedUntil.Should().NotBeNull();
            result.LeasedUntil.GetValueOrDefault().Should().Be(currentDateTime.Add(_options.LeaseInterval));
            _mockAllocationDelay.Verify(m => m.Calculate(It.Is<int>(i => i == _options.Priority),
                It.Is<TimeSpan>(t => t == _options.LeaseInterval)));
        }

        [Fact, IsUnit]
        public async Task TestAllocateLeaseAsyncLeaseCreateSecondWorkerLeaseCreated()
        {
            // Arrange
            DateTime currentDateTime = new DateTime(2000, 1, 1, 12, 0, 0);
            ServerDateTime.UtcNowFunc = () => currentDateTime;

            _mockStore.Setup(m =>
                    m.TryCreateLeaseAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Guid>()))
                .ReturnsAsync(new LeaseStoreResult(null, false));

            _mockStore.SetupSequence(m => m.ReadByLeaseTypeAsync(It.IsAny<string>()))
                .ReturnsAsync((ILease) null)
                .ReturnsAsync(new TestLease
                {
                    LeasedUntil = currentDateTime.Add(_options.LeaseInterval),
                    Priority = 2
                });

            // Act
            var result = await _leaseAllocator.AllocateLeaseAsync(Guid.NewGuid());

            // Assert
            result.Should().BeNull();
        }

        [Fact, IsUnit]
        public async Task TestAllocateLeaseAsyncLeaseAcquireFailed()
        {
            // Arrange
            DateTime currentDateTime = new DateTime(2000, 1, 1, 12, 0, 0);
            ServerDateTime.UtcNowFunc = () => currentDateTime;

            ILease lease = new TestLease
            {
                Priority = 0
            };

            _mockStore.SetupSequence(m => m.ReadByLeaseTypeAsync(It.IsAny<string>()))
                .ReturnsAsync((ILease) null)
                .ReturnsAsync(lease)
                .ReturnsAsync(lease);

            _mockStore.Setup(m => m.TryCreateLeaseAsync(
                    It.Is<string>(s => s == _options.WorkerType),
                    It.Is<int>(i => i == _options.Priority),
                    It.IsAny<Guid>()))
                .ReturnsAsync(new LeaseStoreResult(null, false));

            _mockStore.SetupSequence(m => m.TryUpdateLeaseAsync(It.IsAny<ILease>()))
                .ReturnsAsync(new LeaseStoreResult(null, false));

            // Act
            var result = await _leaseAllocator.AllocateLeaseAsync(Guid.NewGuid());

            // Assert
            result.Should().BeNull();
        }

        [Fact, IsUnit]
        public async Task TestAllocateLeaseAsyncLeaseActivationFailed()
        {
            // Arrange
            DateTime currentDateTime = new DateTime(2000, 1, 1, 12, 0, 0);
            ServerDateTime.UtcNowFunc = () => currentDateTime;

            ILease lease = new TestLease
            {
                Priority = 0
            };

            _mockStore.SetupSequence(m => m.ReadByLeaseTypeAsync(It.Is<string>(s => s == _options.WorkerType)))
                .ReturnsAsync((ILease) null)
                .ReturnsAsync(lease)
                .ReturnsAsync(lease);

            _mockStore.Setup(m => m.TryCreateLeaseAsync(
                    It.Is<string>(s => s == _options.WorkerType),
                    It.Is<int>(i => i == _options.Priority),
                    It.IsAny<Guid>()))
                .ReturnsAsync(new LeaseStoreResult(null, false));

            _mockStore.SetupSequence(m => m.TryUpdateLeaseAsync(It.IsAny<ILease>()))
                .ReturnsAsync(new LeaseStoreResult(lease, true))
                .ReturnsAsync(new LeaseStoreResult(null, false));

            // Act
            var result = await _leaseAllocator.AllocateLeaseAsync(Guid.NewGuid());

            // Assert
            result.Should().BeNull();
        }

        [Fact, IsUnit]
        public async Task TestReleaseLeaseAsyncUpdateFailed()
        {
            // Arrange
            _mockStore.Setup(m =>
                    m.TryUpdateLeaseAsync(It.Is<ILease>(l =>
                        l.Priority == -1 && !l.LeasedUntil.HasValue && !l.InstanceId.HasValue)))
                .ReturnsAsync(new LeaseStoreResult(null, false));

            // Act
            await _leaseAllocator.ReleaseLeaseAsync(new TestLease
            {
                InstanceId = Guid.NewGuid()
            });

            // Assert
            _mockStore.Verify(m =>
                m.TryUpdateLeaseAsync(It.Is<ILease>(l =>
                    l.Priority == -1 && !l.LeasedUntil.HasValue && !l.InstanceId.HasValue)));
        }

        [Fact, IsUnit]
        public async Task TestReleaseLeaseAsyncUpdated()
        {
            var lease = new TestLease
            {
                InstanceId = Guid.NewGuid()
            };

            // Arrange
            _mockStore.Setup(m =>
                    m.TryUpdateLeaseAsync(It.Is<ILease>(l =>
                        l.Priority == -1 && !l.LeasedUntil.HasValue && !l.InstanceId.HasValue)))
                .ReturnsAsync(new LeaseStoreResult(lease, true));

            // Act
            await _leaseAllocator.ReleaseLeaseAsync(lease);

            // Assert
            _mockStore.Verify(m =>
                m.TryUpdateLeaseAsync(It.Is<ILease>(l =>
                    l.Priority == -1 && !l.LeasedUntil.HasValue && !l.InstanceId.HasValue)));
        }
    }
}