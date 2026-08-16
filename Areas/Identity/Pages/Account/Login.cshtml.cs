// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Crm_LotusBlue.Models;
using lotus_blue.Data;
using lotus_blue.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace lotus_blue.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly ApplicationDbContext _context;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<LoginModel> logger,
            ApplicationDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        [BindProperty]
        public bool ManualLogin { get; set; }

        public List<LoginEmployeeProfile> EmployeeLoginProfiles { get; set; } = new List<LoginEmployeeProfile>();

        public class InputModel
        {
            public string ClientPublicIp { get; set; } = "";

            public string ClientIpLocation { get; set; } = "";

            public string ClientDeviceType { get; set; } = "";

            public string ClientUserAgent { get; set; } = "";

            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public class LoginEmployeeProfile
        {
            public string Email { get; set; } = "";

            public string Name { get; set; } = "";

            public string ImageUrl { get; set; } = "/static/DefaultImage.svg";

            public string AllowedIpAddress { get; set; } = "";
        }

        public async Task OnGetAsync(string returnUrl = null, bool manualLogin = false, string selectedEmail = null, string shiftMessage = null)
        {
            if (!string.IsNullOrWhiteSpace(shiftMessage))
            {
                ErrorMessage = shiftMessage;
                TempData["LoginErrorMessage"] = shiftMessage;
            }

            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            ManualLogin = manualLogin;

            ReturnUrl = Url.Content("~/Home/Index");

            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            await LoadEmployeeLoginProfilesAsync();

            Input ??= new InputModel();

            if (!string.IsNullOrWhiteSpace(selectedEmail))
            {
                Input.Email = selectedEmail.Trim();
                ManualLogin = false;
            }
            else if (Request.Cookies.TryGetValue("LuxiraLoginPreferredEmail", out var preferredEmailCookie)
                && !string.IsNullOrWhiteSpace(preferredEmailCookie))
            {
                Input.Email = preferredEmailCookie.Trim();
                ManualLogin = false;
            }
            else if (string.IsNullOrWhiteSpace(Input?.Email) && EmployeeLoginProfiles.Any())
            {
                Input.Email = EmployeeLoginProfiles.First().Email;
            }

            if (!string.IsNullOrWhiteSpace(Input.Email))
            {
                Response.Cookies.Delete("LuxiraLoginPreferredEmail");
            }
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            var homeUrl = Url.Content("~/Home/Index");

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            await LoadEmployeeLoginProfilesAsync();

            if (ModelState.IsValid)
            {
                Input.Email = (Input.Email ?? string.Empty).Trim();

                var user = await _userManager.FindByEmailAsync(Input.Email);

                if (user == null)
                {
                    AddLoginError("البريد الإلكتروني غير مسجل أو غير صحيح.");
                    ManualLogin = true;
                    return Page();
                }

                /*
                    نخلي تسجيل الدخول Persistent لحد ما الموظف يعمل Logout بإيده.
                    كده لو قفل المتصفح وفتحه تاني وهو لسه ماعملش Logout،
                    الحساب يفضل مفتوح ومش يطلب صورة حضور مرة تانية.
                */
                var result = await _signInManager.PasswordSignInAsync(
                    user,
                    Input.Password,
                    isPersistent: true,
                    lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User logged in.");

                    var deviceAccessValidation = await ValidateMobileAndTabletAccessAsync(user);

                    if (!deviceAccessValidation.IsValid)
                    {
                        await ClearFailedLoginSessionAsync();

                        AddLoginError(deviceAccessValidation.Message);
                        return Page();
                    }

                    var currentLoginIp = GetCurrentLoginPublicIp();

                    var isIpCheckExempt = await IsIpCheckExemptUserAsync(user);

                    /*
                        تحقق IP مرن للموظفين فقط:
                        - Admin و ExecutiveDirector و CallCenter مستثنين من فحص IP.
                        - باقي الموظفين: يتم قراءة IP الحالي وحفظه على الشيفت.
                        - لو IP مختلف أو جديد، يتم حفظه ولا يتم منع تسجيل الدخول.
                        - لو لا يوجد شيفت فعال، ننشئ شيفت فعال ونحفظ IP الحالي.
                    */
                    if (!isIpCheckExempt)
                    {
                        var ipValidation = await ValidateAndAttachEmployeeIpAsync(user.Id, currentLoginIp);

                        if (!ipValidation.IsValid)
                        {
                            await ClearFailedLoginSessionAsync();

                            AddLoginError(ipValidation.Message);
                            return Page();
                        }
                    }

                    var shiftAccessValidation = await ValidateShiftAccessAsync(user);

                    if (!shiftAccessValidation.IsValid)
                    {
                        await ClearFailedLoginSessionAsync();

                        AddLoginError(shiftAccessValidation.Message);
                        return Page();
                    }

                    /*
                        لو المستخدم كان IsActive = 0 ودخل من "إضافة مستخدم"
                        نخليه Active بعد نجاح تسجيل الدخول والتحقق من الـ IP.
                    */
                    await ActivateEmployeeAfterSuccessfulLoginAsync(user.Id);

                    /*
                        صورة الحضور مطلوبة فقط بعد Login جديد.
                        لو الموظف قفل المتصفح وفتحه تاني بدون Logout، مش هيعدي على OnPostAsync
                        وبالتالي مش هنعيد طلب الصورة.
                    */
                    TempData["RequireCheckInFaceCapture"] = "1";
                    TempData["CheckInIpAddress"] = currentLoginIp;

                    TempData["CheckInLocation"] = string.IsNullOrWhiteSpace(Input?.ClientIpLocation)
                        ? ""
                        : Input.ClientIpLocation.Trim();

                    /*
                        Cookie احتياطي لو TempData اتفقدت بسبب Redirect/Refresh.
                        الصفحة الرئيسية تمسحه مباشرة بعد نجاح صورة الحضور أو لو السيرفر أكد أن الصورة مسجلة.
                    */
                    Response.Cookies.Append(
                        "LuxiraRequireCheckInFaceCapture",
                        "1",
                        new Microsoft.AspNetCore.Http.CookieOptions
                        {
                            Path = "/",
                            HttpOnly = false,
                            IsEssential = true,
                            SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                            Secure = Request.IsHttps,
                            Expires = DateTimeOffset.UtcNow.AddDays(7)
                        });

                    var loginWelcomeText = await BuildLoginWelcomeTextAsync(user.Id);

                    if (!string.IsNullOrWhiteSpace(loginWelcomeText))
                    {
                        TempData["LoginWelcomeText"] = loginWelcomeText;

                        Response.Cookies.Append(
                            "LoginWelcomeText",
                            WebUtility.UrlEncode(loginWelcomeText),
                            new Microsoft.AspNetCore.Http.CookieOptions
                            {
                                HttpOnly = false,
                                IsEssential = true,
                                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                                Secure = Request.IsHttps,
                                Expires = DateTimeOffset.UtcNow.AddMinutes(2)
                            });
                    }

                    return LocalRedirect(homeUrl);
                }

                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new
                    {
                        ReturnUrl = homeUrl,
                        RememberMe = Input.RememberMe
                    });
                }

                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    AddLoginError("تم قفل الحساب مؤقتًا بسبب محاولات تسجيل دخول كثيرة. برجاء المحاولة لاحقًا أو مراجعة الإدارة.");
                    return Page();
                }

                if (result.IsNotAllowed)
                {
                    AddLoginError("غير مسموح بتسجيل الدخول لهذا الحساب. برجاء التأكد من تفعيل الحساب أو مراجعة الإدارة.");
                    return Page();
                }

                AddLoginError("كلمة المرور غير صحيحة أو بيانات تسجيل الدخول غير صحيحة.");
                return Page();
            }

            return Page();
        }

        private void AddLoginError(string message)
        {
            ErrorMessage = message;
            TempData["LoginErrorMessage"] = message;
            ModelState.AddModelError(string.Empty, message);
        }

        private async Task<DeviceAccessValidationResult> ValidateMobileAndTabletAccessAsync(ApplicationUser user)
        {
            if (user == null)
            {
                return new DeviceAccessValidationResult
                {
                    IsValid = false,
                    Message = "لم يتم العثور على المستخدم الحالي."
                };
            }

            /*
                الإدارة الرئيسية مستثناة حتى لا يتم قفل السيستم بالكامل بالخطأ.
                باقي الموظفين يتم التحكم بهم من زر "موبايل/آيباد" في صفحة الموظفين.
            */
            if (await IsDeviceAccessExemptUserAsync(user))
            {
                return new DeviceAccessValidationResult
                {
                    IsValid = true,
                    Message = ""
                };
            }

            if (!IsMobileOrTabletRequest())
            {
                return new DeviceAccessValidationResult
                {
                    IsValid = true,
                    Message = ""
                };
            }

            var employee = await _context.Employees
                .AsNoTracking()
                .Where(e => e.ApplicationUserId == user.Id)
                .Select(e => new
                {
                    e.Id,
                    Name = !string.IsNullOrWhiteSpace(e.DisplayName)
                        ? e.DisplayName
                        : (!string.IsNullOrWhiteSpace(e.Name) ? e.Name : user.Email)
                })
                .FirstOrDefaultAsync();

            /*
                لو الحساب غير مربوط بموظف، لا نمنعه من الدخول حتى لا يتم قفل حسابات الإدارة/النظام.
            */
            if (employee == null)
            {
                return new DeviceAccessValidationResult
                {
                    IsValid = true,
                    Message = ""
                };
            }

            var isAllowed = await GetEmployeeAllowMobileOrTabletLoginAsync(employee.Id);

            if (isAllowed)
            {
                return new DeviceAccessValidationResult
                {
                    IsValid = true,
                    Message = ""
                };
            }

            return new DeviceAccessValidationResult
            {
                IsValid = false,
                Message = "لا يمكن تسجيل الدخول من الموبايل أو الآيباد لهذا الموظف. برجاء فتح السيستم من جهاز كمبيوتر، أو تفعيل السماح من صفحة الموظفين."
            };
        }

        private async Task<bool> IsDeviceAccessExemptUserAsync(ApplicationUser user)
        {
            if (user == null)
            {
                return false;
            }

            return await _userManager.IsInRoleAsync(user, "Admin")
                || await _userManager.IsInRoleAsync(user, "ExecutiveDirector");
        }

        private bool IsMobileOrTabletRequest()
        {
            var clientDeviceType = (Input?.ClientDeviceType ?? string.Empty).Trim().ToLowerInvariant();

            if (clientDeviceType == "mobile" || clientDeviceType == "tablet")
            {
                return true;
            }

            var clientUserAgent = string.IsNullOrWhiteSpace(Input?.ClientUserAgent)
                ? Request.Headers["User-Agent"].ToString()
                : Input.ClientUserAgent;

            return IsMobileOrTabletUserAgent(clientUserAgent);
        }

        private static bool IsMobileOrTabletUserAgent(string userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                return false;
            }

            userAgent = userAgent.ToLowerInvariant();

            var mobileOrTabletIndicators = new[]
            {
                "mobi",
                "android",
                "iphone",
                "ipad",
                "ipod",
                "tablet",
                "windows phone",
                "blackberry",
                "opera mini",
                "opera mobi",
                "kindle",
                "silk",
                "playbook"
            };

            return mobileOrTabletIndicators.Any(indicator => userAgent.Contains(indicator));
        }

        private async Task<bool> GetEmployeeAllowMobileOrTabletLoginAsync(int employeeId)
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
IF COL_LENGTH('dbo.Employees', 'AllowMobileOrTabletLogin') IS NULL
BEGIN
    SELECT CAST(0 AS BIT);
