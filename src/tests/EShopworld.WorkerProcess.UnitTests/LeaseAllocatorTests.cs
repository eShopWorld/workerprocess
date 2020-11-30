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
        private readonly Mock<ISlottedInterval> _mockSlottedInterval;
        private readonly Mock<IBigBrother> _mockTelemetry;
        private readonly Mock<ILeaseStore> _mockStore;
        private readonly WorkerLeaseOptions _options;
        private readonly LeaseAllocator _leaseAllocator;

        public LeaseAllocatorTests()
        {
            _mockSlottedInterval = new Mock<ISlottedInterval>();
            _mockTelemetry = new Mock<IBigBrother>();
            _mockStore = new Mock<ILeaseStore>();

            _options = new WorkerLeaseOptions
            {
                LeaseInterval = new TimeSpan(0, 2, 0),
                Priority = 1,
                WorkerType = "workertype",
                ElectionDelay = TimeSpan.Zero
            };

            _leaseAllocator = new LeaseAllocator(_mockTelemetry.Object, _mockStore.Object, _mockSlottedInterval.Object, Options.Create(_options));
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
        public async Task TestAllocateLeaseAsyncExistingExpiredLeaseShouldUpdateLease()
        {
            // Arrange
            DateTime currentDateTime = new DateTime(2000, 1, 1, 12, 0, 0);
            ServerDateTime.UtcNowFunc = () => currentDateTime;

            var lease = new TestLease
            {
                Id = "abc",
                InstanceId = Guid.NewGuid(),
                LeasedUntil = currentDateTime.Subtract(new TimeSpan(1, 0, 0)),
                Priority = _options.Priority + 1,
                Interval = TimeSpan.FromMinutes(2),
                LeaseType = _options.WorkerType
            };

            _mockStore.Setup(m => m.ReadByLeaseTypeAsync(It.IsAny<string>())).ReturnsAsync(lease);

            _mockStore.Setup(m =>
                    m.SelectWinnerRequestAsync(lease.LeaseType))
                .ReturnsAsync(lease.InstanceId);

            _mockStore.Setup(m => m.TryUpdateLeaseAsync(It.IsAny<ILease>()))
                .ReturnsAsync(new LeaseStoreResult(lease, true));

            _mockSlottedInterval.Setup(m => m.Calculate(It.IsAny<DateTime>(), It.IsAny<TimeSpan>()))
                .Returns(TimeSpan.FromMinutes(2));

            // Act
            var result = await _leaseAllocator.AllocateLeaseAsync(lease.InstanceId.Value);

            // Assert
            result.Should().Be(lease);
         
            _mockStore.Verify(m => m.TryCreateLeaseAsync(It.IsAny<ILease>()), Times.Never);
            _mockStore.Verify(m => m.SelectWinnerRequestAsync(lease.LeaseType), Times.Once);
            _mockStore.Verify(m => m.TryUpdateLeaseAsync(lease), Times.Once);
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
                InstanceId = Guid.NewGuid(),
                LeasedUntil = currentDateTime.Subtract(new TimeSpan(1, 0, 0)),
                Priority = _options.Priority + 1,
                Interval = TimeSpan.FromMinutes(2),
                LeaseType = _options.WorkerType
            };

            _mockStore.Setup(m => m.ReadByLeaseTypeAsync(It.IsAny<string>())).ReturnsAsync((TestLease) null);

            _mockStore.Setup(m =>
                    m.TryCreateLeaseAsync(It.IsAny<ILease>()))
                .ReturnsAsync(new LeaseStoreResult(lease, true));

            _mockStore.Setup(m =>
                    m.SelectWinnerRequestAsync(lease.LeaseType))
                .ReturnsAsync(lease.InstanceId);

            _mockSlottedInterval.Setup(m => m.Calculate(It.IsAny<DateTime>(), It.IsAny<TimeSpan>()))
                .Returns(TimeSpan.FromMinutes(2));

            // Act
            var result = await _leaseAllocator.AllocateLeaseAsync(lease.InstanceId.Value);

            //Assert
            result.Should().Be(lease);
           
            _mockStore.Verify(m => m.TryCreateLeaseAsync(It.Is<ILease>(l => l.InstanceId == result.InstanceId && l.LeaseType == result.LeaseType)), Times.Once);
            _mockStore.Verify(m => m.SelectWinnerRequestAsync(lease.LeaseType), Times.Once);
            _mockStore.Verify(m => m.TryUpdateLeaseAsync(It.IsAny<ILease>()), Times.Never);
            
        }

        [Fact, IsUnit]
        public async Task TestAllocateLeaseAsync_OnLeaseUpdateConflict_ShouldFail()
        {
            // Arrange
            DateTime currentDateTime = new DateTime(2000, 1, 1, 12, 0, 0);
            ServerDateTime.UtcNowFunc = () => currentDateTime;
            var instanceId = Guid.NewGuid();

            _mockStore.Setup(m =>
                    m.TryUpdateLeaseAsync(It.IsAny<ILease>()))
                .ReturnsAsync(new LeaseStoreResult(null, false));

            _mockStore.SetupSequence(m => m.ReadByLeaseTypeAsync(It.IsAny<string>()))
                .ReturnsAsync(new TestLease
                {
                    Id = Guid.NewGuid().ToString(),
                    InstanceId = instanceId,
                    LeasedUntil = currentDateTime.Subtract(TimeSpan.FromMinutes(5)),
                    Priority = 0
                })
                .ReturnsAsync(new TestLease
                {
                    Id=Guid.NewGuid().ToString(),
                    InstanceId = Guid.NewGuid(),
                    LeasedUntil = currentDateTime.Add(_options.LeaseInterval),
                    Priority = 0
                });

            _mockStore.Setup(m =>
                    m.SelectWinnerRequestAsync(It.IsAny<string>()))
                .ReturnsAsync(instanceId);


            // Act
            var result = await _leaseAllocator.AllocateLeaseAsync(instanceId);

            // Assert
            result.Should().BeNull();
            _mockStore.Verify(m => m.ReadByLeaseTypeAsync(It.IsAny<string>()), Times.Exactly(2));
            _mockStore.Verify(m => m.TryCreateLeaseAsync(It.Is<ILease>(l => l.InstanceId == instanceId)), Times.Never);
            _mockStore.Verify(m => m.SelectWinnerRequestAsync(It.IsAny<string>()), Times.Once);
            _mockStore.Verify(m => m.TryUpdateLeaseAsync(It.IsAny<ILease>()), Times.Once);
        }

        [Fact, IsUnit]
        public async Task TestAllocateLeaseAsync_OnLeaseCreateConflict_ShouldFail()
        {
            // Arrange
            DateTime currentDateTime = new DateTime(2000, 1, 1, 12, 0, 0);
            ServerDateTime.UtcNowFunc = () => currentDateTime;
            var instanceId = Guid.NewGuid();
            var lease = new TestLease
            {
                LeasedUntil = currentDateTime.Add(_options.LeaseInterval),
                Priority = _options.Priority,
                InstanceId = instanceId
            };

            _mockStore.Setup(m =>
                    m.TryCreateLeaseAsync(It.IsAny<ILease>()))
                .ReturnsAsync(new LeaseStoreResult(null, false));

            _mockStore.Setup(m =>
                    m.TryUpdateLeaseAsync(It.IsAny<ILease>()))
                .ReturnsAsync(new LeaseStoreResult(lease, true));

            _mockStore.SetupSequence(m => m.ReadByLeaseTypeAsync(It.IsAny<string>()))
                .ReturnsAsync((ILease)null)
                .ReturnsAsync(new TestLease
                {
                    LeasedUntil = currentDateTime.Add(_options.LeaseInterval),
                    Priority = 2,
                    InstanceId = instanceId
                });

            _mockStore.Setup(m =>
                    m.SelectWinnerRequestAsync(It.IsAny<string>()))
                .ReturnsAsync(instanceId);


            // Act
            var result = await _leaseAllocator.AllocateLeaseAsync(instanceId);

            // Assert
            result.Should().Be(null);
            _mockStore.Verify(m => m.ReadByLeaseTypeAsync(It.IsAny<string>()), Times.Exactly(2));
            _mockStore.Verify(m => m.TryCreateLeaseAsync(It.Is<ILease>(l => l.InstanceId == instanceId)), Times.Once);
            _mockStore.Verify(m => m.SelectWinnerRequestAsync(It.IsAny<string>()), Times.Once);
            _mockStore.Verify(m => m.TryUpdateLeaseAsync(It.IsAny<ILease>()), Times.Never);
        }


        [Fact, IsUnit]
        public async Task TestAllocateLeaseAsync_OnLeaseCreate_ShouldSucceed()
        {
            // Arrange
            DateTime currentDateTime = new DateTime(2000, 1, 1, 12, 0, 0);
            ServerDateTime.UtcNowFunc = () => currentDateTime;
            var instanceId = Guid.NewGuid();
            ILease lease = new TestLease
            {
                Priority = _options.Priority,
                LeaseType = _options.WorkerType,
                InstanceId = instanceId
            };

            _mockStore.Setup(m => m.ReadByLeaseTypeAsync(It.IsAny<string>()))
                .ReturnsAsync((ILease) null);

            _mockStore.Setup(m => m.TryCreateLeaseAsync(
                    It.Is<ILease>(l => l.LeaseType == _options.WorkerType && l.Priority==_options.Priority && l.InstanceId== lease.InstanceId)))
                .ReturnsAsync(new LeaseStoreResult(lease, true));

            _mockStore.Setup(m =>
                    m.SelectWinnerRequestAsync(It.IsAny<string>()))
                .ReturnsAsync(lease.InstanceId);


            // Act
            var result = await _leaseAllocator.AllocateLeaseAsync(lease.InstanceId.Value);

            // Assert
            result.Should().Be(lease);
            _mockStore.Verify(m => m.ReadByLeaseTypeAsync(It.IsAny<string>()), Times.Once);
            _mockStore.Verify(m => m.TryCreateLeaseAsync(It.Is<ILease>(l => l.LeaseType == _options.WorkerType && l.Priority == _options.Priority && l.InstanceId == lease.InstanceId)), Times.Once);
            _mockStore.Verify(m => m.SelectWinnerRequestAsync(It.IsAny<string>()), Times.Once);
            _mockStore.Verify(m => m.TryUpdateLeaseAsync(It.IsAny<ILease>()), Times.Never);
        }

        [Fact, IsUnit]
        public async Task TestAllocateLeaseAsync_OnLeaseUpdate_ShouldSucceed()
        {
            // Arrange
            DateTime currentDateTime = new DateTime(2000, 1, 1, 12, 0, 0);
            ServerDateTime.UtcNowFunc = () => currentDateTime;

            ILease lease = new TestLease
            {
                Id = "Abc",
                Priority = _options.Priority,
                LeaseType = _options.WorkerType,
                InstanceId = Guid.NewGuid()
            };

            _mockStore.Setup(m => m.ReadByLeaseTypeAsync(It.IsAny<string>()))
                .ReturnsAsync(lease);

            _mockStore.Setup(m =>
                    m.SelectWinnerRequestAsync(It.IsAny<string>()))
                .ReturnsAsync(lease.InstanceId);


            _mockStore.Setup(m => m.TryUpdateLeaseAsync(It.IsAny<ILease>()))
                .ReturnsAsync(new LeaseStoreResult(lease, true));

            // Act
            var result = await _leaseAllocator.AllocateLeaseAsync(lease.InstanceId.Value);

            // Assert
            result.Should().Be(lease);
            _mockStore.Verify(m => m.ReadByLeaseTypeAsync(It.IsAny<string>()), Times.Exactly(1));
            _mockStore.Verify(m => m.TryUpdateLeaseAsync(It.Is<ILease>(l => l.LeaseType == _options.WorkerType && l.Priority == _options.Priority && l.InstanceId == lease.InstanceId)), Times.Once);
            _mockStore.Verify(m => m.SelectWinnerRequestAsync(It.IsAny<string>()), Times.Once);
            _mockStore.Verify(m => m.TryCreateLeaseAsync(It.IsAny<ILease>()), Times.Never);
        }

        [Fact, IsUnit]
        public async Task TestAllocateLeaseAsyncFailsAfterLosingElection()
        {
            // Arrange
            DateTime currentDateTime = new DateTime(2000, 1, 1, 12, 0, 0);
            ServerDateTime.UtcNowFunc = () => currentDateTime;
            var instanceId = Guid.NewGuid();

            _mockStore.Setup(m =>
                    m.TryCreateLeaseAsync(It.IsAny<ILease>()))
                .ReturnsAsync(new LeaseStoreResult(null, false));

            _mockStore.SetupSequence(m => m.ReadByLeaseTypeAsync(It.IsAny<string>()))
                .ReturnsAsync((ILease) null);

            _mockStore.Setup(m =>
                    m.SelectWinnerRequestAsync(It.IsAny<string>()))
                .ReturnsAsync(Guid.Empty);


            // Act
            var result = await _leaseAllocator.AllocateLeaseAsync(instanceId);

            // Assert
            result.Should().BeNull();
            _mockStore.Verify(m => m.ReadByLeaseTypeAsync(It.IsAny<string>()), Times.Once);
            _mockStore.Verify(m => m.TryCreateLeaseAsync(It.IsAny<ILease>()), Times.Never);
            _mockStore.Verify(m => m.SelectWinnerRequestAsync(It.IsAny<string>()), Times.Once);
            _mockStore.Verify(m => m.TryUpdateLeaseAsync(It.IsAny<ILease>()), Times.Never);
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