using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.Models.ViewModel;
using lotus_blue.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using static lotus_blue.Models.Common;

namespace lotus_blue.Controllers
{
    public class LeadController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly GetCurrentTimeInIstanbul _timeService;

        public LeadController(
            ApplicationDbContext context,
            GetCurrentTimeInIstanbul timeService)
        {
            _context = context;
            _timeService = timeService;
        }

        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment,CallCenter")]
        public async Task<IActionResult> Index(
            int page = 1,
            int pageSize = 10,
            int? orderSourceId = null,
            string search = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            if (HttpContext.Session.GetInt32("Lead_PageSize") != pageSize)
                HttpContext.Session.SetInt32("Lead_PageSize", pageSize);

            IQueryable<Lead> query = _context.Leads;

            // Scope: CallCenter sees their own only; Admin/FollowUp/ExecutiveDirector see all.
            if (User.IsInRole("CallCenter") && !User.IsInRole("Admin") && !User.IsInRole("ExecutiveDirector") && !User.IsInRole("FollowUpDepartment"))
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                query = query.Where(l => l.ApplicationUserId == currentUserId);
            }

            if (orderSourceId.HasValue && Enum.IsDefined(typeof(OrderSourceEnum), orderSourceId.Value))
            {
                var orderSource = (OrderSourceEnum)orderSourceId.Value;
                if (QueryFilteringService.IsMetaSource(orderSource))
                    query = query.Where(l => l.OrderSource == OrderSourceEnum.فيسبوك || l.OrderSource == OrderSourceEnum.انستغرام);
                else
                    query = query.Where(l => l.OrderSource == orderSource);
            }

            if (startDate.HasValue)
                query = query.Where(l => l.CreatedDate >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(l => l.CreatedDate < endDate.Value.AddDays(1));

            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{search}%";
                query = query.Where(l =>
                    EF.Functions.Like(l.SourceName, pattern) ||
                    (l.PhoneNumber != null && EF.Functions.Like(l.PhoneNumber, pattern)) ||
                    (l.ChatUrl != null && EF.Functions.Like(l.ChatUrl, pattern)));
            }

            var totalItems = await query.CountAsync();

            var items = await query
                .AsNoTracking()
                .OrderByDescending(l => l.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new LeadViewModel
                {
                    Id = l.Id,
                    SourceName = l.SourceName,
                    OrderSource = l.OrderSource,
                    PhoneNumber = l.PhoneNumber,
                    ChatUrl = l.ChatUrl,
                    CreatedDate = l.CreatedDate,
                    EmployeeName = _context.Employees
                        .Where(e => e.ApplicationUserId == l.ApplicationUserId)
                        .Select(e => e.DisplayName)
                        .FirstOrDefault(),
                    EmployeeImage = _context.Employees
                        .Where(e => e.ApplicationUserId == l.ApplicationUserId)
                        .Select(e => e.ImageUrl)
                        .FirstOrDefault()
                })
                .ToListAsync();

            var viewModel = new LeadListViewModel
            {
                PaginationViewModel = new PaginationViewModel<LeadViewModel>
                {
                    Items = items,
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalItems = totalItems
                }
            };

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView(viewModel);

            return View(viewModel);
        }

        // JSON endpoint for the home-page header panel (الطلبات المحتملة).
        // Returns leads grouped by date (newest first). Same role scope as Index.
        [HttpGet]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment,CallCenter")]
        public async Task<IActionResult> Panel()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            IQueryable<Lead> query = _context.Leads;

            if (User.IsInRole("CallCenter") && !isAdmin && !User.IsInRole("ExecutiveDirector") && !User.IsInRole("FollowUpDepartment"))
            {
                query = query.Where(l => l.ApplicationUserId == currentUserId);
            }

            var rows = await query
                .AsNoTracking()
                .OrderByDescending(l => l.CreatedDate)
                .Select(l => new
                {
                    id = l.Id,
                    sourceName = l.SourceName,
                    orderSource = (int)l.OrderSource,
                    phoneNumber = l.PhoneNumber,
                    chatUrl = l.ChatUrl,
                    createdAt = l.CreatedDate,
                    ownerId = l.ApplicationUserId
                })
                .ToListAsync();

            var groups = rows
                .GroupBy(r => r.createdAt.Date)
                .OrderByDescending(g => g.Key)
                .Select(g => new
                {
                    date = g.Key.ToString("yyyy-MM-dd"),
                    cards = g.Select(r => new
                    {
                        r.id,
                        r.sourceName,
                        r.orderSource,
                        r.phoneNumber,
                        r.chatUrl,
                        r.createdAt,
                        canDelete = r.ownerId == currentUserId || isAdmin
                    }).ToList()
                })
                .ToList();

            return Json(new { count = rows.Count, groups });
        }

        // Deletes a lead. Owner can delete their own; Admin can delete any.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment,CallCenter")]
        public async Task<IActionResult> Delete(int id)
        {
            var lead = await _context.Leads.FirstOrDefaultAsync(l => l.Id == id);
            if (lead == null)
                return NotFound();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (lead.ApplicationUserId != currentUserId && !User.IsInRole("Admin"))
                return Forbid();

            _context.Leads.Remove(lead);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}
