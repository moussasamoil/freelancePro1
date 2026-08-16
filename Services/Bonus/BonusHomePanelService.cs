using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.Models.ViewModel;
using lotus_blue.Services.Bonus;
using Microsoft.EntityFrameworkCore;

namespace lotus_blue.Services.Bonus
{
    // Builds the floating bonus panel shown to CallCenter agents on the home page.
    //
    // Display window: [SystemStart, upperBound), where upperBound is either:
    //  - current Istanbul time (live mode), when the employee has no outstanding
    //    bonus orders from before their last payment that were already in success
    //    status at pay time; or
    //  - the current cycle start (freeze mode), when an unpaid pre-payment success
    //    order exists. The freeze prevents the counter from rolling into the next
    //    period until prior bonuses are settled.
    //
    // Late-qualifier exception (plan lines 95-96): an order created before the last
    // DatePaid that didn't reach success until AFTER the payment does NOT trigger
    // the freeze — it's allowed to flow into the live counter for the current period.
    public class BonusHomePanelService
    {
        private readonly ApplicationDbContext _context;
        private readonly BonusCalculationService _bonusCalc;
        private readonly BonusWindowService _bonusWindow;
        private readonly GetCurrentTimeInIstanbul _time;
        private readonly CurrencyExchangeService _fx;

        public BonusHomePanelService(
            ApplicationDbContext context,
            BonusCalculationService bonusCalc,
            BonusWindowService bonusWindow,
            GetCurrentTimeInIstanbul time,
            CurrencyExchangeService fx)
        {
            _context = context;
            _bonusCalc = bonusCalc;
            _bonusWindow = bonusWindow;
            _time = time;
            _fx = fx;
        }

        public async Task<HomeBonusPanelViewModel> BuildAsync(string employeeId)
        {
            var now = _time.GetIstanbulTimeWithOffset();
            var currentCycleStart = _bonusWindow.CycleStartFor(now);

            var latestPayment = await _context.EmployeeBonusPayments
                .Where(p => p.EmployeeId == employeeId)
                .OrderByDescending(p => p.DatePaid)
                .FirstOrDefaultAsync();

            // Determine whether to freeze the upper bound at the current cycle's
            // start (still owe for closed cycles) or extend to now (live).
            bool freeze;
            if (latestPayment == null)
            {
                // No payments yet: anything created before the current cycle is
                // unsettled, so freeze if such an order exists.
                freeze = await _context.Orders.AnyAsync(o =>
                    o.BonusPaymentId == null &&
                    o.CreatedDate < currentCycleStart &&
                    (o.ApplicationUserId == employeeId
                        || (o.Fixedby != null && o.Fixedby == employeeId)));
            }
            else
            {
                freeze = await HasFreezeBlockerAsync(employeeId, latestPayment.DatePaid);
            }

            var upperBound = freeze ? currentCycleStart : now;
            var displayStart = BonusWindowService.SystemStart;

            // Cycle-driven calc for $ amounts (pro threshold is per-cycle).
            var cycles = BuildCyclesUpTo(displayStart, upperBound);

            var attributions = await _bonusCalc.ComputeForEmployeeAcrossCyclesAsync(
                employeeId, cycles, onlyUnpaid: true);

            var successAmount = attributions.Where(a => a.IsCreator).Sum(a => a.BonusUsd);
            var processedAmount = attributions.Where(a => a.IsProcessor).Sum(a => a.BonusUsd);

            // Class counts come from a direct query (other classes don't enter the
            // bonus calc service, only Success does).
            var classCounts = await BuildClassCountsAsync(employeeId, displayStart, upperBound);

            // Today's shift counts (10:30–10:30) — shown as the "اليوم" sub-counter on each card.
            var todayCounts = await BuildTodayClassCountsAsync(employeeId, now);

            // Order IDs per class, used by the card click-through navigation. We hand the
            // exact ID list to the view so clicking a card opens /Home/Index?orderIds=...
            // showing the same orders the count represents — no second filter pass needed.
            var classOrderIds = await BuildClassOrderIdsAsync(employeeId, displayStart, upperBound);

            // Collection-class projected $: raw profit currently pending + bonus payout
            // if those orders eventually deliver. Pro-aware per cycle — uses pro rates
            // when realized success + projected collection profit crosses threshold.
            var collectionProjection = await _bonusCalc.ComputeCollectionProjectedAcrossCyclesAsync(
                employeeId, cycles, attributions);

            // Pro progress for the LATEST cycle in the window (the one currently
            // accumulating). Uses the calc-service attributions to stay consistent
            // with how pro is evaluated elsewhere.
            var rate = await _context.EmployeeBonusRates
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.EmployeeId == employeeId);

