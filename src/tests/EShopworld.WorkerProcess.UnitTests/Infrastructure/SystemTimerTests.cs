using System;
using System.Threading;
using Eshopworld.Tests.Core;
using System.Threading.Tasks;
using EShopworld.WorkerProcess.Infrastructure;
using FluentAssertions;
using Xunit;

namespace EShopworld.WorkerProcess.UnitTests.Infrastructure
{
    public class SystemTimerTests
    {
        [Fact, IsUnit]
        public async Task SystemTimer_WhenLoopStarts_ExecutorShouldRun()
        {
            //Arrange
            var timer = new SystemTimer();
            

            //Act
            var isCalled = false;
            await timer.ExecutePeriodicallyIn(TimeSpan.Zero,()=>
            {
                timer.Stop();
                isCalled = true;
                return  Task.FromResult(TimeSpan.Zero);
            });

            //Assert
            isCalled.Should().BeTrue();
        }

        [Fact, IsUnit]
        public async Task SystemTimer_WhenTimerStops_ExecutorShouldNotRun()
        {
            //Arrange
            var timer = new SystemTimer();
            timer.Stop();

            //Act
            var isCalled = false;
            await timer.ExecutePeriodicallyIn(TimeSpan.Zero, () =>
            {
                isCalled = true;
                return Task.FromResult(TimeSpan.Zero);
            });

            //Assert
            isCalled.Should().BeFalse();
        }
    }
}
