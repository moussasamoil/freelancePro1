// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.Threading.Tasks;
using lotus_blue.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace lotus_blue.Areas.Identity.Pages.Account
{
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LogoutModel> _logger;

        public LogoutModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<LogoutModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> OnPost(string returnUrl = null)
        {
            /*
                مهم:
                - قفل المتصفح فقط لا يعتبر Logout، لذلك الحساب يفضل مفتوح ولا نطلب صورة حضور مرة ثانية.
                - الضغط على Logout هو الخروج الحقيقي.
                - عند Logout نغير SecurityStamp عشان نخرج نفس المستخدم من كل الأجهزة وكل المتصفحات.
            */

            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser != null)
            {
                await _userManager.UpdateSecurityStampAsync(currentUser);
            }

            try
            {
                HttpContext.Session?.Clear();
            }
            catch
            {
                // لو الـ Session غير مفعلة في البيئة الحالية، نتجاهل الخطأ.
            }

            await _signInManager.SignOutAsync();

            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            await HttpContext.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);
            await HttpContext.SignOutAsync(IdentityConstants.TwoFactorRememberMeScheme);

            Response.Cookies.Delete(".AspNetCore.Identity.Application");
            Response.Cookies.Delete("Identity.Application");

            /*
                نمسح أي علامات مؤقتة مرتبطة بالدخول الحالي.
                بعد Logout، أي Login جديد هو الذي ينشئ طلب صورة الدخول من جديد.
            */
            Response.Cookies.Delete("LuxiraRequireCheckInFaceCapture");
            Response.Cookies.Delete("LoginWelcomeText");
            Response.Cookies.Delete("LuxiraLoginPreferredEmail");
            Response.Cookies.Delete("LuxiraAdminSwitchLogin");

            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            _logger.LogInformation("User logged out from all devices by security stamp update.");

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return LocalRedirect("~/Identity/Account/Login");
        }
    }
}