            decimal proRemaining = 0m;
            decimal proThreshold = rate?.ProThreshold ?? 0m;
            decimal proCurrentProfit = 0m;
            bool hasProConfigured = rate != null && rate.ProThreshold > 0m;
            if (hasProConfigured && cycles.Count > 0)
            {
                var lastCycle = cycles[cycles.Count - 1];
                proCurrentProfit = attributions
                    .Where(a => a.AccountingTime >= lastCycle.Start && a.AccountingTime < lastCycle.End)
                    .Sum(a => a.ProfitUsd);
                proRemaining = System.Math.Max(0m, proThreshold - proCurrentProfit);
            }

            // Orders delegated to this employee via DelegateEmployeeId — excluded from counts
            // but unioned into navigation IDs for failure/incomplete cards.
            var delegatedBase = _context.Orders
                .Where(o => o.DelegateEmployeeId == employeeId)
                .Where(o => o.CreatedDate >= displayStart && o.CreatedDate < upperBound);

            var delegatedFailureIds = await delegatedBase
                .Where(o => BonusStatusGroups.Failure.Contains(o.OrderStatus))
                .Select(o => o.Id)
                .ToListAsync();

            var delegatedIncompleteIds = await delegatedBase
                .Where(o => BonusStatusGroups.Incomplete.Contains(o.OrderStatus))
                .Select(o => o.Id)
                .ToListAsync();

            // Net profit (USD) for the three non-bonus classes — displayed on their cards.
            var nonBonusBase = _context.Orders
                .Where(o => o.CreatedDate >= displayStart && o.CreatedDate < upperBound)
                .Where(o => o.BonusPaymentId == null)
                .Where(o => o.ApplicationUserId == employeeId
                            || (o.Fixedby != null && o.Fixedby == employeeId));

            var failureRows = await nonBonusBase
                .Where(o => BonusStatusGroups.Failure.Contains(o.OrderStatus))
                .Select(o => new { o.TotalPrice, o.DeliveryPrice, Country = o.Country.ToString() })
                .ToListAsync();
            var incompleteRows = await nonBonusBase
                .Where(o => BonusStatusGroups.Incomplete.Contains(o.OrderStatus))
                .Select(o => new { o.TotalPrice, o.DeliveryPrice, Country = o.Country.ToString() })
                .ToListAsync();
            var delayedRows = await nonBonusBase
                .Where(o => BonusStatusGroups.Delayed.Contains(o.OrderStatus))
                .Select(o => new { o.TotalPrice, o.DeliveryPrice, Country = o.Country.ToString() })
                .ToListAsync();

            var failureProfitUsd   = failureRows.Sum(r => _fx.ConvertToUSD(r.TotalPrice - r.DeliveryPrice, r.Country));
            var incompleteProfitUsd = incompleteRows.Sum(r => _fx.ConvertToUSD(r.TotalPrice - r.DeliveryPrice, r.Country));
            var delayedProfitUsd   = delayedRows.Sum(r => _fx.ConvertToUSD(r.TotalPrice - r.DeliveryPrice, r.Country));

