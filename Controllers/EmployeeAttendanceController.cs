using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Text;

using Crm_LotusBlue.Models;
using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.Models.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Crm_LotusBlue.Controllers
{
    [Authorize]
    public class EmployeeAttendanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        private const decimal LatePenaltyMultiplier = 2m;
        private const int ShiftAccessGraceMinutes = 30;
        private const string AttendanceIpHistoryNoteMarker = "[IP_CHANGE]";
        private const string QuestionCheckInNoteText = "QuestionCheckIn - تم تسجيل الحضور بسؤال: هل جاهز لبدء الدوام؟ الإجابة: نعم";

        private const string AttendanceDeletedNoteMarker = "[AttendanceDeleted]";

        private const string AttendanceEditHistoryStartMarker = "[ATTENDANCE_EDIT_HISTORY]";

        private const string AttendanceEditHistoryEndMarker = "[/ATTENDANCE_EDIT_HISTORY]";

        private bool ShouldSkipCheckInVerificationForCurrentUser()
        {
            return User.IsInRole("Admin")
                || User.IsInRole("DeliveryCompany")
                || User.IsInRole("DeliveryRepresentative")
                || User.IsInRole("OrderPreparer");
        }

        public EmployeeAttendanceController(
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
        public async Task<IActionResult> AssignWorkTimes()
        {
            ViewBag.Employees = await GetEmployeesSelectListAsync();

            ViewBag.RecentShifts = await _context.EmployeeWorkShifts
                .AsNoTracking()
                .OrderByDescending(s => s.Id)
                .Select(s => new EmployeeWorkShiftRow
                {
                    Id = s.Id,
                    EmployeeId = s.EmployeeId,
                    EmployeeName = _context.Employees
                        .Where(e => e.Id == s.EmployeeId)
                        .Select(e => e.DisplayName == null || e.DisplayName == ""
                            ? (e.Name == null ? "بدون اسم" : e.Name)
                            : e.DisplayName)
                        .FirstOrDefault() ?? "بدون اسم",

                    ShiftStartTime = s.ShiftStartTime,
                    ShiftEndTime = s.ShiftEndTime,
                    AllowedIpAddress = s.AllowedIpAddress == null ? "" : s.AllowedIpAddress,
                    Notes = s.Notes == null ? "" : s.Notes,
                    IsActive = s.IsActive
                })
                .Take(20)
                .ToListAsync();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
        public async Task<IActionResult> AssignWorkTimes(
            int employeeId,
            TimeSpan shiftStartTime,
            TimeSpan shiftEndTime,
            string allowedIpAddress = "",
            string notes = "")
        {
            if (employeeId <= 0)
            {
                TempData["ErrorMessage"] = "اختاري الموظف";
                return RedirectToAction(nameof(AssignWorkTimes));
            }

            var employeeExists = await _context.Employees
                .AsNoTracking()
                .AnyAsync(e => e.Id == employeeId && e.IsActive);

            if (!employeeExists)
            {
                TempData["ErrorMessage"] = "الموظف غير موجود أو غير نشط";
                return RedirectToAction(nameof(AssignWorkTimes));
            }

            var now = GetEgyptNow();
            var cleanNotes = string.IsNullOrWhiteSpace(notes) ? "" : notes.Trim();

            /*
                مهم جدًا:
                تغيير شيفت الموظف لا يعدل الصف القديم.
                نقفل الشيفت القديم IsActive = false ونضيف شيفت جديد.
                سجلات الحضور القديمة تفضل محفوظة على Snapshot الشيفت الذي اتسجلت عليه.
            */
            var oldActiveShifts = await _context.EmployeeWorkShifts
                .Where(s => s.EmployeeId == employeeId && s.IsActive)
                .ToListAsync();

            foreach (var oldShift in oldActiveShifts)
            {
                oldShift.IsActive = false;
                oldShift.UpdatedAt = now;
            }

            var shift = new EmployeeWorkShift
            {
                EmployeeId = employeeId,
                ShiftStartTime = shiftStartTime,
                ShiftEndTime = shiftEndTime,
                AllowedIpAddress = string.IsNullOrWhiteSpace(allowedIpAddress)
                    ? ""
                    : allowedIpAddress.Trim(),
                Notes = cleanNotes,
                IsActive = true,
                CreatedAt = now
            };

            _context.EmployeeWorkShifts.Add(shift);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم تعيين الدوام بنجاح";

            return RedirectToAction(nameof(AssignWorkTimes));
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
        public async Task<IActionResult> AttendanceLog(
            int? employeeId,
            DateTime? fromDate,
            DateTime? toDate,
            bool? useAutoPeriod)
        {
            /*
                Default attendance view:
                - The page auto-shows the current work-day cycle only.
                - The cycle renews every day at 10:00 AM Egypt time.
                - Before 10:00 AM, the visible period is yesterday 10:00 AM -> today 10:00 AM.
                - After 10:00 AM, the visible period is today 10:00 AM -> tomorrow 10:00 AM.
                - When the user chooses a custom date range, useAutoPeriod becomes false so old records can be viewed normally.
            */
            var now = GetEgyptNow();
            var autoPeriodStart = GetAttendanceAutoPeriodStart(now);
            var autoPeriodEnd = autoPeriodStart.AddDays(1);
            var hasManualDateRange = fromDate.HasValue || toDate.HasValue;
            var isAutoPeriodFilter = useAutoPeriod == true || !hasManualDateRange;

            var query = _context.EmployeeAttendanceLogs
                .AsNoTracking()
                .AsQueryable();

            if (employeeId.HasValue && employeeId.Value > 0)
            {
                query = query.Where(a => a.EmployeeId == employeeId.Value);
            }

            if (isAutoPeriodFilter)
            {
                /*
                    مهم:
                    عرض سجل الدوام يعمل بدورة 10 صباحًا -> 10 صباحًا، لكن بعض الموظفين شيفتهم يبدأ قبل 10.
                    لو فلترنا من 10 حرفيًا، دخول 09:00 يختفي، وبعد نهاية الشيفت النظام يفتكره غائب.
                    لذلك في العرض التلقائي نحمل سجلات يوم بداية الدورة من 00:00،
                    مع استمرار حساب الغياب على نفس يوم الشيفت وليس على ساعة 10 فقط.
                */
                var autoPeriodQueryStart = autoPeriodStart.Date;
                query = query.Where(a => a.CheckInAt >= autoPeriodQueryStart && a.CheckInAt < autoPeriodEnd);
            }
            else
            {
                if (fromDate.HasValue)
                {
                    query = query.Where(a => a.CheckInAt >= fromDate.Value.Date);
                }

                if (toDate.HasValue)
                {
                    var toDateExclusive = toDate.Value.Date.AddDays(1);
                    query = query.Where(a => a.CheckInAt < toDateExclusive);
                }
            }

            var attendanceLogs = await query
                .OrderByDescending(a => a.CheckInAt)
                .ThenByDescending(a => a.Id)
                .Select(a => new EmployeeAttendanceLogRow
                {
                    Id = a.Id,
                    UserId = a.UserId,
                    EmployeeId = a.EmployeeId,

                    EmployeeName = a.EmployeeName == null ? "" : a.EmployeeName,
                    EmployeeEmail = a.EmployeeEmail == null ? "" : a.EmployeeEmail,

                    CheckInAt = a.CheckInAt,
                    CheckOutAt = a.CheckOutAt,

                    FaceImagePath = a.FaceImagePath == null ? "" : a.FaceImagePath,
                    CheckOutFaceImagePath = a.CheckOutFaceImagePath == null ? "" : a.CheckOutFaceImagePath,

                    CheckInIpAddress = a.CheckInIpAddress == null ? "" : a.CheckInIpAddress,
                    CheckInLocation = a.CheckInLocation == null ? "" : a.CheckInLocation,
                    CheckOutIpAddress = a.CheckOutIpAddress == null ? "" : a.CheckOutIpAddress,
                    CheckOutLocation = a.CheckOutLocation == null ? "" : a.CheckOutLocation,

                    DeductionAmount = a.DeductionAmount,
                    DeductionReason = a.DeductionReason == null ? "" : a.DeductionReason,
                    Notes = a.Notes == null ? "" : a.Notes
                })
                .Take(500)
                .ToListAsync();

            await ApplyAttendanceShiftSnapshotsAsync(attendanceLogs);

            var attendanceEmployeeIds = attendanceLogs
                .Where(log => log.EmployeeId.HasValue)
                .Select(log => log.EmployeeId.Value)
                .Distinct()
                .ToList();

            var activeEmployeeIdsQuery = _context.Employees
                .AsNoTracking()
                .Where(e => e.IsActive);

            if (employeeId.HasValue && employeeId.Value > 0)
            {
                activeEmployeeIdsQuery = activeEmployeeIdsQuery.Where(e => e.Id == employeeId.Value);
            }

            var activeEmployeeIdsForDisplayedPeriod = await activeEmployeeIdsQuery
                .Select(e => e.Id)
                .Distinct()
                .ToListAsync();

            var weeklyOffDaysByEmployee = await GetEmployeeWeeklyOffDaysMapAsync(activeEmployeeIdsForDisplayedPeriod);

            var shiftEmployeeIds = attendanceEmployeeIds
                .Union(activeEmployeeIdsForDisplayedPeriod)
                .Distinct()
                .ToList();

            var shifts = await _context.EmployeeWorkShifts
                .AsNoTracking()
                .Where(s => shiftEmployeeIds.Contains(s.EmployeeId))
                .OrderByDescending(s => s.CreatedAt)
                .ThenByDescending(s => s.Id)
                .Select(s => new EmployeeShiftLookupRow
                {
                    Id = s.Id,
                    EmployeeId = s.EmployeeId,
                    EmployeeName = _context.Employees
                        .Where(e => e.Id == s.EmployeeId)
                        .Select(e => e.DisplayName == null || e.DisplayName == ""
                            ? (e.Name == null ? "" : e.Name)
                            : e.DisplayName)
                        .FirstOrDefault() ?? "",

                    Salary = _context.Employees
                        .Where(e => e.Id == s.EmployeeId)
                        .Select(e => e.Salary)
                        .FirstOrDefault(),

                    ShiftStartTime = s.ShiftStartTime,
                    ShiftEndTime = s.ShiftEndTime,
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync();

            var shiftBlockRowsRaw = await _context.EmployeeWorkShifts
                .AsNoTracking()
                .Where(s => s.IsActive && s.CreatedAt <= now)
                .OrderByDescending(s => s.CreatedAt)
                .ThenByDescending(s => s.Id)
                .Select(s => new EmployeeShiftBlockRow
                {
                    ShiftId = s.Id,
                    EmployeeId = s.EmployeeId,
                    EmployeeName = _context.Employees
                        .Where(e => e.Id == s.EmployeeId)
                        .Select(e => e.DisplayName == null || e.DisplayName == ""
                            ? (e.Name == null ? "بدون اسم" : e.Name)
                            : e.DisplayName)
                        .FirstOrDefault() ?? "بدون اسم",
                    ShiftStartTime = s.ShiftStartTime,
                    ShiftEndTime = s.ShiftEndTime,
                    IsLoginBlocked = s.IsLoginBlocked,
                    LoginBlockedAt = s.LoginBlockedAt,
                    LoginBlockReason = s.LoginBlockReason == null ? "" : s.LoginBlockReason,
                    AdminUnblockedUntil = s.AdminUnblockedUntil
                })
                .ToListAsync();

            var shiftBlockRows = shiftBlockRowsRaw
                .GroupBy(row => row.EmployeeId)
                .Select(group => group.OrderByDescending(row => row.ShiftId).First())
                .OrderBy(row => row.EmployeeName)
                .ToList();

            foreach (var log in attendanceLogs)
            {
                var shift = FindEmployeeShift(log, shifts);

                if (IsLeaveReason(log.DeductionReason))
                {
                    if (shift != null)
                    {
                        log.ShiftStartTimeText = shift.ShiftStartTime.ToString(@"hh\:mm");
                        log.ShiftEndTimeText = shift.ShiftEndTime.ToString(@"hh\:mm");
                    }
                    else
                    {
                        log.ShiftStartTimeText = "-";
                        log.ShiftEndTimeText = "-";
                    }

                    // يوم الإجازة يظهر في الجدول بدون وقت دخول أو خروج، وبخصم صفر.
                    // الـ View يعرض وقت الدخول "-" عندما IsSyntheticAbsentRow = true.
                    log.IsSyntheticAbsentRow = true;
                    log.IsAbsent = false;
                    log.LateMinutes = 0;
                    log.LateReason = "إجازة";
                    log.SuggestedLateReason = "إجازة";
                    log.SuggestedDeductionAmount = 0m;
                    log.CalculatedDeductionAmount = 0m;
                    log.DeductionAmount = 0m;
                    log.DeductionReason = "إجازة";
                    continue;
                }

                if (IsPureAutomaticAbsentRow(log))
                {
                    if (shift != null)
                    {
                        log.ShiftStartTimeText = shift.ShiftStartTime.ToString(@"hh\:mm");
                        log.ShiftEndTimeText = shift.ShiftEndTime.ToString(@"hh\:mm");
                    }
                    else
                    {
                        log.ShiftStartTimeText = "-";
                        log.ShiftEndTimeText = "-";
                    }

                    var storedAbsentDeduction = log.DeductionAmount ?? 0m;
                    // AutoAbsent المخزن في الداتا يستخدم CheckInAt كتاريخ/بداية الشيفت فقط.
                    // لذلك لازم نعلّمه كصف غياب صناعي في العرض حتى يظهر وقت الدخول/الخروج "-".
                    log.IsSyntheticAbsentRow = true;
                    log.IsAbsent = true;
                    log.LateMinutes = 0;
                    log.LateReason = "غياب";
                    log.SuggestedLateReason = "غياب";
                    log.SuggestedDeductionAmount = storedAbsentDeduction;
                    log.CalculatedDeductionAmount = storedAbsentDeduction;
                    continue;
                }

                if (shift == null)
                {
                    log.ShiftStartTimeText = "-";
                    log.ShiftEndTimeText = "-";
                    log.LateMinutes = null;

                    log.LateReason = string.IsNullOrWhiteSpace(log.DeductionReason)
                        ? "لم يتم تعيين شيفت لهذا الموظف"
                        : log.DeductionReason;

                    log.SuggestedDeductionAmount = log.DeductionAmount ?? 0;
                    log.SuggestedLateReason = log.LateReason;
                    log.CalculatedDeductionAmount = log.DeductionAmount ?? 0;

                    continue;
                }

                log.ShiftStartTimeText = shift.ShiftStartTime.ToString(@"hh\:mm");
                log.ShiftEndTimeText = shift.ShiftEndTime.ToString(@"hh\:mm");

                var result = CalculateLateDeduction(
                    log.CheckInAt,
                    shift.ShiftStartTime,
                    shift.ShiftEndTime,
                    shift.Salary
                );

                log.IsAbsent = result.IsAbsent;
                log.LateMinutes = result.IsAbsent ? 0 : result.LateMinutes;

                log.SuggestedLateReason = result.Reason;
                log.SuggestedDeductionAmount = result.DeductionAmount;

                var storedDeductionAmount = log.DeductionAmount ?? 0m;
                var isRecoveredAutomaticAbsentRow = IsRecoveredAutomaticAbsentRow(log);
                var storedReasonIsNoDeduction = IsNoDeductionReason(log.DeductionReason);
                var storedReasonIsAbsent = IsAbsentReason(log.DeductionReason);
                var storedReasonIsLate = IsLateReason(log.DeductionReason);
                var shouldIgnoreStoredAbsentReason = isRecoveredAutomaticAbsentRow && storedReasonIsAbsent && !result.IsAbsent;

                // للداتا القديمة: لو الخصم المخزن كان بسبب حساب قديم غلط مثل 09:00:01 = تأخير دقيقة،
                // والكود الحالي حسبها بدون خصم، نعرض الصحيح بدل المخزن.
                var shouldClearIncorrectStoredLateDeduction = !result.IsAbsent &&
                    result.DeductionAmount <= 0m &&
                    storedDeductionAmount > 0m &&
                    storedReasonIsLate;

                var shouldUseCalculatedDeduction = result.DeductionAmount > 0m &&
                    (storedDeductionAmount <= 0m || result.IsAbsent || shouldIgnoreStoredAbsentReason);

                log.LateReason = result.IsAbsent ||
                                 shouldUseCalculatedDeduction ||
                                 shouldIgnoreStoredAbsentReason ||
                                 shouldClearIncorrectStoredLateDeduction ||
                                 string.IsNullOrWhiteSpace(log.DeductionReason) ||
                                 storedReasonIsNoDeduction
                    ? result.Reason
                    : log.DeductionReason;

                log.CalculatedDeductionAmount = shouldUseCalculatedDeduction ||
                                                shouldIgnoreStoredAbsentReason ||
                                                shouldClearIncorrectStoredLateDeduction
                    ? result.DeductionAmount
                    : (log.DeductionAmount ?? result.DeductionAmount);

                var hasManualOrSyncedStoredDeduction =
                    storedDeductionAmount > 0m &&
                    !storedReasonIsNoDeduction &&
                    !shouldIgnoreStoredAbsentReason &&
                    !shouldClearIncorrectStoredLateDeduction;

                if (!hasManualOrSyncedStoredDeduction && !result.IsAbsent && log.CheckOutAt.HasValue && shift != null)
                {
                    var earlyCheckOutDisplayResult = CalculateEarlyCheckOutDeduction(
                        log.CheckInAt,
                        log.CheckOutAt.Value,
                        shift.ShiftStartTime,
                        shift.ShiftEndTime,
                        shift.Salary);

                    if (earlyCheckOutDisplayResult.DeductionAmount > 0m &&
                        (shouldIgnoreStoredAbsentReason || storedDeductionAmount <= 0m || string.IsNullOrWhiteSpace(log.DeductionReason) || storedReasonIsNoDeduction))
                    {
                        log.LateReason = earlyCheckOutDisplayResult.Reason;
                        log.CalculatedDeductionAmount = earlyCheckOutDisplayResult.DeductionAmount;
                        log.SuggestedLateReason = earlyCheckOutDisplayResult.Reason;
                        log.SuggestedDeductionAmount = earlyCheckOutDisplayResult.DeductionAmount;
                    }
                }
            }

            var displayedPeriodStart = isAutoPeriodFilter
                ? autoPeriodStart
                : fromDate?.Date
                    ?? (attendanceLogs.Any() ? attendanceLogs.Min(log => log.CheckInAt).Date : autoPeriodStart);

            var displayedPeriodEnd = isAutoPeriodFilter
                ? autoPeriodEnd
                : toDate?.Date.AddDays(1)
                    ?? (attendanceLogs.Any() ? attendanceLogs.Max(log => log.CheckInAt).Date.AddDays(1) : displayedPeriodStart.AddDays(1));

            if (displayedPeriodEnd <= displayedPeriodStart)
            {
                displayedPeriodEnd = displayedPeriodStart.AddDays(1);
            }

            var savedAutomaticAbsentRows = await CreateAutomaticAbsentRowsInDatabaseAsync(
                attendanceLogs,
                shifts,
                activeEmployeeIdsForDisplayedPeriod,
                displayedPeriodStart,
                displayedPeriodEnd,
                now,
                weeklyOffDaysByEmployee);

            if (savedAutomaticAbsentRows.Any())
            {
                attendanceLogs.AddRange(savedAutomaticAbsentRows);
            }

            attendanceLogs = AddAutomaticAbsentRowsToDisplayedLogs(
                attendanceLogs,
                shifts,
                activeEmployeeIdsForDisplayedPeriod,
                displayedPeriodStart,
                displayedPeriodEnd,
                now,
                weeklyOffDaysByEmployee);

            attendanceLogs = attendanceLogs
                .Where(log => !IsDeletedAttendanceNote(log.Notes))
                .ToList();

            attendanceLogs = RemoveAutomaticAbsentRowsWhenRealLogExists(attendanceLogs, shifts);
            attendanceLogs = DeduplicateAutomaticAbsentRowsForDisplay(attendanceLogs);

            await ApplyEmployeeImagesAndIpHistoryAsync(attendanceLogs);

            NormalizeDisplayedAttendanceDeductionReasons(attendanceLogs);

            var transactionAmountMaps = await BuildCumulativeTransactionAmountMapsAsync(attendanceLogs);
            var attendanceSummaryCounts = BuildDisplayedAttendanceSummaryCounts(attendanceLogs);

            ViewBag.PresentCount = attendanceSummaryCounts.PresentCount;
            ViewBag.AbsentCount = attendanceSummaryCounts.AbsentCount;
            ViewBag.LateCount = attendanceSummaryCounts.LateCount;
            ViewBag.DisciplinedCount = attendanceSummaryCounts.DisciplinedCount;
            ViewBag.LeaveCount = 0;

            ViewBag.Employees = await GetEmployeesSelectListAsync();
            ViewBag.AttendanceLogs = attendanceLogs;
            ViewBag.EmployeeShiftBlockRows = shiftBlockRows;
            ViewBag.TransactionDeductionAmountMap = transactionAmountMaps.TransactionDeductionAmountMap;
            ViewBag.AdvanceAmountMap = transactionAmountMaps.AdvanceAmountMap;
            ViewBag.BonusAmountMap = transactionAmountMaps.BonusAmountMap;
            ViewBag.FilterEmployeeId = employeeId?.ToString() ?? "";
            ViewBag.IsAutoPeriodFilter = isAutoPeriodFilter;
            ViewBag.AutoPeriodStart = autoPeriodStart;
            ViewBag.AutoPeriodEnd = autoPeriodEnd;
            ViewBag.FilterFromDate = isAutoPeriodFilter
                ? autoPeriodStart.ToString("yyyy-MM-dd")
                : fromDate?.ToString("yyyy-MM-dd") ?? "";
            ViewBag.FilterToDate = isAutoPeriodFilter
                ? autoPeriodEnd.ToString("yyyy-MM-dd")
                : toDate?.ToString("yyyy-MM-dd") ?? "";

            return View();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ToggleEmployeeLoginBlock([FromBody] ToggleEmployeeLoginBlockRequest request)
        {
            try
            {
                if (request == null || request.ShiftId <= 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "لم يتم العثور على الشيفت المطلوب"
                    });
                }

                var shift = await _context.EmployeeWorkShifts
                    .FirstOrDefaultAsync(s => s.Id == request.ShiftId && s.IsActive);

                if (shift == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "الشيفت غير موجود أو غير نشط"
                    });
                }

                var now = GetEgyptNow();

                if (request.IsBlocked)
                {
                    shift.IsLoginBlocked = true;
                    shift.LoginBlockedAt = now;
                    shift.LoginBlockReason = "تم عمل بلوك يدوي من الإدارة";
                    shift.AdminUnblockedUntil = null;
                    shift.AdminUnblockedAt = null;
                    shift.AdminUnblockedByUserId = null;
                    shift.UpdatedAt = now;

                    await CloseOpenAttendanceLogForManualBlockAsync(shift, now);

                    await _context.SaveChangesAsync();

                    return Json(new
                    {
                        success = true,
                        isBlocked = true,
                        refreshAttendanceLog = true,
                        message = "تم تفعيل بلوك الدخول للموظف وتحديث سجل الدوام فورًا"
                    });
                }

                shift.IsLoginBlocked = false;
                shift.LoginBlockedAt = null;
                shift.LoginBlockReason = "تم فك البلوك يدويًا من الإدارة";
                shift.AdminUnblockedAt = now;
                shift.AdminUnblockedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
                shift.AdminUnblockedUntil = GetNextShiftStartDateTime(now, shift.ShiftStartTime);
                shift.UpdatedAt = now;

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    isBlocked = false,
                    adminUnblockedUntil = shift.AdminUnblockedUntil?.ToString("yyyy/MM/dd HH:mm") ?? "",
                    message = "تم فك البلوك، ويمكن للموظف تسجيل الدخول الآن"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        private async Task CloseOpenAttendanceLogForManualBlockAsync(EmployeeWorkShift shift, DateTime checkOutAt)
        {
            if (shift == null || shift.EmployeeId <= 0)
            {
                return;
            }

            var currentWindow = BuildAttendanceShiftWindowForTime(
                checkOutAt,
                shift.ShiftStartTime,
                shift.ShiftEndTime);

            var openLog = await _context.EmployeeAttendanceLogs
                .Where(log =>
                    log.EmployeeId == shift.EmployeeId &&
                    log.CheckOutAt == null &&
                    (log.Notes == null || !log.Notes.Contains(AttendanceDeletedNoteMarker)) &&
                    log.CheckInAt >= currentWindow.AccessStart &&
                    log.CheckInAt <= currentWindow.ShiftEndWithGrace)
                .OrderByDescending(log => log.CheckInAt)
                .FirstOrDefaultAsync();

            if (openLog == null)
            {
                return;
            }

            var employeeSalary = await _context.Employees
                .AsNoTracking()
                .Where(employee => employee.Id == shift.EmployeeId)
                .Select(employee => employee.Salary)
                .FirstOrDefaultAsync();

            openLog.CheckOutAt = checkOutAt;
            openLog.CheckOutLocation = "تسجيل خروج بسبب بلوك الإدارة";

            ApplyEarlyCheckOutDeduction(
                openLog,
                shift.EmployeeId,
                employeeSalary,
                shift.ShiftStartTime,
                shift.ShiftEndTime,
                checkOutAt,
                createTransaction: true);

            openLog.UpdatedAt = checkOutAt;

            if (string.IsNullOrWhiteSpace(openLog.Notes))
            {
                openLog.Notes = "تسجيل خروج بسبب بلوك الإدارة";
            }
            else if (!openLog.Notes.Contains("تسجيل خروج بسبب بلوك الإدارة"))
            {
                openLog.Notes = openLog.Notes + " - تسجيل خروج بسبب بلوك الإدارة";
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin,ExecutiveDirector,Accountant")]
        public async Task<IActionResult> GetAttendanceLogLiveVersion()
        {
            try
            {
                var latestValue = await _context.EmployeeAttendanceLogs
                    .AsNoTracking()
                    .Select(log => log.UpdatedAt ?? log.CheckOutAt ?? log.CheckInAt)
                    .OrderByDescending(value => value)
                    .FirstOrDefaultAsync();

                var totalCount = await _context.EmployeeAttendanceLogs
                    .AsNoTracking()
                    .CountAsync();

                var latestTransactionValue = await _context.EmployeeTransactions
                    .AsNoTracking()
                    .Select(transaction => transaction.DeletedAt ?? transaction.Date)
                    .OrderByDescending(value => value)
                    .FirstOrDefaultAsync();

                var activeTransactionsCount = await _context.EmployeeTransactions
                    .AsNoTracking()
                    .CountAsync(transaction => !transaction.IsDeleted);

                var latestTicks = Math.Max(latestValue.Ticks, latestTransactionValue.Ticks);

                return Json(new
                {
                    success = true,
                    version = latestTicks.ToString() + "-" + totalCount.ToString() + "-" + activeTransactionsCount.ToString(),
                    totalCount
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        private static DateTime GetNextShiftStartDateTime(DateTime currentTime, TimeSpan shiftStartTime)
        {
            var todayShiftStart = currentTime.Date.Add(shiftStartTime);

            return currentTime < todayShiftStart
                ? todayShiftStart
                : todayShiftStart.AddDays(1);
        }

        private static DateTime GetAttendanceAutoPeriodStart(DateTime currentTime)
        {
            var todayAtTen = currentTime.Date.AddHours(10);

            return currentTime < todayAtTen
                ? todayAtTen.AddDays(-1)
                : todayAtTen;
        }

        private static string EncodeAttendanceHistoryValue(string? value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string DecodeAttendanceHistoryValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value));
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string FormatAttendanceHistoryDecimal(decimal value)
        {
            return value.ToString("0.##");
        }

        private static bool AreAttendanceHistoryValuesDifferent(string? oldValue, string? newValue)
        {
            return !string.Equals((oldValue ?? string.Empty).Trim(), (newValue ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static void AddAttendanceHistoryChange(List<AttendanceEditHistoryChange> changes, string fieldName, string? oldValue, string? newValue)
        {
            var oldText = oldValue ?? string.Empty;
            var newText = newValue ?? string.Empty;

            if (!AreAttendanceHistoryValuesDifferent(oldText, newText))
            {
                return;
            }

            changes.Add(new AttendanceEditHistoryChange
            {
                FieldName = fieldName,
                OldValue = string.IsNullOrWhiteSpace(oldText) ? "-" : oldText,
                NewValue = string.IsNullOrWhiteSpace(newText) ? "-" : newText
            });
        }

        private static string BuildAttendanceEditHistoryChangesPayload(List<AttendanceEditHistoryChange> changes)
        {
            if (changes == null || !changes.Any())
            {
                return string.Empty;
            }

            var lines = changes.Select(change =>
                string.Join("::",
                    EncodeAttendanceHistoryValue(change.FieldName),
                    EncodeAttendanceHistoryValue(change.OldValue),
                    EncodeAttendanceHistoryValue(change.NewValue)));

            return EncodeAttendanceHistoryValue(string.Join("@@", lines));
        }

        private static List<AttendanceEditHistoryChange> ParseAttendanceEditHistoryChangesPayload(string payload)
        {
            var changes = new List<AttendanceEditHistoryChange>();
            var decodedPayload = DecodeAttendanceHistoryValue(payload);

            if (string.IsNullOrWhiteSpace(decodedPayload))
            {
                return changes;
            }

            var lineParts = decodedPayload.Split(new[] { "@@" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lineParts)
            {
                var parts = line.Split(new[] { "::" }, StringSplitOptions.None);

                if (parts.Length < 3)
                {
                    continue;
                }

                changes.Add(new AttendanceEditHistoryChange
                {
                    FieldName = DecodeAttendanceHistoryValue(parts[0]),
                    OldValue = DecodeAttendanceHistoryValue(parts[1]),
                    NewValue = DecodeAttendanceHistoryValue(parts[2])
                });
            }

            return changes;
        }

        private static string AddAttendanceEditHistoryEntry(string? notes, AttendanceEditHistoryEntry entry)
        {
            if (entry == null || entry.Changes == null || !entry.Changes.Any())
            {
                return notes ?? string.Empty;
            }

            var payload = string.Join("|",
                entry.ChangedAt.Ticks.ToString(),
                EncodeAttendanceHistoryValue(entry.EditorName),
                EncodeAttendanceHistoryValue(entry.ShiftStartTimeText),
                EncodeAttendanceHistoryValue(entry.ShiftEndTimeText),
                BuildAttendanceEditHistoryChangesPayload(entry.Changes));

            var marker = AttendanceEditHistoryStartMarker + payload + AttendanceEditHistoryEndMarker;
            var cleanNotes = notes ?? string.Empty;

            return string.IsNullOrWhiteSpace(cleanNotes)
                ? marker
                : cleanNotes.Trim() + Environment.NewLine + marker;
        }

        private static List<AttendanceEditHistoryEntry> ParseAttendanceEditHistoryEntries(string? notes)
        {
            var entries = new List<AttendanceEditHistoryEntry>();
            var text = notes ?? string.Empty;
            var searchIndex = 0;

            while (searchIndex < text.Length)
            {
                var startIndex = text.IndexOf(AttendanceEditHistoryStartMarker, searchIndex, StringComparison.OrdinalIgnoreCase);

                if (startIndex < 0)
                {
                    break;
                }

                var payloadStart = startIndex + AttendanceEditHistoryStartMarker.Length;
                var endIndex = text.IndexOf(AttendanceEditHistoryEndMarker, payloadStart, StringComparison.OrdinalIgnoreCase);

                if (endIndex < 0)
                {
                    break;
                }

                var payload = text.Substring(payloadStart, endIndex - payloadStart);
                var parts = payload.Split('|');

                if (parts.Length >= 5 && long.TryParse(parts[0], out var ticks))
                {
                    var changedAt = DateTime.MinValue;

                    try
                    {
                        changedAt = new DateTime(ticks);
                    }
                    catch
                    {
                        changedAt = DateTime.MinValue;
                    }

                    var changes = ParseAttendanceEditHistoryChangesPayload(parts[4]);

                    if (changedAt != DateTime.MinValue && changes.Any())
                    {
                        entries.Add(new AttendanceEditHistoryEntry
                        {
                            ChangedAt = changedAt,
                            EditorName = DecodeAttendanceHistoryValue(parts[1]),
                            ShiftStartTimeText = DecodeAttendanceHistoryValue(parts[2]),
                            ShiftEndTimeText = DecodeAttendanceHistoryValue(parts[3]),
                            Changes = changes
                        });
                    }
                }

                searchIndex = endIndex + AttendanceEditHistoryEndMarker.Length;
            }

            return entries
                .OrderByDescending(entry => entry.ChangedAt)
                .ToList();
        }

        private async Task<string> GetCurrentAttendanceEditorNameAsync()
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

        private async Task<decimal> GetDailyTransactionTotalForAttendanceLogAsync(EmployeeAttendanceLog log, TransactionTypeEnum transactionType)
        {
            if (log == null || !log.EmployeeId.HasValue)
            {
                return 0m;
            }

            var employeeId = log.EmployeeId.Value;
            var logDate = log.CheckInAt.Date;

            return await _context.EmployeeTransactions
                .AsNoTracking()
                .Where(t =>
                    t.EmployeeId == employeeId &&
                    !t.IsDeleted &&
                    t.TransactionType == transactionType &&
                    t.Date.Date == logDate)
                .SumAsync(t => t.Amount);
        }

        private async Task<EmployeeShiftLookupRow?> GetAttendanceLogShiftAsync(EmployeeAttendanceLog log)
        {
            if (log == null || !log.EmployeeId.HasValue)
            {
                return null;
            }

            var logRow = new EmployeeAttendanceLogRow
            {
                Id = log.Id,
                EmployeeId = log.EmployeeId,
                CheckInAt = log.CheckInAt
            };

            var shifts = await _context.EmployeeWorkShifts
                .AsNoTracking()
                .Where(s => s.EmployeeId == log.EmployeeId.Value)
                .OrderByDescending(s => s.CreatedAt)
                .ThenByDescending(s => s.Id)
                .Select(s => new EmployeeShiftLookupRow
                {
                    Id = s.Id,
                    EmployeeId = s.EmployeeId,
                    EmployeeName = "",
                    Salary = _context.Employees
                        .Where(e => e.Id == s.EmployeeId)
                        .Select(e => e.Salary)
                        .FirstOrDefault(),
                    ShiftStartTime = s.ShiftStartTime,
                    ShiftEndTime = s.ShiftEndTime,
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync();

            return FindEmployeeShift(logRow, shifts);
        }

        private static string FormatAttendanceTimeSpan(TimeSpan time)
        {
            return DateTime.Today.Add(time).ToString("HH:mm");
        }

        private sealed class AttendanceManualEditCalculationResult
        {
            public decimal DeductionAmount { get; set; }

            public string Reason { get; set; } = "";
        }

        private static AttendanceManualEditCalculationResult CalculateAttendanceManualEditDeduction(
            DateTime checkInAt,
            DateTime? checkOutAt,
            TimeSpan shiftStartTime,
            TimeSpan shiftEndTime,
            decimal salary)
        {
            var lateResult = CalculateLateDeduction(
                checkInAt,
                shiftStartTime,
                shiftEndTime,
                salary);

            var deductionAmount = lateResult.DeductionAmount;
            var reason = lateResult.Reason;

            if (checkOutAt.HasValue)
            {
                var earlyCheckOutResult = CalculateEarlyCheckOutDeduction(
                    checkInAt,
                    checkOutAt.Value,
                    shiftStartTime,
                    shiftEndTime,
                    salary);

                if (earlyCheckOutResult.DeductionAmount > 0m)
                {
                    deductionAmount = Math.Round(deductionAmount + earlyCheckOutResult.DeductionAmount, 2);
                    reason = AppendDeductionReason(reason, earlyCheckOutResult.Reason);
                }
            }

            return new AttendanceManualEditCalculationResult
            {
                DeductionAmount = deductionAmount,
                Reason = reason
            };
        }


        private async Task<decimal> GetAttendanceEmployeeSalaryAsync(int? employeeId)
        {
            if (!employeeId.HasValue)
            {
                return 0m;
            }

            try
            {
                return await _context.Employees
                    .AsNoTracking()
                    .Where(e => e.Id == employeeId.Value)
                    .Select(e => e.Salary)
                    .FirstOrDefaultAsync();
            }
            catch
            {
                return 0m;
            }
        }

        private async Task<decimal> SafeGetDailyTransactionTotalForAttendanceLogAsync(EmployeeAttendanceLog log, TransactionTypeEnum transactionType)
        {
            try
            {
                return await GetDailyTransactionTotalForAttendanceLogAsync(log, transactionType);
            }
            catch
            {
                return 0m;
            }
        }

        private async Task SafeUpdateAttendanceTransactionsAsync(
            EmployeeAttendanceLog log,
            decimal oldDeductionAmount,
            string? oldDeductionReason,
            decimal newDeductionAmount,
            string? newDeductionReason,
            DateTime oldLogDate,
            decimal requestedAdvanceAmount,
            decimal requestedBonusAmount)
        {
            try
            {
                await UpdateAttendanceDeductionTransactionForLogAsync(
                    log,
                    oldDeductionAmount,
                    oldDeductionReason,
                    newDeductionAmount,
                    newDeductionReason,
                    oldLogDate);
            }
            catch
            {
                // لا نوقف تعديل سجل الدوام لو حصلت مشكلة في ربط الخصم.
            }

            try
            {
                await UpdateManualAdvanceForLogAsync(log, requestedAdvanceAmount);
            }
            catch
            {
                // لا نوقف تعديل سجل الدوام لو حصلت مشكلة في السلف.
            }

            try
            {
                await UpdateManualBonusForLogAsync(log, requestedBonusAmount);
            }
            catch
            {
                // لا نوقف تعديل سجل الدوام لو حصلت مشكلة في المكافآت.
            }
        }

        private static bool IsDeletedAttendanceNote(string? notes)
        {
            return !string.IsNullOrWhiteSpace(notes) &&
                   notes.Contains(AttendanceDeletedNoteMarker, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAutomaticAbsentNote(string? notes)
        {
            return !string.IsNullOrWhiteSpace(notes) &&
                   notes.Contains("AutoAbsent", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasRealAttendanceEvidence(EmployeeAttendanceLogRow log)
        {
            if (log == null)
            {
                return false;
            }

            /*
                مهم جدًا للداتا القديمة:
                صف الغياب التلقائي AutoAbsent كان ممكن يتسجل عليه خروج بالغلط
                لما دخول اليوم الجديد يتركب كأنه خروج على سجل امبارح.
                لذلك لا نعتبر CheckOutAt أو CheckOut IP دليل حضور حقيقي.
                الحضور الحقيقي لازم يكون له دليل دخول: صورة دخول أو IP دخول أو تسجيل حضور بسؤال.
            */
            return !string.IsNullOrWhiteSpace(log.FaceImagePath) ||
                   !string.IsNullOrWhiteSpace(log.CheckInIpAddress) ||
                   !string.IsNullOrWhiteSpace(log.CheckInLocation) ||
                   IsQuestionCheckInNoteText(log.Notes);
        }

        private static bool IsQuestionCheckInNoteText(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
            {
                return false;
            }

            return notes.Contains("تسجيل الحضور بسؤال", StringComparison.OrdinalIgnoreCase) ||
                   notes.Contains("جاهز لبدء الدوام", StringComparison.OrdinalIgnoreCase) ||
                   notes.Contains("QuestionCheckIn", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPureAutomaticAbsentRow(EmployeeAttendanceLogRow log)
        {
            if (log == null)
            {
                return false;
            }

            return (log.IsSyntheticAbsentRow || IsAutomaticAbsentNote(log.Notes)) &&
                   !HasRealAttendanceEvidence(log);
        }

        private static bool IsRecoveredAutomaticAbsentRow(EmployeeAttendanceLogRow log)
        {
            return log != null &&
                   IsAutomaticAbsentNote(log.Notes) &&
                   HasRealAttendanceEvidence(log);
        }

        private static bool IsAutomaticAbsentDisplayRow(EmployeeAttendanceLogRow log)
        {
            return IsPureAutomaticAbsentRow(log);
        }

        private static bool IsAbsentReason(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return false;
            }

            var text = reason.Trim();
            return text.Contains("غياب", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("غائب", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("AutoAbsent", StringComparison.OrdinalIgnoreCase);
        }

        private static List<EmployeeAttendanceLogRow> RemoveAutomaticAbsentRowsWhenRealLogExists(
            List<EmployeeAttendanceLogRow> attendanceLogs,
            List<EmployeeShiftLookupRow> shifts)
        {
            attendanceLogs ??= new List<EmployeeAttendanceLogRow>();
            shifts ??= new List<EmployeeShiftLookupRow>();

            return attendanceLogs
                .Where(log =>
                {
                    if (!log.EmployeeId.HasValue || !IsAutomaticAbsentDisplayRow(log))
                    {
                        return true;
                    }

                    var shift = FindEmployeeShift(log, shifts);

                    if (shift == null)
                    {
                        var fallbackKey = log.EmployeeId.Value.ToString() + "|" + log.CheckInAt.Date.ToString("yyyyMMdd");

                        return !attendanceLogs.Any(realLog =>
                            realLog.EmployeeId == log.EmployeeId &&
                            !IsAutomaticAbsentDisplayRow(realLog) &&
                            !IsDeletedAttendanceNote(realLog.Notes) &&
                            realLog.CheckInAt.Date.ToString("yyyyMMdd") == log.CheckInAt.Date.ToString("yyyyMMdd") &&
                            realLog.EmployeeId.Value.ToString() + "|" + realLog.CheckInAt.Date.ToString("yyyyMMdd") == fallbackKey);
                    }

                    var intervalStart = log.CheckInAt.Date.Add(shift.ShiftStartTime);
                    var intervalEnd = GetShiftEndDateTimeForShiftStart(intervalStart, shift.ShiftEndTime)
                        .AddMinutes(ShiftAccessGraceMinutes);

                    return !HasRealAttendanceForShift(
                        attendanceLogs,
                        log.EmployeeId.Value,
                        intervalStart,
                        intervalEnd);
                })
                .ToList();
        }

        private static List<EmployeeAttendanceLogRow> DeduplicateAutomaticAbsentRowsForDisplay(List<EmployeeAttendanceLogRow> attendanceLogs)
        {
            attendanceLogs ??= new List<EmployeeAttendanceLogRow>();

            var realRows = attendanceLogs
                .Where(log => !IsAutomaticAbsentDisplayRow(log))
                .ToList();

            var automaticAbsentRows = attendanceLogs
                .Where(IsAutomaticAbsentDisplayRow)
                .GroupBy(log =>
                    (log.EmployeeId?.ToString() ?? "0") + "|" + log.CheckInAt.ToString("yyyyMMddHHmm"),
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(log => log.Id > 0)
                    .ThenByDescending(log => log.Id)
                    .First())
                .ToList();

            return realRows
                .Concat(automaticAbsentRows)
                .OrderByDescending(log => log.CheckInAt)
                .ThenByDescending(log => log.Id)
                .ToList();
        }

        private static string AddAttendanceDeletedMarker(string? notes)
        {
            var cleanNotes = RemoveAttendanceDeletedMarker(notes);

            return string.IsNullOrWhiteSpace(cleanNotes)
                ? AttendanceDeletedNoteMarker
                : cleanNotes.Trim() + " " + AttendanceDeletedNoteMarker;
        }

        private static string RemoveAttendanceDeletedMarker(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
            {
                return "";
            }

            return notes
                .Replace(AttendanceDeletedNoteMarker, "", StringComparison.OrdinalIgnoreCase)
                .Trim();
        }



        private static void NormalizeDisplayedAttendanceDeductionReasons(List<EmployeeAttendanceLogRow> attendanceLogs)
        {
            if (attendanceLogs == null || !attendanceLogs.Any())
            {
                return;
            }

            foreach (var log in attendanceLogs)
            {
                log.LateReason = NormalizeDeductionReasonText(log.LateReason);
                log.DeductionReason = NormalizeDeductionReasonText(log.DeductionReason);
                log.SuggestedLateReason = NormalizeDeductionReasonText(log.SuggestedLateReason);
            }
        }

        private static string NormalizeDeductionReasonText(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return string.Empty;
            }

            var result = new List<string>();
            var seenCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var parts = reason
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

            return result.Any() ? string.Join(" + ", result) : reason.Trim();
        }

        private async Task<AttendanceTransactionAmountMaps> BuildCumulativeTransactionAmountMapsAsync(List<EmployeeAttendanceLogRow> attendanceLogs)
        {
            var maps = new AttendanceTransactionAmountMaps();

            if (attendanceLogs == null || !attendanceLogs.Any())
            {
                return maps;
            }

            foreach (var log in attendanceLogs)
            {
                // الخصم في صفحة اللوج يوم بيومه / سجل بسجل، وليس إجمالي تراكمي.
                maps.TransactionDeductionAmountMap[log.Id] = log.CalculatedDeductionAmount.ToString("0.00");
                maps.AdvanceAmountMap[log.Id] = "0.00";
                maps.BonusAmountMap[log.Id] = "0.00";
            }

            var employeeIds = attendanceLogs
                .Where(log => log.EmployeeId.HasValue)
                .Select(log => log.EmployeeId.Value)
                .Distinct()
                .ToList();

            if (!employeeIds.Any())
            {
                return maps;
            }

            var maxAttendanceDateExclusive = attendanceLogs
                .Max(log => log.CheckInAt.Date)
                .AddDays(1);

            var transactions = await _context.EmployeeTransactions
                .AsNoTracking()
                .Where(transaction =>
                    employeeIds.Contains(transaction.EmployeeId) &&
                    !transaction.IsDeleted &&
                    transaction.Date < maxAttendanceDateExclusive &&
                    (
                        transaction.TransactionType == (TransactionTypeEnum)1 || // مكافأة
                        transaction.TransactionType == (TransactionTypeEnum)2    // سلفة
                    ))
                .Select(transaction => new
                {
                    transaction.EmployeeId,
                    transaction.Amount,
                    transaction.Date,
                    TransactionType = (int)transaction.TransactionType
                })
                .ToListAsync();

            Console.WriteLine("========== Attendance Log Daily Deduction / Daily Advances Bonuses ==========");
            Console.WriteLine($"Attendance rows count: {attendanceLogs.Count}");
            Console.WriteLine($"Employee IDs: {string.Join(", ", employeeIds)}");
            Console.WriteLine($"Bonus/advance transactions loaded: {transactions.Count}");

            foreach (var log in attendanceLogs)
            {
                if (!log.EmployeeId.HasValue)
                {
                    continue;
                }

                var employeeId = log.EmployeeId.Value;
                var logDate = log.CheckInAt.Date;

                var dailyBonusAmount = transactions
                    .Where(transaction =>
                        transaction.EmployeeId == employeeId &&
                        transaction.TransactionType == 1 &&
                        transaction.Date.Date == logDate)
                    .Sum(transaction => transaction.Amount);

                var dailyAdvanceAmount = transactions
                    .Where(transaction =>
                        transaction.EmployeeId == employeeId &&
                        transaction.TransactionType == 2 &&
                        transaction.Date.Date == logDate)
                    .Sum(transaction => transaction.Amount);

                maps.BonusAmountMap[log.Id] = dailyBonusAmount.ToString("0.00");
                maps.AdvanceAmountMap[log.Id] = dailyAdvanceAmount.ToString("0.00");

                Console.WriteLine(
                    $"LogId: {log.Id}, EmployeeId: {employeeId}, Date: {logDate:yyyy-MM-dd}, DailyDeduction: {log.CalculatedDeductionAmount:0.00}, DailyAdvance: {dailyAdvanceAmount:0.00}, DailyBonus: {dailyBonusAmount:0.00}"
                );
            }

            Console.WriteLine("===========================================================================");

            return maps;
        }

        private class AttendanceTransactionAmountMaps
        {
            public Dictionary<int, string> TransactionDeductionAmountMap { get; set; } = new Dictionary<int, string>();

            public Dictionary<int, string> AdvanceAmountMap { get; set; } = new Dictionary<int, string>();

            public Dictionary<int, string> BonusAmountMap { get; set; } = new Dictionary<int, string>();
        }

        private const string CheckInMethodPhoto = "Photo";
        private const string CheckInMethodQuestion = "Question";

        private static string NormalizeCheckInVerificationMethod(string? value)
        {
            var text = (value ?? "").Trim();

            if (text.Equals(CheckInMethodQuestion, StringComparison.OrdinalIgnoreCase) ||
                text.Contains("سؤال"))
            {
                return CheckInMethodQuestion;
            }

            return CheckInMethodPhoto;
        }

        private async Task<string> GetEmployeeCheckInVerificationMethodAsync(int employeeId)
        {
            try
            {
                var connection = _context.Database.GetDbConnection();
                var shouldClose = connection.State != System.Data.ConnectionState.Open;

                if (shouldClose)
                {
                    await connection.OpenAsync();
                }

                try
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        IF COL_LENGTH('Employees', 'CheckInVerificationMethod') IS NULL
                        BEGIN
                            SELECT 'Photo'
                        END
                        ELSE
                        BEGIN
                            SELECT TOP 1 ISNULL(NULLIF([CheckInVerificationMethod], ''), 'Photo')
                            FROM [Employees]
                            WHERE [Id] = @EmployeeId
                        END";

                    var parameter = command.CreateParameter();
                    parameter.ParameterName = "@EmployeeId";
                    parameter.Value = employeeId;
                    command.Parameters.Add(parameter);

                    var result = await command.ExecuteScalarAsync();
                    return NormalizeCheckInVerificationMethod(result?.ToString());
                }
                finally
                {
                    if (shouldClose)
                    {
                        await connection.CloseAsync();
                    }
                }
            }
            catch
            {
                return CheckInMethodPhoto;
            }
        }

        private async Task<string> GetEmployeeSavedImagePathAsync(int employeeId)
        {
            var imagePath = await _context.Employees
                .AsNoTracking()
                .Where(e => e.Id == employeeId)
                .Select(e => e.ImageUrl == null ? "" : e.ImageUrl)
                .FirstOrDefaultAsync();

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

        private static bool IsQuestionCheckInLog(EmployeeAttendanceLog? log)
        {
            if (log == null)
            {
                return false;
            }

            var notes = log.Notes ?? string.Empty;

            return notes.Contains("تسجيل الحضور بسؤال", StringComparison.OrdinalIgnoreCase)
                || notes.Contains("جاهز لبدء الدوام", StringComparison.OrdinalIgnoreCase)
                || notes.Contains("QuestionCheckIn", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRealAttendanceLog(EmployeeAttendanceLog? log)
        {
            if (log == null)
            {
                return false;
            }

            if (IsDeletedAttendanceNote(log.Notes) || IsAutomaticAbsentNote(log.Notes))
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(log.FaceImagePath) ||
                   !string.IsNullOrWhiteSpace(log.CheckInIpAddress) ||
                   !string.IsNullOrWhiteSpace(log.CheckInLocation) ||
                   IsQuestionCheckInLog(log) ||
                   (!string.IsNullOrWhiteSpace(log.Notes) &&
                    log.Notes.Contains("RecoveredCheckIn", StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasCompletedCheckInVerification(EmployeeAttendanceLog? log)
        {
            if (log == null)
            {
                return false;
            }

            if (IsDeletedAttendanceNote(log.Notes) || IsAutomaticAbsentNote(log.Notes))
            {
                return false;
            }

            // مهم:
            // وجود IP أو Location فقط لا يعني أن الموظف صوّر/سجّل الحضور فعليًا.
            // لذلك لا نعتبر الحضور مكتمل إلا بصورة دخول أو تسجيل سؤال.
            return !string.IsNullOrWhiteSpace(log.FaceImagePath) ||
                   IsQuestionCheckInLog(log);
        }

        private async Task<EmployeeAttendanceLog?> FindFirstRealAttendanceLogForEmployeeOnDateAsync(
            string userId,
            int employeeId,
            DateTime referenceTime,
            bool asNoTracking)
        {
            var dayStart = referenceTime.Date;
            var dayEnd = dayStart.AddDays(1);

            /*
                مهم جدًا:
                منع التكرار يكون على EmployeeId وليس UserId فقط.
                بعض سجلات الاسترجاع أو السجلات القديمة ممكن يكون UserId فيها ناقص/مختلف،
                لكن طالما نفس الموظف له حضور حقيقي في نفس اليوم، لا ننشئ سجل جديد ولا نضيف خصم جديد.
            */
            var query = _context.EmployeeAttendanceLogs
                .Where(log =>
                    log.EmployeeId == employeeId &&
                    log.CheckInAt >= dayStart &&
                    log.CheckInAt < dayEnd &&
                    (log.Notes == null ||
                     (!log.Notes.Contains(AttendanceDeletedNoteMarker) &&
                      !log.Notes.Contains("AutoAbsent"))) &&
                    (
                        (log.FaceImagePath != null && log.FaceImagePath != "") ||
                        (log.CheckInIpAddress != null && log.CheckInIpAddress != "") ||
                        (log.CheckInLocation != null && log.CheckInLocation != "") ||
                        (log.Notes != null &&
                         (log.Notes.Contains("QuestionCheckIn") ||
                          log.Notes.Contains("تسجيل الحضور بسؤال") ||
                          log.Notes.Contains("جاهز لبدء الدوام") ||
                          log.Notes.Contains("RecoveredCheckIn")))
                    ));

            if (asNoTracking)
            {
                query = query.AsNoTracking();
            }

            return await query
                .OrderBy(log => log.CheckInAt)
                .ThenBy(log => log.Id)
                .FirstOrDefaultAsync();
        }

        private async Task<EmployeeShiftLoginLookup?> GetCurrentEmployeeShiftLoginLookupAsync(int employeeId, DateTime referenceTime)
        {
            var shifts = await _context.EmployeeWorkShifts
                .AsNoTracking()
                .Where(s =>
                    s.IsActive &&
                    s.EmployeeId == employeeId &&
                    s.CreatedAt <= referenceTime)
                .OrderByDescending(s => s.CreatedAt)
                .ThenByDescending(s => s.Id)
                .Select(s => new EmployeeShiftLoginLookup
                {
                    Id = s.Id,
                    EmployeeId = s.EmployeeId,
                    ShiftStartTime = s.ShiftStartTime,
                    ShiftEndTime = s.ShiftEndTime,
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync();

            if (!shifts.Any())
            {
                return null;
            }

            var windowMatchedShift = shifts.FirstOrDefault(shift =>
            {
                var window = BuildAttendanceShiftWindowForTime(referenceTime, shift.ShiftStartTime, shift.ShiftEndTime);
                return referenceTime >= window.AccessStart && referenceTime <= window.ShiftEndWithGrace;
            });

            return windowMatchedShift ?? shifts.First();
        }

        [HttpGet]
        public async Task<IActionResult> GetCheckInCaptureStatus()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Json(new
                    {
                        success = false,
                        shouldCapture = true,
                        checkInMethod = CheckInMethodPhoto,
                        shouldAskQuestion = false,
                        message = "لم يتم العثور على المستخدم الحالي"
                    });
                }

                if (ShouldSkipCheckInVerificationForCurrentUser())
                {
                    return Json(new
                    {
                        success = true,
                        shouldCapture = false,
                        checkInMethod = CheckInMethodPhoto,
                        shouldAskQuestion = false,
                        hasOpenLog = false,
                        message = "هذا الدور مستثنى من بصمة الدخول"
                    });
                }

                var now = GetEgyptNow();

                var employeeInfo = await _context.Employees
                    .AsNoTracking()
                    .Where(e => e.ApplicationUserId == userId)
                    .Select(e => new EmployeeLoginInfo
                    {
                        EmployeeId = e.Id,
                        Name = e.Name,
                        DisplayName = e.DisplayName,
                        Salary = e.Salary
                    })
                    .FirstOrDefaultAsync();

                if (employeeInfo == null)
                {
                    return Json(new
                    {
                        success = false,
                        shouldCapture = true,
                        checkInMethod = CheckInMethodPhoto,
                        shouldAskQuestion = false,
                        message = "هذا المستخدم غير مربوط بموظف"
                    });
                }

                var checkInMethod = await GetEmployeeCheckInVerificationMethodAsync(employeeInfo.EmployeeId);

                // مهم: لو الموظف له أي سجل حضور حقيقي في نفس اليوم، لا نطلب صورة/سؤال مرة أخرى.
                // نعتمد أول تسجيل دخول في اليوم فقط، حتى لو الموظف عمل Login مرة ثانية.
                var firstTodayAttendanceLog = await FindFirstRealAttendanceLogForEmployeeOnDateAsync(
                    userId,
                    employeeInfo.EmployeeId,
                    now,
                    asNoTracking: true);

                if (HasCompletedCheckInVerification(firstTodayAttendanceLog))
                {
                    return Json(new
                    {
                        success = true,
                        shouldCapture = false,
                        checkInMethod = checkInMethod,
                        shouldAskQuestion = false,
                        hasOpenLog = firstTodayAttendanceLog.CheckOutAt == null,
                        attendanceLogId = firstTodayAttendanceLog.Id,
                        message = "لديك تسجيل حضور مسجل بالفعل اليوم. يتم اعتماد أول تسجيل دخول فقط"
                    });
                }

                var shift = await GetCurrentEmployeeShiftLoginLookupAsync(employeeInfo.EmployeeId, now);

                var latestPeriodLog = await FindLatestAttendanceLogInCurrentWorkPeriodAsync(
                    userId,
                    employeeInfo.EmployeeId,
                    now,
                    shift == null ? (TimeSpan?)null : shift.ShiftStartTime,
                    shift == null ? (TimeSpan?)null : shift.ShiftEndTime,
                    asNoTracking: true);

                if (latestPeriodLog != null)
                {
                    if (checkInMethod == CheckInMethodQuestion && IsQuestionCheckInLog(latestPeriodLog))
                    {
                        return Json(new
                        {
                            success = true,
                            shouldCapture = false,
                            checkInMethod = CheckInMethodQuestion,
                            shouldAskQuestion = false,
                            hasOpenLog = latestPeriodLog.CheckOutAt == null,
                            attendanceLogId = latestPeriodLog.Id,
                            message = "لديك تسجيل حضور بسؤال خلال وقت الدوام الحالي"
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(latestPeriodLog.FaceImagePath))
                    {
                        return Json(new
                        {
                            success = true,
                            shouldCapture = false,
                            checkInMethod = checkInMethod,
                            shouldAskQuestion = false,
                            hasOpenLog = latestPeriodLog.CheckOutAt == null,
                            attendanceLogId = latestPeriodLog.Id,
                            message = "لديك تسجيل حضور مسجل بالفعل خلال وقت الدوام الحالي"
                        });
                    }
                }

                if (checkInMethod == CheckInMethodQuestion)
                {
                    return Json(new
                    {
                        success = true,
                        shouldCapture = false,
                        checkInMethod = CheckInMethodQuestion,
                        shouldAskQuestion = true,
                        hasOpenLog = latestPeriodLog != null && latestPeriodLog.CheckOutAt == null,
                        message = "يجب تأكيد سؤال بدء الدوام"
                    });
                }

                return Json(new
                {
                    success = true,
                    shouldCapture = true,
                    checkInMethod = CheckInMethodPhoto,
                    shouldAskQuestion = false,
                    hasOpenLog = latestPeriodLog != null && latestPeriodLog.CheckOutAt == null,
                    message = "يجب التقاط صورة الحضور"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    shouldCapture = true,
                    checkInMethod = CheckInMethodPhoto,
                    shouldAskQuestion = false,
                    message = ex.Message
                });
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterCheckIn([FromBody] SecureCheckInRequest request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Json(new
                    {
                        success = false,
                        message = "لم يتم العثور على المستخدم الحالي"
                    });
                }

                if (ShouldSkipCheckInVerificationForCurrentUser())
                {
                    return Json(new
                    {
                        success = true,
                        alreadyCheckedIn = true,
                        skipFaceCapture = true,
                        message = "هذا الدور مستثنى من تسجيل الحضور بالصورة"
                    });
                }

                var checkInAt = GetEgyptNow();

                var employeeInfo = await _context.Employees
                    .AsNoTracking()
                    .Where(e => e.ApplicationUserId == userId)
                    .Select(e => new EmployeeLoginInfo
                    {
                        EmployeeId = e.Id,
                        Name = e.Name,
                        DisplayName = e.DisplayName,
                        Salary = e.Salary
                    })
                    .FirstOrDefaultAsync();

                if (employeeInfo == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "هذا المستخدم غير مربوط بموظف"
                    });
                }

                var employeeName = !string.IsNullOrWhiteSpace(employeeInfo.DisplayName)
                    ? employeeInfo.DisplayName.Trim()
                    : !string.IsNullOrWhiteSpace(employeeInfo.Name)
                        ? employeeInfo.Name.Trim()
                        : "بدون اسم";

                var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "";

                // منع تكرار تسجيل الحضور في نفس اليوم:
                // لو فيه سجل حضور حقيقي لنفس الموظف اليوم، نرجع نفس السجل ولا ننشئ سجل جديد ولا نغير وقت الدخول.
                var firstTodayAttendanceLog = await FindFirstRealAttendanceLogForEmployeeOnDateAsync(
                    userId,
                    employeeInfo.EmployeeId,
                    checkInAt,
                    asNoTracking: false);

                if (HasCompletedCheckInVerification(firstTodayAttendanceLog))
                {
                    return Json(new
                    {
                        success = true,
                        alreadyCheckedIn = true,
                        skipFaceCapture = true,
                        attendanceLogId = firstTodayAttendanceLog.Id,
                        firstCheckInAt = firstTodayAttendanceLog.CheckInAt.ToString("yyyy/MM/dd HH:mm"),
                        message = "لديك تسجيل حضور مسجل بالفعل اليوم. تم اعتماد أول وقت دخول فقط"
                    });
                }

                var shift = await GetCurrentEmployeeShiftLoginLookupAsync(employeeInfo.EmployeeId, checkInAt);

                var latestPeriodLog = await FindLatestAttendanceLogInCurrentWorkPeriodAsync(
                    userId,
                    employeeInfo.EmployeeId,
                    checkInAt,
                    shift == null ? (TimeSpan?)null : shift.ShiftStartTime,
                    shift == null ? (TimeSpan?)null : shift.ShiftEndTime,
                    asNoTracking: false);

                if (latestPeriodLog != null &&
                    !string.IsNullOrWhiteSpace(latestPeriodLog.FaceImagePath))
                {
                    var shouldSave = false;

                    if (request != null && UpdateAttendanceLogIpIfChanged(
                        latestPeriodLog,
                        request.CheckInIpAddress,
                        request.CheckInLocation,
                        checkInAt))
                    {
                        shouldSave = true;
                    }

                    if (shouldSave)
                    {
                        latestPeriodLog.UpdatedAt = checkInAt;
                        await _context.SaveChangesAsync();
                    }

                    return Json(new
                    {
                        success = true,
                        alreadyCheckedIn = true,
                        skipFaceCapture = true,
                        attendanceLogId = latestPeriodLog.Id,
                        message = "لديك صورة حضور مسجلة بالفعل خلال وقت الدوام الحالي"
                    });
                }

                if (request == null || string.IsNullOrWhiteSpace(request.FaceImageBase64))
                {
                    return Json(new
                    {
                        success = false,
                        message = "لم يتم إرسال صورة الدخول"
                    });
                }

                var faceImagePath = await SaveCheckInFaceImageAsync(userId, request.FaceImageBase64);

                var completedLogDeductionAmount = 0m;
                var completedLogDeductionReason = "لا يوجد خصم";
                var shouldReplaceClearedCheckInTime = latestPeriodLog != null && string.IsNullOrWhiteSpace(latestPeriodLog.FaceImagePath);
                var effectiveCheckInAtForDeduction = shouldReplaceClearedCheckInTime
                    ? checkInAt
                    : (latestPeriodLog?.CheckInAt ?? checkInAt);

                if (shift == null)
                {
                    completedLogDeductionReason = "لم يتم تعيين شيفت لهذا الموظف";
                }
                else
                {
                    var completedLogDeductionResult = CalculateLateDeduction(
                        effectiveCheckInAtForDeduction,
                        shift.ShiftStartTime,
                        shift.ShiftEndTime,
                        employeeInfo.Salary);

                    completedLogDeductionAmount = completedLogDeductionResult.DeductionAmount;
                    completedLogDeductionReason = completedLogDeductionResult.Reason;
                }

                if (latestPeriodLog != null)
                {
                    if (shouldReplaceClearedCheckInTime)
                    {
                        latestPeriodLog.CheckInAt = checkInAt;
                    }

                    latestPeriodLog.FaceImagePath = faceImagePath;
                    if (IsManualClearedCheckInNote(latestPeriodLog.Notes))
                    {
                        latestPeriodLog.CheckInAt = checkInAt;
                        latestPeriodLog.Notes = RemoveManualClearedCheckInMarker(latestPeriodLog.Notes);
                    }

                    latestPeriodLog.EmployeeEmail = string.IsNullOrWhiteSpace(latestPeriodLog.EmployeeEmail)
                        ? userEmail
                        : latestPeriodLog.EmployeeEmail;
                    latestPeriodLog.EmployeeName = string.IsNullOrWhiteSpace(latestPeriodLog.EmployeeName)
                        ? employeeName
                        : latestPeriodLog.EmployeeName;
                    UpdateAttendanceLogIpIfChanged(
                        latestPeriodLog,
                        request.CheckInIpAddress,
                        request.CheckInLocation,
                        checkInAt);

                    if (!latestPeriodLog.DeductionAmount.HasValue || latestPeriodLog.DeductionAmount.Value <= 0m)
                    {
                        latestPeriodLog.DeductionAmount = completedLogDeductionAmount;
                    }

                    if (string.IsNullOrWhiteSpace(latestPeriodLog.DeductionReason) ||
                        (completedLogDeductionAmount > 0m && IsNoDeductionReason(latestPeriodLog.DeductionReason)))
                    {
                        latestPeriodLog.DeductionReason = completedLogDeductionReason;
                    }

                    latestPeriodLog.UpdatedAt = checkInAt;

                    await _context.SaveChangesAsync();

                    if (shift != null)
                    {
                        var shiftWindow = BuildAttendanceShiftWindowForTime(latestPeriodLog.CheckInAt, shift.ShiftStartTime, shift.ShiftEndTime);
                        await SaveAttendanceShiftSnapshotAsync(latestPeriodLog.Id, shift.Id, shiftWindow.ShiftStart, shiftWindow.ShiftEnd);
                    }

                    return Json(new
                    {
                        success = true,
                        alreadyCheckedIn = true,
                        attendanceLogId = latestPeriodLog.Id,
                        message = "تم استكمال صورة الحضور لسجل الدوام الحالي"
                    });
                }

                var deductionAmount = 0m;
                var deductionReason = "لا يوجد خصم";

                if (shift == null)
                {
                    deductionReason = "لم يتم تعيين شيفت لهذا الموظف";
                }
                else
                {
                    var deductionResult = CalculateLateDeduction(
                        checkInAt,
                        shift.ShiftStartTime,
                        shift.ShiftEndTime,
                        employeeInfo.Salary);

                    deductionAmount = deductionResult.DeductionAmount;
                    deductionReason = deductionResult.Reason;
                }

                var attendanceLog = new EmployeeAttendanceLog
                {
                    UserId = userId,
                    EmployeeId = employeeInfo.EmployeeId,
                    EmployeeEmail = userEmail,
                    EmployeeName = employeeName,
                    CheckInAt = checkInAt,
                    FaceImagePath = faceImagePath,
                    CheckInIpAddress = string.IsNullOrWhiteSpace(request.CheckInIpAddress)
                        ? ""
                        : request.CheckInIpAddress.Trim(),
                    CheckInLocation = string.IsNullOrWhiteSpace(request.CheckInLocation)
                        ? ""
                        : request.CheckInLocation.Trim(),
                    DeductionAmount = deductionAmount,
                    DeductionReason = deductionReason,
                    Notes = "",
                    CreatedAt = checkInAt
                };

                _context.EmployeeAttendanceLogs.Add(attendanceLog);

                if (deductionAmount > 0)
                {
                    var employeeTransaction = new EmployeeTransaction
                    {
                        Amount = deductionAmount,
                        TransactionType = (TransactionTypeEnum)0,
                        Reason = deductionReason,
                        Date = checkInAt,
                        EmployeeId = employeeInfo.EmployeeId
                    };

                    _context.EmployeeTransactions.Add(employeeTransaction);
                }

                await _context.SaveChangesAsync();

                if (shift != null)
                {
                    var shiftWindow = BuildAttendanceShiftWindowForTime(checkInAt, shift.ShiftStartTime, shift.ShiftEndTime);
                    await SaveAttendanceShiftSnapshotAsync(attendanceLog.Id, shift.Id, shiftWindow.ShiftStart, shiftWindow.ShiftEnd);
                }

                return Json(new
                {
                    success = true,
                    attendanceLogId = attendanceLog.Id,
                    message = "تم التقاط صورة الحضور بنجاح"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterQuestionCheckIn([FromBody] SecureQuestionCheckInRequest request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Json(new
                    {
                        success = false,
                        message = "لم يتم العثور على المستخدم الحالي"
                    });
                }

                if (ShouldSkipCheckInVerificationForCurrentUser())
                {
                    return Json(new
                    {
                        success = true,
                        alreadyCheckedIn = true,
                        message = "هذا الدور مستثنى من تسجيل الحضور بالسؤال"
                    });
                }

                var checkInAt = GetEgyptNow();

                var employeeInfo = await _context.Employees
                    .AsNoTracking()
                    .Where(e => e.ApplicationUserId == userId)
                    .Select(e => new EmployeeLoginInfo
                    {
                        EmployeeId = e.Id,
                        Name = e.Name,
                        DisplayName = e.DisplayName,
                        Salary = e.Salary
                    })
                    .FirstOrDefaultAsync();

                if (employeeInfo == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "هذا المستخدم غير مربوط بموظف"
                    });
                }

                var checkInMethod = await GetEmployeeCheckInVerificationMethodAsync(employeeInfo.EmployeeId);

                if (checkInMethod != CheckInMethodQuestion)
                {
                    return Json(new
                    {
                        success = false,
                        message = "طريقة تسجيل الحضور لهذا الموظف ليست بالسؤال"
                    });
                }

                if (request == null || !request.IsReady)
                {
                    return Json(new
                    {
                        success = false,
                        message = "يجب تأكيد جاهزية بدء الدوام"
                    });
                }

                var employeeName = !string.IsNullOrWhiteSpace(employeeInfo.DisplayName)
                    ? employeeInfo.DisplayName.Trim()
                    : !string.IsNullOrWhiteSpace(employeeInfo.Name)
                        ? employeeInfo.Name.Trim()
                        : "بدون اسم";

                var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "";

                // منع تكرار تسجيل الحضور بالسؤال في نفس اليوم:
                // لو فيه سجل حضور حقيقي لنفس الموظف اليوم، نرجع نفس السجل ولا ننشئ سجل جديد ولا نغير وقت الدخول.
                var firstTodayAttendanceLog = await FindFirstRealAttendanceLogForEmployeeOnDateAsync(
                    userId,
                    employeeInfo.EmployeeId,
                    checkInAt,
                    asNoTracking: false);

                if (HasCompletedCheckInVerification(firstTodayAttendanceLog))
                {
                    return Json(new
                    {
                        success = true,
                        alreadyCheckedIn = true,
                        skipFaceCapture = true,
                        attendanceLogId = firstTodayAttendanceLog.Id,
                        firstCheckInAt = firstTodayAttendanceLog.CheckInAt.ToString("yyyy/MM/dd HH:mm"),
                        message = "لديك تسجيل حضور مسجل بالفعل اليوم. تم اعتماد أول وقت دخول فقط"
                    });
                }

                var shift = await GetCurrentEmployeeShiftLoginLookupAsync(employeeInfo.EmployeeId, checkInAt);

                var latestPeriodLog = await FindLatestAttendanceLogInCurrentWorkPeriodAsync(
                    userId,
                    employeeInfo.EmployeeId,
                    checkInAt,
                    shift == null ? (TimeSpan?)null : shift.ShiftStartTime,
                    shift == null ? (TimeSpan?)null : shift.ShiftEndTime,
                    asNoTracking: false);

                var employeeImagePath = await GetEmployeeSavedImagePathAsync(employeeInfo.EmployeeId);

                if (latestPeriodLog != null &&
                    (IsQuestionCheckInLog(latestPeriodLog) || !string.IsNullOrWhiteSpace(latestPeriodLog.FaceImagePath)))
                {
                    var shouldSave = false;

                    if (UpdateAttendanceLogIpIfChanged(
                        latestPeriodLog,
                        request.CheckInIpAddress,
                        request.CheckInLocation,
                        checkInAt))
                    {
                        shouldSave = true;
                    }

                    if (string.IsNullOrWhiteSpace(latestPeriodLog.FaceImagePath))
                    {
                        latestPeriodLog.FaceImagePath = employeeImagePath;
                        shouldSave = true;
                    }

                    if (shouldSave)
                    {
                        latestPeriodLog.UpdatedAt = checkInAt;
                        await _context.SaveChangesAsync();
                    }

                    return Json(new
                    {
                        success = true,
                        alreadyCheckedIn = true,
                        skipFaceCapture = true,
                        attendanceLogId = latestPeriodLog.Id,
                        message = "لديك تسجيل حضور بسؤال خلال وقت الدوام الحالي"
                    });
                }

                var deductionAmount = 0m;
                var deductionReason = "لا يوجد خصم";

                if (shift == null)
                {
                    deductionReason = "لم يتم تعيين شيفت لهذا الموظف";
                }
                else
                {
                    var deductionResult = CalculateLateDeduction(
                        checkInAt,
                        shift.ShiftStartTime,
                        shift.ShiftEndTime,
                        employeeInfo.Salary);

                    deductionAmount = deductionResult.DeductionAmount;
                    deductionReason = deductionResult.Reason;
                }

                if (latestPeriodLog != null)
                {
                    latestPeriodLog.FaceImagePath = employeeImagePath;
                    if (IsManualClearedCheckInNote(latestPeriodLog.Notes))
                    {
                        latestPeriodLog.CheckInAt = checkInAt;
                        latestPeriodLog.Notes = RemoveManualClearedCheckInMarker(latestPeriodLog.Notes);
                    }

                    latestPeriodLog.EmployeeEmail = string.IsNullOrWhiteSpace(latestPeriodLog.EmployeeEmail)
                        ? userEmail
                        : latestPeriodLog.EmployeeEmail;
                    latestPeriodLog.EmployeeName = string.IsNullOrWhiteSpace(latestPeriodLog.EmployeeName)
                        ? employeeName
                        : latestPeriodLog.EmployeeName;
                    UpdateAttendanceLogIpIfChanged(
                        latestPeriodLog,
                        request.CheckInIpAddress,
                        request.CheckInLocation,
                        checkInAt);
                    if (!latestPeriodLog.DeductionAmount.HasValue || latestPeriodLog.DeductionAmount.Value <= 0m)
                    {
                        latestPeriodLog.DeductionAmount = deductionAmount;
                    }

                    if (string.IsNullOrWhiteSpace(latestPeriodLog.DeductionReason) ||
                        (deductionAmount > 0m && IsNoDeductionReason(latestPeriodLog.DeductionReason)))
                    {
                        latestPeriodLog.DeductionReason = deductionReason;
                    }
                    latestPeriodLog.Notes = EnsureQuestionCheckInNote(latestPeriodLog.Notes);
                    latestPeriodLog.UpdatedAt = checkInAt;

                    await _context.SaveChangesAsync();

                    if (shift != null)
                    {
                        var shiftWindow = BuildAttendanceShiftWindowForTime(latestPeriodLog.CheckInAt, shift.ShiftStartTime, shift.ShiftEndTime);
                        await SaveAttendanceShiftSnapshotAsync(latestPeriodLog.Id, shift.Id, shiftWindow.ShiftStart, shiftWindow.ShiftEnd);
                    }

                    return Json(new
                    {
                        success = true,
                        attendanceLogId = latestPeriodLog.Id,
                        message = "تم تسجيل الحضور بالسؤال بنجاح"
                    });
                }

                var attendanceLog = new EmployeeAttendanceLog
                {
                    UserId = userId,
                    EmployeeId = employeeInfo.EmployeeId,
                    EmployeeEmail = userEmail,
                    EmployeeName = employeeName,
                    CheckInAt = checkInAt,
                    FaceImagePath = employeeImagePath,
                    CheckInIpAddress = string.IsNullOrWhiteSpace(request.CheckInIpAddress)
                        ? ""
                        : request.CheckInIpAddress.Trim(),
                    CheckInLocation = string.IsNullOrWhiteSpace(request.CheckInLocation)
                        ? ""
                        : request.CheckInLocation.Trim(),
                    DeductionAmount = deductionAmount,
                    DeductionReason = deductionReason,
                    Notes = QuestionCheckInNoteText,
                    CreatedAt = checkInAt
                };

                _context.EmployeeAttendanceLogs.Add(attendanceLog);

                if (deductionAmount > 0)
                {
                    var employeeTransaction = new EmployeeTransaction
                    {
                        Amount = deductionAmount,
                        TransactionType = (TransactionTypeEnum)0,
                        Reason = deductionReason,
                        Date = checkInAt,
                        EmployeeId = employeeInfo.EmployeeId
                    };

                    _context.EmployeeTransactions.Add(employeeTransaction);
                }

                await _context.SaveChangesAsync();

                if (shift != null)
                {
                    var shiftWindow = BuildAttendanceShiftWindowForTime(checkInAt, shift.ShiftStartTime, shift.ShiftEndTime);
                    await SaveAttendanceShiftSnapshotAsync(attendanceLog.Id, shift.Id, shiftWindow.ShiftStart, shiftWindow.ShiftEnd);
                }

                return Json(new
                {
                    success = true,
                    attendanceLogId = attendanceLog.Id,
                    message = "تم تسجيل الحضور بالسؤال بنجاح"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterCheckOut([FromBody] SecureLogoutRequest request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Json(new
                    {
                        success = false,
                        message = "لم يتم العثور على المستخدم الحالي"
                    });
                }

                var checkOutNow = GetEgyptNow();

                var employeeInfo = await _context.Employees
                    .AsNoTracking()
                    .Where(e => e.ApplicationUserId == userId)
                    .Select(e => new EmployeeLoginInfo
                    {
                        EmployeeId = e.Id,
                        Name = e.Name,
                        DisplayName = e.DisplayName,
                        Salary = e.Salary
                    })
                    .FirstOrDefaultAsync();

                if (employeeInfo == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "هذا المستخدم غير مربوط بموظف"
                    });
                }

                var shift = await GetCurrentEmployeeShiftLoginLookupAsync(employeeInfo.EmployeeId, checkOutNow);

                var attendanceLog = await FindLatestAttendanceLogInCurrentWorkPeriodAsync(
                    userId,
                    employeeInfo.EmployeeId,
                    checkOutNow,
                    shift == null ? (TimeSpan?)null : shift.ShiftStartTime,
                    shift == null ? (TimeSpan?)null : shift.ShiftEndTime,
                    asNoTracking: false);

                if (attendanceLog == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "لا يوجد سجل حضور لهذا المستخدم خلال وقت الدوام الحالي"
                    });
                }

                if (attendanceLog.CheckOutAt.HasValue &&
                    !string.IsNullOrWhiteSpace(attendanceLog.CheckOutFaceImagePath))
                {
                    return Json(new
                    {
                        success = true,
                        alreadyCheckedOut = true,
                        message = "تم تسجيل الخروج مسبقًا خلال وقت الدوام الحالي"
                    });
                }

                if (request == null || string.IsNullOrWhiteSpace(request.FaceImageBase64))
                {
                    return Json(new
                    {
                        success = false,
                        message = "لم يتم إرسال صورة الخروج"
                    });
                }

                var checkOutImagePath = await SaveCheckOutFaceImageAsync(userId, request.FaceImageBase64);

                attendanceLog.CheckOutAt = attendanceLog.CheckOutAt ?? checkOutNow;
                attendanceLog.CheckOutFaceImagePath = checkOutImagePath;
                attendanceLog.CheckOutIpAddress = string.IsNullOrWhiteSpace(request.CheckOutIpAddress)
                    ? attendanceLog.CheckOutIpAddress ?? ""
                    : request.CheckOutIpAddress.Trim();
                attendanceLog.CheckOutLocation = string.IsNullOrWhiteSpace(request.CheckOutLocation)
                    ? attendanceLog.CheckOutLocation ?? ""
                    : request.CheckOutLocation.Trim();

                var checkoutSnapshot = await GetAttendanceShiftSnapshotAsync(attendanceLog.Id);

                if (checkoutSnapshot != null && checkoutSnapshot.ShiftStartAt.HasValue && checkoutSnapshot.ShiftEndAt.HasValue)
                {
                    ApplyEarlyCheckOutDeductionFromSnapshot(
                        attendanceLog,
                        employeeInfo.EmployeeId,
                        employeeInfo.Salary,
                        checkoutSnapshot.ShiftStartAt.Value,
                        checkoutSnapshot.ShiftEndAt.Value,
                        checkOutNow,
                        createTransaction: true);
                }
                else if (shift != null)
                {
                    ApplyEarlyCheckOutDeduction(
                        attendanceLog,
                        employeeInfo.EmployeeId,
                        employeeInfo.Salary,
                        shift.ShiftStartTime,
                        shift.ShiftEndTime,
                        checkOutNow,
                        createTransaction: true);
                }

                attendanceLog.UpdatedAt = checkOutNow;

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "تم تسجيل الخروج بنجاح"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }



        [HttpGet]
        [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
        public async Task<IActionResult> GetAttendanceLogCurrencies(string ids)
        {
            var logIds = ParseIds(ids);

            if (logIds.Count == 0)
            {
                return Json(new
                {
                    success = true,
                    currencies = new Dictionary<int, string>()
                });
            }

            var rows = await _context.EmployeeAttendanceLogs
                .AsNoTracking()
                .Where(x => logIds.Contains(x.Id))
                .Select(x => new
                {
                    x.Id,
                    x.EmployeeId,
                    Country = x.EmployeeId.HasValue
                        ? _context.Employees
                            .Where(e => e.Id == x.EmployeeId.Value)
                            .Select(e => e.Country)
                            .FirstOrDefault()
                        : "",
                    Nationality = x.EmployeeId.HasValue
                        ? _context.Employees
                            .Where(e => e.Id == x.EmployeeId.Value)
                            .Select(e => e.Nationality)
                            .FirstOrDefault()
                        : ""
                })
                .ToListAsync();

            var currencies = rows.ToDictionary(
                row => row.Id,
                row => GetCurrencyByEmployeeCountry(string.IsNullOrWhiteSpace(row.Country) ? row.Nationality : row.Country));

            return Json(new
            {
                success = true,
                currencies
            });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
        public async Task<IActionResult> UpdateAttendanceLogRow([FromBody] UpdateAttendanceLogRowRequest request)
        {
            try
            {
                if (request == null || request.Id <= 0)
                {
                    return Json(new { success = false, message = "رقم السجل غير صحيح" });
                }

                var log = await _context.EmployeeAttendanceLogs
                    .FirstOrDefaultAsync(x => x.Id == request.Id);

                if (log == null)
                {
                    return Json(new { success = false, message = "السجل غير موجود" });
                }

                var shouldClearCheckInTime = request.ClearCheckInTime || string.IsNullOrWhiteSpace(request.CheckInTime);
                var shouldClearCheckOutTime = request.ClearCheckOutTime || string.IsNullOrWhiteSpace(request.CheckOutTime);

                TimeSpan? checkInTime = null;

                if (!shouldClearCheckInTime)
                {
                    if (!TimeSpan.TryParse(request.CheckInTime, out var parsedCheckInTime))
                    {
                        return Json(new { success = false, message = "وقت الدخول غير صحيح" });
                    }

                    checkInTime = parsedCheckInTime;
                }

                TimeSpan? checkOutTime = null;

                if (!shouldClearCheckOutTime)
                {
                    if (!TimeSpan.TryParse(request.CheckOutTime, out var parsedCheckOutTime))
                    {
                        return Json(new { success = false, message = "وقت الخروج غير صحيح" });
                    }

                    checkOutTime = parsedCheckOutTime;
                }

                TimeSpan? requestedShiftStartTime = null;
                TimeSpan? requestedShiftEndTime = null;

                if (!string.IsNullOrWhiteSpace(request.ShiftStartTime))
                {
                    if (!TimeSpan.TryParse(request.ShiftStartTime, out var parsedShiftStartTime))
                    {
                        return Json(new { success = false, message = "وقت بداية الدوام غير صحيح" });
                    }

                    requestedShiftStartTime = parsedShiftStartTime;
                }

                if (!string.IsNullOrWhiteSpace(request.ShiftEndTime))
                {
                    if (!TimeSpan.TryParse(request.ShiftEndTime, out var parsedShiftEndTime))
                    {
                        return Json(new { success = false, message = "وقت نهاية الدوام غير صحيح" });
                    }

                    requestedShiftEndTime = parsedShiftEndTime;
                }

                if (requestedShiftStartTime.HasValue != requestedShiftEndTime.HasValue)
                {
                    return Json(new { success = false, message = "يجب إدخال بداية ونهاية الدوام معًا" });
                }

                var now = GetEgyptNow();
                var originalCheckInDate = log.CheckInAt.Date;
                var oldLogDate = log.CheckInAt.Date;

                var oldCheckInTimeText = log.CheckInAt.ToString("HH:mm");
                var oldCheckOutTimeText = log.CheckOutAt.HasValue ? log.CheckOutAt.Value.ToString("HH:mm") : "";
                var oldDeductionAmount = log.DeductionAmount ?? 0m;
                var oldDeductionReason = log.DeductionReason ?? string.Empty;
                var oldAdvanceAmount = await SafeGetDailyTransactionTotalForAttendanceLogAsync(log, (TransactionTypeEnum)2);
                var oldBonusAmount = await SafeGetDailyTransactionTotalForAttendanceLogAsync(log, (TransactionTypeEnum)1);

                var newCheckInAt = shouldClearCheckInTime || !checkInTime.HasValue
                    ? log.CheckInAt
                    : originalCheckInDate.Add(checkInTime.Value);
                DateTime? newCheckOutAt = null;

                if (!shouldClearCheckInTime && checkOutTime.HasValue)
                {
                    var checkOutDate = originalCheckInDate;

                    if (checkInTime.HasValue && checkOutTime.Value < checkInTime.Value)
                    {
                        checkOutDate = originalCheckInDate.AddDays(1);
                    }
                    else if (log.CheckOutAt.HasValue && log.CheckOutAt.Value.Date > originalCheckInDate)
                    {
                        checkOutDate = log.CheckOutAt.Value.Date;
                    }

                    newCheckOutAt = checkOutDate.Add(checkOutTime.Value);
                }

                var shift = await GetAttendanceLogShiftAsync(log);
                var salary = shift?.Salary ?? await GetAttendanceEmployeeSalaryAsync(log.EmployeeId);

                var effectiveShiftStartTime = requestedShiftStartTime ?? shift?.ShiftStartTime;
                var effectiveShiftEndTime = requestedShiftEndTime ?? shift?.ShiftEndTime;

                decimal finalDeductionAmount = request.DeductionAmount < 0 ? 0 : request.DeductionAmount;
                string finalReason = string.IsNullOrWhiteSpace(request.LateReason) ? "" : request.LateReason.Trim();

                // المطلوب: أي تعديل في وقت الدخول/الخروج أو بداية/نهاية دوام اليوم يعيد حساب السبب والخصم تلقائيًا.
                if (shouldClearCheckInTime)
                {
                    finalDeductionAmount = 0m;
                    finalReason = "تم مسح وقت الدخول من الإدارة، وسيتم طلب صورة دخول من الموظف مرة أخرى";
                }
                else if (effectiveShiftStartTime.HasValue && effectiveShiftEndTime.HasValue)
                {
                    var recalculated = CalculateAttendanceManualEditDeduction(
                        newCheckInAt,
                        newCheckOutAt,
                        effectiveShiftStartTime.Value,
                        effectiveShiftEndTime.Value,
                        salary);

                    finalDeductionAmount = recalculated.DeductionAmount < 0 ? 0 : recalculated.DeductionAmount;
                    finalReason = recalculated.Reason;
                }

                if (string.IsNullOrWhiteSpace(finalReason) && finalDeductionAmount > 0m)
                {
                    finalReason = BuildManualDeductionReason(log.Id);
                }

                log.CheckInAt = newCheckInAt;
                log.CheckOutAt = newCheckOutAt;

                if (shouldClearCheckInTime)
                {
                    log.FaceImagePath = "";
                    log.CheckInIpAddress = "";
                    log.CheckInLocation = "";
                    log.CheckOutAt = null;
                    log.CheckOutFaceImagePath = "";
                    log.CheckOutIpAddress = "";
                    log.CheckOutLocation = "";
                    log.Notes = RemoveCheckInVerificationMarkers(log.Notes);
                    log.Notes = AppendAttendanceNoteLine(log.Notes, "ManualClearedCheckIn - تم مسح وقت الدخول من الإدارة ويجب إعادة تسجيل صورة الدخول");
                }
                else if (shouldClearCheckOutTime)
                {
                    log.CheckOutAt = null;
                    log.CheckOutFaceImagePath = "";
                    log.CheckOutIpAddress = "";
                    log.CheckOutLocation = "";
                    log.Notes = AppendAttendanceNoteLine(log.Notes, "ManualClearedCheckOut - تم مسح وقت الخروج من الإدارة ويجب إعادة تسجيل صورة الخروج");
                }

                log.DeductionAmount = finalDeductionAmount;
                log.DeductionReason = finalReason;
                log.UpdatedAt = now;

                // لو كان السجل غياب تلقائي واتعدل يدويًا، نخليه يظهر كسجل معدل مش غياب صناعي فقط.
                if (!shouldClearCheckInTime && IsAutomaticAbsentNote(log.Notes) && !IsAbsentReason(finalReason))
                {
                    var recoveryNote = "QuestionCheckIn - تم تعديل سجل الدوام يدويًا من صفحة تسجيل الدوام";

                    if (string.IsNullOrWhiteSpace(log.Notes))
                    {
                        log.Notes = recoveryNote;
                    }
                    else if (!IsQuestionCheckInNoteText(log.Notes))
                    {
                        log.Notes = log.Notes.Trim() + Environment.NewLine + recoveryNote;
                    }
                }

                // نحفظ دوام اليوم فقط كـ snapshot إن الأعمدة موجودة، وكماركر قصير داخل Notes كـ fallback لو الأعمدة مش موجودة.
                if (effectiveShiftStartTime.HasValue && effectiveShiftEndTime.HasValue)
                {
                    var shiftWindow = BuildAttendanceShiftWindowForTime(
                        log.CheckInAt,
                        effectiveShiftStartTime.Value,
                        effectiveShiftEndTime.Value);

                    await SaveAttendanceShiftSnapshotOverrideAsync(
                        log.Id,
                        shift?.Id,
                        shiftWindow.ShiftStart,
                        shiftWindow.ShiftEnd);

                    log.Notes = AddManualShiftOverrideMarker(
                        log.Notes,
                        effectiveShiftStartTime.Value,
                        effectiveShiftEndTime.Value);
                }

                await _context.SaveChangesAsync();

                await SafeUpdateAttendanceTransactionsAsync(
                    log,
                    oldDeductionAmount,
                    oldDeductionReason,
                    finalDeductionAmount,
                    finalReason,
                    oldLogDate,
                    request.AdvanceAmount < 0 ? 0 : request.AdvanceAmount,
                    request.BonusAmount < 0 ? 0 : request.BonusAmount);

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    recalculatedDeduction = finalDeductionAmount,
                    recalculatedReason = finalReason,
                    message = "تم تعديل السجل وتحديث السبب والخصم تلقائيًا"
                });
            }
            catch (Exception ex)
            {
                Response.StatusCode = 200;
                return Json(new
                {
                    success = false,
                    message = "تعذر تعديل السجل: " + ex.Message
                });
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
        public async Task<IActionResult> AttendanceEditHistory(int id)
        {
            if (id <= 0)
            {
                return Json(new { success = false, message = "رقم السجل غير صحيح" });
            }

            var log = await _context.EmployeeAttendanceLogs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (log == null)
            {
                return Json(new { success = false, message = "السجل غير موجود" });
            }

            var shift = await GetAttendanceLogShiftAsync(log);
            var shiftStartText = shift == null ? "-" : shift.ShiftStartTime.ToString(@"hh\:mm");
            var shiftEndText = shift == null ? "-" : shift.ShiftEndTime.ToString(@"hh\:mm");

            var entries = ParseAttendanceEditHistoryEntries(log.Notes);

            return Json(new
            {
                success = true,
                totalCount = entries.Count,
                shiftStart = shiftStartText,
                shiftEnd = shiftEndText,
                items = entries.Select(entry => new
                {
                    editorName = string.IsNullOrWhiteSpace(entry.EditorName) ? "موظف" : entry.EditorName,
                    changedAt = entry.ChangedAt.ToString("yyyy/MM/dd HH:mm"),
                    date = entry.ChangedAt.ToString("yyyy/MM/dd"),
                    time = entry.ChangedAt.ToString("HH:mm"),
                    shiftStart = string.IsNullOrWhiteSpace(entry.ShiftStartTimeText) ? shiftStartText : entry.ShiftStartTimeText,
                    shiftEnd = string.IsNullOrWhiteSpace(entry.ShiftEndTimeText) ? shiftEndText : entry.ShiftEndTimeText,
                    changesCount = entry.Changes == null ? 0 : entry.Changes.Count,
                    changes = (entry.Changes ?? new List<AttendanceEditHistoryChange>()).Select(change => new
                    {
                        fieldName = change.FieldName,
                        oldValue = change.OldValue,
                        newValue = change.NewValue
                    })
                })
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
        public async Task<IActionResult> DeleteAttendanceLogRow([FromBody] DeleteAttendanceLogRowRequest request)
        {
            if (request == null || request.Id <= 0)
            {
                return Json(new { success = false, message = "رقم السجل غير صحيح" });
            }

            var log = await _context.EmployeeAttendanceLogs
                .FirstOrDefaultAsync(x => x.Id == request.Id);

            if (log == null)
            {
                return Json(new { success = false, message = "السجل غير موجود" });
            }

            log.Notes = AddAttendanceDeletedMarker(log.Notes);
            log.UpdatedAt = GetEgyptNow();

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "تم نقل السجل إلى سلة المحذوفات"
            });
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
        public async Task<IActionResult> DeletedAttendanceLogs()
        {
            var deletedLogsRaw = await _context.EmployeeAttendanceLogs
                .AsNoTracking()
                .Where(x => x.Notes != null && x.Notes.Contains(AttendanceDeletedNoteMarker))
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Take(300)
                .Select(x => new
                {
                    x.Id,
                    x.EmployeeName,
                    x.CheckInAt,
                    x.CheckOutAt,
                    x.DeductionAmount,
                    x.DeductionReason,
                    DeletedAt = x.UpdatedAt ?? x.CreatedAt
                })
                .ToListAsync();

            var deletedLogs = deletedLogsRaw.Select(x => new
            {
                id = x.Id,
                employeeName = string.IsNullOrWhiteSpace(x.EmployeeName) ? "بدون اسم" : x.EmployeeName,
                date = x.CheckInAt.ToString("yyyy/MM/dd"),
                checkInTime = x.CheckInAt.ToString("HH:mm"),
                checkOutTime = x.CheckOutAt.HasValue ? x.CheckOutAt.Value.ToString("HH:mm") : "-",
                deductionAmount = (x.DeductionAmount ?? 0m).ToString("0.00"),
                reason = string.IsNullOrWhiteSpace(x.DeductionReason) ? "-" : x.DeductionReason,
                deletedAt = x.DeletedAt.ToString("yyyy/MM/dd HH:mm")
            }).ToList();

            return Json(new
            {
                success = true,
                items = deletedLogs
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
        public async Task<IActionResult> RestoreAttendanceLogRow([FromBody] DeleteAttendanceLogRowRequest request)
        {
            if (request == null || request.Id <= 0)
            {
                return Json(new { success = false, message = "رقم السجل غير صحيح" });
            }

            var log = await _context.EmployeeAttendanceLogs
                .FirstOrDefaultAsync(x => x.Id == request.Id);

            if (log == null)
            {
                return Json(new { success = false, message = "السجل غير موجود" });
            }

            log.Notes = RemoveAttendanceDeletedMarker(log.Notes);
            log.UpdatedAt = GetEgyptNow();

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "تم استرداد السجل"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
        public async Task<IActionResult> RestoreAllDeletedAttendanceLogs()
        {
            var logs = await _context.EmployeeAttendanceLogs
                .Where(x => x.Notes != null && x.Notes.Contains(AttendanceDeletedNoteMarker))
                .ToListAsync();

            var now = GetEgyptNow();

            foreach (var log in logs)
            {
                log.Notes = RemoveAttendanceDeletedMarker(log.Notes);
                log.UpdatedAt = now;
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                restoredCount = logs.Count,
                message = "تم استرداد كل السجلات المحذوفة"
            });
        }

        private async Task UpdateAttendanceDeductionTransactionForLogAsync(
            EmployeeAttendanceLog log,
            decimal oldDeductionAmount,
            string? oldDeductionReason,
            decimal newDeductionAmount,
            string? newDeductionReason,
            DateTime oldLogDate)
        {
            try
            {
                if (log == null || !log.EmployeeId.HasValue)
                {
                    return;
                }

                var employeeId = log.EmployeeId.Value;
                var manualDeductionReason = BuildManualDeductionReason(log.Id);
                var newReason = string.IsNullOrWhiteSpace(newDeductionReason)
                    ? manualDeductionReason
                    : newDeductionReason.Trim();

                var newAmountRounded = Math.Round(newDeductionAmount < 0 ? 0 : newDeductionAmount, 2);

                var oldDateStart = oldLogDate.Date;
                var oldDateEnd = oldDateStart.AddDays(1);
                var newDateStart = log.CheckInAt.Date;
                var newDateEnd = newDateStart.AddDays(1);

                /*
                    مهم:
                    خصم الحضور في نفس اليوم لازم يبقى حركة واحدة فقط في صفحة مكافآت وخصومات.
                    لو الموظف عنده تأخير دخول + خروج مبكر، بنحفظهم كإجمالي يوم واحد وسبب واحد مدمج.
                */
                var oldDayTransactions = await _context.EmployeeTransactions
                    .Where(t =>
                        !t.IsDeleted &&
                        t.EmployeeId == employeeId &&
                        t.TransactionType == (TransactionTypeEnum)0 &&
                        t.Date >= oldDateStart &&
                        t.Date < oldDateEnd)
                    .OrderBy(t => t.Id)
                    .ToListAsync();

                var newDayTransactions = oldDateStart == newDateStart
                    ? oldDayTransactions
                    : await _context.EmployeeTransactions
                        .Where(t =>
                            !t.IsDeleted &&
                            t.EmployeeId == employeeId &&
                            t.TransactionType == (TransactionTypeEnum)0 &&
                            t.Date >= newDateStart &&
                            t.Date < newDateEnd)
                        .OrderBy(t => t.Id)
                        .ToListAsync();

                if (newAmountRounded <= 0m)
                {
                    foreach (var oldTransaction in oldDayTransactions)
                    {
                        oldTransaction.IsDeleted = true;
                        oldTransaction.DeletedAt = DateTime.Now;
                        oldTransaction.DeletedByUserName = "تحديث تلقائي من سجل الدوام";
                    }

                    if (oldDateStart != newDateStart)
                    {
                        foreach (var newTransaction in newDayTransactions)
                        {
                            newTransaction.IsDeleted = true;
                            newTransaction.DeletedAt = DateTime.Now;
                            newTransaction.DeletedByUserName = "تحديث تلقائي من سجل الدوام";
                        }
                    }

                    return;
                }

                if (oldDateStart != newDateStart)
                {
                    foreach (var oldTransaction in oldDayTransactions)
                    {
                        oldTransaction.IsDeleted = true;
                        oldTransaction.DeletedAt = DateTime.Now;
                        oldTransaction.DeletedByUserName = "تحديث تلقائي من سجل الدوام";
                    }
                }

                var transaction = newDayTransactions.FirstOrDefault();

                if (transaction == null)
                {
                    _context.EmployeeTransactions.Add(new EmployeeTransaction
                    {
                        EmployeeId = employeeId,
                        TransactionType = (TransactionTypeEnum)0,
                        Reason = newReason,
                        Date = log.CheckInAt,
                        Amount = newAmountRounded
                    });

                    return;
                }

                foreach (var duplicate in newDayTransactions.Skip(1))
                {
                    duplicate.IsDeleted = true;
                    duplicate.DeletedAt = DateTime.Now;
                    duplicate.DeletedByUserName = "دمج تلقائي من سجل الدوام";
                }

                transaction.Amount = newAmountRounded;
                transaction.Reason = newReason;
                transaction.Date = log.CheckInAt;
            }
            catch
            {
                return;
            }
        }

        private async Task UpdateManualAdvanceForLogAsync(EmployeeAttendanceLog log, decimal requestedDisplayedAdvanceTotal)
        {
            if (!log.EmployeeId.HasValue)
            {
                return;
            }

            var employeeId = log.EmployeeId.Value;
            var logDate = log.CheckInAt.Date;
            var manualAdvanceReason = BuildManualAdvanceReason(log.Id);

            var manualAdvanceTransactions = await _context.EmployeeTransactions
                .Where(t =>
                    t.EmployeeId == employeeId &&
                    t.TransactionType == (TransactionTypeEnum)2 &&
                    t.Date.Date == logDate &&
                    t.Reason == manualAdvanceReason)
                .OrderBy(t => t.Id)
                .ToListAsync();

            var otherAdvanceTotal = await _context.EmployeeTransactions
                .AsNoTracking()
                .Where(t =>
                    t.EmployeeId == employeeId &&
                    t.TransactionType == (TransactionTypeEnum)2 &&
                    t.Date.Date <= logDate &&
                    !(t.Date.Date == logDate && t.Reason == manualAdvanceReason))
                .SumAsync(t => t.Amount);

            var manualNeededAmount = Math.Round(requestedDisplayedAdvanceTotal - otherAdvanceTotal, 2);

            if (manualAdvanceTransactions.Count > 1)
            {
                _context.EmployeeTransactions.RemoveRange(manualAdvanceTransactions.Skip(1));
            }

            var manualTransaction = manualAdvanceTransactions.FirstOrDefault();

            if (manualNeededAmount <= 0)
            {
                if (manualTransaction != null)
                {
                    _context.EmployeeTransactions.Remove(manualTransaction);
                }

                return;
            }

            if (manualTransaction == null)
            {
                manualTransaction = new EmployeeTransaction
                {
                    EmployeeId = employeeId,
                    TransactionType = (TransactionTypeEnum)2,
                    Reason = manualAdvanceReason,
                    Date = log.CheckInAt,
                    Amount = manualNeededAmount
                };

                _context.EmployeeTransactions.Add(manualTransaction);
                return;
            }

            manualTransaction.Amount = manualNeededAmount;
            manualTransaction.Date = log.CheckInAt;
            manualTransaction.Reason = manualAdvanceReason;
        }

        private async Task UpdateManualBonusForLogAsync(EmployeeAttendanceLog log, decimal requestedDisplayedBonusTotal)
        {
            if (!log.EmployeeId.HasValue)
            {
                return;
            }

            var employeeId = log.EmployeeId.Value;
            var logDate = log.CheckInAt.Date;
            var manualBonusReason = BuildManualBonusReason(log.Id);

            var manualBonusTransactions = await _context.EmployeeTransactions
                .Where(t =>
                    t.EmployeeId == employeeId &&
                    t.TransactionType == (TransactionTypeEnum)1 &&
                    t.Date.Date == logDate &&
                    t.Reason == manualBonusReason)
                .OrderBy(t => t.Id)
                .ToListAsync();

            // قيمة خانة المكافأة في بوب أب التعديل تمثل إجمالي مكافآت نفس اليوم فقط.
            // لذلك نحسب المكافآت الأخرى في نفس اليوم فقط، ولا نخصم مكافآت الأيام السابقة.
            var otherDailyBonusTotal = await _context.EmployeeTransactions
                .AsNoTracking()
                .Where(t =>
                    t.EmployeeId == employeeId &&
                    t.TransactionType == (TransactionTypeEnum)1 &&
                    t.Date.Date == logDate &&
                    t.Reason != manualBonusReason)
                .SumAsync(t => t.Amount);

            var manualNeededAmount = Math.Round(requestedDisplayedBonusTotal - otherDailyBonusTotal, 2);

            if (manualBonusTransactions.Count > 1)
            {
                _context.EmployeeTransactions.RemoveRange(manualBonusTransactions.Skip(1));
            }

            var manualTransaction = manualBonusTransactions.FirstOrDefault();

            if (manualNeededAmount <= 0)
            {
                if (manualTransaction != null)
                {
                    _context.EmployeeTransactions.Remove(manualTransaction);
                }

                return;
            }

            if (manualTransaction == null)
            {
                manualTransaction = new EmployeeTransaction
                {
                    EmployeeId = employeeId,
                    TransactionType = (TransactionTypeEnum)1,
                    Reason = manualBonusReason,
                    Date = log.CheckInAt,
                    Amount = manualNeededAmount
                };

                _context.EmployeeTransactions.Add(manualTransaction);
                return;
            }

            manualTransaction.Amount = manualNeededAmount;
            manualTransaction.Date = log.CheckInAt;
            manualTransaction.Reason = manualBonusReason;
        }

        private static string BuildManualDeductionReason(int logId)
        {
            return $"خصم من تعديل سجل الدوام #{logId}";
        }

        private static string BuildManualAdvanceReason(int logId)
        {
            return $"سلفة من تعديل سجل الدوام #{logId}";
        }

        private static string BuildManualBonusReason(int logId)
        {
            return $"مكافأة من تعديل سجل الدوام #{logId}";
        }

        private static List<int> ParseIds(string ids)
        {
            if (string.IsNullOrWhiteSpace(ids))
            {
                return new List<int>();
            }

            return ids
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x.Trim(), out var id) ? id : 0)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
        }

        private static string GetCurrencyByEmployeeCountry(string? country)
        {
            var text = (country ?? "").Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            if (text.Contains("egypt") || text.Contains("مصر") || text.Contains("مصري") || text.Contains("egp"))
            {
                return "EGP";
            }

            if (text.Contains("turkey") || text.Contains("turkish") || text.Contains("ترك") || text.Contains("try"))
            {
                return "TRY";
            }

            if (text.Contains("iraq") || text.Contains("العراق") || text.Contains("عراقي") || text.Contains("iqd"))
            {
                return "IQD";
            }

            if (text.Contains("jordan") || text.Contains("الأردن") || text.Contains("اردن") || text.Contains("jod"))
            {
                return "JOD";
            }

            if (text.Contains("libya") || text.Contains("ليبيا") || text.Contains("ليبي") || text.Contains("lyd"))
            {
                return "LYD";
            }

            if (text.Contains("kuwait") || text.Contains("الكويت") || text.Contains("kwd"))
            {
                return "KWD";
            }

            if (text.Contains("qatar") || text.Contains("قطر") || text.Contains("qar"))
            {
                return "QAR";
            }

            if (text.Contains("oman") || text.Contains("عمان") || text.Contains("omr"))
            {
                return "OMR";
            }

            if (text.Contains("bahrain") || text.Contains("البحرين") || text.Contains("bhd"))
            {
                return "BHD";
            }

            if (text.Contains("tunisia") || text.Contains("تونس") || text.Contains("tnd"))
            {
                return "TND";
            }

            return "";
        }

        private static string AppendAttendanceNoteLine(string? notes, string line)
        {
            notes = notes ?? string.Empty;
            line = line ?? string.Empty;

            if (string.IsNullOrWhiteSpace(line))
            {
                return notes;
            }

            if (string.IsNullOrWhiteSpace(notes))
            {
                return line;
            }

            return notes.TrimEnd() + "\n" + line;
        }


        private static bool IsManualClearedCheckInNote(string? notes)
        {
            return !string.IsNullOrWhiteSpace(notes) &&
                   notes.Contains("ManualClearedCheckIn", StringComparison.OrdinalIgnoreCase);
        }

        private static string RemoveManualClearedCheckInMarker(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
            {
                return string.Empty;
            }

            var lines = notes
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Split('\n')
                .Where(line => !line.Contains("ManualClearedCheckIn", StringComparison.OrdinalIgnoreCase))
                .Select(line => line.TrimEnd())
                .Where(line => !string.IsNullOrWhiteSpace(line));

            return string.Join(Environment.NewLine, lines).Trim();
        }

        private static string RemoveCheckInVerificationMarkers(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
            {
                return string.Empty;
            }

            var lines = notes
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Split('\n')
                .Where(line =>
                    !line.Contains("QuestionCheckIn", StringComparison.OrdinalIgnoreCase) &&
                    !line.Contains("تسجيل الحضور بسؤال", StringComparison.OrdinalIgnoreCase) &&
                    !line.Contains("جاهز لبدء الدوام", StringComparison.OrdinalIgnoreCase) &&
                    !line.Contains("RecoveredCheckIn", StringComparison.OrdinalIgnoreCase) &&
                    !line.Contains("ManualClearedCheckIn", StringComparison.OrdinalIgnoreCase))
                .Select(line => line.TrimEnd())
                .Where(line => !string.IsNullOrWhiteSpace(line));

            return string.Join(Environment.NewLine, lines).Trim();
        }

        private static string EnsureQuestionCheckInNote(string? notes)
        {
            if (!string.IsNullOrWhiteSpace(notes) && notes.Contains("QuestionCheckIn"))
            {
                return notes;
            }

            return AppendAttendanceNoteLine(notes, QuestionCheckInNoteText);
        }

        private static string BuildIpChangeNoteLine(DateTime changedAt, string oldIp, string newIp)
        {
            return $"{AttendanceIpHistoryNoteMarker}|{changedAt:yyyy-MM-dd HH:mm:ss}|{oldIp}|{newIp}";
        }

        private static bool UpdateAttendanceLogIpIfChanged(EmployeeAttendanceLog attendanceLog, string? newIpAddress, string? newLocation, DateTime changedAt)
        {
            if (attendanceLog == null || string.IsNullOrWhiteSpace(newIpAddress))
            {
                return false;
            }

            var trimmedNewIp = newIpAddress.Trim();
            var currentIp = NormalizeAttendanceIp(attendanceLog.CheckInIpAddress);
            var normalizedNewIp = NormalizeAttendanceIp(trimmedNewIp);
            var hasChanges = false;

            if (string.IsNullOrWhiteSpace(currentIp))
            {
                attendanceLog.CheckInIpAddress = trimmedNewIp;
                hasChanges = true;
            }
            else if (!string.Equals(currentIp, normalizedNewIp, StringComparison.OrdinalIgnoreCase))
            {
                attendanceLog.Notes = AppendAttendanceNoteLine(
                    attendanceLog.Notes,
                    BuildIpChangeNoteLine(changedAt, attendanceLog.CheckInIpAddress ?? string.Empty, trimmedNewIp));

                attendanceLog.CheckInIpAddress = trimmedNewIp;
                hasChanges = true;
            }

            if (!string.IsNullOrWhiteSpace(newLocation) &&
                (string.IsNullOrWhiteSpace(attendanceLog.CheckInLocation) || hasChanges))
            {
                attendanceLog.CheckInLocation = newLocation.Trim();
                hasChanges = true;
            }

            return hasChanges;
        }

        private static List<AttendanceIpHistoryItem> ExtractIpChangeHistoryFromNotes(int logId, int employeeId, string? notes)
        {
            var items = new List<AttendanceIpHistoryItem>();

            if (string.IsNullOrWhiteSpace(notes))
            {
                return items;
            }

            var lines = notes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var text = line.Trim();

                if (!text.StartsWith(AttendanceIpHistoryNoteMarker, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var parts = text.Split('|');

                if (parts.Length < 4)
                {
                    continue;
                }

                if (!DateTime.TryParse(parts[1], out var changedAt))
                {
                    continue;
                }

                var oldIp = parts[2].Trim();
                var newIp = parts[3].Trim();

                items.Add(new AttendanceIpHistoryItem
                {
                    LogId = logId,
                    EmployeeId = employeeId,
                    IpAddress = newIp,
                    CheckInAt = changedAt,
                    Description = $"{changedAt:yyyy/MM/dd HH:mm} - اتغير من {oldIp} إلى {newIp}",
                    IsChangeInsideSameLog = true
                });
            }

            return items;
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

        private static string NormalizeAttendanceIp(string? ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
            {
                return string.Empty;
            }

            return ip.Trim().Replace(" ", string.Empty);
        }

        private async Task ApplyEmployeeImagesAndIpHistoryAsync(List<EmployeeAttendanceLogRow> attendanceLogs)
        {
            if (attendanceLogs == null || !attendanceLogs.Any())
            {
                return;
            }

            var employeeIds = attendanceLogs
                .Where(log => log.EmployeeId.HasValue)
                .Select(log => log.EmployeeId.Value)
                .Distinct()
                .ToList();

            if (!employeeIds.Any())
            {
                return;
            }

            var employeeImages = await _context.Employees
                .AsNoTracking()
                .Where(e => employeeIds.Contains(e.Id))
                .Select(e => new
                {
                    e.Id,
                    ImageUrl = e.ImageUrl == null ? "" : e.ImageUrl
                })
                .ToListAsync();

            var employeeImageMap = employeeImages
                .ToDictionary(e => e.Id, e => NormalizeEmployeeImagePath(e.ImageUrl));

            foreach (var log in attendanceLogs)
            {
                if (log.EmployeeId.HasValue && employeeImageMap.TryGetValue(log.EmployeeId.Value, out var employeeImage))
                {
                    log.EmployeeImagePath = employeeImage;
                }
                else
                {
                    log.EmployeeImagePath = NormalizeEmployeeImagePath(log.FaceImagePath);
                }
            }

            var ipHistory = await _context.EmployeeAttendanceLogs
                .AsNoTracking()
                .Where(log =>
                    log.EmployeeId.HasValue &&
                    employeeIds.Contains(log.EmployeeId.Value) &&
                    log.CheckInIpAddress != null &&
                    log.CheckInIpAddress != "" &&
                    (log.Notes == null || !log.Notes.Contains(AttendanceDeletedNoteMarker)))
                .OrderByDescending(log => log.CheckInAt)
                .ThenByDescending(log => log.Id)
                .Select(log => new AttendanceIpHistoryItem
                {
                    LogId = log.Id,
                    EmployeeId = log.EmployeeId.Value,
                    IpAddress = log.CheckInIpAddress == null ? "" : log.CheckInIpAddress,
                    CheckInAt = log.CheckInAt,
                    Notes = log.Notes == null ? "" : log.Notes
                })
                .Take(3000)
                .ToListAsync();

            foreach (var item in ipHistory)
            {
                item.Description = $"{item.CheckInAt:yyyy/MM/dd HH:mm} - IP: {item.IpAddress}";
            }

            var expandedIpHistory = new List<AttendanceIpHistoryItem>();
            expandedIpHistory.AddRange(ipHistory);

            foreach (var item in ipHistory)
            {
                expandedIpHistory.AddRange(ExtractIpChangeHistoryFromNotes(
                    item.LogId,
                    item.EmployeeId,
                    item.Notes));
            }

            var ipHistoryByEmployee = expandedIpHistory
                .GroupBy(item => item.EmployeeId)
                .ToDictionary(group => group.Key, group => group.ToList());

            foreach (var log in attendanceLogs)
            {
                if (!log.EmployeeId.HasValue || string.IsNullOrWhiteSpace(log.CheckInIpAddress))
                {
                    log.IsCheckInIpChanged = false;
                    log.CheckInIpHistoryTooltip = "";
                    continue;
                }

                if (!ipHistoryByEmployee.TryGetValue(log.EmployeeId.Value, out var employeeIpHistory) || !employeeIpHistory.Any())
                {
                    log.IsCheckInIpChanged = false;
                    log.CheckInIpHistoryTooltip = "";
                    continue;
                }

                var currentIp = NormalizeAttendanceIp(log.CheckInIpAddress);
                var sameLogChangeItems = ExtractIpChangeHistoryFromNotes(
                    log.Id,
                    log.EmployeeId.Value,
                    log.Notes);

                var hasIpChangedInsideCurrentDay = sameLogChangeItems
                    .Any(item => item.CheckInAt.Date == log.CheckInAt.Date);

                var previousIpLog = employeeIpHistory
                    .Where(item => item.LogId != log.Id && item.CheckInAt < log.CheckInAt && !string.IsNullOrWhiteSpace(item.IpAddress))
                    .OrderByDescending(item => item.CheckInAt)
                    .ThenByDescending(item => item.LogId)
                    .FirstOrDefault();

                var previousIp = NormalizeAttendanceIp(previousIpLog?.IpAddress);
                var changedFromPreviousLog = !string.IsNullOrWhiteSpace(currentIp) &&
                    !string.IsNullOrWhiteSpace(previousIp) &&
                    !string.Equals(currentIp, previousIp, StringComparison.OrdinalIgnoreCase);

                var distinctEmployeeIps = employeeIpHistory
                    .Select(item => NormalizeAttendanceIp(item.IpAddress))
                    .Where(ip => !string.IsNullOrWhiteSpace(ip))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                /*
                    مهم: أحيانًا السجل المعروض يكون أحدث IP محفوظ بعد تحديث نفس اليوم،
                    وبالتالي المقارنة مع السجل السابق فقط لا تكفي.
                    طالما لنفس الموظف يوجد أكثر من IP مختلف في التاريخ، نلون الـ IP المعروض بالأحمر.
                */
                var hasAnyDifferentIpInEmployeeHistory = distinctEmployeeIps.Count > 1;

                log.IsCheckInIpChanged = hasIpChangedInsideCurrentDay
                    || changedFromPreviousLog
                    || hasAnyDifferentIpInEmployeeHistory;

                var historyLines = employeeIpHistory
                    .Where(item => !string.IsNullOrWhiteSpace(item.IpAddress))
                    .OrderByDescending(item => item.CheckInAt)
                    .ThenByDescending(item => item.LogId)
                    .Take(15)
                    .Select(item => string.IsNullOrWhiteSpace(item.Description)
                        ? $"{item.CheckInAt:yyyy/MM/dd HH:mm} - IP: {item.IpAddress}"
                        : item.Description)
                    .ToList();

                if (hasIpChangedInsideCurrentDay)
                {
                    historyLines.Insert(0, "IP اتغير خلال نفس اليوم");
                }
                else if (changedFromPreviousLog && previousIpLog != null)
                {
                    historyLines.Insert(0, $"IP متغير عن آخر دخول سابق: {previousIpLog.IpAddress}");
                }
                else if (hasAnyDifferentIpInEmployeeHistory)
                {
                    historyLines.Insert(0, "يوجد أكثر من IP مختلف لهذا الموظف");
                }

                log.CheckInIpHistoryTooltip = string.Join("\n", historyLines);
            }
        }

        private class AttendanceIpHistoryItem
        {
            public int LogId { get; set; }

            public int EmployeeId { get; set; }

            public string IpAddress { get; set; } = "";

            public DateTime CheckInAt { get; set; }

            public string Notes { get; set; } = "";

            public string Description { get; set; } = "";

            public bool IsChangeInsideSameLog { get; set; }
        }



        private sealed class AttendanceShiftSnapshot
        {
            public int LogId { get; set; }

            public int? ShiftId { get; set; }

            public DateTime? ShiftStartAt { get; set; }

            public DateTime? ShiftEndAt { get; set; }
        }

        private async Task<bool> AttendanceShiftSnapshotColumnsExistAsync()
        {
            try
            {
                var connection = _context.Database.GetDbConnection();
                var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;

                if (shouldCloseConnection)
                {
                    await connection.OpenAsync();
                }

                try
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = @"
SELECT CASE
    WHEN COL_LENGTH('dbo.EmployeeAttendanceLogs', 'ShiftId') IS NOT NULL
     AND COL_LENGTH('dbo.EmployeeAttendanceLogs', 'ShiftStartAt') IS NOT NULL
     AND COL_LENGTH('dbo.EmployeeAttendanceLogs', 'ShiftEndAt') IS NOT NULL
    THEN CAST(1 AS BIT)
    ELSE CAST(0 AS BIT)
END";

                    var result = await command.ExecuteScalarAsync();
                    return result != null && result != DBNull.Value && Convert.ToBoolean(result);
                }
                finally
                {
                    if (shouldCloseConnection)
                    {
                        await connection.CloseAsync();
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task SaveAttendanceShiftSnapshotAsync(int logId, int? shiftId, DateTime? shiftStartAt, DateTime? shiftEndAt)
        {
            try
            {
                if (logId <= 0 || !shiftStartAt.HasValue || !shiftEndAt.HasValue)
                {
                    return;
                }

                if (!await AttendanceShiftSnapshotColumnsExistAsync())
                {
                    return;
                }

                var connection = _context.Database.GetDbConnection();
                var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;

                if (shouldCloseConnection)
                {
                    await connection.OpenAsync();
                }

                try
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = @"
UPDATE dbo.EmployeeAttendanceLogs
SET ShiftId = @ShiftId,
    ShiftStartAt = @ShiftStartAt,
    ShiftEndAt = @ShiftEndAt
WHERE Id = @LogId
  AND (ShiftStartAt IS NULL OR ShiftEndAt IS NULL);";

                    var logIdParameter = command.CreateParameter();
                    logIdParameter.ParameterName = "@LogId";
                    logIdParameter.Value = logId;
                    command.Parameters.Add(logIdParameter);

                    var shiftIdParameter = command.CreateParameter();
                    shiftIdParameter.ParameterName = "@ShiftId";
                    shiftIdParameter.Value = shiftId.HasValue ? shiftId.Value : DBNull.Value;
                    command.Parameters.Add(shiftIdParameter);

                    var shiftStartParameter = command.CreateParameter();
                    shiftStartParameter.ParameterName = "@ShiftStartAt";
                    shiftStartParameter.Value = shiftStartAt.Value;
                    command.Parameters.Add(shiftStartParameter);

                    var shiftEndParameter = command.CreateParameter();
                    shiftEndParameter.ParameterName = "@ShiftEndAt";
                    shiftEndParameter.Value = shiftEndAt.Value;
                    command.Parameters.Add(shiftEndParameter);

                    await command.ExecuteNonQueryAsync();
                }
                finally
                {
                    if (shouldCloseConnection)
                    {
                        await connection.CloseAsync();
                    }
                }
            }
            catch
            {
                return;
            }
        }

        private async Task SaveAttendanceShiftSnapshotOverrideAsync(int logId, int? shiftId, DateTime? shiftStartAt, DateTime? shiftEndAt)
        {
            try
            {
                if (logId <= 0 || !shiftStartAt.HasValue || !shiftEndAt.HasValue)
                {
                    return;
                }

                if (!await AttendanceShiftSnapshotColumnsExistAsync())
                {
                    return;
                }

                var connection = _context.Database.GetDbConnection();
                var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;

                if (shouldCloseConnection)
                {
                    await connection.OpenAsync();
                }

                try
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = @"
UPDATE dbo.EmployeeAttendanceLogs
SET ShiftId = @ShiftId,
    ShiftStartAt = @ShiftStartAt,
    ShiftEndAt = @ShiftEndAt
WHERE Id = @LogId;";

                    var logIdParameter = command.CreateParameter();
                    logIdParameter.ParameterName = "@LogId";
                    logIdParameter.Value = logId;
                    command.Parameters.Add(logIdParameter);

                    var shiftIdParameter = command.CreateParameter();
                    shiftIdParameter.ParameterName = "@ShiftId";
                    shiftIdParameter.Value = shiftId.HasValue ? shiftId.Value : DBNull.Value;
                    command.Parameters.Add(shiftIdParameter);

                    var shiftStartParameter = command.CreateParameter();
                    shiftStartParameter.ParameterName = "@ShiftStartAt";
                    shiftStartParameter.Value = shiftStartAt.Value;
                    command.Parameters.Add(shiftStartParameter);

                    var shiftEndParameter = command.CreateParameter();
                    shiftEndParameter.ParameterName = "@ShiftEndAt";
                    shiftEndParameter.Value = shiftEndAt.Value;
                    command.Parameters.Add(shiftEndParameter);

                    await command.ExecuteNonQueryAsync();
                }
                finally
                {
                    if (shouldCloseConnection)
                    {
                        await connection.CloseAsync();
                    }
                }
            }
            catch
            {
                return;
            }
        }

        private async Task<AttendanceShiftSnapshot?> GetAttendanceShiftSnapshotAsync(int logId)
        {
            if (logId <= 0 || !await AttendanceShiftSnapshotColumnsExistAsync())
            {
                return null;
            }

            var connection = _context.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;

            if (shouldCloseConnection)
            {
                await connection.OpenAsync();
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = @"
SELECT TOP 1 Id, ShiftId, ShiftStartAt, ShiftEndAt
FROM dbo.EmployeeAttendanceLogs
WHERE Id = @LogId;";

                var logIdParameter = command.CreateParameter();
                logIdParameter.ParameterName = "@LogId";
                logIdParameter.Value = logId;
                command.Parameters.Add(logIdParameter);

                using var reader = await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return null;
                }

                return new AttendanceShiftSnapshot
                {
                    LogId = reader.GetInt32(0),
                    ShiftId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    ShiftStartAt = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                    ShiftEndAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3)
                };
            }
            finally
            {
                if (shouldCloseConnection)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private async Task ApplyAttendanceShiftSnapshotsAsync(List<EmployeeAttendanceLogRow> attendanceLogs)
        {
            if (attendanceLogs == null || !attendanceLogs.Any())
            {
                return;
            }

            // fallback مهم: لو أعمدة ShiftId/ShiftStartAt/ShiftEndAt غير موجودة أو لسه الميجريشن متطبقش،
            // نقرأ آخر دوام محفوظ في سجل التعديلات داخل Notes عشان تعديل دوام اليوم يفضل ظاهر بعد Refresh.
            ApplyAttendanceShiftSnapshotsFromEditHistoryNotes(attendanceLogs);

            if (!await AttendanceShiftSnapshotColumnsExistAsync())
            {
                return;
            }

            var ids = attendanceLogs
                .Where(log => log.Id > 0)
                .Select(log => log.Id)
                .Distinct()
                .ToList();

            if (!ids.Any())
            {
                return;
            }

            var connection = _context.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;

            if (shouldCloseConnection)
            {
                await connection.OpenAsync();
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = $@"
SELECT Id, ShiftId, ShiftStartAt, ShiftEndAt
FROM dbo.EmployeeAttendanceLogs
WHERE Id IN ({string.Join(",", ids)});";

                var map = new Dictionary<int, AttendanceShiftSnapshot>();

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    map[reader.GetInt32(0)] = new AttendanceShiftSnapshot
                    {
                        LogId = reader.GetInt32(0),
                        ShiftId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                        ShiftStartAt = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                        ShiftEndAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3)
                    };
                }

                foreach (var log in attendanceLogs)
                {
                    if (!map.TryGetValue(log.Id, out var snapshot))
                    {
                        continue;
                    }

                    if (snapshot.ShiftStartAt.HasValue && snapshot.ShiftEndAt.HasValue)
                    {
                        log.ShiftSnapshotId = snapshot.ShiftId;
                        log.ShiftSnapshotStartAt = snapshot.ShiftStartAt;
                        log.ShiftSnapshotEndAt = snapshot.ShiftEndAt;
                    }
                }
            }
            finally
            {
                if (shouldCloseConnection)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private const string ManualShiftOverrideStartMarker = "[MANUAL_SHIFT_OVERRIDE]";
        private const string ManualShiftOverrideEndMarker = "[/MANUAL_SHIFT_OVERRIDE]";

        private static string RemoveManualShiftOverrideMarkers(string? notes)
        {
            var text = notes ?? string.Empty;

            while (true)
            {
                var startIndex = text.IndexOf(ManualShiftOverrideStartMarker, StringComparison.OrdinalIgnoreCase);
                if (startIndex < 0)
                {
                    break;
                }

                var endIndex = text.IndexOf(ManualShiftOverrideEndMarker, startIndex + ManualShiftOverrideStartMarker.Length, StringComparison.OrdinalIgnoreCase);
                if (endIndex < 0)
                {
                    text = text.Remove(startIndex).Trim();
                    break;
                }

                text = text.Remove(startIndex, (endIndex + ManualShiftOverrideEndMarker.Length) - startIndex).Trim();
            }

            return text.Trim();
        }

        private static string AddManualShiftOverrideMarker(string? notes, TimeSpan shiftStartTime, TimeSpan shiftEndTime)
        {
            var cleanNotes = RemoveManualShiftOverrideMarkers(notes);
            var marker = ManualShiftOverrideStartMarker +
                         shiftStartTime.ToString(@"hh\:mm") +
                         "|" +
                         shiftEndTime.ToString(@"hh\:mm") +
                         ManualShiftOverrideEndMarker;

            return string.IsNullOrWhiteSpace(cleanNotes)
                ? marker
                : cleanNotes + Environment.NewLine + marker;
        }

        private static bool TryReadManualShiftOverrideMarker(string? notes, out TimeSpan shiftStartTime, out TimeSpan shiftEndTime)
        {
            shiftStartTime = default;
            shiftEndTime = default;

            var text = notes ?? string.Empty;
            var startIndex = text.LastIndexOf(ManualShiftOverrideStartMarker, StringComparison.OrdinalIgnoreCase);
            if (startIndex < 0)
            {
                return false;
            }

            var payloadStart = startIndex + ManualShiftOverrideStartMarker.Length;
            var endIndex = text.IndexOf(ManualShiftOverrideEndMarker, payloadStart, StringComparison.OrdinalIgnoreCase);
            if (endIndex < 0)
            {
                return false;
            }

            var payload = text.Substring(payloadStart, endIndex - payloadStart);
            var parts = payload.Split('|');

            return parts.Length == 2 &&
                   TimeSpan.TryParse(parts[0], out shiftStartTime) &&
                   TimeSpan.TryParse(parts[1], out shiftEndTime);
        }

        private static void ApplyAttendanceShiftSnapshotsFromEditHistoryNotes(List<EmployeeAttendanceLogRow> attendanceLogs)
        {
            if (attendanceLogs == null || !attendanceLogs.Any())
            {
                return;
            }

            foreach (var log in attendanceLogs)
            {
                if (log == null || string.IsNullOrWhiteSpace(log.Notes))
                {
                    continue;
                }

                TimeSpan shiftStartTime;
                TimeSpan shiftEndTime;

                if (!TryReadManualShiftOverrideMarker(log.Notes, out shiftStartTime, out shiftEndTime))
                {
                    var latestEntry = ParseAttendanceEditHistoryEntries(log.Notes)
                        .FirstOrDefault(entry =>
                            !string.IsNullOrWhiteSpace(entry.ShiftStartTimeText) &&
                            !string.IsNullOrWhiteSpace(entry.ShiftEndTimeText) &&
                            entry.ShiftStartTimeText != "-" &&
                            entry.ShiftEndTimeText != "-");

                    if (latestEntry == null)
                    {
                        continue;
                    }

                    if (!TimeSpan.TryParse(latestEntry.ShiftStartTimeText, out shiftStartTime) ||
                        !TimeSpan.TryParse(latestEntry.ShiftEndTimeText, out shiftEndTime))
                    {
                        continue;
                    }
                }

                var shiftWindow = BuildAttendanceShiftWindowForTime(log.CheckInAt, shiftStartTime, shiftEndTime);
                log.ShiftSnapshotStartAt = shiftWindow.ShiftStart;
                log.ShiftSnapshotEndAt = shiftWindow.ShiftEnd;
            }
        }

        private async Task<EmployeeAttendanceLog?> FindOpenAttendanceLogForEmployeeAsync(string userId, int employeeId, bool asNoTracking)
        {
            var query = _context.EmployeeAttendanceLogs
                .Where(log =>
                    log.UserId == userId &&
                    log.EmployeeId == employeeId &&
                    log.CheckOutAt == null &&
                    (log.Notes == null ||
                     (!log.Notes.Contains(AttendanceDeletedNoteMarker) &&
                      !log.Notes.Contains("AutoAbsent"))));

            if (asNoTracking)
            {
                query = query.AsNoTracking();
            }

            return await query
                .OrderByDescending(log => log.CheckInAt)
                .ThenByDescending(log => log.Id)
                .FirstOrDefaultAsync();
        }

        private async Task<Dictionary<int, HashSet<DayOfWeek>>> GetEmployeeWeeklyOffDaysMapAsync(List<int> employeeIds)
        {
            var map = new Dictionary<int, HashSet<DayOfWeek>>();

            if (employeeIds == null || !employeeIds.Any())
            {
                return map;
            }

            try
            {
                var connection = _context.Database.GetDbConnection();
                var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;

                if (shouldCloseConnection)
                {
                    await connection.OpenAsync();
                }

                try
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = $@"
IF COL_LENGTH('dbo.Employees', 'WeeklyOffDays') IS NULL
BEGIN
    SELECT CAST(0 AS INT) AS Id, CAST(N'' AS NVARCHAR(100)) AS WeeklyOffDays
    WHERE 1 = 0;
END
ELSE
BEGIN
    SELECT Id, CAST(ISNULL(WeeklyOffDays, N'') AS NVARCHAR(100)) AS WeeklyOffDays
    FROM dbo.Employees
    WHERE Id IN ({string.Join(",", employeeIds.Distinct())});
END";

                    using var reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        var employeeId = reader.GetInt32(0);
                        var daysText = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        var days = ParseWeeklyOffDays(daysText);

                        if (days.Any())
                        {
                            map[employeeId] = days;
                        }
                    }
                }
                finally
                {
                    if (shouldCloseConnection)
                    {
                        await connection.CloseAsync();
                    }
                }
            }
            catch
            {
                return map;
            }

            return map;
        }

        private static HashSet<DayOfWeek> ParseWeeklyOffDays(string daysText)
        {
            var result = new HashSet<DayOfWeek>();

            if (string.IsNullOrWhiteSpace(daysText))
            {
                return result;
            }

            foreach (var item in daysText.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (Enum.TryParse<DayOfWeek>(item.Trim(), true, out var day))
                {
                    result.Add(day);
                }
            }

            return result;
        }

        private static bool IsEmployeeWeeklyOffDay(
            int employeeId,
            DateTime day,
            Dictionary<int, HashSet<DayOfWeek>> weeklyOffDaysByEmployee)
        {
            return weeklyOffDaysByEmployee != null &&
                   weeklyOffDaysByEmployee.TryGetValue(employeeId, out var weeklyOffDays) &&
                   weeklyOffDays.Contains(day.DayOfWeek);
        }

        private async Task<List<EmployeeAttendanceLogRow>> CreateAutomaticAbsentRowsInDatabaseAsync(
            List<EmployeeAttendanceLogRow> attendanceLogs,
            List<EmployeeShiftLookupRow> shifts,
            List<int> activeEmployeeIds,
            DateTime periodStart,
            DateTime periodEnd,
            DateTime now,
            Dictionary<int, HashSet<DayOfWeek>> weeklyOffDaysByEmployee)
        {
            var createdRows = new List<EmployeeAttendanceLogRow>();
            attendanceLogs ??= new List<EmployeeAttendanceLogRow>();
            shifts ??= new List<EmployeeShiftLookupRow>();
            activeEmployeeIds ??= new List<int>();

            if (!activeEmployeeIds.Any() || !shifts.Any())
            {
                return createdRows;
            }

            if (periodEnd <= periodStart)
            {
                periodEnd = periodStart.AddDays(1);
            }

            var employeeUserIdMap = await _context.Employees
                .AsNoTracking()
                .Where(e => activeEmployeeIds.Contains(e.Id) &&
                            e.ApplicationUserId != null &&
                            e.ApplicationUserId != "")
                .Join(_context.Users.AsNoTracking(),
                    employee => employee.ApplicationUserId,
                    user => user.Id,
                    (employee, user) => new
                    {
                        EmployeeId = employee.Id,
                        UserId = user.Id
                    })
                .ToDictionaryAsync(item => item.EmployeeId, item => item.UserId);

            if (!employeeUserIdMap.Any())
            {
                return createdRows;
            }

            var rows = attendanceLogs.ToList();
            var periodDayStart = periodStart.Date;
            var periodDayEnd = periodEnd.Date;

            if (periodDayEnd <= periodDayStart)
            {
                periodDayEnd = periodDayStart.AddDays(1);
            }

            var newLogs = new List<(EmployeeAttendanceLog Log, EmployeeShiftLookupRow Shift, decimal DayDeductionAmount)>();

            foreach (var employeeId in activeEmployeeIds.Distinct())
            {
                if (!employeeUserIdMap.TryGetValue(employeeId, out var applicationUserId) ||
                    string.IsNullOrWhiteSpace(applicationUserId))
                {
                    continue;
                }

                var employeeShifts = shifts
                    .Where(s => s.EmployeeId == employeeId)
                    .OrderByDescending(s => GetShiftEffectiveDateTime(s))
                    .ThenByDescending(s => s.Id)
                    .ToList();

                if (!employeeShifts.Any())
                {
                    continue;
                }

                for (var day = periodDayStart; day < periodDayEnd; day = day.AddDays(1))
                {
                    var shift = FindShiftForDay(employeeId, day, shifts);

                    if (shift == null)
                    {
                        continue;
                    }

                    if (IsEmployeeWeeklyOffDay(employeeId, day, weeklyOffDaysByEmployee))
                    {
                        continue;
                    }

                    var intervalStart = day.Date.Add(shift.ShiftStartTime);
                    var intervalEnd = day.Date.Add(shift.ShiftEndTime);

                    if (intervalEnd <= intervalStart)
                    {
                        intervalEnd = intervalEnd.AddDays(1);
                    }

                    var intervalEndWithGrace = intervalEnd.AddMinutes(ShiftAccessGraceMinutes);

                    if (intervalEndWithGrace <= periodStart || intervalStart >= periodEnd)
                    {
                        continue;
                    }

                    if (now <= intervalEndWithGrace)
                    {
                        continue;
                    }

                    var hasAnyLogForThisShift = HasAnyAttendanceOrAutoAbsentForShift(
                        rows,
                        employeeId,
                        intervalStart,
                        intervalEndWithGrace);

                    if (hasAnyLogForThisShift)
                    {
                        continue;
                    }

                    var daysInMonth = DateTime.DaysInMonth(intervalStart.Year, intervalStart.Month);
                    var dayDeductionAmount = daysInMonth > 0 && shift.Salary > 0
                        ? Math.Round(shift.Salary / daysInMonth, 2)
                        : 0m;

                    var log = new EmployeeAttendanceLog
                    {
                        UserId = applicationUserId,
                        EmployeeId = employeeId,
                        EmployeeEmail = "",
                        EmployeeName = string.IsNullOrWhiteSpace(shift.EmployeeName) ? "بدون اسم" : shift.EmployeeName,
                        CheckInAt = intervalStart,
                        CheckOutAt = null,
                        FaceImagePath = "",
                        CheckOutFaceImagePath = "",
                        CheckInIpAddress = "",
                        CheckInLocation = "",
                        CheckOutIpAddress = "",
                        CheckOutLocation = "",
                        DeductionAmount = dayDeductionAmount,
                        DeductionReason = "غياب",
                        Notes = "AutoAbsent",
                        CreatedAt = now
                    };

                    _context.EmployeeAttendanceLogs.Add(log);
                    newLogs.Add((log, shift, dayDeductionAmount));
                }
            }

            if (!newLogs.Any())
            {
                return createdRows;
            }

            await _context.SaveChangesAsync();

            foreach (var item in newLogs)
            {
                await SaveAttendanceShiftSnapshotAsync(
                    item.Log.Id,
                    item.Shift.Id,
                    item.Log.CheckInAt,
                    GetShiftEndDateTimeForShiftStart(item.Log.CheckInAt, item.Shift.ShiftEndTime));
            }

            foreach (var item in newLogs)
            {
                createdRows.Add(new EmployeeAttendanceLogRow
                {
                    Id = item.Log.Id,
                    UserId = item.Log.UserId ?? "",
                    EmployeeId = item.Log.EmployeeId,
                    EmployeeName = item.Log.EmployeeName ?? "بدون اسم",
                    EmployeeEmail = item.Log.EmployeeEmail ?? "",
                    CheckInAt = item.Log.CheckInAt,
                    CheckOutAt = item.Log.CheckOutAt,
                    FaceImagePath = item.Log.FaceImagePath ?? "",
                    CheckOutFaceImagePath = item.Log.CheckOutFaceImagePath ?? "",
                    CheckInIpAddress = item.Log.CheckInIpAddress ?? "",
                    CheckInLocation = item.Log.CheckInLocation ?? "",
                    CheckOutIpAddress = item.Log.CheckOutIpAddress ?? "",
                    CheckOutLocation = item.Log.CheckOutLocation ?? "",
                    DeductionAmount = item.DayDeductionAmount,
                    DeductionReason = "غياب",
                    Notes = item.Log.Notes ?? "",
                    ShiftSnapshotId = item.Shift.Id,
                    ShiftSnapshotStartAt = item.Log.CheckInAt,
                    ShiftSnapshotEndAt = GetShiftEndDateTimeForShiftStart(item.Log.CheckInAt, item.Shift.ShiftEndTime),
                    ShiftStartTimeText = item.Shift.ShiftStartTime.ToString(@"hh\:mm"),
                    ShiftEndTimeText = item.Shift.ShiftEndTime.ToString(@"hh\:mm"),
                    LateMinutes = 0,
                    IsAbsent = true,
                    // ده صف AutoAbsent حقيقي من الداتا، لكنه في العرض لازم يتعامل كصف غياب صناعي
                    // عشان وقت الدخول يظهر "-" مش وقت بداية الشيفت.
                    IsSyntheticAbsentRow = true,
                    LateReason = "غياب",
                    CalculatedDeductionAmount = item.DayDeductionAmount,
                    SuggestedDeductionAmount = item.DayDeductionAmount,
                    SuggestedLateReason = "غياب"
                });
            }

            return createdRows;
        }

        private static List<EmployeeAttendanceLogRow> AddAutomaticAbsentRowsToDisplayedLogs(
            List<EmployeeAttendanceLogRow> attendanceLogs,
            List<EmployeeShiftLookupRow> shifts,
            List<int> activeEmployeeIds,
            DateTime periodStart,
            DateTime periodEnd,
            DateTime now,
            Dictionary<int, HashSet<DayOfWeek>> weeklyOffDaysByEmployee)
        {
            attendanceLogs ??= new List<EmployeeAttendanceLogRow>();
            shifts ??= new List<EmployeeShiftLookupRow>();
            activeEmployeeIds ??= new List<int>();

            if (!activeEmployeeIds.Any() || !shifts.Any())
            {
                return attendanceLogs
                    .OrderByDescending(log => log.CheckInAt)
                    .ThenByDescending(log => log.Id)
                    .ToList();
            }

            if (periodEnd <= periodStart)
            {
                periodEnd = periodStart.AddDays(1);
            }

            var rows = attendanceLogs.ToList();
            var nextAbsentId = rows.Any() ? Math.Min(0, rows.Min(log => log.Id)) - 1 : -1;
            var periodDayStart = periodStart.Date;
            var periodDayEnd = periodEnd.Date;

            if (periodDayEnd <= periodDayStart)
            {
                periodDayEnd = periodDayStart.AddDays(1);
            }

            foreach (var employeeId in activeEmployeeIds.Distinct())
            {
                var employeeShifts = shifts
                    .Where(s => s.EmployeeId == employeeId)
                    .OrderByDescending(s => GetShiftEffectiveDateTime(s))
                    .ThenByDescending(s => s.Id)
                    .ToList();

                if (!employeeShifts.Any())
                {
                    continue;
                }

                for (var day = periodDayStart; day < periodDayEnd; day = day.AddDays(1))
                {
                    var shift = FindShiftForDay(employeeId, day, shifts);

                    if (shift == null)
                    {
                        continue;
                    }

                    if (IsEmployeeWeeklyOffDay(employeeId, day, weeklyOffDaysByEmployee))
                    {
                        continue;
                    }

                    var intervalStart = day.Date.Add(shift.ShiftStartTime);
                    var intervalEnd = day.Date.Add(shift.ShiftEndTime);

                    if (intervalEnd <= intervalStart)
                    {
                        intervalEnd = intervalEnd.AddDays(1);
                    }

                    var intervalEndWithGrace = intervalEnd.AddMinutes(ShiftAccessGraceMinutes);

                    if (intervalEndWithGrace <= periodStart || intervalStart >= periodEnd)
                    {
                        continue;
                    }

                    if (now <= intervalEndWithGrace)
                    {
                        continue;
                    }

                    var hasAnyLogForThisShift = HasAnyAttendanceOrAutoAbsentForShift(
                        rows,
                        employeeId,
                        intervalStart,
                        intervalEndWithGrace);

                    if (hasAnyLogForThisShift)
                    {
                        continue;
                    }

                    var daysInMonth = DateTime.DaysInMonth(intervalStart.Year, intervalStart.Month);
                    var dayDeductionAmount = daysInMonth > 0 && shift.Salary > 0
                        ? Math.Round(shift.Salary / daysInMonth, 2)
                        : 0m;

                    rows.Add(new EmployeeAttendanceLogRow
                    {
                        Id = nextAbsentId--,
                        UserId = "",
                        EmployeeId = employeeId,
                        EmployeeName = string.IsNullOrWhiteSpace(shift.EmployeeName) ? "بدون اسم" : shift.EmployeeName,
                        EmployeeEmail = "",
                        CheckInAt = intervalStart,
                        CheckOutAt = null,
                        FaceImagePath = "",
                        CheckOutFaceImagePath = "",
                        CheckInIpAddress = "",
                        CheckInLocation = "",
                        CheckOutIpAddress = "",
                        CheckOutLocation = "",
                        DeductionAmount = dayDeductionAmount,
                        DeductionReason = "غياب",
                        Notes = "",
                        ShiftStartTimeText = shift.ShiftStartTime.ToString(@"hh\:mm"),
                        ShiftEndTimeText = shift.ShiftEndTime.ToString(@"hh\:mm"),
                        LateMinutes = 0,
                        IsAbsent = true,
                        IsSyntheticAbsentRow = true,
                        LateReason = "غياب",
                        CalculatedDeductionAmount = dayDeductionAmount,
                        SuggestedDeductionAmount = dayDeductionAmount,
                        SuggestedLateReason = "غياب"
                    });
                }
            }

            return rows
                .OrderByDescending(log => log.CheckInAt)
                .ThenByDescending(log => log.Id)
                .ToList();
        }

        private static AttendanceSummaryCounts BuildDisplayedAttendanceSummaryCounts(List<EmployeeAttendanceLogRow> attendanceLogs)
        {
            var summary = new AttendanceSummaryCounts();

            if (attendanceLogs == null || !attendanceLogs.Any())
            {
                return summary;
            }

            summary.AbsentCount = attendanceLogs.Count(log => log.IsAbsent);
            summary.LateCount = attendanceLogs.Count(log => !log.IsAbsent && log.LateMinutes.HasValue && log.LateMinutes.Value > 0);
            summary.DisciplinedCount = attendanceLogs.Count(log => !log.IsAbsent && log.LateMinutes.HasValue && log.LateMinutes.Value == 0);
            summary.PresentCount = attendanceLogs.Count(log => !log.IsAbsent && log.LateMinutes.HasValue);
            summary.LeaveCount = 0;

            return summary;
        }

        private async Task<AttendanceSummaryCounts> BuildAttendanceSummaryCountsAsync(
            DateTime periodStart,
            DateTime periodEnd,
            DateTime now,
            int? filteredEmployeeId)
        {
            var summary = new AttendanceSummaryCounts();

            if (periodEnd <= periodStart)
            {
                periodEnd = periodStart.AddDays(1);
            }

            var activeEmployeeQuery = _context.Employees
                .AsNoTracking()
                .Where(e => e.IsActive);

            if (filteredEmployeeId.HasValue && filteredEmployeeId.Value > 0)
            {
                activeEmployeeQuery = activeEmployeeQuery.Where(e => e.Id == filteredEmployeeId.Value);
            }

            var activeEmployeeIds = await activeEmployeeQuery
                .Select(e => e.Id)
                .Distinct()
                .ToListAsync();

            if (!activeEmployeeIds.Any())
            {
                return summary;
            }

            var employeeShifts = await _context.EmployeeWorkShifts
                .AsNoTracking()
                .Where(s =>
                    activeEmployeeIds.Contains(s.EmployeeId) &&
                    s.CreatedAt < periodEnd)
                .OrderBy(s => s.EmployeeId)
                .ThenBy(s => s.CreatedAt)
                .ThenBy(s => s.Id)
                .Select(s => new EmployeeShiftLookupRow
                {
                    Id = s.Id,
                    EmployeeId = s.EmployeeId,
                    EmployeeName = _context.Employees
                        .Where(e => e.Id == s.EmployeeId)
                        .Select(e => e.DisplayName == null || e.DisplayName == ""
                            ? (e.Name == null ? "" : e.Name)
                            : e.DisplayName)
                        .FirstOrDefault() ?? "",
                    Salary = _context.Employees
                        .Where(e => e.Id == s.EmployeeId)
                        .Select(e => e.Salary)
                        .FirstOrDefault(),
                    ShiftStartTime = s.ShiftStartTime,
                    ShiftEndTime = s.ShiftEndTime,
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync();

            if (!employeeShifts.Any())
            {
                return summary;
            }

            var searchStart = periodStart.AddDays(-1);
            var searchEnd = periodEnd.AddDays(1);

            var logs = await _context.EmployeeAttendanceLogs
                .AsNoTracking()
                .Where(log =>
                    log.EmployeeId.HasValue &&
                    activeEmployeeIds.Contains(log.EmployeeId.Value) &&
                    log.CheckInAt >= searchStart &&
                    log.CheckInAt < searchEnd &&
                    (log.Notes == null || !log.Notes.Contains(AttendanceDeletedNoteMarker)))
                .Select(log => new
                {
                    log.EmployeeId,
                    log.CheckInAt
                })
                .ToListAsync();

            var periodDayStart = periodStart.Date;
            var periodDayEnd = periodEnd.Date;

            if (periodEnd.TimeOfDay > TimeSpan.Zero)
            {
                periodDayEnd = periodDayEnd.AddDays(1);
            }

            for (var day = periodDayStart; day < periodDayEnd; day = day.AddDays(1))
            {
                foreach (var employeeId in activeEmployeeIds)
                {
                    var shift = FindShiftForDay(employeeId, day, employeeShifts);

                    if (shift == null)
                    {
                        continue;
                    }

                    var intervalStart = day.Date.Add(shift.ShiftStartTime);
                    var intervalEnd = day.Date.Add(shift.ShiftEndTime);

                    if (intervalEnd <= intervalStart)
                    {
                        intervalEnd = intervalEnd.AddDays(1);
                    }

                    if (intervalEnd <= periodStart || intervalStart >= periodEnd)
                    {
                        continue;
                    }

                    if (intervalStart > now)
                    {
                        continue;
                    }

                    var employeeLogs = logs
                        .Where(log => log.EmployeeId == employeeId)
                        .OrderBy(log => log.CheckInAt)
                        .ToList();

                    var logsInsideShift = employeeLogs
                        .Where(log => log.CheckInAt >= intervalStart && log.CheckInAt <= intervalEnd)
                        .OrderBy(log => log.CheckInAt)
                        .ToList();

                    var firstLogInsideOrAfterShift = employeeLogs
                        .Where(log => log.CheckInAt >= intervalStart && log.CheckInAt < periodEnd)
                        .OrderBy(log => log.CheckInAt)
                        .FirstOrDefault();

                    if (firstLogInsideOrAfterShift != null && firstLogInsideOrAfterShift.CheckInAt > intervalEnd)
                    {
                        summary.AbsentCount++;
                        continue;
                    }

                    if (logsInsideShift.Any())
                    {
                        summary.PresentCount++;

                        if (logsInsideShift.First().CheckInAt > intervalStart)
                        {
                            summary.LateCount++;
                        }
                        else
                        {
                            summary.DisciplinedCount++;
                        }

                        continue;
                    }

                    if (now > intervalEnd)
                    {
                        summary.AbsentCount++;
                    }
                }
            }

            summary.LeaveCount = 0;

            return summary;
        }

        private static ShiftInterval? GetRelevantShiftIntervalForPeriod(
            TimeSpan shiftStartTime,
            TimeSpan shiftEndTime,
            DateTime periodStart,
            DateTime periodEnd,
            DateTime now)
        {
            var candidates = new List<ShiftInterval>();

            for (var day = periodStart.Date.AddDays(-1); day <= periodEnd.Date.AddDays(1); day = day.AddDays(1))
            {
                var start = day.Add(shiftStartTime);
                var end = day.Add(shiftEndTime);

                if (end <= start)
                {
                    end = end.AddDays(1);
                }

                if (end > periodStart && start < periodEnd && start <= now)
                {
                    candidates.Add(new ShiftInterval
                    {
                        Start = start,
                        End = end
                    });
                }
            }

            return candidates
                .OrderByDescending(interval => interval.Start)
                .FirstOrDefault();
        }

        private async Task<List<SelectListItem>> GetEmployeesSelectListAsync()
        {
            var employees = await _context.Employees
                .AsNoTracking()
                .Include(e => e.ApplicationUser)
                .Where(e => e.IsActive && e.ApplicationUser != null && e.ApplicationUser.EmailConfirmed)
                .OrderBy(e => e.Name)
                .Select(e => new
                {
                    e.Id,
                    e.Name,
                    e.DisplayName,
                    ImageUrl = e.ImageUrl == null ? "" : e.ImageUrl
                })
                .ToListAsync();

            return employees
                .Select(e => new SelectListItem
                {
                    Value = e.Id.ToString(),
                    Text = e.DisplayName == null || e.DisplayName == ""
                        ? (e.Name == null ? "بدون اسم" : e.Name)
                        : e.DisplayName,
                    Group = new SelectListGroup
                    {
                        Name = NormalizeEmployeeImagePath(e.ImageUrl)
                    }
                })
                .ToList();
        }


        private async Task<string> SaveCheckInFaceImageAsync(string userId, string faceImageBase64)
        {
            if (string.IsNullOrWhiteSpace(faceImageBase64))
            {
                return "";
            }

            var webRootPath = _environment.WebRootPath;

            if (string.IsNullOrWhiteSpace(webRootPath))
            {
                webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            }

            var uploadsFolder = Path.Combine(webRootPath, "attendance-faces");

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var base64Data = faceImageBase64
                .Replace("data:image/jpeg;base64,", "")
                .Replace("data:image/jpg;base64,", "")
                .Replace("data:image/png;base64,", "");

            var imageBytes = Convert.FromBase64String(base64Data);

            var fileName = $"{userId}_{DateTime.Now:yyyyMMddHHmmss}.jpg";
            var filePath = Path.Combine(uploadsFolder, fileName);

            await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);

            return $"/attendance-faces/{fileName}";
        }

        private async Task<string> SaveCheckOutFaceImageAsync(string userId, string faceImageBase64)
        {
            if (string.IsNullOrWhiteSpace(faceImageBase64))
            {
                return "";
            }

            var webRootPath = _environment.WebRootPath;

            if (string.IsNullOrWhiteSpace(webRootPath))
            {
                webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            }

            var uploadsFolder = Path.Combine(webRootPath, "attendance-checkout-faces");

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var base64Data = faceImageBase64
                .Replace("data:image/jpeg;base64,", "")
                .Replace("data:image/jpg;base64,", "")
                .Replace("data:image/png;base64,", "");

            var imageBytes = Convert.FromBase64String(base64Data);

            var fileName = $"{userId}_{DateTime.Now:yyyyMMddHHmmss}.jpg";
            var filePath = Path.Combine(uploadsFolder, fileName);

            await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);

            return $"/attendance-checkout-faces/{fileName}";
        }

        private sealed class AttendanceShiftWindow
        {
            public DateTime ShiftStart { get; set; }

            public DateTime AccessStart { get; set; }

            public DateTime ShiftEnd { get; set; }

            public DateTime ShiftEndWithGrace { get; set; }
        }

        private static AttendanceShiftWindow BuildAttendanceShiftWindowForTime(
            DateTime currentTime,
            TimeSpan shiftStartTime,
            TimeSpan shiftEndTime)
        {
            var todayShiftStart = currentTime.Date.Add(shiftStartTime);
            var todayShiftEnd = GetShiftEndDateTimeForShiftStart(todayShiftStart, shiftEndTime);
            var todayAccessStart = todayShiftStart.AddMinutes(-ShiftAccessGraceMinutes);

            if (currentTime >= todayAccessStart)
            {
                return new AttendanceShiftWindow
                {
                    ShiftStart = todayShiftStart,
                    AccessStart = todayAccessStart,
                    ShiftEnd = todayShiftEnd,
                    ShiftEndWithGrace = todayShiftEnd.AddMinutes(ShiftAccessGraceMinutes)
                };
            }

            var yesterdayShiftStart = currentTime.Date.AddDays(-1).Add(shiftStartTime);
            var yesterdayShiftEnd = GetShiftEndDateTimeForShiftStart(yesterdayShiftStart, shiftEndTime);

            return new AttendanceShiftWindow
            {
                ShiftStart = yesterdayShiftStart,
                AccessStart = yesterdayShiftStart.AddMinutes(-ShiftAccessGraceMinutes),
                ShiftEnd = yesterdayShiftEnd,
                ShiftEndWithGrace = yesterdayShiftEnd.AddMinutes(ShiftAccessGraceMinutes)
            };
        }

        private static bool HasRealAttendanceForShift(
            List<EmployeeAttendanceLogRow> rows,
            int employeeId,
            DateTime intervalStart,
            DateTime intervalEndWithGrace)
        {
            var accessStart = intervalStart.AddMinutes(-ShiftAccessGraceMinutes);

            return rows.Any(log =>
                !IsAutomaticAbsentDisplayRow(log) &&
                !IsDeletedAttendanceNote(log.Notes) &&
                log.EmployeeId == employeeId &&
                log.CheckInAt >= accessStart &&
                log.CheckInAt <= intervalEndWithGrace);
        }

        private static bool HasAnyAttendanceOrAutoAbsentForShift(
            List<EmployeeAttendanceLogRow> rows,
            int employeeId,
            DateTime intervalStart,
            DateTime intervalEndWithGrace)
        {
            var accessStart = intervalStart.AddMinutes(-ShiftAccessGraceMinutes);

            return rows.Any(log =>
                !IsDeletedAttendanceNote(log.Notes) &&
                log.EmployeeId == employeeId &&
                log.CheckInAt >= accessStart &&
                log.CheckInAt <= intervalEndWithGrace);
        }

        private async Task<EmployeeAttendanceLog> FindLatestAttendanceLogInCurrentWorkPeriodAsync(
            string userId,
            int employeeId,
            DateTime currentTime,
            TimeSpan? shiftStartTime,
            TimeSpan? shiftEndTime,
            bool asNoTracking)
        {
            var query = _context.EmployeeAttendanceLogs
                .Where(x =>
                    x.UserId == userId &&
                    x.EmployeeId == employeeId &&
                    (x.Notes == null ||
                     (!x.Notes.Contains(AttendanceDeletedNoteMarker) &&
                      !x.Notes.Contains("AutoAbsent"))));

            if (asNoTracking)
            {
                query = query.AsNoTracking();
            }

            if (!shiftStartTime.HasValue || !shiftEndTime.HasValue)
            {
                var dayStart = currentTime.Date;
                var dayEnd = dayStart.AddDays(1);

                return await query
                    .Where(x => x.CheckInAt >= dayStart && x.CheckInAt < dayEnd)
                    .OrderByDescending(x => x.CheckInAt)
                    .ThenByDescending(x => x.Id)
                    .FirstOrDefaultAsync();
            }

            var currentWindow = BuildAttendanceShiftWindowForTime(
                currentTime,
                shiftStartTime.Value,
                shiftEndTime.Value);

            return await query
                .Where(x =>
                    x.CheckInAt >= currentWindow.AccessStart &&
                    x.CheckInAt <= currentWindow.ShiftEndWithGrace)
                .OrderByDescending(x => x.CheckInAt)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync();
        }

        private static EmployeeShiftLookupRow? FindEmployeeShift(
            EmployeeAttendanceLogRow log,
            List<EmployeeShiftLookupRow> shifts)
        {
            if (log == null || shifts == null || !shifts.Any())
            {
                return null;
            }

            var snapshotShift = BuildShiftFromAttendanceSnapshot(log, shifts);

            if (snapshotShift != null)
            {
                return snapshotShift;
            }

            IEnumerable<EmployeeShiftLookupRow> relatedShifts = Enumerable.Empty<EmployeeShiftLookupRow>();

            if (log.EmployeeId.HasValue)
            {
                relatedShifts = shifts.Where(s => s.EmployeeId == log.EmployeeId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(log.EmployeeName))
            {
                var normalizedLogName = NormalizeName(log.EmployeeName);
                relatedShifts = shifts.Where(s => NormalizeName(s.EmployeeName) == normalizedLogName);
            }

            var employeeShifts = relatedShifts
                .OrderByDescending(s => GetShiftEffectiveDateTime(s))
                .ThenByDescending(s => s.Id)
                .ToList();

            if (!employeeShifts.Any())
            {
                return null;
            }

            /*
                نربط السجل بالشيفت الذي يحتوي وقت الدخول داخل نافذته الفعلية.
                هذا يمنع أن تغيير الشيفت في منتصف اليوم يعيد حساب سجل قديم على الشيفت الجديد.
            */
            var windowMatchedShift = employeeShifts
                .Where(shift =>
                    IsAttendanceTimeInsideShiftWindow(log.CheckInAt, shift) &&
                    GetShiftEffectiveDateTime(shift) <= log.CheckInAt)
                .OrderByDescending(s => GetShiftEffectiveDateTime(s))
                .ThenByDescending(s => s.Id)
                .FirstOrDefault();

            if (windowMatchedShift != null)
            {
                return windowMatchedShift;
            }

            var effectiveShift = employeeShifts
                .Where(s => GetShiftEffectiveDateTime(s) <= log.CheckInAt)
                .OrderByDescending(s => GetShiftEffectiveDateTime(s))
                .ThenByDescending(s => s.Id)
                .FirstOrDefault();

            if (effectiveShift != null)
            {
                return effectiveShift;
            }

            return employeeShifts
                .OrderBy(s => GetShiftEffectiveDateTime(s))
                .ThenBy(s => s.Id)
                .FirstOrDefault();
        }

        private static EmployeeShiftLookupRow? BuildShiftFromAttendanceSnapshot(EmployeeAttendanceLogRow log, List<EmployeeShiftLookupRow> shifts)
        {
            if (log == null || !log.ShiftSnapshotStartAt.HasValue || !log.ShiftSnapshotEndAt.HasValue)
            {
                return null;
            }

            decimal salary = 0m;
            var shiftName = log.EmployeeName ?? "";
            var snapshotId = log.ShiftSnapshotId ?? 0;

            var matchedShift = shifts?
                .FirstOrDefault(s => snapshotId > 0 && s.Id == snapshotId);

            if (matchedShift == null && log.EmployeeId.HasValue)
            {
                matchedShift = shifts?
                    .Where(s => s.EmployeeId == log.EmployeeId.Value)
                    .OrderByDescending(s => GetShiftEffectiveDateTime(s))
                    .ThenByDescending(s => s.Id)
                    .FirstOrDefault();
            }

            if (matchedShift != null)
            {
                salary = matchedShift.Salary;
                shiftName = matchedShift.EmployeeName;
            }

            return new EmployeeShiftLookupRow
            {
                Id = snapshotId,
                EmployeeId = log.EmployeeId ?? (matchedShift?.EmployeeId ?? 0),
                EmployeeName = string.IsNullOrWhiteSpace(shiftName) ? (log.EmployeeName ?? "") : shiftName,
                Salary = salary,
                ShiftStartTime = log.ShiftSnapshotStartAt.Value.TimeOfDay,
                ShiftEndTime = log.ShiftSnapshotEndAt.Value.TimeOfDay,
                CreatedAt = log.ShiftSnapshotStartAt.Value
            };
        }

        private static bool IsAttendanceTimeInsideShiftWindow(DateTime attendanceTime, EmployeeShiftLookupRow shift)
        {
            var window = BuildAttendanceShiftWindowForTime(
                attendanceTime,
                shift.ShiftStartTime,
                shift.ShiftEndTime);

            return attendanceTime >= window.AccessStart &&
                   attendanceTime <= window.ShiftEndWithGrace;
        }

        private static EmployeeShiftLookupRow? FindShiftForDay(
            int employeeId,
            DateTime day,
            List<EmployeeShiftLookupRow> shifts)
        {
            var employeeShifts = (shifts ?? new List<EmployeeShiftLookupRow>())
                .Where(s => s.EmployeeId == employeeId)
                .OrderByDescending(s => GetShiftEffectiveDateTime(s))
                .ThenByDescending(s => s.Id)
                .ToList();

            if (!employeeShifts.Any())
            {
                return null;
            }

            var dayEndExclusive = day.Date.AddDays(1);

            var effectiveShift = employeeShifts
                .Where(s => GetShiftEffectiveDateTime(s) < dayEndExclusive)
                .OrderByDescending(s => GetShiftEffectiveDateTime(s))
                .ThenByDescending(s => s.Id)
                .FirstOrDefault();

            if (effectiveShift != null)
            {
                return effectiveShift;
            }

            return employeeShifts
                .OrderBy(s => GetShiftEffectiveDateTime(s))
                .ThenBy(s => s.Id)
                .FirstOrDefault();
        }

        private static DateTime GetShiftEffectiveDateTime(EmployeeShiftLookupRow shift)
        {
            return shift == null || shift.CreatedAt == default
                ? DateTime.MinValue
                : shift.CreatedAt;
        }

        private static bool IsLeaveReason(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return false;
            }

            var text = reason.Trim();
            return text.Contains("إجازة", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("اجازة", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("Weekly off", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("Leave", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("Off day", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLateReason(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return false;
            }

            var text = reason.Trim();
            return text.Contains("تأخر", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("متأخر", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("دقيقة خصم", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNoDeductionReason(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return false;
            }

            return reason.Contains("لا يوجد خصم") || reason.Contains("لا يوجد تأخير");
        }

        private void ApplyEarlyCheckOutDeductionFromSnapshot(
            EmployeeAttendanceLog attendanceLog,
            int employeeId,
            decimal salary,
            DateTime shiftStartAt,
            DateTime shiftEndAt,
            DateTime checkOutAt,
            bool createTransaction)
        {
            if (attendanceLog == null)
            {
                return;
            }

            if (ContainsEarlyCheckOutDeductionReason(attendanceLog.DeductionReason))
            {
                return;
            }

            if (checkOutAt >= shiftEndAt)
            {
                return;
            }

            var earlyMinutes = (int)Math.Ceiling((shiftEndAt - checkOutAt).TotalMinutes);

            if (earlyMinutes <= 0)
            {
                return;
            }

            var daysInMonth = DateTime.DaysInMonth(shiftStartAt.Year, shiftStartAt.Month);
            var shiftDurationMinutes = Math.Max(1m, (decimal)(shiftEndAt - shiftStartAt).TotalMinutes);
            var dayRate = daysInMonth > 0 ? salary / daysInMonth : 0m;
            var minuteRate = shiftDurationMinutes > 0 ? dayRate / shiftDurationMinutes : 0m;
            var deductionAmount = Math.Round(earlyMinutes * LatePenaltyMultiplier * minuteRate, 2);

            if (deductionAmount <= 0m)
            {
                return;
            }

            var reason = $"خروج مبكر {earlyMinutes} دقيقة × {LatePenaltyMultiplier:0.##} = {earlyMinutes * LatePenaltyMultiplier:0.##} دقيقة خصم";
            var currentDeductionAmount = attendanceLog.DeductionAmount ?? 0m;

            if (IsNoDeductionReason(attendanceLog.DeductionReason))
            {
                currentDeductionAmount = 0m;
            }

            attendanceLog.DeductionAmount = Math.Round(currentDeductionAmount + deductionAmount, 2);
            attendanceLog.DeductionReason = AppendDeductionReason(attendanceLog.DeductionReason, reason);

            if (createTransaction)
            {
                _context.EmployeeTransactions.Add(new EmployeeTransaction
                {
                    Amount = deductionAmount,
                    TransactionType = (TransactionTypeEnum)0,
                    Reason = reason,
                    Date = checkOutAt,
                    EmployeeId = employeeId
                });
            }
        }

        private void ApplyEarlyCheckOutDeduction(
            EmployeeAttendanceLog attendanceLog,
            int employeeId,
            decimal salary,
            TimeSpan shiftStartTime,
            TimeSpan shiftEndTime,
            DateTime checkOutAt,
            bool createTransaction)
        {
            if (attendanceLog == null)
            {
                return;
            }

            if (ContainsEarlyCheckOutDeductionReason(attendanceLog.DeductionReason))
            {
                return;
            }

            var earlyCheckOutResult = CalculateEarlyCheckOutDeduction(
                attendanceLog.CheckInAt,
                checkOutAt,
                shiftStartTime,
                shiftEndTime,
                salary);

            if (earlyCheckOutResult.DeductionAmount <= 0m)
            {
                return;
            }

            var currentDeductionAmount = attendanceLog.DeductionAmount ?? 0m;

            if (IsNoDeductionReason(attendanceLog.DeductionReason))
            {
                currentDeductionAmount = 0m;
            }

            attendanceLog.DeductionAmount = Math.Round(currentDeductionAmount + earlyCheckOutResult.DeductionAmount, 2);
            attendanceLog.DeductionReason = AppendDeductionReason(attendanceLog.DeductionReason, earlyCheckOutResult.Reason);

            if (createTransaction)
            {
                _context.EmployeeTransactions.Add(new EmployeeTransaction
                {
                    Amount = earlyCheckOutResult.DeductionAmount,
                    TransactionType = (TransactionTypeEnum)0,
                    Reason = earlyCheckOutResult.Reason,
                    Date = checkOutAt,
                    EmployeeId = employeeId
                });
            }
        }

        private static bool ContainsEarlyCheckOutDeductionReason(string? reason)
        {
            return !string.IsNullOrWhiteSpace(reason) &&
                   reason.Contains("خروج مبكر", StringComparison.OrdinalIgnoreCase);
        }

        private static string AppendDeductionReason(string? existingReason, string newReason)
        {
            existingReason = existingReason ?? string.Empty;
            newReason = newReason ?? string.Empty;

            if (string.IsNullOrWhiteSpace(newReason))
            {
                return existingReason.Trim();
            }

            if (string.IsNullOrWhiteSpace(existingReason) || IsNoDeductionReason(existingReason))
            {
                return newReason.Trim();
            }

            if (existingReason.Contains(newReason, StringComparison.OrdinalIgnoreCase))
            {
                return existingReason.Trim();
            }

            var newCategory = GetDeductionReasonCategory(newReason);
            if (!string.IsNullOrWhiteSpace(newCategory) &&
                existingReason.Contains(newCategory, StringComparison.OrdinalIgnoreCase))
            {
                return existingReason.Trim();
            }

            return existingReason.Trim() + " + " + newReason.Trim();
        }

        private static string GetDeductionReasonCategory(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return string.Empty;
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

            return string.Empty;
        }

        private static LateDeductionResult CalculateEarlyCheckOutDeduction(
            DateTime checkInAt,
            DateTime checkOutAt,
            TimeSpan shiftStartTime,
            TimeSpan shiftEndTime,
            decimal salary)
        {
            checkInAt = TruncateToMinute(checkInAt);
            checkOutAt = TruncateToMinute(checkOutAt);

            var shiftStartDateTime = GetShiftStartDateTimeForCheckIn(checkInAt, shiftStartTime, shiftEndTime);
            var shiftEndDateTime = GetShiftEndDateTimeForShiftStart(shiftStartDateTime, shiftEndTime);

            var shiftDuration = shiftEndTime - shiftStartTime;

            if (shiftDuration.TotalMinutes <= 0)
            {
                shiftDuration = shiftDuration.Add(TimeSpan.FromHours(24));
            }

            var daysInMonth = DateTime.DaysInMonth(checkOutAt.Year, checkOutAt.Month);
            var monthlyWorkingMinutes = (decimal)shiftDuration.TotalMinutes * daysInMonth;

            var minuteRate = monthlyWorkingMinutes > 0 && salary > 0
                ? salary / monthlyWorkingMinutes
                : 0m;

            if (checkOutAt >= shiftEndDateTime)
            {
                return new LateDeductionResult
                {
                    LateMinutes = 0,
                    PenaltyMinutes = 0,
                    MinuteRate = 0,
                    DeductionAmount = 0,
                    Reason = "لا يوجد خصم"
                };
            }

            var earlyMinutes = (int)Math.Ceiling((shiftEndDateTime - checkOutAt).TotalMinutes);

            if (earlyMinutes <= 0)
            {
                return new LateDeductionResult
                {
                    LateMinutes = 0,
                    PenaltyMinutes = 0,
                    MinuteRate = 0,
                    DeductionAmount = 0,
                    Reason = "لا يوجد خصم"
                };
            }

            var penaltyMinutes = earlyMinutes * LatePenaltyMultiplier;
            var deductionAmount = Math.Round(penaltyMinutes * minuteRate, 2);

            return new LateDeductionResult
            {
                LateMinutes = earlyMinutes,
                PenaltyMinutes = penaltyMinutes,
                MinuteRate = minuteRate,
                DeductionAmount = deductionAmount,
                Reason = $"خروج مبكر {earlyMinutes} دقيقة × {LatePenaltyMultiplier:0.##} = {penaltyMinutes:0.##} دقيقة خصم"
            };
        }

        private static LateDeductionResult CalculateLateDeduction(
            DateTime checkInAt,
            TimeSpan shiftStartTime,
            TimeSpan shiftEndTime,
            decimal salary)
        {
            checkInAt = TruncateToMinute(checkInAt);

            var shiftStartDateTime = GetShiftStartDateTimeForCheckIn(checkInAt, shiftStartTime, shiftEndTime);
            var shiftEndDateTime = GetShiftEndDateTimeForShiftStart(shiftStartDateTime, shiftEndTime);

            var shiftDuration = shiftEndTime - shiftStartTime;

            if (shiftDuration.TotalMinutes <= 0)
            {
                shiftDuration = shiftDuration.Add(TimeSpan.FromHours(24));
            }

            var daysInMonth = DateTime.DaysInMonth(checkInAt.Year, checkInAt.Month);
            var monthlyWorkingMinutes = (decimal)shiftDuration.TotalMinutes * daysInMonth;

            var minuteRate = monthlyWorkingMinutes > 0 && salary > 0
                ? salary / monthlyWorkingMinutes
                : 0m;

            var dayDeductionAmount = daysInMonth > 0 && salary > 0
                ? Math.Round(salary / daysInMonth, 2)
                : 0m;

            if (checkInAt > shiftEndDateTime)
            {
                return new LateDeductionResult
                {
                    IsAbsent = true,
                    LateMinutes = 0,
                    PenaltyMinutes = 0,
                    MinuteRate = minuteRate,
                    DeductionAmount = dayDeductionAmount,
                    Reason = "غياب"
                };
            }

            if (checkInAt <= shiftStartDateTime)
            {
                return new LateDeductionResult
                {
                    LateMinutes = 0,
                    PenaltyMinutes = 0,
                    MinuteRate = 0,
                    DeductionAmount = 0,
                    Reason = "لا يوجد خصم"
                };
            }

            var lateMinutes = (int)Math.Ceiling((checkInAt - shiftStartDateTime).TotalMinutes);
            var penaltyMinutes = lateMinutes * LatePenaltyMultiplier;

            var deductionAmount = Math.Round(penaltyMinutes * minuteRate, 2);

            return new LateDeductionResult
            {
                LateMinutes = lateMinutes,
                PenaltyMinutes = penaltyMinutes,
                MinuteRate = minuteRate,
                DeductionAmount = deductionAmount,
                Reason = $"تأخر {lateMinutes} دقيقة × {LatePenaltyMultiplier:0.##} = {penaltyMinutes:0.##} دقيقة خصم"
            };
        }

        private static DateTime TruncateToMinute(DateTime value)
        {
            return new DateTime(
                value.Year,
                value.Month,
                value.Day,
                value.Hour,
                value.Minute,
                0,
                value.Kind);
        }

        private static DateTime GetShiftStartDateTimeForCheckIn(
            DateTime checkInAt,
            TimeSpan shiftStartTime,
            TimeSpan shiftEndTime)
        {
            var shiftStartsPreviousDay =
                shiftEndTime <= shiftStartTime &&
                checkInAt.TimeOfDay <= shiftEndTime;

            return shiftStartsPreviousDay
                ? checkInAt.Date.AddDays(-1).Add(shiftStartTime)
                : checkInAt.Date.Add(shiftStartTime);
        }

        private static DateTime GetShiftStartDateTimeForCheckOut(
            DateTime checkOutAt,
            TimeSpan shiftStartTime,
            TimeSpan shiftEndTime)
        {
            var shiftStartsPreviousDay =
                shiftEndTime <= shiftStartTime &&
                checkOutAt.TimeOfDay <= shiftEndTime;

            return shiftStartsPreviousDay
                ? checkOutAt.Date.AddDays(-1).Add(shiftStartTime)
                : checkOutAt.Date.Add(shiftStartTime);
        }

        private static DateTime GetShiftEndDateTimeForShiftStart(DateTime shiftStartDateTime, TimeSpan shiftEndTime)
        {
            var shiftEndDateTime = shiftStartDateTime.Date.Add(shiftEndTime);

            if (shiftEndDateTime <= shiftStartDateTime)
            {
                shiftEndDateTime = shiftEndDateTime.AddDays(1);
            }

            return shiftEndDateTime;
        }

        private static string NormalizeName(string value)
        {
            return (value ?? "").Trim().ToLowerInvariant();
        }


        private static bool IsOpenAttendanceStillInsideNoCapturePeriod(
            DateTime openCheckInAt,
            DateTime currentTime,
            TimeSpan? nextShiftStartTime)
        {
            if (openCheckInAt.Date == currentTime.Date)
            {
                return true;
            }

            if (openCheckInAt.Date < currentTime.Date &&
                nextShiftStartTime.HasValue &&
                currentTime.TimeOfDay < nextShiftStartTime.Value)
            {
                return true;
            }

            return false;
        }

        private static DateTime GetEgyptNow()
        {
            var utcNow = DateTime.UtcNow;

            var timeZoneIds = new[]
            {
                "Africa/Cairo",       // Linux/macOS
                "Egypt Standard Time" // Windows
            };

            foreach (var timeZoneId in timeZoneIds)
            {
                try
                {
                    var egyptTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                    return TimeZoneInfo.ConvertTimeFromUtc(utcNow, egyptTimeZone);
                }
                catch
                {
                    // Try the next timezone id.
                }
            }

            // Fallback for hosting environments that do not have timezone data installed.
            // Egypt is UTC+3 during summer/DST, which covers the current attendance period.
            return utcNow.AddHours(3);
        }
    }


    public class AttendanceSummaryCounts
    {
        public int PresentCount { get; set; }

        public int AbsentCount { get; set; }

        public int LateCount { get; set; }

        public int DisciplinedCount { get; set; }

        public int LeaveCount { get; set; }
    }

    public class ShiftInterval
    {
        public DateTime Start { get; set; }

        public DateTime End { get; set; }
    }

    public class SecureCheckInRequest
    {
        public string FaceImageBase64 { get; set; } = "";

        public string CheckInIpAddress { get; set; } = "";


        public string CheckInLocation { get; set; } = "";
    }

    public class SecureQuestionCheckInRequest
    {
        public bool IsReady { get; set; }

        public string CheckInIpAddress { get; set; } = "";


        public string CheckInLocation { get; set; } = "";
    }

    public class SecureLogoutRequest
    {
        public string FaceImageBase64 { get; set; } = "";

        public string CheckOutIpAddress { get; set; } = "";

        public string CheckOutLocation { get; set; } = "";
    }


    public class ToggleEmployeeLoginBlockRequest
    {
        public int ShiftId { get; set; }

        public bool IsBlocked { get; set; }
    }

    public class AttendanceEditHistoryChange
    {
        public string FieldName { get; set; } = "";

        public string OldValue { get; set; } = "";

        public string NewValue { get; set; } = "";
    }

    public class AttendanceEditHistoryEntry
    {
        public DateTime ChangedAt { get; set; }

        public string EditorName { get; set; } = "";

        public string ShiftStartTimeText { get; set; } = "";

        public string ShiftEndTimeText { get; set; } = "";

        public List<AttendanceEditHistoryChange> Changes { get; set; } = new List<AttendanceEditHistoryChange>();
    }

    public class UpdateAttendanceLogRowRequest
    {
        public int Id { get; set; }

        public string CheckInTime { get; set; } = "";

        public string CheckOutTime { get; set; } = "";

        public bool ClearCheckInTime { get; set; }

        public bool ClearCheckOutTime { get; set; }

        public string ShiftStartTime { get; set; } = "";

        public string ShiftEndTime { get; set; } = "";

        public decimal DeductionAmount { get; set; }

        public decimal AdvanceAmount { get; set; }

        public decimal BonusAmount { get; set; }

        public string LateReason { get; set; } = "";
    }

    public class DeleteAttendanceLogRowRequest
    {
        public int Id { get; set; }
    }

    public class EmployeeWorkShiftRow
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        public string EmployeeName { get; set; } = "";

        public TimeSpan ShiftStartTime { get; set; }

        public TimeSpan ShiftEndTime { get; set; }

        public string AllowedIpAddress { get; set; } = "";

        public string Notes { get; set; } = "";

        public bool IsActive { get; set; }
    }

    public class EmployeeShiftBlockRow
    {
        public int ShiftId { get; set; }

        public int EmployeeId { get; set; }

        public string EmployeeName { get; set; } = "";

        public TimeSpan ShiftStartTime { get; set; }

        public TimeSpan ShiftEndTime { get; set; }

        public bool IsLoginBlocked { get; set; }

        public DateTime? LoginBlockedAt { get; set; }

        public string LoginBlockReason { get; set; } = "";

        public DateTime? AdminUnblockedUntil { get; set; }
    }

    public class EmployeeAttendanceLogRow
    {
        public int Id { get; set; }

        public string UserId { get; set; } = "";

        public int? EmployeeId { get; set; }

        public string EmployeeName { get; set; } = "";

        public string EmployeeEmail { get; set; } = "";

        public DateTime CheckInAt { get; set; }

        public DateTime? CheckOutAt { get; set; }

        public string FaceImagePath { get; set; } = "";

        public string CheckOutFaceImagePath { get; set; } = "";

        public string CheckInIpAddress { get; set; } = "";

        public bool IsCheckInIpChanged { get; set; }

        public string CheckInIpHistoryTooltip { get; set; } = "";

        public string EmployeeImagePath { get; set; } = "/static/circle-user-solid.svg";

        public string CheckInLocation { get; set; } = "";

        public string CheckOutIpAddress { get; set; } = "";

        public string CheckOutLocation { get; set; } = "";

        public decimal? DeductionAmount { get; set; }

        public string DeductionReason { get; set; } = "";

        public string Notes { get; set; } = "";

        public int? ShiftSnapshotId { get; set; }

        public DateTime? ShiftSnapshotStartAt { get; set; }

        public DateTime? ShiftSnapshotEndAt { get; set; }

        public string ShiftStartTimeText { get; set; } = "-";

        public string ShiftEndTimeText { get; set; } = "-";

        public int? LateMinutes { get; set; }

        public bool IsAbsent { get; set; }

        public bool IsSyntheticAbsentRow { get; set; }

        public string LateReason { get; set; } = "";

        public decimal CalculatedDeductionAmount { get; set; }

        public decimal SuggestedDeductionAmount { get; set; }

        public string SuggestedLateReason { get; set; } = "";
    }

    public class EmployeeShiftLookupRow
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        public string EmployeeName { get; set; } = "";

        public decimal Salary { get; set; }

        public TimeSpan ShiftStartTime { get; set; }

        public TimeSpan ShiftEndTime { get; set; }

        public DateTime CreatedAt { get; set; }
    }


    public class EmployeeLoginInfo
    {
        public int EmployeeId { get; set; }

        public string Name { get; set; } = "";

        public string DisplayName { get; set; } = "";

        public decimal Salary { get; set; }
    }

    public class EmployeeShiftLoginLookup
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }

        public TimeSpan ShiftStartTime { get; set; }

        public TimeSpan ShiftEndTime { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class LateDeductionResult
    {
        public bool IsAbsent { get; set; }

        public int LateMinutes { get; set; }

        public decimal PenaltyMinutes { get; set; }

        public decimal MinuteRate { get; set; }

        public decimal DeductionAmount { get; set; }

        public string Reason { get; set; } = "";
    }
}