END
ELSE
BEGIN
    SELECT TOP 1 CAST(ISNULL(AllowMobileOrTabletLogin, 0) AS BIT)
    FROM dbo.Employees
    WHERE Id = @EmployeeId;
END";

                    var parameter = command.CreateParameter();
                    parameter.ParameterName = "@EmployeeId";
                    parameter.Value = employeeId;
                    command.Parameters.Add(parameter);

                    var result = await command.ExecuteScalarAsync();

                    return result != null
                        && result != DBNull.Value
                        && Convert.ToBoolean(result);
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

        private async Task<bool> IsIpCheckExemptUserAsync(ApplicationUser user)
        {
            if (user == null)
            {
                return false;
            }

            return await _userManager.IsInRoleAsync(user, "Admin")
                || await _userManager.IsInRoleAsync(user, "ExecutiveDirector")
                || await _userManager.IsInRoleAsync(user, "CallCenter");
        }

        private async Task ClearFailedLoginSessionAsync()
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

        private async Task<ShiftAccessValidationResult> ValidateShiftAccessAsync(ApplicationUser user)
        {
            if (user == null)
            {
                return new ShiftAccessValidationResult
                {
                    IsValid = false,
                    Message = "لم يتم العثور على المستخدم الحالي"
                };
            }

            if (await IsShiftAccessExemptUserAsync(user))
            {
                return new ShiftAccessValidationResult
                {
                    IsValid = true,
                    Message = ""
                };
            }

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == user.Id);

            if (employee == null)
            {
                return new ShiftAccessValidationResult
                {
                    IsValid = true,
                    Message = ""
                };
            }

            // لو بلوك = لا للموظف، لا نطبق عليه Access Shift ويقدر يدخل في أي وقت.
            if (!await GetEmployeeApplyShiftAccessAsync(employee.Id))
            {
                return new ShiftAccessValidationResult
                {
                    IsValid = true,
                    Message = ""
                };
            }

            var activeShift = await _context.EmployeeWorkShifts
                .Where(s => s.EmployeeId == employee.Id && s.IsActive)
                .OrderByDescending(s => s.Id)
                .FirstOrDefaultAsync();

            if (activeShift == null)
            {
                return new ShiftAccessValidationResult
                {
                    IsValid = true,
                    Message = ""
                };
            }

            var now = GetEgyptNow();

            if (IsAdminUnblockActive(activeShift, now))
            {
                if (activeShift.IsLoginBlocked)
                {
                    activeShift.IsLoginBlocked = false;
                    activeShift.UpdatedAt = now;
                    await _context.SaveChangesAsync();
                }

                return new ShiftAccessValidationResult
                {
                    IsValid = true,
                    Message = ""
                };
            }

            var shiftWindow = BuildShiftWindow(now, activeShift.ShiftStartTime, activeShift.ShiftEndTime);

            if (now < shiftWindow.AccessStart)
            {
                return new ShiftAccessValidationResult
                {
                    IsValid = false,
                    Message = $"لا يمكنك الدخول الآن. مسموح بالدخول قبل بداية دوامك بنصف ساعة، بداية من الساعة {shiftWindow.AccessStart:HH:mm}"
                };
            }

            if (now >= shiftWindow.EndWithGrace)
            {
                activeShift.IsLoginBlocked = true;
                activeShift.LoginBlockedAt = now;
                activeShift.LoginBlockReason = "انتهى موعد الدوام وتم عمل بلوك تلقائي";
                activeShift.AdminUnblockedUntil = null;
                activeShift.AdminUnblockedAt = null;
                activeShift.AdminUnblockedByUserId = null;
                activeShift.UpdatedAt = now;

                await _context.SaveChangesAsync();

                return new ShiftAccessValidationResult
                {
                    IsValid = false,
                    Message = $"تم انتهاء موعد دوامك، لا يمكنك الدخول الآن. سيتم السماح بالدخول مرة أخرى قبل بداية دوامك بنصف ساعة."
                };
            }

            if (activeShift.IsLoginBlocked)
            {
                activeShift.IsLoginBlocked = false;
                activeShift.LoginBlockReason = null;
                activeShift.LoginBlockedAt = null;
                activeShift.UpdatedAt = now;

                await _context.SaveChangesAsync();
            }

            return new ShiftAccessValidationResult
            {
                IsValid = true,
                Message = ""
            };
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

        private async Task<bool> IsShiftAccessExemptUserAsync(ApplicationUser user)
        {
            return await _userManager.IsInRoleAsync(user, "Admin")
                || await _userManager.IsInRoleAsync(user, "ExecutiveDirector")
                || await _userManager.IsInRoleAsync(user, "DeliveryCompany")
                || await _userManager.IsInRoleAsync(user, "DeliveryRepresentative")
                || await _userManager.IsInRoleAsync(user, "OrderPreparer");
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

        private static bool IsAdminUnblockActive(EmployeeWorkShift shift, DateTime now)
        {
            return shift.AdminUnblockedUntil.HasValue &&
                   shift.AdminUnblockedUntil.Value > now;
        }

        private static string FormatShiftTime(TimeSpan time)
        {
            return DateTime.Today.Add(time).ToString("HH:mm");
        }

        private async Task LoadEmployeeLoginProfilesAsync()
        {
            /*
                تعرض قائمة تسجيل الدخول الموظفين الذين تنطبق عليهم الشروط الآتية:
                1) الموظف Active = 1.
                2) الموظف مربوط بمستخدم لديه Email.
                3) الموظف لديه Shift فعال Active = 1.

                يتم إرسال AllowedIpAddress للصفحة، والـ JavaScript في شاشة Login يعرض فقط الموظفين المطابقين لنفس IP الحالي.
            */
            var profileRows = await (
                from employee in _context.Employees.AsNoTracking()
                join user in _context.Users.AsNoTracking()
                    on employee.ApplicationUserId equals user.Id
                join shift in _context.EmployeeWorkShifts.AsNoTracking()
                    on employee.Id equals shift.EmployeeId
                where employee.ApplicationUserId != null
                    && employee.ApplicationUserId != ""
                    && user.Email != null
                    && user.Email != ""
                    && employee.IsActive
                    && shift.IsActive
                orderby employee.Name
                select new
                {
                    Email = user.Email,
                    Name = !string.IsNullOrWhiteSpace(employee.DisplayName)
                        ? employee.DisplayName
                        : (!string.IsNullOrWhiteSpace(employee.Name) ? employee.Name : user.Email),
                    ImageUrl = !string.IsNullOrWhiteSpace(employee.ImageUrl)
                        ? "/" + employee.ImageUrl.TrimStart('/')
                        : "/static/DefaultImage.svg",
                    AllowedIpAddress = shift.AllowedIpAddress == null ? "" : shift.AllowedIpAddress
                })
                .ToListAsync();

            EmployeeLoginProfiles = profileRows
                .GroupBy(row => row.Email.Trim().ToLowerInvariant())
                .Select(group =>
                {
                    var firstRow = group.First();

                    return new LoginEmployeeProfile
                    {
                        Email = firstRow.Email,
                        Name = firstRow.Name,
                        ImageUrl = firstRow.ImageUrl,
                        AllowedIpAddress = string.Join(",",
                            group
                                .Select(row => row.AllowedIpAddress)
                                .Where(ip => !string.IsNullOrWhiteSpace(ip))
                                .Distinct(StringComparer.OrdinalIgnoreCase))
                    };
                })
                .OrderBy(profile => profile.Name)
                .ToList();
        }


        private async Task ActivateEmployeeAfterSuccessfulLoginAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == userId);

            if (employee == null)
            {
                return;
            }

            if (!employee.IsActive)
            {
                employee.IsActive = true;
                await _context.SaveChangesAsync();
            }
        }

        private async Task<IpValidationResult> ValidateAndAttachEmployeeIpAsync(string userId, string currentLoginIp)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new IpValidationResult
                {
                    IsValid = true,
                    Message = "لم يتم العثور على المستخدم الحالي، تم السماح بالدخول بدون حفظ IP"
                };
            }

            currentLoginIp = NormalizeIp(currentLoginIp);

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == userId);

            if (employee == null)
            {
                return new IpValidationResult
                {
                    IsValid = true,
                    Message = "لا يوجد موظف مربوط بالمستخدم، تم السماح بالدخول بدون تحقق IP"
                };
            }

            var activeShift = await _context.EmployeeWorkShifts
                .Where(s => s.EmployeeId == employee.Id && s.IsActive)
                .OrderByDescending(s => s.Id)
                .FirstOrDefaultAsync();

            if (activeShift == null)
            {
                _context.EmployeeWorkShifts.Add(new EmployeeWorkShift
                {
                    EmployeeId = employee.Id,
                    IsActive = true,
                    AllowedIpAddress = string.IsNullOrWhiteSpace(currentLoginIp) ? "" : currentLoginIp,
                    ShiftStartTime = TimeSpan.Parse("09:00:00"),
                    ShiftEndTime = TimeSpan.Parse("19:00:00"),
                    Notes = "تم إنشاء الشيفت تلقائيًا عند أول تسجيل دخول وحفظ IP الشبكة الحالية بدون منع الدخول",
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();

                return new IpValidationResult
                {
                    IsValid = true,
                    Message = "لا يوجد شيفت فعال، تم إنشاء شيفت وحفظ IP الشبكة الحالية بدون منع الدخول"
                };
            }

            if (string.IsNullOrWhiteSpace(currentLoginIp))
            {
                return new IpValidationResult
                {
                    IsValid = true,
                    Message = "لم يتم التعرف على IP الشبكة الحالية، وتم السماح بالدخول بدون منع"
                };
            }

            activeShift.AllowedIpAddress = MergeAllowedIpAddresses(activeShift.AllowedIpAddress, currentLoginIp);
            activeShift.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return new IpValidationResult
            {
                IsValid = true,
                Message = "تم حفظ IP الحالي والسماح بالدخول"
            };
        }

        private static List<string> SplitAllowedIpAddresses(string allowedIpAddress)
        {
            return (allowedIpAddress ?? "")
                .Split(new[] { ',', ';', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeIp)
                .Where(ip => !string.IsNullOrWhiteSpace(ip))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task EnsureEmployeeVisibleInLoginListAsync(string userId, string currentLoginIp)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            currentLoginIp = NormalizeIp(currentLoginIp);

            if (string.IsNullOrWhiteSpace(currentLoginIp))
            {
                return;
            }

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == userId);

            if (employee == null)
            {
                return;
            }

            if (!employee.IsActive)
            {
                employee.IsActive = true;
            }

            var activeShift = await _context.EmployeeWorkShifts
                .Where(s => s.EmployeeId == employee.Id && s.IsActive)
                .OrderByDescending(s => s.Id)
                .FirstOrDefaultAsync();

            if (activeShift != null)
            {
                activeShift.AllowedIpAddress = MergeAllowedIpAddresses(
                    activeShift.AllowedIpAddress,
                    currentLoginIp);

                await _context.SaveChangesAsync();
                return;
            }

            await _context.SaveChangesAsync();

            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO dbo.EmployeeWorkShifts
                (
                    EmployeeId,
                    IsActive,
                    AllowedIpAddress,
                    ShiftStartTime,
                    ShiftEndTime,
                    CreatedAt
                )
                VALUES
                (
                    {employee.Id},
                    {true},
                    {currentLoginIp},
                    {"09:00:00"},
                    {"19:00:00"},
                    {DateTime.Now}
                )");
        }

        private string GetCurrentLoginPublicIp()
        {
            var clientPublicIp = string.IsNullOrWhiteSpace(Input?.ClientPublicIp)
                ? ""
                : NormalizeIp(Input.ClientPublicIp);

            if (!string.IsNullOrWhiteSpace(clientPublicIp))
            {
                return clientPublicIp;
            }

            return NormalizeIp(GetCurrentIpAddress());
        }

        private static string MergeAllowedIpAddresses(string existingIps, string newIp)
        {
            newIp = NormalizeIp(newIp);

            if (string.IsNullOrWhiteSpace(newIp))
            {
                return existingIps ?? "";
            }

            var ips = (existingIps ?? "")
                .Split(new[] { ',', ';', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeIp)
                .Where(ip => !string.IsNullOrWhiteSpace(ip))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!ips.Any(ip => string.Equals(ip, newIp, StringComparison.OrdinalIgnoreCase)))
            {
                ips.Add(newIp);
            }

            return string.Join(",", ips);
        }

        private async Task<IpValidationResult> ValidateEmployeeIpAsync(string userId)
        {
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
                return new IpValidationResult
                {
                    IsValid = true,
                    Message = "لا يوجد موظف مربوط بالمستخدم، تم السماح بالدخول بدون تحقق شيفت أو IP"
                };
            }

            var shift = await _context.EmployeeWorkShifts
                .AsNoTracking()
                .Where(s => s.EmployeeId == employee.Id && s.IsActive)
                .OrderByDescending(s => s.Id)
                .Select(s => new
                {
                    s.AllowedIpAddress
                })
                .FirstOrDefaultAsync();

            if (shift == null)
            {
                return new IpValidationResult
                {
                    IsValid = true,
                    Message = "لا يوجد شيفت فعال لهذا الموظف، تم السماح بالدخول بدون تحقق IP"
                };
            }

            if (string.IsNullOrWhiteSpace(shift.AllowedIpAddress))
            {
                return new IpValidationResult
                {
                    IsValid = true,
                    Message = "لا يوجد IP مسموح مسجل لهذا الشيفت، تم السماح بالدخول"
                };
            }

            var serverIp = GetCurrentIpAddress();

            var clientPublicIp = string.IsNullOrWhiteSpace(Input?.ClientPublicIp)
                ? ""
                : Input.ClientPublicIp.Trim();

            var currentIp = !string.IsNullOrWhiteSpace(clientPublicIp)
                ? clientPublicIp
                : serverIp;

            if (string.IsNullOrWhiteSpace(currentIp))
            {
                return new IpValidationResult
                {
                    IsValid = false,
                    Message = "لم يتم التعرف على IP الجهاز الحالي"
                };
            }

            var actualIp = NormalizeIp(currentIp);

            var allowedIps = shift.AllowedIpAddress
                .Split(new[] { ',', ';', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeIp)
                .Where(ip => !string.IsNullOrWhiteSpace(ip))
                .ToList();

            if (allowedIps.Count == 0)
            {
                return new IpValidationResult
                {
                    IsValid = true,
                    Message = "قيمة IP المسجلة غير واضحة، تم السماح بالدخول"
                };
            }

            if (!allowedIps.Contains(actualIp))
            {
                return new IpValidationResult
                {
                    IsValid = false,
                    Message = $"IP الجهاز غير مطابق للـ IP المسجل لهذا الموظف. IP الحالي: {actualIp}"
                };
            }

            return new IpValidationResult
            {
                IsValid = true,
                Message = "IP صحيح"
            };
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

        private async Task<string> BuildLoginWelcomeTextAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return "";
            }

            var employee = await _context.Employees
                .AsNoTracking()
                .Where(e => e.ApplicationUserId == userId)
                .Select(e => new
                {
                    e.Id,
                    Name = !string.IsNullOrWhiteSpace(e.DisplayName)
                        ? e.DisplayName
                        : (!string.IsNullOrWhiteSpace(e.Name) ? e.Name : "")
                })
                .FirstOrDefaultAsync();

            if (employee == null)
            {
                return "";
            }

            var shift = await _context.EmployeeWorkShifts
                .AsNoTracking()
                .Where(s => s.EmployeeId == employee.Id && s.IsActive)
                .OrderByDescending(s => s.Id)
                .FirstOrDefaultAsync();

            var firstName = GetFirstNameForWelcome(employee.Name);

            var shiftStartValue = shift?.GetType()
                .GetProperty("ShiftStartTime")
                ?.GetValue(shift);

            var shiftEndValue = shift?.GetType()
                .GetProperty("ShiftEndTime")
                ?.GetValue(shift);

            var greeting = IsEveningShift(shiftStartValue, shiftEndValue)
                ? "مساء الخير"
                : "صباح الخير";

            return string.IsNullOrWhiteSpace(firstName)
                ? greeting
                : $"{greeting} يا {firstName}";
        }

        private static string GetFirstNameForWelcome(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "";
            }

            name = name.Trim();

            if (name.Contains("@"))
            {
                name = name.Split('@')[0];
            }

            var firstName = name
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? "";

            if (firstName.Equals("Nadeen", StringComparison.OrdinalIgnoreCase)
                || firstName.Equals("Nadine", StringComparison.OrdinalIgnoreCase))
            {
                return "نادين";
            }

            return firstName;
        }

        private static bool IsEveningShift(object shiftStartTime, object shiftEndTime)
        {
            /*
                الأساس هنا هو الشيفت:
                - لو بداية الشيفت من 12 ظهرًا أو بعده => مساء الخير.
                - لو بداية الشيفت قبل 12 ظهرًا => صباح الخير.
                - لو بداية الشيفت غير متاحة، نستخدم نهاية الشيفت كاحتياط.
                - لو لا توجد بيانات شيفت، نستخدم توقيت مصر الحالي كاحتياط فقط.
            */
            if (TryGetHourFromShiftValue(shiftStartTime, out var startHour))
            {
                return startHour >= 12;
            }

            if (TryGetHourFromShiftValue(shiftEndTime, out var endHour))
            {
                return endHour > 18;
            }

            return GetEgyptNow().Hour >= 12;
        }

        private static bool TryGetHourFromShiftValue(object value, out int hour)
        {
            hour = 0;

            if (value == null)
            {
                return false;
            }

            if (value is TimeSpan timeSpan)
            {
                hour = timeSpan.Hours;
                return true;
            }

            if (value is DateTime dateTime)
            {
                hour = dateTime.Hour;
                return true;
            }

            var hourProperty = value.GetType().GetProperty("Hour");

            if (hourProperty != null)
            {
                var hourValue = hourProperty.GetValue(value);

                if (hourValue != null && int.TryParse(hourValue.ToString(), out var propertyHour))
                {
                    hour = propertyHour;
                    return true;
                }
            }

            var text = value.ToString();

            if (TimeSpan.TryParse(text, out var parsedTimeSpan))
            {
                hour = parsedTimeSpan.Hours;
                return true;
            }

            if (DateTime.TryParse(text, out var parsedDateTime))
            {
                hour = parsedDateTime.Hour;
                return true;
            }

            return false;
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

        private class DeviceAccessValidationResult
        {
            public bool IsValid { get; set; }

            public string Message { get; set; } = "";
        }

        private class ShiftAccessValidationResult
        {
            public bool IsValid { get; set; }

            public string Message { get; set; } = "";
        }

        private class IpValidationResult
        {
            public bool IsValid { get; set; }

            public string Message { get; set; } = "";
        }
    }
}
