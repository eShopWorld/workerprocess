using System;

namespace EShopworld.WorkerProcess
{
    public class SlottedInterval: ISlottedInterval
    {
        public TimeSpan Calculate(DateTime time, TimeSpan interval)
        {
            return TimeSpan.FromMilliseconds(interval.TotalMilliseconds -
                   time.TimeOfDay.TotalMilliseconds / interval.TotalMilliseconds % 1 *
                   interval.TotalMilliseconds);
        }
    }
}
