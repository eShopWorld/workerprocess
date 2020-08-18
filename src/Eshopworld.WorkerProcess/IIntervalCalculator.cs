using System;
namespace EShopworld.WorkerProcess
{
    public interface ISlottedInterval
    {
        TimeSpan Calculate(DateTime time, TimeSpan interval);
    }
}
