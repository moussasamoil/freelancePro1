using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.Models.ViewModel;
using lotus_blue.Services;
using lotus_blue.Services.Bonus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace lotus_blue.Controllers
{
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public class EmployeeBonusController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly DataCacheService _dataCacheService;
        private readonly BonusCalculationService _bonusCalc;
        private readonly BonusWindowService _bonusWindow;
        private readonly GetCurrentTimeInIstanbul _time;
        private readonly CurrencyExchangeService _fx;

        public EmployeeBonusController(
            ApplicationDbContext context,
            DataCacheService dataCacheService,
            BonusCalculationService bonusCalc,
            BonusWindowService bonusWindow,
            GetCurrentTimeInIstanbul time,
            CurrencyExchangeService fx)
        {
            _context = context;
            _dataCacheService = dataCacheService;
            _bonusCalc = bonusCalc;
            _bonusWindow = bonusWindow;
            _time = time;
            _fx = fx;
        }

        // ----- Index -----

        [HttpGet]
        public async Task<IActionResult> Index(string startDate, string endDate, string employeeId)
        {
            var filterStart = ParseFilterDate(startDate);
            var filterEnd = ParseFilterDate(endDate);
            var window = _bonusWindow.GetActiveWindow(filterStart, filterEnd);

            var employees = (await _dataCacheService.GetCachedEmployeesAsync())
                .Where(e => !string.IsNullOrEmpty(e.ApplicationUserId))
                .OrderBy(e => e.DisplayName ?? e.Name)
                .ToList();

            var employeeOptions = employees
                .Select(e => new EmployeeOption { Id = e.ApplicationUserId, Name = e.DisplayName ?? e.Name })
                .ToList();

            var rates = await _context.EmployeeBonusRates.ToListAsync();
            var ratesByEmployee = rates.ToDictionary(r => r.EmployeeId, r => r);

            // Decide which employees contribute to global $ + row payable amounts.
            var targetEmployees = string.IsNullOrEmpty(employeeId)
                ? employees
                : employees.Where(e => e.ApplicationUserId == employeeId).ToList();

            // Per-employee attributions across cycles (pro threshold resolved per cycle).
            var perEmployeeAttributions = new Dictionary<string, List<BonusCalculationService.OrderBonusAttribution>>();
            foreach (var emp in targetEmployees)
            {
                var attrs = await _bonusCalc.ComputeForEmployeeAcrossCyclesAsync(
                    emp.ApplicationUserId,
                    window.CalcCycles,
                    onlyUnpaid: true);
                perEmployeeAttributions[emp.ApplicationUserId] = attrs;
            }

            var todayCounts = window.IsDateFiltered
                ? (Dictionary<BonusStatClass, int>)null
                : await BuildTodayClassCountsAsync(employeeId);

            var classOrderIds = await BuildClassOrderIdsAsync(window, employeeId);

            // When viewing a single employee, union in orders they've been delegated to
            // for failure/incomplete card navigation (counts are unaffected).
            Dictionary<BonusStatClass, List<int>> delegateNavIds = null;
            if (!string.IsNullOrEmpty(employeeId))
                delegateNavIds = await BuildDelegateNavIdsAsync(window, employeeId);

            var stats = await BuildStatsAsync(window, employeeId, perEmployeeAttributions, todayCounts, classOrderIds, delegateNavIds);

            ProProgressCard pro = null;
            if (!string.IsNullOrEmpty(employeeId))
            {
                var selectedCountry = Common.EmployeeCountryToEnum(
                    employees.FirstOrDefault(e => e.ApplicationUserId == employeeId)?.Country);
                pro = BuildProProgress(employeeId, window, ratesByEmployee, perEmployeeAttributions, selectedCountry);
            }

            // Rows always list every employee (the filter narrows stats, not the table).
            // Closed-cycle payable splits into two kinds of unpaid orders:
            //   - BLOCKING: from a closed cycle that's never been paid yet → must use regular pay.
            //   - STRAGGLERS: from a closed cycle that's already been paid out at least once
            //     (late completions resurfacing) → these don't block early pay; they ride along
            //     with the next early-pay batch.
            // If any blocking dues exist, the row shows regular "دفع الشهر" and the regular
            // pay action picks up both blocking + stragglers (same code path as before).
            // If no blocking dues exist, the row shows yellow "دفع مبكر" with amount =
            // stragglers + current cycle.
            var closedCycles = ClosedCyclesOnly(window).ToList();
            var currentCycle = CurrentCycleOrNull(window);
            var rowPayable = new Dictionary<string, (decimal amount, int orderCount, bool isEarly)>();
            foreach (var emp in employees)
            {
                var (settled, neverPaid) = await PartitionClosedCyclesAsync(emp.ApplicationUserId, closedCycles);

                var blockingAttrs = neverPaid.Count > 0
                    ? await _bonusCalc.ComputeForEmployeeAcrossCyclesAsync(emp.ApplicationUserId, neverPaid, onlyUnpaid: true)
                    : new List<BonusCalculationService.OrderBonusAttribution>();
                var stragglerAttrs = settled.Count > 0
                    ? await _bonusCalc.ComputeForEmployeeAcrossCyclesAsync(emp.ApplicationUserId, settled, onlyUnpaid: true)
                    : new List<BonusCalculationService.OrderBonusAttribution>();

                var blockingAmount = blockingAttrs.Sum(a => a.BonusUsd);
                var stragglerAmount = stragglerAttrs.Sum(a => a.BonusUsd);

                if (blockingAmount > 0m || currentCycle == null)
                {
                    // Normal mode — regular pay includes both blocking + stragglers.
                    var amount = blockingAmount + stragglerAmount;
                    var count = blockingAttrs.Concat(stragglerAttrs).Select(a => a.OrderId).Distinct().Count();
                    rowPayable[emp.ApplicationUserId] = (amount, count, false);
                    continue;
                }

                var currentAttrs = await _bonusCalc.ComputeForEmployeeAsync(
                    emp.ApplicationUserId,
                    currentCycle.Value.Start,
                    currentCycle.Value.End,
                    onlyUnpaid: true);
                var earlyAmount = stragglerAmount + currentAttrs.Sum(a => a.BonusUsd);
                var earlyCount = stragglerAttrs.Concat(currentAttrs).Select(a => a.OrderId).Distinct().Count();
                rowPayable[emp.ApplicationUserId] = (earlyAmount, earlyCount, earlyAmount > 0m);
            }

            // Per-employee bonus order IDs across all stat classes (Success/Failure/Collection/
            // Incomplete/Delayed + ProcessedForOthers). Drives the row-click navigation, which
            // sends one URL covering every card for that employee.
            var rowOrderIds = await BuildRowOrderIdsAsync(window, employees.Select(e => e.ApplicationUserId).ToList());

            var rows = employees.Select(e =>
            {
                ratesByEmployee.TryGetValue(e.ApplicationUserId, out var rate);
                var payable = rowPayable[e.ApplicationUserId];
                return new EmployeeBonusIndexRow
                {
                    EmployeeId = e.ApplicationUserId,
                    EmployeeName = e.DisplayName ?? e.Name,
                    BonusPercentage = rate?.BonusPercentage ?? 0m,
                    BonusProcessingPercentage = rate?.BonusProcessingPercentage ?? 0m,
                    ProBonusPercentage = rate?.ProBonusPercentage ?? 0m,
                    ProBonusProcessingPercentage = rate?.ProBonusProcessingPercentage ?? 0m,
                    ProThreshold = rate?.ProThreshold ?? 0m,
                    PayableUsd = payable.amount,
                    PayableOrderCount = payable.orderCount,
                    IsEarlyPay = payable.isEarly,
                    IsBonusPanelHidden = rate?.IsBonusPanelHidden ?? false,
                    Country = Common.EmployeeCountryToEnum(e.Country),
                    AllBonusOrderIds = rowOrderIds.GetValueOrDefault(e.ApplicationUserId) ?? new List<int>()
                };
            }).ToList();

            var vm = new EmployeeBonusIndexViewModel
            {
                SelectedEmployeeId = employeeId,
                FilterStart = filterStart,
                FilterEnd = filterEnd,
                IsDateFiltered = window.IsDateFiltered,
                WindowStart = window.DisplayStart,
                WindowEnd = window.DisplayEnd,
                Stats = stats,
                ProProgress = pro,
                Rows = rows,
                EmployeeOptions = employeeOptions
            };

            return View(vm);
        }

        // ----- Pay -----
        // Stamps all unpaid orders from CLOSED cycles for a single employee with a
        // new EmployeeBonusPayment row. Current cycle is never included (plan line 57).

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(string employeeId, bool early = false)
        {
            if (string.IsNullOrEmpty(employeeId))
            {
                TempData["Error"] = "لم يتم تحديد الموظف.";
                return RedirectToAction(nameof(Index));
            }

            var window = _bonusWindow.GetActiveWindow(null, null);
            var closedCycles = ClosedCyclesOnly(window).ToList();

            List<BonusCalculationService.OrderBonusAttribution> attributions;
            if (early)
            {
                // Early pay precondition: no BLOCKING dues. Stragglers (unpaid orders from
                // a closed cycle that's already been paid out at least once) are allowed and
                // ride along in this batch.
                var (settled, neverPaid) = await PartitionClosedCyclesAsync(employeeId, closedCycles);
                if (neverPaid.Count > 0)
                {
                    var blockingAttrs = await _bonusCalc.ComputeForEmployeeAcrossCyclesAsync(
                        employeeId, neverPaid, onlyUnpaid: true);
                    if (blockingAttrs.Any(a => a.BonusUsd > 0m))
                    {
                        TempData["Error"] = "لا يمكن الدفع المبكر: يوجد مستحقات من فترات سابقة.";
                        return RedirectToAction(nameof(Index));
                    }
                }

                var currentCycle = CurrentCycleOrNull(window);
                if (currentCycle == null)
                {
                    TempData["Error"] = "لا توجد فترة حالية قابلة للدفع المبكر.";
                    return RedirectToAction(nameof(Index));
                }

                var stragglerAttrs = settled.Count > 0
                    ? await _bonusCalc.ComputeForEmployeeAcrossCyclesAsync(employeeId, settled, onlyUnpaid: true)
                    : new List<BonusCalculationService.OrderBonusAttribution>();
                var currentAttrs = await _bonusCalc.ComputeForEmployeeAsync(
                    employeeId,
                    currentCycle.Value.Start,
                    currentCycle.Value.End,
                    onlyUnpaid: true);
                attributions = stragglerAttrs.Concat(currentAttrs).ToList();
            }
            else
            {
                if (closedCycles.Count == 0)
                {
                    TempData["Error"] = "لا توجد فترات سابقة قابلة للدفع.";
                    return RedirectToAction(nameof(Index));
                }

                attributions = await _bonusCalc.ComputeForEmployeeAcrossCyclesAsync(
                    employeeId,
                    closedCycles,
                    onlyUnpaid: true);
            }

            if (attributions.Count == 0)
            {
                TempData["Error"] = "لا توجد عمولات غير مدفوعة لهذا الموظف.";
                return RedirectToAction(nameof(Index));
            }

            var successAmount = attributions.Where(a => a.IsCreator).Sum(a => a.BonusUsd);
            var processingAmount = attributions.Where(a => a.IsProcessor).Sum(a => a.BonusUsd);
            var amountPaid = successAmount + processingAmount;

            var proAttributions = attributions.Where(a => a.UsedProRate).ToList();
            var proExtra = ComputeProExtraDelta(employeeId, proAttributions);

            var successOrderIds = attributions.Where(a => a.IsCreator).Select(a => a.OrderId).Distinct().ToList();
            var processingOrderIds = attributions.Where(a => a.IsProcessor).Select(a => a.OrderId).Distinct().ToList();
            var proOrderIds = proAttributions.Select(a => a.OrderId).Distinct().ToList();
            var allOrderIds = attributions.Select(a => a.OrderId).Distinct().ToList();

            var payment = new EmployeeBonusPayment
            {
                EmployeeId = employeeId,
                DatePaid = _time.GetIstanbulTimeWithOffset(),
                AmountPaid = amountPaid,
                ProExtraAmount = proExtra,
                TotalOrderCount = allOrderIds.Count,
                ProOrderCount = proOrderIds.Count,
                SuccessOrderCount = successOrderIds.Count,
                ProcessingOrderCount = processingOrderIds.Count,
                ProcessingAmount = processingAmount,
                SuccessAmount = successAmount
            };

            // Stamp orders via the navigation property so EF inserts payment + updates
            // orders inside one SaveChanges transaction (avoids an orphan payment row
            // if the order update fails).
            var orders = await _context.Orders
                .Where(o => allOrderIds.Contains(o.Id) && o.BonusPaymentId == null)
                .ToListAsync();
            foreach (var o in orders)
            {
                o.BonusPayment = payment;
            }
            _context.EmployeeBonusPayments.Add(payment);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"تم دفع العمولات بنجاح. المبلغ: ${amountPaid:0.##}.";
            return RedirectToAction(nameof(Index));
        }

        // ----- Archive -----

        [HttpGet]
        public async Task<IActionResult> Archive(string startDate, string endDate, string employeeId)
        {
            var filterStart = ParseFilterDate(startDate);
            var filterEnd = ParseFilterDate(endDate);

            var employees = (await _dataCacheService.GetCachedEmployeesAsync())
                .Where(e => !string.IsNullOrEmpty(e.ApplicationUserId))
                .OrderBy(e => e.DisplayName ?? e.Name)
                .ToList();
            var employeeNameById = employees.ToDictionary(
                e => e.ApplicationUserId,
                e => e.DisplayName ?? e.Name);

            var query = _context.EmployeeBonusPayments.AsQueryable();
            if (filterStart.HasValue)
            {
                var s = filterStart.Value.Date.AddHours(10).AddMinutes(30);
                query = query.Where(p => p.DatePaid >= s);
            }
            if (filterEnd.HasValue)
            {
                var e = filterEnd.Value.Date.AddDays(1).AddHours(10).AddMinutes(30);
                query = query.Where(p => p.DatePaid < e);
            }
            if (!string.IsNullOrEmpty(employeeId))
            {
                query = query.Where(p => p.EmployeeId == employeeId);
            }

            var payments = await query
                .OrderByDescending(p => p.DatePaid)
                .ToListAsync();

            var rows = payments.Select(p => new EmployeeBonusArchiveRow
            {
                PaymentId = p.Id,
                EmployeeId = p.EmployeeId,
                EmployeeName = employeeNameById.GetValueOrDefault(p.EmployeeId, p.EmployeeId),
                DatePaid = p.DatePaid,
                AmountPaid = p.AmountPaid,
                ProExtraAmount = p.ProExtraAmount,
                TotalOrderCount = p.TotalOrderCount,
                ProOrderCount = p.ProOrderCount,
                SuccessOrderCount = p.SuccessOrderCount,
                ProcessingOrderCount = p.ProcessingOrderCount,
                ProcessingAmount = p.ProcessingAmount,
                SuccessAmount = p.SuccessAmount
            }).ToList();

            return View(new EmployeeBonusArchiveViewModel
            {
                SelectedEmployeeId = employeeId,
                FilterStart = filterStart,
                FilterEnd = filterEnd,
                Rows = rows,
                EmployeeOptions = employees
                    .Select(e => new EmployeeOption { Id = e.ApplicationUserId, Name = e.DisplayName ?? e.Name })
                    .ToList()
            });
        }

        // ----- Create -----

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var viewModel = await BuildCreateViewModel();
            return View(viewModel);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeBonusCreateViewModel viewModel)
        {
            if (viewModel?.Rows == null || viewModel.Rows.Count == 0)
            {
                TempData["Error"] = "لم يتم تقديم أي بيانات.";
                return RedirectToAction(nameof(Create));
            }

            var employeeIds = viewModel.Rows
                .Where(r => !string.IsNullOrEmpty(r.EmployeeId))
                .Select(r => r.EmployeeId)
                .ToList();

            var existingRates = await _context.EmployeeBonusRates
                .Where(r => employeeIds.Contains(r.EmployeeId))
                .ToListAsync();

            var existingByEmployee = existingRates.ToDictionary(r => r.EmployeeId, r => r);

            foreach (var row in viewModel.Rows)
            {
                if (string.IsNullOrEmpty(row.EmployeeId))
                    continue;

                // ProThreshold is stored exactly as the admin entered it — in the employee's
                // local currency (USD when no country is set). The runtime comparison in
                // BonusCalculationService converts cycle USD profit into the same currency,
                // so we no longer round-trip through ConvertToUSD here (the spread between
                // buy/sell rates was drifting the stored value on every save).

                if (existingByEmployee.TryGetValue(row.EmployeeId, out var existing))
                {
                    existing.BonusPercentage = row.BonusPercentage;
                    existing.BonusProcessingPercentage = row.BonusProcessingPercentage;
                    existing.ProBonusPercentage = row.ProBonusPercentage;
                    existing.ProBonusProcessingPercentage = row.ProBonusProcessingPercentage;
                    existing.ProThreshold = row.ProThreshold;
                }
                else
                {
                    _context.EmployeeBonusRates.Add(new EmployeeBonusRate
                    {
                        EmployeeId = row.EmployeeId,
                        BonusPercentage = row.BonusPercentage,
                        BonusProcessingPercentage = row.BonusProcessingPercentage,
                        ProBonusPercentage = row.ProBonusPercentage,
                        ProBonusProcessingPercentage = row.ProBonusProcessingPercentage,
                        ProThreshold = row.ProThreshold
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "تم حفظ نسب العمولة بنجاح.";
            return RedirectToAction(nameof(Create));
        }

        // ----- helpers -----

        private static DateTime? ParseFilterDate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            if (DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var d))
                return d;
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2))
                return d2;
            return null;
        }

        private async Task<EmployeeBonusCreateViewModel> BuildCreateViewModel()
        {
            var employees = await _dataCacheService.GetCachedEmployeesAsync();
            var employeeUserIds = employees
                .Where(e => !string.IsNullOrEmpty(e.ApplicationUserId))
                .Select(e => e.ApplicationUserId)
                .ToList();

            var rates = await _context.EmployeeBonusRates
                .Where(r => employeeUserIds.Contains(r.EmployeeId))
                .ToListAsync();
            var ratesByEmployee = rates.ToDictionary(r => r.EmployeeId, r => r);

            var rows = employees
                .Where(e => !string.IsNullOrEmpty(e.ApplicationUserId))
                .OrderBy(e => e.Name)
                .Select(e =>
                {
                    ratesByEmployee.TryGetValue(e.ApplicationUserId, out var rate);
                    return new EmployeeBonusRateRow
                    {
                        EmployeeId = e.ApplicationUserId,
                        EmployeeName = e.DisplayName ?? e.Name,
                        BonusPercentage = rate?.BonusPercentage ?? 0m,
                        BonusProcessingPercentage = rate?.BonusProcessingPercentage ?? 0m,
                        ProBonusPercentage = rate?.ProBonusPercentage ?? 0m,
                        ProBonusProcessingPercentage = rate?.ProBonusProcessingPercentage ?? 0m,
                        ProThreshold = rate?.ProThreshold ?? 0m,
                        Country = Common.EmployeeCountryToEnum(e.Country)
                    };
                })
                .ToList();

            return new EmployeeBonusCreateViewModel { Rows = rows };
        }

        // Cycles excluding the current (still-open) one. Pay can only target closed cycles.
        private IEnumerable<(DateTime Start, DateTime End)> ClosedCyclesOnly(BonusWindow window)
        {
            if (window.IsDateFiltered)
            {
                return window.CalcCycles;
            }
            var now = _time.GetIstanbulTimeWithOffset();
            var currentStart = _bonusWindow.CycleStartFor(now);
            return window.CalcCycles.Where(c => c.Start < currentStart);
        }

        // The currently-open cycle, or null when the page is date-filtered (early pay is
        // disabled under a custom filter window since "current" is ambiguous there).
        private (DateTime Start, DateTime End)? CurrentCycleOrNull(BonusWindow window)
        {
            if (window.IsDateFiltered) return null;
            var now = _time.GetIstanbulTimeWithOffset();
            var start = _bonusWindow.CycleStartFor(now);
            return (start, start.AddMonths(1));
        }

        // Splits closed cycles into:
        //   - Settled: at least one EmployeeBonusPayment exists for this employee dated on/after
        //     the cycle's end (i.e., admin already ran pay for that cycle at least once).
        //     Unpaid eligible orders here are "stragglers" — late completions resurfacing
        //     after the cycle was settled.
        //   - NeverPaid: no payment exists past the cycle's end. Unpaid eligible orders here
        //     are first-time dues and must go through regular pay.
        private async Task<(List<(DateTime Start, DateTime End)> Settled, List<(DateTime Start, DateTime End)> NeverPaid)>
            PartitionClosedCyclesAsync(string employeeId, List<(DateTime Start, DateTime End)> closedCycles)
        {
            if (closedCycles.Count == 0)
            {
                return (new List<(DateTime, DateTime)>(), new List<(DateTime, DateTime)>());
            }
            var paymentDates = await _context.EmployeeBonusPayments
                .Where(p => p.EmployeeId == employeeId)
                .Select(p => p.DatePaid)
                .ToListAsync();
            var settled = closedCycles.Where(c => paymentDates.Any(d => d >= c.End)).ToList();
            var neverPaid = closedCycles.Where(c => !paymentDates.Any(d => d >= c.End)).ToList();
            return (settled, neverPaid);
        }

        private async Task<List<BonusStatCard>> BuildStatsAsync(
            BonusWindow window,
            string employeeId,
            Dictionary<string, List<BonusCalculationService.OrderBonusAttribution>> perEmployeeAttributions,
            Dictionary<BonusStatClass, int> todayCounts,
            Dictionary<BonusStatClass, List<int>> classOrderIds,
            Dictionary<BonusStatClass, List<int>> delegateNavIds = null)
        {
            // $ amounts come from already-computed attributions.
            var successAmount = perEmployeeAttributions.Values
                .SelectMany(v => v)
                .Where(a => a.IsCreator)
                .Sum(a => a.BonusUsd);
            var processedForOthersAmount = perEmployeeAttributions.Values
                .SelectMany(v => v)
                .Where(a => a.IsProcessor)
                .Sum(a => a.BonusUsd);
            var successProfit = perEmployeeAttributions.Values
                .SelectMany(v => v)
                .Where(a => a.IsCreator)
                .Sum(a => a.ProfitUsd);
            var processedForOthersProfit = perEmployeeAttributions.Values
                .SelectMany(v => v)
                .Where(a => a.IsProcessor)
                .Sum(a => a.ProfitUsd);

            // Order counts come from a direct query — counts include all classes
            // (not just Success), so we can't derive them from attributions alone.
            var classCounts = await BuildClassCountsAsync(window, employeeId);

            // Collection-class projected $: per-employee, pro-aware per cycle. Summed
            // across the target set so the card mirrors the rest of the stats.
            decimal collectionProfit = 0m;
            decimal collectionBonus = 0m;
            foreach (var kvp in perEmployeeAttributions)
            {
                var proj = await _bonusCalc.ComputeCollectionProjectedAcrossCyclesAsync(
                    kvp.Key, window.CalcCycles, kvp.Value);
                collectionProfit += proj.ProfitUsd;
                collectionBonus += proj.BonusUsd;
            }

            // todayCounts is non-null only when no filter is active (unfiltered view).
            // When a filter is applied (date range or specific employee), ContextCount
            // mirrors OrderCount so the view can show a consistent "date range" label.
            int TodayCtx(BonusStatClass cls) =>
                todayCounts != null
                    ? todayCounts.GetValueOrDefault(cls)
                    : classCounts.GetValueOrDefault(cls);
            int TodayCtxSentinel() =>
                todayCounts != null
                    ? todayCounts.GetValueOrDefault((BonusStatClass)100)
                    : classCounts.GetValueOrDefault((BonusStatClass)100);

            List<int> Ids(BonusStatClass cls) =>
                classOrderIds != null && classOrderIds.TryGetValue(cls, out var l) ? l : new List<int>();

            // Returns the navigation union of classOrderIds + delegateNavIds for the given class.
            // Null when there are no extras (avoids allocating a duplicate list).
            List<int> NavIds(BonusStatClass cls)
            {
                var baseIds = Ids(cls);
                if (delegateNavIds == null || !delegateNavIds.TryGetValue(cls, out var extra) || extra.Count == 0)
                    return null;
                return baseIds.Union(extra).ToList();
            }

            var netProfits = await BuildClassNetProfitAsync(window, employeeId);

            var stats = new List<BonusStatCard>
            {
                new BonusStatCard
                {
                    Label = BonusStatusGroups.ArabicLabel(BonusStatClass.Success),
                    ColorHex = BonusStatusGroups.ColorHex(BonusStatClass.Success),
                    OrderCount = classCounts.GetValueOrDefault(BonusStatClass.Success),
                    ContextCount = TodayCtx(BonusStatClass.Success),
                    AmountUsd = successAmount,
                    ProfitUsd = successProfit,
                    OrderIds = Ids(BonusStatClass.Success)
                },
                new BonusStatCard
                {
                    Label = "الطلبات المعالجة",
                    ColorHex = BonusStatusGroups.ProcessedForOthersColor,
                    OrderCount = classCounts.GetValueOrDefault((BonusStatClass)100),
                    ContextCount = TodayCtxSentinel(),
                    AmountUsd = processedForOthersAmount,
                    ProfitUsd = processedForOthersProfit,
                    OrderIds = Ids((BonusStatClass)100)
                },
                new BonusStatCard
                {
                    Label = BonusStatusGroups.ArabicLabel(BonusStatClass.Failure),
                    ColorHex = BonusStatusGroups.ColorHex(BonusStatClass.Failure),
                    OrderCount = classCounts.GetValueOrDefault(BonusStatClass.Failure),
                    ContextCount = TodayCtx(BonusStatClass.Failure),
                    NetProfitUsd = netProfits.GetValueOrDefault(BonusStatClass.Failure),
                    OrderIds = Ids(BonusStatClass.Failure),
                    NavOrderIds = NavIds(BonusStatClass.Failure),
                },
                new BonusStatCard
                {
                    Label = BonusStatusGroups.ArabicLabel(BonusStatClass.Collection),
                    ColorHex = BonusStatusGroups.ColorHex(BonusStatClass.Collection),
                    OrderCount = classCounts.GetValueOrDefault(BonusStatClass.Collection),
                    ContextCount = TodayCtx(BonusStatClass.Collection),
                    AmountUsd = collectionBonus,
                    ProfitUsd = collectionProfit,
                    OrderIds = Ids(BonusStatClass.Collection)
                },
                new BonusStatCard
                {
                    Label = BonusStatusGroups.ArabicLabel(BonusStatClass.Incomplete),
                    ColorHex = BonusStatusGroups.ColorHex(BonusStatClass.Incomplete),
                    OrderCount = classCounts.GetValueOrDefault(BonusStatClass.Incomplete),
                    ContextCount = TodayCtx(BonusStatClass.Incomplete),
                    NetProfitUsd = netProfits.GetValueOrDefault(BonusStatClass.Incomplete),
                    OrderIds = Ids(BonusStatClass.Incomplete),
                    NavOrderIds = NavIds(BonusStatClass.Incomplete)
                },
                new BonusStatCard
                {
                    Label = BonusStatusGroups.ArabicLabel(BonusStatClass.Delayed),
                    ColorHex = BonusStatusGroups.ColorHex(BonusStatClass.Delayed),
                    OrderCount = classCounts.GetValueOrDefault(BonusStatClass.Delayed),
                    ContextCount = TodayCtx(BonusStatClass.Delayed),
                    NetProfitUsd = netProfits.GetValueOrDefault(BonusStatClass.Delayed),
                    OrderIds = Ids(BonusStatClass.Delayed)
                }
            };
            return stats;
        }

        // Parallel to BuildClassCountsAsync: returns the IDs that compose each card so the
        // view can emit data-order-ids and click-through to /Home/Index?orderIds=…
        private async Task<Dictionary<BonusStatClass, List<int>>> BuildClassOrderIdsAsync(
            BonusWindow window,
            string employeeId)
        {
            var result = new Dictionary<BonusStatClass, List<int>>();

            var baseQuery = _context.Orders
                .Where(o => o.CreatedDate >= window.DisplayStart && o.CreatedDate < window.DisplayEnd)
                .Where(o => o.BonusPaymentId == null);

            if (!string.IsNullOrEmpty(employeeId))
            {
                baseQuery = baseQuery.Where(o =>
                    o.ApplicationUserId == employeeId
                    || (o.Fixedby != null && o.Fixedby == employeeId));
            }

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

        // Per-employee union of bonus-classifiable orders in the active window. One row per
        // employee — clicking the row sends every one of these IDs as a single ?orderIds=…
        // navigation, so the home page shows all of that employee's cards at once.
        private async Task<Dictionary<string, List<int>>> BuildRowOrderIdsAsync(
            BonusWindow window,
            List<string> employeeIds)
        {
            if (employeeIds == null || employeeIds.Count == 0)
                return new Dictionary<string, List<int>>();

            var validStatuses = BonusStatusGroups.Success
                .Concat(BonusStatusGroups.Failure)
                .Concat(BonusStatusGroups.Collection)
                .Concat(BonusStatusGroups.Incomplete)
                .Concat(BonusStatusGroups.Delayed)
                .Distinct()
                .ToArray();

            var raw = await _context.Orders
                .Where(o => o.CreatedDate >= window.DisplayStart && o.CreatedDate < window.DisplayEnd)
                .Where(o => o.BonusPaymentId == null)
                .Where(o => validStatuses.Contains(o.OrderStatus))
                .Where(o => employeeIds.Contains(o.ApplicationUserId)
                            || (o.Fixedby != null && employeeIds.Contains(o.Fixedby))
                            || (o.DelegateEmployeeId != null && employeeIds.Contains(o.DelegateEmployeeId)))
                .Select(o => new { o.Id, o.ApplicationUserId, o.Fixedby, o.DelegateEmployeeId })
                .ToListAsync();

            var byEmployee = employeeIds.ToDictionary(id => id, _ => new HashSet<int>());
            foreach (var o in raw)
            {
                if (!string.IsNullOrEmpty(o.ApplicationUserId) && byEmployee.ContainsKey(o.ApplicationUserId))
                    byEmployee[o.ApplicationUserId].Add(o.Id);
                if (!string.IsNullOrEmpty(o.Fixedby) && o.Fixedby != o.ApplicationUserId && byEmployee.ContainsKey(o.Fixedby))
                    byEmployee[o.Fixedby].Add(o.Id);
                if (!string.IsNullOrEmpty(o.DelegateEmployeeId) && byEmployee.ContainsKey(o.DelegateEmployeeId))
                    byEmployee[o.DelegateEmployeeId].Add(o.Id);
            }
            return byEmployee.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList());
        }

        // Orders delegated to a specific employee (DelegateEmployeeId) in the window,
        // for failure and incomplete classes only. Used to enrich nav IDs without touching counts.
        private async Task<Dictionary<BonusStatClass, List<int>>> BuildDelegateNavIdsAsync(
            BonusWindow window,
            string employeeId)
        {
            var baseQuery = _context.Orders
                .Where(o => o.DelegateEmployeeId == employeeId)
                .Where(o => o.CreatedDate >= window.DisplayStart && o.CreatedDate < window.DisplayEnd);

            var failureIds = await baseQuery
                .Where(o => BonusStatusGroups.Failure.Contains(o.OrderStatus))
                .Select(o => o.Id)
                .ToListAsync();

            var incompleteIds = await baseQuery
                .Where(o => BonusStatusGroups.Incomplete.Contains(o.OrderStatus))
                .Select(o => o.Id)
                .ToListAsync();

            return new Dictionary<BonusStatClass, List<int>>
            {
                [BonusStatClass.Failure] = failureIds,
                [BonusStatClass.Incomplete] = incompleteIds
            };
        }

        private async Task<Dictionary<BonusStatClass, decimal>> BuildClassNetProfitAsync(
            BonusWindow window,
            string employeeId)
        {
            var baseQuery = _context.Orders
                .Where(o => o.CreatedDate >= window.DisplayStart && o.CreatedDate < window.DisplayEnd)
                .Where(o => o.BonusPaymentId == null);

            if (!string.IsNullOrEmpty(employeeId))
            {
                baseQuery = baseQuery.Where(o =>
                    o.ApplicationUserId == employeeId
                    || (o.Fixedby != null && o.Fixedby == employeeId));
            }

            async Task<decimal> SumAsync(OrderStatusEnum[] statuses)
            {
                var rows = await baseQuery
                    .Where(o => statuses.Contains(o.OrderStatus))
                    .Select(o => new { o.TotalPrice, o.DeliveryPrice, Country = o.Country.ToString() })
                    .ToListAsync();
                return rows.Sum(r => _fx.ConvertToUSD(r.TotalPrice - r.DeliveryPrice, r.Country));
            }

            return new Dictionary<BonusStatClass, decimal>
            {
                [BonusStatClass.Failure]    = await SumAsync(BonusStatusGroups.Failure),
                [BonusStatClass.Incomplete] = await SumAsync(BonusStatusGroups.Incomplete),
                [BonusStatClass.Delayed]    = await SumAsync(BonusStatusGroups.Delayed),
            };
        }

        private async Task<Dictionary<BonusStatClass, int>> BuildClassCountsAsync(
            BonusWindow window,
            string employeeId)
        {
            // Sentinel: (BonusStatClass)100 = ProcessedForOthers (orders in Success class
            // where Fixedby != null AND Fixedby != ApplicationUserId).
            var result = new Dictionary<BonusStatClass, int>();

            var baseQuery = _context.Orders
                .Where(o => o.CreatedDate >= window.DisplayStart && o.CreatedDate < window.DisplayEnd)
                .Where(o => o.BonusPaymentId == null);

            if (!string.IsNullOrEmpty(employeeId))
            {
                baseQuery = baseQuery.Where(o =>
                    o.ApplicationUserId == employeeId
                    || (o.Fixedby != null && o.Fixedby == employeeId));
            }

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

            // ProcessedForOthers count (sentinel key = (BonusStatClass)100).
            var successStatuses = BonusStatusGroups.Success;
            var processedForOthersCount = await baseQuery
                .Where(o => successStatuses.Contains(o.OrderStatus))
                .Where(o => o.Fixedby != null && o.Fixedby != o.ApplicationUserId)
                .CountAsync();
            result[(BonusStatClass)100] = processedForOthersCount;

            return result;
        }

        // Returns counts scoped to today's shift (10:30–10:30) for each stat class.
        // Used as the ContextCount when no date filter is active.
        private async Task<Dictionary<BonusStatClass, int>> BuildTodayClassCountsAsync(string employeeId)
        {
            var now = _time.GetIstanbulTimeWithOffset();
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
                .Where(o => o.InstantAddedDate >= shiftStart && o.InstantAddedDate < shiftEnd);

            if (!string.IsNullOrEmpty(employeeId))
            {
                baseQuery = baseQuery.Where(o =>
                    o.ApplicationUserId == employeeId
                    || (o.Fixedby != null && o.Fixedby == employeeId));
            }

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
            var processedForOthersCount = await baseQuery
                .Where(o => successStatuses.Contains(o.OrderStatus))
                .Where(o => o.Fixedby != null && o.Fixedby != o.ApplicationUserId)
                .CountAsync();
            result[(BonusStatClass)100] = processedForOthersCount;

            return result;
        }

        private ProProgressCard BuildProProgress(
            string employeeId,
            BonusWindow window,
            Dictionary<string, EmployeeBonusRate> ratesByEmployee,
            Dictionary<string, List<BonusCalculationService.OrderBonusAttribution>> perEmployeeAttributions,
            Common.Countries? country)
        {
            if (!ratesByEmployee.TryGetValue(employeeId, out var rate) || rate.ProThreshold <= 0m)
            {
                return new ProProgressCard { ThresholdReached = false };
            }

            // ProThreshold is stored in the employee's local currency. Translate to USD
            // so it can be compared (and shown alongside) the cycle's USD profit.
            var thresholdUsd = country.HasValue
                ? _fx.ConvertToUSD(rate.ProThreshold, country.Value)
                : rate.ProThreshold;

            // Pro is evaluated per cycle. Show progress for the LATEST cycle in the window
            // (current cycle when not date-filtered, full filter window otherwise).
            var attrs = perEmployeeAttributions.GetValueOrDefault(employeeId) ?? new List<BonusCalculationService.OrderBonusAttribution>();
            var lastCycle = window.CalcCycles.LastOrDefault();
            var lastCycleAttrs = lastCycle.Start == default
                ? attrs
                : attrs.Where(a => a.AccountingTime >= lastCycle.Start && a.AccountingTime < lastCycle.End).ToList();

            // Profit per attribution row already represents the employee's attributable
            // share of that order; sum over distinct orders to avoid double-counting when
            // an employee is both creator and processor of the same order.
            var profit = lastCycleAttrs.Sum(a => a.ProfitUsd);
            var remaining = Math.Max(0m, thresholdUsd - profit);

            return new ProProgressCard
            {
                CycleProfitUsd = profit,
                ThresholdUsd = thresholdUsd,
                RemainingUsd = remaining,
                ThresholdReached = profit >= thresholdUsd
            };
        }

        // ----- ToggleBonusPanel -----

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleBonusPanel(string employeeId, bool hidden)
        {
            if (string.IsNullOrEmpty(employeeId))
                return Json(new { success = false });

            var rate = await _context.EmployeeBonusRates
                .FirstOrDefaultAsync(r => r.EmployeeId == employeeId);

            if (rate == null)
            {
                rate = new EmployeeBonusRate { EmployeeId = employeeId, IsBonusPanelHidden = hidden };
                _context.EmployeeBonusRates.Add(rate);
            }
            else
            {
                rate.IsBonusPanelHidden = hidden;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // Difference between bonus paid at the pro rate and what it would have been at
        // the base rate. Used to populate the "pro extra" column on EmployeeBonusPayment rows.
        private decimal ComputeProExtraDelta(string employeeId, List<BonusCalculationService.OrderBonusAttribution> proAttrs)
        {
            if (proAttrs.Count == 0) return 0m;
            var rate = _context.EmployeeBonusRates
                .AsNoTracking()
                .FirstOrDefault(r => r.EmployeeId == employeeId);
            if (rate == null) return 0m;

            decimal proExtra = 0m;
            foreach (var a in proAttrs)
            {
                if (a.IsCreator)
                {
                    var baseShare = a.ProfitUsd * (rate.BonusPercentage / 100m);
                    var proShare  = a.ProfitUsd * (rate.ProBonusPercentage / 100m);
                    proExtra += proShare - baseShare;
                }
                else if (a.IsProcessor)
                {
                    var baseShare = a.ProfitUsd * (rate.BonusProcessingPercentage / 100m);
                    var proShare  = a.ProfitUsd * (rate.ProBonusProcessingPercentage / 100m);
                    proExtra += proShare - baseShare;
                }
            }
            return proExtra;
        }
    }
}
