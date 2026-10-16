using System;

namespace OutOfOffice
{
    // Where the time comes from, so daily resets can be tested without waiting for midnight.
    public interface IClock
    {
        DateTime UtcNow { get; }
        DateTime ToLocal(DateTime utc);
    }

    public class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime ToLocal(DateTime utc) => utc.ToLocalTime();
    }

    // A clock set by hand, in a fixed time zone.
    public class FakeClock : IClock
    {
        public DateTime UtcNow { get; set; }
        public TimeSpan Offset { get; set; }

        public FakeClock(DateTime utcNow, TimeSpan offset = default)
        {
            UtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
            Offset = offset;
        }

        public void Advance(TimeSpan by) => UtcNow += by;
        public DateTime ToLocal(DateTime utc) => DateTime.SpecifyKind(utc + Offset, DateTimeKind.Local);
    }
}
