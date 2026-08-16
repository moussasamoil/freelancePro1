using System;
using System.Collections.Generic;

namespace lotus_blue.Services.Bonus
{
    public class BonusWindow
    {
        // Inclusive lower bound of the window the page is showing.
        public DateTime DisplayStart { get; set; }

        // Exclusive upper bound.
        public DateTime DisplayEnd { get; set; }

        // Monthly cycles (1st@10am .. next 1st@10am) inside the window.
        // For date-filtered windows this is a single tuple equal to (DisplayStart, DisplayEnd).
        // For default (unfiltered) windows this lists every cycle from the system epoch
        // (April 1 2026 10am) through the current cycle's end, in chronological order.
        // Pro-rate threshold is evaluated per-cycle, so iterate this list when computing bonuses.
        public List<(DateTime Start, DateTime End)> CalcCycles { get; set; } = new List<(DateTime, DateTime)>();

        public bool IsDateFiltered { get; set; }
    }

    public class BonusWindowService
    {
        // System-wide epoch for the bonus system. Nothing before this date is ever
        // included in carryover totals. 10:30 matches the rating system's shift boundary.
        public static readonly DateTime SystemStart = new DateTime(2026, 4, 11, 10, 30, 0);

        private readonly GetCurrentTimeInIstanbul _time;

        public BonusWindowService(GetCurrentTimeInIstanbul time)
        {
            _time = time;
        }

        public BonusWindow GetActiveWindow(DateTime? filterStart, DateTime? filterEnd)
        {
            if (filterStart.HasValue || filterEnd.HasValue)
            {
                var s = filterStart?.Date.AddHours(10).AddMinutes(30) ?? SystemStart;
                var e = filterEnd?.Date.AddHours(10).AddMinutes(30) ?? _time.GetIstanbulTimeWithOffset();
                if (e <= s) e = s.AddSeconds(1);

                return new BonusWindow
                {
                    DisplayStart = s,
                    DisplayEnd = e,
                    CalcCycles = new List<(DateTime, DateTime)> { (s, e) },
                    IsDateFiltered = true
                };
            }

            var now = _time.GetIstanbulTimeWithOffset();
            var currentCycleStart = CycleStartFor(now);
            var currentCycleEnd = currentCycleStart.AddMonths(1);

            var cycles = new List<(DateTime, DateTime)>();
            var cursor = SystemStart;
            while (cursor < currentCycleEnd)
            {
                var next = new DateTime(cursor.Year, cursor.Month, 1, 10, 30, 0).AddMonths(1);
                cycles.Add((cursor, next));
                cursor = next;
            }

            return new BonusWindow
            {
                DisplayStart = SystemStart,
                DisplayEnd = currentCycleEnd,
                CalcCycles = cycles,
                IsDateFiltered = false
            };
        }

        // The cycle that `instant` falls into. Cycle = [1st@10:30, next 1st@10:30).
        // For instants before the 1st at 10:30, the active cycle is the previous month's.
        public DateTime CycleStartFor(DateTime instant)
        {
            var firstOfThisMonthAtTen = new DateTime(instant.Year, instant.Month, 1, 10, 30, 0);
            if (instant < firstOfThisMonthAtTen)
                return firstOfThisMonthAtTen.AddMonths(-1);
            return firstOfThisMonthAtTen;
        }
    }
}
