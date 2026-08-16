using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.Models.ViewModel;
using lotus_blue.Services;

public class EmployeeTransactionsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly GetCurrentTimeInIstanbul _timeService;
    private readonly DecimalFormattingService _decimalFormattingService;

    public EmployeeTransactionsController(
        ApplicationDbContext context,
        GetCurrentTimeInIstanbul timeservice,
        DecimalFormattingService decimalFormattingService)
    {
        _context = context;
        _timeService = timeservice;
        _decimalFormattingService = decimalFormattingService;
    }

    // GET: EmployeeTransactions
    [Authorize(Roles = "Admin,Accountant,Observer,ExecutiveDirector")]
    public async Task<IActionResult> Index(
        int page = 1,
        int pageSize = 10,
        int? employeeId = null,
        bool formerEmployees = false,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 10 : pageSize;

        var allowedPageSizes = new[] { 10, 25, 50, 100, 200, 300, 500, 1000 };
        if (!allowedPageSizes.Contains(pageSize))
        {
            pageSize = 10;
        }

        // نزامن خصومات سجل الدوام أولًا حتى تكون صفحة المكافآت والخصومات مطابقة لنفس فلتر سجل الدوام.
        await SyncAttendanceDeductionsIntoEmployeeTransactionsAsync(employeeId, formerEmployees, fromDate, toDate);

        var query = _context.EmployeeTransactions
            .Include(et => et.Employee)
            .AsNoTracking()
            .Where(et => !et.IsDeleted)
            .AsQueryable();

        if (employeeId.HasValue && employeeId.Value > 0)
        {
            query = query.Where(et => et.EmployeeId == employeeId.Value);
        }
        else if (formerEmployees)
        {
            query = query.Where(et => et.Employee != null && !et.Employee.IsActive);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(et => et.Date >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            var toDateExclusive = toDate.Value.Date.AddDays(1);
            query = query.Where(et => et.Date < toDateExclusive);
        }

        /*
            مهم:
            نعرض الخصومات في نفس اليوم للموظف كصف واحد فقط.
            مثال: تأخير دخول + خروج مبكر في نفس اليوم = صف واحد بإجمالي الخصم وسببين مدموجين.
        */
        var allFilteredTransactions = await query
            .OrderByDescending(et => et.Date)
            .ThenByDescending(et => et.Id)
            .ToListAsync();

        decimal totalTransactionsAmount = allFilteredTransactions.Sum(et => et.Amount);

        var groupedTransactions = allFilteredTransactions
            .GroupBy(et => new
            {
                et.EmployeeId,
                TransactionDay = et.Date.Date,
                et.TransactionType
            })
            .Select(group =>
            {
                var orderedGroup = group
                    .OrderByDescending(t => t.Date)
                    .ThenByDescending(t => t.Id)
                    .ToList();

                var primary = orderedGroup.First();
                var totalAmount = orderedGroup.Sum(t => t.Amount);

                return new GroupedEmployeeTransactionForList
                {
                    PrimaryTransactionId = primary.Id,
                    EmployeeId = primary.EmployeeId,
                    Employee = primary.Employee,
                    TransactionType = primary.TransactionType,
                    TransactionDay = group.Key.TransactionDay,
                    DisplayDate = orderedGroup.Max(t => t.Date),
                    Amount = totalAmount,
                    Reason = BuildDailyTransactionReasonText(orderedGroup.Select(t => t.Reason)),
                    GroupTransactionIds = orderedGroup.Select(t => t.Id).ToList()
                };
            })
            .OrderByDescending(row => row.TransactionDay)
            .ThenByDescending(row => row.PrimaryTransactionId)
            .ToList();

        int totalItems = groupedTransactions.Count;

        var transactions = groupedTransactions
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var employeeIds = transactions
            .Select(et => et.EmployeeId)
            .Distinct()
            .ToList();

        var latestShiftRows = await _context.EmployeeWorkShifts
            .AsNoTracking()
            .Where(s => employeeIds.Contains(s.EmployeeId))
            .OrderByDescending(s => s.IsActive)
            .ThenByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .Select(s => new EmployeeTransactionShiftLookupRow
            {
                EmployeeId = s.EmployeeId,
                ShiftStartTime = s.ShiftStartTime,
                ShiftEndTime = s.ShiftEndTime
            })
            .ToListAsync();

        var shiftMap = latestShiftRows
            .GroupBy(s => s.EmployeeId)
            .ToDictionary(g => g.Key, g => g.First());

        string formattedTotal = _decimalFormattingService.DecimalFormat(totalTransactionsAmount);

        var transactionViewModels = transactions.Select(et =>
        {
            shiftMap.TryGetValue(et.EmployeeId, out var shift);

            var employeeName = BuildEmployeeName(et.Employee);

            return new EmployeeTransactionViewModel
            {
                Id = et.PrimaryTransactionId,
                EmployeeId = et.EmployeeId,
                EmployeeName = employeeName,
                EmployeeImagePath = NormalizeEmployeeImagePath(et.Employee?.ImageUrl),
                EmployeeIsActive = et.Employee?.IsActive ?? false,
                Amount = et.Amount,
                TransactionType = et.TransactionType,
                Reason = et.Reason,
                TotalDiscountPriceTRY = formattedTotal,
                TransactionDate = et.DisplayDate.ToString("yyyy-MM-dd HH:mm:ss"),
                TransactionDateOnly = et.TransactionDay.ToString("yyyy-MM-dd"),
                ShiftStartTimeText = shift == null ? "-" : FormatTime(shift.ShiftStartTime),
                ShiftEndTimeText = shift == null ? "-" : FormatTime(shift.ShiftEndTime),
                DeductionAmount = et.TransactionType == TransactionTypeEnum.خصم ? et.Amount : 0m,
                AdvanceAmount = et.TransactionType == TransactionTypeEnum.سلفة ? et.Amount : 0m,
                BonusAmount = et.TransactionType == TransactionTypeEnum.مكافأة ? et.Amount : 0m
            };
        }).ToList();

        var viewModel = new PaginationViewModel<EmployeeTransactionViewModel>
        {
            Items = transactionViewModels,
            CurrentPage = page,
            PageSize = pageSize,
            TotalItems = totalItems
        };

        ViewBag.ActiveEmployees = await GetEmployeesSelectListAsync(isActive: true);
        ViewBag.FormerEmployees = await GetEmployeesSelectListAsync(isActive: false);
        ViewBag.FilterEmployeeId = employeeId?.ToString() ?? string.Empty;
        ViewBag.FormerEmployeesFilter = formerEmployees;
        ViewBag.FilterFromDate = fromDate?.ToString("yyyy-MM-dd") ?? string.Empty;
        ViewBag.FilterToDate = toDate?.ToString("yyyy-MM-dd") ?? string.Empty;

        return View(viewModel);
    }

    // GET: EmployeeTransactions/Create
    [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
    public IActionResult Create()
    {
        var model = new EmployeeTransactionViewModel();
        return View(model);
    }

    // POST: EmployeeTransactions/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
    public async Task<IActionResult> Create(EmployeeTransactionViewModel model)
    {
        if (ModelState.IsValid)
        {
            var employeeTransaction = new EmployeeTransaction
            {
                Amount = model.Amount,
                TransactionType = model.TransactionType,
                Reason = model.Reason,
                Date = _timeService.GetIstanbulTimeWithOffset(),
                EmployeeId = model.EmployeeId
            };

            _context.Add(employeeTransaction);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }



    [HttpGet]
    [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
    public async Task<IActionResult> GetTransactionForEdit(int id)
    {
        if (id <= 0)
        {
            return Json(new { success = false, message = "لم يتم العثور على رقم الحركة" });
        }

        var employeeTransaction = await _context.EmployeeTransactions
            .AsNoTracking()
            .Include(t => t.Employee)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

        if (employeeTransaction == null)
        {
            return Json(new { success = false, message = "الحركة غير موجودة أو موجودة في سلة المهملات" });
        }

        var dayStart = employeeTransaction.Date.Date;
        var dayEnd = dayStart.AddDays(1);

        var sameDayTransactions = await _context.EmployeeTransactions
            .AsNoTracking()
            .Where(t =>
                !t.IsDeleted &&
                t.EmployeeId == employeeTransaction.EmployeeId &&
                t.TransactionType == employeeTransaction.TransactionType &&
                t.Date >= dayStart &&
                t.Date < dayEnd)
            .OrderBy(t => t.Id)
            .ToListAsync();

        var totalAmount = sameDayTransactions.Sum(t => t.Amount);
        var combinedReason = BuildDailyTransactionReasonText(sameDayTransactions.Select(t => t.Reason));

        var shift = await _context.EmployeeWorkShifts
            .AsNoTracking()
            .Where(s => s.EmployeeId == employeeTransaction.EmployeeId)
            .OrderByDescending(s => s.IsActive)
            .ThenByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .Select(s => new EmployeeTransactionShiftLookupRow
            {
                EmployeeId = s.EmployeeId,
                ShiftStartTime = s.ShiftStartTime,
                ShiftEndTime = s.ShiftEndTime
            })
            .FirstOrDefaultAsync();

        var employeeName = BuildEmployeeName(employeeTransaction.Employee);

        return Json(new
        {
            success = true,
            data = new
            {
                id = employeeTransaction.Id,
                employeeId = employeeTransaction.EmployeeId,
                employeeName,
                amount = totalAmount,
                amountText = _decimalFormattingService.DecimalFormat(totalAmount) + " TRY",
                transactionType = (int)employeeTransaction.TransactionType,
                transactionTypeText = employeeTransaction.TransactionType.ToString(),
                reason = combinedReason,
                transactionDate = employeeTransaction.Date.ToString("yyyy-MM-dd HH:mm:ss"),
                transactionDateOnly = employeeTransaction.Date.ToString("yyyy-MM-dd"),
                shiftStartTimeText = shift == null ? "-" : FormatTime(shift.ShiftStartTime),
                shiftEndTimeText = shift == null ? "-" : FormatTime(shift.ShiftEndTime),
                deductionAmount = employeeTransaction.TransactionType == TransactionTypeEnum.خصم ? totalAmount.ToString("0.00") : "0.00",
                advanceAmount = employeeTransaction.TransactionType == TransactionTypeEnum.سلفة ? totalAmount.ToString("0.00") : "0.00",
                bonusAmount = employeeTransaction.TransactionType == TransactionTypeEnum.مكافأة ? totalAmount.ToString("0.00") : "0.00"
            }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
    public async Task<IActionResult> UpdateTransactionFromPopup([FromBody] UpdateEmployeeTransactionPopupRequest request)
    {
        if (request == null || request.Id <= 0)
        {
            return Json(new { success = false, message = "لم يتم العثور على رقم الحركة" });
        }

        var employeeTransaction = await _context.EmployeeTransactions
            .FirstOrDefaultAsync(t => t.Id == request.Id && !t.IsDeleted);

        if (employeeTransaction == null)
        {
            return Json(new { success = false, message = "الحركة غير موجودة أو موجودة في سلة المهملات" });
        }

        var oldTransactionType = employeeTransaction.TransactionType;
        var oldAmount = employeeTransaction.Amount;
        var oldReason = employeeTransaction.Reason ?? string.Empty;
        var oldDate = employeeTransaction.Date;
        var oldDayStart = oldDate.Date;
        var oldDayEnd = oldDayStart.AddDays(1);

        var oldSameDayTransactions = await _context.EmployeeTransactions
            .Where(t =>
                !t.IsDeleted &&
                t.EmployeeId == employeeTransaction.EmployeeId &&
                t.TransactionType == oldTransactionType &&
                t.Date >= oldDayStart &&
                t.Date < oldDayEnd)
            .OrderBy(t => t.Id)
            .ToListAsync();

        var oldGroupedAmount = oldSameDayTransactions.Sum(t => t.Amount);
        var oldGroupedReason = BuildDailyTransactionReasonText(oldSameDayTransactions.Select(t => t.Reason));

        var deductionAmount = request.DeductionAmount < 0 ? 0 : request.DeductionAmount;
        var advanceAmount = request.AdvanceAmount < 0 ? 0 : request.AdvanceAmount;
        var bonusAmount = request.BonusAmount < 0 ? 0 : request.BonusAmount;

        var positiveAmountsCount = new[] { deductionAmount, advanceAmount, bonusAmount }
            .Count(value => value > 0);

        if (positiveAmountsCount == 0)
        {
            return Json(new { success = false, message = "يجب إدخال مبلغ في الخصم أو السلفة أو المكافأة" });
        }

        if (positiveAmountsCount > 1)
        {
            return Json(new { success = false, message = "اختار نوع واحد فقط: خصم أو سلفة أو مكافأة" });
        }

        if (!DateTime.TryParse(request.TransactionDate, out var parsedTransactionDate))
        {
            return Json(new { success = false, message = "يجب اختيار تاريخ صحيح" });
        }

        var newTransactionType = TransactionTypeEnum.خصم;
        var newAmount = deductionAmount;

        if (advanceAmount > 0)
        {
            newTransactionType = TransactionTypeEnum.سلفة;
            newAmount = advanceAmount;
        }
        else if (bonusAmount > 0)
        {
            newTransactionType = TransactionTypeEnum.مكافأة;
            newAmount = bonusAmount;
        }

        var newDate = parsedTransactionDate.Date.Add(employeeTransaction.Date.TimeOfDay);
        var newDayStart = newDate.Date;
        var newDayEnd = newDayStart.AddDays(1);

        /*
            لو الصف المعروض كان مجمع أكثر من خصم لنفس اليوم:
            نخلي الحركة الحالية هي الحركة الأساسية بالمبلغ الجديد،
            ونحذف باقي حركات نفس الموظف/نفس اليوم/نفس النوع حتى لا يظهروا كصفين.
        */
        var oldGroupDuplicates = oldSameDayTransactions
            .Where(t => t.Id != employeeTransaction.Id)
            .ToList();

        foreach (var duplicate in oldGroupDuplicates)
        {
            duplicate.IsDeleted = true;
            duplicate.DeletedAt = _timeService.GetIstanbulTimeWithOffset();
            duplicate.DeletedByUserName = "دمج تلقائي بعد تعديل الحركة";
        }

        var newSameDayDuplicates = await _context.EmployeeTransactions
            .Where(t =>
                !t.IsDeleted &&
                t.Id != employeeTransaction.Id &&
                t.EmployeeId == employeeTransaction.EmployeeId &&
                t.TransactionType == newTransactionType &&
                t.Date >= newDayStart &&
                t.Date < newDayEnd)
            .OrderBy(t => t.Id)
            .ToListAsync();

        foreach (var duplicate in newSameDayDuplicates)
        {
            duplicate.IsDeleted = true;
            duplicate.DeletedAt = _timeService.GetIstanbulTimeWithOffset();
            duplicate.DeletedByUserName = "دمج تلقائي بعد تعديل الحركة";
        }

        employeeTransaction.TransactionType = newTransactionType;
        employeeTransaction.Amount = newAmount;
        employeeTransaction.Reason = string.IsNullOrWhiteSpace(request.Reason)
            ? null
            : request.Reason.Trim();
        employeeTransaction.Date = newDate;

        var historyChanges = new List<EmployeeTransactionEditHistoryChange>();
        AddEditHistoryChange(historyChanges, "نوع العملية", oldTransactionType.ToString(), employeeTransaction.TransactionType.ToString());
        AddEditHistoryChange(historyChanges, "المبلغ", oldGroupedAmount.ToString("0.##"), employeeTransaction.Amount.ToString("0.##"));
        AddEditHistoryChange(historyChanges, "السبب", oldGroupedReason, employeeTransaction.Reason ?? string.Empty);
        AddEditHistoryChange(historyChanges, "تاريخ الحركة", oldDate.ToString("yyyy-MM-dd"), employeeTransaction.Date.ToString("yyyy-MM-dd"));

        if (historyChanges.Any())
        {
            var employeeName = await GetEmployeeNameByIdAsync(employeeTransaction.EmployeeId);
            AppendEditHistory(employeeTransaction, new EmployeeTransactionEditHistoryEntry
            {
                TransactionId = employeeTransaction.Id,
                EmployeeId = employeeTransaction.EmployeeId,
                EmployeeName = employeeName,
                ChangedAt = _timeService.GetIstanbulTimeWithOffset(),
                ChangedBy = await GetCurrentEmployeeTransactionUserNameAsync(),
                Changes = historyChanges
            });
        }

        _context.Update(employeeTransaction);

        // نحفظ الأول عشان دالة مزامنة سجل الدوام تقرأ القيم الجديدة فعلًا من الداتا بيز.
        await _context.SaveChangesAsync();

        var shouldSyncOldDeductionDay = oldTransactionType == TransactionTypeEnum.خصم || employeeTransaction.TransactionType == TransactionTypeEnum.خصم;
        await SyncAttendanceLogForTransactionDayAsync(employeeTransaction.EmployeeId, oldDate, shouldSyncOldDeductionDay);

        if (employeeTransaction.Date.Date != oldDate.Date || employeeTransaction.TransactionType != oldTransactionType)
        {
            await SyncAttendanceLogForTransactionDayAsync(employeeTransaction.EmployeeId, employeeTransaction.Date, employeeTransaction.TransactionType == TransactionTypeEnum.خصم);
        }

        // نحفظ تعديلات سجل الدوام بعد المزامنة.
        await _context.SaveChangesAsync();

        return Json(new
        {
            success = true,
            message = "تم التعديل",
            status = employeeTransaction.TransactionType.ToString(),
            refreshTable = true,
            attendanceSynced = true
        });
    }

    // GET: EmployeeTransactions/Edit/5
    [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var employeeTransaction = await _context.EmployeeTransactions.FindAsync(id);

        if (employeeTransaction == null)
        {
            return NotFound();
        }

        var model = new EmployeeTransactionViewModel
        {
            Id = employeeTransaction.Id,
            Amount = employeeTransaction.Amount,
            TransactionType = employeeTransaction.TransactionType,
            Reason = employeeTransaction.Reason,
            EmployeeId = employeeTransaction.EmployeeId,
            TransactionDate = employeeTransaction.Date.ToString("yyyy-MM-dd HH:mm:ss"),
            TransactionDateOnly = employeeTransaction.Date.ToString("yyyy-MM-dd")
        };

        return View(model);
    }

    // POST: EmployeeTransactions/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
    public async Task<IActionResult> Edit(int id, EmployeeTransactionViewModel model)
    {
        if (id != model.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var employeeTransaction = await _context.EmployeeTransactions.FindAsync(id);

            if (employeeTransaction == null)
            {
                return NotFound();
            }

            employeeTransaction.Amount = model.Amount;
            employeeTransaction.TransactionType = model.TransactionType;
            employeeTransaction.Reason = model.Reason;
            employeeTransaction.EmployeeId = model.EmployeeId;

            _context.Update(employeeTransaction);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
    public async Task<IActionResult> GetTransactionsEditHistory()
    {
        var rows = await _context.EmployeeTransactions
            .AsNoTracking()
            .Include(t => t.Employee)
            .Where(t => t.EditHistoryJson != null && t.EditHistoryJson != "")
            .OrderByDescending(t => t.Date)
            .Take(1000)
            .ToListAsync();

        var items = new List<object>();

        foreach (var row in rows)
        {
            var historyEntries = ParseEditHistory(row.EditHistoryJson);
            foreach (var entry in historyEntries)
            {
                items.Add(new
                {
                    transactionId = row.Id,
                    employeeName = string.IsNullOrWhiteSpace(entry.EmployeeName)
                        ? BuildEmployeeName(row.Employee)
                        : entry.EmployeeName,
                    changedAt = entry.ChangedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    changedBy = string.IsNullOrWhiteSpace(entry.ChangedBy) ? "-" : entry.ChangedBy,
                    changes = entry.Changes.Select(change => new
                    {
                        fieldName = change.FieldName,
                        oldValue = string.IsNullOrWhiteSpace(change.OldValue) ? "-" : change.OldValue,
                        newValue = string.IsNullOrWhiteSpace(change.NewValue) ? "-" : change.NewValue
                    }).ToList()
                });
            }
        }

        var orderedItems = items
            .OrderByDescending(item => DateTime.TryParse(item.GetType().GetProperty("changedAt")?.GetValue(item)?.ToString(), out var date) ? date : DateTime.MinValue)
            .ToList();

        return Json(new { success = true, items = orderedItems });
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
    public async Task<IActionResult> GetDeletedTransactions()
    {
        var rows = await _context.EmployeeTransactions
            .AsNoTracking()
            .Include(t => t.Employee)
            .Where(t => t.IsDeleted)
            .OrderByDescending(t => t.DeletedAt ?? t.Date)
            .Take(1000)
            .ToListAsync();

        var items = rows.Select(t => new
        {
            id = t.Id,
            employeeName = BuildEmployeeName(t.Employee),
            amount = t.Amount.ToString("0.##"),
            transactionType = t.TransactionType.ToString(),
            reason = string.IsNullOrWhiteSpace(t.Reason) ? "-" : t.Reason,
            transactionDate = t.Date.ToString("yyyy-MM-dd HH:mm:ss"),
            deletedAt = t.DeletedAt.HasValue ? t.DeletedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "-",
            deletedBy = string.IsNullOrWhiteSpace(t.DeletedByUserName) ? "-" : t.DeletedByUserName
        }).ToList();

        return Json(new { success = true, items });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
    public async Task<IActionResult> RestoreDeleted(int id)
    {
        var employeeTransaction = await _context.EmployeeTransactions
            .FirstOrDefaultAsync(t => t.Id == id && t.IsDeleted);

        if (employeeTransaction == null)
        {
            return Json(new { success = false, message = "الحركة غير موجودة في سلة المهملات" });
        }

        employeeTransaction.IsDeleted = false;
        employeeTransaction.DeletedAt = null;
        employeeTransaction.DeletedByUserName = null;

        // نحفظ الاسترداد الأول حتى تدخل الحركة في حسابات المزامنة.
        await _context.SaveChangesAsync();

        await SyncAttendanceLogForTransactionDayAsync(
            employeeTransaction.EmployeeId,
            employeeTransaction.Date,
            employeeTransaction.TransactionType == TransactionTypeEnum.خصم);

        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "تم الاسترداد", refreshTable = true, attendanceSynced = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
    public async Task<IActionResult> RestoreAllDeleted()
    {
        var deletedTransactions = await _context.EmployeeTransactions
            .Where(t => t.IsDeleted)
            .ToListAsync();

        var affectedDays = deletedTransactions
            .Select(t => new
            {
                t.EmployeeId,
                Day = t.Date.Date,
                SyncDeduction = t.TransactionType == TransactionTypeEnum.خصم
            })
            .GroupBy(x => new { x.EmployeeId, x.Day })
            .Select(g => new
            {
                g.Key.EmployeeId,
                Date = g.Key.Day,
                SyncDeduction = g.Any(x => x.SyncDeduction)
            })
            .ToList();

        foreach (var transaction in deletedTransactions)
        {
            transaction.IsDeleted = false;
            transaction.DeletedAt = null;
            transaction.DeletedByUserName = null;
        }

        // نحفظ الاسترداد الأول حتى تدخل الحركات في حسابات المزامنة.
        await _context.SaveChangesAsync();

        foreach (var day in affectedDays)
        {
            await SyncAttendanceLogForTransactionDayAsync(day.EmployeeId, day.Date, day.SyncDeduction);
        }

        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "تم استرداد كل الحركات", refreshTable = true, attendanceSynced = true });
    }



    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
    public async Task<IActionResult> DeleteSelected(string ids)
    {
        if (string.IsNullOrWhiteSpace(ids))
        {
            return Json(new { success = false, message = "اختار حركة واحدة على الأقل" });
        }

        var selectedIds = ids
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => int.TryParse(value.Trim(), out var parsedId) ? parsedId : 0)
            .Where(value => value > 0)
            .Distinct()
            .ToList();

        if (!selectedIds.Any())
        {
            return Json(new { success = false, message = "اختار حركة واحدة على الأقل" });
        }

        var selectedTransactions = await _context.EmployeeTransactions
            .Where(t => selectedIds.Contains(t.Id) && !t.IsDeleted)
            .ToListAsync();

        if (!selectedTransactions.Any())
        {
            return Json(new { success = false, message = "الحركات المحددة غير موجودة أو تم حذفها من قبل" });
        }

        /*
            كل صف في الجدول قد يكون صف مجمع لخصومات نفس الموظف في نفس اليوم.
            لذلك عند حذف المحدد نحذف نفس اليوم ونفس النوع لكل صف محدد، وليس Id واحد فقط.
        */
        var groupsToDelete = selectedTransactions
            .Select(t => new
            {
                t.EmployeeId,
                Day = t.Date.Date,
                t.TransactionType
            })
            .Distinct()
            .ToList();

        var transactionsToDelete = new List<EmployeeTransaction>();

        foreach (var group in groupsToDelete)
        {
            var dayEnd = group.Day.AddDays(1);

            var sameDayTransactions = await _context.EmployeeTransactions
                .Where(t =>
                    !t.IsDeleted &&
                    t.EmployeeId == group.EmployeeId &&
                    t.TransactionType == group.TransactionType &&
                    t.Date >= group.Day &&
                    t.Date < dayEnd)
                .ToListAsync();

            transactionsToDelete.AddRange(sameDayTransactions);
        }

        transactionsToDelete = transactionsToDelete
            .GroupBy(t => t.Id)
            .Select(g => g.First())
            .ToList();

        if (!transactionsToDelete.Any())
        {
            return Json(new { success = false, message = "لا توجد حركات للحذف" });
        }

        var affectedDays = transactionsToDelete
            .Select(t => new
            {
                t.EmployeeId,
                Day = t.Date.Date,
                SyncDeduction = t.TransactionType == TransactionTypeEnum.خصم
            })
            .GroupBy(x => new { x.EmployeeId, x.Day })
            .Select(g => new
            {
                g.Key.EmployeeId,
                Date = g.Key.Day,
                SyncDeduction = g.Any(x => x.SyncDeduction)
            })
            .ToList();

        var deletedAt = _timeService.GetIstanbulTimeWithOffset();
        var deletedBy = await GetCurrentEmployeeTransactionUserNameAsync();

        foreach (var transaction in transactionsToDelete)
        {
            transaction.IsDeleted = true;
            transaction.DeletedAt = deletedAt;
            transaction.DeletedByUserName = deletedBy;
        }

        await _context.SaveChangesAsync();

        foreach (var day in affectedDays)
        {
            await SyncAttendanceLogForTransactionDayAsync(day.EmployeeId, day.Date, day.SyncDeduction);
        }

        await _context.SaveChangesAsync();

        return Json(new
        {
            success = true,
            message = $"تم حذف {transactionsToDelete.Count} حركة محددة",
            deletedCount = transactionsToDelete.Count,
            refreshTable = true,
            attendanceSynced = true
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
    public async Task<IActionResult> DeleteAllActive(
        int? employeeId = null,
        bool formerEmployees = false,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var query = _context.EmployeeTransactions
            .Include(t => t.Employee)
            .Where(t => !t.IsDeleted)
            .AsQueryable();

        if (employeeId.HasValue && employeeId.Value > 0)
        {
            query = query.Where(t => t.EmployeeId == employeeId.Value);
        }
        else if (formerEmployees)
        {
            query = query.Where(t => t.Employee != null && !t.Employee.IsActive);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(t => t.Date >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            var toDateExclusive = toDate.Value.Date.AddDays(1);
            query = query.Where(t => t.Date < toDateExclusive);
        }

        var transactions = await query.ToListAsync();

        if (!transactions.Any())
        {
            return Json(new { success = true, message = "لا توجد حركات للحذف", deletedCount = 0, refreshTable = true });
        }

        var affectedDays = transactions
            .Select(t => new
            {
                t.EmployeeId,
                Day = t.Date.Date,
                SyncDeduction = t.TransactionType == TransactionTypeEnum.خصم
            })
            .GroupBy(x => new { x.EmployeeId, x.Day })
            .Select(g => new
            {
                g.Key.EmployeeId,
                Date = g.Key.Day,
                SyncDeduction = g.Any(x => x.SyncDeduction)
            })
            .ToList();

        var deletedAt = _timeService.GetIstanbulTimeWithOffset();
        var deletedBy = await GetCurrentEmployeeTransactionUserNameAsync();

        foreach (var transaction in transactions)
        {
            transaction.IsDeleted = true;
            transaction.DeletedAt = deletedAt;
            transaction.DeletedByUserName = deletedBy;
        }

        await _context.SaveChangesAsync();

        foreach (var day in affectedDays)
        {
            await SyncAttendanceLogForTransactionDayAsync(day.EmployeeId, day.Date, day.SyncDeduction);
        }

        await _context.SaveChangesAsync();

        return Json(new
        {
            success = true,
            message = $"تم حذف {transactions.Count} حركة",
            deletedCount = transactions.Count,
            refreshTable = true,
            attendanceSynced = true
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
    public async Task<IActionResult> DeleteAllDeletedPermanently()
    {
        var deletedTransactions = await _context.EmployeeTransactions
            .Where(t => t.IsDeleted)
            .ToListAsync();

        if (!deletedTransactions.Any())
        {
            return Json(new { success = true, message = "سلة المهملات فارغة", deletedCount = 0 });
        }

        var count = deletedTransactions.Count;
        _context.EmployeeTransactions.RemoveRange(deletedTransactions);
        await _context.SaveChangesAsync();

        return Json(new
        {
            success = true,
            message = $"تم حذف {count} حركة من سلة المهملات نهائيًا",
            deletedCount = count,
            refreshTable = true
        });
    }

    // POST: EmployeeTransactions/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var employeeTransaction = await _context.EmployeeTransactions
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

        if (employeeTransaction == null)
        {
            if (IsAjaxRequest())
            {
                return Json(new { success = false, message = "الحركة غير موجودة أو تم حذفها من قبل" });
            }

            return NotFound();
        }

        var dayStart = employeeTransaction.Date.Date;
        var dayEnd = dayStart.AddDays(1);
        var deletedBy = await GetCurrentEmployeeTransactionUserNameAsync();
        var deletedAt = _timeService.GetIstanbulTimeWithOffset();

        /*
            لو الصف المعروض عبارة عن إجمالي خصم اليوم، نحذف كل حركات نفس الموظف/نفس اليوم/نفس النوع
            حتى لا تفضل حركة ثانية ظاهرة بعد الحذف.
        */
        var sameDayTransactions = await _context.EmployeeTransactions
            .Where(t =>
                !t.IsDeleted &&
                t.EmployeeId == employeeTransaction.EmployeeId &&
                t.TransactionType == employeeTransaction.TransactionType &&
                t.Date >= dayStart &&
                t.Date < dayEnd)
            .ToListAsync();

        foreach (var transaction in sameDayTransactions)
        {
            transaction.IsDeleted = true;
            transaction.DeletedAt = deletedAt;
            transaction.DeletedByUserName = deletedBy;
        }

        // نحفظ الحذف الأول حتى تخرج الحركات المحذوفة من حسابات المزامنة.
        await _context.SaveChangesAsync();

        await SyncAttendanceLogForTransactionDayAsync(
            employeeTransaction.EmployeeId,
            employeeTransaction.Date,
            employeeTransaction.TransactionType == TransactionTypeEnum.خصم);

        await _context.SaveChangesAsync();

        if (IsAjaxRequest())
        {
            return Json(new
            {
                success = true,
                message = "تم الحذف",
                refreshTable = true,
                attendanceSynced = true
            });
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task SyncAttendanceLogForTransactionDayAsync(int employeeId, DateTime transactionDate, bool syncDeductionFields)
    {
        if (employeeId <= 0)
        {
            return;
        }

        var dayStart = transactionDate.Date;
        var dayEnd = dayStart.AddDays(1);

        var attendanceLogs = await _context.EmployeeAttendanceLogs
            .Where(log =>
                log.EmployeeId == employeeId &&
                log.CheckInAt >= dayStart &&
                log.CheckInAt < dayEnd &&
                (log.Notes == null || !log.Notes.Contains("[AttendanceDeleted]")))
            .OrderByDescending(log => log.CheckInAt)
            .ToListAsync();

        if (!attendanceLogs.Any())
        {
            return;
        }

        var now = _timeService.GetIstanbulTimeWithOffset();

        if (syncDeductionFields)
        {
            var activeDeductionTransactions = await _context.EmployeeTransactions
                .AsNoTracking()
                .Where(transaction =>
                    !transaction.IsDeleted &&
                    transaction.EmployeeId == employeeId &&
                    transaction.TransactionType == TransactionTypeEnum.خصم &&
                    transaction.Date >= dayStart &&
                    transaction.Date < dayEnd)
                .OrderBy(transaction => transaction.Id)
                .Select(transaction => new
                {
                    transaction.Amount,
                    transaction.Reason
                })
                .ToListAsync();

            var totalDeduction = activeDeductionTransactions.Sum(transaction => transaction.Amount);
            var reasonText = string.Join(" / ", activeDeductionTransactions
                .Select(transaction => string.IsNullOrWhiteSpace(transaction.Reason) ? "خصم" : transaction.Reason.Trim())
                .Where(reason => !string.IsNullOrWhiteSpace(reason))
                .Distinct());

            if (string.IsNullOrWhiteSpace(reasonText))
            {
                reasonText = totalDeduction > 0 ? "خصم" : "لا يوجد خصم";
            }

            foreach (var log in attendanceLogs)
            {
                log.DeductionAmount = totalDeduction;
                log.DeductionReason = totalDeduction > 0 ? reasonText : "لا يوجد خصم";
                log.UpdatedAt = now;
            }
        }
        else
        {
            foreach (var log in attendanceLogs)
            {
                log.UpdatedAt = now;
            }
        }
    }



    private async Task SyncAttendanceDeductionsIntoEmployeeTransactionsAsync(
        int? employeeId,
        bool formerEmployees,
        DateTime? fromDate,
        DateTime? toDate)
    {
        /*
            سبب اختلاف صفحة مكافآت وخصومات عن سجل الدوام كان إن الأولى تقرأ من EmployeeTransactions فقط،
            بينما سجل الدوام يعرض خصومات محفوظة/محسوبة من EmployeeAttendanceLogs.
            هنا نعمل مزامنة قبل عرض الصفحة حتى نفس فلتر الموظف والتاريخ يعطي نفس خصومات سجل الدوام.
        */
        var logsQuery = _context.EmployeeAttendanceLogs
            .AsNoTracking()
            .Where(log =>
                log.EmployeeId.HasValue &&
                (log.Notes == null || !log.Notes.Contains("[AttendanceDeleted]")))
            .AsQueryable();

        if (employeeId.HasValue && employeeId.Value > 0)
        {
            logsQuery = logsQuery.Where(log => log.EmployeeId == employeeId.Value);
        }
        else if (formerEmployees)
        {
            logsQuery = logsQuery.Where(log =>
                _context.Employees.Any(employee =>
                    employee.Id == log.EmployeeId.Value &&
                    !employee.IsActive));
        }

        if (fromDate.HasValue)
        {
            logsQuery = logsQuery.Where(log => log.CheckInAt >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            var toDateExclusive = toDate.Value.Date.AddDays(1);
            logsQuery = logsQuery.Where(log => log.CheckInAt < toDateExclusive);
        }

        var attendanceRows = await logsQuery
            .Select(log => new AttendanceDeductionSyncRow
            {
                EmployeeId = log.EmployeeId!.Value,
                CheckInAt = log.CheckInAt,
                DeductionAmount = log.DeductionAmount,
                DeductionReason = log.DeductionReason
            })
            .ToListAsync();

        if (!attendanceRows.Any())
        {
            return;
        }

        var employeeIds = attendanceRows
            .Select(row => row.EmployeeId)
            .Distinct()
            .ToList();

        var minDate = attendanceRows.Min(row => row.CheckInAt.Date);
        var maxDateExclusive = attendanceRows.Max(row => row.CheckInAt.Date).AddDays(1);
        var now = _timeService.GetIstanbulTimeWithOffset();

        var existingDeductionTransactions = await _context.EmployeeTransactions
            .Where(transaction =>
                employeeIds.Contains(transaction.EmployeeId) &&
                !transaction.IsDeleted &&
                transaction.TransactionType == TransactionTypeEnum.خصم &&
                transaction.Date >= minDate &&
                transaction.Date < maxDateExclusive)
            .OrderBy(transaction => transaction.Id)
            .ToListAsync();

        var positiveAttendanceGroups = attendanceRows
            .Where(row =>
                Math.Round(row.DeductionAmount ?? 0m, 2) > 0m &&
                !IsNoDeductionTransactionReason(row.DeductionReason))
            .GroupBy(row => new
            {
                row.EmployeeId,
                Day = row.CheckInAt.Date
            })
            .ToList();

        var positiveKeys = new HashSet<string>();

        foreach (var group in positiveAttendanceGroups)
        {
            var dayStart = group.Key.Day;
            var dayEnd = dayStart.AddDays(1);
            var key = $"{group.Key.EmployeeId}|{dayStart:yyyyMMdd}";
            positiveKeys.Add(key);

            var dailyAmount = Math.Round(group.Sum(row => row.DeductionAmount ?? 0m), 2);
            var dailyReason = BuildDailyTransactionReasonText(group.Select(row => row.DeductionReason));
            if (string.IsNullOrWhiteSpace(dailyReason))
            {
                dailyReason = "خصم من سجل الدوام";
            }

            var sameDayTransactions = existingDeductionTransactions
                .Where(transaction =>
                    transaction.EmployeeId == group.Key.EmployeeId &&
                    transaction.Date >= dayStart &&
                    transaction.Date < dayEnd)
                .OrderBy(transaction => transaction.Id)
                .ToList();

            var primaryTransaction = sameDayTransactions.FirstOrDefault();

            if (primaryTransaction == null)
            {
                _context.EmployeeTransactions.Add(new EmployeeTransaction
                {
                    EmployeeId = group.Key.EmployeeId,
                    TransactionType = TransactionTypeEnum.خصم,
                    Amount = dailyAmount,
                    Reason = dailyReason,
                    Date = group.Max(row => row.CheckInAt)
                });
            }
            else
            {
                primaryTransaction.Amount = dailyAmount;
                primaryTransaction.Reason = dailyReason;
                primaryTransaction.Date = group.Max(row => row.CheckInAt);

                foreach (var duplicate in sameDayTransactions.Skip(1))
                {
                    duplicate.IsDeleted = true;
                    duplicate.DeletedAt = now;
                    duplicate.DeletedByUserName = "دمج تلقائي من مزامنة سجل الدوام";
                }
            }
        }

        // لا نعيد حذف الحركات المستردة تلقائيًا من هنا حتى يعمل الاسترداد بشكل صحيح.
        if (_context.ChangeTracker.HasChanges())
        {
            await _context.SaveChangesAsync();
        }
    }

    private static bool IsNoDeductionTransactionReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return false;
        }

        return reason.Contains("لا يوجد خصم", StringComparison.OrdinalIgnoreCase) ||
               reason.Contains("بدون خصم", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeAttendanceDeductionReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return false;
        }

        return reason.Contains("تأخر", StringComparison.OrdinalIgnoreCase) ||
               reason.Contains("تاخر", StringComparison.OrdinalIgnoreCase) ||
               reason.Contains("خروج مبكر", StringComparison.OrdinalIgnoreCase) ||
               reason.Contains("غياب", StringComparison.OrdinalIgnoreCase) ||
               reason.Contains("غائب", StringComparison.OrdinalIgnoreCase) ||
               reason.Contains("سجل الدوام", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildDailyTransactionReasonText(IEnumerable<string?> reasons)
    {
        var result = new List<string>();
        var seenCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawReason in reasons ?? Enumerable.Empty<string?>())
        {
            if (string.IsNullOrWhiteSpace(rawReason))
            {
                continue;
            }

            var parts = rawReason
                .Split(new[] { " + ", " / ", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => !string.IsNullOrWhiteSpace(part));

            foreach (var part in parts)
            {
                var category = GetDeductionReasonCategory(part);

                if (!string.IsNullOrWhiteSpace(category))
                {
                    if (seenCategories.Contains(category))
                    {
                        continue;
                    }

                    seenCategories.Add(category);
                    result.Add(part);
                    continue;
                }

                if (seenTexts.Add(part))
                {
                    result.Add(part);
                }
            }
        }

        return result.Any() ? string.Join(" + ", result) : "";
    }

    private static string GetDeductionReasonCategory(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return "";
        }

        if (reason.Contains("خروج مبكر", StringComparison.OrdinalIgnoreCase))
        {
            return "خروج مبكر";
        }

        if (reason.Contains("تأخر", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("تاخر", StringComparison.OrdinalIgnoreCase))
        {
            return "تأخر";
        }

        if (reason.Contains("غياب", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("غائب", StringComparison.OrdinalIgnoreCase))
        {
            return "غياب";
        }

        return "";
    }

    private bool IsAjaxRequest()
    {
        return string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(Request.Headers["Accept"], "application/json", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> GetCurrentEmployeeTransactionUserNameAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var employeeName = await _context.Employees
                .AsNoTracking()
                .Where(e => e.ApplicationUserId == userId)
                .Select(e => e.DisplayName == null || e.DisplayName == ""
                    ? (e.Name == null ? "" : e.Name)
                    : e.DisplayName)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(employeeName))
            {
                return employeeName.Trim();
            }
        }

        return User.Identity?.Name ?? "موظف";
    }

    private async Task<string> GetEmployeeNameByIdAsync(int employeeId)
    {
        var employeeName = await _context.Employees
            .AsNoTracking()
            .Where(e => e.Id == employeeId)
            .Select(e => e.DisplayName == null || e.DisplayName == ""
                ? (e.Name == null ? "" : e.Name)
                : e.DisplayName)
            .FirstOrDefaultAsync();

        return string.IsNullOrWhiteSpace(employeeName) ? "بدون اسم" : employeeName.Trim();
    }

    private static string BuildEmployeeName(Employee? employee)
    {
        if (employee == null)
        {
            return "بدون اسم";
        }

        if (!string.IsNullOrWhiteSpace(employee.DisplayName))
        {
            return employee.DisplayName;
        }

        return !string.IsNullOrWhiteSpace(employee.Name) ? employee.Name : "بدون اسم";
    }

    private static void AddEditHistoryChange(
        List<EmployeeTransactionEditHistoryChange> changes,
        string fieldName,
        string? oldValue,
        string? newValue)
    {
        var oldText = (oldValue ?? string.Empty).Trim();
        var newText = (newValue ?? string.Empty).Trim();

        if (string.Equals(oldText, newText, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        changes.Add(new EmployeeTransactionEditHistoryChange
        {
            FieldName = fieldName,
            OldValue = string.IsNullOrWhiteSpace(oldText) ? "-" : oldText,
            NewValue = string.IsNullOrWhiteSpace(newText) ? "-" : newText
        });
    }

    private static void AppendEditHistory(EmployeeTransaction transaction, EmployeeTransactionEditHistoryEntry entry)
    {
        var history = ParseEditHistory(transaction.EditHistoryJson);
        history.Add(entry);
        transaction.EditHistoryJson = JsonSerializer.Serialize(history);
    }

    private static List<EmployeeTransactionEditHistoryEntry> ParseEditHistory(string? editHistoryJson)
    {
        if (string.IsNullOrWhiteSpace(editHistoryJson))
        {
            return new List<EmployeeTransactionEditHistoryEntry>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<EmployeeTransactionEditHistoryEntry>>(editHistoryJson)
                   ?? new List<EmployeeTransactionEditHistoryEntry>();
        }
        catch
        {
            return new List<EmployeeTransactionEditHistoryEntry>();
        }
    }

    private async Task<List<SelectListItem>> GetEmployeesSelectListAsync(bool isActive)
    {
        var employees = await _context.Employees
            .AsNoTracking()
            .Where(e => e.IsActive == isActive)
            .OrderBy(e => e.Name)
            .Select(e => new
            {
                e.Id,
                e.Name,
                e.DisplayName,
                ImageUrl = e.ImageUrl == null ? string.Empty : e.ImageUrl
            })
            .ToListAsync();

        return employees.Select(e => new SelectListItem
        {
            Value = e.Id.ToString(),
            Text = !string.IsNullOrWhiteSpace(e.DisplayName)
                ? e.DisplayName
                : (!string.IsNullOrWhiteSpace(e.Name) ? e.Name : "بدون اسم"),
            Group = new SelectListGroup
            {
                Name = NormalizeEmployeeImagePath(e.ImageUrl)
            }
        }).ToList();
    }

    private static string NormalizeEmployeeImagePath(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return "/static/circle-user-solid.svg";
        }

        imagePath = imagePath.Trim();

        if (!imagePath.StartsWith("/"))
        {
            imagePath = "/" + imagePath;
        }

        return imagePath;
    }

    private static string FormatTime(TimeSpan time)
    {
        return DateTime.Today.Add(time).ToString("hh:mm") + " " + (time.Hours < 12 ? "ص" : "م");
    }



    private sealed class AttendanceDeductionSyncRow
    {
        public int EmployeeId { get; set; }

        public DateTime CheckInAt { get; set; }

        public decimal? DeductionAmount { get; set; }

        public string? DeductionReason { get; set; }
    }

    private sealed class GroupedEmployeeTransactionForList
    {
        public int PrimaryTransactionId { get; set; }

        public int EmployeeId { get; set; }

        public Employee? Employee { get; set; }

        public TransactionTypeEnum TransactionType { get; set; }

        public DateTime TransactionDay { get; set; }

        public DateTime DisplayDate { get; set; }

        public decimal Amount { get; set; }

        public string Reason { get; set; } = string.Empty;

        public List<int> GroupTransactionIds { get; set; } = new List<int>();
    }

    private sealed class EmployeeTransactionShiftLookupRow
    {
        public int EmployeeId { get; set; }
        public TimeSpan ShiftStartTime { get; set; }
        public TimeSpan ShiftEndTime { get; set; }
    }

    public sealed class EmployeeTransactionEditHistoryEntry
    {
        public int TransactionId { get; set; }

        public int EmployeeId { get; set; }

        public string EmployeeName { get; set; } = string.Empty;

        public DateTime ChangedAt { get; set; }

        public string ChangedBy { get; set; } = string.Empty;

        public List<EmployeeTransactionEditHistoryChange> Changes { get; set; } = new List<EmployeeTransactionEditHistoryChange>();
    }

    public sealed class EmployeeTransactionEditHistoryChange
    {
        public string FieldName { get; set; } = string.Empty;

        public string OldValue { get; set; } = string.Empty;

        public string NewValue { get; set; } = string.Empty;
    }

    public sealed class UpdateEmployeeTransactionPopupRequest
    {
        public int Id { get; set; }

        public decimal DeductionAmount { get; set; }

        public decimal AdvanceAmount { get; set; }

        public decimal BonusAmount { get; set; }

        public string? Reason { get; set; }

        public string? TransactionDate { get; set; }
    }

}
