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
    public class PotentialOrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly DataCacheService _dataCacheService;
        private readonly GetCurrentTimeInIstanbul _timeService;

        public PotentialOrderController(
            ApplicationDbContext context,
            DataCacheService dataCacheService,
            GetCurrentTimeInIstanbul timeService)
        {
            _context = context;
            _dataCacheService = dataCacheService;
            _timeService = timeService;
        }

        // Temporarily restricted to Admin only — was: [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment,CallCenter")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index(
            int page = 1,
            int pageSize = 10,
            int? countryId = null,
            int? statusId = null,
            string search = null,
            int? storeId = null,
            int? orderSourceId = null,
            string employeeId = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            // Store page size in session
            if (HttpContext.Session.GetInt32("PotentialOrder_PageSize") != pageSize)
                HttpContext.Session.SetInt32("PotentialOrder_PageSize", pageSize);

            IQueryable<PotentialOrder> query = _context.PotentialOrders;

            // Filter by country
            if (countryId.HasValue && Enum.IsDefined(typeof(Countries), countryId.Value))
            {
                var country = (Countries)countryId.Value;
                query = query.Where(p => p.Country == country);
            }

            // Filter by status
            if (statusId.HasValue && Enum.IsDefined(typeof(PotentialOrderStatus), statusId.Value))
            {
                var status = (PotentialOrderStatus)statusId.Value;
                query = query.Where(p => p.Status == status);
            }

            // Filter by store
            if (storeId.HasValue)
            {
                var storeName = await _context.ManufacturingCompanies
                    .Where(m => m.Id == storeId.Value)
                    .Select(m => m.Name)
                    .FirstOrDefaultAsync();
                if (storeName != null)
                    query = query.Where(p => p.StoreName == storeName);
            }

            // Filter by order source
            if (orderSourceId.HasValue && Enum.IsDefined(typeof(OrderSourceEnum), orderSourceId.Value))
            {
                var orderSource = (OrderSourceEnum)orderSourceId.Value;
                if (QueryFilteringService.IsMetaSource(orderSource))
                    query = query.Where(p => p.OrderSource == OrderSourceEnum.فيسبوك || p.OrderSource == OrderSourceEnum.انستغرام);
                else
                    query = query.Where(p => p.OrderSource == orderSource);
            }

            // Filter by employee (Admin/ExecutiveDirector only)
            if (!string.IsNullOrWhiteSpace(employeeId) && (User.IsInRole("Admin") || User.IsInRole("ExecutiveDirector")))
            {
                query = query.Where(p => p.ApplicationUserId == employeeId);
            }

            // Filter by date range (CreatedDate)
            if (startDate.HasValue)
                query = query.Where(p => p.CreatedDate >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(p => p.CreatedDate < endDate.Value.AddDays(1));

            // Global search across all columns
            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{search}%";

                // Match enum values by name in C# (can't do ToString() in SQL)
                var matchingCountries = Enum.GetValues(typeof(Countries)).Cast<Countries>()
                    .Where(c => c.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var matchingStatuses = Enum.GetValues(typeof(PotentialOrderStatus)).Cast<PotentialOrderStatus>()
                    .Where(s => s.ToString().Replace("_", " ").Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var matchingOrderSources = Enum.GetValues(typeof(OrderSourceEnum)).Cast<OrderSourceEnum>()
                    .Where(o => o.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var matchingEmployeeUserIds = _context.Employees
                    .Where(e => e.Name != null && e.Name.Contains(search))
                    .Select(e => e.ApplicationUserId)
                    .ToList();

                query = query.Where(p =>
                    EF.Functions.Like(p.CustomerName, pattern) ||
                    EF.Functions.Like(p.StoreName, pattern) ||
                    EF.Functions.Like(p.ChatUrl, pattern) ||
                    (p.PhoneNumber != null && EF.Functions.Like(p.PhoneNumber, pattern)) ||
                    matchingCountries.Contains(p.Country) ||
                    matchingStatuses.Contains(p.Status) ||
                    matchingOrderSources.Contains(p.OrderSource) ||
                    matchingEmployeeUserIds.Contains(p.ApplicationUserId));
            }

            var totalItems = await query.CountAsync();

            var items = await query
                .OrderByDescending(p => p.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PotentialOrderViewModel
                {
                    Id = p.Id,
                    CustomerName = p.CustomerName,
                    Country = p.Country,
                    ChatUrl = p.ChatUrl,
                    StoreName = p.StoreName,
                    StoreLogoUrl = _context.ManufacturingCompanies
                        .Where(m => m.Name == p.StoreName)
                        .Select(m => m.ImageUrl)
                        .FirstOrDefault(),
                    PhoneNumber = p.PhoneNumber,
                    OrderSource = p.OrderSource,
                    Status = p.Status,
                    CreatedDate = p.CreatedDate,
                    LastEditedDate = p.LastEditedDate,
                    EmployeeName = _context.Employees
                        .Where(e => e.ApplicationUserId == p.ApplicationUserId)
                        .Select(e => e.DisplayName)
                        .FirstOrDefault(),
                    EmployeeImage = _context.Employees
                        .Where(e => e.ApplicationUserId == p.ApplicationUserId)
                        .Select(e => e.ImageUrl)
                        .FirstOrDefault()
                })
                .ToListAsync();

            var viewModel = new PotentialOrderListViewModel
            {
                PaginationViewModel = new PaginationViewModel<PotentialOrderViewModel>
                {
                    Items = items,
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalItems = totalItems
                },
                Countries = Enum.GetValues(typeof(Countries)).Cast<Countries>().ToList(),
                Statuses = Enum.GetValues(typeof(PotentialOrderStatus)).Cast<PotentialOrderStatus>().ToList(),
                CountryImageUrls = _dataCacheService.GetCachedCountryImageUrls(),
            };

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView(viewModel);

            return View(viewModel);
        }

        [HttpPost]
        // Temporarily restricted to Admin only — was: [Authorize(Roles = "Admin,CallCenter,FollowUpDepartment,ExecutiveDirector")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateStatusForMultiple(List<string> ids)
        {
            if (ids == null || !ids.Any())
                return Json(new { success = false, message = "لم يتم تحديد أي طلبات" });

            var intIds = ids.Select(int.Parse).ToList();

            var potentialOrders = await _context.PotentialOrders
                .Where(p => intIds.Contains(p.Id) && p.Status != PotentialOrderStatus.تم_إرسال_العرض_6)
                .ToListAsync();

            if (!potentialOrders.Any())
                return Json(new { success = false, message = "لا توجد طلبات قابلة للترقية" });

            var now = _timeService.GetIstanbulTimeWithOffset();

            foreach (var po in potentialOrders)
            {
                po.Status = po.Status + 1;
                po.LastEditedDate = now;
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "تم تحديث حالة الطلبات بنجاح" });
        }
    }
}
