using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using lotus_blue.Data;
using lotus_blue.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using lotus_blue.Models.ViewModel;
using Microsoft.AspNetCore.Identity;
using lotus_blue.Roles;
using lotus_blue.OrderStatus;
using System.Drawing;
using static lotus_blue.Models.Common;
using lotus_blue.API;
using Newtonsoft.Json;
using lotus_blue.Services;
using DotNetCorePdf.Enums;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static NuGet.Packaging.PackagingConstants;
using System.Drawing.Printing;
using QRCoder;
using System.Net.NetworkInformation;
using System.Reflection.PortableExecutable;
using Microsoft.AspNetCore.SignalR;
using lotus_blue.Hubs;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using Z.EntityFramework.Plus;
using System.Text.Json;
using lotus_blue.Attributes;
using Microsoft.CodeAnalysis;
namespace lotus_blue.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly RoleAuthorizationService _roleAuthService;
        private readonly RESTAPI _restApi;
        private readonly GetCurrentTimeInIstanbul _timeService;
        private readonly PdfReportGenerator _reportGenerator;
        private readonly DeliveryCompanyService _deliveryCompanyService;
        private readonly FinancialService _financialService;
        private readonly DynamicCommon _dynamicCommon;
        private readonly RESTAPI _restapi;
        private readonly PdfReportGenertorOrderDetails _pdfReportGenertorOrderDetails;
        private readonly CacheService _cacheService;
        private readonly DataCacheService _dataCacheService;
        private readonly DecimalFormattingService _decimalFormattingService;
        private readonly OrderService _orderService;
        private readonly CurrencyExchangeService _currencyExchangeService;
        private readonly IHubContext<OrderHub> _hubContext;
        private readonly QueryFilteringService _queryFilteringService;
        private readonly FileUploadService _fileUploadService;

        // Temporary images for collaborative status selections.
        // Stored in memory to avoid requiring a new column on OrderStatusUpdateSelection.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, string> _temporaryStatusSelectionFailureImages = new();


        public OrderController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
            RESTAPI restApi, GetCurrentTimeInIstanbul timeService, PdfReportGenerator reportGenerator,
            DeliveryCompanyService deliveryCompanyService, FinancialService financialService,
            DynamicCommon dynamicCommon, RESTAPI restapi, PdfReportGenertorOrderDetails pdfReportGenertorOrderDetails,
             CacheService cacheService,
            DataCacheService dataCacheService,
            DecimalFormattingService decimalFormattingService,
            OrderService orderService,
            CurrencyExchangeService currencyExchangeService, IHubContext<OrderHub> hubContext,
            QueryFilteringService queryFilteringService,
            FileUploadService fileUploadService)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _roleAuthService = new RoleAuthorizationService();
            _restApi = restApi;
            _timeService = timeService;
            _reportGenerator = reportGenerator;
            _deliveryCompanyService = deliveryCompanyService;
            _financialService = financialService;
            _dynamicCommon = dynamicCommon;
            _restapi = restapi;
            _pdfReportGenertorOrderDetails = pdfReportGenertorOrderDetails;
            _cacheService = cacheService;
            _dataCacheService = dataCacheService;
            _decimalFormattingService = decimalFormattingService;
            _orderService = orderService;
            _currencyExchangeService = currencyExchangeService;
            _hubContext = hubContext;
            _queryFilteringService = queryFilteringService;
            _fileUploadService = fileUploadService;
        }

        // Live total-price validator for the Create/Edit order forms.
        // Mirrors the server-side gate in CreateOrder/EditOrder: looks up CountryMinimumPrices
        // for the (country, manufacturingCompany) pair and checks TotalPrice >= MinimumPriceForOffers.
        // Admin/ExecutiveDirector/FollowUpDepartment bypass the rule (matches their bypass on submit).
        [HttpGet]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment,CallCenter")]
        public async Task<IActionResult> ValidateTotalPrice(
            Common.Countries? country,
            int? manufacturingCompanyId,
            decimal? totalPrice,
            bool strict = false)
        {
            // مهم:
            // هذا الأكشن يتم استدعاؤه Live أثناء الكتابة في إنشاء/تعديل الطلب.
            // لذلك لا نرجع valid=false أثناء الكتابة أو عند نقص البيانات؛ لأن ذلك يجعل الـ JS يضيف Error/Popup
            // وقد يقطع الكتابة أو اللصق داخل input. الحماية النهائية موجودة داخل Create/Edit عند الحفظ.
            if (User.IsInRole("Admin") || User.IsInRole("ExecutiveDirector") || User.IsInRole("FollowUpDepartment"))
            {
                return Json(new { valid = true, bypass = true });
            }

            if (totalPrice == null
                || !country.HasValue
                || !manufacturingCompanyId.HasValue
                || manufacturingCompanyId.Value <= 0)
            {
                return Json(new { valid = true, pending = true });
            }

            var countryMinPrice = await _context.CountryMinimumPrices
                .AsNoTracking()
                .FirstOrDefaultAsync(cmp => cmp.Country == country.Value
                    && cmp.ManufacturingCompanyId == manufacturingCompanyId.Value);

            if (countryMinPrice == null)
            {
                return Json(new { valid = true });
            }

            if (totalPrice.Value < countryMinPrice.MinimumPriceForOffers)
            {
                var message = $"لا يمكننك تنزيل طلب بأقل من الحد الأدنى {countryMinPrice.MinimumPriceForOffers}";

                // strict=false هو الوضع الآمن للكتابة Live؛ نرجع warning فقط ولا نكسر الـ input.
                // لو احتجتي فحص Live صارم لاحقًا، ابعتي strict=true من الـ JS.
                if (!strict)
                {
                    return Json(new
                    {
                        valid = true,
                        warning = true,
                        message,
                        minimum = countryMinPrice.MinimumPriceForOffers
                    });
                }

                return Json(new
                {
                    valid = false,
                    message,
                    minimum = countryMinPrice.MinimumPriceForOffers
                });
            }

            return Json(new { valid = true });
        }

        // ============================================================
        // Product minimum selling price validation
        // يتحقق من الحد الأدنى للبيع لكل منتج رئيسي حسب البلد والمتجر والكمية.
        // ============================================================
        [HttpGet]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment,CallCenter")]
        public async Task<IActionResult> ValidateProductMinimumSellingPrice(
            Common.Countries? country,
            int? manufacturingCompanyId,
            decimal? totalPrice,
            string? mainWarehouseIds,
            string? amounts,
            bool strict = false)
        {
            if (!country.HasValue
                || !manufacturingCompanyId.HasValue
                || manufacturingCompanyId.Value <= 0
                || !totalPrice.HasValue
                || string.IsNullOrWhiteSpace(mainWarehouseIds))
            {
                return Json(new { valid = true });
            }

            var productIds = ParseCsvIntList(mainWarehouseIds);
            var quantities = ParseCsvIntList(amounts);

            if (productIds.Count == 0)
            {
                return Json(new { valid = true });
            }

            var quantityByMainWarehouse = new Dictionary<int, int>();

            for (var i = 0; i < productIds.Count; i++)
            {
                var productId = productIds[i];
                var qty = i < quantities.Count && quantities[i] > 0 ? quantities[i] : 1;

                if (!quantityByMainWarehouse.ContainsKey(productId))
                {
                    quantityByMainWarehouse[productId] = 0;
                }

                quantityByMainWarehouse[productId] += qty;
            }

            var selectedMainWarehouseIds = quantityByMainWarehouse.Keys.ToList();

            var minimumRows = await _context.ProductMinimumSellingPrices
                .AsNoTracking()
                .Include(x => x.MainWarehouse)
                .Where(x =>
                    x.Country == country.Value &&
                    x.ManufacturingCompanyId == manufacturingCompanyId.Value &&
                    selectedMainWarehouseIds.Contains(x.MainWarehouseId))
                .ToListAsync();

            if (minimumRows.Count == 0)
            {
                return Json(new { valid = true });
            }

            decimal requiredMinimumTotal = 0;
            var details = new List<string>();

            foreach (var row in minimumRows)
            {
                var qty = quantityByMainWarehouse.TryGetValue(row.MainWarehouseId, out var foundQty)
                    ? foundQty
                    : 1;

                var productRequiredMinimum = row.MinimumSellingPrice * qty;
                requiredMinimumTotal += productRequiredMinimum;

                var productName = row.MainWarehouse?.Name ?? "المنتج";
                details.Add($"{productName}: {row.MinimumSellingPrice:0.00} × {qty} = {productRequiredMinimum:0.00}");
            }

            if (totalPrice.Value < requiredMinimumTotal)
            {
                var message = "السعر أقل من الحد الأدنى لسعر البيع. الحد الأدنى المطلوب هو "
                    + requiredMinimumTotal.ToString("0.00")
                    + ". التفاصيل: "
                    + string.Join(" | ", details);

                // هذا أكشن Live أثناء الكتابة. لا نرجع valid=false إلا لو strict=true
                // عشان المدخلات واللصق يفضلوا شغالين طبيعي.
                if (!strict)
                {
                    return Json(new
                    {
                        valid = true,
                        warning = true,
                        minimumPrice = requiredMinimumTotal,
                        message
                    });
                }

                return Json(new
                {
                    valid = false,
                    minimumPrice = requiredMinimumTotal,
                    message
                });
            }

            return Json(new { valid = true });
        }

        private async Task<(bool IsValid, string Message)> ValidateOrderProductMinimumSellingPriceAsync(OrderViewModel model)
        {
            if (model == null
                || !model.ManufacturingCompanyId.HasValue
                || model.ManufacturingCompanyId.Value <= 0
                || model.SelectedWarehouses == null)
            {
                return (true, "");
            }

            var selectedWarehouseRows = model.SelectedWarehouses
                .Where(x => x.WarehouseId > 0)
                .Select(x => new
                {
                    x.WarehouseId,
                    Amount = x.Amount > 0 ? x.Amount : 1
                })
                .ToList();

            if (selectedWarehouseRows.Count == 0)
            {
                return (true, "");
            }

            var warehouseIds = selectedWarehouseRows
                .Select(x => x.WarehouseId)
                .Distinct()
                .ToList();

            var warehouses = await _context.Warehouses
                .AsNoTracking()
                .Where(x => warehouseIds.Contains(x.Id))
                .Select(x => new
                {
                    x.Id,
                    MainWarehouseId = (int?)x.MainWarehouseId
                })
                .ToListAsync();

            var quantityByMainWarehouse = new Dictionary<int, int>();

            foreach (var item in selectedWarehouseRows)
            {
                var warehouse = warehouses.FirstOrDefault(x => x.Id == item.WarehouseId);

                if (warehouse?.MainWarehouseId == null || warehouse.MainWarehouseId.Value <= 0)
                {
                    continue;
                }

                var mainWarehouseId = warehouse.MainWarehouseId.Value;

                if (!quantityByMainWarehouse.ContainsKey(mainWarehouseId))
                {
                    quantityByMainWarehouse[mainWarehouseId] = 0;
                }

                quantityByMainWarehouse[mainWarehouseId] += item.Amount;
            }

            if (quantityByMainWarehouse.Count == 0)
            {
                return (true, "");
            }

            var mainWarehouseIds = quantityByMainWarehouse.Keys.ToList();
            var storeId = model.ManufacturingCompanyId.Value;

            var minimumRows = await _context.ProductMinimumSellingPrices
                .AsNoTracking()
                .Include(x => x.MainWarehouse)
                .Where(x =>
                    x.Country == model.Country &&
                    x.ManufacturingCompanyId == storeId &&
                    mainWarehouseIds.Contains(x.MainWarehouseId))
                .ToListAsync();

            if (minimumRows.Count == 0)
            {
                return (true, "");
            }

            decimal requiredMinimumTotal = 0;
            var details = new List<string>();

            foreach (var row in minimumRows)
            {
                var qty = quantityByMainWarehouse.TryGetValue(row.MainWarehouseId, out var foundQty)
                    ? foundQty
                    : 1;

                var productRequiredMinimum = row.MinimumSellingPrice * qty;
                requiredMinimumTotal += productRequiredMinimum;

                var productName = row.MainWarehouse?.Name ?? "المنتج";
                details.Add($"{productName}: {row.MinimumSellingPrice:0.00} × {qty} = {productRequiredMinimum:0.00}");
            }

            if (model.TotalPrice < requiredMinimumTotal)
            {
                return (false,
                    "السعر أقل من الحد الأدنى لسعر البيع. الحد الأدنى المطلوب هو "
                    + requiredMinimumTotal.ToString("0.00")
                    + ". التفاصيل: "
                    + string.Join(" | ", details));
            }

            return (true, "");
        }

        private static List<int> ParseCsvIntList(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new List<int>();
            }

            return value
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x.Trim(), out var number) ? number : 0)
                .Where(x => x > 0)
                .ToList();
        }

        // Generic filter counts endpoint — returns {id, count} pairs for any filter dimension
        [HttpGet]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
        public async Task<IActionResult> GetFilterCounts(
            string dimension,
            Common.Countries? countryId = null,
            OrderStatusEnum? orderstatusId = null,
            string? cityId = null,
            int? storeId = null,
            int? deliverycompanyId = null,
            int? deliveryrepresentativeId = null,
            string? search = null,
            OrderSourceEnum? ordersourceId = null,
            string? failureReason = null,
            int? productId = null,
            string? employeeId = null,
            bool? fromcomments = null,
            bool? gender = null,
            bool? isOffers = null,
            bool? isDiscount = null,
            bool? isBonus = null,
            bool? isspecialClients = null,
            bool? isFixedAndDelivered = null,
            bool? isHidden = null,
            bool? IsComplaints = null,
            bool? isPaid = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier);
            IQueryable<Order> query = _context.Orders.AsNoTracking();

            if (User.IsInRole("CallCenter"))
            {
                query = query.Where(o => o.ApplicationUserId == currentUser);
            }
            if (User.IsInRole("FollowUpDepartment"))
            {
                query = query.Where(o => o.ManufacturingCompany.EmployeeManufacturingCompanies.Any(a => a.ApplicationUserId == currentUser && a.CanSeeManufacturingCompany));
            }

            // Null out the dimension being counted so it doesn't filter itself
            switch (dimension)
            {
                case "orderStatus": orderstatusId = null; break;
                case "country": countryId = null; break;
                case "city": cityId = null; break;
                case "store": storeId = null; break;
                case "deliveryCompany": deliverycompanyId = null; break;
                case "deliveryRepresentative": deliveryrepresentativeId = null; break;
                case "orderSource": ordersourceId = null; break;
            }

            query = _queryFilteringService.ApplyFilters(
                query, countryId, orderstatusId, ordersourceId, storeId, deliverycompanyId, deliveryrepresentativeId,
                productId, startDate, endDate, cityId, search, employeeId, fromcomments, gender,
                isOffers, isDiscount, isBonus, isspecialClients, isFixedAndDelivered, isHidden, IsComplaints, isPaid, null, "",
                failureReason);

            object counts;
            switch (dimension)
            {
                case "orderStatus":
                    counts = await query.GroupBy(o => (int)o.OrderStatus)
                        .Select(g => new { id = g.Key.ToString(), count = g.Count() }).ToListAsync();
                    break;
                case "country":
                    counts = await query.GroupBy(o => (int)o.Country)
                        .Select(g => new { id = g.Key.ToString(), count = g.Count() }).ToListAsync();
                    break;
                case "city":
                    counts = await query.Where(o => o.State != null && o.State != "")
                        .GroupBy(o => o.State)
                        .Select(g => new { id = g.Key, count = g.Count() }).ToListAsync();
                    break;
                case "store":
                    counts = await query.Where(o => o.ManufacturingCompanyId != null)
                        .GroupBy(o => o.ManufacturingCompanyId)
                        .Select(g => new { id = g.Key.ToString(), count = g.Count() }).ToListAsync();
                    break;
                case "deliveryCompany":
                    counts = await query.Where(o => o.DeliveryCompanyId != null)
                        .GroupBy(o => o.DeliveryCompanyId)
                        .Select(g => new { id = g.Key.ToString(), count = g.Count() }).ToListAsync();
                    break;
                case "deliveryRepresentative":
                    counts = await query.Where(o => o.DeliveryCompanyId != null)
                        .GroupBy(o => o.DeliveryCompanyId)
                        .Select(g => new { id = g.Key.ToString(), count = g.Count() }).ToListAsync();
                    break;
                case "orderSource":
                    // ميتا is a virtual filter (expands to فيسبوك + انستغرام in ApplyFilters).
                    // Orders are stored with their real source, so group by the raw value.
                    // The client should sum فيسبوك + انستغرام counts when the ميتا pill is active.
                    counts = await query.GroupBy(o => (int)o.OrderSource)
                        .Select(g => new { id = g.Key.ToString(), count = g.Count() }).ToListAsync();
                    break;
                default:
                    return BadRequest("Invalid dimension");
            }

            return Ok(counts);
        }

        // Returns distinct failure reasons with counts, respecting all active filters
        [HttpGet]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
        public async Task<IActionResult> GetFailureReasonCounts(
            Common.Countries? countryId = null,
            OrderStatusEnum? orderstatusId = null,
            string? cityId = null,
            int? storeId = null,
            int? deliverycompanyId = null,
            int? deliveryrepresentativeId = null,
            string? search = null,
            OrderSourceEnum? ordersourceId = null,
            int? productId = null,
            string? employeeId = null,
            bool? fromcomments = null,
            bool? gender = null,
            bool? isOffers = null,
            bool? isDiscount = null,
            bool? isBonus = null,
            bool? isspecialClients = null,
            bool? isFixedAndDelivered = null,
            bool? isHidden = null,
            bool? IsComplaints = null,
            bool? isPaid = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier);
            IQueryable<Order> query = _context.Orders.AsNoTracking();

            if (User.IsInRole("CallCenter"))
                query = query.Where(o => o.ApplicationUserId == currentUser);
            if (User.IsInRole("FollowUpDepartment"))
                query = query.Where(o => o.ManufacturingCompany.EmployeeManufacturingCompanies.Any(a => a.ApplicationUserId == currentUser && a.CanSeeManufacturingCompany));

            query = _queryFilteringService.ApplyFilters(
                query, countryId, orderstatusId, ordersourceId, storeId, deliverycompanyId, deliveryrepresentativeId,
                productId, startDate, endDate, cityId, search, employeeId, fromcomments, gender,
                isOffers, isDiscount, isBonus, isspecialClients, isFixedAndDelivered, isHidden, IsComplaints, isPaid, null, "",
                null);

            var failureStatuses = new[]
            {
                OrderStatusEnum.فشل_التسليم,
                OrderStatusEnum.فشل_التسليم_2,
                OrderStatusEnum.فشل_التسليم_3,
                OrderStatusEnum.فشل_التسليم_4,
                OrderStatusEnum.فشل_التسليم_5,
                OrderStatusEnum.فشل_التسليم_6,
                OrderStatusEnum.فشل_التسليم_7,
                OrderStatusEnum.انتظار_المعالجة,
                OrderStatusEnum.الطلبات_المرجعة,
                OrderStatusEnum.أرشيف_المرجع,
            };

            var matchingOrderIds = await query.Select(o => (int?)o.Id).ToListAsync();

            var counts = await _context.OrderStatusHistories
                .Where(osh => matchingOrderIds.Contains(osh.OrderId)
                    && osh.Reason != null && osh.Reason != ""
                    && osh.Status.HasValue && failureStatuses.Contains(osh.Status.Value))
                .GroupBy(osh => osh.Reason)
                .Select(g => new { id = g.Key, count = g.Count() })
                .ToListAsync();

            return Ok(counts);
        }

        // DONE (kept for backwards compatibility, delegates to GetFilterCounts logic)
        [HttpGet]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment,CallCenter")]
        public async Task<IActionResult> GetOrderStatusCounts(
            Common.Countries? countryId = null,
            string? cityId = null,
            int? storeId = null,
            int? deliverycompanyId = null,
            int? deliveryrepresentativeId = null,
            string? search = null,
            OrderSourceEnum? ordersourceId = null,
            string? failureReason = null,
            int? productId = null,
            string? employeeId = null,
            bool? fromcomments = null,
            bool? gender = null,
            bool? isOffers = null,
            bool? isDiscount = null,
            bool? isBonus = null,
            bool? isspecialClients = null,
            bool? isFixedAndDelivered = null,
            bool? isHidden = null,
            bool? IsComplaints = null,
            bool? isPaid = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier);

            IQueryable<Order> query = _context.Orders.AsNoTracking();

            if (User.IsInRole("CallCenter"))
            {
                query = query.Where(o => o.ApplicationUserId == currentUser);
            }
            if (User.IsInRole("FollowUpDepartment"))
            {
                query = query.Where(o => o.ManufacturingCompany.EmployeeManufacturingCompanies.Any(a => a.ApplicationUserId == currentUser && a.CanSeeManufacturingCompany));
            }

            query = _queryFilteringService.ApplyFilters(
                query, countryId, null, ordersourceId, storeId, deliverycompanyId, deliveryrepresentativeId,
                productId, startDate, endDate, cityId, search, employeeId, fromcomments, gender,
                isOffers, isDiscount, isBonus, isspecialClients, isFixedAndDelivered, isHidden, IsComplaints, isPaid, null, "",
                failureReason);

            var counts = await query
                .GroupBy(o => (int)o.OrderStatus)
                .Select(g => new { statusId = g.Key, count = g.Count() })
                .ToListAsync();

            return Ok(counts);
        }


        [HttpGet]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment,DeliveryCompany,DeliveryRepresentative,CallCenter")]
        public async Task<IActionResult> DeliveryCostsByOrderIds(string ids)
        {
            var orderIds = ParseDeliveryCostOrderIds(ids);

            if (orderIds.Count == 0)
            {
                return Json(new
                {
                    success = true,
                    items = Array.Empty<object>()
                });
            }

            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier);

            IQueryable<Order> query = _context.Orders.AsNoTracking()
                .Where(order => orderIds.Contains(order.Id));

            if (User.IsInRole("CallCenter"))
            {
                query = query.Where(order => order.ApplicationUserId == currentUser);
            }

            if (User.IsInRole("FollowUpDepartment"))
            {
                query = query.Where(order =>
                    order.ManufacturingCompany.EmployeeManufacturingCompanies.Any(access =>
                        access.ApplicationUserId == currentUser &&
                        access.CanSeeManufacturingCompany));
            }

            if (User.IsInRole("DeliveryCompany") || User.IsInRole("DeliveryRepresentative"))
            {
                query = query.Where(order => order.DeliveryCompany.UserId == currentUser);
            }

            var items = await query
                .Select(order => new
                {
                    orderId = order.Id,
                    deliveryCost = order.DeliveryPrice
                })
                .ToListAsync();

            return Json(new
            {
                success = true,
                items
            });
        }

        private static List<int> ParseDeliveryCostOrderIds(string ids)
        {
            if (string.IsNullOrWhiteSpace(ids))
            {
                return new List<int>();
            }

            return ids
                .Split(new[] { ',', ';', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => int.TryParse(value.Trim(), out var orderId) ? orderId : 0)
                .Where(orderId => orderId > 0)
                .Distinct()
                .Take(300)
                .ToList();
        }

        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment,DeliveryCompany,DeliveryRepresentative,CallCenter")]
        public async Task<IActionResult> Index(
      int page = 1,
      int? pageSize = null,
      Common.Countries? countryId = null,
      string? cityId = null,
      int? storeId = null,
      int? deliverycompanyId = null,
      int? deliveryrepresentativeId = null,
      OrderStatusEnum? orderstatusId = null,
      string? search = null,
      bool? isBonus = null,
      string? employeeId = null,
      OrderSourceEnum? ordersourceId = null,
      string? failureReason = null,
      bool showDebugQuery = false
  )
        {
            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var sessionPrefix = "Order_Index_";

            decimal totalOrderPriceDollar = 0;
            decimal totalOrderPriceTRY = 0;

            // A fresh navigation to /Order (non-AJAX, no filter query params) should start clean.
            // Subsequent AJAX refreshes sent by the page always include the current filter values,
            // so honoring session only on AJAX requests keeps pagination/filter state across those
            // while preventing stale filters from bleeding in from other pages or earlier visits.
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            bool hasAnyFilterParam = countryId.HasValue || cityId != null || storeId.HasValue
                || deliverycompanyId.HasValue || deliveryrepresentativeId.HasValue
                || orderstatusId.HasValue || !string.IsNullOrEmpty(search) || isBonus.HasValue
                || !string.IsNullOrEmpty(employeeId) || ordersourceId.HasValue
                || !string.IsNullOrEmpty(failureReason) || pageSize.HasValue;

            if (!isAjax && !hasAnyFilterParam)
            {
                foreach (var key in new[]
                {
                    "PageSize", "CountryId", "CityId", "StoreId", "DeliveryCompanyId",
                    "DeliveryRepresentativeId", "OrderStatusId", "Search", "IsBonus",
                    "EmployeeId", "OrderSourceId", "FailureReason"
                })
                {
                    HttpContext.Session.Remove($"{sessionPrefix}{key}");
                }
                pageSize = pageSize ?? 10;
            }
            else
            {
                // Retrieve filter values from session if they are not provided by the request
                pageSize = pageSize ?? HttpContext.Session.GetInt32($"{sessionPrefix}PageSize") ?? 10;
                countryId = countryId ?? (Enum.TryParse(HttpContext.Session.GetString($"{sessionPrefix}CountryId"), out Common.Countries country) ? country : (Common.Countries?)null);
                cityId = cityId ?? HttpContext.Session.GetString($"{sessionPrefix}CityId");
                storeId = storeId ?? HttpContext.Session.GetInt32($"{sessionPrefix}StoreId");
                deliverycompanyId = deliverycompanyId ?? HttpContext.Session.GetInt32($"{sessionPrefix}DeliveryCompanyId");
                deliveryrepresentativeId = deliveryrepresentativeId ?? HttpContext.Session.GetInt32($"{sessionPrefix}DeliveryRepresentativeId");
                orderstatusId = orderstatusId ?? (Enum.TryParse(HttpContext.Session.GetString($"{sessionPrefix}OrderStatusId"), out OrderStatusEnum status) ? status : (OrderStatusEnum?)null);
                search = search ?? HttpContext.Session.GetString($"{sessionPrefix}Search");
                isBonus = isBonus ?? (bool.TryParse(HttpContext.Session.GetString($"{sessionPrefix}IsBonus"), out bool bonus) ? bonus : (bool?)null);
                employeeId = employeeId ?? HttpContext.Session.GetString($"{sessionPrefix}EmployeeId");
            }

            // Query to retrieve orders
            IQueryable<Order> query = _context.Orders
                .AsNoTracking();



            // Apply specific role-based filters
            if (User.IsInRole("DeliveryCompany") || User.IsInRole("DeliveryRepresentative"))
            {
                var failureClassStatuses = new[]
                {
                    OrderStatusEnum.فشل_التسليم,
                    OrderStatusEnum.الطلبات_المرجعة,
                    OrderStatusEnum.أرشيف_المرجع,
                    OrderStatusEnum.فشل_التسليم_2,
                    OrderStatusEnum.فشل_التسليم_3,
                    OrderStatusEnum.فشل_التسليم_4,
                    OrderStatusEnum.فشل_التسليم_5,
                    OrderStatusEnum.فشل_التسليم_6,
                    OrderStatusEnum.فشل_التسليم_7,
                };

                query = query.Where(o =>
                    o.DeliveryCompany.UserId == currentUser &&
                    o.OrderStatus != OrderStatusEnum.الطلبات_الغير_معرفة &&
                    o.OrderStatus != OrderStatusEnum.الطلبات_المعلقة &&
                    !failureClassStatuses.Contains(o.OrderStatus) &&
                    !o.IsHidden);
            }

            // Apply specific role-based filters
            if (User.IsInRole("CallCenter"))
            {
                query = query.Where(o => o.ApplicationUserId == currentUser);

            }

            // Apply filters using the ApplyFilters service
            query = _queryFilteringService.ApplyFilters(
                query, countryId, orderstatusId, ordersourceId, storeId, deliverycompanyId, deliveryrepresentativeId, null,
                null, null, cityId, search, employeeId, null, null, isBonus, null, null, null, null, null, null, null, null, sessionPrefix,
                failureReason);

            // صفحة تحديث جميع الحالات لا تعرض أي طلب عليه اختيار مؤقت من صفحة فشل التسليم أو تم التسليم.
            var requestPath = Request.Path.Value ?? string.Empty;
            if (requestPath.Contains("UpdateAllStatuses", StringComparison.OrdinalIgnoreCase))
            {
                var nowForSelections = _timeService.GetIstanbulTimeWithOffset();
                var activeSelectionsQuery = GetActiveStatusUpdateSelectionsQuery(nowForSelections);
                query = query.Where(o => !activeSelectionsQuery.Any(s => s.OrderId == o.Id));
            }

            // Determine if any filters are applied
            bool isFilterApplied = countryId != null || cityId != null || storeId != null ||
                                   deliverycompanyId != null || deliveryrepresentativeId != null ||
                                   orderstatusId != null || !string.IsNullOrEmpty(search) ||
                                   isBonus != null || !string.IsNullOrEmpty(employeeId) ||
                                   !string.IsNullOrEmpty(failureReason);


            // Ensure query is ordered before pagination
            var orderedQuery = query.OrderByDescending(o => o.LastEditedDate); // Correct ordering

            string? debugQuery = (User.IsInRole("Admin") && showDebugQuery) ? orderedQuery.ToQueryString() : null;

            // Apply pagination only if pageSize is not null
            IQueryable<Order> paginatedQuery = orderedQuery; // Assign to IQueryable by default

            if (pageSize != null)
            {
                paginatedQuery = orderedQuery
                    .Skip((page - 1) * pageSize.Value)
                    .Take(pageSize.Value);
            }

            // Execute the query and retrieve the orders
            var orders = paginatedQuery
                .Select(o => new OrderViewModel
                {
                    Id = o.Id,
                    IsPaid = o.IsPaid,
                    TelephoneNumber = o.TelephoneNumber,
                    FixedOrderDate = o.FixedOrderDate,
                    CreatedDate = o.CreatedDate,
                    Country = o.Country,
                    CustomerName = o.CustomerName,
                    State = o.State,
                    OrderSource = o.OrderSource,
                    SourceName = o.SourceName,
                    Gender = o.Gender,
                    ManufacturingCompany = o.ManufacturingCompany == null ? null : new ManufacturingCompanyViewModel
                    {
                        Id = o.ManufacturingCompany.Id,
                        Name = o.ManufacturingCompany.Name,
                        Logo = o.ManufacturingCompany.ImageUrl,
                    },
                    DeliveryCompany = o.DeliveryCompany == null ? null : new DeliveryCompanyViewModel
                    {
                        Id = o.DeliveryCompany.Id,
                        Name = o.DeliveryCompany.DisplayName,
                        Logo = o.DeliveryCompany.ImageUrl,
                    },
                    Employee = _context.Employees
                        .Where(e => e.ApplicationUserId == o.ApplicationUserId)
                        .Select(e => new GetDataListViewModel
                        {
                            Name = e.DisplayName,
                            LogoUrl = e.ImageUrl
                        })
                        .FirstOrDefault() ?? new GetDataListViewModel { Name = "null", LogoUrl = "null" },
                    LastEditedDate = o.LastEditedDate,
                    OrderStatus = o.OrderStatus,
                    chatUrl = o.Chaturl,
                    TotalPrice = o.TotalPrice,
                    DeliveryCost = o.DeliveryPrice,
                })
                .ToList();

            var totalItems = await query.CountAsync(); // Count total items asynchronously
            var allOrderIds = paginatedQuery.Select(order => order.Id).ToList();

            // Fetch latest failure reason for each order from OrderStatusHistories
            var failureReasons = await _context.OrderStatusHistories
                .Where(oh => allOrderIds.Contains(oh.OrderId.Value) && oh.Reason != null)
                .GroupBy(oh => oh.OrderId)
                .Select(g => new { OrderId = g.Key, Reason = g.OrderByDescending(oh => oh.Id).First().Reason })
                .ToDictionaryAsync(x => x.OrderId.Value, x => x.Reason);

            // Set FailureReasonDisplay on each order
            // مهم: لو الحالة الحالية قيد التوصيل لا نعرض سبب فشل قديم من تاريخ سابق.
            foreach (var order in orders)
            {
                if (order.OrderStatus == OrderStatusEnum.قيد_التوصيل)
                {
                    order.FailureReasonDisplay = null;
                    continue;
                }

                order.FailureReasonDisplay = failureReasons.GetValueOrDefault(order.Id);
            }

            // Show the failure reason column when the selected filter is a failure stage,
            // انتظار_المعالجة, الطلبات_المرجعة, أرشيف_المرجع, or قيد_التوصيل
            bool showFailureReasonColumn = orderstatusId.HasValue && (
                OrderStatusHelper.IsFailureStatus(orderstatusId.Value)
                || orderstatusId.Value == OrderStatusEnum.انتظار_المعالجة
                || orderstatusId.Value == OrderStatusEnum.الطلبات_المرجعة
                || orderstatusId.Value == OrderStatusEnum.أرشيف_المرجع
                || orderstatusId.Value == OrderStatusEnum.قيد_التوصيل
            );

            if (countryId.HasValue || cityId != null || storeId.HasValue || deliverycompanyId.HasValue || orderstatusId.HasValue || !string.IsNullOrEmpty(search) || !string.IsNullOrEmpty(employeeId) || deliveryrepresentativeId.HasValue)
            {
                decimal totalorderpriceholder = await _orderService.CalculateTotalPriceInUSDForOrdersAsync(allOrderIds);
                decimal totalorderdeliveryCompanyPricepriceholder = await _deliveryCompanyService.CalculateTotalDeliveryPricesInUSDForOrdersByCountryAsync(allOrderIds);
                totalOrderPriceDollar = totalorderpriceholder - totalorderdeliveryCompanyPricepriceholder;
                totalOrderPriceTRY = _currencyExchangeService.ConvertToTurkishLira(totalOrderPriceDollar);
            }

            // Create the PaginationViewModel
            var paginationViewModel = new PaginationViewModel<OrderViewModel>
            {
                Items = orders,
                CurrentPage = page,
                PageSize = pageSize ?? totalItems,
                TotalItems = totalItems
            };



            // Retrieve cached data
            var viewModel = new HomeViewModel
            {
                PaginationViewModel = paginationViewModel,
                OrderStatuses = _dataCacheService.GetCachedOrderStatuses(),
                OrderStatusesForDeliveryCompanyAndRepresentative = _dataCacheService.GetCachedOrderStatusesForDeliveryCompanyAndRepresentative(),
                Countries = _dataCacheService.GetCachedCountries(),
                OrderStatusIconUrls = _dataCacheService.GetCachedOrderStatusIconUrls(),
                CountryImageUrls = _dataCacheService.GetCachedCountryImageUrls(),
                SocialMediaIconUrls = _dataCacheService.GetCachedSocialMediaIconUrls(),
                CurrencySymbols = _dataCacheService.GetCachedCurrencySymbols(),
                TotalOrderPriceDollar = _decimalFormattingService.DecimalFormat(totalOrderPriceDollar),
                TotalOrderPriceTRY = _decimalFormattingService.DecimalFormat(totalOrderPriceTRY),
                ShowFailureReasonColumn = showFailureReasonColumn,
                DebugQuery = debugQuery,
            };

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView(viewModel);

            return View(viewModel); // Return the view with the ViewModel
        }


        // DONE


        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
        public async Task<IActionResult> UpdateAllStatuses(
            int page = 1,
            int? pageSize = null,
            Common.Countries? countryId = null,
            string? cityId = null,
            int? storeId = null,
            int? deliverycompanyId = null,
            int? deliveryrepresentativeId = null,
            OrderStatusEnum? orderstatusId = null,
            string? search = null,
            bool? isBonus = null,
            string? employeeId = null,
            OrderSourceEnum? ordersourceId = null,
            string? failureReason = null,
            bool showDebugQuery = false)
        {
            if (Request.Headers["X-Requested-With"] != "XMLHttpRequest")
            {
                // لا نمسح الاختيارات المؤقتة تلقائيًا عند فتح/ريفريش الصفحة.
                // الاختيار يفضل موجود إلى أن يتم الضغط على إلغاء أو يتم التحديث النهائي.
                pageSize = 10;
                HttpContext.Session.Remove("Order_Index_PageSize");
            }
            else
            {
                pageSize ??= 10;
            }

            var result = await Index(
                page,
                pageSize,
                countryId,
                cityId,
                storeId,
                deliverycompanyId,
                deliveryrepresentativeId,
                orderstatusId,
                search,
                isBonus,
                employeeId,
                ordersourceId,
                failureReason,
                showDebugQuery);

            if (result is ViewResult viewResult)
            {
                viewResult.ViewName = "UpdateAllStatuses";
                return viewResult;
            }

            if (result is PartialViewResult partialViewResult)
            {
                partialViewResult.ViewName = "UpdateAllStatuses";
                return partialViewResult;
            }

            return result;
        }

        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
        public async Task<IActionResult> UpdateFailedDelivery(
            int page = 1,
            int? pageSize = null,
            Common.Countries? countryId = null,
            int? storeId = null,
            int? deliverycompanyId = null,
            int? deliveryrepresentativeId = null)
        {
            if (Request.Headers["X-Requested-With"] != "XMLHttpRequest")
            {
                // لا نمسح الاختيارات المؤقتة تلقائيًا عند فتح/ريفريش الصفحة.
                // الاختيار يفضل موجود إلى أن يتم الضغط على إلغاء أو يتم التحديث النهائي.
                pageSize = 10;
            }
            else
            {
                pageSize ??= 10;
            }

            return await BuildDeliveryStatusUpdatePage(
                viewName: "UpdateFailedDelivery",
                sourceStatus: OrderStatusEnum.قيد_التوصيل,
                page: page,
                pageSize: pageSize,
                countryId: countryId,
                storeId: storeId,
                deliverycompanyId: deliverycompanyId,
                deliveryrepresentativeId: deliveryrepresentativeId,
                showFailureReasonColumn: true);
        }

        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
        public async Task<IActionResult> UpdateDelivered(
            int page = 1,
            int? pageSize = null,
            Common.Countries? countryId = null,
            int? storeId = null,
            int? deliverycompanyId = null,
            int? deliveryrepresentativeId = null)
        {
            if (Request.Headers["X-Requested-With"] != "XMLHttpRequest")
            {
                // لا نمسح الاختيارات المؤقتة تلقائيًا عند فتح/ريفريش الصفحة.
                // الاختيار يفضل موجود إلى أن يتم الضغط على إلغاء أو يتم التحديث النهائي.
                pageSize = 10;
            }
            else
            {
                pageSize ??= 10;
            }

            return await BuildDeliveryStatusUpdatePage(
                viewName: "UpdateDelivered",
                sourceStatus: OrderStatusEnum.قيد_التوصيل,
                page: page,
                pageSize: pageSize,
                countryId: countryId,
                storeId: storeId,
                deliverycompanyId: deliverycompanyId,
                deliveryrepresentativeId: deliveryrepresentativeId,
                showFailureReasonColumn: false);
        }


        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
        public IActionResult UpdateDeliverySplit()
        {
            return View("UpdateDeliverySplit");
        }

        private async Task<IActionResult> BuildDeliveryStatusUpdatePage(
            string viewName,
            OrderStatusEnum sourceStatus,
            int page,
            int? pageSize,
            Common.Countries? countryId,
            int? storeId,
            int? deliverycompanyId,
            int? deliveryrepresentativeId,
            bool showFailureReasonColumn)
        {
            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier);
            pageSize = pageSize ?? 10;

            IQueryable<Order> query = _context.Orders
                .AsNoTracking()
                .Where(o => !o.IsHidden);

            // إخفاء الاختيارات المؤقتة الخاصة بالصفحة الأخرى على مستوى السيرفر.
            // مثال: لو طلب اتحدد كفشل، يختفي من صفحة تم التسليم عند أي جهاز بعد الريلود أو AJAX.
            var nowForSelections = _timeService.GetIstanbulTimeWithOffset();
            var activeSelectionsQuery = GetActiveStatusUpdateSelectionsQuery(nowForSelections);
            var pageOwnTargetStatus = viewName == "UpdateDelivered"
                ? OrderStatusEnum.تم_التسليم
                : OrderStatusEnum.فشل_التسليم;

            query = query.Where(o => !activeSelectionsQuery.Any(s =>
                s.OrderId == o.Id &&
                s.TargetStatus != pageOwnTargetStatus));

            if (User.IsInRole("FollowUpDepartment"))
            {
                query = query.Where(o =>
                    o.ManufacturingCompany.EmployeeManufacturingCompanies.Any(a =>
                        a.ApplicationUserId == currentUser &&
                        a.CanSeeManufacturingCompany));
            }

            query = _queryFilteringService.ApplyFilters(
                query,
                countryId,
                sourceStatus,
                null,
                storeId,
                deliverycompanyId,
                deliveryrepresentativeId,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "",
                null);

            var totalItems = await query.CountAsync();

            var paginatedQuery = query
                .OrderByDescending(o => o.LastEditedDate)
                .ThenByDescending(o => o.Id)
                .Skip((page - 1) * pageSize.Value)
                .Take(pageSize.Value);

            var orders = await paginatedQuery
                .Select(o => new OrderViewModel
                {
                    Id = o.Id,
                    IsPaid = o.IsPaid,
                    TelephoneNumber = o.TelephoneNumber,
                    FixedOrderDate = o.FixedOrderDate,
                    CreatedDate = o.CreatedDate,
                    Country = o.Country,
                    CustomerName = o.CustomerName,
                    State = o.State,
                    OrderSource = o.OrderSource,
                    SourceName = o.SourceName,
                    Gender = o.Gender,
                    ManufacturingCompany = o.ManufacturingCompany == null ? null : new ManufacturingCompanyViewModel
                    {
                        Id = o.ManufacturingCompany.Id,
                        Name = o.ManufacturingCompany.Name,
                        Logo = o.ManufacturingCompany.ImageUrl,
                    },
                    DeliveryCompany = o.DeliveryCompany == null ? null : new DeliveryCompanyViewModel
                    {
                        Id = o.DeliveryCompany.Id,
                        Name = o.DeliveryCompany.DisplayName,
                        Logo = o.DeliveryCompany.ImageUrl,
                    },
                    Employee = _context.Employees
                        .Where(e => e.ApplicationUserId == o.ApplicationUserId)
                        .Select(e => new GetDataListViewModel
                        {
                            Name = e.DisplayName,
                            LogoUrl = e.ImageUrl
                        })
                        .FirstOrDefault() ?? new GetDataListViewModel { Name = "null", LogoUrl = "null" },
                    LastEditedDate = o.LastEditedDate,
                    OrderStatus = o.OrderStatus,
                    chatUrl = o.Chaturl,
                    TotalPrice = o.TotalPrice,
                    DeliveryCost = o.DeliveryPrice,
                })
                .ToListAsync();

            var allOrderIds = orders.Select(o => o.Id).ToList();

            if (showFailureReasonColumn && allOrderIds.Any())
            {
                var failureReasons = await _context.OrderStatusHistories
                    .Where(oh => oh.OrderId.HasValue && allOrderIds.Contains(oh.OrderId.Value) && oh.Reason != null)
                    .GroupBy(oh => oh.OrderId)
                    .Select(g => new { OrderId = g.Key, Reason = g.OrderByDescending(oh => oh.Id).First().Reason })
                    .ToDictionaryAsync(x => x.OrderId.Value, x => x.Reason);

                foreach (var order in orders)
                {
                    // صفحة تحديث فشل التسليم تعرض قيد التوصيل، لذلك السبب القديم لا يظهر.
                    if (order.OrderStatus == OrderStatusEnum.قيد_التوصيل)
                    {
                        order.FailureReasonDisplay = null;
                        continue;
                    }

                    order.FailureReasonDisplay = failureReasons.GetValueOrDefault(order.Id);
                }
            }

            var paginationViewModel = new PaginationViewModel<OrderViewModel>
            {
                Items = orders,
                CurrentPage = page,
                PageSize = pageSize.Value,
                TotalItems = totalItems
            };

            var viewModel = new HomeViewModel
            {
                PaginationViewModel = paginationViewModel,
                OrderStatuses = _dataCacheService.GetCachedOrderStatuses(),
                OrderStatusesForDeliveryCompanyAndRepresentative = _dataCacheService.GetCachedOrderStatusesForDeliveryCompanyAndRepresentative(),
                Countries = _dataCacheService.GetCachedCountries(),
                OrderStatusIconUrls = _dataCacheService.GetCachedOrderStatusIconUrls(),
                CountryImageUrls = _dataCacheService.GetCachedCountryImageUrls(),
                SocialMediaIconUrls = _dataCacheService.GetCachedSocialMediaIconUrls(),
                CurrencySymbols = _dataCacheService.GetCachedCurrencySymbols(),
                TotalOrderPriceDollar = _decimalFormattingService.DecimalFormat(0),
                TotalOrderPriceTRY = _decimalFormattingService.DecimalFormat(0),
                ShowFailureReasonColumn = showFailureReasonColumn
            };

            return View(viewName, viewModel);

        }

        private IQueryable<OrderStatusUpdateSelection> GetActiveStatusUpdateSelectionsQuery(DateTime now)
        {
            return _context.OrderStatusUpdateSelections
                .Where(x => x.IsActive && (!x.ExpiresAt.HasValue || x.ExpiresAt.Value > now));
        }

        private static bool IsStatusUpdateSelectionTarget(OrderStatusEnum status)
        {
            return status == OrderStatusEnum.فشل_التسليم
                || status == OrderStatusEnum.تم_التسليم
                || status == OrderStatusEnum.الطلبات_المعلقة;
        }

        private async Task DeactivateOrderStatusUpdateSelectionsAsync(IEnumerable<int> orderIds, bool saveNow = false)
        {
            var ids = orderIds
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (!ids.Any())
            {
                return;
            }

            var activeSelections = await _context.OrderStatusUpdateSelections
                .Where(x => ids.Contains(x.OrderId) && x.IsActive)
                .ToListAsync();

            foreach (var selection in activeSelections)
            {
                selection.IsActive = false;
                selection.ExpiresAt = _timeService.GetIstanbulTimeWithOffset();
                _temporaryStatusSelectionFailureImages.TryRemove(selection.OrderId, out _);
            }

            if (saveNow)
            {
                await _context.SaveChangesAsync();
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
        public async Task<IActionResult> SaveStatusUpdateSelection(
            int orderId,
            string targetStatus,
            string? failureReason = null,
            IFormFile? failureReasonImageFile = null,
            string? failureReasonImageBase64 = null)
        {
            if (orderId <= 0)
            {
                return Json(new { success = false, message = "رقم الطلب غير صحيح" });
            }

            if (!Enum.TryParse<OrderStatusEnum>(targetStatus, out var parsedTargetStatus))
            {
                return Json(new { success = false, message = "الحالة غير صحيحة" });
            }

            if (!IsStatusUpdateSelectionTarget(parsedTargetStatus))
            {
                return Json(new { success = false, message = "هذه الحالة غير مسموح بها في صفحة التحديث" });
            }

            if (parsedTargetStatus == OrderStatusEnum.فشل_التسليم && string.IsNullOrWhiteSpace(failureReason))
            {
                return Json(new { success = false, message = "سبب الفشل مطلوب" });
            }

            var currentUserId = _userManager.GetUserId(User) ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(currentUserId)
                || !await _context.Users.AsNoTracking().AnyAsync(user => user.Id == currentUserId))
            {
                return BadRequest(new { success = false, message = "جلسة المستخدم غير صحيحة. سجلي خروج ودخول مرة أخرى ثم أعيدي الإرسال." });
            }
            var currentUserName = User.Identity?.Name ?? string.Empty;

            var orderQuery = _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == orderId && !o.IsHidden);

            if (User.IsInRole("FollowUpDepartment"))
            {
                orderQuery = orderQuery.Where(o =>
                    o.ManufacturingCompany.EmployeeManufacturingCompanies.Any(a =>
                        a.ApplicationUserId == currentUserId &&
                        a.CanSeeManufacturingCompany));
            }

            var order = await orderQuery.FirstOrDefaultAsync();

            if (order == null)
            {
                return BadRequest(new { success = false, message = "الطلب غير موجود أو ليس لديك صلاحية عليه" });
            }

            if (order.OrderStatus != OrderStatusEnum.قيد_التوصيل)
            {
                return Json(new { success = false, message = "الطلب لم يعد في حالة قيد التوصيل" });
            }

            var now = _timeService.GetIstanbulTimeWithOffset();

            var activeSelection = await _context.OrderStatusUpdateSelections
                .FirstOrDefaultAsync(x => x.OrderId == orderId && x.IsActive);

            if (activeSelection != null
                && !string.Equals(activeSelection.SelectedByUserId, currentUserId, StringComparison.Ordinal))
            {
                return Json(new
                {
                    success = false,
                    message = "هذا الطلب محدد حاليًا بواسطة موظف آخر ولا يمكن تعديله حتى يتم إلغاء اختياره.",
                    orderId,
                    selectedByUserId = activeSelection.SelectedByUserId,
                    selectedByName = activeSelection.SelectedByName,
                    targetStatus = activeSelection.TargetStatus.ToString(),
                    failureReason = activeSelection.FailureReason,
                    failureImagePath = _temporaryStatusSelectionFailureImages.TryGetValue(orderId, out var lockedFailureImagePath) ? lockedFailureImagePath : null
                });
            }

            string? failureImagePath = activeSelection != null && _temporaryStatusSelectionFailureImages.TryGetValue(orderId, out var existingFailureImagePath)
                ? existingFailureImagePath
                : null;

            if (parsedTargetStatus == OrderStatusEnum.فشل_التسليم)
            {
                var uploadedFailureImagePath = await SaveFailureReasonImageAsync(
                    orderId,
                    failureReasonImageFile,
                    failureReasonImageBase64,
                    allowStandardNames: true);

                if (!string.IsNullOrWhiteSpace(uploadedFailureImagePath))
                {
                    failureImagePath = uploadedFailureImagePath;
                }

                if (string.IsNullOrWhiteSpace(failureImagePath))
                {
                    return Json(new
                    {
                        success = false,
                        message = "صورة سبب الفشل إجبارية قبل حفظ الاختيار المؤقت."
                    });
                }
            }

            if (activeSelection == null)
            {
                activeSelection = new OrderStatusUpdateSelection
                {
                    OrderId = orderId,
                    IsActive = true
                };

                _context.OrderStatusUpdateSelections.Add(activeSelection);
            }

            activeSelection.TargetStatus = parsedTargetStatus;
            activeSelection.FailureReason = parsedTargetStatus == OrderStatusEnum.فشل_التسليم
                ? failureReason?.Trim()
                : null;
            if (parsedTargetStatus == OrderStatusEnum.فشل_التسليم && !string.IsNullOrWhiteSpace(failureImagePath))
            {
                _temporaryStatusSelectionFailureImages[orderId] = failureImagePath;
            }
            else
            {
                _temporaryStatusSelectionFailureImages.TryRemove(orderId, out _);
            }
            activeSelection.SelectedByUserId = currentUserId;
            activeSelection.SelectedByName = currentUserName;
            activeSelection.SelectedAt = now;
            activeSelection.ExpiresAt = null;

            await _context.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("OrderStatusUpdateSelectionChanged", new
            {
                orderId,
                targetStatus = parsedTargetStatus.ToString(),
                failureReason = activeSelection.FailureReason,
                failureImagePath = failureImagePath,
                selectedByUserId = activeSelection.SelectedByUserId,
                selectedByName = activeSelection.SelectedByName,
                selectedAt = activeSelection.SelectedAt,
                isActive = true
            });

            return Json(new
            {
                success = true,
                orderId,
                targetStatus = parsedTargetStatus.ToString(),
                failureReason = activeSelection.FailureReason,
                failureImagePath = failureImagePath,
                selectedByUserId = activeSelection.SelectedByUserId,
                selectedByName = activeSelection.SelectedByName,
                isMine = true
            });
        }

        private async Task<List<int>> DeactivateCurrentUserStatusUpdateSelectionsAsync(IEnumerable<int>? orderIds = null, bool saveNow = true)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return new List<int>();
            }

            var ids = orderIds?
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            var query = _context.OrderStatusUpdateSelections
                .Where(x => x.IsActive && x.SelectedByUserId == currentUserId);

            if (ids != null && ids.Any())
            {
                query = query.Where(x => ids.Contains(x.OrderId));
            }

            var activeSelections = await query.ToListAsync();

            if (!activeSelections.Any())
            {
                return new List<int>();
            }

            var now = _timeService.GetIstanbulTimeWithOffset();
            var clearedOrderIds = activeSelections
                .Select(x => x.OrderId)
                .Distinct()
                .ToList();

            foreach (var selection in activeSelections)
            {
                selection.IsActive = false;
                selection.ExpiresAt = now;
                _temporaryStatusSelectionFailureImages.TryRemove(selection.OrderId, out _);
            }

            if (saveNow)
            {
                await _context.SaveChangesAsync();
            }

            foreach (var clearedOrderId in clearedOrderIds)
            {
                await _hubContext.Clients.All.SendAsync("OrderStatusUpdateSelectionChanged", new
                {
                    orderId = clearedOrderId,
                    isActive = false
                });
            }

            return clearedOrderIds;
        }

        private static List<int> ParseStatusUpdateSelectionIds(string? orderIds)
        {
            if (string.IsNullOrWhiteSpace(orderIds))
            {
                return new List<int>();
            }

            return orderIds
                .Split(new[] { ',', ';', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => int.TryParse(value.Trim(), out var orderId) ? orderId : 0)
                .Where(orderId => orderId > 0)
                .Distinct()
                .ToList();
        }

        [HttpPost]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
        public async Task<IActionResult> ClearMyStatusUpdateSelections([FromForm] string? orderIds = null)
        {
            var ids = ParseStatusUpdateSelectionIds(orderIds);
            var clearedIds = await DeactivateCurrentUserStatusUpdateSelectionsAsync(ids.Any() ? ids : null, saveNow: true);

            return Json(new
            {
                success = true,
                clearedOrderIds = clearedIds
            });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
        public async Task<IActionResult> ClearStatusUpdateSelection(int orderId)
        {
            if (orderId <= 0)
            {
                return Json(new { success = false, message = "رقم الطلب غير صحيح" });
            }

            var activeSelection = await _context.OrderStatusUpdateSelections
                .FirstOrDefaultAsync(x => x.OrderId == orderId && x.IsActive);

            if (activeSelection != null)
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

                if (!string.Equals(activeSelection.SelectedByUserId, currentUserId, StringComparison.Ordinal))
                {
                    return Json(new
                    {
                        success = false,
                        message = "هذا الطلب محدد بواسطة موظف آخر ولا يمكنك إلغاء اختياره.",
                        orderId,
                        selectedByUserId = activeSelection.SelectedByUserId,
                        selectedByName = activeSelection.SelectedByName
                    });
                }

                activeSelection.IsActive = false;
                activeSelection.ExpiresAt = _timeService.GetIstanbulTimeWithOffset();
                _temporaryStatusSelectionFailureImages.TryRemove(orderId, out _);

                await _context.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("OrderStatusUpdateSelectionChanged", new
                {
                    orderId,
                    selectedByUserId = activeSelection.SelectedByUserId,
                    selectedByName = activeSelection.SelectedByName,
                    isActive = false
                });
            }

            return Json(new { success = true, orderId });
        }

        [HttpGet]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
        public async Task<IActionResult> GetStatusUpdateSelections()
        {
            var now = _timeService.GetIstanbulTimeWithOffset();
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            var activeSelections = await GetActiveStatusUpdateSelectionsQuery(now)
                .AsNoTracking()
                .Select(x => new
                {
                    x.OrderId,
                    TargetStatus = x.TargetStatus.ToString(),
                    x.FailureReason,
                    x.SelectedByUserId,
                    x.SelectedByName,
                    x.SelectedAt
                })
                .ToListAsync();

            var selections = activeSelections
                .Select(x => new
                {
                    x.OrderId,
                    x.TargetStatus,
                    x.FailureReason,
                    FailureImagePath = _temporaryStatusSelectionFailureImages.TryGetValue(x.OrderId, out var temporaryFailureImagePath)
                        ? temporaryFailureImagePath
                        : null,
                    x.SelectedByUserId,
                    x.SelectedByName,
                    x.SelectedAt,
                    IsMine = x.SelectedByUserId == currentUserId
                })
                .ToList();

            return Json(new { success = true, selections, currentUserId });
        }


        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public async Task<IActionResult> HiddenOrders(
                int page = 1,
                int? pageSize = null,
                Common.Countries? countryId = null,
                string? cityId = null,
                int? storeId = null,
                int? deliverycompanyId = null,
                OrderStatusEnum? orderstatusId = null,
                string? search = null,
                int? deliveryrepresentativeId = null,
                bool? isBonus = null,
                string? employeeId = null


            )

        {
            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // query
            IQueryable<Order> query = _context.Orders.Where(a => a.IsHidden).AsNoTracking();

            // Apply filters using the ApplyFilters method
            query = _queryFilteringService.ApplyFilters(query, countryId, orderstatusId, null, storeId, deliverycompanyId, deliveryrepresentativeId, null,
                                 null, null, cityId, search, employeeId, null, null, isBonus, null, null, null, null);

            // Apply pagination before transforming the data
            var paginatedQuery = query
            .OrderByDescending(o => o.LastEditedDate)
            .Skip((page - 1) * (pageSize ?? 10))
            .Take(pageSize ?? 10);


            var orders = paginatedQuery
                 .Select(o => new OrderViewModel
                 {
                     Id = o.Id,
                     TelephoneNumber = o.TelephoneNumber,
                     FixedOrderDate = o.FixedOrderDate,
                     CreatedDate = o.CreatedDate,
                     Country = o.Country,
                     CustomerName = o.CustomerName,
                     State = o.State,
                     IsPaid = o.IsPaid,
                     OrderSource = o.OrderSource,
                     SourceName = o.SourceName,
                     Gender = o.Gender,
                     ManufacturingCompany = o.ManufacturingCompany == null ? null : new ManufacturingCompanyViewModel
                     {
                         Id = o.ManufacturingCompany.Id,
                         Name = o.ManufacturingCompany.Name,
                         Logo = o.ManufacturingCompany.ImageUrl,
                     },
                     DeliveryCompany = o.DeliveryCompany == null ? null : new DeliveryCompanyViewModel
                     {
                         Id = o.DeliveryCompany.Id,
                         Name = o.DeliveryCompany.DisplayName,
                         Logo = o.DeliveryCompany.ImageUrl,
                     },
                     Employee = _context.Employees
                .Where(e => e.ApplicationUserId == o.ApplicationUserId)
                .Select(e => new GetDataListViewModel
                {
                    Name = e.DisplayName,
                    LogoUrl = e.ImageUrl
                })
                .FirstOrDefault() ?? new GetDataListViewModel { Name = "null", LogoUrl = "null" },

                     LastEditedDate = o.LastEditedDate,
                     OrderStatus = o.OrderStatus,
                     TotalPrice = o.TotalPrice,
                     DeliveryCost = o.DeliveryPrice,
                 })
                  .ToList();

            var totalItems = await query.CountAsync(); // Asynchronous operation to count total items


            // Create a PaginationViewModel instance and populate it with data
            var paginationViewModel = new PaginationViewModel<OrderViewModel>
            {
                Items = orders,
                CurrentPage = page,
                PageSize = pageSize ?? 10,
                TotalItems = totalItems
            };

            // cached items
            var countryImageUrls = _dataCacheService.GetCachedCountryImageUrls();

            var socialMediaIconUrls = _dataCacheService.GetCachedSocialMediaIconUrls();

            var orderStatusIconUrls = _dataCacheService.GetCachedOrderStatusIconUrls();

            var currencySymbols = _dataCacheService.GetCachedCurrencySymbols();

            var countryinfos = _dataCacheService.GetCachedCountryInfos();

            var orderStatuses = _dataCacheService.GetCachedOrderStatuses();

            var orderStatusesForDeliveryCompanyAndRepresentative = _dataCacheService.GetCachedOrderStatusesForDeliveryCompanyAndRepresentative();

            var countries = _dataCacheService.GetCachedCountries();

            // Create an instance of the HomeViewModel and populate it
            var viewModel = new HomeViewModel
            {
                PaginationViewModel = paginationViewModel,
                OrderStatuses = orderStatuses,
                OrderStatusesForDeliveryCompanyAndRepresentative = orderStatusesForDeliveryCompanyAndRepresentative,
                Countries = countries,
                OrderStatusIconUrls = orderStatusIconUrls,
                CountryImageUrls = countryImageUrls,
                SocialMediaIconUrls = socialMediaIconUrls,
                CurrencySymbols = currencySymbols,
            };

            return View(viewModel); // Pass the viewModel to the view
        }


        // POST: Order/Create
        [HttpPost]
        [PreventDuplicationRequest(10)]
        [Authorize(Roles = "Admin,CallCenter,FollowUpDepartment,ExecutiveDirector")]
        public async Task<IActionResult> Create(OrderViewModel viewModel)
        {
            try
            {
                // Chat URL is required for every source except WhatsApp.
                if (viewModel.OrderSource != OrderSourceEnum.واتساب && string.IsNullOrWhiteSpace(viewModel.chatUrl))
                {
                    return new JsonResult(new { message = "حقل رابط المحادثة مطلوب." }) { StatusCode = 400 };
                }

                // Order screenshot is mandatory on create. Edit has its own flow that
                // keeps the previously saved photo, so the rule lives here only.
                if (viewModel.PhotoFile == null || viewModel.PhotoFile.Length == 0)
                {
                    return new JsonResult(new { message = "صورة الطلب مطلوبة." }) { StatusCode = 400 };
                }

                // Payment receipt is mandatory on create only when the order is marked as paid
                // (تم التحويل حوالة بنكية). Otherwise it is optional.
                if (viewModel.IsPaid && (viewModel.PaymentReceiptFile == null || viewModel.PaymentReceiptFile.Length == 0))
                {
                    return new JsonResult(new { message = "صورة إيصال الحوالة بنكية مطلوب عند تحديد حالة الدفع كمدفوع." }) { StatusCode = 400 };
                }

                viewModel.TelephoneNumber = NormalizePhone(viewModel.TelephoneNumber);
                if (viewModel.SecondTelephoneNumber != null)
                    viewModel.SecondTelephoneNumber = NormalizePhone(viewModel.SecondTelephoneNumber);

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var currentDate = _timeService.GetIstanbulTimeWithOffset();
                var createdDate = viewModel.CreatedDate;
                var hoursDifference = (createdDate - currentDate).TotalHours;

                // Check if the created date is in the future and not today
                if (hoursDifference > 48)
                {
                    viewModel.OrderStatus = OrderStatusEnum.الطلبات_المؤجلة;
                }
                else if (hoursDifference <= 48 && createdDate > currentDate)
                {
                    viewModel.OrderStatus = OrderStatusEnum.طلب_جديد;
                }


                // Reject any prior order with the same phone + store, unless that order is already
                // closed successfully (Delivered / Balance Updated / Paid) — those shouldn't block repeat customers.
                var recentOrder = await _context.Orders
                    .FirstOrDefaultAsync(o => o.TelephoneNumber == viewModel.TelephoneNumber
                                && o.ManufacturingCompanyId == viewModel.ManufacturingCompanyId
                                && o.OrderStatus != OrderStatusEnum.تم_التسليم
                                && o.OrderStatus != OrderStatusEnum.تم_تحديث_الرصيد
                                && o.OrderStatus != OrderStatusEnum.تم_الدفع);

                if (recentOrder != null)
                {
                    var duplicateOrderMessage = $"الطلب مكرر رقم الطلب {recentOrder.Id}";
                    Console.WriteLine(duplicateOrderMessage);
                    return new JsonResult(new { message = duplicateOrderMessage }) { StatusCode = 400 };
                }

                /*
                    سياسة التكرار المطلوبة:
                    نفس رقم الهاتف + نفس الدولة + متجر مختلف خلال 3 أيام لا يمنع إنشاء الطلب.
                    يتم المقارنة بين الطلب الجديد وكل الطلبات المشابهة داخل نفس نافذة الـ 3 أيام.
                    الطلب الأعلى سعرًا هو الذي يظل كما هو، وكل الأقل سعرًا يتحول إلى الطلبات المؤجلة.
                    لو السعر متساوي، الطلب الأقدم يفضل كما هو والجديد يتأجل.
                    مهم: الحالة تُحفظ داخل قاعدة البيانات وليست مجرد عرض في الواجهة.
                */
                var duplicateWindowStart = createdDate.AddDays(-3);
                var duplicateWindowEnd = createdDate.AddDays(3);
                var newStoreId = viewModel.ManufacturingCompanyId;
                var forceNewOrderDelayedBecauseOfSimilarOrder = false;
                var existingSimilarOrdersToDelayBecauseOfSimilarOrder = new List<Order>();

                var similarOrdersWithinThreeDays = await _context.Orders
                    .AsTracking()
                    .Where(o => o.TelephoneNumber == viewModel.TelephoneNumber
                        && o.Country == viewModel.Country
                        && o.ManufacturingCompanyId != newStoreId
                        && ((o.InstantAddedDate.HasValue ? o.InstantAddedDate.Value : o.CreatedDate) >= duplicateWindowStart)
                        && ((o.InstantAddedDate.HasValue ? o.InstantAddedDate.Value : o.CreatedDate) < duplicateWindowEnd)
                        && o.OrderStatus != OrderStatusEnum.تم_التسليم
                        && o.OrderStatus != OrderStatusEnum.تم_تحديث_الرصيد
                        && o.OrderStatus != OrderStatusEnum.تم_الدفع)
                    .ToListAsync();

                if (similarOrdersWithinThreeDays.Any())
                {
                    var bestExistingOrder = similarOrdersWithinThreeDays
                        .OrderByDescending(o => o.TotalPrice)
                        .ThenBy(o => o.InstantAddedDate.HasValue ? o.InstantAddedDate.Value : o.CreatedDate)
                        .ThenBy(o => o.Id)
                        .FirstOrDefault();

                    var newOrderIsHighestPrice = bestExistingOrder == null || viewModel.TotalPrice > bestExistingOrder.TotalPrice;

                    if (!newOrderIsHighestPrice)
                    {
                        // القديم أعلى أو مساوي، لذلك الجديد هو الذي يتأجل.
                        // في حالة التساوي نحافظ على القديم ونؤجل الجديد.
                        forceNewOrderDelayedBecauseOfSimilarOrder = true;
                        viewModel.OrderStatus = OrderStatusEnum.الطلبات_المؤجلة;
                    }

                    foreach (var existingSimilarOrder in similarOrdersWithinThreeDays)
                    {
                        var shouldKeepThisExistingOrder = !newOrderIsHighestPrice
                            && bestExistingOrder != null
                            && existingSimilarOrder.Id == bestExistingOrder.Id;

                        if (shouldKeepThisExistingOrder)
                        {
                            continue;
                        }

                        // لو الجديد هو الأعلى، كل الطلبات القديمة المشابهة الأقل منه تتأجل.
                        // ولو فيه طلب قديم أعلى، أي طلبات قديمة أقل منه تتأجل أيضًا.
                        if (existingSimilarOrder.OrderStatus != OrderStatusEnum.الطلبات_المؤجلة)
                        {
                            existingSimilarOrdersToDelayBecauseOfSimilarOrder.Add(existingSimilarOrder);
                        }
                    }

                    foreach (var existingOrderToDelay in existingSimilarOrdersToDelayBecauseOfSimilarOrder
                        .GroupBy(o => o.Id)
                        .Select(g => g.First()))
                    {
                        if (_context.Entry(existingOrderToDelay).State == EntityState.Detached)
                        {
                            _context.Orders.Attach(existingOrderToDelay);
                        }

                        existingOrderToDelay.OrderStatus = OrderStatusEnum.الطلبات_المؤجلة;
                        existingOrderToDelay.LastEditedDate = currentDate;

                        _context.Entry(existingOrderToDelay).Property(o => o.OrderStatus).IsModified = true;
                        _context.Entry(existingOrderToDelay).Property(o => o.LastEditedDate).IsModified = true;

                        _context.OrderStatusHistories.Add(new OrderStatusHistory
                        {
                            OrderId = existingOrderToDelay.Id,
                            Status = OrderStatusEnum.الطلبات_المؤجلة,
                            CreatedAt = currentDate,
                            ApplicationUserId = userId
                        });
                    }
                }

                // Anti-fraud: block phone numbers smuggled into free-text fields (and the secondary-phone field) to bypass
                // the duplicate-phone guard above. The primary guard only matches Order.TelephoneNumber == submitted primary,
                // so a fraudster can hide the real customer phone in Notes/Address/etc., or in SecondTelephoneNumber, and
                // still create a duplicate. We extract all 7+ digit runs from those fields, suffix-match the last 7 digits
                // against active orders' primary AND secondary phones in the same manufacturing company, and also detect
                // the same payload's own primary phone echoed inside any of these fields.
                var fraudFields = new (string Label, string FieldName, string? Value)[]
                {
                    ("ملاحظات", nameof(viewModel.Notes), viewModel.Notes),
                    ("اسم المصدر", nameof(viewModel.SourceName), viewModel.SourceName),
                    ("اسم العميل", nameof(viewModel.CustomerName), viewModel.CustomerName),
                    ("العنوان", nameof(viewModel.Address), viewModel.Address),
                    ("رابط المحادثة", nameof(viewModel.chatUrl), viewModel.chatUrl),
                    ("رقم الهاتف الثاني", nameof(viewModel.SecondTelephoneNumber), viewModel.SecondTelephoneNumber),
                };

                var submittedPrimarySuffix = SuffixOrNull(viewModel.TelephoneNumber, 7);

                var extractedDigitRuns = new List<(string Label, string FieldName, string Suffix7)>();
                foreach (var field in fraudFields)
                {
                    foreach (var run in ExtractDigitRuns(field.Value, minLength: 7))
                    {
                        extractedDigitRuns.Add((field.Label, field.FieldName, run[^7..]));
                    }
                }

                if (extractedDigitRuns.Count > 0)
                {
                    // 1) Self-match: the submitted primary phone appears inside any of the scanned fields (incl. secondary phone).
                    foreach (var run in extractedDigitRuns)
                    {
                        if (submittedPrimarySuffix != null && run.Suffix7 == submittedPrimarySuffix)
                        {
                            await LogFraudAttemptAsync(viewModel, run.FieldName, run.Suffix7, existingOrderId: null, userId, currentDate);
                            var selfMessage = $"تم اكتشاف رقم الهاتف الأساسي مكرر داخل حقل {run.Label}. لا يمكن إنشاء الطلب.";
                            Console.WriteLine(selfMessage);
                            return new JsonResult(new { message = selfMessage }) { StatusCode = 400 };
                        }
                    }

                    // 2) Cross-order match: any scanned field contains a phone-suffix of an active order in the same store.
                    var activeOrdersForStore = await _context.Orders
                        .Where(o => o.ManufacturingCompanyId == viewModel.ManufacturingCompanyId
                                    && o.OrderStatus != OrderStatusEnum.تم_التسليم
                                    && o.OrderStatus != OrderStatusEnum.تم_تحديث_الرصيد
                                    && o.OrderStatus != OrderStatusEnum.تم_الدفع)
                        .Select(o => new { o.Id, o.TelephoneNumber, o.SecondTelephoneNumber })
                        .ToListAsync();

                    var activePhoneIndex = new Dictionary<string, int>();
                    foreach (var o in activeOrdersForStore)
                    {
                        var primary = SuffixOrNull(o.TelephoneNumber, 7);
                        if (primary != null) activePhoneIndex[primary] = o.Id;
                        var secondary = SuffixOrNull(o.SecondTelephoneNumber, 7);
                        if (secondary != null && !activePhoneIndex.ContainsKey(secondary)) activePhoneIndex[secondary] = o.Id;
                    }

                    foreach (var run in extractedDigitRuns)
                    {
                        if (activePhoneIndex.TryGetValue(run.Suffix7, out var existingOrderId))
                        {
                            await LogFraudAttemptAsync(viewModel, run.FieldName, run.Suffix7, existingOrderId, userId, currentDate);
                            var fraudMessage = $"تم اكتشاف رقم هاتف يطابق طلب نشط رقم {existingOrderId} داخل حقل {run.Label}. لا يمكن إنشاء الطلب.";
                            Console.WriteLine(fraudMessage);
                            return new JsonResult(new { message = fraudMessage }) { StatusCode = 400 };
                        }
                    }
                }

                var countryMinPrice = await _context.CountryMinimumPrices
                    .FirstOrDefaultAsync(cmp => cmp.Country == viewModel.Country
                        && cmp.ManufacturingCompanyId == viewModel.ManufacturingCompanyId);

                if (countryMinPrice != null)
                {
                    var effectiveTotalPrice = viewModel.TotalPrice;

                    if (effectiveTotalPrice < countryMinPrice.MinimumPriceForOffers)
                    {
                        var minPriceMessage = $"لا يمكننك تنزيل طلب بأقل من الحد الأدنى {countryMinPrice.MinimumPriceForOffers}";
                        Console.WriteLine(minPriceMessage);
                        return new JsonResult(new { message = minPriceMessage }) { StatusCode = 400 };
                    }

                    if (effectiveTotalPrice <= countryMinPrice.MaximumPriceForOffers)
                    {
                        viewModel.IsDiscount = true;
                    }
                }

                if (viewModel.SelectedWarehouses == null || viewModel.SelectedWarehouses.Count == 0)
                {
                    var warehouseErrorMessage = "حدث خطأ: لا توجد مستودعات محددة.";
                    Console.WriteLine(warehouseErrorMessage);
                    return new JsonResult(new { message = warehouseErrorMessage }) { StatusCode = 400 };
                }

                foreach (var warehouseAmount in viewModel.SelectedWarehouses)
                {
                    if (warehouseAmount.WarehouseId <= 0 || warehouseAmount.Amount <= 0)
                    {
                        var invalidWarehouseMessage = "حدث خطأ: توجد مستودعات غير صالحة.";
                        Console.WriteLine(invalidWarehouseMessage);
                        return new JsonResult(new { message = invalidWarehouseMessage }) { StatusCode = 400 };
                    }
                }

                var productMinimumValidation = await ValidateOrderProductMinimumSellingPriceAsync(viewModel);
                if (!productMinimumValidation.IsValid)
                {
                    Console.WriteLine(productMinimumValidation.Message);
                    return new JsonResult(new { message = productMinimumValidation.Message }) { StatusCode = 400 };
                }

                // Validate that at least one warehouse belongs to the store's main warehouse
                // Admin, FollowUpDepartment, and ExecutiveDirector are exempt from this restriction
                if (viewModel.ManufacturingCompanyId.HasValue
                    && !User.IsInRole("Admin")
                    && !User.IsInRole("FollowUpDepartment")
                    && !User.IsInRole("ExecutiveDirector"))
                {
                    var store = await _context.ManufacturingCompanies
                        .FirstOrDefaultAsync(m => m.Id == viewModel.ManufacturingCompanyId.Value);
                    if (store?.MainWarehouseId != null)
                    {
                        var warehouseIds = (viewModel.SelectedWarehouses ?? new List<WarehouseAmountViewModel>())
                            .Select(w => w.WarehouseId).ToList();
                        var hasMatchingWarehouse = warehouseIds.Count > 0 && await _context.Warehouses
                            .AnyAsync(w => warehouseIds.Contains(w.Id) && w.MainWarehouseId == store.MainWarehouseId);
                        if (!hasMatchingWarehouse)
                        {
                            return new JsonResult(new { message = "يجب إضافة منتج واحد على الأقل من المستودع الرئيسي المرتبط بالمتجر." }) { StatusCode = 400 };
                        }
                    }
                }

                // Validate that the selected campaign belongs to the selected store
                if (viewModel.CampaignId.HasValue)
                {
                    var campaign = await _context.Campaigns
                        .FirstOrDefaultAsync(c => c.Id == viewModel.CampaignId.Value);
                    if (campaign?.ManufacturingCompanyId != null
                        && campaign.ManufacturingCompanyId != viewModel.ManufacturingCompanyId)
                    {
                        return new JsonResult(new { message = "الحملة المختارة غير مرتبطة بالمتجر المختار." }) { StatusCode = 400 };
                    }
                }

                // Set the DeliveryPrice based on the DeliveryCompany, Country, and City
                var deliveryCompanyPrice = await _context.DeliveryCompanyPrices
                    .FirstOrDefaultAsync(p => p.DeliveryCompanyId == viewModel.DeliveryCompanyId
                                              && p.Country == viewModel.Country
                                              && p.City == viewModel.State); // Assuming `State` represents `City` in the model



                //if (deliveryCompanyPrice == null)
                //{
                //    // Handle case where no matching price is found
                //    var priceErrorMessage = "لم يتم العثور على سعر مناسب للشركة والبلد والمدينة المحددة.";
                //    Console.WriteLine(priceErrorMessage);
                //    return new JsonResult(new { message = priceErrorMessage }) { StatusCode = 400 };
                //}


                // Determine if the order qualifies for a bonus
                var bonusConfigurations = await _context.OrderBonusConfigurations.ToListAsync();
                bool isBonus = bonusConfigurations.Any(bc => bc.OrderThreshold <= viewModel.TotalPrice && bc.Country == viewModel.Country);

                string? photoUrl = null;
                if (viewModel.PhotoFile != null && viewModel.PhotoFile.Length > 0)
                {
                    var uploaded = await _fileUploadService.UploadFileAsync(viewModel.PhotoFile, "images/orders");
                    photoUrl = uploaded != null ? "/" + uploaded.TrimStart('/') : null;
                }

                string? paymentReceiptUrl = null;
                if (viewModel.PaymentReceiptFile != null && viewModel.PaymentReceiptFile.Length > 0)
                {
                    var uploadedReceipt = await _fileUploadService.UploadFileAsync(viewModel.PaymentReceiptFile, "images/receipts");
                    paymentReceiptUrl = uploadedReceipt != null ? "/" + uploadedReceipt.TrimStart('/') : null;
                }

                // Create an Order instance and map properties from the view model
                var order = new Order
                {
                    Country = viewModel.Country,
                    State = viewModel.State,
                    OrderSource = viewModel.OrderSource,
                    SourceName = viewModel.SourceName,
                    ManufacturingCompanyId = viewModel.ManufacturingCompanyId,
                    DeliveryCompanyId = viewModel.DeliveryCompanyId,
                    TelephoneNumber = viewModel.TelephoneNumber,
                    SecondTelephoneNumber = viewModel.SecondTelephoneNumber,
                    CustomerName = viewModel.CustomerName,
                    Notes = viewModel.Notes,
                    Address = viewModel.Address,
                    CreatedDate = viewModel.CreatedDate,
                    LastEditedDate = _timeService.GetIstanbulTimeWithOffset(),
                    // مهم: لو الطلب الجديد اتأجل بسبب طلب مشابه، نحفظ الحالة صريحة في الداتا بيز
                    // بدل الاعتماد على أي حالة جاية من الفورم أو الواجهة.
                    OrderStatus = forceNewOrderDelayedBecauseOfSimilarOrder
                        ? OrderStatusEnum.الطلبات_المؤجلة
                        : viewModel.OrderStatus,
                    TotalPrice = viewModel.TotalPrice,
                    ApplicationUserId = userId,
                    InstantAddedDate = _timeService.GetIstanbulTimeWithOffset(),
                    Gender = (viewModel.ManufacturingCompanyId == 24 || viewModel.ManufacturingCompanyId == 25) ? true : viewModel.Gender,
                    IsDiscount = viewModel.IsDiscount,
                    IsPaid = viewModel.IsPaid,
                    FromComments = viewModel.FromComments,
                    DeliveryPrice = deliveryCompanyPrice?.Price ?? 0,// Set DeliveryPrice here
                    IsBonus = isBonus, // Set IsBonus based on qualification
                    Chaturl = viewModel.chatUrl ?? string.Empty,
                    CampaignId = viewModel.CampaignId,
                    CreationDurationSeconds = viewModel.CreationDurationSeconds,
                    PhotoUrl = photoUrl,
                    PaymentReceiptUrl = paymentReceiptUrl,

                };

                //if (viewModel.Country == Common.Countries.العراق)
                //{
                //    var externalOrderData = new OrderPostApi
                //    {
                //        Country = (int)viewModel.Country,
                //        State = viewModel.State,
                //        OrderSource = (int)viewModel.OrderSource,
                //        SourceName = viewModel.SourceName,
                //        TelephoneNumber = viewModel.TelephoneNumber,
                //        SecondTelephoneNumber = viewModel.SecondTelephoneNumber ?? "",
                //        CustomerName = viewModel.CustomerName,
                //        Notes = viewModel.Notes ?? "",
                //        Address = viewModel.Address,
                //        CreatedDate = viewModel.CreatedDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                //        TotalPrice = viewModel.TotalPrice,
                //    };

                //    switch (viewModel.ManufacturingCompanyId)
                //    {
                //        case 1:
                //            externalOrderData.StoreId = 40;
                //            break;
                //        case 2:
                //            externalOrderData.StoreId = 38;
                //            break;
                //        case 4:
                //            externalOrderData.StoreId = 52;
                //            break;
                //        case 5:
                //            externalOrderData.StoreId = 54;
                //            break;
                //        case 7:
                //            externalOrderData.StoreId = 80;
                //            break;
                //        default:
                //            break;
                //    }

                //    if (viewModel.OrderSource == OrderSourceEnum.واتساب)
                //    {
                //        externalOrderData.SourceName = "واتساب";
                //    }

                //    Console.WriteLine("Sending external order data: " + JsonConvert.SerializeObject(externalOrderData));

                //    try
                //    {
                //        var apiResponse = await _restApi.CreateOrderAsync(externalOrderData);
                //        if (!apiResponse.IsSuccessStatusCode)
                //        {
                //            var responseContent = await apiResponse.Content.ReadAsStringAsync();
                //            var apiErrorMessage = $"API call failed with status code: {apiResponse.StatusCode}, response: {responseContent}";

                //            Console.WriteLine("API call failed. Status code: " + apiResponse.StatusCode);
                //            Console.WriteLine("Response: " + responseContent);

                //            return new JsonResult(new { message = apiErrorMessage }) { StatusCode = 500 };
                //        }

                //        var responseString = await apiResponse.Content.ReadAsStringAsync();
                //        var apiResult = JsonConvert.DeserializeObject<OrderPostApi>(responseString);
                //        order.ExternalOrderId = apiResult.OrderId;
                //    }
                //    catch (Exception ex)
                //    {
                //        Console.WriteLine("An error occurred while calling the external API: " + ex.Message);
                //        Console.WriteLine(ex.StackTrace);
                //        return new JsonResult(new { message = "An error occurred while calling the external API." }) { StatusCode = 500 };
                //    }
                //}

                _context.Add(order);

                if (await _context.SaveChangesAsync() == 0)
                {
                    var saveErrorMessage = "خطأ في تنزيل الطلب على العراق. أرجو المحاولة لاحقا.";
                    Console.WriteLine(saveErrorMessage);
                    return new JsonResult(new { message = saveErrorMessage }) { StatusCode = 500 };
                }

                // تأكيد حفظ حالة الطلب المؤجل داخل قاعدة البيانات فعليًا قبل أي رجوع للواجهة.
                // ده يمنع إن الحالة تظهر مؤقتًا وبعد الريفريش أو من براوزر تاني ترجع طلب جديد.
                if (forceNewOrderDelayedBecauseOfSimilarOrder && order.OrderStatus != OrderStatusEnum.الطلبات_المؤجلة)
                {
                    order.OrderStatus = OrderStatusEnum.الطلبات_المؤجلة;
                    order.LastEditedDate = currentDate;
                    _context.Entry(order).Property(o => o.OrderStatus).IsModified = true;
                    _context.Entry(order).Property(o => o.LastEditedDate).IsModified = true;
                    await _context.SaveChangesAsync();
                }

                foreach (var warehouseAmount in viewModel.SelectedWarehouses)
                {
                    var warehouse = await _context.Warehouses.FindAsync(warehouseAmount.WarehouseId);
                    if (warehouse != null)
                    {
                        warehouse.Amount -= warehouseAmount.Amount;
                        _context.Update(warehouse);

                        var orderWarehouse = new OrderWarehouse
                        {
                            WarehouseId = warehouse.Id,
                            OrderId = order.Id,
                            Amount = warehouseAmount.Amount
                        };
                        _context.OrderWarehouses.Add(orderWarehouse);
                    }
                }

                await _context.SaveChangesAsync();

                var orderHistory = new OrderStatusHistory
                {
                    CreatedAt = _timeService.GetIstanbulTimeWithOffset(),
                    Status = order.OrderStatus,
                    ApplicationUserId = userId,
                    OrderId = order.Id
                };

                _context.OrderStatusHistories.Add(orderHistory);
                await _context.SaveChangesAsync();

                // Delete matching potential orders by chat URL / phone + country + store name
                {
                    var manufacturingCompany = await _context.ManufacturingCompanies
                        .FirstOrDefaultAsync(m => m.Id == order.ManufacturingCompanyId);

                    if (manufacturingCompany != null)
                    {
                        List<PotentialOrder> matchingPotentialOrders;
                        if (order.OrderSource == OrderSourceEnum.واتساب)
                        {
                            matchingPotentialOrders = await _context.PotentialOrders
                                .Where(p => p.PhoneNumber == order.TelephoneNumber
                                    && p.Country == order.Country
                                    && p.StoreName == manufacturingCompany.Name)
                                .ToListAsync();
                        }
                        else if (!string.IsNullOrWhiteSpace(order.Chaturl))
                        {
                            matchingPotentialOrders = await _context.PotentialOrders
                                .Where(p => p.ChatUrl == order.Chaturl
                                    && p.Country == order.Country
                                    && p.StoreName == manufacturingCompany.Name)
                                .ToListAsync();
                        }
                        else
                        {
                            matchingPotentialOrders = new List<PotentialOrder>();
                        }

                        if (matchingPotentialOrders.Any())
                        {
                            _context.PotentialOrders.RemoveRange(matchingPotentialOrders);
                            await _context.SaveChangesAsync();
                        }
                    }
                }

                var fetchedOrder = await _context.Orders
                   .Include(o => o.ManufacturingCompany)
                   .Include(o => o.DeliveryCompany)
                   .Include(o => o.ApplicationUser)
                   .FirstOrDefaultAsync(o => o.Id == order.Id);


                var orderDetails = new
                {
                    Order = new
                    {
                        order.Id,
                        order.TelephoneNumber,
                        order.CustomerName,
                        CreatedDate = order.CreatedDate.ToString("yyyy-MM-dd"),
                        LastEditedDate = order.LastEditedDate?.ToString("yyyy-MM-dd") ?? "Not Edited", // Handle nullable LastEditedDate
                        order.TotalPrice,
                        OrderStatusString = order.OrderStatus.ToString(),
                        OrderStatusImageUrl = Common.StatusIconUrl.ContainsKey(order.OrderStatus) ? Common.StatusIconUrl[order.OrderStatus] : "/static/default.svg",
                        CountryString = order.Country.ToString(),
                        CountryImageUrl = Common.ImageUrlByCountry.ContainsKey(order.Country) ? Common.ImageUrlByCountry[order.Country] : "/Countries/default.svg",
                        Currency = Common.CurrencyByCountry.ContainsKey(order.Country) ? Common.CurrencyByCountry[order.Country] : "N/A",
                        OrderSourceString = order.OrderSource.ToString(),
                        OrderSourceImageUrl = Common.SocialMediaIconUrl.ContainsKey(order.OrderSource) ? Common.SocialMediaIconUrl[order.OrderSource] : "/socialmediaicons/default.svg",
                        Gender = order.Gender ? "ذكر" : "أنثى",
                        State = order.State,// Handle the City (State) fallback
                        SourceName = order.SourceName, // Handle PageName fallback
                    },
                    Username = fetchedOrder.ApplicationUser.Name,
                    ManufacturingCompanyimage = fetchedOrder.ManufacturingCompany.ImageUrl,
                    ManufacturingCompanyName = fetchedOrder.ManufacturingCompany.Name,
                    Deliverycompanyname = fetchedOrder.DeliveryCompany.DisplayName,
                    Deliverycompanyimage = fetchedOrder.DeliveryCompany.ImageUrl,
                };


                var orderJson = JsonConvert.SerializeObject(orderDetails, new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });


                // Send to UsersExpectDelivery group
                await _hubContext.Clients.Group("UsersExpectDelivery").SendAsync("NotifyOrderAdded", orderJson);

                // Send to delivery company group
                var targetDeliveryCompanyId = order.DeliveryCompanyId;
                var deliveryCompanyGroup = $"deliveryCompany_{targetDeliveryCompanyId}";
                await _hubContext.Clients.Group(deliveryCompanyGroup).SendAsync("NotifyOrderAdded", orderJson);

                // Send to manufacturing company group
                var targetManufacturingCompanyId = order.ManufacturingCompanyId;
                var manufacturingCompanyGroup = $"manufacturingCompany_{targetManufacturingCompanyId}";
                await _hubContext.Clients.Group(manufacturingCompanyGroup).SendAsync("NotifyOrderAdded", orderJson);


                return Json(new { orderId = order.Id });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unexpected error: " + ex.Message);
                return new JsonResult(new { message = ex.Message }) { StatusCode = 500 };
            }
        }



        // POST: Order/Edit/5
        [HttpPost]
        [Authorize(Roles = "Admin,CallCenter,FollowUpDepartment,ExecutiveDirector")]
        public async Task<IActionResult> Edit(int id, OrderViewModel viewModel)
        {
            if (id != viewModel.Id)
            {
                return NotFound();
            }

            // Chat URL is required for every source except WhatsApp.
            if (viewModel.OrderSource != OrderSourceEnum.واتساب && string.IsNullOrWhiteSpace(viewModel.chatUrl))
            {
                return new JsonResult(new { message = "حقل رابط المحادثة مطلوب." }) { StatusCode = 400 };
            }

            // Payment receipt is mandatory when payment status is "paid". Either a freshly
            // pasted file or the previously saved receipt (round-tripped via ExistingPaymentReceiptUrl)
            // satisfies the rule.
            if (viewModel.IsPaid
                && (viewModel.PaymentReceiptFile == null || viewModel.PaymentReceiptFile.Length == 0)
                && string.IsNullOrWhiteSpace(viewModel.ExistingPaymentReceiptUrl))
            {
                return new JsonResult(new { message = "صورة إيصال الحوالة بنكية مطلوب عند تحديد حالة الدفع كمدفوع." }) { StatusCode = 400 };
            }

            // An order must always have at least one real warehouse with a positive amount.
            // Why: the GET edit endpoint injects a placeholder {WarehouseId=0, Amount=0} when an order
            // has no warehouses (HomeController). Without this check, that placeholder round-trips and
            // the order is saved with zero products, perpetuating the empty state.
            var allSubmitted = viewModel.SelectedWarehouses ?? new List<WarehouseAmountViewModel>();
            var invalidSubmitted = allSubmitted
                .Select((w, i) => new { Row = w, Index = i })
                .Where(x => !(x.Row.WarehouseId > 0 && x.Row.Amount > 0))
                .ToList();
            var validSelectedWarehouses = allSubmitted
                .Where(w => w.WarehouseId > 0 && w.Amount > 0)
                .ToList();
            if (validSelectedWarehouses.Count == 0)
            {
                var invalidDetails = invalidSubmitted.Count > 0
                    ? " (" + string.Join("، ", invalidSubmitted.Select(x =>
                        $"الصف {x.Index + 1}: {(string.IsNullOrWhiteSpace(x.Row.WarehouseName) ? "بدون اسم" : x.Row.WarehouseName)} (المستودع #{x.Row.WarehouseId}، الكمية {x.Row.Amount})")) + ")"
                    : "";
                return new JsonResult(new { message = "يجب إضافة مستودع واحد على الأقل بكمية أكبر من صفر." + invalidDetails }) { StatusCode = 400 };
            }
            var submittedWarehouseIds = validSelectedWarehouses.Select(w => w.WarehouseId).Distinct().ToList();
            var existingWarehouseIds = await _context.Warehouses
                .Where(w => submittedWarehouseIds.Contains(w.Id))
                .Select(w => w.Id)
                .ToListAsync();
            var missingIds = submittedWarehouseIds.Except(existingWarehouseIds).ToList();
            if (missingIds.Count > 0)
            {
                var missingDetails = string.Join("، ", validSelectedWarehouses
                    .Where(w => missingIds.Contains(w.WarehouseId))
                    .Select(w => $"{(string.IsNullOrWhiteSpace(w.WarehouseName) ? "بدون اسم" : w.WarehouseName)} (#{w.WarehouseId})"));
                return new JsonResult(new { message = "أحد المستودعات المحددة غير موجود: " + missingDetails }) { StatusCode = 400 };
            }
            viewModel.SelectedWarehouses = validSelectedWarehouses;

            var productMinimumValidation = await ValidateOrderProductMinimumSellingPriceAsync(viewModel);
            if (!productMinimumValidation.IsValid)
            {
                Console.WriteLine(productMinimumValidation.Message);
                return new JsonResult(new { success = false, message = productMinimumValidation.Message }) { StatusCode = 400 };
            }

            viewModel.TelephoneNumber = NormalizePhone(viewModel.TelephoneNumber);
            if (viewModel.SecondTelephoneNumber != null)
                viewModel.SecondTelephoneNumber = NormalizePhone(viewModel.SecondTelephoneNumber);

            var existingOrder = await _context.Orders
             .Include(o => o.OrderWarehouses)
                 .ThenInclude(ow => ow.Warehouse)
                     .ThenInclude(w => w.MainWarehouse)
                         .Include(o => o.ManufacturingCompany)
                         .Include(o => o.DeliveryCompany)
                         .FirstOrDefaultAsync(o => o.Id == id);



            if (existingOrder == null)
            {
                return NotFound();
            }

            // Reject any other order with the same phone + store, unless that order is already
            // closed successfully (Delivered / Balance Updated / Paid) or still Incomplete —
            // those shouldn't block repeat customers or an employee finishing an in-progress order.
            // Editing an Incomplete order itself skips the check entirely, so staff can complete its data.
            var isEditingIncompleteOrder = existingOrder.OrderStatus == OrderStatusEnum.الطلبات_الغير_مكتملة;

            var duplicateOrder = isEditingIncompleteOrder
                ? null
                : await _context.Orders
                    .FirstOrDefaultAsync(o => o.Id != id
                        && o.TelephoneNumber == viewModel.TelephoneNumber
                        && o.ManufacturingCompanyId == viewModel.ManufacturingCompanyId
                        && o.OrderStatus != OrderStatusEnum.تم_التسليم
                        && o.OrderStatus != OrderStatusEnum.تم_تحديث_الرصيد
                        && o.OrderStatus != OrderStatusEnum.تم_الدفع
                        && o.OrderStatus != OrderStatusEnum.الطلبات_الغير_مكتملة);

            if (duplicateOrder != null)
            {
                var duplicateMessage = $"بسبب إدخال الطلب سابقا برقم وصل {duplicateOrder.Id}\r\nلايمكنك تعديل هذا الطلب.";
                return new JsonResult(new { message = duplicateMessage }) { StatusCode = 400 };
            }

            // CallCenter may not edit orders in preparation, delivery, or terminal/financial states.
            if (User.IsInRole("CallCenter")
                && (existingOrder.OrderStatus == OrderStatusEnum.تم_التجهيز
                    || existingOrder.OrderStatus == OrderStatusEnum.قيد_التوصيل
                    || existingOrder.OrderStatus == OrderStatusEnum.تم_التسليم
                    || existingOrder.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد
                    || existingOrder.OrderStatus == OrderStatusEnum.تم_الدفع))
            {
                return new JsonResult(new { message = "لا يمكن لقسم الكول سنتر تعديل الطلبات في حالة تم التجهيز أو قيد التوصيل أو تم التسليم أو تم تحديث الرصيد أو تم الدفع." }) { StatusCode = 403 };
            }

            // FollowUpDepartment may edit orders in any status except these terminal/financial states.
            if (User.IsInRole("FollowUpDepartment")
                && (existingOrder.OrderStatus == OrderStatusEnum.تم_التسليم
                    || existingOrder.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد
                    || existingOrder.OrderStatus == OrderStatusEnum.تم_الدفع))
            {
                return new JsonResult(new { message = "لا يمكن لقسم المتابعة تعديل الطلبات في حالة تم التسليم أو تم تحديث الرصيد أو تم الدفع." }) { StatusCode = 403 };
            }

            // Update the ManufacturingCompanyId
            existingOrder.ManufacturingCompanyId = viewModel.ManufacturingCompanyId;



            // Explicitly reload the ManufacturingCompany after the ID has been updated
            if (existingOrder.ManufacturingCompanyId.HasValue)
            {
                existingOrder.ManufacturingCompany = await _context.ManufacturingCompanies
                    .FirstOrDefaultAsync(mc => mc.Id == existingOrder.ManufacturingCompanyId.Value);
            }

            var currentDate = _timeService.GetIstanbulTimeWithOffset();
            var createdDate = viewModel.CreatedDate;
            var totalDays = (createdDate - currentDate).TotalDays;

            // Check if the created date is more than 48 hours from now
            viewModel.OrderStatus = totalDays > 2 ? OrderStatusEnum.الطلبات_المؤجلة : OrderStatusEnum.طلب_جديد;


            if ((existingOrder.OrderStatus == OrderStatusEnum.الطلبات_المؤجلة && viewModel.OrderStatus == OrderStatusEnum.طلب_جديد)
              || viewModel.OrderStatus == OrderStatusEnum.الطلبات_المؤجلة)
            {
                existingOrder.OrderStatus = viewModel.OrderStatus;

                var orderStatusHistory = new OrderStatusHistory
                {
                    OrderId = existingOrder.Id,
                    Status = viewModel.OrderStatus,
                    CreatedAt = _timeService.GetIstanbulTimeWithOffset(),
                    ApplicationUserId = _userManager.GetUserId(User) // Assuming _userManager is available in your context
                };

                _context.OrderStatusHistories.Add(orderStatusHistory);

                var colorStyle = OrderStatusHelper.StatusColorMapping.GetValueOrDefault(viewModel.OrderStatus, "");

                // Pass the object directly to SignalR
                var orderStatusData = new
                {
                    orderStatusHistory.OrderId,
                    orderStatusHistory.Id,
                    orderStatusHistory.Status,
                    orderStatusHistory.ApplicationUserId,
                    UserName = orderStatusHistory.User?.UserName ?? "Unknown",
                    StatusPhrase = OrderStatusHelper.GetOrderStatusPhrase(viewModel.OrderStatus),
                    ColorStyle = colorStyle
                };


                // Send to UsersExpectDelivery group
                await _hubContext.Clients.Group("UsersExpectDelivery").SendAsync("OrderStatusUpdated", orderStatusData);

                // Send to delivery company group
                var targetDeliveryCompanyId = existingOrder.DeliveryCompanyId;
                var deliveryCompanyGroup = $"deliveryCompany_{targetDeliveryCompanyId}";
                await _hubContext.Clients.Group(deliveryCompanyGroup).SendAsync("OrderStatusUpdated", orderStatusData);

                // Send to manufacturing company group
                var targetManufacturingCompanyId = existingOrder.ManufacturingCompanyId;
                var manufacturingCompanyGroup = $"manufacturingCompany_{targetManufacturingCompanyId}";
                await _hubContext.Clients.Group(manufacturingCompanyGroup).SendAsync("OrderStatusUpdated", orderStatusData);


            }

            // Fetch the new delivery price based on the delivery company, country, and city
            var deliveryCompanyPrice = await _context.DeliveryCompanyPrices
                .FirstOrDefaultAsync(p => p.DeliveryCompanyId == viewModel.DeliveryCompanyId
                                          && p.Country == viewModel.Country
                                          && p.City == viewModel.State); // Assuming `State` represents `City`




            existingOrder.DeliveryPrice = deliveryCompanyPrice?.Price ?? 0; // Set to 0 if no price found


            var countryMinPrice = await _context.CountryMinimumPrices
                .FirstOrDefaultAsync(cmp => cmp.Country == viewModel.Country
                    && cmp.ManufacturingCompanyId == viewModel.ManufacturingCompanyId);

            if (countryMinPrice != null)
            {
                var effectiveTotalPrice = viewModel.TotalPrice;

                // Check if the total price is below the minimum price
                if (effectiveTotalPrice < countryMinPrice.MinimumPriceForOffers)
                {
                    var minPriceMessage = $"لا يمكننك تنزيل طلب بأقل من السعر الأدنى {countryMinPrice.MinimumPriceForOffers}";
                    return new JsonResult(new { message = minPriceMessage }) { StatusCode = 400 };
                }

                // Check if the total price is within the specified range and set FromOffers to true
                if (effectiveTotalPrice <= countryMinPrice.MaximumPriceForOffers)
                {
                    viewModel.IsDiscount = true;
                }
            }

            // Determine if the order qualifies for a bonus
            var bonusConfigurations = await _context.OrderBonusConfigurations.ToListAsync();
            existingOrder.IsBonus = bonusConfigurations.Any(bc => bc.OrderThreshold <= viewModel.TotalPrice && bc.Country == viewModel.Country);

            if (viewModel.ExternalOrderId != null)
            {
                // Map the properties from viewModel to externalOrderData
                var externalOrderData = new PostOrderToSbs
                {
                    id = viewModel.ExternalOrderId.Value, // External order ID
                    Country = (int)viewModel.Country,
                    State = viewModel.State,
                    OrderSource = (int)viewModel.OrderSource,
                    SourceName = viewModel.SourceName,
                    TelephoneNumber = viewModel.TelephoneNumber,
                    SecondTelephoneNumber = viewModel.SecondTelephoneNumber ?? "NULL", // Handle nullable string with an empty string as default
                    CustomerName = viewModel.CustomerName,
                    Notes = viewModel.Notes ?? "null", // Handle nullable string with an empty string as default
                    Address = viewModel.Address,
                    TotalPrice = viewModel.TotalPrice,
                    CreatedDate = viewModel.CreatedDate,
                };
                // Determine StoreId based on ManufacturingCompanyId
                switch (viewModel.ManufacturingCompanyId)
                {
                    case 1:
                        externalOrderData.StoreId = 40;
                        break;
                    case 2:
                        externalOrderData.StoreId = 38;
                        break;
                    case 4:
                        externalOrderData.StoreId = 49;
                        break;
                    case 5:
                        externalOrderData.StoreId = 54;
                        break;
                    case 7:
                        externalOrderData.StoreId = 80;
                        break;

                    // Add more cases as needed
                    default:
                        // Handle default case if necessary
                        break;
                }




                // Check if viewModel.OrderSource equals to ordersource enum "واتساب"
                if (viewModel.OrderSource == OrderSourceEnum.واتساب)
                {
                    externalOrderData.SourceName = "واتساب";
                }
                // Send a POST request to update the order in the external API
                var response = await _restApi.UpdateOrderAsync(externalOrderData.id, externalOrderData);
                if (!response.IsSuccessStatusCode)
                {
                    // API call failed, return an error response
                    Console.WriteLine($"API call failed with status code: {response.StatusCode}");
                    // You can also log response content or other details if needed
                    return BadRequest("External API call failed.");
                }
            }

            // get last edit number  
            var lastEditNumber = await _context.OrderEditHistories
                .Where(eh => eh.OrderId == id)
                .Select(eh => eh.EditNumber)
                .OrderByDescending(eh => eh)
                .FirstOrDefaultAsync();


            var orderHistory = new OrderEditHistory
            {
                OrderId = existingOrder.Id,
                Country = existingOrder.Country,
                State = existingOrder.State,
                OrderSource = existingOrder.OrderSource,
                SourceName = existingOrder.SourceName,
                ManufacturingCompanyId = existingOrder.ManufacturingCompanyId,
                TelephoneNumber = existingOrder.TelephoneNumber,
                DeliveryCompanyId = existingOrder.DeliveryCompanyId,
                SecondTelephoneNumber = existingOrder.SecondTelephoneNumber,
                CustomerName = existingOrder.CustomerName,
                Notes = existingOrder.Notes,
                Address = existingOrder.Address,
                CreatedDate = existingOrder.CreatedDate,
                LastEditedDate = existingOrder.LastEditedDate,
                FixedOrderDate = existingOrder.FixedOrderDate,
                InstantAddedDate = existingOrder.InstantAddedDate,
                TotalPrice = existingOrder.TotalPrice,
                //   ExternalOrderId = existingOrder.ExternalOrderId,
                ApplicationUserId = existingOrder.ApplicationUserId,
                FromComments = existingOrder.FromComments,
                Gender = existingOrder.Gender,
                EditNumber = lastEditNumber + 1,
                IsPaid = existingOrder.IsPaid,
                Editedby = existingOrder.Editedby,
                FromOffers = viewModel.IsDiscount,
                CampaignId = existingOrder.CampaignId,
                DeliveryPrice = existingOrder.DeliveryPrice,
                Chaturl = existingOrder.Chaturl,

            };

            // increase the number by one for each edit history 
            _context.OrderEditHistories.Add(orderHistory);

            existingOrder.Country = viewModel.Country;
            existingOrder.State = viewModel.State;
            existingOrder.OrderSource = viewModel.OrderSource;
            existingOrder.SourceName = viewModel.SourceName;
            existingOrder.DeliveryCompanyId = viewModel.DeliveryCompanyId;
            existingOrder.TelephoneNumber = viewModel.TelephoneNumber;
            existingOrder.SecondTelephoneNumber = viewModel.SecondTelephoneNumber;
            existingOrder.CustomerName = viewModel.CustomerName;
            existingOrder.Notes = viewModel.Notes;
            existingOrder.Address = viewModel.Address;
            existingOrder.LastEditedDate = _timeService.GetIstanbulTimeWithOffset();
            existingOrder.TotalPrice = viewModel.TotalPrice;
            existingOrder.CreatedDate = createdDate;
            existingOrder.Gender = viewModel.Gender;
            existingOrder.IsPaid = viewModel.IsPaid;
            existingOrder.Editedby = _userManager.GetUserId(User);
            existingOrder.DeliveryPrice = viewModel.DeliveryPrice;
            existingOrder.FromComments = viewModel.FromComments;
            existingOrder.Chaturl = viewModel.chatUrl ?? string.Empty;
            existingOrder.CampaignId = viewModel.CampaignId;
            // Photo: a freshly pasted file replaces the old one (also delete the old file).
            // If no new file came in, keep whatever ExistingPhotoUrl the form posted back.
            if (viewModel.PhotoFile != null && viewModel.PhotoFile.Length > 0)
            {
                var newPhoto = await _fileUploadService.UpdateFileAsync(existingOrder.PhotoUrl?.TrimStart('/'), viewModel.PhotoFile, "images/orders");
                existingOrder.PhotoUrl = newPhoto != null ? "/" + newPhoto.TrimStart('/') : null;
            }
            else
            {
                existingOrder.PhotoUrl = viewModel.ExistingPhotoUrl;
            }
            // Payment receipt: same pattern as photo. Never required on edit — the
            // create-time mandatory check is gated on IsPaid, but on edit we keep
            // whatever ExistingPaymentReceiptUrl the form posted back if no new file came in.
            if (viewModel.PaymentReceiptFile != null && viewModel.PaymentReceiptFile.Length > 0)
            {
                var newReceipt = await _fileUploadService.UpdateFileAsync(existingOrder.PaymentReceiptUrl?.TrimStart('/'), viewModel.PaymentReceiptFile, "images/receipts");
                existingOrder.PaymentReceiptUrl = newReceipt != null ? "/" + newReceipt.TrimStart('/') : null;
            }
            else
            {
                existingOrder.PaymentReceiptUrl = viewModel.ExistingPaymentReceiptUrl;
            }
            _context.Update(existingOrder); // Marks the entity and its navigation properties as modified
            await _context.SaveChangesAsync();

            // Update existingOrder warehouses
            var warehouseChanges = new Dictionary<int, int>(); // Dictionary to track warehouse changes
            var orderWarehouseEditHistories = new List<OrderWarehouseEditHistory>(); // List to store OrderWarehouseEditHistory

            // Save existing selected warehouses to history
            foreach (var orderWarehouse in existingOrder.OrderWarehouses)
            {
                var editHistory = new OrderWarehouseEditHistory
                {
                    OrderId = existingOrder.Id,
                    WarehouseId = orderWarehouse.WarehouseId,
                    Amount = orderWarehouse.Amount,
                    EditDate = _timeService.GetIstanbulTimeWithOffset(),
                    EditNumber = lastEditNumber,
                    OrderEditHistoryId = orderHistory.Id // Assuming orderHistory is already created
                };
                orderWarehouseEditHistories.Add(editHistory);
            }

            // Update warehouse changes
            foreach (var selectedWarehouse in viewModel.SelectedWarehouses.Where(w => w.Amount > 0))
            {
                if (!warehouseChanges.ContainsKey(selectedWarehouse.WarehouseId))
                {
                    warehouseChanges[selectedWarehouse.WarehouseId] = selectedWarehouse.Amount;
                }
                else
                {
                    warehouseChanges[selectedWarehouse.WarehouseId] += selectedWarehouse.Amount;
                }
            }

            // Update existing order warehouses
            foreach (var orderWarehouse in existingOrder.OrderWarehouses.ToList())
            {
                if (warehouseChanges.TryGetValue(orderWarehouse.WarehouseId, out var newAmount))
                {
                    orderWarehouse.Amount = newAmount;
                    warehouseChanges.Remove(orderWarehouse.WarehouseId);
                }
                else
                {
                    _context.OrderWarehouses.Remove(orderWarehouse);
                }
            }

            // Add new warehouses to the order
            foreach (var warehouseChange in warehouseChanges)
            {
                existingOrder.OrderWarehouses.Add(new OrderWarehouse
                {
                    WarehouseId = warehouseChange.Key,
                    Amount = warehouseChange.Value
                });
            }

            // Save changes and return to order details
            _context.OrderWarehouseEditHistories.AddRange(orderWarehouseEditHistories);
            await _context.SaveChangesAsync();

            // Fetch warehouse details directly from _context.Warehouses using the WarehouseId
            var warehouseIds = existingOrder.OrderWarehouses.Select(ow => ow.WarehouseId).ToList();

            var warehouses = await _context.Warehouses
                .Where(w => warehouseIds.Contains(w.Id))
                .Include(w => w.MainWarehouse) // Include MainWarehouse if needed
                .ToListAsync();



            // Notify clients via SignalR
            var updatedOrderDetails = new
            {
                Id = existingOrder.Id,
                ManufacturingCompanyName = existingOrder.ManufacturingCompany?.Name,
                ManufacturingCompanyLogoUrl = existingOrder.ManufacturingCompany?.ImageUrl,

                DeliveryCompanyName = existingOrder.DeliveryCompany?.Name,
                DeliveryCompanyLogoUrl = existingOrder.DeliveryCompany?.ImageUrl,
                TotalPrice = existingOrder.TotalPrice,
                CustomerName = existingOrder.CustomerName,
                State = existingOrder.State,
                Address = existingOrder.Address,
                TelephoneNumber = existingOrder.TelephoneNumber,
                SourceName = existingOrder.SourceName,
                OrderSource = existingOrder.OrderSource.ToString(),
                OrderSourceIconUrl = Common.GetSocialMediaIconUrl(existingOrder.OrderSource),
                Notes = existingOrder.Notes,
                DeliveryPrice = existingOrder.DeliveryPrice,
                Warehouses = existingOrder.OrderWarehouses.Select(ow =>
                {
                    var warehouse = warehouses.FirstOrDefault(w => w.Id == ow.WarehouseId);
                    return new
                    {
                        WarehouseId = ow.WarehouseId,
                        WarehouseName = warehouse?.Name ?? "Unknown", // Handle null WarehouseName
                        Amount = ow.Amount,
                        WarehouseImage = warehouse?.MainWarehouse?.ImageUrl ?? "/static/default-image.jpg" // Handle null MainWarehouseImage
                    };
                }).ToList()
            };
            // Log the warehouse data to check if it's being passed correctly
            Console.WriteLine("Updated Order Details: ");
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(updatedOrderDetails));

            foreach (var ow in existingOrder.OrderWarehouses)
            {
                Console.WriteLine($"WarehouseId: {ow.WarehouseId}, WarehouseName: {ow.Warehouse?.Name ?? "null"}, MainWarehouseId: {ow.Warehouse?.MainWarehouseId}, MainWarehouseImage: {ow.Warehouse?.MainWarehouse?.ImageUrl ?? "null"}");
            }

            await _hubContext.Clients.All.SendAsync("OrderDetailsUpdated", updatedOrderDetails);


            // If everything is successful
            return Json(new { success = true });
        }


        // GET: Order/Details/5
        [Authorize]
        public async Task<IActionResult> Details(int id)
        {
            var result = await BuildOrderViewModel(id);
            if (result == null)
                return NotFound();
            if (result.IsForbidden)
                return Forbid();

            return View(result.ViewModel);
        }

        // GET: Order/DetailsPartial/5 — returns the body content without layout (for AJAX modal)
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> DetailsPartial(int id)
        {
            var result = await BuildOrderViewModel(id);
            if (result == null)
                return NotFound();
            if (result.IsForbidden)
                return Forbid();

            return PartialView("_DetailsPartial", result.ViewModel);
        }

        private async Task<OrderViewModelResult> BuildOrderViewModel(int id)
        {
            if (id == 0)
                return null;

            var UserId = User.FindFirst(ClaimTypes.NameIdentifier).Value;

            var OrderQuery = _context.Orders.AsQueryable();
            // call center or follow up acess only his gived manfacture company permisson
            if (User.IsInRole("CallCenter") || User.IsInRole("FollowUpDepartment"))
            {
                OrderQuery = OrderQuery.Where(a => a.ManufacturingCompany.EmployeeManufacturingCompanies.Any(m => m.ApplicationUserId == UserId && m.CanSeeManufacturingCompany));
            }

            // get who set is comment history
            var orderFromCommentsHistory = await _context.OrderFromCommentsHistories
             .Where(eh => eh.OrderId == id)
             .Select(eh => eh.ApplicationUserId)
             .ToListAsync();

            var order = await OrderQuery
                .Include(o => o.ManufacturingCompany)
                .Include(o => o.DeliveryCompany)
                .Include(o => o.Campaign)
                .Include(o => o.OrderWarehouses)
                .ThenInclude(ow => ow.Warehouse)
                 .ThenInclude(w => w.MainWarehouse)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null)
                return null;

            // check if orer not related to them
            if (User.IsInRole("DeliveryCompany") || User.IsInRole("DeliveryRepresentative"))
            {
                if (order.DeliveryCompany.UserId != UserId)
                {
                    return new OrderViewModelResult { IsForbidden = true };
                }
            }

            var employees = _context.Employees.AsQueryable();

            // get employees
            var employee = employees
                .Where(e => e.ApplicationUserId == order.ApplicationUserId)
                .Select(e => new
                {
                    Name = e.Name,
                    Image = e.ImageUrl ?? "static/DefaultImage.svg"
                })
                .FirstOrDefault();
            // fixed by who
            var fixedbyemployee = employees
                     .Where(e => e.ApplicationUserId == order.Fixedby)
                     .Select(e => new
                     {
                         Name = e.Name,
                         Image = e.ImageUrl ?? "static/DefaultImage.svg"
                     }).FirstOrDefault();
            // edited by who
            var editedbyemployee = employees
                 .Where(e => e.ApplicationUserId == order.Editedby)
                     .Select(e => new
                     {
                         Name = e.Name,
                         Image = e.ImageUrl ?? "static/DefaultImage.svg"
                     }).FirstOrDefault();
            // form comments by who
            var fromCommentsEmployee = employees
                 .Where(e => orderFromCommentsHistory.Contains(e.ApplicationUserId))
                     .Select(e => new
                     {
                         Name = e.Name,
                         Image = e.ImageUrl ?? "static/DefaultImage.svg"
                     }).FirstOrDefault();

            // number of entires
            var entries = OrderQuery
                 .Count(o => o.TelephoneNumber == order.TelephoneNumber);

            // pass warehouse info
            var warehouseInfo = order.OrderWarehouses
                 .Select(ow =>
                 {
                     var warehouse = ow.Warehouse;
                     var mainWarehouse = warehouse?.MainWarehouse;

                     return new WarehouseAmountViewModel
                     {
                         WarehouseId = ow.WarehouseId,
                         WarehouseName = warehouse?.Name ?? "Unknown Warehouse",
                         Amount = ow.Amount,
                         Image = mainWarehouse?.ImageUrl ?? "default-image-url",
                         MainWarehouseId = warehouse?.MainWarehouseId,
                     };
                 })
                 .ToList() ?? new List<WarehouseAmountViewModel>();

            // Get the current user's role
            bool isAdmin = User.IsInRole("Admin");

            // Query the order histories
            var orderHistoriesQuery = _context.OrderStatusHistories
                .Where(oh => oh.OrderId == id)
                .Include(oh => oh.User) // Include the User navigation property
                .OrderByDescending(oh => oh.Id)
                .Select(oh => new OrderStatusHistoryModel
                {
                    Id = oh.Id,
                    OrderId = order.Id,
                    CreatedAt = oh.CreatedAt,
                    Status = oh.Status,
                    Reason = oh.Reason,
                    FailureReasonImageUrl = oh.FailureReasonImageUrl,
                    UserName = oh.User.Name,
                    UserId = oh.ApplicationUserId,
                    IsHidden = oh.IsHidden
                });

            // Count distinct failure tracks (each track starts with فشل_التسليم stage 1)
            var failureStatuses = new List<OrderStatusEnum?>
            {
                OrderStatusEnum.فشل_التسليم,
                OrderStatusEnum.فشل_التسليم_2,
                OrderStatusEnum.فشل_التسليم_3,
                OrderStatusEnum.فشل_التسليم_4,
                OrderStatusEnum.فشل_التسليم_5,
                OrderStatusEnum.فشل_التسليم_6,
                OrderStatusEnum.فشل_التسليم_7
            };
            int failedOrderTimes = await orderHistoriesQuery
                .CountAsync(oh => oh.OrderId == id && oh.Status == OrderStatusEnum.فشل_التسليم);

            // Calculate how many times order status was تم_المعالجة
            int fixedOrderTimes = await orderHistoriesQuery
                .CountAsync(oh => oh.OrderId == id && oh.Status == OrderStatusEnum.تم_المعالجة);

            // Apply filter based on user role
            if (!User.IsInRole("Admin"))
            {
                // Apply filter to exclude records where IsHidden is true for non-admin users
                orderHistoriesQuery = orderHistoriesQuery.Where(oh => oh.IsHidden == false);
            }

            var orderHistories = await orderHistoriesQuery.ToListAsync();

            // if is processed
            bool isProcessed = orderHistoriesQuery.Any(oh => oh.Status == OrderStatusEnum.تم_المعالجة);

            // cancel reason to be displayed (newest reason across all failure stages)
            var cancelReasonForDeliveryFailure = orderHistoriesQuery
               .Where(oh => oh.Reason != null)
               .Select(oh => oh.Reason)
               .FirstOrDefault(); // Retrieves newest reason (query is already ordered by Id desc)

            var lastEditHistory = orderHistoriesQuery.FirstOrDefault();

            var orderViewModel = new OrderViewModel
            {
                Id = order.Id,
                CountryId = (int)order.Country,
                Country = order.Country,
                State = order.State,
                OrderSource = order.OrderSource,
                SourceName = order.SourceName,
                ManufacturingCompanyId = order.ManufacturingCompanyId,
                DeliveryCompanyId = order.DeliveryCompanyId,
                TelephoneNumber = order.TelephoneNumber,
                SecondTelephoneNumber = order.SecondTelephoneNumber,
                CustomerName = order.CustomerName,
                Notes = order.Notes,
                Address = order.Address,
                CreatedDate = order.CreatedDate,
                LastEditedDate = order.LastEditedDate,
                OrderStatus = order.OrderStatus,
                TotalPrice = order.TotalPrice,
                ApplicationUserId = order.ApplicationUserId,
                ManufacturingCompany = new ManufacturingCompanyViewModel
                {
                    Id = order.ManufacturingCompany.Id,
                    Name = order.ManufacturingCompany.Name,
                    InvoiceImage = order.ManufacturingCompany.InvoiceImage,
                    Logo = order.ManufacturingCompany.ImageUrl,
                },
                DeliveryCompany = new DeliveryCompanyViewModel
                {
                    Id = order.DeliveryCompany.Id,
                    Name = order.DeliveryCompany.Name,
                    Logo = order.DeliveryCompany.ImageUrl,
                },
                DeliveryCost = order.DeliveryPrice,
                Entries = entries,
                SelectedWarehouses = warehouseInfo,
                OrderStatusHistories = orderHistories,
                CancelReasonForDeliveryFailure = cancelReasonForDeliveryFailure,
                ExternalOrderId = order.ExternalOrderId,
                EmployeeName = employee?.Name ?? "Unknown",
                FromComments = order.FromComments,
                EmployeeImage = employee != null ? employee.Image : null,
                FixedbyEmployee = fixedbyemployee?.Name,
                FromCommentsEmployee = fromCommentsEmployee?.Name,
                FixedbyEmployeeImage = fixedbyemployee?.Image,
                FromCommentsEmployeeImage = fromCommentsEmployee?.Image,
                IsPaid = order.IsPaid,
                IsDiscount = order.IsDiscount,
                Employeebouns = order.IsBonus,
                Gender = order.Gender,
                IsFixedBefore = isProcessed,
                LoggedInUserId = UserId,
                IsClientSpecial = order.IsClientSpecial,
                IsHidden = order.IsHidden,
                IsComplaints = order.IsComplaints,
                // ... existing properties ...
                FixedOrderTimes = fixedOrderTimes,
                FailedOrderTimes = failedOrderTimes,
                chatUrl = order.Chaturl,
                CreationDurationSeconds = order.CreationDurationSeconds,
                PhotoUrl = order.PhotoUrl,
                PaymentReceiptUrl = order.PaymentReceiptUrl,
                CampaignId = order.CampaignId,
                CampaignImageUrl = order.Campaign != null ? order.Campaign.ImageUrl : null,
            };

            if (lastEditHistory != null)
            {
                orderViewModel.LastEditedBy = editedbyemployee?.Name;
                orderViewModel.LastEditedByImage = editedbyemployee?.Image;
            }
            else
            {
                orderViewModel.LastEditedBy = "تحديث تلقائي";
            }

            return new OrderViewModelResult { ViewModel = orderViewModel };
        }

        private class OrderViewModelResult
        {
            public OrderViewModel ViewModel { get; set; }
            public bool IsForbidden { get; set; }
        }


        // تعديل الحالات

        private async Task<string?> SaveFailureReasonImageAsync(
            int orderId,
            IFormFile? explicitFile = null,
            string? explicitBase64 = null,
            bool allowStandardNames = true)
        {
            IFormFile? imageFile = explicitFile;

            if (imageFile == null && Request.HasFormContentType)
            {
                var fileKeys = new[]
                {
                    $"failureReasonImageFile_{orderId}",
                    $"failedReasonImageFile_{orderId}",
                    $"failureImageFile_{orderId}",
                    $"reasonImageFile_{orderId}",
                    $"imageFile_{orderId}",
                    $"failureReasonImageFile[{orderId}]",
                    $"failedReasonImageFile[{orderId}]",
                    $"failureImageFile[{orderId}]",
                    $"reasonImageFile[{orderId}]",
                    $"imageFile[{orderId}]",
                    $"failureReasonImages[{orderId}]",
                    $"failureReasonImages_{orderId}"
                };

                imageFile = Request.Form.Files.FirstOrDefault(file =>
                    fileKeys.Contains(file.Name, StringComparer.OrdinalIgnoreCase));

                if (imageFile == null && allowStandardNames)
                {
                    var standardKeys = new[]
                    {
                        "failureReasonImageFile",
                        "failedReasonImageFile",
                        "failureImageFile",
                        "reasonImageFile",
                        "imageFile"
                    };

                    imageFile = Request.Form.Files.FirstOrDefault(file =>
                        standardKeys.Contains(file.Name, StringComparer.OrdinalIgnoreCase));
                }
            }

            if (imageFile != null && imageFile.Length > 0)
            {
                if (!string.IsNullOrWhiteSpace(imageFile.ContentType)
                    && !imageFile.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                var uploaded = await _fileUploadService.UploadFileAsync(imageFile, $"images/failure-reasons/order-{orderId}");
                return uploaded != null ? "/" + uploaded.TrimStart('/') : null;
            }

            var base64Value = explicitBase64;

            if (string.IsNullOrWhiteSpace(base64Value) && Request.HasFormContentType)
            {
                var base64Keys = new[]
                {
                    $"failureReasonImageBase64_{orderId}",
                    $"failureImageBase64_{orderId}",
                    $"reasonImageBase64_{orderId}",
                    $"failureReasonImageBase64[{orderId}]",
                    $"failureImageBase64[{orderId}]",
                    $"reasonImageBase64[{orderId}]"
                };

                foreach (var key in base64Keys)
                {
                    if (Request.Form.TryGetValue(key, out var foundValue) && !string.IsNullOrWhiteSpace(foundValue))
                    {
                        base64Value = foundValue.ToString();
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(base64Value) && allowStandardNames)
                {
                    var standardBase64Keys = new[]
                    {
                        "failureReasonImageBase64",
                        "failureImageBase64",
                        "reasonImageBase64"
                    };

                    foreach (var key in standardBase64Keys)
                    {
                        if (Request.Form.TryGetValue(key, out var foundValue) && !string.IsNullOrWhiteSpace(foundValue))
                        {
                            base64Value = foundValue.ToString();
                            break;
                        }
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(base64Value))
            {
                return null;
            }

            var base64Text = base64Value.Trim();
            var contentType = "image/png";
            var extension = ".png";

            var commaIndex = base64Text.IndexOf(',');
            if (base64Text.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex > -1)
            {
                var meta = base64Text.Substring(5, commaIndex - 5);
                var semicolonIndex = meta.IndexOf(';');

                if (semicolonIndex > -1)
                {
                    contentType = meta.Substring(0, semicolonIndex);
                }

                base64Text = base64Text.Substring(commaIndex + 1);
            }

            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) || contentType.Equals("image/jpg", StringComparison.OrdinalIgnoreCase))
            {
                extension = ".jpg";
            }
            else if (contentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase))
            {
                extension = ".webp";
            }
            else if (contentType.Equals("image/gif", StringComparison.OrdinalIgnoreCase))
            {
                extension = ".gif";
            }

            byte[] imageBytes;

            try
            {
                imageBytes = Convert.FromBase64String(base64Text);
            }
            catch
            {
                return null;
            }

            if (imageBytes.Length == 0)
            {
                return null;
            }

            await using var stream = new MemoryStream(imageBytes);
            var formFile = new FormFile(stream, 0, imageBytes.Length, "failureReasonImageFile", $"failure-reason-{orderId}-{Guid.NewGuid():N}{extension}")
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };

            var uploadedBase64Image = await _fileUploadService.UploadFileAsync(formFile, $"images/failure-reasons/order-{orderId}");
            return uploadedBase64Image != null ? "/" + uploadedBase64Image.TrimStart('/') : null;
        }

        private async Task<string?> GetLatestFailureReasonImageUrlAsync(int orderId)
        {
            return await _context.OrderStatusHistories
                .Where(oh => oh.OrderId == orderId
                    && oh.FailureReasonImageUrl != null
                    && oh.FailureReasonImageUrl != "")
                .OrderByDescending(oh => oh.Id)
                .Select(oh => oh.FailureReasonImageUrl)
                .FirstOrDefaultAsync();
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetLatestFailureReasonImage(int orderId)
        {
            if (orderId <= 0)
            {
                return Json(new { success = false, message = "رقم الطلب غير صحيح" });
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var orderQuery = _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == orderId && !o.IsHidden);

            if (User.IsInRole("CallCenter"))
            {
                orderQuery = orderQuery.Where(o => o.ApplicationUserId == currentUserId);
            }

            if (User.IsInRole("FollowUpDepartment"))
            {
                orderQuery = orderQuery.Where(o =>
                    o.ManufacturingCompany.EmployeeManufacturingCompanies.Any(a =>
                        a.ApplicationUserId == currentUserId &&
                        a.CanSeeManufacturingCompany));
            }

            if (User.IsInRole("DeliveryCompany") || User.IsInRole("DeliveryRepresentative"))
            {
                orderQuery = orderQuery.Where(o => o.DeliveryCompany.UserId == currentUserId);
            }

            var canSeeOrder = await orderQuery.AnyAsync();
            if (!canSeeOrder)
            {
                return Json(new { success = false, message = "الطلب غير موجود أو ليس لديك صلاحية عليه" });
            }

            var imageUrl = await GetLatestFailureReasonImageUrlAsync(orderId);

            return Json(new
            {
                success = true,
                orderId,
                imageUrl
            });
        }


        private bool HasStatusUpdateImageInputForOrder(
            int orderId,
            bool allowStandardNames,
            IFormFile? explicitFile = null,
            string? explicitBase64 = null)
        {
            if (explicitFile != null && explicitFile.Length > 0)
            {
                return string.IsNullOrWhiteSpace(explicitFile.ContentType)
                    || explicitFile.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrWhiteSpace(explicitBase64))
            {
                return true;
            }

            if (!Request.HasFormContentType)
            {
                return false;
            }

            var fileKeys = new[]
            {
                $"failureReasonImageFile_{orderId}",
                $"failedReasonImageFile_{orderId}",
                $"failureImageFile_{orderId}",
                $"reasonImageFile_{orderId}",
                $"imageFile_{orderId}",
                $"failureReasonImageFile[{orderId}]",
                $"failedReasonImageFile[{orderId}]",
                $"failureImageFile[{orderId}]",
                $"reasonImageFile[{orderId}]",
                $"imageFile[{orderId}]",
                $"failureReasonImages[{orderId}]",
                $"failureReasonImages_{orderId}"
            };

            var hasNamedImageFile = Request.Form.Files.Any(file =>
                file.Length > 0
                && fileKeys.Contains(file.Name, StringComparer.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(file.ContentType)
                    || file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)));

            if (hasNamedImageFile)
            {
                return true;
            }

            if (allowStandardNames)
            {
                var standardKeys = new[]
                {
                    "failureReasonImageFile",
                    "failedReasonImageFile",
                    "failureImageFile",
                    "reasonImageFile",
                    "imageFile"
                };

                var hasStandardImageFile = Request.Form.Files.Any(file =>
                    file.Length > 0
                    && standardKeys.Contains(file.Name, StringComparer.OrdinalIgnoreCase)
                    && (string.IsNullOrWhiteSpace(file.ContentType)
                        || file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)));

                if (hasStandardImageFile)
                {
                    return true;
                }
            }

            var base64Keys = new[]
            {
                $"failureReasonImageBase64_{orderId}",
                $"failureImageBase64_{orderId}",
                $"reasonImageBase64_{orderId}",
                $"failureReasonImageBase64[{orderId}]",
                $"failureImageBase64[{orderId}]",
                $"reasonImageBase64[{orderId}]"
            };

            foreach (var key in base64Keys)
            {
                if (Request.Form.TryGetValue(key, out var foundValue)
                    && !string.IsNullOrWhiteSpace(foundValue.ToString()))
                {
                    return true;
                }
            }

            if (allowStandardNames)
            {
                var standardBase64Keys = new[]
                {
                    "failureReasonImageBase64",
                    "failureImageBase64",
                    "reasonImageBase64"
                };

                foreach (var key in standardBase64Keys)
                {
                    if (Request.Form.TryGetValue(key, out var foundValue)
                        && !string.IsNullOrWhiteSpace(foundValue.ToString()))
                    {
                        return true;
                    }
                }
            }

            return false;
        }


        [HttpPost]
        [Authorize(Roles = "Admin,FollowUpDepartment,ExecutiveDirector,DeliveryCompany,CallCenter,DeliveryRepresentative")]
        public async Task<IActionResult> UpdateStatus(int id, OrderStatusEnum orderStatus, string? reason = null, IFormFile? failureReasonImageFile = null, string? failureReasonImageBase64 = null)
        {
            var order = await _context.Orders
                .Include(o => o.OrderWarehouses)
                .ThenInclude(ow => ow.Warehouse)
                                .Include(o => o.ManufacturingCompany)

                                                .Include(o => o.DeliveryCompany)

                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return Json(new { success = false, message = "Order not found." });
            }

            var failureReasonImageUrl = await SaveFailureReasonImageAsync(
                order.Id,
                failureReasonImageFile,
                failureReasonImageBase64,
                allowStandardNames: true);

            if (OrderStatusHelper.IsFailureStatus(orderStatus) && string.IsNullOrWhiteSpace(failureReasonImageUrl))
            {
                return Json(new { success = false, message = "صورة سبب الفشل إجبارية. اختاري السبب ثم أضيفي صورة قبل الحفظ." });
            }

            // CallCenter can ONLY apply تم المعالجة to orders in specific statuses
            if (User.IsInRole("CallCenter") && orderStatus == OrderStatusEnum.تم_المعالجة)
            {
                var callCenterCanProcess =
                    OrderStatusHelper.IsFailureStatus(order.OrderStatus)
                    || OrderStatusHelper.IsIncompleteStatus(order.OrderStatus)
                    || order.OrderStatus == OrderStatusEnum.أرشيف_المرجع
                    || order.OrderStatus == OrderStatusEnum.انتظار_المعالجة
                    || order.OrderStatus == OrderStatusEnum.الطلبات_المرجعة
                    || order.OrderStatus == OrderStatusEnum.تم_الإلغاء
                    || order.OrderStatus == OrderStatusEnum.الطلبات_الغير_معرفة;

                if (!callCenterCanProcess)
                {
                    return Json(new { success = false, message = "لا يمكن لقسم الكول سنتر تطبيق تم المعالجة على هذا الطلب." });
                }
            }

            Console.WriteLine($"Current Status: {order.OrderStatus} ({(int)order.OrderStatus}), New Status: {orderStatus} ({(int)orderStatus}), Reason: {reason}, IsFailure: {OrderStatusHelper.IsFailureStatus(orderStatus)}");

            bool isNotDelivered = !_context.OrderStatusHistories.Any(h => h.Status == OrderStatusEnum.تم_التسليم && h.OrderId == order.Id);

            if (order.OrderStatus == OrderStatusEnum.تم_الدفع && orderStatus != OrderStatusEnum.تم_الدفع)
            {
                order.IsPaid = false;
                Console.WriteLine("Order payment status changed to unpaid.");
            }

            if (orderStatus == OrderStatusEnum.تم_تحديث_الرصيد && isNotDelivered)
            {
                return Json(new { success = false, message = "الطلب لم يتسلم." });
            }

            var roles = await _userManager.GetRolesAsync(await _userManager.GetUserAsync(User));
            var roleName = roles.FirstOrDefault();

            if (roleName == "DeliveryCompany" || roleName == "DeliveryRepresentative")
            {
                var deliveryError = _roleAuthService.ValidateDeliveryRoleStatusChange(order.OrderStatus, orderStatus);
                if (deliveryError != null)
                    return Json(new { success = false, message = deliveryError });
            }

            // Failure reason validation + carry-forward for failure stages
            if (OrderStatusHelper.RequiresLegalFailureReason(orderStatus))
            {
                if (string.IsNullOrEmpty(reason))
                {
                    // Carry-forward: copy reason from the latest failure-stage history record
                    var previousReason = await _context.OrderStatusHistories
                        .Where(oh => oh.OrderId == id && oh.Reason != null)
                        .OrderByDescending(oh => oh.Id)
                        .Select(oh => oh.Reason)
                        .FirstOrDefaultAsync();

                    if (previousReason != null)
                    {
                        reason = previousReason;
                    }
                    else
                    {
                        return Json(new { success = false, message = "يجب تحديد سبب الفشل." });
                    }
                }
                else if (!OrderStatusHelper.IsValidFailureReason(reason))
                {
                    return Json(new { success = false, message = "سبب الفشل غير صالح." });
                }
            }

            if (OrderStatusHelper.IsFailureStatus(orderStatus))
            {
                // append in notificaiton
                var failedOrderNotification = new
                {
                    orderId = order.Id,
                    country = order.Country, // Assuming you have these fields
                    deliveryCompanyName = order.DeliveryCompany.Name, // Assuming this relationship exists
                    manufacturerCompanyName = order.ManufacturingCompany.Name, // Assuming this relationship exists
                    failureReason = reason // Pass failure reason if applicable
                };

                // Send real-time notification to the clients
                await _hubContext.Clients.Group("UsersExpectDelivery").SendAsync("FailedOrdersNotification", failedOrderNotification);
                await _hubContext.Clients.Group($"deliveryCompany_{order.DeliveryCompanyId}").SendAsync("FailedOrdersNotification", failedOrderNotification);

            }

            if (orderStatus == OrderStatusEnum.تم_المعالجة)
            {
                order.FixedOrderDate = _timeService.GetIstanbulTimeWithOffset();
            }

            OrderStatusEnum currentStatus = order.OrderStatus;
            bool statusChanged = order.OrderStatus != orderStatus;
            string responseAction = "none";
            if (statusChanged)
            {
                order.OrderStatus = orderStatus;
                order.LastEditedDate = _timeService.GetIstanbulTimeWithOffset();

                Console.WriteLine($"Order status updated to: {orderStatus}");

                if (string.IsNullOrWhiteSpace(failureReasonImageUrl)
                    && OrderStatusHelper.RequiresLegalFailureReason(orderStatus))
                {
                    failureReasonImageUrl = await GetLatestFailureReasonImageUrlAsync(order.Id);
                }

                var orderHistory = new OrderStatusHistory
                {
                    OrderId = order.Id,
                    Status = orderStatus,
                    CreatedAt = _timeService.GetIstanbulTimeWithOffset(),
                    ApplicationUserId = _userManager.GetUserId(User),
                    Reason = reason,
                    FailureReasonImageUrl = failureReasonImageUrl,
                };

                _context.OrderStatusHistories.Add(orderHistory);

                var colorStyle = OrderStatusHelper.StatusColorMapping.GetValueOrDefault(orderStatus, "");
                var StatusPhrase = OrderStatusHelper.GetOrderStatusPhrase(orderStatus);

                // Get the UserName dynamically using UserService
                var userName = await _dynamicCommon.GetUserNameByIdAsync(orderHistory.ApplicationUserId);




                // Pass the object directly to SignalR
                var orderStatusData = new
                {
                    orderHistory.OrderId,
                    orderHistory.Id,
                    orderHistory.Status,
                    orderHistory.CreatedAt,
                    orderHistory.ApplicationUserId,
                    orderHistory.Reason,
                    UserName = userName,  // Use the dynamically fetched UserName
                    StatusPhrase = StatusPhrase,
                    ColorStyle = colorStyle
                };


                Console.WriteLine(orderStatusData);

                Console.WriteLine(orderStatusData);
                Console.WriteLine(orderStatusData);
                Console.WriteLine(orderStatusData);
                Console.WriteLine(orderStatusData);
                Console.WriteLine(orderStatusData);
                Console.WriteLine(orderStatusData);
                Console.WriteLine(orderStatusData);
                Console.WriteLine(orderStatusData);
                Console.WriteLine(orderStatusData);
                Console.WriteLine(orderStatusData);
                Console.WriteLine(orderStatusData);
                Console.WriteLine(orderStatusData);
                Console.WriteLine(orderStatusData);
                Console.WriteLine(orderStatusData);

                // Send to UsersExpectDelivery group
                await _hubContext.Clients.Group("UsersExpectDelivery").SendAsync("OrderStatusUpdated", orderStatusData);

                // Send to delivery company group
                var targetDeliveryCompanyId = order.DeliveryCompanyId;
                var deliveryCompanyGroup = $"deliveryCompany_{targetDeliveryCompanyId}";
                await _hubContext.Clients.Group(deliveryCompanyGroup).SendAsync("OrderStatusUpdated", orderStatusData);

                // Send to manufacturing company group
                var targetManufacturingCompanyId = order.ManufacturingCompanyId;
                var manufacturingCompanyGroup = $"manufacturingCompany_{targetManufacturingCompanyId}";
                await _hubContext.Clients.Group(manufacturingCompanyGroup).SendAsync("OrderStatusUpdated", orderStatusData);
                responseAction = "status_changed";
            }
            else
            {
                Console.WriteLine("Order status did not change.");

                // If status didn't change but a reason was provided for a failure status,
                // update the latest history record's reason so "إضافة سبب الفشل" works
                if (!string.IsNullOrEmpty(reason))
                {
                    var latestHistory = await _context.OrderStatusHistories
                        .Where(oh => oh.OrderId == id && oh.Status == orderStatus)
                        .OrderByDescending(oh => oh.Id)
                        .FirstOrDefaultAsync();

                    if (latestHistory != null)
                    {
                        latestHistory.Reason = reason;

                        if (!string.IsNullOrWhiteSpace(failureReasonImageUrl))
                        {
                            latestHistory.FailureReasonImageUrl = failureReasonImageUrl;
                        }

                        responseAction = "reason_updated";
                    }
                    else
                    {
                        // No history record exists for this status yet — create one
                        var orderHistory = new OrderStatusHistory
                        {
                            OrderId = order.Id,
                            Status = orderStatus,
                            CreatedAt = _timeService.GetIstanbulTimeWithOffset(),
                            ApplicationUserId = _userManager.GetUserId(User),
                            Reason = reason,
                            FailureReasonImageUrl = failureReasonImageUrl,
                        };
                        _context.OrderStatusHistories.Add(orderHistory);
                        responseAction = "reason_created";
                    }
                }
            }

            _context.Update(order);
            await _context.SaveChangesAsync();
            Console.WriteLine("Order changes saved to database.");

            if (order.ExternalOrderId.HasValue)
            {
                var updateStatusRequest = new UpdateStatusRequest
                {
                    NewStatus = orderStatus,
                    Reason = reason,
                };

                var response = await _restApi.UpdateOrderStatusAsync(order.ExternalOrderId.Value, updateStatusRequest);
                Console.WriteLine($"External API response: {response}");
            }

            return Json(new { success = true, message = "تم تغيير حالة الطلب بنجاح", orderId = id, statusChanged, action = responseAction, currentStatus = (int)order.OrderStatus, receivedStatus = (int)orderStatus, isFailure = OrderStatusHelper.IsFailureStatus(orderStatus), receivedReason = reason });
        }



        // تعيين الموظف في صفحة تفاصيل الطلب 
        [HttpPost]
        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public async Task<IActionResult> UpdateOrderApplicationUser(int orderId, string newApplicationUserId)
        {
            if (string.IsNullOrWhiteSpace(newApplicationUserId))
            {
                return BadRequest("Invalid user ID provided.");
            }

            // Find the order by ID
            var orderToUpdate = await _context.Orders
                .Include(o => o.ApplicationUser)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (orderToUpdate == null)
            {
                return NotFound($"Order with ID {orderId} not found.");
            }

            var oldApplicationUserId = orderToUpdate.ApplicationUserId;
            orderToUpdate.ApplicationUserId = newApplicationUserId;

            // Fetch the employee details from the Employees table
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == newApplicationUserId);

            if (employee == null)
            {
                return NotFound($"Employee with ID {newApplicationUserId} not found.");
            }

            var user = await _userManager.GetUserAsync(User);

            // Create a log entry
            var orderUserChangeHistory = new OrderUserChangeHistory
            {
                OrderId = orderId,
                PreviousOrderEntryUser = oldApplicationUserId,
                NewOrderEntryUser = newApplicationUserId,
                ChangedBy = user.Id,
                ChangedOn = _timeService.GetIstanbulTimeWithOffset(),
            };
            _context.OrderUserChangeHistories.Add(orderUserChangeHistory);

            // Save changes to the database
            await _context.SaveChangesAsync();

            // Prepare the data to be sent via SignalR
            var updateData = new
            {
                OrderId = orderId,
                EmployeeName = employee.Name ?? "Unknown",
                EmployeeImage = employee.ImageUrl ?? "/static/LuxiraLogo.svg"
            };

            // Broadcast the update to all connected clients
            await _hubContext.Clients.All.SendAsync("OrderApplicationUserUpdated", updateData);

            // Return success response
            return Json(new { success = true });
        }


        [HttpPost]
        [Authorize]
        public async Task<IActionResult> PostponeOrder(int orderId, DateTime newCreatedDate)
        {
            var orderToUpdate = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (orderToUpdate == null)
                return NotFound($"Order with ID {orderId} not found.");

            orderToUpdate.CreatedDate = newCreatedDate;

            var currentDate = _timeService.GetIstanbulTimeWithOffset();
            var totalDays = (newCreatedDate - currentDate).TotalDays;

            // Check if the created date is more than 48 hours from now
            var newOrderStatus = totalDays > 2 ? OrderStatusEnum.الطلبات_المؤجلة : OrderStatusEnum.طلب_جديد;

            if ((orderToUpdate.OrderStatus == OrderStatusEnum.الطلبات_المؤجلة && newOrderStatus == OrderStatusEnum.طلب_جديد)
              || newOrderStatus == OrderStatusEnum.الطلبات_المؤجلة)
            {
                orderToUpdate.OrderStatus = newOrderStatus;

                var orderStatusHistory = new OrderStatusHistory
                {
                    OrderId = orderToUpdate.Id,
                    Status = newOrderStatus,
                    CreatedAt = _timeService.GetIstanbulTimeWithOffset(),
                    ApplicationUserId = _userManager.GetUserId(User)
                };

                _context.OrderStatusHistories.Add(orderStatusHistory);
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }


        [HttpPost]
        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public async Task<IActionResult> HideOrder(int orderId, bool isHidden)
        {
            var orderToToggle = await _context.Orders.FindAsync(orderId);

            if (orderToToggle == null)
            {
                return NotFound($"Order with ID {orderId} not found.");
            }

            orderToToggle.IsHidden = isHidden;
            await _context.SaveChangesAsync();

            // Prepare data to broadcast
            var hideOrderData = new
            {
                orderId = orderId,
                isHidden = orderToToggle.IsHidden
            };

            // Broadcast the update to all connected clients
            await _hubContext.Clients.All.SendAsync("OrderHiddenStatusUpdated", hideOrderData);

            string statusMessage = isHidden ? "hidden" : "unhidden";
            return Ok($"الطلب بالمعرّف {orderId} الآن {statusMessage}.");
        }


        // POST: Product/SetSpecial/5
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> SetSpecial(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound();
            }

            order.IsClientSpecial = !order.IsClientSpecial;
            _context.Update(order);
            await _context.SaveChangesAsync();

            // Prepare data to broadcast
            var orderSpecialData = new
            {
                OrderId = id,
                IsClientSpecial = order.IsClientSpecial
            };

            // Broadcast the update to all clients
            await _hubContext.Clients.All.SendAsync("UpdateOrderClientType", orderSpecialData);

            return Json(new { redirectUrl = Url.Action("Details", "Order", new { id = id }) });
        }


        // POST: Product/SetIsComplaints/5
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> SetIsComplaints(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound();
            }

            order.IsComplaints = !order.IsComplaints;
            _context.Update(order);
            await _context.SaveChangesAsync();

            // Prepare data to broadcast
            var complaintsData = new
            {
                OrderId = id,
                IsComplaints = order.IsComplaints
            };

            // Broadcast the update to all clients
            await _hubContext.Clients.All.SendAsync("UpdateOrderComplaintsType", complaintsData);

            return Json(new { redirectUrl = Url.Action("Details", "Order", new { id = id }) });
        }

        [Authorize]
        public IActionResult FailureReasonModal()
        {
            return PartialView("_FailureReasonModal");
        }

        [Authorize]
        public IActionResult PaymentReceiptModal()
        {
            return PartialView("_PaymentReceiptModal");
        }

        [HttpPost]
        [Authorize]
        [Route("Order/SetIsPaid/{id}")]
        public async Task<IActionResult> SetIsPaid(int id, bool isPaid, IFormFile? paymentReceiptFile)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound();
            }

            if (isPaid)
            {
                if (paymentReceiptFile == null || paymentReceiptFile.Length == 0)
                {
                    if (string.IsNullOrWhiteSpace(order.PaymentReceiptUrl))
                    {
                        return BadRequest(new { success = false, message = "الرجاء إرفاق صورة إيصال الحوالة البنكية." });
                    }
                }
                else
                {
                    var uploadedReceipt = await _fileUploadService.UpdateFileAsync(order.PaymentReceiptUrl?.TrimStart('/'), paymentReceiptFile, "images/receipts");
                    order.PaymentReceiptUrl = uploadedReceipt != null ? "/" + uploadedReceipt.TrimStart('/') : null;
                }
            }

            // Set the explicit value instead of toggling
            order.IsPaid = isPaid;
            _context.Update(order);
            await _context.SaveChangesAsync();

            var complaintsData = new
            {
                OrderId = id,
                IsPaid = order.IsPaid
            };

            await _hubContext.Clients.All.SendAsync("OrderDetailsUpdated", complaintsData);

            return Json(new
            {
                redirectUrl = Url.Action("Details", "Order", new { id = id }),
                isPaid = order.IsPaid
            });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SetBonusPaidForEmployee([FromBody] List<int> orderIds)
        {
            if (orderIds == null || !orderIds.Any())
            {
                return BadRequest("No order IDs provided.");
            }

            var orders = await _context.Orders.Where(o => orderIds.Contains(o.Id)).ToListAsync();

            foreach (var order in orders)
            {
                order.IsBonusPaidForEmployee = true;
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "تم الدفع ", redirectUrl = Url.Action("Employees", "Financial") });
        }


        [HttpGet]
        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public async Task<IActionResult> GetAllOrderIdsForEmployeeBonus(string? employeeId = null, bool? isEmployeebonus = null)
        {
            IQueryable<Order> query = _context.Orders.AsNoTracking();

            if (!string.IsNullOrEmpty(employeeId))
            {
                query = query.Where(x => x.ApplicationUserId == employeeId);
            }

            if (isEmployeebonus.HasValue)
            {
                query = query.Where(x => x.IsBonus);

            }

            var orderIds = await query.Select(o => o.Id).ToListAsync();

            return Ok(orderIds);
        }


        // enhanced performance  
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> RemoveWarehouse(int orderId, int warehouseId)
        {
            // Retrieve the existing order along with its associated warehouses
            var existingOrder = await _context.Orders
                .Include(o => o.OrderWarehouses)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (existingOrder == null)
            {
                return NotFound();
            }

            // Check if the order has already been delivered
            if (existingOrder.OrderStatus == OrderStatusEnum.تم_التسليم)
            {
                return Content("تم التسليم لا تستطيع التعديل عليه");
            }

            // Find the warehouse to remove from the order
            var orderWarehouse = existingOrder.OrderWarehouses.FirstOrDefault(ow => ow.WarehouseId == warehouseId);

            if (orderWarehouse != null)
            {
                // Retrieve the warehouse from the database
                var warehouse = await _context.Warehouses.FindAsync(warehouseId);

                if (warehouse != null)
                {
                    // Increase the warehouse's amount by the amount from the order
                    warehouse.Amount += orderWarehouse.Amount;
                    _context.Update(warehouse);
                }

                // Remove the warehouse from the order
                existingOrder.OrderWarehouses.Remove(orderWarehouse);
                _context.Update(existingOrder);

                // Save changes to the database
                await _context.SaveChangesAsync();

                return Ok(); // or return appropriate response
            }

            return NotFound(); // Warehouse not found in order
        }


        [Authorize]
        public IActionResult PrintOrder(string ids)
        {
            // Check if the string is null or empty
            if (string.IsNullOrEmpty(ids))
            {
                Console.WriteLine("No IDs received.");
                return BadRequest("No order IDs provided.");
            }

            // Split the comma-separated string into an array of strings
            var idArray = ids.Split(',');

            // Convert the array of strings into an array of integers
            var orderIds = idArray.Select(id => int.TryParse(id, out var parsedId) ? parsedId : (int?)null)
                                 .Where(id => id.HasValue)
                                 .Select(id => id.Value)
                                 .ToArray();

            if (orderIds.Length == 0)
            {
                Console.WriteLine("No valid IDs received.");
                return BadRequest("Invalid order IDs provided.");
            }

            Console.WriteLine($"IDs Received: {string.Join(", ", orderIds)}"); // Print the valid ids

            var ordersViewModels = new List<OrderViewModel>();
            bool hasCompany8 = false;

            foreach (var id in orderIds)
            {
                Console.WriteLine($"Processing Order ID: {id}"); // Print the id before querying the database

                var order = _context.Orders
                    .AsNoTracking()
                    .Include(o => o.ManufacturingCompany)
                    .Include(o => o.OrderWarehouses)
                        .ThenInclude(ow => ow.Warehouse)
                        .ThenInclude(ow => ow.MainWarehouse)
                    .FirstOrDefault(o => o.Id == id);

                if (order != null)
                {
                    // Check if any order has ManufacturingCompanyId == 8
                    if (order.ManufacturingCompanyId == 8)
                    {
                        hasCompany8 = true;
                    }


                    var qrCodeUrl = Url.Action("Details", "Order", new { id = id }, protocol: Request.Scheme);

                    var orderViewModel = new OrderViewModel
                    {
                        Id = order.Id,
                        CustomerName = order.CustomerName,
                        Country = order.Country,
                        State = order.State,
                        Address = order.Address,
                        IsPaid = order.IsPaid,
                        TelephoneNumber = order.TelephoneNumber,
                        CreatedDate = order.CreatedDate,
                        ManufacturingCompanyId = order.ManufacturingCompanyId,
                        ManufacturingCompany = new ManufacturingCompanyViewModel
                        {
                            Id = order.ManufacturingCompany.Id,
                            Name = order.ManufacturingCompany.Name
                        },
                        ManufacturingCompanyName = order.ManufacturingCompany.Name,
                        QRCodeUrl = qrCodeUrl,
                        SelectedWarehouses = order.OrderWarehouses.Select(ow => new WarehouseAmountViewModel
                        {
                            WarehouseId = ow.WarehouseId,
                            WarehouseName = ow.Warehouse?.Name ?? "N/A",
                            Amount = ow.Amount,
                            Image = ow.Warehouse.MainWarehouse.ImageUrl,
                        }).ToList(),
                        TotalPrice = order.IsPaid ? 0 : order.TotalPrice
                    };

                    ordersViewModels.Add(orderViewModel);
                }
            }


            // If any order has ManufacturingCompanyId == 8, return the special view
            if (hasCompany8)
            {
                return View("PrintOrder8", ordersViewModels);
            }

            return View(ordersViewModels);
        }



        [Authorize(Roles = "Admin,Accountant,FollowUpDepartment,ExecutiveDirector,DeliveryCompany,DeliveryRepresentative")]
        public async Task<IActionResult> PrintSelectedOrders(string ids)
        {
            if (string.IsNullOrEmpty(ids))
            {
                return NotFound("No orders selected.");
            }

            var selectedOrderIds = ids.Split(',').Select(int.Parse).ToList();

            var orders = await _context.Orders
                                            .Include(or => or.DeliveryCompany)

                                       .Where(o => selectedOrderIds.Contains(o.Id))
                                       .ToListAsync();

            if (!orders.Any())
            {
                return NotFound("Selected orders not found.");
            }


            var headers = new List<string> { " كود الشحنة", "التاريخ", "اسم العميل", "رقم الهاتف", "المدينة", "المبلغ الإجمالي ", "سعر التوصيل", "صافي المبلغ" };

            var valueSelectors = new List<Func<Order, string>> {
                        o => o.Id.ToString(),
                        o => o.CreatedDate.ToString("yyyy-MM-dd"),
                        o => o.CustomerName,
                        o => o.TelephoneNumber,
                        o => o.State,
                        o => o.TotalPrice.ToString() // Consider formatting this as currency
                    };




            // Calculate RemainingValue and totalDeliveryPrice directly using the DeliveryPrice from orders
            decimal remainingValue = orders.Sum(order => order.TotalPrice - order.DeliveryPrice);
            decimal totalDeliveryPrice = orders.Sum(order => order.DeliveryPrice);




            // Assuming you want the country of the first order
            string countryName = orders.FirstOrDefault()?.Country.ToString();
            string currencyCode = Common.GetCurrencyByCountryName(countryName);
            decimal totalValue = orders.Sum(o => o.TotalPrice);
            string totalAmount = $"{totalValue:N2}\u200E {currencyCode}";

            string deliveryAmount = $"{totalDeliveryPrice:N2}\u200E {currencyCode}";
            // Calculate the total amount
            string remaningAmount = $"{remainingValue:N2}\u200E {currencyCode}";
            var firstOrder = orders.FirstOrDefault();

            // Other details
            var deliveryCompanyName = firstOrder.DeliveryCompany?.Name ?? "DefaultCompanyName";
            var deliveryCompanyAddress = firstOrder.DeliveryCompany?.Address ?? "DefaultCompanyAddress";
            var deliveryCompanyPhoneNumber = firstOrder.DeliveryCompany?.PhoneNumber ?? "DefaultCompanyPhoneNumber";
            var createdDateString = _timeService.GetIstanbulTimeWithOffset().ToString("yyyy-MM-dd");
            var reportIdString = firstOrder.Id.ToString();
            var totalOrderNumberString = orders.Count.ToString();



            var pdfBytes = await _reportGenerator.CreatePdfReportAsync(
                orders, headers, valueSelectors,
                deliveryCompanyName, deliveryCompanyAddress, deliveryCompanyPhoneNumber,
                createdDateString, reportIdString, totalAmount, deliveryAmount, remaningAmount, totalOrderNumberString, countryName);

            Response.Headers.Add("Content-Disposition", "inline; filename=OrdersReport.pdf");


            return File(pdfBytes, "application/pdf");
        }

        [Authorize(Roles = "Admin,Accountant,FollowUpDepartment,ExecutiveDirector,DeliveryCompany,DeliveryRepresentative")]
        public async Task<IActionResult> PrintSelectedOrdersForDelivery(int[] ids)
        {
            // Assuming you have a DbContext or a service to fetch orders, replace 'yourDbContext' with your actual data context or service.
            var orders = await _context.Orders
                .Where(order => ids.Contains(order.Id)) // Filter orders by the provided ids
                .Include(order => order.ManufacturingCompany) // Include related data as needed
                .Include(order => order.OrderWarehouses)
                    .ThenInclude(ow => ow.Warehouse)
                .ToListAsync();

            // Transform the fetched orders into a list of OrderViewModel
            var orderViewModels = orders.Select(order => new OrderViewModel
            {
                Id = order.Id,
                CustomerName = order.CustomerName,
                Country = order.Country,
                State = order.State,
                Address = order.Address,
                TelephoneNumber = order.TelephoneNumber,
                CreatedDate = order.CreatedDate,
                ManufacturingCompanyId = order.ManufacturingCompanyId,
                ManufacturingCompany = new ManufacturingCompanyViewModel
                {
                    Id = order.ManufacturingCompany.Id,
                    Name = order.ManufacturingCompany.Name
                },
                ManufacturingCompanyName = order.ManufacturingCompany?.Name, // Use conditional access to avoid null reference
                SelectedWarehouses = order.OrderWarehouses.Select(ow => new WarehouseAmountViewModel
                {
                    WarehouseId = ow.WarehouseId,
                    WarehouseName = ow.Warehouse?.Name ?? "N/A",
                    Amount = ow.Amount,
                    Image = ow.Warehouse?.MainWarehouse.ImageUrl, // Use conditional access to avoid null reference
                }).ToList(),
                TotalPrice = order.TotalPrice
            }).ToList();

            // Pass the list of OrderViewModels to the view
            return View("PrintSelectedOrdersForDelivery", orderViewModels);
        }



        // الطلبات
        [HttpPost]
        [Authorize(Roles = "Admin,FollowUpDepartment,ExecutiveDirector,DeliveryCompany,CallCenter,DeliveryRepresentative")]
        public async Task<IActionResult> UpdateStatusForMultiple(List<int> ids, OrderStatusEnum orderStatus, string? reason = null, string? orderReasons = null, IFormFile? failureReasonImageFile = null, string? failureReasonImageBase64 = null)
        {
            var orders = await _context.Orders
                .Include(o => o.OrderWarehouses)
                .ThenInclude(ow => ow.Warehouse)
                .Where(o => ids.Contains(o.Id))
                .ToListAsync();

            if (orders == null || !orders.Any())
            {
                return Json(new { success = false, message = "No orders found for the provided IDs." });
            }

            var nowForStatusUpdateSelections = _timeService.GetIstanbulTimeWithOffset();
            var activeStatusUpdateSelections = await GetActiveStatusUpdateSelectionsQuery(nowForStatusUpdateSelections)
                .Where(x => ids.Contains(x.OrderId))
                .AsNoTracking()
                .Select(x => new
                {
                    x.OrderId,
                    x.SelectedByUserId
                })
                .ToListAsync();

            var temporaryFailureImagePathByOrderId = ids
                .Where(id => _temporaryStatusSelectionFailureImages.ContainsKey(id))
                .Distinct()
                .ToDictionary(id => id, id => _temporaryStatusSelectionFailureImages[id]);

            // Parse per-order reasons if provided (JSON: {"orderId":"reason",...})
            Dictionary<int, string>? perOrderReasons = null;
            if (!string.IsNullOrEmpty(orderReasons))
            {
                try
                {
                    perOrderReasons = System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, string>>(orderReasons);
                }
                catch (System.Text.Json.JsonException)
                {
                    return Json(new { success = false, message = "تنسيق أسباب الفشل غير صالح." });
                }
            }

            var roles = await _userManager.GetRolesAsync(await _userManager.GetUserAsync(User));
            var roleName = roles.FirstOrDefault();

            if (roleName != "Admin" && roleName != "ExecutiveDirector" && roleName != "FollowUpDepartment")
            {
                if (!_roleAuthService.CanUpdateStatus(roleName, orderStatus))
                {
                    return Json(new { success = false, message = "لاتسطتيع التعديل" });
                }
            }

            //if (orderStatus == OrderStatusEnum.فشل_التسليم)
            //{
            //    return Json(new { success = false, message = "لايمكنك تحديث فشل التسليم" });
            //}

            // Validate failure reason when targeting a failure status
            if (OrderStatusHelper.RequiresLegalFailureReason(orderStatus))
            {
                if (perOrderReasons != null && perOrderReasons.Count > 0)
                {
                    foreach (var kvp in perOrderReasons)
                    {
                        if (string.IsNullOrEmpty(kvp.Value) || !OrderStatusHelper.IsValidFailureReason(kvp.Value))
                            return Json(new { success = false, message = $"سبب الفشل غير صالح للطلب رقم {kvp.Key}." });
                    }
                    var missingReasonIds = ids.Where(id => !perOrderReasons.ContainsKey(id)).ToList();
                    if (missingReasonIds.Any())
                        return Json(new { success = false, message = "بعض الطلبات المحددة لا تحتوي على سبب فشل.", orderIds = missingReasonIds });
                }
                else if (string.IsNullOrEmpty(reason))
                {
                    return Json(new { success = false, message = "يجب تحديد سبب فشل صالح." });
                }
                else if (!OrderStatusHelper.IsValidFailureReason(reason))
                {
                    return Json(new { success = false, message = "سبب الفشل غير صالح." });
                }
            }

            if (OrderStatusHelper.IsFailureStatus(orderStatus))
            {
                var missingImageIds = ids
                    .Where(id => id > 0)
                    .Distinct()
                    .Where(id =>
                        !temporaryFailureImagePathByOrderId.ContainsKey(id)
                        && !HasStatusUpdateImageInputForOrder(
                            id,
                            ids.Count == 1,
                            ids.Count == 1 ? failureReasonImageFile : null,
                            ids.Count == 1 ? failureReasonImageBase64 : null))
                    .ToList();

                if (missingImageIds.Any())
                {
                    return Json(new
                    {
                        success = false,
                        message = "صورة سبب الفشل إجبارية لكل طلب محدد. اختاري السبب ثم أضيفي صورة قبل التحديث.",
                        orderIds = missingImageIds
                    });
                }
            }

            // For bulk failure status updates, track orders that need a reason but don't have one
            var ordersWithoutReason = new List<int>();
            var ordersWithoutFailureImage = new List<int>();

            foreach (var order in orders)
            {
                OrderStatusEnum currentStatus = order.OrderStatus;

                if (roleName == "DeliveryCompany" || roleName == "DeliveryRepresentative")
                {
                    var deliveryError = _roleAuthService.ValidateDeliveryRoleStatusChange(currentStatus, orderStatus);
                    if (deliveryError != null)
                        return Json(new { success = false, message = deliveryError });
                }
                else if (currentStatus != OrderStatusEnum.أرشيف_المرجع)
                {
                    if (roleName != "Admin" && roleName != "FollowUpDepartment" && roleName != "ExecutiveDirector")
                    {
                        var validNextStatuses = OrderStatusHelper.GetValidNextStatuses(order.OrderStatus);
                        if (!validNextStatuses.Contains(orderStatus))
                        {
                            return Json(new { success = false, message = "الحالة المطلوبة غير متاحة للتغيير إليها من الحالة الحالية." });
                        }
                    }
                }

                bool isChangingToDelivery = (OrderStatusHelper.IsFailureStatus(currentStatus) ||
                                              currentStatus == OrderStatusEnum.الطلبات_المرجعة ||
                                              currentStatus == OrderStatusEnum.انتظار_المعالجة) &&
                                             (orderStatus == OrderStatusEnum.تم_المعالجة ||
                                              orderStatus == OrderStatusEnum.الطلبات_المؤجلة);

                if (isChangingToDelivery)
                {
                    order.Fixedby = _userManager.GetUserId(User);
                    if (order.FixedOrderDate == null)
                    {
                        order.FixedOrderDate = _timeService.GetIstanbulTimeWithOffset();
                    }
                }

                order.OrderStatus = orderStatus;
                order.LastEditedDate = _timeService.GetIstanbulTimeWithOffset();

                // Determine failure reason for this order (carry-forward if not provided)
                string? orderReason = null;
                if (OrderStatusHelper.IsFailureStatus(orderStatus))
                {
                    if (perOrderReasons != null && perOrderReasons.TryGetValue(order.Id, out var perOrderReason))
                    {
                        orderReason = perOrderReason;
                    }
                    else if (!string.IsNullOrEmpty(reason))
                    {
                        orderReason = reason;
                    }
                    else
                    {
                        // Carry-forward: copy from latest failure-stage history
                        orderReason = await _context.OrderStatusHistories
                            .Where(oh => oh.OrderId == order.Id && oh.Reason != null)
                            .OrderByDescending(oh => oh.Id)
                            .Select(oh => oh.Reason)
                            .FirstOrDefaultAsync();

                        if (orderReason == null)
                        {
                            // This order has no previous failure reason — skip it and report
                            ordersWithoutReason.Add(order.Id);
                            continue;
                        }
                    }
                }

                var failureReasonImageUrl = await SaveFailureReasonImageAsync(
                    order.Id,
                    ids.Count == 1 ? failureReasonImageFile : null,
                    ids.Count == 1 ? failureReasonImageBase64 : null,
                    allowStandardNames: ids.Count == 1);

                if (string.IsNullOrWhiteSpace(failureReasonImageUrl)
                    && temporaryFailureImagePathByOrderId.TryGetValue(order.Id, out var temporaryFailureImagePath))
                {
                    failureReasonImageUrl = temporaryFailureImagePath;
                }

                if (OrderStatusHelper.IsFailureStatus(orderStatus) && string.IsNullOrWhiteSpace(failureReasonImageUrl))
                {
                    ordersWithoutFailureImage.Add(order.Id);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(failureReasonImageUrl)
                    && !OrderStatusHelper.IsFailureStatus(orderStatus)
                    && !string.IsNullOrWhiteSpace(orderReason))
                {
                    failureReasonImageUrl = await GetLatestFailureReasonImageUrlAsync(order.Id);
                }

                var orderHistory = new OrderStatusHistory
                {
                    OrderId = order.Id,
                    Status = orderStatus,
                    CreatedAt = _timeService.GetIstanbulTimeWithOffset(),
                    ApplicationUserId = _userManager.GetUserId(User),
                    Reason = orderReason,
                    FailureReasonImageUrl = failureReasonImageUrl,
                };

                _context.OrderStatusHistories.Add(orderHistory);

                // Broadcast to the UsersExpectDelivery group
                await _hubContext.Clients.Group("UsersExpectDelivery").SendAsync("OrderStatusUpdated", new
                {
                    orderHistory.OrderId,
                    orderHistory.Status,
                    orderHistory.CreatedAt,
                    orderHistory.ApplicationUserId,
                    UserName = orderHistory.User?.UserName ?? "Unknown",
                    StatusPhrase = OrderStatusHelper.GetOrderStatusPhrase(orderStatus),
                    ColorStyle = OrderStatusHelper.StatusColorMapping.GetValueOrDefault(orderStatus, "")
                });

                // Broadcast to the specific delivery company group
                var targetGroup = $"deliveryCompany_{order.DeliveryCompanyId}";
                await _hubContext.Clients.Group(targetGroup).SendAsync("OrderStatusUpdated", new
                {
                    orderHistory.OrderId,
                    orderHistory.Status,
                    orderHistory.CreatedAt,
                    orderHistory.ApplicationUserId,
                    UserName = orderHistory.User?.UserName ?? "Unknown",
                    StatusPhrase = OrderStatusHelper.GetOrderStatusPhrase(orderStatus),
                    ColorStyle = OrderStatusHelper.StatusColorMapping.GetValueOrDefault(orderStatus, "")
                });

                if (OrderStatusHelper.IsFailureStatus(currentStatus) &&
                    !OrderStatusHelper.IsFailureStatus(orderStatus) &&
                    orderStatus != OrderStatusEnum.الطلبات_المرجعة &&
                    orderStatus != OrderStatusEnum.انتظار_المعالجة)
                {
                    foreach (var orderWarehouse in order.OrderWarehouses)
                    {
                        var warehouse = orderWarehouse.Warehouse;
                        warehouse.Amount -= orderWarehouse.Amount;
                        _context.Update(warehouse);
                    }
                }

                if (OrderStatusHelper.ShouldRefundToWarehouse(orderStatus) && !OrderStatusHelper.IsFailureStatus(currentStatus))
                {
                    foreach (var orderWarehouse in order.OrderWarehouses)
                    {
                        var warehouse = orderWarehouse.Warehouse;
                        warehouse.Amount += orderWarehouse.Amount;
                        _context.Update(warehouse);
                    }
                }
            }

            if (ordersWithoutFailureImage.Any())
            {
                return Json(new { success = false, message = "صورة سبب الفشل إجبارية للطلبات التالية.", orderIds = ordersWithoutFailureImage });
            }

            if (ordersWithoutReason.Any())
            {
                return Json(new { success = false, message = "الطلبات التالية تحتاج إلى إضافة سبب الفشل أولاً", orderIds = ordersWithoutReason });
            }

            await _context.SaveChangesAsync();

            foreach (var order in orders)
            {
                var externalOrderId = order.ExternalOrderId;
                if (externalOrderId.HasValue)
                {
                    var updateStatusRequest = new UpdateStatusRequest
                    {
                        NewStatus = orderStatus,
                    };

                    var response = await _restApi.UpdateOrderStatusAsync(externalOrderId.Value, updateStatusRequest);

                    Console.WriteLine($"External order ID: {externalOrderId}, Response: {response}");
                }
            }

            if (ordersWithoutReason.Any())
            {
                return Json(new { success = false, message = "الطلبات التالية تحتاج إلى إضافة سبب الفشل أولاً", orderIds = ordersWithoutReason });
            }

            await DeactivateOrderStatusUpdateSelectionsAsync(ids, saveNow: true);

            return Json(new { success = true, message = "تم تغيير حالة الطلبات بنجاح", orderIds = ids });
        }


        [HttpPost]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment,DeliveryCompany,DeliveryRepresentative")]
        public async Task<IActionResult> AdvanceFailureStatus(List<int> ids, string? reason = null, IFormFile? failureReasonImageFile = null, string? failureReasonImageBase64 = null)
        {
            var orders = await _context.Orders
                .Include(o => o.OrderWarehouses)
                .ThenInclude(ow => ow.Warehouse)
                .Where(o => ids.Contains(o.Id))
                .ToListAsync();

            if (orders == null || !orders.Any())
            {
                return Json(new { success = false, message = "لم يتم العثور على طلبات." });
            }

            // Validate reason if provided
            if (!string.IsNullOrEmpty(reason) && !OrderStatusHelper.IsValidFailureReason(reason))
            {
                return Json(new { success = false, message = "سبب الفشل غير صالح." });
            }

            var ordersWithoutReason = new List<int>();

            foreach (var order in orders)
            {
                var currentStatus = order.OrderStatus;
                var validNextStatuses = OrderStatusHelper.GetValidNextStatuses(currentStatus);
                if (!validNextStatuses.Any())
                {
                    continue;
                }

                var nextStatus = validNextStatuses.First();

                // Determine failure reason: use provided, or carry-forward from previous
                string? orderReason = null;
                if (OrderStatusHelper.IsFailureStatus(nextStatus) || nextStatus == OrderStatusEnum.انتظار_المعالجة)
                {
                    if (!string.IsNullOrEmpty(reason))
                    {
                        orderReason = reason;
                    }
                    else
                    {
                        orderReason = await _context.OrderStatusHistories
                            .Where(oh => oh.OrderId == order.Id && oh.Reason != null)
                            .OrderByDescending(oh => oh.Id)
                            .Select(oh => oh.Reason)
                            .FirstOrDefaultAsync();

                        if (orderReason == null)
                        {
                            ordersWithoutReason.Add(order.Id);
                            continue;
                        }
                    }
                }

                order.OrderStatus = nextStatus;
                order.LastEditedDate = _timeService.GetIstanbulTimeWithOffset();

                var failureReasonImageUrl = await SaveFailureReasonImageAsync(
                    order.Id,
                    ids.Count == 1 ? failureReasonImageFile : null,
                    ids.Count == 1 ? failureReasonImageBase64 : null,
                    allowStandardNames: ids.Count == 1);

                if (string.IsNullOrWhiteSpace(failureReasonImageUrl)
                    && (OrderStatusHelper.IsFailureStatus(nextStatus)
                        || nextStatus == OrderStatusEnum.انتظار_المعالجة
                        || !string.IsNullOrWhiteSpace(orderReason)))
                {
                    failureReasonImageUrl = await GetLatestFailureReasonImageUrlAsync(order.Id);
                }

                var orderHistory = new OrderStatusHistory
                {
                    OrderId = order.Id,
                    Status = nextStatus,
                    CreatedAt = _timeService.GetIstanbulTimeWithOffset(),
                    ApplicationUserId = _userManager.GetUserId(User),
                    Reason = orderReason,
                    FailureReasonImageUrl = failureReasonImageUrl,
                };

                _context.OrderStatusHistories.Add(orderHistory);

                await _hubContext.Clients.Group("UsersExpectDelivery").SendAsync("OrderStatusUpdated", new
                {
                    orderHistory.OrderId,
                    orderHistory.Status,
                    orderHistory.CreatedAt,
                    orderHistory.ApplicationUserId,
                    UserName = orderHistory.User?.UserName ?? "Unknown",
                    StatusPhrase = OrderStatusHelper.GetOrderStatusPhrase(nextStatus),
                    ColorStyle = OrderStatusHelper.StatusColorMapping.GetValueOrDefault(nextStatus, "")
                });

                var targetGroup = $"deliveryCompany_{order.DeliveryCompanyId}";
                await _hubContext.Clients.Group(targetGroup).SendAsync("OrderStatusUpdated", new
                {
                    orderHistory.OrderId,
                    orderHistory.Status,
                    orderHistory.CreatedAt,
                    orderHistory.ApplicationUserId,
                    UserName = orderHistory.User?.UserName ?? "Unknown",
                    StatusPhrase = OrderStatusHelper.GetOrderStatusPhrase(nextStatus),
                    ColorStyle = OrderStatusHelper.StatusColorMapping.GetValueOrDefault(nextStatus, "")
                });

                if (OrderStatusHelper.IsFailureStatus(currentStatus) &&
                    !OrderStatusHelper.IsFailureStatus(nextStatus) &&
                    nextStatus != OrderStatusEnum.الطلبات_المرجعة &&
                    nextStatus != OrderStatusEnum.انتظار_المعالجة)
                {
                    foreach (var orderWarehouse in order.OrderWarehouses)
                    {
                        var warehouse = orderWarehouse.Warehouse;
                        warehouse.Amount -= orderWarehouse.Amount;
                        _context.Update(warehouse);
                    }
                }

                if (OrderStatusHelper.ShouldRefundToWarehouse(nextStatus) && !OrderStatusHelper.IsFailureStatus(currentStatus))
                {
                    foreach (var orderWarehouse in order.OrderWarehouses)
                    {
                        var warehouse = orderWarehouse.Warehouse;
                        warehouse.Amount += orderWarehouse.Amount;
                        _context.Update(warehouse);
                    }
                }
            }

            await _context.SaveChangesAsync();

            foreach (var order in orders)
            {
                var externalOrderId = order.ExternalOrderId;
                if (externalOrderId.HasValue)
                {
                    var updateStatusRequest = new UpdateStatusRequest
                    {
                        NewStatus = order.OrderStatus,
                        Reason = reason,
                    };
                    await _restApi.UpdateOrderStatusAsync(externalOrderId.Value, updateStatusRequest);
                }
            }

            if (ordersWithoutReason.Any())
            {
                return Json(new { success = false, message = "الطلبات التالية تحتاج إلى إضافة سبب الفشل أولاً", orderIds = ordersWithoutReason });
            }

            return Json(new { success = true, message = "تم التقدم للحالة التالية بنجاح" });
        }


        [HttpPost]
        [Authorize(Roles = "Admin,Accountant,FollowUpDepartment,ExecutiveDirector,DeliveryCompany,DeliveryRepresentative")]
        public async Task<IActionResult> MarkAsPrepared(List<int> ids)
        {
            var ordersToMarkAsPrepared = await _context.Orders
                .Include(a => a.DeliveryCompany)
                .Where(o => ids.Contains(o.Id))
                .ToListAsync();

            if (!ordersToMarkAsPrepared.Any())
            {
                return Json(new { success = false, message = "No orders found to update." });
            }

            string currentTime = _timeService.GetIstanbulTimeWithOffset().ToString("yyyy-MM-dd");
            string userId = _userManager.GetUserId(User);

            // Create order status histories
            var orderHistories = ordersToMarkAsPrepared.Select(order => new OrderStatusHistory
            {
                OrderId = order.Id,
                Status = OrderStatusEnum.تم_التجهيز,
                CreatedAt = _timeService.GetIstanbulTimeWithOffset(),
                ApplicationUserId = userId
            }).ToList();

            _context.OrderStatusHistories.AddRange(orderHistories);

            // Save changes related to order histories first
            await _context.SaveChangesAsync();

            // Create and save OrderReport
            var totalAmount = ordersToMarkAsPrepared.Sum(order => order.TotalPrice); // Assuming TotalPrice exists in the Order model
            var deliveryCompany = ordersToMarkAsPrepared.FirstOrDefault()?.DeliveryCompany;
            var country = ordersToMarkAsPrepared.FirstOrDefault()?.Country; // Assuming Country exists in the Order model

            // Create the OrderReport object
            var orderReport = new OrderReport
            {
                GeneratedTime = _timeService.GetIstanbulTimeWithOffset(),
                TotalAmount = totalAmount,
                Country = country,
                DeliveryCompanyId = deliveryCompany?.Id,
                DeliveryCompany = deliveryCompany,
                OrderStatus = OrderStatusEnum.تم_التجهيز,
                Orders = ordersToMarkAsPrepared,
            };

            _context.OrderReports.Add(orderReport);
            var orderReportCreationResult = await _context.SaveChangesAsync();

            // Ensure OrderReport was successfully created
            if (orderReportCreationResult == 0 || orderReport.Id == 0)
            {
                return Json(new { success = false, message = "Failed to create the order report. Status update aborted." });
            }

            // After saving the order report, update the OrderReportOrders with the correct OrderReportId
            var orderReportOrders = ordersToMarkAsPrepared.Select(order => new OrderReportOrder
            {
                OrderId = order.Id,
                OrderReportId = orderReport.Id // Now set the correct OrderReportId after saving
            }).ToList();

            // Add the OrderReportOrder entities to the context
            _context.OrderReportOrders.AddRange(orderReportOrders);
            await _context.SaveChangesAsync();

            // Update order statuses only after successfully creating the report
            await _context.Orders
                .Where(o => ordersToMarkAsPrepared.Select(order => order.Id).Contains(o.Id))
                .UpdateAsync(o => new Order { OrderStatus = OrderStatusEnum.تم_التجهيز, LastEditedDate = _timeService.GetIstanbulTimeWithOffset() });

            // Broadcast the status update to SignalR groups
            foreach (var orderHistory in orderHistories)
            {
                var orderData = new
                {
                    orderHistory.OrderId,
                    orderHistory.Status,
                    orderHistory.CreatedAt,
                    orderHistory.ApplicationUserId,
                    UserName = orderHistory.User?.UserName ?? "Unknown",
                    StatusPhrase = OrderStatusHelper.GetOrderStatusPhrase(orderHistory.Status ?? OrderStatusEnum.تم_التجهيز),
                    ColorStyle = OrderStatusHelper.StatusColorMapping.GetValueOrDefault(orderHistory.Status ?? OrderStatusEnum.تم_التجهيز, "")
                };

                // Broadcast to the UsersExpectDelivery group
                await _hubContext.Clients.Group("UsersExpectDelivery").SendAsync("OrderStatusUpdated", orderData);

                // Broadcast to the specific delivery company group
                var targetGroup = $"deliveryCompany_{ordersToMarkAsPrepared.FirstOrDefault(o => o.Id == orderHistory.OrderId)?.DeliveryCompanyId}";
                if (targetGroup != null)
                {
                    await _hubContext.Clients.Group(targetGroup).SendAsync("OrderStatusUpdated", orderData);
                }
            }

            return Json(new { success = true, message = "تم تغيير حالة الطلبات بنجاح", orderReportId = orderReport.Id });
        }


        // update delviery company in details page
        [HttpPost]
        [Authorize(Roles = "Admin,FollowUpDepartment,ExecutiveDirector,CallCenter")]
        public async Task<IActionResult> TransferOrderWarehouse(int[] orderIds, int newDeliveryCompanyId)
        {
            foreach (var orderId in orderIds)
            {
                var order = await _context.Orders
                    .Include(o => o.OrderWarehouses)
                    .ThenInclude(ow => ow.Warehouse)
                    .Include(o => o.DeliveryCompany)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null)
                {
                    return NotFound($"Order {orderId} not found");
                }

                var oldDeliveryCompany = order.DeliveryCompany;
                var newDeliveryCompany = await _context.DeliveryCompanies.FindAsync(newDeliveryCompanyId);

                if (newDeliveryCompany == null)
                {
                    return NotFound($"Delivery company {newDeliveryCompanyId} not found");
                }

                // Get the delivery price for the new company based on order's country and state (city)
                var deliveryPrice = await _context.DeliveryCompanyPrices
                    .FirstOrDefaultAsync(dcp => dcp.DeliveryCompanyId == newDeliveryCompanyId
                                            && dcp.Country == order.Country
                                            && (dcp.City == null || dcp.City == order.State || order.State == null));

                decimal newDeliveryPrice = deliveryPrice?.Price ?? 0; // Default to 0 if no price found

                foreach (var orderWarehouse in order.OrderWarehouses.ToList())
                {
                    var currentWarehouse = orderWarehouse.Warehouse;

                    if (currentWarehouse.DeliveryCompanyId != order.DeliveryCompanyId)
                    {
                        continue;
                    }

                    var newWarehouse = await _context.Warehouses
                        .FirstOrDefaultAsync(w => w.DeliveryCompanyId == newDeliveryCompanyId
                                              && w.SubWarehouseId == currentWarehouse.SubWarehouseId);

                    if (newWarehouse == null)
                    {
                        newWarehouse = new Warehouse
                        {
                            Name = currentWarehouse.Name,
                            Price = currentWarehouse.Price,
                            UnchangingAmount = currentWarehouse.UnchangingAmount,
                            Amount = currentWarehouse.Amount,
                            DeliveryCompanyId = newDeliveryCompanyId,
                            ManufacturingCompanyId = currentWarehouse.ManufacturingCompanyId,
                            DateAdded = DateTime.Now,
                            DateUpdated = DateTime.Now,
                            MainWarehouseId = currentWarehouse.MainWarehouseId,
                            Countries = order.Country,
                            City = order.State,
                            SubWarehouseId = currentWarehouse.SubWarehouseId,
                        };

                        _context.Warehouses.Add(newWarehouse);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        newWarehouse.Amount += currentWarehouse.Amount;
                        _context.Entry(newWarehouse).Property(w => w.Amount).IsModified = true;
                    }

                    currentWarehouse.Amount = 0;
                    _context.Entry(currentWarehouse).Property(w => w.Amount).IsModified = true;
                    await _context.SaveChangesAsync();

                    var newOrderWarehouse = new OrderWarehouse
                    {
                        OrderId = orderWarehouse.OrderId,
                        WarehouseId = newWarehouse.Id,
                        Amount = orderWarehouse.Amount
                    };

                    _context.OrderWarehouses.Add(newOrderWarehouse);
                    _context.OrderWarehouses.Remove(orderWarehouse);
                }

                // Update both delivery company and delivery price
                order.DeliveryCompanyId = newDeliveryCompanyId;
                order.DeliveryPrice = newDeliveryPrice; // This was missing in your original code
                await _context.SaveChangesAsync();

                // Notify clients about the updated delivery company and price
                var deliveryCompanyData = new
                {
                    orderId = order.Id,
                    deliveryCompanyName = newDeliveryCompany.Name,
                    deliveryCompanyLogoUrl = newDeliveryCompany.ImageUrl,
                    deliveryPrice = newDeliveryPrice
                };

                await _hubContext.Clients.All.SendAsync("UpdateOrderDeliveryCompany", deliveryCompanyData);
            }

            return Json(new { success = true });
        }


        // Bulk-assign a delegate employee (تعيين بالنيابة) to a list of failed/incomplete orders.
        // Only sets DelegateEmployeeId — does not change order status, bonus counts, or anything else.
        [HttpPost]
        [Authorize(Roles = "Admin,FollowUpDepartment,ExecutiveDirector")]
        public async Task<IActionResult> AssignOrdersToDelegate(int[] orderIds, string delegateEmployeeId)
        {
            if (orderIds == null || orderIds.Length == 0)
                return Json(new { success = false, message = "لم يتم تحديد أي طلبات" });

            if (string.IsNullOrWhiteSpace(delegateEmployeeId))
                return Json(new { success = false, message = "لم يتم تحديد الموظف" });

            var userExists = await _context.Users.AnyAsync(u => u.Id == delegateEmployeeId);
            if (!userExists)
                return Json(new { success = false, message = "الموظف غير موجود" });

            var orders = await _context.Orders
                .Where(o => orderIds.Contains(o.Id))
                .ToListAsync();

            foreach (var order in orders)
                order.DelegateEmployeeId = delegateEmployeeId;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }


        // Hide order status history
        [HttpPost]
        [Route("Order/DeleteOrderStatusHistoryAsync")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteOrderStatusHistoryAsync(int id)
        {
            Console.WriteLine($"Attempting to delete order status history with ID: {id}");

            var orderStatusHistory = await _context.OrderStatusHistories.FindAsync(id);
            if (orderStatusHistory == null)
            {
                Console.WriteLine($"Order status history not found for ID: {id}");
                return Json(new { success = false, message = "Order status history not found" });
            }

            // Retrieve all status histories for the order, ordered by CreatedAt
            var orderStatusHistories = await _context.OrderStatusHistories
                                                     .Where(x => x.OrderId == orderStatusHistory.OrderId)
                                                     .OrderBy(x => x.CreatedAt)
                                                     .ToListAsync();

            // Get the last two status histories
            var lastStatusHistory = orderStatusHistories.LastOrDefault();
            var secondLastStatusHistory = orderStatusHistories.Count > 1 ? orderStatusHistories[^2] : null; // Second-to-last status

            // Check if we're deleting the last status
            if (orderStatusHistory.Id == lastStatusHistory?.Id)
            {
                // If it's the last status and there's a second-to-last status, update the order's status
                if (secondLastStatusHistory != null)
                {
                    var order = await _context.Orders.FindAsync(orderStatusHistory.OrderId);
                    if (order != null && secondLastStatusHistory.Status.HasValue)
                    {
                        // Set the order's status to the second-to-last status with explicit cast
                        order.OrderStatus = (OrderStatusEnum)secondLastStatusHistory.Status;
                        _context.Update(order);
                        await _context.SaveChangesAsync();

                        Console.WriteLine($"Order status updated to: {secondLastStatusHistory.Status}");
                    }
                }
            }

            // Delete the status history
            _context.OrderStatusHistories.Remove(orderStatusHistory);
            await _context.SaveChangesAsync();

            // Prepare data to send to the client
            var deleteOrderStatusData = new
            {
                historyId = id,
                isDeleted = true, // Always true since we're deleting the row
                isHidden = false  // Optional: retain isHidden logic if needed for admins
            };

            // Broadcast the deletion update to all clients using SignalR
            await _hubContext.Clients.All.SendAsync("OrderStatusHistoryDelete", deleteOrderStatusData);

            return Json(new { success = true });
        }


        [HttpPost]
        [Route("Order/DeleteOrdersStatusHistoryAsync")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteOrdersStatusHistoryAsync(string ids)
        {
            if (string.IsNullOrEmpty(ids))
            {
                Console.WriteLine("No Order IDs provided");
                return Json(new { success = false, message = "No Order IDs provided" });
            }

            // Parse the comma-separated Order IDs
            var orderIds = ids.Split(',')
                             .Select(id => int.TryParse(id, out var num) ? num : -1)
                             .Where(id => id > 0)
                             .ToList();

            if (!orderIds.Any())
            {
                Console.WriteLine("Invalid Order IDs format");
                return Json(new { success = false, message = "Invalid Order IDs format" });
            }

            Console.WriteLine($"Attempting to delete last status histories for orders: {string.Join(", ", orderIds)}");

            var deletedStatusHistoryIds = new List<int>();
            var deleteOrderStatusDataList = new List<object>();

            foreach (var orderId in orderIds)
            {
                // Get all status histories for this order, ordered by CreatedAt
                var orderStatusHistories = await _context.OrderStatusHistories
                    .Where(x => x.OrderId == orderId)
                    .OrderBy(x => x.CreatedAt)
                    .ToListAsync();

                if (!orderStatusHistories.Any())
                {
                    Console.WriteLine($"No status histories found for Order ID: {orderId}");
                    continue;
                }

                // Get the last status history
                var lastStatusHistory = orderStatusHistories.LastOrDefault();
                if (lastStatusHistory == null) continue;

                // Get the second-to-last status (if exists)
                var secondLastStatusHistory = orderStatusHistories.Count > 1 ? orderStatusHistories[^2] : null;

                // Update the order's status if we're deleting the last status
                if (secondLastStatusHistory != null)
                {
                    var order = await _context.Orders.FindAsync(orderId);
                    if (order != null && secondLastStatusHistory.Status.HasValue)
                    {
                        order.OrderStatus = (OrderStatusEnum)secondLastStatusHistory.Status;
                        _context.Update(order);
                        Console.WriteLine($"Order {orderId} status updated to: {secondLastStatusHistory.Status}");
                    }
                }

                // Mark the last status history for deletion
                _context.OrderStatusHistories.Remove(lastStatusHistory);
                deletedStatusHistoryIds.Add(lastStatusHistory.Id);

                // Prepare data for SignalR
                deleteOrderStatusDataList.Add(new
                {
                    historyId = lastStatusHistory.Id,
                    isDeleted = true,
                    isHidden = false
                });
            }

            // Save all changes at once
            await _context.SaveChangesAsync();

            // Broadcast all deletions to clients using SignalR
            await _hubContext.Clients.All.SendAsync("OrderStatusHistoryDelete", deleteOrderStatusDataList);

            return Json(new
            {
                success = true,
                deletedCount = deletedStatusHistoryIds.Count,
                deletedIds = deletedStatusHistoryIds
            });
        }

        //فواتير التجهيز  in orders page 
        [HttpGet]
        [Authorize(Roles = "Admin,Accountant,DeliveryCompany,DeliveryRepresentative")]
        public async Task<IActionResult> OrdersInvoice(int? storeId, int? page, int? pagesize, int? deliveryCompanyIdFilter, string? search = null, DateTime? startDay = null, DateTime? endDay = null, Common.Countries? CountryId = null)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Get logged-in user's ID
            var isDeliveryCompanyRole = User.IsInRole("DeliveryCompany") || User.IsInRole("DeliveryRepresentative");
            int? deliveryCompanyId = null;

            if (isDeliveryCompanyRole)
            {
                deliveryCompanyId = _context.DeliveryCompanies
                    .Where(dc => dc.UserId == currentUserId)
                    .Select(dc => dc.Id)
                    .FirstOrDefault();
            }

            var orderReportsQuery = _context.OrderReports
                .Where(or => or.DeliveryCompanyId != null && or.OrderStatus == OrderStatusEnum.تم_التجهيز);

            if (isDeliveryCompanyRole && deliveryCompanyId.HasValue)
            {
                orderReportsQuery = orderReportsQuery.Where(or => or.DeliveryCompanyId == deliveryCompanyId.Value);
            }
            if (storeId.HasValue)
            {
                orderReportsQuery = orderReportsQuery.Where(a => a.Orders.First().ManufacturingCompanyId == storeId);
            }

            if (deliveryCompanyIdFilter.HasValue)
            {
                orderReportsQuery = orderReportsQuery.Where(or => or.DeliveryCompanyId == deliveryCompanyIdFilter.Value);
            }
            if (CountryId.HasValue)
            {
                orderReportsQuery = orderReportsQuery.Where(or => or.Country == CountryId.Value);
            }

            if (startDay != null && endDay != null)
            {
                orderReportsQuery = orderReportsQuery.Where(or => or.GeneratedTime >= startDay && or.GeneratedTime <= endDay);
            }

            if (!string.IsNullOrEmpty(search))
            {
                // Try to parse the search term as an integer for Id
                if (int.TryParse(search, out int searchId))
                {
                    orderReportsQuery = orderReportsQuery.Where(or => or.Id == searchId);
                }
                else if (DateTime.TryParse(search, out DateTime searchDate))
                {
                    // Filter by GeneratedTime within a date range starting with searchDate
                    var nextDay = searchDate.AddDays(1);
                    orderReportsQuery = orderReportsQuery.Where(or => or.GeneratedTime >= searchDate && or.GeneratedTime < nextDay);
                }
                else
                {
                    // If not a country, Id, or DateTime, filter by other criteria
                    orderReportsQuery = orderReportsQuery.Where(or =>
                        or.DeliveryCompany.Name.Contains(search));

                }
            }

            var orderReports = await orderReportsQuery
                .Include(or => or.DeliveryCompany)
                .Select(or => new OrderReportViewModel
                {
                    Id = or.Id,
                    GeneratedTime = or.GeneratedTime.ToString("yyyy-MM-dd"), // Corrected format string
                    TotalAmount = _decimalFormattingService.DecimalFormat(or.TotalAmount),
                    Country = or.Country.ToString(), // Assuming Country is an enum
                    DeliveryCompanyName = or.DeliveryCompany.Name,
                })
                .ToListAsync();

            // Apply pagination
            page = page ?? 1; // Default page number
            pagesize = pagesize ?? 10; // Default page size
            var paginatedOrderReports = orderReports
                .OrderByDescending(or => or.GeneratedTime)
                .Skip((page.Value - 1) * pagesize.Value)
                .Take(pagesize.Value)
                .ToList();

            var paginationViewModel = new PaginationViewModel<OrderReportViewModel>
            {
                Items = paginatedOrderReports,
                CurrentPage = page.Value,
                PageSize = pagesize.Value,
                TotalItems = orderReports.Count // Total count before pagination
            };

            return View(paginationViewModel);
        }

        // generate order report 
        private async Task GenerateOrderReport(List<Order> orders, OrderStatusEnum orderStatus)
        {
            string countryName = orders.FirstOrDefault()?.Country.ToString();
            decimal totalAmountValue = orders.Sum(o => o.TotalPrice);
            string currencyCode = Common.GetCurrencyByCountryName(countryName);
            string totalAmount = $"{totalAmountValue:N2}\u200E {currencyCode}";
            var totalOrderNumber = orders.Count.ToString();

            var filteredOrders = orders
                .Where(order => order.DeliveryCompany != null)
                .ToList();

            var deliveryCompanyName = filteredOrders.FirstOrDefault()?.DeliveryCompany?.Name;
            var deliveryCompanyAddress = filteredOrders.FirstOrDefault()?.DeliveryCompany?.Address;
            var deliveryCompanyPhoneNumber = filteredOrders.FirstOrDefault()?.DeliveryCompany?.PhoneNumber;

            var headers = new List<string> { " كود الشحنة", "التاريخ", "اسم العميل", "رقم الهاتف", "المدينة", "المبلغ الإجمالي ", "سعر التوصيل", "صافي المبلغ" };
            var valueSelectors = new List<Func<Order, string>> {
                o => o.Id.ToString(),
                o => o.CreatedDate.ToString("yyyy-MM-dd"),
                o => o.CustomerName,
                o => o.TelephoneNumber,
                o => o.State,
                o => o.TotalPrice.ToString()
            };

            List<int> orderIds = orders.Select(order => order.Id).ToList();

            // Calculate RemainingValue and totalDeliveryPrice directly using the DeliveryPrice from orders
            decimal remainingValue = orders.Sum(order => order.TotalPrice - order.DeliveryPrice);
            decimal totalDeliveryPrice = orders.Sum(order => order.DeliveryPrice);


            // Check if an OrderReport already exists for these orders
            var existingReport = await _context.OrderReports
                .FirstOrDefaultAsync(r => r.OrderReportOrders.Any(oro => orderIds.Contains(oro.OrderId)));

            if (existingReport != null)
            {
                // Update existing report
                existingReport.GeneratedTime = _timeService.GetIstanbulTimeWithOffset();
                existingReport.TotalAmount = orders.Sum(o => o.TotalPrice);
                existingReport.Country = orders.FirstOrDefault()?.Country;
                existingReport.DeliveryCompanyId = orders.FirstOrDefault()?.DeliveryCompanyId;
                existingReport.DeliveryCompany = orders.FirstOrDefault()?.DeliveryCompany;
                existingReport.OrderStatus = orderStatus;

                _context.OrderReports.Update(existingReport);
                await _context.SaveChangesAsync();

                // Ensure all orders are associated with this report
                var orderReportOrders = orders.Select(order => new OrderReportOrder
                {
                    OrderReportId = existingReport.Id,
                    OrderId = order.Id
                }).ToList();

                _context.OrderReportOrders.AddRange(orderReportOrders);
            }
            else
            {
                // Create a new report
                var orderReport = new OrderReport
                {
                    GeneratedTime = _timeService.GetIstanbulTimeWithOffset(),
                    TotalAmount = orders.Sum(o => o.TotalPrice),
                    Country = orders.FirstOrDefault()?.Country,
                    DeliveryCompanyId = orders.FirstOrDefault()?.DeliveryCompanyId,
                    DeliveryCompany = orders.FirstOrDefault()?.DeliveryCompany,
                    OrderStatus = orderStatus
                };

                _context.OrderReports.Add(orderReport);
                await _context.SaveChangesAsync();

                // Associate the orders with the new report
                var orderReportOrders = orders.Select(order => new OrderReportOrder
                {
                    OrderReportId = orderReport.Id,
                    OrderId = order.Id
                }).ToList();

                _context.OrderReportOrders.AddRange(orderReportOrders);
            }

            await _context.SaveChangesAsync();

            var reportId = (existingReport != null) ? existingReport.Id.ToString() : _context.OrderReports.OrderByDescending(r => r.Id).FirstOrDefault().Id.ToString();
            string deliveryAmount = $"{totalDeliveryPrice:N2}\u200E {currencyCode}";
            string remaningAmount = $"{remainingValue:N2}\u200E {currencyCode}";

            var pdfBytes = await _reportGenerator.CreatePdfReportAsync(
                orders, headers, valueSelectors,
                deliveryCompanyName, deliveryCompanyAddress, deliveryCompanyPhoneNumber,
                _timeService.GetIstanbulTimeWithOffset().ToString("yyyy-MM-dd"), reportId, totalAmount, deliveryAmount, remaningAmount, totalOrderNumber, countryName);

            Response.Headers.Add("Content-Disposition", "inline; filename=OrdersReport.pdf");
            await Response.Body.WriteAsync(pdfBytes, 0, pdfBytes.Length); // Ensure the response is written
        }

        internal static readonly Dictionary<Common.Countries, string> _countryDialCodes = new()
        {
            { Common.Countries.العراق,       "964" },
            { Common.Countries.الإمارات,     "971" },
            { Common.Countries.قطر,          "974" },
            { Common.Countries.ليبيا,        "218" },
            { Common.Countries.سلطنة_عمان,   "968" },
            { Common.Countries.فلسطين,       "970" },
            { Common.Countries.تركيا,        "90"  },
            { Common.Countries.الأردن,       "962" },
            { Common.Countries.الكويت,       "965" },
            { Common.Countries.البحرين,      "973" },
            { Common.Countries.السعودية,     "966" },
            { Common.Countries.تونس,         "216" },
            { Common.Countries.المغرب,       "212" },
            { Common.Countries.الجزائر,      "213" },
            { Common.Countries.لبنان,        "961" },
            { Common.Countries.مصر,          "20"  },
        };

        // Countries whose local subscriber format does NOT use a leading 0.
        // For these, stripping an 00xxx/+xxx prefix must NOT prepend "0" — doing so creates malformed
        // values that the per-country edit-form validator (COUNTRY_PHONE_CONFIG in _EditOrder.cshtml) rejects.
        private static readonly HashSet<Common.Countries> _noLeadingZeroLocalCountries = new()
        {
            Common.Countries.قطر,
            Common.Countries.سلطنة_عمان,
            Common.Countries.البحرين,
            Common.Countries.تونس,
            Common.Countries.الكويت,
        };

        // Mirrors the JS validatePhone cleaning: Arabic digits → ASCII, strip invisible chars/dashes/spaces, normalize 00xxx/+xxx → local form
        internal static string NormalizePhone(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw;

            // Arabic-Indic and Eastern Arabic-Indic digits → ASCII
            var sb = new System.Text.StringBuilder(raw.Length);
            foreach (var c in raw)
            {
                if (c >= '٠' && c <= '٩') sb.Append((char)(c - '٠' + '0'));
                else if (c >= '۰' && c <= '۹') sb.Append((char)(c - '۰' + '0'));
                else sb.Append(c);
            }

            var cleaned = sb.ToString()
                .Replace("⁦", "").Replace("⁧", "").Replace("⁨", "").Replace("⁩", "") // LTR/RTL isolates
                .Replace("-", "")
                .Replace(" ", "");

            // 00xxx… → local (with or without leading 0 depending on country convention)
            if (cleaned.StartsWith("00") && cleaned.Length > 4)
            {
                foreach (var kvp in _countryDialCodes.OrderByDescending(c => c.Value.Length))
                {
                    var prefix = "00" + kvp.Value;
                    if (cleaned.StartsWith(prefix))
                    {
                        var local = cleaned[prefix.Length..];
                        return _noLeadingZeroLocalCountries.Contains(kvp.Key) ? local : "0" + local;
                    }
                }
            }

            // +xxx… → local
            if (cleaned.StartsWith("+") && cleaned.Length > 4)
            {
                foreach (var kvp in _countryDialCodes.OrderByDescending(c => c.Value.Length))
                {
                    var prefix = "+" + kvp.Value;
                    if (cleaned.StartsWith(prefix))
                    {
                        var local = cleaned[prefix.Length..];
                        return _noLeadingZeroLocalCountries.Contains(kvp.Key) ? local : "0" + local;
                    }
                }
            }

            return cleaned;
        }

        private async Task LogFraudAttemptAsync(OrderViewModel viewModel, string matchedField, string matchedDigits, int? existingOrderId, string userId, DateTime attemptedAt)
        {
            _context.FraudAttemptLogs.Add(new FraudAttemptLog
            {
                OrderTelephoneNumber = viewModel.TelephoneNumber,
                OrderSecondTelephoneNumber = viewModel.SecondTelephoneNumber,
                MatchedField = matchedField,
                MatchedDigits = matchedDigits,
                ExistingOrderId = existingOrderId,
                ManufacturingCompanyId = viewModel.ManufacturingCompanyId,
                AttemptedByUserId = userId,
                AttemptedAt = attemptedAt,
                SubmittedCustomerName = viewModel.CustomerName,
                SubmittedAddress = viewModel.Address,
                SubmittedNotes = viewModel.Notes,
                SubmittedSourceName = viewModel.SourceName,
                SubmittedChatUrl = viewModel.chatUrl,
            });
            await _context.SaveChangesAsync();
        }

        // Pulls every contiguous digit-run of at least minLength digits out of free-text input,
        // after normalizing Arabic-Indic / Eastern Arabic-Indic digits to ASCII so smuggled phones
        // typed in Arabic numerals don't slip past the scan.
        internal static IEnumerable<string> ExtractDigitRuns(string? raw, int minLength)
        {
            if (string.IsNullOrWhiteSpace(raw)) yield break;

            var sb = new System.Text.StringBuilder(raw.Length);
            foreach (var c in raw)
            {
                if (c >= '٠' && c <= '٩') sb.Append((char)(c - '٠' + '0'));
                else if (c >= '۰' && c <= '۹') sb.Append((char)(c - '۰' + '0'));
                else sb.Append(c);
            }
            var text = sb.ToString();

            var run = new System.Text.StringBuilder();
            foreach (var c in text)
            {
                if (c >= '0' && c <= '9') run.Append(c);
                else
                {
                    if (run.Length >= minLength) yield return run.ToString();
                    run.Clear();
                }
            }
            if (run.Length >= minLength) yield return run.ToString();
        }

        // Returns the last `length` digits of a NormalizePhone'd value, or null if too short.
        internal static string? SuffixOrNull(string? phone, int length)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;
            var normalized = NormalizePhone(phone);
            var digits = new string(normalized.Where(char.IsDigit).ToArray());
            if (digits.Length < length) return null;
            return digits[^length..];
        }


        // Quick report endpoint used by the three status update pages.
        // It avoids the 404 from /OrderPosts/Create on these pages and writes to the same OrderPosts tables dynamically.
        [HttpPost]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment,CallCenter")]
        [Route("/Order/QuickReportCreate")]
        public async Task<IActionResult> QuickReportCreate(
            [FromForm] string? orderCode = null,
            [FromForm] string? orderId = null,
            [FromForm] int type = 0,
            [FromForm] string? body = null,
            [FromForm] string? reason = null,
            [FromForm] List<IFormFile>? images = null)
        {
            var submittedCodeRaw = (orderCode ?? orderId ?? string.Empty).Trim();
            var submittedCode = NormalizeQuickReportOrderCode(submittedCodeRaw);

            if (string.IsNullOrWhiteSpace(submittedCode))
            {
                return BadRequest(new { success = false, message = "اكتب كود الطلب" });
            }

            if (!int.TryParse(submittedCode, out var submittedNumber) || submittedNumber <= 0)
            {
                return BadRequest(new { success = false, message = "كود الطلب غير صحيح" });
            }

            var text = string.IsNullOrWhiteSpace(body) ? reason : body;
            text = (text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(text) && (images == null || !images.Any(file => file != null && file.Length > 0)))
            {
                return BadRequest(new { success = false, message = "اكتب سبب البلاغ" });
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            IQueryable<Order> orderQuery = _context.Orders
                .AsNoTracking()
                .Where(order => !order.IsHidden &&
                    (order.Id == submittedNumber || (order.ExternalOrderId.HasValue && order.ExternalOrderId.Value == submittedNumber)));

            if (User.IsInRole("FollowUpDepartment"))
            {
                orderQuery = orderQuery.Where(order =>
                    order.ManufacturingCompany.EmployeeManufacturingCompanies.Any(access =>
                        access.ApplicationUserId == currentUserId &&
                        access.CanSeeManufacturingCompany));
            }

            if (User.IsInRole("CallCenter"))
            {
                orderQuery = orderQuery.Where(order => order.ApplicationUserId == currentUserId);
            }

            var order = await orderQuery
                .Select(o => new { o.Id })
                .FirstOrDefaultAsync();

            if (order == null)
            {
                return BadRequest(new { success = false, message = "الطلب غير موجود أو ليس لديك صلاحية عليه" });
            }

            var realOrderId = order.Id;
            var now = _timeService.GetIstanbulTimeWithOffset();
            var uploadedImageUrls = new List<string>();

            if (images != null)
            {
                foreach (var image in images.Where(file => file != null && file.Length > 0))
                {
                    if (!string.IsNullOrWhiteSpace(image.ContentType)
                        && !image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var uploaded = await _fileUploadService.UploadFileAsync(image, $"images/order-posts/order-{realOrderId}");
                    if (!string.IsNullOrWhiteSpace(uploaded))
                    {
                        uploadedImageUrls.Add("/" + uploaded.TrimStart('/'));
                    }
                }
            }

            try
            {
                var postId = await InsertQuickReportPostAsync(realOrderId, type, text, currentUserId, now, uploadedImageUrls.FirstOrDefault());

                if (postId <= 0)
                {
                    return BadRequest(new { success = false, message = "تعذر حفظ البلاغ داخل قاعدة البيانات" });
                }

                if (uploadedImageUrls.Any())
                {
                    await InsertQuickReportPostImagesAsync(postId, uploadedImageUrls, currentUserId, now);
                }

                await _hubContext.Clients.Group("OrderPostListeners").SendAsync("newOrderPost", realOrderId, type);
                await _hubContext.Clients.All.SendAsync("newOrderPost", realOrderId, type);

                return Json(new
                {
                    success = true,
                    id = postId,
                    orderId = realOrderId,
                    submittedCode,
                    type,
                    body = text,
                    images = uploadedImageUrls
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("QuickReportCreate error: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
                return StatusCode(500, new { success = false, message = "فشل إرسال البلاغ: " + ex.Message });
            }
        }


        [HttpGet]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment,CallCenter")]
        [Route("/Order/QuickReportList")]
        public async Task<IActionResult> QuickReportList([FromQuery] int? type = null)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Unauthorized(new { success = false, message = "انتهت الجلسة، سجلي الدخول مرة أخرى" });
            }

            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var currentUserDisplayName = currentUser?.Name ?? currentUser?.UserName ?? User.Identity?.Name ?? "الموظف";

                var table = await FindExistingTableAsync("OrderPosts", "OrderPost");
                if (table == null)
                {
                    return Json(new
                    {
                        success = true,
                        reports = Array.Empty<object>(),
                        problemCount = 0,
                        editCount = 0,
                        totalCount = 0
                    });
                }

                var columns = await GetTableColumnsAsync(table.Value.Schema, table.Value.Name);
                var idColumn = FindFirstExistingColumn(columns, new[] { "Id", "OrderPostId", "OrderPostID", "PostId", "PostID" });
                var orderIdColumn = FindFirstExistingColumn(columns, new[] { "OrderId", "OrderID" });
                var typeColumn = FindFirstExistingColumn(columns, new[] { "Type", "PostType", "OrderPostType", "OrderPostTypeId", "OrderPostTypeID" });
                var bodyColumn = FindFirstExistingColumn(columns, new[] { "Body", "Text", "Content", "Description", "Message", "Note", "Reason", "Title" });
                var createdColumn = FindFirstExistingColumn(columns, new[] { "CreatedAt", "CreatedOn", "CreatedDate", "DateCreated", "AddedAt", "AddedDate" });
                var userColumn = FindFirstExistingColumn(columns, QuickReportUserColumnCandidates());
                var imageColumn = FindFirstExistingColumn(columns, new[] { "ImageUrl", "ImagePath", "PhotoUrl", "PhotoPath", "PictureUrl", "FilePath", "AttachmentUrl" });

                if (string.IsNullOrWhiteSpace(idColumn) || string.IsNullOrWhiteSpace(userColumn))
                {
                    return Json(new
                    {
                        success = true,
                        reports = Array.Empty<object>(),
                        problemCount = 0,
                        editCount = 0,
                        totalCount = 0
                    });
                }

                var whereParts = new List<string>
                {
                    $"{QuoteSqlName(userColumn)} = @currentUserId"
                };

                AddBooleanColumnFilterIfExists(whereParts, columns, "IsDeleted", false);
                AddBooleanColumnFilterIfExists(whereParts, columns, "Deleted", false);
                AddBooleanColumnFilterIfExists(whereParts, columns, "IsHidden", false);
                AddBooleanColumnFilterIfExists(whereParts, columns, "IsActive", true);

                var idSelect = QuoteSqlName(idColumn);
                var orderIdSelect = string.IsNullOrWhiteSpace(orderIdColumn) ? "CAST(0 AS int)" : QuoteSqlName(orderIdColumn);
                var typeSelect = string.IsNullOrWhiteSpace(typeColumn) ? "CAST(0 AS int)" : QuoteSqlName(typeColumn);
                var bodySelect = string.IsNullOrWhiteSpace(bodyColumn) ? "N''" : $"CAST({QuoteSqlName(bodyColumn)} AS nvarchar(max))";
                var createdSelect = string.IsNullOrWhiteSpace(createdColumn) ? "CAST(NULL AS datetime2)" : QuoteSqlName(createdColumn);
                var imageSelect = string.IsNullOrWhiteSpace(imageColumn) ? "N''" : $"CAST({QuoteSqlName(imageColumn)} AS nvarchar(max))";
                var orderBy = !string.IsNullOrWhiteSpace(createdColumn)
                    ? $"{QuoteSqlName(createdColumn)} DESC, {QuoteSqlName(idColumn)} DESC"
                    : $"{QuoteSqlName(idColumn)} DESC";

                var reports = new List<QuickReportListItem>();
                var connection = _context.Database.GetDbConnection();
                var shouldClose = connection.State != System.Data.ConnectionState.Open;

                if (shouldClose)
                {
                    await connection.OpenAsync();
                }

                try
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = $@"
SELECT TOP 500
    {idSelect} AS Id,
    {orderIdSelect} AS OrderId,
    {typeSelect} AS TypeValue,
    {bodySelect} AS BodyValue,
    {createdSelect} AS CreatedAtValue,
    {imageSelect} AS ImageValue
FROM {QuoteSqlName(table.Value.Schema)}.{QuoteSqlName(table.Value.Name)}
WHERE {string.Join(" AND ", whereParts)}
ORDER BY {orderBy}";

                    var userParameter = command.CreateParameter();
                    userParameter.ParameterName = "@currentUserId";
                    userParameter.Value = currentUserId;
                    command.Parameters.Add(userParameter);

                    await using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var reportType = SafeIntValue(reader["TypeValue"]);

                        reports.Add(new QuickReportListItem
                        {
                            Id = SafeIntValue(reader["Id"]),
                            OrderId = SafeIntValue(reader["OrderId"]),
                            Type = reportType,
                            Body = SafeStringValue(reader["BodyValue"]),
                            CreatedAt = SafeDateTimeValue(reader["CreatedAtValue"]),
                            ImageUrl = SafeStringValue(reader["ImageValue"])
                        });
                    }
                }
                finally
                {
                    if (shouldClose)
                    {
                        await connection.CloseAsync();
                    }
                }

                var postIds = reports.Select(item => item.Id).Where(id => id > 0).ToList();
                var imagesByPostId = await GetQuickReportImagesByPostIdAsync(postIds);

                foreach (var report in reports)
                {
                    if (string.IsNullOrWhiteSpace(report.ImageUrl)
                        && imagesByPostId.TryGetValue(report.Id, out var postImages)
                        && postImages.Any())
                    {
                        report.ImageUrl = postImages.First();
                    }
                }

                var problemCount = reports.Count(item => item.Type == 0);
                var editCount = reports.Count(item => item.Type == 1);
                var filteredReports = type.HasValue
                    ? reports.Where(item => item.Type == type.Value).ToList()
                    : reports;

                return Json(new
                {
                    success = true,
                    problemCount,
                    editCount,
                    totalCount = reports.Count,
                    reports = filteredReports.Select(item => new
                    {
                        item.Id,
                        item.OrderId,
                        item.Type,
                        TypeText = item.Type == 1 ? "تعديل مطلوب" : "بلاغ",
                        item.Body,
                        CreatedAt = item.CreatedAt?.ToString("yyyy-MM-dd HH:mm") ?? "",
                        item.ImageUrl,
                        UserName = currentUserDisplayName,
                        AuthorName = currentUserDisplayName,
                        EmployeeName = currentUserDisplayName
                    })
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("QuickReportList error: " + ex.Message);
                return StatusCode(500, new { success = false, message = "فشل تحميل البلاغات: " + ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment,CallCenter")]
        [Route("/Order/QuickReportDelete")]
        public async Task<IActionResult> QuickReportDelete([FromForm] int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { success = false, message = "رقم البلاغ غير صحيح" });
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Unauthorized(new { success = false, message = "انتهت الجلسة، سجلي الدخول مرة أخرى" });
            }

            try
            {
                var table = await FindExistingTableAsync("OrderPosts", "OrderPost");
                if (table == null)
                {
                    return BadRequest(new { success = false, message = "جدول البلاغات غير موجود" });
                }

                var columns = await GetTableColumnsAsync(table.Value.Schema, table.Value.Name);
                var idColumn = FindFirstExistingColumn(columns, new[] { "Id", "OrderPostId", "OrderPostID", "PostId", "PostID" });
                var userColumn = FindFirstExistingColumn(columns, QuickReportUserColumnCandidates());

                if (string.IsNullOrWhiteSpace(idColumn) || string.IsNullOrWhiteSpace(userColumn))
                {
                    return BadRequest(new { success = false, message = "تعذر تحديد أعمدة البلاغ" });
                }

                var connection = _context.Database.GetDbConnection();
                var shouldClose = connection.State != System.Data.ConnectionState.Open;

                if (shouldClose)
                {
                    await connection.OpenAsync();
                }

                try
                {
                    await using var command = connection.CreateCommand();
                    var idFilter = $"{QuoteSqlName(idColumn)} = @id AND {QuoteSqlName(userColumn)} = @currentUserId";

                    if (columns.Contains("IsDeleted") || columns.Contains("Deleted") || columns.Contains("IsHidden") || columns.Contains("IsActive"))
                    {
                        var setParts = new List<string>();
                        if (columns.Contains("IsDeleted")) setParts.Add("[IsDeleted] = 1");
                        if (columns.Contains("Deleted")) setParts.Add("[Deleted] = 1");
                        if (columns.Contains("IsHidden")) setParts.Add("[IsHidden] = 1");
                        if (columns.Contains("IsActive")) setParts.Add("[IsActive] = 0");

                        var updatedColumn = FindFirstExistingColumn(columns, new[] { "UpdatedAt", "UpdatedOn", "LastEditedDate" });
                        if (!string.IsNullOrWhiteSpace(updatedColumn))
                        {
                            setParts.Add($"{QuoteSqlName(updatedColumn)} = @now");
                        }

                        command.CommandText = $@"
UPDATE {QuoteSqlName(table.Value.Schema)}.{QuoteSqlName(table.Value.Name)}
SET {string.Join(", ", setParts)}
WHERE {idFilter}";

                        if (!string.IsNullOrWhiteSpace(updatedColumn))
                        {
                            var nowParameter = command.CreateParameter();
                            nowParameter.ParameterName = "@now";
                            nowParameter.Value = _timeService.GetIstanbulTimeWithOffset();
                            command.Parameters.Add(nowParameter);
                        }
                    }
                    else
                    {
                        await DeleteQuickReportImagesAsync(id, connection);
                        command.CommandText = $@"
DELETE FROM {QuoteSqlName(table.Value.Schema)}.{QuoteSqlName(table.Value.Name)}
WHERE {idFilter}";
                    }

                    var idParameter = command.CreateParameter();
                    idParameter.ParameterName = "@id";
                    idParameter.Value = id;
                    command.Parameters.Add(idParameter);

                    var userParameter = command.CreateParameter();
                    userParameter.ParameterName = "@currentUserId";
                    userParameter.Value = currentUserId;
                    command.Parameters.Add(userParameter);

                    var affected = await command.ExecuteNonQueryAsync();
                    if (affected <= 0)
                    {
                        return BadRequest(new { success = false, message = "البلاغ غير موجود أو ليس لديك صلاحية حذفه" });
                    }
                }
                finally
                {
                    if (shouldClose)
                    {
                        await connection.CloseAsync();
                    }
                }

                return Json(new { success = true, id });
            }
            catch (Exception ex)
            {
                Console.WriteLine("QuickReportDelete error: " + ex.Message);
                return StatusCode(500, new { success = false, message = "فشل حذف البلاغ: " + ex.Message });
            }
        }

        private class QuickReportListItem
        {
            public int Id { get; set; }
            public int OrderId { get; set; }
            public int Type { get; set; }
            public string Body { get; set; } = string.Empty;
            public DateTime? CreatedAt { get; set; }
            public string ImageUrl { get; set; } = string.Empty;
        }

        private async Task<Dictionary<int, List<string>>> GetQuickReportImagesByPostIdAsync(List<int> postIds)
        {
            var result = new Dictionary<int, List<string>>();
            if (postIds == null || postIds.Count == 0)
            {
                return result;
            }

            var table = await FindExistingTableAsync("OrderPostImages", "OrderPostImage", "OrderPostAttachments", "OrderPostAttachment");
            if (table == null)
            {
                return result;
            }

            var columns = await GetTableColumnsAsync(table.Value.Schema, table.Value.Name);
            var postIdColumn = FindFirstExistingColumn(columns, new[] { "OrderPostId", "OrderPostID", "PostId", "PostID" });
            var imageColumn = FindFirstExistingColumn(columns, new[] { "ImageUrl", "Url", "ImagePath", "Path", "FilePath", "PhotoUrl", "AttachmentUrl" });
            if (string.IsNullOrWhiteSpace(postIdColumn) || string.IsNullOrWhiteSpace(imageColumn))
            {
                return result;
            }

            var connection = _context.Database.GetDbConnection();
            var shouldClose = connection.State != System.Data.ConnectionState.Open;

            if (shouldClose)
            {
                await connection.OpenAsync();
            }

            try
            {
                await using var command = connection.CreateCommand();
                var safePostIds = postIds.Where(id => id > 0).Distinct().Take(500).ToList();
                var parameterNames = safePostIds.Select((_, index) => "@post" + index).ToList();
                var whereParts = new List<string>
                {
                    $"{QuoteSqlName(postIdColumn)} IN ({string.Join(", ", parameterNames)})"
                };

                AddBooleanColumnFilterIfExists(whereParts, columns, "IsDeleted", false);
                AddBooleanColumnFilterIfExists(whereParts, columns, "Deleted", false);
                AddBooleanColumnFilterIfExists(whereParts, columns, "IsHidden", false);
                AddBooleanColumnFilterIfExists(whereParts, columns, "IsActive", true);

                command.CommandText = $@"
SELECT {QuoteSqlName(postIdColumn)} AS PostIdValue, CAST({QuoteSqlName(imageColumn)} AS nvarchar(max)) AS ImageValue
FROM {QuoteSqlName(table.Value.Schema)}.{QuoteSqlName(table.Value.Name)}
WHERE {string.Join(" AND ", whereParts)}";

                for (var i = 0; i < safePostIds.Count; i++)
                {
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = parameterNames[i];
                    parameter.Value = safePostIds[i];
                    command.Parameters.Add(parameter);
                }

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var postId = SafeIntValue(reader["PostIdValue"]);
                    var imageUrl = SafeStringValue(reader["ImageValue"]);
                    if (postId <= 0 || string.IsNullOrWhiteSpace(imageUrl))
                    {
                        continue;
                    }

                    if (!result.ContainsKey(postId))
                    {
                        result[postId] = new List<string>();
                    }

                    result[postId].Add(imageUrl);
                }
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }

            return result;
        }

        private async Task DeleteQuickReportImagesAsync(int postId, System.Data.Common.DbConnection openConnection)
        {
            var table = await FindExistingTableAsync("OrderPostImages", "OrderPostImage", "OrderPostAttachments", "OrderPostAttachment");
            if (table == null)
            {
                return;
            }

            var columns = await GetTableColumnsAsync(table.Value.Schema, table.Value.Name);
            var postIdColumn = FindFirstExistingColumn(columns, new[] { "OrderPostId", "OrderPostID", "PostId", "PostID" });
            if (string.IsNullOrWhiteSpace(postIdColumn))
            {
                return;
            }

            await using var command = openConnection.CreateCommand();
            command.CommandText = $@"
DELETE FROM {QuoteSqlName(table.Value.Schema)}.{QuoteSqlName(table.Value.Name)}
WHERE {QuoteSqlName(postIdColumn)} = @postId";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@postId";
            parameter.Value = postId;
            command.Parameters.Add(parameter);
            await command.ExecuteNonQueryAsync();
        }

        private static IEnumerable<string> QuickReportUserColumnCandidates()
        {
            return new[]
            {
                "AuthorUserId",
                "AuthUserId",
                "ApplicationUserId",
                "UserId",
                "AuthorId",
                "CreatedById",
                "CreatedByUserId",
                "CreatedByApplicationUserId",
                "CreatedBy",
                "EmployeeId"
            };
        }

        private static string? FindFirstExistingColumn(HashSet<string> columns, IEnumerable<string> candidateNames)
        {
            if (columns == null || candidateNames == null)
            {
                return null;
            }

            return candidateNames.FirstOrDefault(columns.Contains);
        }

        private static void AddBooleanColumnFilterIfExists(List<string> whereParts, HashSet<string> columns, string columnName, bool expectedValue)
        {
            if (whereParts == null || columns == null || !columns.Contains(columnName))
            {
                return;
            }

            whereParts.Add($"({QuoteSqlName(columnName)} IS NULL OR {QuoteSqlName(columnName)} = {(expectedValue ? 1 : 0)})");
        }

        private static int SafeIntValue(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return 0;
            }

            try
            {
                if (value is int intValue) return intValue;
                if (value is long longValue) return Convert.ToInt32(longValue);
                if (value is short shortValue) return shortValue;
                if (value is byte byteValue) return byteValue;
                if (value is decimal decimalValue) return Convert.ToInt32(decimalValue);
                if (value is bool boolValue) return boolValue ? 1 : 0;
                if (int.TryParse(value.ToString(), out var parsed)) return parsed;
            }
            catch
            {
            }

            return 0;
        }

        private static string SafeStringValue(object? value)
        {
            return value == null || value == DBNull.Value ? string.Empty : value.ToString() ?? string.Empty;
        }

        private static DateTime? SafeDateTimeValue(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            if (value is DateTime dateTimeValue)
            {
                return dateTimeValue;
            }

            if (DateTime.TryParse(value.ToString(), out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private async Task<int> InsertQuickReportPostAsync(
            int orderId,
            int type,
            string body,
            string userId,
            DateTime createdAt,
            string? firstImageUrl)
        {
            var table = await FindExistingTableAsync("OrderPosts", "OrderPost");
            if (table == null)
            {
                throw new InvalidOperationException("جدول OrderPosts غير موجود في قاعدة البيانات");
            }

            var columns = await GetTableColumnsAsync(table.Value.Schema, table.Value.Name);

            var values = new Dictionary<string, object?>();
            AddIfColumnExists(values, columns, new[] { "OrderId", "OrderID" }, orderId);
            AddIfColumnExists(values, columns, new[] { "Type", "PostType", "OrderPostType", "OrderPostTypeId", "OrderPostTypeID" }, type);
            AddIfColumnExists(values, columns, new[] { "Body", "Text", "Content", "Description", "Message", "Note" }, body);
            AddIfColumnExists(values, columns, new[] { "Reason", "Title" }, body);
            AddUserIdColumnsIfExist(values, columns, userId);
            AddIfColumnExists(values, columns, new[] { "CreatedAt", "CreatedOn", "CreatedDate", "DateCreated", "AddedAt", "AddedDate" }, createdAt);
            AddIfColumnExists(values, columns, new[] { "UpdatedAt", "UpdatedOn", "LastEditedDate" }, createdAt);
            AddIfColumnExists(values, columns, new[] { "IsDeleted", "Deleted", "IsHidden" }, false);
            AddIfColumnExists(values, columns, new[] { "IsActive" }, true);

            if (!string.IsNullOrWhiteSpace(firstImageUrl))
            {
                AddIfColumnExists(values, columns, new[] { "ImageUrl", "ImagePath", "PhotoUrl", "PhotoPath", "PictureUrl", "FilePath", "AttachmentUrl" }, firstImageUrl);
            }

            if (!values.Keys.Any(key => key.Equals("OrderId", StringComparison.OrdinalIgnoreCase) || key.Equals("OrderID", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("لم يتم العثور على عمود OrderId داخل جدول OrderPosts");
            }

            if (!values.Keys.Any(key => key.Equals("Body", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Text", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Content", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Description", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Message", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Note", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("لم يتم العثور على عمود نص البلاغ داخل جدول OrderPosts");
            }

            await AddMissingRequiredColumnDefaultsAsync(table.Value.Schema, table.Value.Name, values, createdAt);

            var insertedId = await InsertDynamicRowAsync(table.Value.Schema, table.Value.Name, values);
            return insertedId;
        }

        private async Task InsertQuickReportPostImagesAsync(
            int postId,
            IEnumerable<string> imageUrls,
            string userId,
            DateTime createdAt)
        {
            var table = await FindExistingTableAsync("OrderPostImages", "OrderPostImage", "OrderPostAttachments", "OrderPostAttachment");
            if (table == null)
            {
                return;
            }

            var columns = await GetTableColumnsAsync(table.Value.Schema, table.Value.Name);

            foreach (var imageUrl in imageUrls.Where(url => !string.IsNullOrWhiteSpace(url)))
            {
                var values = new Dictionary<string, object?>();
                AddIfColumnExists(values, columns, new[] { "OrderPostId", "OrderPostID", "PostId", "PostID" }, postId);
                AddIfColumnExists(values, columns, new[] { "ImageUrl", "Url", "ImagePath", "Path", "FilePath", "PhotoUrl", "AttachmentUrl" }, imageUrl);
                AddUserIdColumnsIfExist(values, columns, userId);
                AddIfColumnExists(values, columns, new[] { "CreatedAt", "CreatedOn", "CreatedDate", "DateCreated", "AddedAt", "AddedDate" }, createdAt);
                AddIfColumnExists(values, columns, new[] { "IsDeleted", "Deleted", "IsHidden" }, false);
                AddIfColumnExists(values, columns, new[] { "IsActive" }, true);

                if (values.Count > 0)
                {
                    await AddMissingRequiredColumnDefaultsAsync(table.Value.Schema, table.Value.Name, values, createdAt);
                    await InsertDynamicRowAsync(table.Value.Schema, table.Value.Name, values);
                }
            }
        }

        private async Task AddMissingRequiredColumnDefaultsAsync(
            string schema,
            string tableName,
            Dictionary<string, object?> values,
            DateTime fallbackDate)
        {
            var requiredColumns = await GetRequiredTableColumnsAsync(schema, tableName);

            foreach (var column in requiredColumns)
            {
                if (values.ContainsKey(column.Name))
                {
                    continue;
                }

                values[column.Name] = GetDefaultValueForSqlType(column.DataType, fallbackDate);
            }
        }

        private async Task<List<(string Name, string DataType)>> GetRequiredTableColumnsAsync(string schema, string tableName)
        {
            var columns = new List<(string Name, string DataType)>();
            var connection = _context.Database.GetDbConnection();
            var shouldClose = connection.State != System.Data.ConnectionState.Open;

            if (shouldClose)
            {
                await connection.OpenAsync();
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = @"
SELECT COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = @schema
  AND TABLE_NAME = @tableName
  AND IS_NULLABLE = 'NO'
  AND COLUMN_DEFAULT IS NULL
  AND COLUMNPROPERTY(OBJECT_ID(QUOTENAME(TABLE_SCHEMA) + '.' + QUOTENAME(TABLE_NAME)), COLUMN_NAME, 'IsIdentity') = 0
  AND COLUMNPROPERTY(OBJECT_ID(QUOTENAME(TABLE_SCHEMA) + '.' + QUOTENAME(TABLE_NAME)), COLUMN_NAME, 'IsComputed') = 0
  AND DATA_TYPE NOT IN ('timestamp', 'rowversion')";

                var schemaParameter = command.CreateParameter();
                schemaParameter.ParameterName = "@schema";
                schemaParameter.Value = schema;
                command.Parameters.Add(schemaParameter);

                var tableParameter = command.CreateParameter();
                tableParameter.ParameterName = "@tableName";
                tableParameter.Value = tableName;
                command.Parameters.Add(tableParameter);

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    columns.Add((reader.GetString(0), reader.GetString(1)));
                }
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }

            return columns;
        }

        private static object GetDefaultValueForSqlType(string dataType, DateTime fallbackDate)
        {
            dataType = (dataType ?? string.Empty).ToLowerInvariant();

            if (dataType.Contains("char") || dataType.Contains("text") || dataType == "xml")
            {
                return string.Empty;
            }

            if (dataType == "bit")
            {
                return false;
            }

            if (dataType.Contains("date") || dataType.Contains("time"))
            {
                return fallbackDate;
            }

            if (dataType == "uniqueidentifier")
            {
                return Guid.NewGuid();
            }

            if (dataType.Contains("binary"))
            {
                return Array.Empty<byte>();
            }

            if (dataType is "decimal" or "numeric" or "money" or "smallmoney" or "float" or "real")
            {
                return 0m;
            }

            return 0;
        }

        private async Task<(string Schema, string Name)?> FindExistingTableAsync(params string[] tableNames)
        {
            var connection = _context.Database.GetDbConnection();
            var shouldClose = connection.State != System.Data.ConnectionState.Open;

            if (shouldClose)
            {
                await connection.OpenAsync();
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = @"
SELECT TOP 1 TABLE_SCHEMA, TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE'
  AND TABLE_NAME IN (" + string.Join(",", tableNames.Select((_, index) => "@t" + index)) + @")
ORDER BY CASE TABLE_NAME " + string.Join(" ", tableNames.Select((name, index) => $"WHEN @t{index} THEN {index}")) + " END";

                for (var i = 0; i < tableNames.Length; i++)
                {
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = "@t" + i;
                    parameter.Value = tableNames[i];
                    command.Parameters.Add(parameter);
                }

                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return (reader.GetString(0), reader.GetString(1));
                }

                return null;
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private async Task<HashSet<string>> GetTableColumnsAsync(string schema, string tableName)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var connection = _context.Database.GetDbConnection();
            var shouldClose = connection.State != System.Data.ConnectionState.Open;

            if (shouldClose)
            {
                await connection.OpenAsync();
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = @"
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @tableName";

                var schemaParameter = command.CreateParameter();
                schemaParameter.ParameterName = "@schema";
                schemaParameter.Value = schema;
                command.Parameters.Add(schemaParameter);

                var tableParameter = command.CreateParameter();
                tableParameter.ParameterName = "@tableName";
                tableParameter.Value = tableName;
                command.Parameters.Add(tableParameter);

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    columns.Add(reader.GetString(0));
                }
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }

            return columns;
        }

        private static string NormalizeQuickReportOrderCode(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder();

            foreach (var character in value.Trim())
            {
                if (character >= '0' && character <= '9')
                {
                    builder.Append(character);
                }
                else if (character >= '٠' && character <= '٩')
                {
                    builder.Append((char)(character - '٠' + '0'));
                }
                else if (character >= '۰' && character <= '۹')
                {
                    builder.Append((char)(character - '۰' + '0'));
                }
            }

            return builder.ToString();
        }

        private static void AddUserIdColumnsIfExist(
            Dictionary<string, object?> values,
            HashSet<string> columns,
            string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            foreach (var columnName in QuickReportUserColumnCandidates())
            {
                if (columns.Contains(columnName) && !values.ContainsKey(columnName))
                {
                    values[columnName] = userId;
                }
            }
        }

        private static void AddIfColumnExists(
            Dictionary<string, object?> values,
            HashSet<string> columns,
            IEnumerable<string> candidateNames,
            object? value)
        {
            var columnName = candidateNames.FirstOrDefault(columns.Contains);
            if (string.IsNullOrWhiteSpace(columnName) || values.ContainsKey(columnName))
            {
                return;
            }

            values[columnName] = value;
        }

        private async Task<int> InsertDynamicRowAsync(string schema, string tableName, Dictionary<string, object?> values)
        {
            if (values == null || values.Count == 0)
            {
                return 0;
            }

            var connection = _context.Database.GetDbConnection();
            var shouldClose = connection.State != System.Data.ConnectionState.Open;

            if (shouldClose)
            {
                await connection.OpenAsync();
            }

            try
            {
                await using var command = connection.CreateCommand();
                var columnNames = values.Keys.ToList();
                var parameterNames = columnNames.Select((_, index) => "@p" + index).ToList();

                command.CommandText = $@"
INSERT INTO {QuoteSqlName(schema)}.{QuoteSqlName(tableName)} ({string.Join(", ", columnNames.Select(QuoteSqlName))})
VALUES ({string.Join(", ", parameterNames)});
SELECT CAST(SCOPE_IDENTITY() AS int);";

                for (var i = 0; i < columnNames.Count; i++)
                {
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = parameterNames[i];
                    parameter.Value = values[columnNames[i]] ?? DBNull.Value;
                    command.Parameters.Add(parameter);
                }

                var result = await command.ExecuteScalarAsync();
                return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private static string QuoteSqlName(string name)
        {
            return "[" + (name ?? string.Empty).Replace("]", "]]") + "]";
        }


    }
}
