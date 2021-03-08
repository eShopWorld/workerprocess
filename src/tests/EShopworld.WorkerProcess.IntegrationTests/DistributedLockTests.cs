using System;
using System.IO;
using System.Threading.Tasks;
using Eshopworld.Tests.Core;
using EShopworld.WorkerProcess.Configuration;
using EShopworld.WorkerProcess.CosmosDistributedLock;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EShopworld.WorkerProcess.IntegrationTests
{
    public class DistributedLockTests
    {
        private readonly ServiceProvider _serviceProvider;

        public DistributedLockTests()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false, true)
                .Build();

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection, configuration);

            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        [Fact, IsIntegration]
        public void AcquireLock_ForNewLock_ShouldNotThrowException()
        {
            // Arrange
            Func<Task> act = async () =>
            {
                var distributedLock = _serviceProvider.GetService<IDistributedLock>();
                await using (await distributedLock.AcquireAsync("test1"))
                {
                    // critical section code
                }
            };

            // Act - Assert
            act.Should().NotThrow();
        }

        [Fact, IsIntegration]
        public void AcquireLock_ForExistingLockName_ShouldThrowException()
        {
            // Arrange
            Func<Task> act = async () =>
            {
                var distributedLock1 = _serviceProvider.GetService<IDistributedLock>();
                var distributedLock2 = _serviceProvider.GetService<IDistributedLock>();
                await using (await distributedLock1.AcquireAsync("test2"))
                {
                    await using (await distributedLock2.AcquireAsync("test2"))
                    {
                        // critical section code
                    }
                }
            };

            // Act - Assert
            act.Should().Throw<DistributedLockNotAcquiredException>();
        }

        [Fact, IsIntegration]
        public void AcquireLock_ForExistingLockOnSameObject_ShouldThrowException()
        {
            // Arrange
            Func<Task> act = async () =>
            {
                var distributedLock = _serviceProvider.GetService<IDistributedLock>();
                await using (await distributedLock.AcquireAsync("test3"))
                {
                    await using (await distributedLock.AcquireAsync("test456"))
                    {
                        // critical section code
                    }
                }
            };

            // Act - Assert
            act.Should().Throw<DistributedLockAlreadyAcquiredException>();
        }

        private void ConfigureServices(IServiceCollection services, IConfigurationRoot configuration)
        {
            services.AddCosmosDistributedLock(configuration);
        }
    }
}