            return new HomeBonusPanelViewModel
            {
                EmployeeId = employeeId,
                Frozen = freeze,
                DisplayStart = displayStart,
                DisplayEnd = upperBound,
                SuccessCount = classCounts.GetValueOrDefault(BonusStatClass.Success),
                FailureCount = classCounts.GetValueOrDefault(BonusStatClass.Failure),
                CollectionCount = classCounts.GetValueOrDefault(BonusStatClass.Collection),
                IncompleteCount = classCounts.GetValueOrDefault(BonusStatClass.Incomplete),
                DelayedCount = classCounts.GetValueOrDefault(BonusStatClass.Delayed),
                ProcessedForOthersCount = classCounts.GetValueOrDefault((BonusStatClass)100),
                SuccessTodayCount = todayCounts.GetValueOrDefault(BonusStatClass.Success),
                FailureTodayCount = todayCounts.GetValueOrDefault(BonusStatClass.Failure),
                CollectionTodayCount = todayCounts.GetValueOrDefault(BonusStatClass.Collection),
                IncompleteTodayCount = todayCounts.GetValueOrDefault(BonusStatClass.Incomplete),
                DelayedTodayCount = todayCounts.GetValueOrDefault(BonusStatClass.Delayed),
                ProcessedForOthersTodayCount = todayCounts.GetValueOrDefault((BonusStatClass)100),
                SuccessOrderIds = classOrderIds.GetValueOrDefault(BonusStatClass.Success) ?? new(),
                FailureOrderIds = classOrderIds.GetValueOrDefault(BonusStatClass.Failure) ?? new(),
                CollectionOrderIds = classOrderIds.GetValueOrDefault(BonusStatClass.Collection) ?? new(),
                IncompleteOrderIds = classOrderIds.GetValueOrDefault(BonusStatClass.Incomplete) ?? new(),
                DelayedOrderIds = classOrderIds.GetValueOrDefault(BonusStatClass.Delayed) ?? new(),
                ProcessedForOthersOrderIds = classOrderIds.GetValueOrDefault((BonusStatClass)100) ?? new(),
                DelegatedFailureOrderIds = delegatedFailureIds,
                DelegatedIncompleteOrderIds = delegatedIncompleteIds,
                SuccessAmountUsd = successAmount,
                ProcessedForOthersAmountUsd = processedAmount,
                SuccessProfitUsd = attributions.Where(a => a.IsCreator).Sum(a => a.ProfitUsd),
                ProcessedForOthersProfitUsd = attributions.Where(a => a.IsProcessor).Sum(a => a.ProfitUsd),
                CollectionProfitUsd = collectionProjection.ProfitUsd,
                CollectionProjectedBonusUsd = collectionProjection.BonusUsd,
                FailureProfitUsd = failureProfitUsd,
                IncompleteProfitUsd = incompleteProfitUsd,
                DelayedProfitUsd = delayedProfitUsd,
                HasProConfigured = hasProConfigured,
                ProRemainingUsd = proRemaining,
                ProThresholdUsd = proThreshold,
                ProCurrentProfitUsd = proCurrentProfit,
                AmountsRevealed = rate?.IsBonusAmountsRevealed ?? false
            };
        }

        // True iff there is at least one unpaid order created before DatePaid that was
        // already in a success status at DatePaid (i.e. should have been included in
        // that payment). Orders that became success-qualifying AFTER DatePaid are
        // excluded — they're late qualifiers and don't trigger the freeze.
        private async Task<bool> HasFreezeBlockerAsync(string employeeId, DateTime datePaid)
        {
            var successStatuses = BonusStatusGroups.Success;

            var blockerOrderIds = await _context.Orders
                .Where(o => o.BonusPaymentId == null
                            && o.CreatedDate < datePaid
                            && successStatuses.Contains(o.OrderStatus)
                            && (o.ApplicationUserId == employeeId
                                || (o.Fixedby != null && o.Fixedby == employeeId)))
                .Select(o => o.Id)
                .ToListAsync();

            if (blockerOrderIds.Count == 0) return false;

            // Of those, keep only orders whose FIRST success-status transition was
            // on or before DatePaid (i.e. they were already paying out at that time).
            var firstSuccessByOrder = await _context.OrderStatusHistories
                .Where(h => blockerOrderIds.Contains(h.OrderId.Value)
                            && h.Status.HasValue
                            && successStatuses.Contains(h.Status.Value))
                .GroupBy(h => h.OrderId.Value)
                .Select(g => new { OrderId = g.Key, FirstAt = g.Min(h => h.CreatedAt) })
                .ToListAsync();

            return firstSuccessByOrder.Any(x => x.FirstAt <= datePaid);
        }

        private List<(DateTime Start, DateTime End)> BuildCyclesUpTo(DateTime start, DateTime endExclusive)
        {
            var cycles = new List<(DateTime, DateTime)>();
            if (endExclusive <= start) return cycles;

            var cursor = start;
            while (cursor < endExclusive)
            {
                var next = new DateTime(cursor.Year, cursor.Month, 1, 10, 30, 0).AddMonths(1);
                if (next > endExclusive) next = endExclusive;
                cycles.Add((cursor, next));
                cursor = next;
            }
            return cycles;
        }

