using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.Services;
using Microsoft.EntityFrameworkCore;

namespace lotus_blue.Services.Bonus
{
    // Computes bonuses per the spec in plan9bonus.txt.
    // Frozen-at-pay-time model: when the admin pays bonuses for an order, the
    // resulting amount/profit numbers are snapshotted into EmployeeBonusPayment
    // and the order is linked via Order.BonusPaymentId. Post-payment edits to
    // the Order row don't change anything that's already been paid.
    public class BonusCalculationService
    {
        private readonly ApplicationDbContext _context;
        private readonly CurrencyExchangeService _fx;

        public BonusCalculationService(ApplicationDbContext context, CurrencyExchangeService fx)
        {
            _context = context;
            _fx = fx;
        }

        public class ProfitBasis
        {
            // Profit (USD) using the order's original TotalPrice/DeliveryPrice — taken
            // from the earliest OrderEditHistory row if any edits exist, otherwise the
            // current Order row.
            public decimal AtCreationUsd { get; set; }
            // Profit (USD) using the current Order row values at the moment of calc.
            public decimal NowUsd { get; set; }
            public decimal CreatorBasisUsd => Math.Min(AtCreationUsd, NowUsd);
        }

        public ProfitBasis ComputeProfit(Order order, OrderEditHistory? earliestEdit)
        {
            var nowAmount = order.TotalPrice - order.DeliveryPrice;
            var createdAmount = earliestEdit != null
                ? earliestEdit.TotalPrice - earliestEdit.DeliveryPrice
                : nowAmount;

            var countryName = order.Country.ToString();
            return new ProfitBasis
            {
                AtCreationUsd = _fx.ConvertToUSD(createdAmount, countryName),
                NowUsd = _fx.ConvertToUSD(nowAmount, countryName)
            };
        }

        // Per-order outcome attributed to one employee.
        public class OrderBonusAttribution
        {
            public int OrderId { get; set; }
            public string EmployeeId { get; set; } = string.Empty;
            // Role this employee played for this order.
            public bool IsCreator { get; set; }
            public bool IsProcessor { get; set; }
            public BonusStatClass? Class { get; set; }
            // Profit (USD) attributable to this employee for threshold accumulation.
            public decimal ProfitUsd { get; set; }
            // Bonus dollars (USD) earned for this order.
            public decimal BonusUsd { get; set; }
            // True iff this order's rate used pro rates (threshold crossed before this order).
            public bool UsedProRate { get; set; }
            // Timestamp used for ordering within the cycle.
            public DateTime AccountingTime { get; set; }
        }

