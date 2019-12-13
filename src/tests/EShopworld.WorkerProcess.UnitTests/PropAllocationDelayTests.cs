using System;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Xunit;

namespace EShopworld.WorkerProcess.UnitTests
{
    public class PropAllocationDelayTests
    {
        private readonly ProportionalAllocationDelay _delay;

        public PropAllocationDelayTests()
        {
            _delay = new ProportionalAllocationDelay();
        }

        [Theory, IsUnit]
        [InlineData(0, 16666666L)]
        [InlineData(1, 18750000L)]
        [InlineData(2, 21428571L)]
        [InlineData(3, 25000000L)]
        [InlineData(4, 30000000L)]
        [InlineData(5, 37500000L)]
        [InlineData(6, 50000000L)]
        [InlineData(7, 75000000L)]
        public void TestDelay(int priority, long expectedDelayTicks)
        {
            // Arrange

            // Act
            var result = _delay.Calculate(priority, TimeSpan.FromMinutes(1));

            // Assert
            result.Should().Be(new TimeSpan(expectedDelayTicks));
        }
    }
}
