using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.Services.Bonus;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using static lotus_blue.Models.Common;

namespace lotus_blue.ViewComponents
{
    // Renders the CallCenter floating bonus panel. Returns an empty content result
    // when invoked by a non-CallCenter user or an anonymous request — callers can
    // unconditionally render the component and the visibility gate lives here.
    public class BonusPanelViewComponent : ViewComponent
    {
        private readonly BonusHomePanelService _panelService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public BonusPanelViewComponent(
            BonusHomePanelService panelService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _panelService = panelService;
            _userManager = userManager;
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (User?.Identity == null || !User.Identity.IsAuthenticated)
                return Content(string.Empty);
            if (!User.IsInRole("CallCenter") && !User.IsInRole("FollowUpDepartment") && !User.IsInRole("Admin"))
                return Content(string.Empty);
            var user = await _userManager.GetUserAsync(HttpContext.User);
            if (user == null) return Content(string.Empty);

            var rate = await _context.EmployeeBonusRates
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.EmployeeId == user.Id);
            if (rate?.IsBonusPanelHidden == true)
                return Content(string.Empty);

            var model = await _panelService.BuildAsync(user.Id);

            // Alt currency: TL when the employee has no country, otherwise the employee's
            // country currency (shown by its 3-letter code, e.g. IQD). Country lives on
            // the Employee record (string) — bridged through Common.EmployeeCountryToEnum.
            var employeeCountryStr = await _context.Employees
                .Where(e => e.ApplicationUserId == user.Id)
                .Select(e => e.Country)
                .FirstOrDefaultAsync();
            var employeeCountry = EmployeeCountryToEnum(employeeCountryStr);
            if (employeeCountry.HasValue && CurrencyByCountry.TryGetValue(employeeCountry.Value, out var code))
            {
                model.AltCurrencyCountry = employeeCountry;
                model.AltCurrencyCode = code;
                model.AltCurrencySubtitle = $"(ما يقابله بـ {code})";
            }

            return View(model);
        }
    }
}