        // Compute bonuses for one employee for a defined window [windowStart, windowEnd).
        // Orders included: any order where this employee is the creator OR the latest
        // processor (Order.Fixedby) AND the order has reached success class.
        // Only success-class orders generate bonus dollars (per spec). Other classes are
        // ignored here — they're for display only in the index page and don't enter
        // accumulator or payouts.
        public async Task<List<OrderBonusAttribution>> ComputeForEmployeeAsync(
            string employeeId,
            DateTime windowStartUtc,
            DateTime windowEndUtc,
            bool onlyUnpaid = true)
        {
            var rate = await _context.EmployeeBonusRates
                .FirstOrDefaultAsync(r => r.EmployeeId == employeeId);

            // Missing rate row → default 0%, no threshold (per user decision).
            var bonusPct = rate?.BonusPercentage ?? 0m;
            var procPct = rate?.BonusProcessingPercentage ?? 0m;
            var proBonusPct = rate?.ProBonusPercentage ?? 0m;
            var proProcPct = rate?.ProBonusProcessingPercentage ?? 0m;
            // ProThreshold is stored in the employee's local currency. Translate to USD
            // so it can be compared against the cycle's USD profit. Country lives on the
            // Employee record (string) — bridged through Common.EmployeeCountryToEnum.
            var employeeCountryStr = await _context.Employees
                .Where(e => e.ApplicationUserId == employeeId)
                .Select(e => e.Country)
                .FirstOrDefaultAsync();
            var employeeCountry = Common.EmployeeCountryToEnum(employeeCountryStr);
            var threshold = rate?.ProThreshold ?? 0m;
            var thresholdUsd = employeeCountry.HasValue
                ? _fx.ConvertToUSD(threshold, employeeCountry.Value)
                : threshold;

            // Pull candidate orders: creator OR latest processor, success class, within window.
            // Cycle membership uses Order.CreatedDate (plan lines 90, 94, 96 — pay/freeze logic
            // is gated on the order's creation timestamp, not when it reached success).
            var successStatuses = BonusStatusGroups.Success;

            var orderQuery = _context.Orders
                .Where(o => successStatuses.Contains(o.OrderStatus))
                .Where(o => o.ApplicationUserId == employeeId
                            || (o.Fixedby != null && o.Fixedby == employeeId))
                .Where(o => o.CreatedDate >= windowStartUtc && o.CreatedDate < windowEndUtc);

            if (onlyUnpaid)
            {
                orderQuery = orderQuery.Where(o => o.BonusPaymentId == null);
            }

            var inWindow = await orderQuery
                .OrderBy(o => o.CreatedDate)
                .ToListAsync();
            var orderIds = inWindow.Select(o => o.Id).ToList();

            // For each order, pull only the earliest OrderEditHistory row (snapshot
            // before the first edit = values at creation time). Orders with no edits
            // skip this entirely and fall back to current Order values.
            var earliestEdits = await _context.OrderEditHistories
                .Where(h => orderIds.Contains(h.OrderId))
                .GroupBy(h => h.OrderId)
                .Select(g => g.OrderBy(h => h.EditNumber).First())
                .ToListAsync();
            var earliestByOrder = earliestEdits.ToDictionary(h => h.OrderId, h => h);

            // When this employee is the CREATOR and a different processor exists,
            // the deduction from creator uses the PROCESSOR's base BonusProcessingPercentage
            // (not the creator's). Load the rate rows for all such processors in one query.
            var processorIds = inWindow
                .Where(o => o.ApplicationUserId == employeeId
                            && !string.IsNullOrEmpty(o.Fixedby)
                            && o.Fixedby != employeeId)
                .Select(o => o.Fixedby!)
                .Distinct()
                .ToList();
            var processorRates = await _context.EmployeeBonusRates
                .Where(r => processorIds.Contains(r.EmployeeId))
                .ToDictionaryAsync(r => r.EmployeeId, r => r);

            // Pre-compute per-order roles + profit, then sum cycle profit to decide
            // pro-rate. The flip is cycle-level and retroactive: if the cycle total
            // crosses the threshold, every order in the cycle uses pro rates.
            var staged = new List<(Order order, bool isCreator, bool isProcessor,
                                   bool hasProcessor, ProfitBasis profit, decimal attributable,
                                   decimal processorBaseRate)>();

            foreach (var order in inWindow)
            {
                var isCreator = order.ApplicationUserId == employeeId;
                var hasProcessor = !string.IsNullOrEmpty(order.Fixedby)
                                   && order.Fixedby != order.ApplicationUserId;
                var isProcessor = hasProcessor && order.Fixedby == employeeId;
                if (!isCreator && !isProcessor) continue;

                earliestByOrder.TryGetValue(order.Id, out var earliest);
                var profit = ComputeProfit(order, earliest);
                var attributable = isCreator ? profit.CreatorBasisUsd : profit.NowUsd;

                // Processor's base rate (used for the creator-side deduction).
                // Missing rate row → 0%.
                decimal processorBase = 0m;
                if (isCreator && hasProcessor
                    && processorRates.TryGetValue(order.Fixedby!, out var pr))
                {
                    processorBase = pr.BonusProcessingPercentage;
                }

                staged.Add((order, isCreator, isProcessor, hasProcessor, profit,
                            attributable, processorBase));
            }

            // Pro decision is per CYCLE — and the cycle's "realized profit" must include
            // orders that were already paid in a previous payment. Otherwise stragglers
            // (late-completing orders from a cycle that already crossed the threshold)
            // would be paid at the BASE rate, breaking parity with their cycle-mates.
            decimal alreadyPaidProfit = 0m;
            if (onlyUnpaid)
            {
                var paidInCycle = await _context.Orders
                    .Where(o => successStatuses.Contains(o.OrderStatus))
                    .Where(o => o.ApplicationUserId == employeeId
                                || (o.Fixedby != null && o.Fixedby == employeeId))
                    .Where(o => o.CreatedDate >= windowStartUtc && o.CreatedDate < windowEndUtc)
                    .Where(o => o.BonusPaymentId != null)
                    .ToListAsync();

                if (paidInCycle.Count > 0)
                {
                    var paidIds = paidInCycle.Select(o => o.Id).ToList();
                    var paidEdits = await _context.OrderEditHistories
                        .Where(h => paidIds.Contains(h.OrderId))
                        .GroupBy(h => h.OrderId)
                        .Select(g => g.OrderBy(h => h.EditNumber).First())
                        .ToListAsync();
                    var paidEditsByOrder = paidEdits.ToDictionary(h => h.OrderId, h => h);

                    foreach (var o in paidInCycle)
                    {
                        var isCreatorPaid = o.ApplicationUserId == employeeId;
                        var isProcessorPaid = !isCreatorPaid && o.Fixedby == employeeId;
                        if (!isCreatorPaid && !isProcessorPaid) continue;

                        paidEditsByOrder.TryGetValue(o.Id, out var earliestPaid);
                        var profitPaid = ComputeProfit(o, earliestPaid);
                        alreadyPaidProfit += isCreatorPaid ? profitPaid.CreatorBasisUsd : profitPaid.NowUsd;
                    }
                }
            }

            var totalCycleProfit = staged.Sum(s => s.attributable) + alreadyPaidProfit;
            bool useProRate = thresholdUsd > 0m && totalCycleProfit >= thresholdUsd;

            var creatorPct = useProRate ? proBonusPct : bonusPct;
            var procPctEff = useProRate ? proProcPct : procPct;

            var results = new List<OrderBonusAttribution>();
            foreach (var s in staged)
            {
                decimal bonus;
                if (s.isCreator)
                {
                    bonus = s.profit.CreatorBasisUsd * (creatorPct / 100m);
                    // Spec: processor's amount is deducted from the creator's bonus.
                    // Deduction uses the PROCESSOR's BASE BonusProcessingPercentage —
                    // not the creator's, and not the processor's pro rate.
                    if (s.hasProcessor)
                    {
                        var processorTake = s.profit.NowUsd * (s.processorBaseRate / 100m);
                        bonus -= processorTake;
                    }
                }
                else
                {
                    bonus = s.profit.NowUsd * (procPctEff / 100m);
                }

                // Floor at 0: never pay a negative bonus. Negative comes from edge cases
                // like profit_at_creation < 0 (price < delivery) flowing into the min-basis.
                if (bonus < 0m) bonus = 0m;

                results.Add(new OrderBonusAttribution
                {
                    OrderId = s.order.Id,
                    EmployeeId = employeeId,
                    IsCreator = s.isCreator,
                    IsProcessor = s.isProcessor,
                    Class = BonusStatusGroups.Classify(s.order.OrderStatus),
                    ProfitUsd = s.attributable,
                    BonusUsd = bonus,
                    UsedProRate = useProRate,
                    AccountingTime = s.order.CreatedDate
                });
            }

            return results;
        }

