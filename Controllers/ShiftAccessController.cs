using Crm_LotusBlue.Models;
using lotus_blue.Data;
using lotus_blue.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Crm_LotusBlue.Controllers
{
    [Authorize]
    public class ShiftAccessController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public ShiftAccessController(
            ApplicationDbContext context,
            SignInManager<ApplicationUser> signInManager)
        {
            _context = context;
            _signInManager = signInManager;
        }

        [HttpGet]
        public async Task<IActionResult> CheckCurrentShift()
        {
            try
            {
                if (ShouldSkipShiftAccessForCurrentUser())
                {
                    return Json(new
                    {
                        success = true,
                        shouldLogout = false,
                        message = ""
                    });
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrWhiteSpace(userId))
                {
                    return Json(new
                    {
                        success = false,
                        shouldLogout = false,
                        message = "لم يتم العثور على المستخدم الحالي"
                    });
                }

                var employee = await _context.Employees
                    .AsNoTracking()
                    .Where(e => e.ApplicationUserId == userId)
                    .Select(e => new
                    {
                        e.Id,
                        e.Name,
                        e.DisplayName
                    })
                    .FirstOrDefaultAsync();

                if (employee == null)
                {
                    return Json(new
                    {
                        success = true,
                        shouldLogout = false,
                        message = ""
                    });
                }

                if (!await GetEmployeeApplyShiftAccessAsync(employee.Id))
                {
                    return Json(new
                    {
                        success = true,
                        shouldLogout = false,
                        message = ""
                    });
                }

                var shift = await _context.EmployeeWorkShifts
                    .Where(s => s.EmployeeId == employee.Id && s.IsActive)
                    .OrderByDescending(s => s.Id)
                    .FirstOrDefaultAsync();

                if (shift == null)
                {
                    return Json(new
                    {
                        success = true,
                        shouldLogout = false,
                        message = ""
                    });
                }

                var now = GetEgyptNow();

                if (IsAdminUnblockActive(shift, now))
                {
                    if (shift.IsLoginBlocked)
                    {
                        shift.IsLoginBlocked = false;
                        shift.UpdatedAt = now;
                        await _context.SaveChangesAsync();
                    }

                    return Json(new
                    {
                        success = true,
                        shouldLogout = false,
                        message = ""
                    });
                }

                var shiftStart = shift.ShiftStartTime;
                var shiftEnd = shift.ShiftEndTime;
                var shiftWindow = BuildShiftWindow(now, shiftStart, shiftEnd);

                if (now < shiftWindow.AccessStart || now >= shiftWindow.EndWithGrace)
                {
                    var afterShiftEnd = now >= shiftWindow.EndWithGrace;
                    var outsideShiftMessage = BuildOutsideShiftMessage(now, shiftStart, shiftEnd, shiftWindow, afterShiftEnd);

                    shift.IsLoginBlocked = true;
                    shift.LoginBlockedAt = now;
                    shift.LoginBlockReason = afterShiftEnd
                        ? "انتهى موعد الدوام وتم عمل بلوك تلقائي"
                        : "محاولة استخدام النظام قبل بداية موعد الدوام";
                    shift.UpdatedAt = now;

                    if (afterShiftEnd)
                    {
                        shift.AdminUnblockedUntil = null;
                        shift.AdminUnblockedAt = null;
                        shift.AdminUnblockedByUserId = null;
                    }

                    await CloseOpenAttendanceLogAsync(
                        userId,
                        employee.Id,
                        now,
                        afterShiftEnd
                            ? "تسجيل خروج تلقائي بعد نهاية الشيفت بنصف ساعة"
                            : "تسجيل خروج تلقائي خارج وقت الشيفت");

                    await _context.SaveChangesAsync();
                    await ForceSignOutAsync();

                    return Json(new
                    {
                        success = true,
                        shouldLogout = true,
                        redirectUrl = BuildLoginRedirectUrl(outsideShiftMessage),
                        message = outsideShiftMessage
                    });
                }

                if (shift.IsLoginBlocked)
                {
                    shift.IsLoginBlocked = false;
                    shift.LoginBlockedAt = null;
                    shift.LoginBlockReason = null;
                    shift.UpdatedAt = now;

                    await _context.SaveChangesAsync();
                }

                return Json(new
                {
                    success = true,
                    shouldLogout = false,
                    message = ""
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    shouldLogout = false,
                    message = ex.Message
                });
            }
        }

        private bool ShouldSkipShiftAccessForCurrentUser()
        {
            return User.IsInRole("Admin")
                || User.IsInRole("ExecutiveDirector")
                || User.IsInRole("DeliveryCompany")
                || User.IsInRole("DeliveryRepresentative")
                || User.IsInRole("OrderPreparer");
        }



        private async Task<bool> GetEmployeeApplyShiftAccessAsync(int employeeId)
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
IF COL_LENGTH('dbo.Employees', 'ApplyShiftAccess') IS NULL
BEGIN
    SELECT CAST(1 AS BIT);
END
ELSE
BEGIN
    SELECT TOP 1 CAST(ISNULL(ApplyShiftAccess, 1) AS BIT)
    FROM dbo.Employees
    WHERE Id = @EmployeeId;
END";

                    var parameter = command.CreateParameter();
                    parameter.ParameterName = "@EmployeeId";
                    parameter.Value = employeeId;
                    command.Parameters.Add(parameter);

                    var result = await command.ExecuteScalarAsync();
                    return result == null || result == DBNull.Value ? true : Convert.ToBoolean(result);
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
                return true;
            }
        }

        private async Task CloseOpenAttendanceLogAsync(string userId, int employeeId, DateTime checkOutAt, string reason)
        {
            var openLog = await _context.EmployeeAttendanceLogs
                .Where(log =>
                    log.UserId == userId &&
                    log.EmployeeId == employeeId &&
                    log.CheckOutAt == null)
                .OrderByDescending(log => log.CheckInAt)
                .FirstOrDefaultAsync();

            if (openLog == null)
            {
                return;
            }

            openLog.CheckOutAt = checkOutAt;
            openLog.CheckOutIpAddress = GetCurrentIpAddress();
            openLog.CheckOutLocation = reason;
            openLog.UpdatedAt = checkOutAt;

            if (string.IsNullOrWhiteSpace(openLog.Notes))
            {
                openLog.Notes = reason;
            }
            else if (!openLog.Notes.Contains(reason))
            {
                openLog.Notes = openLog.Notes + " - " + reason;
            }
        }

        private async Task ForceSignOutAsync()
        {
            await _signInManager.SignOutAsync();
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

            Response.Cookies.Delete(".AspNetCore.Identity.Application");
            Response.Cookies.Delete("Identity.Application");
            Response.Cookies.Delete("LuxiraRequireCheckInFaceCapture");
            Response.Cookies.Delete("LoginWelcomeText");
            Response.Cookies.Delete("LuxiraLoginPreferredEmail");
            Response.Cookies.Delete("LuxiraShiftAutoLogoutMessage");
        }

        private string BuildLoginRedirectUrl(string message)
        {
            return "/Identity/Account/Login?manualLogin=true&shiftMessage=" + Uri.EscapeDataString(message ?? "");
        }

        private static bool IsAdminUnblockActive(EmployeeWorkShift shift, DateTime now)
        {
            return shift.AdminUnblockedUntil.HasValue &&
                   shift.AdminUnblockedUntil.Value > now;
        }

        private static string FormatShiftTime(TimeSpan time)
        {
            return DateTime.Today.Add(time).ToString("HH:mm");
        }

        private sealed class ShiftWindow
        {
            public DateTime Start { get; set; }

            public DateTime AccessStart { get; set; }

            public DateTime End { get; set; }

            public DateTime EndWithGrace { get; set; }
        }

        private static ShiftWindow BuildShiftWindow(DateTime now, TimeSpan shiftStartTime, TimeSpan shiftEndTime)
        {
            var todayStart = now.Date.Add(shiftStartTime);
            var crossesMidnight = shiftEndTime <= shiftStartTime;

            if (!crossesMidnight)
            {
                var todayEnd = now.Date.Add(shiftEndTime);

                return new ShiftWindow
                {
                    Start = todayStart,
                    AccessStart = todayStart.AddMinutes(-30),
                    End = todayEnd,
                    EndWithGrace = todayEnd.AddMinutes(30)
                };
            }

            var todayOvernightStart = now.Date.Add(shiftStartTime);
            var todayOvernightEnd = now.Date.AddDays(1).Add(shiftEndTime);

            if (now >= todayOvernightStart.AddMinutes(-30))
            {
                return new ShiftWindow
                {
                    Start = todayOvernightStart,
                    AccessStart = todayOvernightStart.AddMinutes(-30),
                    End = todayOvernightEnd,
                    EndWithGrace = todayOvernightEnd.AddMinutes(30)
                };
            }

            var yesterdayOvernightStart = now.Date.AddDays(-1).Add(shiftStartTime);
            var yesterdayOvernightEnd = now.Date.Add(shiftEndTime);
            var yesterdayOvernightEndWithGrace = yesterdayOvernightEnd.AddMinutes(30);

            if (now < yesterdayOvernightEndWithGrace)
            {
                return new ShiftWindow
                {
                    Start = yesterdayOvernightStart,
                    AccessStart = yesterdayOvernightStart.AddMinutes(-30),
                    End = yesterdayOvernightEnd,
                    EndWithGrace = yesterdayOvernightEndWithGrace
                };
            }

            return new ShiftWindow
            {
                Start = todayOvernightStart,
                AccessStart = todayOvernightStart.AddMinutes(-30),
                End = todayOvernightEnd,
                EndWithGrace = todayOvernightEnd.AddMinutes(30)
            };
        }

        private static string BuildOutsideShiftMessage(
            DateTime now,
            TimeSpan shiftStart,
            TimeSpan shiftEnd,
            ShiftWindow shiftWindow,
            bool afterShiftEnd)
        {
            if (afterShiftEnd)
            {
                return $"تم انتهاء موعد دوامك، لا يمكنك الدخول الآن. سيتم السماح بالدخول مرة أخرى قبل بداية دوامك بنصف ساعة.";
            }

            return $"لا يمكنك الدخول الآن. مسموح بالدخول قبل بداية دوامك بنصف ساعة، بداية من الساعة {shiftWindow.AccessStart:HH:mm}";
        }

        private string GetCurrentIpAddress()
        {
            var forwardedFor = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                var firstIp = forwardedFor.Split(',').FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(firstIp))
                {
                    return NormalizeIp(firstIp);
                }
            }

            var realIp = HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(realIp))
            {
                return NormalizeIp(realIp);
            }

            var remoteIp = HttpContext.Connection.RemoteIpAddress;

            if (remoteIp != null)
            {
                if (remoteIp.IsIPv4MappedToIPv6)
                {
                    remoteIp = remoteIp.MapToIPv4();
                }

                if (IPAddress.IsLoopback(remoteIp))
                {
                    return "127.0.0.1";
                }

                return remoteIp.ToString();
            }

            return "";
        }

        private static string NormalizeIp(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
            {
                return "";
            }

            ip = ip.Trim();

            if (ip.Contains(","))
            {
                ip = ip.Split(',')[0].Trim();
            }

            if (ip == "::1")
            {
                return "127.0.0.1";
            }

            if (ip.StartsWith("::ffff:"))
            {
                ip = ip.Replace("::ffff:", "");
            }

            if (IPAddress.TryParse(ip, out var parsedIp))
            {
                if (parsedIp.IsIPv4MappedToIPv6)
                {
                    parsedIp = parsedIp.MapToIPv4();
                }

                if (IPAddress.IsLoopback(parsedIp))
                {
                    return "127.0.0.1";
                }

                return parsedIp.ToString();
            }

            return ip;
        }

        private static DateTime GetEgyptNow()
        {
            var utcNow = DateTime.UtcNow;

            foreach (var timeZoneId in new[] { "Egypt Standard Time", "Africa/Cairo" })
            {
                try
                {
                    var egyptTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                    return TimeZoneInfo.ConvertTimeFromUtc(utcNow, egyptTimeZone);
                }
                catch
                {
                    // Try the next time zone id because Windows and Linux use different ids.
                }
            }

            return DateTime.Now;
        }
    }
}
