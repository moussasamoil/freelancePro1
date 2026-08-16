using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using lotus_blue.Data;
using lotus_blue.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace lotus_blue.Controllers
{
    /*
        مهم:
        لم نضع [Authorize(Roles = "Admin")] على الكنترولر كله،
        لأن الموظف بعد السويتش قد لا يكون Admin، ومع ذلك نحتاج نسمح له يكمل تبديل الحسابات
        طالما الجلسة أصلًا مفتوحة من Admin.
    */
    [Authorize]
    [Route("[controller]")]
    public class AccountSwitchController : Controller
    {
        private const string OriginalAdminUserIdClaim = "OriginalAdminUserId";
        private const string OriginalAdminEmailClaim = "OriginalAdminEmail";
        private const string OriginalAdminNameClaim = "OriginalAdminName";
        private const string IsAdminSwitchSessionClaim = "IsAdminSwitchSession";
        private const string IsSwitchedAccountClaim = "IsSwitchedAccount";

        private const string AdminSwitchLoginCookieName = "LuxiraAdminSwitchLogin";
        private const string OriginalAdminUserIdCookieName = "LuxiraOriginalAdminUserId";
        private const string OriginalAdminEmailCookieName = "LuxiraOriginalAdminEmail";
        private const string OriginalAdminNameCookieName = "LuxiraOriginalAdminName";
        private const string RequireCheckInFaceCaptureCookieName = "LuxiraRequireCheckInFaceCapture";

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IDataProtector _adminSwitchProtector;

        public AccountSwitchController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IDataProtectionProvider dataProtectionProvider)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _adminSwitchProtector = dataProtectionProvider.CreateProtector("lotus_blue.AccountSwitch.AdminSwitchSession.v1");
        }

        /*
            يعرض كل إيميلات الموظفين المرتبطين بجدول Employees.
            مسموح به للأدمن الحقيقي أو لأي حساب موظف مفتوح من خلال Admin Switch.
        */
        [HttpGet("MyAccounts")]
        public async Task<IActionResult> MyAccounts()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Unauthorized(new { success = false, message = "جلسة المستخدم غير صالحة." });
            }

            if (!await CanUseAccountSwitchAsync())
            {
                return Unauthorized(new { success = false, message = "غير مسموح باستخدام تبديل الحسابات." });
            }

            var employeesWithUsers = await (
                from employee in _context.Employees.AsNoTracking()
                join user in _context.Users.AsNoTracking()
                    on employee.ApplicationUserId equals user.Id
                where employee.ApplicationUserId != null
                      && employee.ApplicationUserId != ""
                      && user.Email != null
                      && user.Email != ""
                select new
                {
                    EmployeeId = employee.Id,
                    EmployeeName = employee.Name,
                    EmployeeDisplayName = employee.DisplayName,
                    employee.IsActive,
                    UserId = user.Id,
                    user.Email,
                    user.UserName,
                    UserNameField = user.Name
                })
                .ToListAsync();

            var employeeUserIds = employeesWithUsers
                .Select(x => x.UserId)
                .Distinct()
                .ToList();

            // إضافة حسابات شركات التوصيل والمندوبين للسويتش بدون تغيير شكل السايدبار أو منطق الموظفين الحالي.
            var deliveryAccountUserIds = await (
                from userRole in _context.UserRoles.AsNoTracking()
                join role in _context.Roles.AsNoTracking()
                    on userRole.RoleId equals role.Id
                where role.Name == "DeliveryCompany"
                      || role.Name == "DeliveryRepresentative"
                select userRole.UserId)
                .Distinct()
                .ToListAsync();

            var deliveryAccountsWithUsers = await _context.Users
                .AsNoTracking()
                .Where(user => deliveryAccountUserIds.Contains(user.Id)
                               && !employeeUserIds.Contains(user.Id)
                               && user.Email != null
                               && user.Email != "")
                .Select(user => new
                {
                    EmployeeId = 0,
                    EmployeeName = user.Name,
                    EmployeeDisplayName = user.Name,
                    IsActive = true,
                    UserId = user.Id,
                    user.Email,
                    user.UserName,
                    UserNameField = user.Name
                })
                .ToListAsync();

            var switchAccountsWithUsers = employeesWithUsers
                .Concat(deliveryAccountsWithUsers)
                .ToList();

            var userIds = switchAccountsWithUsers
                .Select(x => x.UserId)
                .Distinct()
                .ToList();

            var roles = await (
                from userRole in _context.UserRoles.AsNoTracking()
                join role in _context.Roles.AsNoTracking()
                    on userRole.RoleId equals role.Id
                where userIds.Contains(userRole.UserId)
                select new
                {
                    userRole.UserId,
                    RoleName = role.Name
                })
                .ToListAsync();

            var roleMap = roles
                .GroupBy(x => x.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => string.Join("، ", g.Select(x => x.RoleName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()));

            var accounts = switchAccountsWithUsers
                .GroupBy(x => x.UserId)
                .Select(g =>
                {
                    var first = g.First();

                    var displayName =
                        !string.IsNullOrWhiteSpace(first.EmployeeDisplayName)
                            ? first.EmployeeDisplayName
                            : (!string.IsNullOrWhiteSpace(first.EmployeeName)
                                ? first.EmployeeName
                                : (!string.IsNullOrWhiteSpace(first.UserNameField)
                                    ? first.UserNameField
                                    : first.Email));

                    return new
                    {
                        id = first.UserId,
                        employeeId = first.EmployeeId,
                        email = first.Email,
                        userName = first.UserName,
                        displayName,
                        roleName = roleMap.ContainsKey(first.UserId) ? roleMap[first.UserId] : "",
                        isActive = first.IsActive,
                        isCurrent = first.UserId == currentUserId
                    };
                })
                .OrderBy(x => x.displayName)
                .ThenBy(x => x.email)
                .ToList();

            return Ok(new
            {
                success = true,
                title = "الملفات الشخصية للموظفين",
                accounts
            });
        }

        /*
            Admin Switch:
            - الأدمن يدخل على حساب موظف بدون Password.
            - بدون IP check.
            - بدون WorkShift check.
            - الموظف المفتوح من الأدمن يقدر يبدل لحساب موظف آخر طالما أصل الجلسة Admin.

            حل المشكلة الحالية:
            بعض الأكواد داخل المشروع قد تعمل RefreshSignInAsync أو تعيد بناء Claims الحساب الحالي،
            وده ممكن يضيّع Claims السويتش بعد الدخول على الموظف.
            لذلك خزّنا بيانات الأدمن الأصلي في Cookies محمية DataProtection بجانب الـ Claims.
        */
        [HttpPost("Switch")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Switch([FromBody] SwitchAccountRequest request)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Unauthorized(new { success = false, message = "جلسة المستخدم غير صالحة." });
            }

            if (!await CanUseAccountSwitchAsync())
            {
                return Unauthorized(new { success = false, message = "غير مسموح باستخدام تبديل الحسابات." });
            }

            if (request == null || string.IsNullOrWhiteSpace(request.TargetUserId))
            {
                return BadRequest(new { success = false, message = "اختاري الملف الشخصي أولًا." });
            }

            if (request.TargetUserId == currentUserId)
            {
                return BadRequest(new { success = false, message = "أنتِ بالفعل داخل هذا الحساب." });
            }

            var targetUser = await _userManager.FindByIdAsync(request.TargetUserId);

            if (targetUser == null)
            {
                return NotFound(new { success = false, message = "الحساب غير موجود." });
            }

            var employeeExists = await _context.Employees
                .AsNoTracking()
                .AnyAsync(e => e.ApplicationUserId == targetUser.Id);

            var targetRoles = await _userManager.GetRolesAsync(targetUser);
            var isDeliverySwitchAccount = targetRoles.Any(role =>
                role == "DeliveryCompany" || role == "DeliveryRepresentative");

            if (!employeeExists && !isDeliverySwitchAccount)
            {
                return BadRequest(new { success = false, message = "هذا الحساب غير مربوط بموظف أو شركة توصيل أو مندوب، لذلك لن يظهر كملف شخصي للسويتش." });
            }

            var currentUser = await _userManager.FindByIdAsync(currentUserId);

            if (currentUser == null)
            {
                return Unauthorized(new { success = false, message = "جلسة المستخدم غير صالحة." });
            }

            var originalAdmin = await ResolveOriginalAdminAsync(currentUser);

            if (originalAdmin == null)
            {
                return Unauthorized(new { success = false, message = "غير مسموح باستخدام تبديل الحسابات." });
            }

            var originalAdminUserId = originalAdmin.Id;
            var originalAdminEmail = originalAdmin.Email ?? originalAdmin.UserName ?? "";
            var originalAdminName = !string.IsNullOrWhiteSpace(originalAdmin.Name)
                ? originalAdmin.Name
                : (!string.IsNullOrWhiteSpace(originalAdmin.Email) ? originalAdmin.Email : (originalAdmin.UserName ?? "Admin"));

            var switchClaims = new List<Claim>
            {
                new Claim(IsAdminSwitchSessionClaim, "true"),
                new Claim(IsSwitchedAccountClaim, "true"),
                new Claim(OriginalAdminUserIdClaim, originalAdminUserId),
                new Claim(OriginalAdminEmailClaim, originalAdminEmail),
                new Claim(OriginalAdminNameClaim, originalAdminName)
            };

            await _signInManager.SignOutAsync();
            await _signInManager.SignInWithClaimsAsync(targetUser, isPersistent: false, switchClaims);

            SuppressCheckInFaceCaptureForAdminSwitch(originalAdminUserId, originalAdminEmail, originalAdminName);

            return Ok(new
            {
                success = true,
                message = "تم فتح الملف الشخصي للموظف بنجاح.",
                redirectUrl = Url.Content("~/Home/Index")
            });
        }

        /*
            اختياري: يستخدم فقط لو عندك زر مستقل للرجوع للأدمن بدون Logout.
            زر الخروج الأساسي في السايدبار لا يستخدم هذا المسار.
        */
        [HttpPost("ReturnToOriginalAdmin")]
        [Authorize]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ReturnToOriginalAdmin()
        {
            var originalAdmin = await ResolveOriginalAdminAsync();

            if (originalAdmin == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "هذه الجلسة ليست جلسة سويتش من أدمن."
                });
            }

            await _signInManager.SignOutAsync();
            await _signInManager.SignInAsync(originalAdmin, isPersistent: false);

            ClearAdminSwitchCheckInFlags();

            return Ok(new
            {
                success = true,
                message = "تم الرجوع لحساب الأدمن الأصلي.",
                redirectUrl = Url.Content("~/Home/Index")
            });
        }

        /*
            GET اختياري للرجوع للأدمن لو عندك زر أو لينك مباشر بدل Ajax.
            لا يستخدمه زر الخروج الأساسي.
        */
        [HttpGet("ReturnToOriginalAdminDirect")]
        [Authorize]
        public async Task<IActionResult> ReturnToOriginalAdminDirect()
        {
            var originalAdmin = await ResolveOriginalAdminAsync();

            if (originalAdmin == null)
            {
                await _signInManager.SignOutAsync();
                ClearAdminSwitchCheckInFlags();
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            await _signInManager.SignOutAsync();
            await _signInManager.SignInAsync(originalAdmin, isPersistent: false);

            ClearAdminSwitchCheckInFlags();

            return RedirectToAction("Index", "Home");
        }

        /*
            خروج موظف مفتوح من خلال الأدمن:
            المطلوب هنا Logout كامل إلى صفحة Login، وليس رجوع تلقائي لحساب الأدمن.
            لكن بدون طلب صورة خروج؛ لأن الدخول كان Admin Switch وليس Login حقيقي للموظف.
            نحفظ إيميل الأدمن الأصلي مؤقتًا حتى تظهر صفحة اللوجين على حساب الأدمن.
        */
        [HttpGet("LogoutSwitchToLogin")]
        [Authorize]
        public async Task<IActionResult> LogoutSwitchToLogin()
        {
            var originalAdmin = await ResolveOriginalAdminAsync();
            var originalAdminEmail = originalAdmin?.Email
                ?? User.FindFirstValue(OriginalAdminEmailClaim)
                ?? ReadProtectedCookie(OriginalAdminEmailCookieName)
                ?? "";

            if (!string.IsNullOrWhiteSpace(originalAdminEmail))
            {
                Response.Cookies.Append(
                    "LuxiraLoginPreferredEmail",
                    originalAdminEmail,
                    new Microsoft.AspNetCore.Http.CookieOptions
                    {
                        Path = "/",
                        HttpOnly = false,
                        IsEssential = true,
                        SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                        Secure = Request.IsHttps,
                        Expires = DateTimeOffset.UtcNow.AddMinutes(10)
                    });
            }

            ClearAdminSwitchCheckInFlags();

            await _signInManager.SignOutAsync();

            return RedirectToPage("/Account/Login", new
            {
                area = "Identity",
                selectedEmail = originalAdminEmail
            });
        }

        private async Task<bool> CanUseAccountSwitchAsync()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return false;
            }

            var currentUser = await _userManager.FindByIdAsync(currentUserId);

            if (currentUser == null)
            {
                return false;
            }

            if (await _userManager.IsInRoleAsync(currentUser, "Admin"))
            {
                return true;
            }

            var originalAdmin = await ResolveOriginalAdminAsync(currentUser);
            return originalAdmin != null;
        }

        private async Task<ApplicationUser> ResolveOriginalAdminAsync(ApplicationUser currentUser = null)
        {
            var originalAdminUserId = User.FindFirstValue(OriginalAdminUserIdClaim);

            if (string.IsNullOrWhiteSpace(originalAdminUserId))
            {
                originalAdminUserId = ReadProtectedCookie(OriginalAdminUserIdCookieName);
            }

            if (!string.IsNullOrWhiteSpace(originalAdminUserId))
            {
                var originalAdmin = await _userManager.FindByIdAsync(originalAdminUserId);

                if (originalAdmin != null && await _userManager.IsInRoleAsync(originalAdmin, "Admin"))
                {
                    return originalAdmin;
                }
            }

            if (currentUser != null && await _userManager.IsInRoleAsync(currentUser, "Admin"))
            {
                return currentUser;
            }

            return null;
        }

        /*
            يمنع طلب صورة تسجيل الحضور عند الدخول من خلال Admin Switch.
            هذا لا يؤثر على Login الحقيقي للموظف؛ فقط جلسة السويتش.
        */
        private void SuppressCheckInFaceCaptureForAdminSwitch(string originalAdminUserId, string originalAdminEmail, string originalAdminName)
        {
            TempData.Remove("RequireCheckInFaceCapture");

            Response.Cookies.Delete(RequireCheckInFaceCaptureCookieName);

            var expires = DateTimeOffset.UtcNow.AddHours(8);

            AppendProtectedCookie(AdminSwitchLoginCookieName, "true", expires);
            AppendProtectedCookie(OriginalAdminUserIdCookieName, originalAdminUserId, expires);
            AppendProtectedCookie(OriginalAdminEmailCookieName, originalAdminEmail ?? "", expires);
            AppendProtectedCookie(OriginalAdminNameCookieName, originalAdminName ?? "Admin", expires);
        }

        private void AppendProtectedCookie(string cookieName, string value, DateTimeOffset expires)
        {
            Response.Cookies.Append(
                cookieName,
                _adminSwitchProtector.Protect(value ?? ""),
                new Microsoft.AspNetCore.Http.CookieOptions
                {
                    Path = "/",
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                    Secure = Request.IsHttps,
                    Expires = expires
                });
        }

        private string ReadProtectedCookie(string cookieName)
        {
            if (!Request.Cookies.TryGetValue(cookieName, out var protectedValue) || string.IsNullOrWhiteSpace(protectedValue))
            {
                return null;
            }

            try
            {
                return _adminSwitchProtector.Unprotect(protectedValue);
            }
            catch
            {
                return null;
            }
        }

        /*
            يستخدم عند الرجوع للأدمن أو الخروج من جلسة السويتش حتى لا تفضل
            أي علامة قديمة تطلب صورة حضور/خروج بالخطأ.
        */
        private void ClearAdminSwitchCheckInFlags()
        {
            TempData.Remove("RequireCheckInFaceCapture");
            Response.Cookies.Delete(RequireCheckInFaceCaptureCookieName);
            Response.Cookies.Delete(AdminSwitchLoginCookieName);
            Response.Cookies.Delete(OriginalAdminUserIdCookieName);
            Response.Cookies.Delete(OriginalAdminEmailCookieName);
            Response.Cookies.Delete(OriginalAdminNameCookieName);
        }

        public class SwitchAccountRequest
        {
            public string TargetUserId { get; set; } = string.Empty;
        }
    }
}
