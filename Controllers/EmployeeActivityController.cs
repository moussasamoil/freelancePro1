using System.Security.Claims;
using Crm_LotusBlue.Models;
using lotus_blue.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm_LotusBlue.Controllers
{
    [Authorize]
    public class EmployeeActivityController : Controller
    {
        private readonly ApplicationDbContext _context;

        private const int MaximumContinuousHeartbeatSeconds = 120;

        public EmployeeActivityController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Authorize(Roles = "Admin,ExecutiveDirector")]
        [HttpGet]
        public async Task<IActionResult> LoginSessions(DateTime? fromDate, DateTime? toDate)
        {
            var today = DateTime.Now.Date;
            var filterFromDate = fromDate?.Date ?? today;
            var filterToDate = toDate?.Date ?? today;

            var logs = await _context.EmployeeActivityLogs
                .AsNoTracking()
                .Where(log => log.ActivityDate >= filterFromDate && log.ActivityDate <= filterToDate)
                .OrderByDescending(log => log.LastSeenAt)
                .ToListAsync();

            var employeeIds = logs
                .Where(log => log.EmployeeId.HasValue)
                .Select(log => log.EmployeeId.Value)
                .Distinct()
                .ToList();

            var shifts = await _context.EmployeeWorkShifts
                .AsNoTracking()
                .Where(shift => shift.IsActive && employeeIds.Contains(shift.EmployeeId))
                .OrderByDescending(shift => shift.Id)
                .Select(shift => new EmployeeActivityShiftViewModel
                {
                    EmployeeId = shift.EmployeeId,
                    ShiftStartTime = shift.ShiftStartTime,
                    ShiftEndTime = shift.ShiftEndTime
                })
                .ToListAsync();

            var shiftMap = shifts
                .GroupBy(shift => shift.EmployeeId)
                .ToDictionary(group => group.Key, group => group.First());

            var attendanceMap = await BuildAttendanceTimeMapAsync(employeeIds, filterFromDate, filterToDate);

            var now = DateTime.Now;

            var cards = logs.Select(log =>
            {
                var status = ResolveStatus(log, now);

                var shift = log.EmployeeId.HasValue && shiftMap.ContainsKey(log.EmployeeId.Value)
                    ? shiftMap[log.EmployeeId.Value]
                    : null;

                var shiftStartAt = GetShiftStartDateTime(log.ActivityDate, shift);
                var workWindowStart = shiftStartAt ?? log.FirstSeenAt;
                var workWindowEnd = status.Status == "Offline" ? log.LastSeenAt : now;

                if (workWindowEnd < workWindowStart)
                {
                    workWindowEnd = workWindowStart;
                }

                var totalOnlineSeconds = log.TotalOnlineSeconds;
                var totalActiveSeconds = log.TotalActiveSeconds;

                if (totalOnlineSeconds <= 0)
                {
                    totalOnlineSeconds = EstimateOnlineSeconds(log, now, status);
                }

                if (totalActiveSeconds <= 0 && log.LastActivityAt.HasValue)
                {
                    totalActiveSeconds = EstimateActiveSeconds(log, now, status);
                }

                var attendance = ResolveAttendanceForActivityLog(log, attendanceMap);

                return new EmployeeLoginActivityCardViewModel
                {
                    Id = log.Id,
                    EmployeeId = log.EmployeeId,
                    EmployeeName = string.IsNullOrWhiteSpace(log.EmployeeName) ? "بدون اسم" : log.EmployeeName,
                    EmployeeEmail = log.EmployeeEmail ?? "",
                    EmployeeImageUrl = string.IsNullOrWhiteSpace(log.EmployeeImageUrl)
                        ? "/static/circle-user-solid.svg"
                        : log.EmployeeImageUrl,
                    ActivityDate = log.ActivityDate,
                    FirstSeenAt = log.FirstSeenAt,
                    LastSeenAt = log.LastSeenAt,
                    LastActivityAt = log.LastActivityAt,
                    CurrentPage = string.IsNullOrWhiteSpace(log.CurrentPage) ? "-" : log.CurrentPage,
                    IpAddress = string.IsNullOrWhiteSpace(log.IpAddress) ? "-" : log.IpAddress,
                    IsTabActive = log.IsTabActive,
                    Status = status.Status,
                    StatusText = status.StatusText,
                    StatusClass = status.StatusClass,
                    LastActivityText = log.LastActivityAt.HasValue
                        ? log.LastActivityAt.Value.ToString("yyyy/MM/dd hh:mm tt")
                        : "-",
                    LastSeenText = log.LastSeenAt.ToString("yyyy/MM/dd hh:mm tt"),
                    CheckInTimeText = attendance == null
                        ? "-"
                        : attendance.CheckInAt.ToString("yyyy/MM/dd hh:mm tt"),
                    CheckOutTimeText = attendance == null || !attendance.CheckOutAt.HasValue
                        ? "-"
                        : attendance.CheckOutAt.Value.ToString("yyyy/MM/dd hh:mm tt"),
                    ShiftText = shift == null
                        ? "-"
                        : $"{shift.ShiftStartTime:hh\\:mm} - {shift.ShiftEndTime:hh\\:mm}",
                    ShiftStartValue = shift == null ? "09:00" : shift.ShiftStartTime.ToString(@"hh\:mm"),
                    ShiftEndValue = shift == null ? "17:00" : shift.ShiftEndTime.ToString(@"hh\:mm"),
                    TotalOnlineText = FormatDuration(totalOnlineSeconds),
                    TotalActiveText = FormatDuration(totalActiveSeconds),
                    WorkWindowText = FormatDuration((int)(workWindowEnd - workWindowStart).TotalSeconds),
                    WorkWindowUntilText = status.Status == "Offline"
                        ? $"حتى آخر ظهور {log.LastSeenAt:hh:mm tt}"
                        : "حتى الآن"
                };
            }).ToList();

            var viewModel = new EmployeeLoginActivityPageViewModel
            {
                FromDate = filterFromDate,
                ToDate = filterToDate,
                Cards = cards
            };

            return View(viewModel);
        }

        [Authorize(Roles = "Admin,ExecutiveDirector")]
        [HttpGet]
        public async Task<IActionResult> LiveStatuses(DateTime? fromDate, DateTime? toDate)
        {
            var today = DateTime.Now.Date;
            var filterFromDate = fromDate?.Date ?? today;
            var filterToDate = toDate?.Date ?? today;

            var logs = await _context.EmployeeActivityLogs
                .AsNoTracking()
                .Where(log => log.ActivityDate >= filterFromDate && log.ActivityDate <= filterToDate)
                .OrderByDescending(log => log.LastSeenAt)
                .ToListAsync();

            var employeeIds = logs
                .Where(log => log.EmployeeId.HasValue)
                .Select(log => log.EmployeeId.Value)
                .Distinct()
                .ToList();

            var shifts = await _context.EmployeeWorkShifts
                .AsNoTracking()
                .Where(shift => shift.IsActive && employeeIds.Contains(shift.EmployeeId))
                .OrderByDescending(shift => shift.Id)
                .Select(shift => new EmployeeActivityShiftViewModel
                {
                    EmployeeId = shift.EmployeeId,
                    ShiftStartTime = shift.ShiftStartTime,
                    ShiftEndTime = shift.ShiftEndTime
                })
                .ToListAsync();

            var shiftMap = shifts
                .GroupBy(shift => shift.EmployeeId)
                .ToDictionary(group => group.Key, group => group.First());

            var attendanceMap = await BuildAttendanceTimeMapAsync(employeeIds, filterFromDate, filterToDate);

            var now = DateTime.Now;

            var items = logs.Select(log =>
            {
                var status = ResolveStatus(log, now);

                var shift = log.EmployeeId.HasValue && shiftMap.ContainsKey(log.EmployeeId.Value)
                    ? shiftMap[log.EmployeeId.Value]
                    : null;

                var shiftStartAt = GetShiftStartDateTime(log.ActivityDate, shift);
                var workWindowStart = shiftStartAt ?? log.FirstSeenAt;
                var workWindowEnd = status.Status == "Offline" ? log.LastSeenAt : now;

                if (workWindowEnd < workWindowStart)
                {
                    workWindowEnd = workWindowStart;
                }

                var totalOnlineSeconds = log.TotalOnlineSeconds;
                var totalActiveSeconds = log.TotalActiveSeconds;

                if (totalOnlineSeconds <= 0)
                {
                    totalOnlineSeconds = EstimateOnlineSeconds(log, now, status);
                }

                if (totalActiveSeconds <= 0 && log.LastActivityAt.HasValue)
                {
                    totalActiveSeconds = EstimateActiveSeconds(log, now, status);
                }

                var attendance = ResolveAttendanceForActivityLog(log, attendanceMap);

                return new
                {
                    id = log.Id,
                    employeeId = log.EmployeeId,
                    status = status.Status,
                    statusText = status.StatusText,
                    statusClass = status.StatusClass,
                    lastActivityText = log.LastActivityAt.HasValue
                        ? log.LastActivityAt.Value.ToString("yyyy/MM/dd hh:mm tt")
                        : "-",
                    lastSeenText = log.LastSeenAt.ToString("yyyy/MM/dd hh:mm tt"),
                    checkInTimeText = attendance == null
                        ? "-"
                        : attendance.CheckInAt.ToString("yyyy/MM/dd hh:mm tt"),
                    checkOutTimeText = attendance == null || !attendance.CheckOutAt.HasValue
                        ? "-"
                        : attendance.CheckOutAt.Value.ToString("yyyy/MM/dd hh:mm tt"),
                    currentPage = string.IsNullOrWhiteSpace(log.CurrentPage) ? "-" : log.CurrentPage,
                    ipAddress = string.IsNullOrWhiteSpace(log.IpAddress) ? "-" : log.IpAddress,
                    totalOnlineText = FormatDuration(totalOnlineSeconds),
                    totalActiveText = FormatDuration(totalActiveSeconds),
                    workWindowText = FormatDuration((int)(workWindowEnd - workWindowStart).TotalSeconds),
                    workWindowUntilText = status.Status == "Offline"
                        ? $"حتى آخر ظهور {log.LastSeenAt:hh:mm tt}"
                        : "حتى الآن"
                };
            }).ToList();

            return Json(new
            {
                success = true,
                now = now.ToString("yyyy/MM/dd hh:mm tt"),
                items
            });
        }

        [Authorize(Roles = "Admin,ExecutiveDirector")]
        [HttpGet]
        public async Task<IActionResult> HourlyStatus(int employeeId, DateTime activityDate, string time)
        {
            if (employeeId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "EmployeeId غير صحيح"
                });
            }

            if (!TimeSpan.TryParse(time, out var requestedTime))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "صيغة الوقت غير صحيحة"
                });
            }

            var hourStartAt = activityDate.Date.Add(new TimeSpan(requestedTime.Hours, 0, 0));
            var hourEndAt = hourStartAt.AddHours(1);

            var hourlyLog = await _context.EmployeeActivityHourlyLogs
                .AsNoTracking()
                .Where(log =>
                    log.EmployeeId == employeeId &&
                    log.HourStartAt == hourStartAt)
                .OrderByDescending(log => log.Id)
                .FirstOrDefaultAsync();

            var shift = await _context.EmployeeWorkShifts
                .AsNoTracking()
                .Where(item => item.IsActive && item.EmployeeId == employeeId)
                .OrderByDescending(item => item.Id)
                .Select(item => new EmployeeActivityShiftViewModel
                {
                    EmployeeId = item.EmployeeId,
                    ShiftStartTime = item.ShiftStartTime,
                    ShiftEndTime = item.ShiftEndTime
                })
                .FirstOrDefaultAsync();

            var isInsideShift = shift == null || IsTimeInsideShift(requestedTime, shift.ShiftStartTime, shift.ShiftEndTime);

            if (hourlyLog == null)
            {
                return Json(new
                {
                    success = true,
                    employeeId,
                    activityDate = activityDate.ToString("yyyy/MM/dd"),
                    hour = $"{hourStartAt:HH:mm} - {hourEndAt:HH:mm}",
                    status = "Offline",
                    statusText = isInsideShift ? "غير متصل / لا يوجد نشاط في هذه الساعة" : "خارج وقت الشيفت",
                    statusClass = isInsideShift ? "offline" : "out-shift",
                    totalOnlineText = "00:00 ساعة",
                    totalActiveText = "00:00 ساعة",
                    firstSeenText = "-",
                    lastSeenText = "-",
                    lastActivityText = "-",
                    currentPage = "-"
                });
            }

            string status;
            string statusText;
            string statusClass;

            if (!isInsideShift)
            {
                status = "OutShift";
                statusText = "خارج وقت الشيفت";
                statusClass = "out-shift";
            }
            else if (hourlyLog.TotalActiveSeconds > 0 || hourlyLog.LastActivityAt.HasValue)
            {
                status = "Active";
                statusText = "فعال في هذه الساعة";
                statusClass = "online";
            }
            else if (hourlyLog.TotalOnlineSeconds > 0 || hourlyLog.LastSeenAt.HasValue)
            {
                status = "Idle";
                statusText = "غير فعال / السيستم كان مفتوح بدون حركة";
                statusClass = "idle";
            }
            else
            {
                status = "Offline";
                statusText = "غير متصل / لا يوجد نشاط في هذه الساعة";
                statusClass = "offline";
            }

            return Json(new
            {
                success = true,
                employeeId,
                activityDate = activityDate.ToString("yyyy/MM/dd"),
                hour = $"{hourStartAt:HH:mm} - {hourEndAt:HH:mm}",
                status,
                statusText,
                statusClass,
                totalOnlineText = FormatDuration(hourlyLog.TotalOnlineSeconds),
                totalActiveText = FormatDuration(hourlyLog.TotalActiveSeconds),
                firstSeenText = hourlyLog.FirstSeenAt.HasValue ? hourlyLog.FirstSeenAt.Value.ToString("yyyy/MM/dd hh:mm tt") : "-",
                lastSeenText = hourlyLog.LastSeenAt.HasValue ? hourlyLog.LastSeenAt.Value.ToString("yyyy/MM/dd hh:mm tt") : "-",
                lastActivityText = hourlyLog.LastActivityAt.HasValue ? hourlyLog.LastActivityAt.Value.ToString("yyyy/MM/dd hh:mm tt") : "-",
                currentPage = string.IsNullOrWhiteSpace(hourlyLog.CurrentPage) ? "-" : hourlyLog.CurrentPage
            });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Heartbeat([FromBody] EmployeeActivityHeartbeatRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var now = DateTime.Now;
            var today = now.Date;

            var isMonitoringPage = request.IsMonitoringPage || IsEmployeeActivityMonitoringPage(request.CurrentPage);
            var shouldTrackAsWork = !isMonitoringPage;

            var employee = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.ApplicationUserId == userId);

            var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "";

            var employeeName = employee == null
                ? userEmail
                : string.IsNullOrWhiteSpace(employee.DisplayName)
                    ? (string.IsNullOrWhiteSpace(employee.Name) ? userEmail : employee.Name)
                    : employee.DisplayName;

            var employeeImageUrl = employee?.ImageUrl ?? "";

            if (!string.IsNullOrWhiteSpace(employeeImageUrl) && !employeeImageUrl.StartsWith("/"))
            {
                employeeImageUrl = "/" + employeeImageUrl;
            }

            var ipAddress = GetClientIpAddress();
            var userAgent = Request.Headers["User-Agent"].ToString();

            var log = await _context.EmployeeActivityLogs
                .FirstOrDefaultAsync(activityLog =>
                    activityLog.UserId == userId &&
                    activityLog.ActivityDate == today);

            DateTime? previousHeartbeatAt = null;

            if (log == null)
            {
                log = new EmployeeActivityLog
                {
                    UserId = userId,
                    EmployeeId = employee?.Id,
                    EmployeeName = employeeName,
                    EmployeeEmail = userEmail,
                    EmployeeImageUrl = employeeImageUrl,
                    ActivityDate = today,
                    FirstSeenAt = now,
                    LastSeenAt = now,
                    LastActivityAt = shouldTrackAsWork && request.HasActivity ? now : null,
                    LastHeartbeatAt = shouldTrackAsWork ? now : null,
                    TotalOnlineSeconds = 0,
                    TotalActiveSeconds = 0,
                    CurrentPage = shouldTrackAsWork ? CleanPageUrl(request.CurrentPage) : "-",
                    IsTabActive = request.IsTabActive,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    CreatedAt = now
                };

                _context.EmployeeActivityLogs.Add(log);
            }
            else
            {
                previousHeartbeatAt = log.LastHeartbeatAt ?? log.LastSeenAt;

                if (shouldTrackAsWork)
                {
                    var heartbeatGapSeconds = (int)Math.Round((now - previousHeartbeatAt.Value).TotalSeconds);

                    if (heartbeatGapSeconds > 0 && heartbeatGapSeconds <= MaximumContinuousHeartbeatSeconds)
                    {
                        log.TotalOnlineSeconds += heartbeatGapSeconds;

                        if (request.HasActivity && request.IsTabActive)
                        {
                            log.TotalActiveSeconds += heartbeatGapSeconds;
                        }
                    }
                }

                log.EmployeeId = employee?.Id ?? log.EmployeeId;
                log.EmployeeName = string.IsNullOrWhiteSpace(employeeName) ? log.EmployeeName : employeeName;
                log.EmployeeEmail = string.IsNullOrWhiteSpace(userEmail) ? log.EmployeeEmail : userEmail;
                log.EmployeeImageUrl = string.IsNullOrWhiteSpace(employeeImageUrl) ? log.EmployeeImageUrl : employeeImageUrl;

                // LastSeenAt means the browser is still connected.
                // We update it even on the monitoring page so the user is not marked offline incorrectly.
                log.LastSeenAt = now;

                if (shouldTrackAsWork)
                {
                    log.LastHeartbeatAt = now;

                    if (request.HasActivity)
                    {
                        log.LastActivityAt = now;
                    }

                    log.CurrentPage = CleanPageUrl(request.CurrentPage);
                }

                log.IsTabActive = request.IsTabActive;
                log.IpAddress = ipAddress;
                log.UserAgent = userAgent;
                log.UpdatedAt = now;
            }

            if (shouldTrackAsWork)
            {
                await UpdateHourlyActivityAsync(
                    userId,
                    employee?.Id,
                    employeeName,
                    userEmail,
                    employeeImageUrl,
                    previousHeartbeatAt,
                    now,
                    request,
                    ipAddress,
                    userAgent
                );
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                lastSeenAt = now
            });
        }

        private async Task UpdateHourlyActivityAsync(
            string userId,
            int? employeeId,
            string employeeName,
            string userEmail,
            string employeeImageUrl,
            DateTime? previousHeartbeatAt,
            DateTime now,
            EmployeeActivityHeartbeatRequest request,
            string ipAddress,
            string userAgent)
        {
            var hasContinuousGap = false;
            var segmentStart = now;

            if (previousHeartbeatAt.HasValue)
            {
                var gapSeconds = (int)Math.Round((now - previousHeartbeatAt.Value).TotalSeconds);

                if (gapSeconds > 0 && gapSeconds <= MaximumContinuousHeartbeatSeconds)
                {
                    hasContinuousGap = true;
                    segmentStart = previousHeartbeatAt.Value;
                }
            }

            if (!hasContinuousGap)
            {
                await TouchHourlyLogAsync(
                    userId,
                    employeeId,
                    employeeName,
                    userEmail,
                    employeeImageUrl,
                    now,
                    now,
                    0,
                    request.HasActivity,
                    request.IsTabActive,
                    request.CurrentPage,
                    ipAddress,
                    userAgent
                );

                return;
            }

            var cursor = segmentStart;

            while (cursor < now)
            {
                var currentHourStart = GetHourStart(cursor);
                var nextHourStart = currentHourStart.AddHours(1);
                var segmentEnd = nextHourStart < now ? nextHourStart : now;
                var seconds = (int)Math.Round((segmentEnd - cursor).TotalSeconds);

                if (seconds > 0)
                {
                    await TouchHourlyLogAsync(
                        userId,
                        employeeId,
                        employeeName,
                        userEmail,
                        employeeImageUrl,
                        cursor,
                        segmentEnd,
                        seconds,
                        request.HasActivity,
                        request.IsTabActive,
                        request.CurrentPage,
                        ipAddress,
                        userAgent
                    );
                }

                cursor = segmentEnd;
            }
        }

        private async Task TouchHourlyLogAsync(
            string userId,
            int? employeeId,
            string employeeName,
            string userEmail,
            string employeeImageUrl,
            DateTime segmentStart,
            DateTime segmentEnd,
            int secondsToAdd,
            bool hasActivity,
            bool isTabActive,
            string? currentPage,
            string ipAddress,
            string userAgent)
        {
            var hourStartAt = GetHourStart(segmentStart);
            var hourEndAt = hourStartAt.AddHours(1);

            var hourlyLog = await _context.EmployeeActivityHourlyLogs
                .FirstOrDefaultAsync(log =>
                    log.UserId == userId &&
                    log.HourStartAt == hourStartAt);

            if (hourlyLog == null)
            {
                hourlyLog = new EmployeeActivityHourlyLog
                {
                    UserId = userId,
                    EmployeeId = employeeId,
                    EmployeeName = employeeName,
                    EmployeeEmail = userEmail,
                    EmployeeImageUrl = employeeImageUrl,
                    ActivityDate = hourStartAt.Date,
                    HourStartAt = hourStartAt,
                    HourEndAt = hourEndAt,
                    FirstSeenAt = segmentStart,
                    LastSeenAt = segmentEnd,
                    LastActivityAt = hasActivity ? segmentEnd : null,
                    TotalOnlineSeconds = 0,
                    TotalActiveSeconds = 0,
                    CurrentPage = CleanPageUrl(currentPage),
                    IsTabActive = isTabActive,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    CreatedAt = DateTime.Now
                };

                _context.EmployeeActivityHourlyLogs.Add(hourlyLog);
            }
            else
            {
                hourlyLog.EmployeeId = employeeId ?? hourlyLog.EmployeeId;
                hourlyLog.EmployeeName = string.IsNullOrWhiteSpace(employeeName) ? hourlyLog.EmployeeName : employeeName;
                hourlyLog.EmployeeEmail = string.IsNullOrWhiteSpace(userEmail) ? hourlyLog.EmployeeEmail : userEmail;
                hourlyLog.EmployeeImageUrl = string.IsNullOrWhiteSpace(employeeImageUrl) ? hourlyLog.EmployeeImageUrl : employeeImageUrl;
                hourlyLog.LastSeenAt = segmentEnd;

                if (hasActivity)
                {
                    hourlyLog.LastActivityAt = segmentEnd;
                }

                hourlyLog.CurrentPage = CleanPageUrl(currentPage);
                hourlyLog.IsTabActive = isTabActive;
                hourlyLog.IpAddress = ipAddress;
                hourlyLog.UserAgent = userAgent;
                hourlyLog.UpdatedAt = DateTime.Now;
            }

            if (secondsToAdd > 0)
            {
                hourlyLog.TotalOnlineSeconds += secondsToAdd;

                if (hasActivity && isTabActive)
                {
                    hourlyLog.TotalActiveSeconds += secondsToAdd;
                }
            }
        }

        private async Task<Dictionary<string, EmployeeActivityAttendanceTimeViewModel>> BuildAttendanceTimeMapAsync(
            List<int> employeeIds,
            DateTime filterFromDate,
            DateTime filterToDate)
        {
            var map = new Dictionary<string, EmployeeActivityAttendanceTimeViewModel>();

            if (employeeIds == null || employeeIds.Count == 0)
            {
                return map;
            }

            var fromDateTime = filterFromDate.Date;
            var toDateTimeExclusive = filterToDate.Date.AddDays(1);

            var attendanceRows = await _context.EmployeeAttendanceLogs
                .AsNoTracking()
                .Where(log =>
                    log.EmployeeId.HasValue &&
                    employeeIds.Contains(log.EmployeeId.Value) &&
                    log.CheckInAt >= fromDateTime &&
                    log.CheckInAt < toDateTimeExclusive)
                .Select(log => new EmployeeActivityAttendanceTimeViewModel
                {
                    EmployeeId = log.EmployeeId.Value,
                    AttendanceDate = log.CheckInAt.Date,
                    CheckInAt = log.CheckInAt,
                    CheckOutAt = log.CheckOutAt
                })
                .ToListAsync();

            foreach (var group in attendanceRows
                .GroupBy(row => BuildEmployeeDateKey(row.EmployeeId, row.AttendanceDate)))
            {
                var selectedRow = group
                    .OrderByDescending(row => row.CheckInAt)
                    .FirstOrDefault();

                if (selectedRow != null)
                {
                    map[group.Key] = selectedRow;
                }
            }

            return map;
        }

        private static EmployeeActivityAttendanceTimeViewModel? ResolveAttendanceForActivityLog(
            EmployeeActivityLog log,
            Dictionary<string, EmployeeActivityAttendanceTimeViewModel> attendanceMap)
        {
            if (!log.EmployeeId.HasValue)
            {
                return null;
            }

            var key = BuildEmployeeDateKey(log.EmployeeId.Value, log.ActivityDate.Date);

            return attendanceMap.TryGetValue(key, out var attendance)
                ? attendance
                : null;
        }

        private static string BuildEmployeeDateKey(int employeeId, DateTime date)
        {
            return employeeId.ToString() + "|" + date.ToString("yyyyMMdd");
        }

        private string GetClientIpAddress()
        {
            var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                return forwardedFor.Split(',').First().Trim();
            }

            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
        }

        private static string CleanPageUrl(string? pageUrl)
        {
            if (string.IsNullOrWhiteSpace(pageUrl))
            {
                return "/";
            }

            pageUrl = pageUrl.Trim();

            if (pageUrl.Length > 500)
            {
                pageUrl = pageUrl.Substring(0, 500);
            }

            return pageUrl;
        }

        private static bool IsEmployeeActivityMonitoringPage(string? pageUrl)
        {
            if (string.IsNullOrWhiteSpace(pageUrl))
            {
                return false;
            }

            return pageUrl.StartsWith("/EmployeeActivity/LoginSessions", StringComparison.OrdinalIgnoreCase);
        }

        private static DateTime GetHourStart(DateTime value)
        {
            return new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0);
        }

        private static DateTime? GetShiftStartDateTime(DateTime activityDate, EmployeeActivityShiftViewModel? shift)
        {
            if (shift == null)
            {
                return null;
            }

            return activityDate.Date.Add(shift.ShiftStartTime);
        }

        private static bool IsTimeInsideShift(TimeSpan requestedTime, TimeSpan shiftStart, TimeSpan shiftEnd)
        {
            if (shiftStart == shiftEnd)
            {
                return true;
            }

            if (shiftEnd > shiftStart)
            {
                return requestedTime >= shiftStart && requestedTime < shiftEnd;
            }

            return requestedTime >= shiftStart || requestedTime < shiftEnd;
        }

        private static int EstimateOnlineSeconds(EmployeeActivityLog log, DateTime now, EmployeeLoginActivityStatus status)
        {
            var end = status.Status == "Offline" ? log.LastSeenAt : now;

            if (end < log.FirstSeenAt)
            {
                return 0;
            }

            return (int)(end - log.FirstSeenAt).TotalSeconds;
        }

        private static int EstimateActiveSeconds(EmployeeActivityLog log, DateTime now, EmployeeLoginActivityStatus status)
        {
            if (!log.LastActivityAt.HasValue)
            {
                return 0;
            }

            var end = status.Status == "Offline" ? log.LastActivityAt.Value : now;

            if (end < log.FirstSeenAt)
            {
                return 0;
            }

            var estimatedSeconds = (int)(end - log.FirstSeenAt).TotalSeconds;

            return Math.Max(0, Math.Min(estimatedSeconds, EstimateOnlineSeconds(log, now, status)));
        }

        private static string FormatDuration(int totalSeconds)
        {
            if (totalSeconds < 0)
            {
                totalSeconds = 0;
            }

            var time = TimeSpan.FromSeconds(totalSeconds);
            var totalHours = (int)Math.Floor(time.TotalHours);

            return $"{totalHours:00}:{time.Minutes:00} ساعة";
        }

        private static EmployeeLoginActivityStatus ResolveStatus(EmployeeActivityLog log, DateTime now)
        {
            var minutesSinceLastSeen = (now - log.LastSeenAt).TotalMinutes;

            if (minutesSinceLastSeen > 10)
            {
                return new EmployeeLoginActivityStatus
                {
                    Status = "Offline",
                    StatusText = "غير نشط",
                    StatusClass = "offline"
                };
            }

            if (log.LastActivityAt.HasValue)
            {
                var minutesSinceLastActivity = (now - log.LastActivityAt.Value).TotalMinutes;

                if (minutesSinceLastActivity < 3)
                {
                    return new EmployeeLoginActivityStatus
                    {
                        Status = "Online",
                        StatusText = "نشط",
                        StatusClass = "online"
                    };
                }
            }

            return new EmployeeLoginActivityStatus
            {
                Status = "Idle",
                StatusText = "غير متفاعل مؤقتا",
                StatusClass = "idle"
            };
        }
    }

    public class EmployeeActivityHeartbeatRequest
    {
        public bool HasActivity { get; set; }

        public string? CurrentPage { get; set; }

        public bool IsTabActive { get; set; }

        public bool IsMonitoringPage { get; set; }
    }

    public class EmployeeLoginActivityPageViewModel
    {
        public DateTime FromDate { get; set; }

        public DateTime ToDate { get; set; }

        public List<EmployeeLoginActivityCardViewModel> Cards { get; set; } = new List<EmployeeLoginActivityCardViewModel>();
    }

    public class EmployeeLoginActivityCardViewModel
    {
        public int Id { get; set; }

        public int? EmployeeId { get; set; }

        public string EmployeeName { get; set; } = "";

        public string EmployeeEmail { get; set; } = "";

        public string EmployeeImageUrl { get; set; } = "";

        public DateTime ActivityDate { get; set; }

        public DateTime FirstSeenAt { get; set; }

        public DateTime LastSeenAt { get; set; }

        public DateTime? LastActivityAt { get; set; }

        public string CurrentPage { get; set; } = "";

        public string IpAddress { get; set; } = "";

        public bool IsTabActive { get; set; }

        public string Status { get; set; } = "";

        public string StatusText { get; set; } = "";

        public string StatusClass { get; set; } = "";

        public string LastActivityText { get; set; } = "";

        public string LastSeenText { get; set; } = "";

        public string CheckInTimeText { get; set; } = "";

        public string CheckOutTimeText { get; set; } = "";

        public string ShiftText { get; set; } = "";

        public string ShiftStartValue { get; set; } = "";

        public string ShiftEndValue { get; set; } = "";

        public string TotalOnlineText { get; set; } = "";

        public string TotalActiveText { get; set; } = "";

        public string WorkWindowText { get; set; } = "";

        public string WorkWindowUntilText { get; set; } = "";
    }

    public class EmployeeActivityShiftViewModel
    {
        public int EmployeeId { get; set; }

        public TimeSpan ShiftStartTime { get; set; }

        public TimeSpan ShiftEndTime { get; set; }
    }

    public class EmployeeActivityAttendanceTimeViewModel
    {
        public int EmployeeId { get; set; }

        public DateTime AttendanceDate { get; set; }

        public DateTime CheckInAt { get; set; }

        public DateTime? CheckOutAt { get; set; }
    }

    public class EmployeeLoginActivityStatus
    {
        public string Status { get; set; } = "";

        public string StatusText { get; set; } = "";

        public string StatusClass { get; set; } = "";
    }
}