        // Projected bonus for Collection-class orders, pro-aware per cycle.
        //
        // Pro promotion is evaluated per cycle and is retroactive across the cycle:
        // if a cycle's realized success profit plus its projected collection profit
        // crosses the employee's threshold, that cycle's collection orders are
        // projected at pro rates. Realized success attributions are passed in so
        // their per-cycle profit can be summed alongside the collection projection.
        public async Task<(decimal ProfitUsd, decimal BonusUsd)> ComputeCollectionProjectedAcrossCyclesAsync(
            string employeeId,
            IEnumerable<(DateTime Start, DateTime End)> cycles,
            IEnumerable<OrderBonusAttribution> realizedSuccessAttributions,
            bool onlyUnpaid = true)
        {
            var rate = await _context.EmployeeBonusRates
                .FirstOrDefaultAsync(r => r.EmployeeId == employeeId);
            var bonusPct = rate?.BonusPercentage ?? 0m;
            var procPct = rate?.BonusProcessingPercentage ?? 0m;
            var proBonusPct = rate?.ProBonusPercentage ?? 0m;
            var proProcPct = rate?.ProBonusProcessingPercentage ?? 0m;
            // ProThreshold is stored in the employee's local currency. Translate to USD
            // so it can be compared against cycle USD profit. Country lives on the
            // Employee record (string) — bridged through Common.EmployeeCountryToEnum.
            var employeeCountryStr = await _context.Employees
                .Where(e => e.ApplicationUserId == employeeId)
                .Select(e => e.Country)
                .FirstOrDefaultAsync();
            var employeeCountry = Common.EmployeeCountryToEnum(employeeCountryStr);
            var threshold = rate?.ProThreshold ?? 0m;
            var thresholdUsd = employeeCountry.HasValue
                ? _fx.ConvertToUSD(threshold, employeeCountry.Value)
                : threshold;

            var collectionStatuses = BonusStatusGroups.Collection;
            var realized = realizedSuccessAttributions.ToList();

            decimal totalProfit = 0m;
            decimal totalBonus = 0m;

            foreach (var (cStart, cEnd) in cycles)
            {
                var q = _context.Orders
                    .Where(o => collectionStatuses.Contains(o.OrderStatus))
                    .Where(o => o.ApplicationUserId == employeeId
                                || (o.Fixedby != null && o.Fixedby == employeeId))
                    .Where(o => o.CreatedDate >= cStart && o.CreatedDate < cEnd);
                if (onlyUnpaid) q = q.Where(o => o.BonusPaymentId == null);

                var orders = await q.ToListAsync();
                if (orders.Count == 0) continue;

                var orderIds = orders.Select(o => o.Id).ToList();
                var earliestEdits = await _context.OrderEditHistories
                    .Where(h => orderIds.Contains(h.OrderId))
                    .GroupBy(h => h.OrderId)
                    .Select(g => g.OrderBy(h => h.EditNumber).First())
                    .ToListAsync();
                var earliestByOrder = earliestEdits.ToDictionary(h => h.OrderId, h => h);

                var processorIds = orders
                    .Where(o => o.ApplicationUserId == employeeId
                                && !string.IsNullOrEmpty(o.Fixedby)
                                && o.Fixedby != employeeId)
                    .Select(o => o.Fixedby!)
                    .Distinct()
                    .ToList();
                var processorRates = await _context.EmployeeBonusRates
                    .Where(r => processorIds.Contains(r.EmployeeId))
                    .ToDictionaryAsync(r => r.EmployeeId, r => r);

                var staged = new List<(Order order, bool isCreator, bool hasProcessor,
                                       ProfitBasis profit, decimal attributable,
                                       decimal processorBaseRate)>();

                foreach (var order in orders)
                {
                    var isCreator = order.ApplicationUserId == employeeId;
                    var hasProcessor = !string.IsNullOrEmpty(order.Fixedby)
                                       && order.Fixedby != order.ApplicationUserId;
                    var isProcessor = hasProcessor && order.Fixedby == employeeId;
                    if (!isCreator && !isProcessor) continue;

                    earliestByOrder.TryGetValue(order.Id, out var earliest);
                    var profit = ComputeProfit(order, earliest);
                    var attributable = isCreator ? profit.CreatorBasisUsd : profit.NowUsd;

                    decimal processorBase = 0m;
                    if (isCreator && hasProcessor
                        && processorRates.TryGetValue(order.Fixedby!, out var pr))
                    {
                        processorBase = pr.BonusProcessingPercentage;
                    }

                    staged.Add((order, isCreator, hasProcessor, profit, attributable, processorBase));
                }

                var realizedCycleProfit = realized
                    .Where(a => a.AccountingTime >= cStart && a.AccountingTime < cEnd)
                    .Sum(a => a.ProfitUsd);
                var collectionCycleProfit = staged.Sum(s => s.attributable);
                bool useProRate = thresholdUsd > 0m
                    && (realizedCycleProfit + collectionCycleProfit) >= thresholdUsd;

                var creatorPct = useProRate ? proBonusPct : bonusPct;
                var procPctEff = useProRate ? proProcPct : procPct;

                foreach (var s in staged)
                {
                    decimal bonus;
                    if (s.isCreator)
                    {
                        bonus = s.profit.CreatorBasisUsd * (creatorPct / 100m);
                        if (s.hasProcessor)
                        {
                            // Processor deduction uses processor's BASE rate (matches success logic).
                            bonus -= s.profit.NowUsd * (s.processorBaseRate / 100m);
                        }
                    }
                    else
                    {
                        bonus = s.profit.NowUsd * (procPctEff / 100m);
                    }
                    if (bonus < 0m) bonus = 0m;
                    totalBonus += bonus;
                }

                totalProfit += collectionCycleProfit;
            }

            return (totalProfit, totalBonus);
        }

        // Convenience: iterate cycles from BonusWindowService and concatenate.
        // Pro-rate threshold is evaluated independently per cycle, so a multi-cycle
        // window must NOT be passed as a single (start, end) to ComputeForEmployeeAsync.
        public async Task<List<OrderBonusAttribution>> ComputeForEmployeeAcrossCyclesAsync(
            string employeeId,
            IEnumerable<(DateTime Start, DateTime End)> cycles,
            bool onlyUnpaid = true)
        {
            var all = new List<OrderBonusAttribution>();
            foreach (var (s, e) in cycles)
            {
                var batch = await ComputeForEmployeeAsync(employeeId, s, e, onlyUnpaid);
                all.AddRange(batch);
            }
            return all;
        }
    }
}