        // Returns the order IDs that make up each card's count. Mirrors BuildClassCountsAsync
        // exactly so the click-through opens the same set the count was reporting.
        // Sentinel: (BonusStatClass)100 = ProcessedForOthers.
        private async Task<Dictionary<BonusStatClass, List<int>>> BuildClassOrderIdsAsync(
            string employeeId, DateTime start, DateTime endExclusive)
        {
            var result = new Dictionary<BonusStatClass, List<int>>();

            var baseQuery = _context.Orders
                .Where(o => o.CreatedDate >= start && o.CreatedDate < endExclusive)
                .Where(o => o.BonusPaymentId == null)
                .Where(o => o.ApplicationUserId == employeeId
                            || (o.Fixedby != null && o.Fixedby == employeeId));

            async Task FillAsync(BonusStatClass cls, OrderStatusEnum[] statuses)
            {
                result[cls] = await baseQuery
                    .Where(o => statuses.Contains(o.OrderStatus))
                    .Select(o => o.Id)
                    .ToListAsync();
            }

            await FillAsync(BonusStatClass.Success, BonusStatusGroups.Success);
            await FillAsync(BonusStatClass.Failure, BonusStatusGroups.Failure);
            await FillAsync(BonusStatClass.Collection, BonusStatusGroups.Collection);
            await FillAsync(BonusStatClass.Incomplete, BonusStatusGroups.Incomplete);
            await FillAsync(BonusStatClass.Delayed, BonusStatusGroups.Delayed);

            result[(BonusStatClass)100] = await baseQuery
                .Where(o => BonusStatusGroups.Success.Contains(o.OrderStatus))
                .Where(o => o.Fixedby != null && o.Fixedby != o.ApplicationUserId)
                .Select(o => o.Id)
                .ToListAsync();

            return result;
        }

        // Counts scoped to today's shift (10:30–10:30 Istanbul). Matches the same
        // shift definition used by EmployeeBonusController.BuildTodayClassCountsAsync.
        private async Task<Dictionary<BonusStatClass, int>> BuildTodayClassCountsAsync(
            string employeeId, DateTime now)
        {
            DateTime shiftStart, shiftEnd;
            if (now.TimeOfDay < new TimeSpan(10, 30, 0))
            {
                shiftStart = now.Date.AddDays(-1).AddHours(10).AddMinutes(30);
                shiftEnd   = now.Date.AddHours(10).AddMinutes(30);
            }
            else
            {
                shiftStart = now.Date.AddHours(10).AddMinutes(30);
                shiftEnd   = now.Date.AddDays(1).AddHours(10).AddMinutes(30);
            }

            var result = new Dictionary<BonusStatClass, int>();

            var baseQuery = _context.Orders
                .Where(o => o.InstantAddedDate >= shiftStart && o.InstantAddedDate < shiftEnd)
                .Where(o => o.ApplicationUserId == employeeId
                            || (o.Fixedby != null && o.Fixedby == employeeId));

            var grouped = await baseQuery
                .GroupBy(o => o.OrderStatus)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            foreach (var g in grouped)
            {
                var cls = BonusStatusGroups.Classify(g.Status);
                if (cls == null) continue;
                if (!result.ContainsKey(cls.Value)) result[cls.Value] = 0;
                result[cls.Value] += g.Count;
            }

            var successStatuses = BonusStatusGroups.Success;
            var processedForOthers = await baseQuery
                .Where(o => successStatuses.Contains(o.OrderStatus))
                .Where(o => o.Fixedby != null && o.Fixedby != o.ApplicationUserId)
                .CountAsync();
            result[(BonusStatClass)100] = processedForOthers;

            return result;
        }

        private async Task<Dictionary<BonusStatClass, int>> BuildClassCountsAsync(
            string employeeId, DateTime start, DateTime endExclusive)
        {
            var result = new Dictionary<BonusStatClass, int>();

            var baseQuery = _context.Orders
                .Where(o => o.CreatedDate >= start && o.CreatedDate < endExclusive)
                .Where(o => o.BonusPaymentId == null)
                .Where(o => o.ApplicationUserId == employeeId
                            || (o.Fixedby != null && o.Fixedby == employeeId));

            var grouped = await baseQuery
                .GroupBy(o => o.OrderStatus)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            foreach (var g in grouped)
            {
                var cls = BonusStatusGroups.Classify(g.Status);
                if (cls == null) continue;
                if (!result.ContainsKey(cls.Value)) result[cls.Value] = 0;
                result[cls.Value] += g.Count;
            }

            var successStatuses = BonusStatusGroups.Success;
            var processedForOthers = await baseQuery
                .Where(o => successStatuses.Contains(o.OrderStatus))
                .Where(o => o.Fixedby != null && o.Fixedby != o.ApplicationUserId)
                .CountAsync();
            result[(BonusStatClass)100] = processedForOthers;

            return result;
        }
    }
}
