using lotus_blue.API;
using lotus_blue.Data;
using lotus_blue.Hubs;
using lotus_blue.Models;
using lotus_blue.Models.ViewModel;
using lotus_blue.Services;
using lotus_blue.OrderStatus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Security.Claims;
using System.Text.Json;
using System.Web.Http.Cors;
using static lotus_blue.Models.Common;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace lotus_blue.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RESTAPI _restapi;
        private readonly GetCurrentTimeInIstanbul _timeService;
        private readonly OrderService _orderService;
        private readonly DeliveryCompanyService _deliveryCompanyService;
        private readonly DynamicCommon _dynamicCommon;
        private readonly IMemoryCache _cache;
        private readonly DataCacheService _dataCacheService;
        private readonly CurrencyExchangeService _currencyExchangeService;
        private readonly DecimalFormattingService _decimalFormattingService;
        private readonly QueryFilteringService _queryFilteringService;
        private readonly IHubContext<OrderHub> _hubContext;
        public HomeController(ILogger<HomeController> logger,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RESTAPI restApi,
            GetCurrentTimeInIstanbul timeService,
            OrderService orderService,
            DeliveryCompanyService deliveryCompanyService,
            DynamicCommon dynamicCommon,
            RESTAPI restapi,
            IMemoryCache memoryCache,
            DataCacheService dataCacheService,
            CurrencyExchangeService currencyExchangeService,
            DecimalFormattingService decimalFormattingService,
             QueryFilteringService queryFilteringService,
            IHubContext<OrderHub> hubContext)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
            _restapi = restApi;
            _timeService = timeService;
            _orderService = orderService;
            _deliveryCompanyService = deliveryCompanyService;
            _dynamicCommon = dynamicCommon;
            _cache = memoryCache;
            _dataCacheService = dataCacheService;
            _currencyExchangeService = currencyExchangeService;
            _decimalFormattingService = decimalFormattingService;
            _queryFilteringService = queryFilteringService;
            _hubContext = hubContext;
        }

        [HttpPost]
        public IActionResult ClearFilters()
        {
            HttpContext.Session.Clear(); // This clears all session data
            return Ok();
        }



        // done
        [Authorize]
        public async Task<IActionResult> Index(
        int page = 1,
        int? pageSize = null,
        Common.Countries? countryId = null,
        OrderStatusEnum? orderstatusId = null,
        OrderSourceEnum? ordersourceId = null,
        int? storeId = null,
        int? deliverycompanyId = null,
        int? deliveryrepresentativeId = null,
        int? productId = null,
        string? cityId = null,
        string? search = null,
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
        bool? IsPaid = null,
                 DateTime? startDate = null, DateTime? endDate = null,
        string? failureReason = null,
        bool? isDuplicateOrders = null,
        bool showDebugQuery = false,
        bool investigationMode = false

            )

        {
            try
            {
                // sum total prices based on country filter
                Dictionary<int, decimal> totalDeliveryCompanyPrice;
                decimal totalOrderPrice = 0;
                string? SelectedCoutnryfromfilter = null;
                decimal totalOrderPriceDollar = 0;
                decimal totalOrderPriceTRY = 0;
                var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var investigationEmployeeIdFromRequest = employeeId;
                ViewBag.IsInvestigationPage = investigationMode;

                // query
                IQueryable<Order> query = _context.Orders.AsNoTracking();


                var sessionPrefix = "Home_Index_";
                var now = _timeService.GetIstanbulTimeWithOffset(); // Get the current Istanbul time
                bool isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
                bool hasExplicitDateRangeInRequest = startDate.HasValue || endDate.HasValue;

                // CallCenter on Home: with no filters set, the table collapses to a single row —
                // the user's own latest-created order. Captured BEFORE session-merging so stale
                // session filters from previous visits don't false-positive the "has filter" check
                // (the user can't see those filters in the UI, only the request URL counts).
                bool isCallCenter = User.IsInRole("CallCenter");
                bool requestHasHomeFilter = countryId.HasValue || !string.IsNullOrEmpty(cityId) || storeId.HasValue || deliverycompanyId.HasValue
                    || orderstatusId.HasValue || !string.IsNullOrEmpty(search) || !string.IsNullOrEmpty(employeeId)
                    || productId.HasValue || ordersourceId.HasValue || fromcomments.HasValue
                    || deliveryrepresentativeId.HasValue || gender.HasValue
                    || startDate.HasValue || endDate.HasValue
                    || isOffers.HasValue || isDiscount.HasValue || isBonus.HasValue
                    || isspecialClients.HasValue || isFixedAndDelivered.HasValue || isHidden.HasValue
                    || IsComplaints.HasValue || IsPaid.HasValue || !string.IsNullOrEmpty(failureReason)
                    || isDuplicateOrders == true;
                bool isCallCenterNoFilters = isCallCenter && !requestHasHomeFilter && !investigationMode;

                // Retrieve filter values from session if they are not provided by the request
                pageSize = pageSize ?? HttpContext.Session.GetInt32($"{sessionPrefix}PageSize") ?? 10;
                if (!hasExplicitDateRangeInRequest && !isAjaxRequest)
                {
                    HttpContext.Session.Remove($"{sessionPrefix}StartDate");
                    HttpContext.Session.Remove($"{sessionPrefix}EndDate");
                }

                startDate = startDate ?? ((hasExplicitDateRangeInRequest || isAjaxRequest)
                    ? (DateTime.TryParse(HttpContext.Session.GetString($"{sessionPrefix}StartDate"), out DateTime start) ? start : (DateTime?)null)
                    : null);
                endDate = endDate ?? ((hasExplicitDateRangeInRequest || isAjaxRequest)
                    ? (DateTime.TryParse(HttpContext.Session.GetString($"{sessionPrefix}EndDate"), out DateTime end) ? end : (DateTime?)null)
                    : null);
                countryId = countryId ?? (Enum.TryParse(HttpContext.Session.GetString($"{sessionPrefix}CountryId"), out Common.Countries country) ? country : (Common.Countries?)null);
                orderstatusId = orderstatusId ?? (Enum.TryParse(HttpContext.Session.GetString($"{sessionPrefix}OrderStatusId"), out OrderStatusEnum status) ? status : (OrderStatusEnum?)null);
                ordersourceId = ordersourceId ?? (Enum.TryParse(HttpContext.Session.GetString($"{sessionPrefix}OrderSourceId"), out OrderSourceEnum source) ? source : (OrderSourceEnum?)null);
                storeId = storeId ?? HttpContext.Session.GetInt32($"{sessionPrefix}StoreId");
                deliverycompanyId = deliverycompanyId ?? HttpContext.Session.GetInt32($"{sessionPrefix}DeliveryCompanyId");
                deliveryrepresentativeId = deliveryrepresentativeId ?? HttpContext.Session.GetInt32($"{sessionPrefix}DeliveryRepresentativeId");
                productId = productId ?? HttpContext.Session.GetInt32($"{sessionPrefix}ProductId");
                cityId = cityId ?? HttpContext.Session.GetString($"{sessionPrefix}CityId");
                employeeId = employeeId ?? HttpContext.Session.GetString($"{sessionPrefix}EmployeeId");
                fromcomments = fromcomments ?? (bool.TryParse(HttpContext.Session.GetString($"{sessionPrefix}FromComments"), out bool fromComm) ? fromComm : (bool?)null);
                gender = gender ?? (bool.TryParse(HttpContext.Session.GetString($"{sessionPrefix}Gender"), out bool gen) ? gen : (bool?)null);
                isOffers = isOffers ?? (bool.TryParse(HttpContext.Session.GetString($"{sessionPrefix}IsOffers"), out bool offers) ? offers : (bool?)null);
                isDiscount = isDiscount ?? (bool.TryParse(HttpContext.Session.GetString($"{sessionPrefix}IsDiscount"), out bool discount) ? discount : (bool?)null);
                isBonus = isBonus ?? (bool.TryParse(HttpContext.Session.GetString($"{sessionPrefix}IsBonus"), out bool bonus) ? bonus : (bool?)null);
                isspecialClients = isspecialClients ?? (bool.TryParse(HttpContext.Session.GetString($"{sessionPrefix}IsSpecialClients"), out bool specialClients) ? specialClients : (bool?)null);
                isFixedAndDelivered = isFixedAndDelivered ?? (bool.TryParse(HttpContext.Session.GetString($"{sessionPrefix}IsFixedAndDelivered"), out bool fixedAndDelivered) ? fixedAndDelivered : (bool?)null);
                isHidden = isHidden ?? (bool.TryParse(HttpContext.Session.GetString($"{sessionPrefix}IsHidden"), out bool hidden) ? hidden : (bool?)null);
                IsComplaints = IsComplaints ?? (bool.TryParse(HttpContext.Session.GetString($"{sessionPrefix}IsComplaints"), out bool complaints) ? complaints : (bool?)null);
                IsPaid = IsPaid ?? (bool.TryParse(HttpContext.Session.GetString($"{sessionPrefix}IsPaid"), out bool paid) ? paid : (bool?)null);
                failureReason = failureReason ?? HttpContext.Session.GetString($"{sessionPrefix}FailureReason");
                isDuplicateOrders = isDuplicateOrders ?? (bool.TryParse(HttpContext.Session.GetString($"{sessionPrefix}IsDuplicateOrders"), out bool duplicateOrders) ? duplicateOrders : (bool?)null);

                if (isDuplicateOrders == true)
                {
                    HttpContext.Session.SetString($"{sessionPrefix}IsDuplicateOrders", "true");
                }
                else
                {
                    isDuplicateOrders = null;
                    HttpContext.Session.Remove($"{sessionPrefix}IsDuplicateOrders");
                }

                // قيد التحقيق: نعرض الطلبات الجديدة فقط، ونلغي أي فلاتر مخزنة في السيشن
                // حتى تظهر الصفحة بنفس جدول الهوم لكن مفلترة على الطلبات غير المعتمدة فقط.
                if (investigationMode)
                {
                    countryId = null;
                    ordersourceId = null;
                    storeId = null;
                    deliverycompanyId = null;
                    deliveryrepresentativeId = null;
                    productId = null;
                    cityId = null;
                    search = null;
                    // قيد التحقق:
                    // الكول سنتر يشوف طلباته هو فقط، حتى لو اتبعت فلتر موظف من الصفحة.
                    employeeId = User.IsInRole("CallCenter")
                        ? currentUser
                        : (string.IsNullOrWhiteSpace(investigationEmployeeIdFromRequest)
                            ? null
                            : investigationEmployeeIdFromRequest);
                    fromcomments = null;
                    gender = null;
                    isOffers = null;
                    isDiscount = null;
                    isBonus = null;
                    isspecialClients = null;
                    isFixedAndDelivered = null;
                    isHidden = null;
                    IsComplaints = null;
                    IsPaid = null;
                    startDate = null;
                    endDate = null;
                    failureReason = null;
                    isDuplicateOrders = null;
                    // قيد التحقق:
                    // لا نمرر فلتر الحالة إلى ApplyFilters؛ لأننا سنطبق فلتر خاص بالأسفل:
                    // 1) طلبات حالتها طلب جديد.
                    // 2) أو طلبات اتفتحت من صفحة قيد التحقق واتغيرت حالتها، وتفضل ظاهرة حتى تم الاعتماد.
                    orderstatusId = null;
                    page = 1;
                    pageSize = int.MaxValue;
                }

                if (User.IsInRole("DeliveryCompany") || User.IsInRole("DeliveryRepresentative"))
                {
                    // Get the delivery company associated with the current user
                    query = query.Where(o => o.DeliveryCompany.UserId == currentUser
                    && o.OrderStatus != OrderStatusEnum.الطلبات_المعلقة
                    && o.OrderStatus != OrderStatusEnum.الطلبات_الغير_معرفة
                    && !o.IsHidden).AsQueryable();
                }

                if (User.IsInRole("FollowUpDepartment") || User.IsInRole("CallCenter"))
                {
                    query = query.Where(o => o.ManufacturingCompany.EmployeeManufacturingCompanies.Any(a => a.ApplicationUserId == currentUser && a.CanSeeManufacturingCompany));

                    if (isCallCenterNoFilters)
                    {
                        // Pin to this user's own latest-created order — bypass the default status exclusion
                        // so the row is shown regardless of status.
                        query = query.Where(o => o.ApplicationUserId == currentUser);
                    }
                    else if (string.IsNullOrEmpty(search))
                    {
                        // Exclude certain statuses on initial page load if there's no search term
                        query = query.Where(x =>
                            !(
                                x.OrderStatus == OrderStatusEnum.تم_الدفع ||
                                x.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد ||
                                x.OrderStatus == OrderStatusEnum.تم_التسليم
                            )
                        );
                    }
                }

                // قيد التحقيق: استبعاد أي طلب تم اعتماده من قبل.
                // ونعرض طلب جديد أو طلب تم فتحه من صفحة قيد التحقق حتى لو حالته اتغيرت.
                List<int> investigationOpenedOrderIds = new List<int>();
                if (investigationMode)
                {
                    investigationOpenedOrderIds = await GetInvestigationOpenedOrderIdsAsync();
                    var investigationOpenedByCurrentUserOrderIds = User.IsInRole("CallCenter")
                        ? await GetInvestigationOpenedOrderIdsByUserAsync(currentUser)
                        : new List<int>();

                    var validInvestigationApprovalsQuery = ValidInvestigationApprovalsQuery();
                    query = query.Where(o => !validInvestigationApprovalsQuery.Any(a => a.OrderId == o.Id));

                    if (User.IsInRole("CallCenter"))
                    {
                        // الكول سنتر يشوف طلباته الجديدة فقط.
                        // ولو فتح الطلب من صفحة قيد التحقق، يختفي من عنده فقط ولا يعتبر اعتماد.
                        query = query.Where(o => o.ApplicationUserId == currentUser
                            && o.OrderStatus == OrderStatusEnum.طلب_جديد);

                        if (investigationOpenedByCurrentUserOrderIds.Any())
                        {
                            query = query.Where(o => !investigationOpenedByCurrentUserOrderIds.Contains(o.Id));
                        }
                    }
                    else if (investigationOpenedOrderIds.Any())
                    {
                        query = query.Where(o =>
                            o.OrderStatus == OrderStatusEnum.طلب_جديد ||
                            investigationOpenedOrderIds.Contains(o.Id));
                    }
                    else
                    {
                        query = query.Where(o => o.OrderStatus == OrderStatusEnum.طلب_جديد);
                    }
                }


                // ترتيب الصفحة الرئيسية حسب نافذة التحديثات التشغيلية فقط:
                // لا نفلتر الداتا نفسها من 10 لـ 10؛ كل الطلبات تفضل ظاهرة عادي.
                // لكن أي طلب اتعدل أو اتغيرت حالته داخل نافذة اليوم التشغيلي الحالية
                // يظهر في الأول، وبعده باقي الطلبات بنفس الترتيب المعتاد.
                var todayTenAM = now.Date.AddHours(10);
                var operationalUpdateStart = now >= todayTenAM
                    ? todayTenAM
                    : todayTenAM.AddDays(-1);
                var operationalUpdateEndExclusive = operationalUpdateStart.AddDays(1);

                // Apply filters using the QueryFilteringService
                // التاريخ هنا يمر فقط لو المستخدم اختاره بنفسه من الفلتر.
                // بدون اختيار تاريخ، لا يوجد فلتر تاريخ افتراضي والداتا كلها تظهر.
                query = _queryFilteringService.ApplyFilters(
                    query,
                    countryId,
                    orderstatusId,
                    ordersourceId,
                    storeId,
                    deliverycompanyId,
                    deliveryrepresentativeId,
                    productId,
                    startDate,
                    endDate,
                    cityId,
                    search,
                    employeeId,
                    fromcomments,
                    gender,
                    isOffers,
                    isDiscount,
                    isBonus,
                    isspecialClients,
                    isFixedAndDelivered,
                    isHidden,
                    IsComplaints,
                    IsPaid,
                    null,
                    sessionPrefix,
                    failureReason,
                    includeSourceNameSearch: true
                );

                // لا يوجد فلتر تاريخ افتراضي هنا. الفلتر الزمني الافتراضي تحول لترتيب فقط
                // حسب LastEditedDate داخل نافذة 10 صباحًا حتى 10 صباحًا.

                // فلتر الطلبات المكررة للأدمن والمدير التنفيذي فقط.
                // التعريف: نفس رقم الهاتف + نفس الدولة + نفس المدينة + نفس المتجر.
                if (!investigationMode
                    && isDuplicateOrders == true
                    && (User.IsInRole("Admin") || User.IsInRole("ExecutiveDirector")))
                {
                    query = query.Where(order =>
                        !string.IsNullOrWhiteSpace(order.TelephoneNumber)
                        && _context.Orders.Any(other =>
                            other.Id != order.Id
                            && other.TelephoneNumber == order.TelephoneNumber
                            && other.Country == order.Country
                            && other.State == order.State
                            && other.ManufacturingCompanyId == order.ManufacturingCompanyId));
                }


                // Apply pagination before transforming the data
                bool canUseOrderPinActionForCurrentUser =
                    User.IsInRole("Admin") ||
                    User.IsInRole("ExecutiveDirector") ||
                    User.IsInRole("FollowUpDepartment") ||
                    User.IsInRole("CallCenter");

                bool hasManualPinnedOrdersInCurrentQuery = canUseOrderPinActionForCurrentUser &&
                    await query.AnyAsync(o => o.IsPinned);

                ViewBag.HasManualPinnedOrders = hasManualPinnedOrdersInCurrentQuery;

                IQueryable<Order> orderedQuery = isCallCenterNoFilters
                    ? query.OrderByDescending(o => o.IsPinned)
                        .ThenByDescending(o => o.PinnedAt)
                        .ThenByDescending(o => o.CreatedDate)
                    : canUseOrderPinActionForCurrentUser
                        ? query
                            .OrderByDescending(o => o.LastEditedDate.HasValue
                                && o.LastEditedDate.Value >= operationalUpdateStart
                                && o.LastEditedDate.Value < operationalUpdateEndExclusive)
                            .ThenByDescending(o => o.LastEditedDate)
                            .ThenByDescending(o => o.IsPinned)
                            .ThenByDescending(o => o.PinnedAt)
                            .ThenByDescending(o => o.CreatedDate)
                        : query
                            .OrderByDescending(o => o.LastEditedDate.HasValue
                                && o.LastEditedDate.Value >= operationalUpdateStart
                                && o.LastEditedDate.Value < operationalUpdateEndExclusive)
                            .ThenByDescending(o => o.LastEditedDate)
                            .ThenByDescending(o => o.CreatedDate);
                string? debugQuery = (User.IsInRole("Admin") && showDebugQuery) ? orderedQuery.ToQueryString() : null;
                IQueryable<Order> paginatedQuery = investigationMode
                    ? orderedQuery
                    : isCallCenterNoFilters
                        ? orderedQuery.Take(1)
                        : orderedQuery.Skip((page - 1) * (pageSize ?? 10)).Take(pageSize ?? 10);


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
                         OrderSource = o.OrderSource,
                         chatUrl = o.Chaturl,
                         SourceName = o.SourceName,
                         Gender = o.Gender,
                         IsPaid = o.IsPaid,
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
                             Logo = o.DeliveryCompany.ImageUrl ?? "Static/DefaultImage.svg",
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
                         DeliveryPrice = o.DeliveryPrice,
                         IsPinned = o.IsPinned,
                         PinnedAt = o.PinnedAt,
                         PinnedByUserId = o.PinnedByUserId,
                     })
                      .ToList();


                // بيانات الطلبات التي تم فتحها/اعتمادها من صفحة قيد التحقق
                // لعرض اسم من فتح الطلب تحت اسم الموظف في الجدول مع وقت وتاريخ الفتح في Hover.
                var currentPageOrderIds = orders.Select(o => o.Id).ToList();

                // بادج "مؤجل مسبقا":
                // لو الطلب الحالي حالته طلب جديد لكن كان دخل حالة الطلبات المؤجلة قبل كده، نعرض بادج صغيرة فوق الحالة.
                ViewBag.PreviouslyDelayedOrderIds = await _context.OrderStatusHistories
                    .AsNoTracking()
                    .Where(history =>
                        history.OrderId.HasValue
                        && currentPageOrderIds.Contains(history.OrderId.Value)
                        && history.Status == OrderStatusEnum.الطلبات_المؤجلة)
                    .Select(history => history.OrderId.Value)
                    .Distinct()
                    .ToListAsync();

                // Sales indicators are calculated after the order is created and loaded in Home/Index.
                // Net selling price = TotalPrice - DeliveryPrice.
                // If the order has more than one product/quantity, the net price is averaged over the total quantity.
                var salesIndicatorDisplayMap = await BuildSalesIndicatorDisplayMapAsync(currentPageOrderIds);
                foreach (var order in orders)
                {
                    if (salesIndicatorDisplayMap.TryGetValue(order.Id, out var salesIndicatorDisplay))
                    {
                        order.SalesIndicatorState = salesIndicatorDisplay.State;
                        order.SalesIndicatorText = salesIndicatorDisplay.Text;
                        order.SalesIndicatorNetSellingPrice = salesIndicatorDisplay.NetSellingPrice;
                        order.SalesIndicatorAverageSellingPrice = salesIndicatorDisplay.AverageSellingPrice;
                    }
                }

                var currentPageInvestigationApprovals = await ValidInvestigationApprovalsQuery()
                    .AsNoTracking()
                    .Where(a => currentPageOrderIds.Contains(a.OrderId))
                    .OrderByDescending(a => a.ApprovedAt)
                    .Select(a => new
                    {
                        a.OrderId,
                        a.EmployeeName,
                        a.ApprovedAt
                    })
                    .ToListAsync();

                ViewBag.InvestigationOpenedBy = currentPageInvestigationApprovals
                    .GroupBy(a => a.OrderId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.First().EmployeeName ?? string.Empty
                    );

                ViewBag.InvestigationOpenedAt = currentPageInvestigationApprovals
                    .GroupBy(a => a.OrderId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.First().ApprovedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                    );


                // In CallCenter no-filter mode the table is intentionally capped at 1 row,
                // so the footer/pagination should reflect that — not the user's whole order count.
                var totalItems = isCallCenterNoFilters ? orders.Count : await query.CountAsync();

                // Calculate price totals using DB-side aggregation (no ToList)
                bool hasAnyFilter = countryId.HasValue || cityId != null || storeId.HasValue || deliverycompanyId.HasValue
                    || orderstatusId.HasValue || !string.IsNullOrEmpty(search) || !string.IsNullOrEmpty(employeeId)
                    || productId.HasValue || ordersourceId.HasValue || fromcomments.HasValue
                    || deliveryrepresentativeId.HasValue || gender.HasValue
                    || isDuplicateOrders == true;

                if (countryId.HasValue)
                {
                    var totals = await query.GroupBy(o => 1)
                        .Select(g => new { TotalPrice = g.Sum(o => o.TotalPrice), TotalDelivery = g.Sum(o => o.DeliveryPrice) })
                        .FirstOrDefaultAsync();
                    if (totals != null)
                    {
                        totalOrderPrice = totals.TotalPrice - totals.TotalDelivery;
                        totalOrderPriceDollar = _currencyExchangeService.ConvertToUSD(totalOrderPrice, countryId.ToString());
                        totalOrderPriceTRY = _currencyExchangeService.ConvertToTurkishLira(totalOrderPriceDollar);
                    }
                    SelectedCoutnryfromfilter = countryId.ToString();
                }
                else if (hasAnyFilter)
                {
                    var countryTotals = await query.GroupBy(o => o.Country)
                        .Select(g => new { Country = g.Key, TotalPrice = g.Sum(o => o.TotalPrice), TotalDelivery = g.Sum(o => o.DeliveryPrice) })
                        .ToListAsync();
                    foreach (var ct in countryTotals)
                    {
                        var netPrice = ct.TotalPrice - ct.TotalDelivery;
                        totalOrderPriceDollar += _currencyExchangeService.ConvertToUSD(netPrice, ct.Country.ToString());
                    }
                    totalOrderPriceTRY = _currencyExchangeService.ConvertToTurkishLira(totalOrderPriceDollar);
                }

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

                var orderStatusesForEmployees = _dataCacheService.GetCachedOrderStatusesForEmployees();


                var countries = _dataCacheService.GetCachedCountries();

                // Create an instance of the HomeViewModel and populate it
                var viewModel = new HomeViewModel
                {
                    PaginationViewModel = paginationViewModel,
                    OrderStatuses = orderStatuses,
                    OrderStatusesForDeliveryCompanyAndRepresentative = orderStatusesForDeliveryCompanyAndRepresentative,
                    OrderStatusesForEmployees = orderStatusesForEmployees,
                    Countries = countries,
                    TotalOrderPrice = _decimalFormattingService.DecimalFormat(totalOrderPrice),
                    TotalOrderPriceDollar = _decimalFormattingService.DecimalFormat(totalOrderPriceDollar),
                    TotalOrderPriceTRY = _decimalFormattingService.DecimalFormat(totalOrderPriceTRY),
                    SelectedCoutnry = SelectedCoutnryfromfilter,
                    OrderStatusIconUrls = orderStatusIconUrls,
                    CountryImageUrls = countryImageUrls,
                    SocialMediaIconUrls = socialMediaIconUrls,
                    CurrencySymbols = currencySymbols,
                    DebugQuery = debugQuery,
                    IsCallCenterNoFilters = isCallCenterNoFilters,
                    IsCallCenter = isCallCenter,
                };

                var loggedInUser = await _userManager.GetUserAsync(User);
                viewModel.UserName = loggedInUser?.Name;

                if (investigationMode)
                {
                    ViewBag.SelectedInvestigationEmployeeId = employeeId ?? string.Empty;

                    // فلتر الموظفين في صفحة قيد التحقيق:
                    // نعرض فقط الموظفين النشطين IsActive = 1
                    // وكمان يكون لهم طلبات ظاهرة في قيد التحقيق، عشان القائمة ما تجيبش كل موظفين السيستم.
                    var activeInvestigationEmployeeIds = await BuildNewOrdersUnderInvestigationQuery(currentUser, investigationOpenedOrderIds)
                        .Where(o => !string.IsNullOrEmpty(o.ApplicationUserId))
                        .Select(o => o.ApplicationUserId)
                        .Distinct()
                        .ToListAsync();

                    ViewBag.InvestigationEmployees = await _context.Employees
                        .AsNoTracking()
                        .Where(e =>
                            e.IsActive == true &&
                            !string.IsNullOrEmpty(e.ApplicationUserId) &&
                            activeInvestigationEmployeeIds.Contains(e.ApplicationUserId))
                        .OrderBy(e => e.DisplayName)
                        .Select(e => new SelectListItem
                        {
                            Value = e.ApplicationUserId,
                            Text = e.DisplayName
                        })
                        .ToListAsync();
                }

                return View(investigationMode ? "NewOrdersUnderInvestigation" : "Index", viewModel); // في وضع قيد التحقيق نفتح صفحة منفصلة بنفس جدول وفانكشن الاندكس
            }
            catch (Exception ex)
            {
                Response.StatusCode = 500;
                return Content($"[DEBUG ERROR] {ex.GetType().Name}: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}\n\nInner: {ex.InnerException?.Message}");
            }
        }



        [Authorize(Roles = "Observer,ExecutiveDirector,OrderPreparer,FollowUpDepartment,CallCenter,Admin,Accountant,WareHouse")]
        public IActionResult OrderTypeChooser()
        {
            return PartialView("_OrderTypeChooser");
        }

        // Lightweight JSON search used by the order-details modal search bar.
        // Mirrors the search predicates in QueryFilteringService.ApplyFilters (phone / source name / state,
        // with *N => Id and -N => ExternalOrderId), but skips session writes and all other filters.
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> SearchOrders(string? search, int limit = 100)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return Json(new { results = Array.Empty<object>(), count = 0 });
            }

            if (limit < 1) limit = 1;
            if (limit > 100) limit = 100;

            var term = search.Trim().ToLower();

            IQueryable<Order> q = _context.Orders.AsNoTracking();

            if (term.StartsWith("*") && int.TryParse(term.Trim('*'), out var searchId))
            {
                q = q.Where(o => o.Id == searchId);
            }
            else if (term.StartsWith("-") && int.TryParse(term.Trim('-'), out var externalId))
            {
                q = q.Where(o => o.ExternalOrderId == externalId);
            }
            else
            {
                var phoneTerm = OrderController.NormalizePhone(term);
                var nameTerm = term;

                // البحث برقم الطلب مباشرة بدون الحاجة لكتابة *
                // وأيضًا يظل البحث شغال برقم الهاتف واسم الصفحة.
                if (int.TryParse(term, out var normalOrderNumber))
                {
                    q = q.Where(o =>
                        o.Id == normalOrderNumber
                        || o.ExternalOrderId == normalOrderNumber
                        || o.TelephoneNumber.Contains(phoneTerm)
                        || (o.SourceName != null && o.SourceName.ToLower().Contains(nameTerm)));
                }
                else
                {
                    q = q.Where(o => o.TelephoneNumber.Contains(phoneTerm)
                        || (o.SourceName != null && o.SourceName.ToLower().Contains(nameTerm)));
                }
            }

            // Count once so the client can decide "1 → open it" vs "N → narrow it down"
            // without another round trip. Cap work with Take(limit + 1) for the list.
            var totalCount = await q.CountAsync();

            var results = await q
                .OrderByDescending(o => o.Id)
                .Take(limit)
                .Select(o => new
                {
                    id = o.Id,
                    telephoneNumber = o.TelephoneNumber,
                    sourceName = o.SourceName,
                    state = o.State,
                    country = o.Country.ToString(),
                    orderStatus = o.OrderStatus.ToString()
                })
                .ToListAsync();

            return Json(new { results, count = totalCount });
        }

        // done
        [Authorize(Roles = "Observer,ExecutiveDirector,OrderPreparer,FollowUpDepartment,CallCenter,Admin,Accountant,WareHouse")]
        public IActionResult CreateOrder()
        {

            return PartialView("_CreateOrder");
        }



        [Authorize]
        [HttpGet]
        public async Task<IActionResult> EditOrder(int orderId)
        {
            OrderViewModel orderViewModel = null;

            var orderToEdit = _context.Orders
                .Include(a => a.ManufacturingCompany)
                .Include(o => o.OrderWarehouses)
                        .ThenInclude(ow => ow.Warehouse) // Ensure Warehouse is also loaded

                .FirstOrDefault(o => o.Id == orderId);

            if (orderToEdit == null)
            {
                return NotFound();
            }

            if (!User.IsInRole("Admin") && !User.IsInRole("FollowUpDepartment") && !User.IsInRole("ExecutiveDirector"))
            {
                bool canEdit = orderToEdit.OrderStatus == OrderStatusEnum.طلب_جديد
                    || orderToEdit.OrderStatus == OrderStatusEnum.تم_المعالجة
                    || orderToEdit.OrderStatus == OrderStatusEnum.الطلبات_المؤجلة
                    || OrderStatusHelper.IsFailureStatus(orderToEdit.OrderStatus);

                if (!canEdit)
                {
                    return Json(new { message = "تسطيع التعديل عند انتظار التجهيز فقط" });
                }
            }


            // Get the selected warehouses for the order
            var selectedWarehouses = orderToEdit.OrderWarehouses != null
                ? orderToEdit.OrderWarehouses.Where(ow => ow != null && ow.Warehouse != null).Select(ow => new WarehouseAmountViewModel
                {
                    WarehouseId = ow.Warehouse.Id,
                    WarehouseName = ow.Warehouse.Name,
                    Amount = ow.Amount,
                    RemainingAmount = ow.Warehouse.Amount,
                }).ToList()
                : new List<WarehouseAmountViewModel>();

            // Ensure there is at least one WarehouseAmountViewModel item if the list is empty
            if (!selectedWarehouses.Any())
            {
                selectedWarehouses.Add(new WarehouseAmountViewModel
                {
                    WarehouseId = 0, // Assuming '0' is a safe default. Adjust as necessary.
                    WarehouseName = "مستودع فاضي ",
                    Amount = 0,
                    RemainingAmount = 0
                });
            }

            // Create an instance of your OrderViewModel and pass it to the view
            // ---- Stores ----
            var storeQuery = _context.ManufacturingCompanies.Where(a => a.IsShown).AsQueryable();
            var availableStores = storeQuery.Select(mc => new StoreOptionVm
            {
                id = mc.Id,
                name = mc.Name,
                logoUrl = mc.ImageUrl ?? "static/DefaultImage.svg",
                mainWarehouseId = mc.MainWarehouseId
            }).ToList();

            // ---- Delivery parties (companies + representatives) for this country/city ----
            // Companies are country-scoped; representatives are country+city-scoped. Companies first, then reps.
            var availableDeliveryCompanies = _context.DeliveryCompanies
                .Where(dc => dc.IsShown && !dc.IsRepresentative && dc.Country == orderToEdit.Country)
                .Select(dc => new DeliveryCompanyOptionVm
                {
                    id = dc.Id,
                    name = dc.Name,
                    logoUrl = dc.ImageUrl ?? "static/DefaultImage.svg",
                    isRepresentative = false
                }).ToList();

            var availableDeliveryRepresentatives = _context.DeliveryCompanies
                .Where(dc => dc.IsShown && dc.IsRepresentative && dc.Country == orderToEdit.Country && dc.City == orderToEdit.State)
                .Select(dc => new DeliveryCompanyOptionVm
                {
                    id = dc.Id,
                    name = dc.Name,
                    logoUrl = dc.ImageUrl ?? "static/DefaultImage.svg",
                    isRepresentative = true
                }).ToList();

            var availableDeliveryParties = availableDeliveryCompanies.Concat(availableDeliveryRepresentatives).ToList();

            // ---- Campaigns for this country ----
            var availableCampaigns = _context.Campaigns
                .Where(c => c.Country == orderToEdit.Country && c.IsActive)
                .Select(c => new CampaignOptionVm
                {
                    id = c.Id,
                    name = c.Name,
                    imageUrl = c.ImageUrl,
                    manufacturingCompanyId = c.ManufacturingCompanyId
                }).OrderBy(c => c.name).ToList();

            // ---- Warehouses for the order's delivery company ----
            var availableWarehouses = _context.Warehouses
                .Where(w => w.IsShown && w.Amount > 0 && w.DeliveryCompanyId == orderToEdit.DeliveryCompanyId)
                .Select(w => new WarehouseOptionVm
                {
                    id = w.Id,
                    name = w.Name,
                    amount = w.Amount,
                    productImage = w.MainWarehouse.ImageUrl ?? "static/DefaultImage.svg",
                    mainWarehouseId = w.MainWarehouseId
                }).ToList();

            orderViewModel = new OrderViewModel
            {
                Id = orderToEdit.Id,
                CreatedDate = orderToEdit.CreatedDate,
                SelectedWarehouses = selectedWarehouses,
                Country = orderToEdit.Country,
                State = orderToEdit.State,
                OrderSource = orderToEdit.OrderSource,
                SourceName = orderToEdit.SourceName,
                ManufacturingCompanyId = orderToEdit.ManufacturingCompanyId,
                DeliveryCompanyId = orderToEdit.DeliveryCompanyId,
                TelephoneNumber = orderToEdit.TelephoneNumber,
                SecondTelephoneNumber = orderToEdit.SecondTelephoneNumber,
                CustomerName = orderToEdit.CustomerName,
                Notes = orderToEdit.Notes,
                Address = orderToEdit.Address,
                TotalPrice = orderToEdit.TotalPrice,
                LastEditedDate = _timeService.GetIstanbulTimeWithOffset(),
                Gender = orderToEdit.Gender,
                FromComments = orderToEdit.FromComments,
                IsPaid = orderToEdit.IsPaid,
                DeliveryPrice = orderToEdit.DeliveryPrice,
                ManufacturingCompanyName = orderToEdit.ManufacturingCompany.Name,
                ManufacturingCompanylogo = orderToEdit.ManufacturingCompany.ImageUrl,
                chatUrl = orderToEdit.Chaturl,
                CampaignId = orderToEdit.CampaignId,
                PhotoUrl = orderToEdit.PhotoUrl,
                PaymentReceiptUrl = orderToEdit.PaymentReceiptUrl,
                ManufacturingCompany = orderToEdit.ManufacturingCompany != null ? new ManufacturingCompanyViewModel
                {
                    Id = orderToEdit.ManufacturingCompany.Id,
                    Name = orderToEdit.ManufacturingCompany.Name,
                } : null,
                AvailableStores = availableStores,
                AvailableDeliveryParties = availableDeliveryParties,
                AvailableCampaigns = availableCampaigns,
                AvailableWarehouses = availableWarehouses,
            };


            return PartialView("_EditOrder", orderViewModel);
        }


        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ResendOrder(int orderId)
        {
            OrderViewModel orderViewModel = null;

            // Retrieve the order to edit from the database based on orderId
            var ordeToResend = _context.Orders
                .Include(o => o.OrderWarehouses)
                .ThenInclude(ow => ow.Warehouse)
                .SingleOrDefault(o => o.Id == orderId);

            if (ordeToResend == null)
            {
                return NotFound();
            }



            // Create an instance of your OrderViewModel and pass it to the view
            orderViewModel = new OrderViewModel
            {
                Id = ordeToResend.Id,
                CreatedDate = ordeToResend.CreatedDate,
                Country = ordeToResend.Country,
                State = ordeToResend.State,
                OrderSource = ordeToResend.OrderSource,
                SourceName = ordeToResend.SourceName,
                ManufacturingCompanyId = ordeToResend.ManufacturingCompanyId,
                DeliveryCompanyId = ordeToResend.DeliveryCompanyId,
                TelephoneNumber = ordeToResend.TelephoneNumber,
                SecondTelephoneNumber = ordeToResend.SecondTelephoneNumber,
                CustomerName = ordeToResend.CustomerName,
                Notes = ordeToResend.Notes,
                Address = ordeToResend.Address,
                TotalPrice = ordeToResend.TotalPrice,
                ExternalOrderId = ordeToResend.ExternalOrderId,
                LastEditedDate = _timeService.GetIstanbulTimeWithOffset(),

            };



            // Populate dropdown lists or perform any necessary setup, just like in CreateOrder
            return PartialView("_ResendOrder", orderViewModel);
        }


        // done
        public IActionResult Privacy()
        {
            return View();
        }
        // done
        public IActionResult HelpCenter()
        {
            return View();
        }
        // done
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        // done 
        public IActionResult Test()
        {
            return View();
        }

        // done
        [Authorize]
        // get cities 
        public IActionResult GetCities(Common.Countries country)
        {
            var cities = Common.CitiesByCountry.ContainsKey(country) ? Common.CitiesByCountry[country] : new List<string>();
            return Json(cities);
        }


        // done 
        [Authorize]
        public ActionResult GetFailedOrders(Common.Countries? countryId
   )
        {
            IQueryable<Order> query = _context.Orders
                .Where(order => order.OrderStatus == OrderStatusEnum.فشل_التسليم);

            // Apply the generic filters
            query = _queryFilteringService.ApplyFilters(
                query,
                countryId,
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
                null,
                null
            );

            var failedOrdersCount = query.Count();

            var failedOrdersFiltered = query
                .Select(order => new
                {
                    LastEditedDate = order.LastEditedDate.HasValue ? order.LastEditedDate.Value.ToString("yyyy-MM-dd") : null,
                    ManufacturerCompanyName = order.ManufacturingCompany.Name,
                    DeliveryCompanyName = order.DeliveryCompany.Name,
                    Id = order.Id,
                    Country = order.Country.ToString(),
                    FailureReason = _context.OrderStatusHistories
                        .Where(history => history.OrderId == order.Id && history.Status == OrderStatusEnum.فشل_التسليم)
                        .OrderByDescending(history => history.CreatedAt)
                        .Select(history => history.Reason)
                        .FirstOrDefault()
                })
                .ToList();

            return Json(new
            {
                Count = failedOrdersCount,
                Orders = failedOrdersFiltered
            });
        }



        // done
        // done
        [Authorize]
        public ActionResult GetFixedOrders(
            Common.Countries? countryId = null,
            OrderSourceEnum? ordersourceId = null,
            string? cityId = null,
            int? storeId = null,
            int? deliveryCompanyId = null,
            string? employeeId = null,
            string? productname = null,
            int? hoursrange = null,
            bool? fromcomments = null,
            bool? gender = null,
             DateTime? startDate = null,
             DateTime? endDate = null

        )
        {
            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier); // Get the current user

            IQueryable<Order> query = _context.Orders
                .Where(order => order.OrderStatus == OrderStatusEnum.تم_المعالجة && order.DeliveryCompany.UserId == currentUser);



            // Apply the filters using the QueryFilteringService
            query = _queryFilteringService.ApplyFilters(
                query,
                countryId,
                null, // No specific order status, since we're already filtering by "تم_المعالجة"
                ordersourceId,
                storeId,
                deliveryCompanyId,
                null, // No delivery representative filter
                null, // No product ID filter
                startDate,
                endDate,
                cityId,
                productname,
                employeeId,
                fromcomments,
                gender,
                null, // No isOffers filter
                null, // No isDiscount filter
                null, // No isBonus filter
                null, // No isSpecialClients filter
                null, // No isFixedAndDelivered filter
                null, // No isHidden filter
                null, // No isComplaints filter
                null  // No isPaid filter
            );

            var FixedOrders = query
                .Select(order => new
                {
                    LastEditedDate = order.LastEditedDate.HasValue ? order.LastEditedDate.Value.ToString("yyyy-MM-dd") : null,
                    ManufacturerCompanyName = order.ManufacturingCompany.Name,
                    DeliveryCompanyName = order.DeliveryCompany.Name,
                    Id = order.Id,
                    Country = order.Country.ToString()
                })
                .ToList();

            return Json(FixedOrders);
        }


        [Authorize(Roles = "Observer,ExecutiveDirector,OrderPreparer,FollowUpDepartment,CallCenter,Admin,Accountant,WareHouse")]
        public IActionResult CreatePotentialClient()
        {
            return PartialView("_CreatePotentialClient");
        }

        [HttpPost]
        [Authorize(Roles = "Admin,CallCenter,FollowUpDepartment,ExecutiveDirector")]
        public async Task<IActionResult> CreatePotentialClient(string? customerName, string country, string? chatUrl, int storeId, string? phoneNumber, int orderSource)
        {
            bool isWhatsApp = orderSource == (int)OrderSourceEnum.واتساب;

            if ((!isWhatsApp && string.IsNullOrWhiteSpace(customerName)) || string.IsNullOrWhiteSpace(country) || storeId == 0)
                return new JsonResult(new { message = "يرجى تعبئة جميع الحقول" }) { StatusCode = 400 };

            if (isWhatsApp && string.IsNullOrWhiteSpace(phoneNumber))
                return new JsonResult(new { message = "رقم الهاتف مطلوب لطلبات واتساب" }) { StatusCode = 400 };

            if (!isWhatsApp && string.IsNullOrWhiteSpace(chatUrl))
                return new JsonResult(new { message = "رابط المحادثة مطلوب" }) { StatusCode = 400 };

            if (!Enum.TryParse<Common.Countries>(country, out var parsedCountry))
                return new JsonResult(new { message = "البلد غير صالح" }) { StatusCode = 400 };

            if (!Enum.IsDefined(typeof(OrderSourceEnum), orderSource))
                return new JsonResult(new { message = "نوع الصفحة غير صالح" }) { StatusCode = 400 };

            // Normalize once so the value used for duplicate-checks AND the value stored share one canonical form.
            // Order.TelephoneNumber is stored via NormalizePhone, so the cross-system lookup below must compare normalized-to-normalized.
            var normalizedPhone = OrderController.NormalizePhone(phoneNumber);

            var storeName = await _context.ManufacturingCompanies
                .Where(m => m.Id == storeId)
                .Select(m => m.Name)
                .FirstOrDefaultAsync();
            if (storeName == null)
                return new JsonResult(new { message = "المتجر غير صالح" }) { StatusCode = 400 };

            if (isWhatsApp)
            {
                var existingPotential = await _context.PotentialOrders
                    .FirstOrDefaultAsync(p => p.PhoneNumber == normalizedPhone && p.Country == parsedCountry && p.StoreName == storeName);
                if (existingPotential != null)
                    return new JsonResult(new { message = $"يوجد طلب محتمل بنفس المعطيات رقم {existingPotential.Id}" }) { StatusCode = 400 };

                var existingOrder = await _context.Orders
                    .Include(o => o.ManufacturingCompany)
                    .FirstOrDefaultAsync(o => o.TelephoneNumber == normalizedPhone && o.Country == parsedCountry && o.ManufacturingCompany.Name == storeName);
                if (existingOrder != null)
                    return new JsonResult(new { message = $"لا يمكن ادخال الرقم،، موجود مسبقا بكود - {existingOrder.Id}" }) { StatusCode = 400 };
            }
            else
            {
                var existingPotential = await _context.PotentialOrders
                    .FirstOrDefaultAsync(p => p.ChatUrl == chatUrl && p.Country == parsedCountry && p.StoreName == storeName);
                if (existingPotential != null)
                    return new JsonResult(new { message = $"يوجد طلب محتمل بنفس المعطيات رقم {existingPotential.Id}" }) { StatusCode = 400 };

                var existingOrder = await _context.Orders
                    .Include(o => o.ManufacturingCompany)
                    .FirstOrDefaultAsync(o => o.Chaturl == chatUrl && o.Country == parsedCountry && o.ManufacturingCompany.Name == storeName);
                if (existingOrder != null)
                    return new JsonResult(new { message = $"لا يمكن ادخال الرابط،، موجود مسبقا بكود - {existingOrder.Id}" }) { StatusCode = 400 };
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var now = _timeService.GetIstanbulTimeWithOffset();

            var potentialOrder = new PotentialOrder
            {
                CustomerName = customerName,
                Country = parsedCountry,
                ChatUrl = isWhatsApp ? null : chatUrl,
                StoreName = storeName,
                PhoneNumber = string.IsNullOrWhiteSpace(normalizedPhone) ? null : normalizedPhone,
                OrderSource = (OrderSourceEnum)orderSource,
                Status = PotentialOrderStatus.عميل_محتمل,
                CreatedDate = now,
                LastEditedDate = now,
                ApplicationUserId = userId
            };

            _context.PotentialOrders.Add(potentialOrder);
            await _context.SaveChangesAsync();

            var user = await _userManager.GetUserAsync(User);
            // Expanded SignalR payload: the PotentialOrder/Index table needs image URLs and formatted
            // strings to build the new row client-side without a page reload.
            // Previously the payload only carried Id/CustomerName/Country/StoreName/ChatUrl/PhoneNumber/OrderSource,
            // which was only enough for the browser notification — not enough to render a full table row.
            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.ApplicationUserId == userId);
            var storeLogoUrl = await _context.ManufacturingCompanies
                .Where(m => m.Id == storeId)
                .Select(m => m.ImageUrl)
                .FirstOrDefaultAsync();
            var potentialOrderDetails = new
            {
                PotentialOrder = new
                {
                    potentialOrder.Id,
                    potentialOrder.CustomerName,
                    Country = potentialOrder.Country.ToString(),
                    CountryImageUrl = Common.GetImageUrlByCountryName(potentialOrder.Country.ToString()),
                    potentialOrder.StoreName,
                    StoreLogoUrl = storeLogoUrl ?? "",
                    potentialOrder.ChatUrl,
                    potentialOrder.PhoneNumber,
                    OrderSource = potentialOrder.OrderSource.ToString(),
                    OrderSourceIconUrl = Common.GetSocialMediaIconUrl(potentialOrder.OrderSource),
                    // Status is always عميل_محتمل on creation — format it the same way the Razor view does
                    Status = potentialOrder.Status.ToString().Replace("_", " "),
                    // StatusInt drives the po-status-{n} CSS class in addPotentialOrderToTable
                    StatusInt = (int)potentialOrder.Status,
                    CreatedDate = potentialOrder.CreatedDate.ToString("yyyy-MM-dd"),
                    LastEditedDate = potentialOrder.LastEditedDate?.ToString("yyyy-MM-dd") ?? "-",
                },
                EmployeeName = employee?.DisplayName ?? user?.Name ?? "غير معروف",
                EmployeeImage = employee?.ImageUrl ?? ""
            };

            var potentialOrderJson = JsonConvert.SerializeObject(potentialOrderDetails, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });

            await _hubContext.Clients.Group("UsersExpectDelivery").SendAsync("NotifyPotentialOrderAdded", potentialOrderJson);

            return Json(new { id = potentialOrder.Id });
        }

        [HttpGet]
        [Authorize(Roles = "Admin,FollowUpDepartment,ExecutiveDirector,CallCenter")]
        public async Task<IActionResult> NewOrdersUnderInvestigation(string? employeeId = null)
        {
            return await Index(
                page: 1,
                pageSize: int.MaxValue,
                employeeId: employeeId,
                orderstatusId: null,
                investigationMode: true
            );
        }

        private static void AddDbParameter(System.Data.Common.DbCommand command, string name, object? value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        private IQueryable<OrderInvestigationApproval> ValidInvestigationApprovalsQuery()
        {
            return _context.OrderInvestigationApprovals.Where(a =>
                _context.UserRoles.Any(ur => ur.UserId == a.ApplicationUserId &&
                    _context.Roles.Any(r => r.Id == ur.RoleId &&
                        (r.Name == "Admin" || r.Name == "ExecutiveDirector" || r.Name == "FollowUpDepartment"))));
        }

        private async Task<List<int>> GetInvestigationOpenedOrderIdsAsync()
        {
            var result = new List<int>();
            var connection = _context.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;

            try
            {
                if (shouldCloseConnection)
                {
                    await connection.OpenAsync();
                }

                using var command = connection.CreateCommand();
                command.CommandText = @"
SELECT DISTINCT [OrderId]
FROM [OrderInvestigationOpenings]
WHERE [OrderId] IS NOT NULL";

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                    {
                        result.Add(reader.GetInt32(0));
                    }
                }
            }
            catch
            {
                // لو جدول الفتح غير موجود في أي بيئة قديمة، لا نوقف الصفحة.
                result.Clear();
            }
            finally
            {
                if (shouldCloseConnection && connection.State == System.Data.ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }

            return result;
        }

        private async Task<List<int>> GetInvestigationOpenedOrderIdsByUserAsync(string? applicationUserId)
        {
            var result = new List<int>();

            if (string.IsNullOrWhiteSpace(applicationUserId))
            {
                return result;
            }

            var connection = _context.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;

            try
            {
                if (shouldCloseConnection)
                {
                    await connection.OpenAsync();
                }

                using var command = connection.CreateCommand();
                command.CommandText = @"
SELECT DISTINCT [OrderId]
FROM [OrderInvestigationOpenings]
WHERE [OrderId] IS NOT NULL
  AND [ApplicationUserId] = @ApplicationUserId";

                AddDbParameter(command, "@ApplicationUserId", applicationUserId);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                    {
                        result.Add(reader.GetInt32(0));
                    }
                }
            }
            catch
            {
                result.Clear();
            }
            finally
            {
                if (shouldCloseConnection && connection.State == System.Data.ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }

            return result;
        }

        private async Task<bool> TryMarkOrderOpenedForInvestigationAsync(int orderId, string? currentUser)
        {
            if (orderId <= 0)
            {
                return false;
            }

            var invalidExistingApprovals = await _context.OrderInvestigationApprovals
                .Where(a => a.OrderId == orderId &&
                    !_context.UserRoles.Any(ur => ur.UserId == a.ApplicationUserId &&
                        _context.Roles.Any(r => r.Id == ur.RoleId &&
                            (r.Name == "Admin" || r.Name == "ExecutiveDirector" || r.Name == "FollowUpDepartment"))))
                .ToListAsync();

            if (invalidExistingApprovals.Any())
            {
                _context.OrderInvestigationApprovals.RemoveRange(invalidExistingApprovals);
                await _context.SaveChangesAsync();
            }

            var loggedInUser = await _userManager.GetUserAsync(User);

            var employeeName = await _context.Employees
                .AsNoTracking()
                .Where(e => e.ApplicationUserId == currentUser)
                .Select(e => e.DisplayName)
                .FirstOrDefaultAsync();

            var openedBy = !string.IsNullOrWhiteSpace(employeeName)
                ? employeeName
                : loggedInUser?.Name ?? User.Identity?.Name ?? "غير معروف";

            object openedAtValue = _timeService.GetIstanbulTimeWithOffset();
            if (openedAtValue is DateTimeOffset openedAtOffset)
            {
                openedAtValue = openedAtOffset.DateTime;
            }

            var connection = _context.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;

            try
            {
                if (shouldCloseConnection)
                {
                    await connection.OpenAsync();
                }

                using var command = connection.CreateCommand();
                command.CommandText = @"
IF NOT EXISTS (
    SELECT 1
    FROM [OrderInvestigationOpenings]
    WHERE [OrderId] = @OrderId
      AND [ApplicationUserId] = @ApplicationUserId
)
BEGIN
    INSERT INTO [OrderInvestigationOpenings] ([OrderId], [ApplicationUserId], [EmployeeName], [OpenedAt])
    VALUES (@OrderId, @ApplicationUserId, @EmployeeName, @OpenedAt)
END";

                AddDbParameter(command, "@OrderId", orderId);
                AddDbParameter(command, "@ApplicationUserId", currentUser ?? string.Empty);
                AddDbParameter(command, "@EmployeeName", openedBy);
                AddDbParameter(command, "@OpenedAt", openedAtValue);

                await command.ExecuteNonQueryAsync();
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (shouldCloseConnection && connection.State == System.Data.ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private IQueryable<Order> BuildNewOrdersUnderInvestigationQuery(
            string? currentUser,
            IReadOnlyCollection<int>? openedOrderIds = null,
            bool includeChangedStatus = false,
            IReadOnlyCollection<int>? openedByCurrentUserOrderIds = null)
        {
            var validInvestigationApprovalsQuery = ValidInvestigationApprovalsQuery();

            IQueryable<Order> query = _context.Orders
                .AsNoTracking()
                .Where(o => !validInvestigationApprovalsQuery.Any(a => a.OrderId == o.Id));

            // العدادات وقائمة قيد التحقق تعرض:
            // 1) للمدير/الأدمن/المتابعة: الطلبات الجديدة، أو الطلبات التي تم فتحها من قيد التحقق حتى لو تغيرت حالتها.
            // 2) للكول سنتر: طلباته الجديدة فقط، ولو فتح طلبًا يختفي من عنده فقط ولا يعتبر اعتماد.
            if (!includeChangedStatus)
            {
                if (User.IsInRole("CallCenter"))
                {
                    query = query.Where(o => o.OrderStatus == OrderStatusEnum.طلب_جديد);

                    var openedByCurrentUserIds = openedByCurrentUserOrderIds?
                        .Where(id => id > 0)
                        .Distinct()
                        .ToList() ?? new List<int>();

                    if (openedByCurrentUserIds.Any())
                    {
                        query = query.Where(o => !openedByCurrentUserIds.Contains(o.Id));
                    }
                }
                else
                {
                    var openedIds = openedOrderIds?
                        .Where(id => id > 0)
                        .Distinct()
                        .ToList() ?? new List<int>();

                    if (openedIds.Any())
                    {
                        query = query.Where(o =>
                            o.OrderStatus == OrderStatusEnum.طلب_جديد ||
                            openedIds.Contains(o.Id));
                    }
                    else
                    {
                        query = query.Where(o => o.OrderStatus == OrderStatusEnum.طلب_جديد);
                    }
                }
            }

            if (User.IsInRole("FollowUpDepartment") || User.IsInRole("CallCenter"))
            {
                query = query.Where(o =>
                    o.ManufacturingCompany.EmployeeManufacturingCompanies
                        .Any(a => a.ApplicationUserId == currentUser && a.CanSeeManufacturingCompany));
            }

            // الكول سنتر يشوف طلباته فقط في قيد التحقق وفي العدادات.
            if (User.IsInRole("CallCenter"))
            {
                query = query.Where(o => o.ApplicationUserId == currentUser);
            }

            return query;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,FollowUpDepartment,ExecutiveDirector,CallCenter")]
        public async Task<IActionResult> MarkOrderOpenedForInvestigation(int orderId)
        {
            if (orderId <= 0)
            {
                return Json(new
                {
                    success = false,
                    message = "رقم الطلب غير صحيح"
                });
            }

            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var openedOrderIds = await GetInvestigationOpenedOrderIdsAsync();
            var openedByCurrentUserOrderIds = User.IsInRole("CallCenter")
                ? await GetInvestigationOpenedOrderIdsByUserAsync(currentUser)
                : new List<int>();

            var orderExists = await BuildNewOrdersUnderInvestigationQuery(
                    currentUser,
                    openedOrderIds,
                    includeChangedStatus: true,
                    openedByCurrentUserOrderIds: openedByCurrentUserOrderIds)
                .AnyAsync(o => o.Id == orderId);

            if (!orderExists)
            {
                return Json(new
                {
                    success = false,
                    message = "هذا الطلب غير موجود في قيد التحقق أو تم اعتماده من قبل"
                });
            }

            var marked = await TryMarkOrderOpenedForInvestigationAsync(orderId, currentUser);

            if (marked && User.IsInRole("CallCenter"))
            {
                openedByCurrentUserOrderIds = await GetInvestigationOpenedOrderIdsByUserAsync(currentUser);
            }

            var remainingCount = await BuildNewOrdersUnderInvestigationQuery(
                    currentUser,
                    openedOrderIds,
                    openedByCurrentUserOrderIds: openedByCurrentUserOrderIds)
                .CountAsync();

            return Json(new
            {
                success = marked,
                remainingCount
            });
        }

        [HttpGet]
        [Authorize(Roles = "Admin,FollowUpDepartment,ExecutiveDirector,CallCenter")]
        public async Task<IActionResult> GetNewOrdersUnderInvestigationCount()
        {
            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var openedOrderIds = await GetInvestigationOpenedOrderIdsAsync();
            var openedByCurrentUserOrderIds = User.IsInRole("CallCenter")
                ? await GetInvestigationOpenedOrderIdsByUserAsync(currentUser)
                : new List<int>();
            var count = await BuildNewOrdersUnderInvestigationQuery(
                    currentUser,
                    openedOrderIds,
                    openedByCurrentUserOrderIds: openedByCurrentUserOrderIds)
                .CountAsync();

            return Json(new
            {
                success = true,
                count
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,FollowUpDepartment,ExecutiveDirector")]
        public async Task<IActionResult> ApproveNewOrderInvestigation(int orderId)
        {
            if (orderId <= 0)
            {
                return Json(new
                {
                    success = false,
                    message = "رقم الطلب غير صحيح"
                });
            }

            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var existingApproval = await ValidInvestigationApprovalsQuery()
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.OrderId == orderId);

            if (existingApproval != null)
            {
                var openedOrderIdsAlreadyApproved = await GetInvestigationOpenedOrderIdsAsync();
                var remainingAlready = await BuildNewOrdersUnderInvestigationQuery(currentUser, openedOrderIdsAlreadyApproved).CountAsync();

                return Json(new
                {
                    success = true,
                    alreadyApproved = true,
                    approvedBy = existingApproval.EmployeeName,
                    remainingCount = remainingAlready
                });
            }
            var openedOrderIds = await GetInvestigationOpenedOrderIdsAsync();
            var orderExists = await BuildNewOrdersUnderInvestigationQuery(currentUser, openedOrderIds, includeChangedStatus: true)
                .AnyAsync(o => o.Id == orderId);

            if (!orderExists)
            {
                return Json(new
                {
                    success = false,
                    message = "هذا الطلب غير موجود في قيد التحقيق أو تم اعتماده من قبل"
                });
            }

            var loggedInUser = await _userManager.GetUserAsync(User);

            var employeeName = await _context.Employees
                .AsNoTracking()
                .Where(e => e.ApplicationUserId == currentUser)
                .Select(e => e.DisplayName)
                .FirstOrDefaultAsync();

            var approvedBy = !string.IsNullOrWhiteSpace(employeeName)
                ? employeeName
                : loggedInUser?.Name ?? User.Identity?.Name ?? "غير معروف";

            var approval = new OrderInvestigationApproval
            {
                OrderId = orderId,
                ApplicationUserId = currentUser ?? string.Empty,
                EmployeeName = approvedBy,
                ApprovedAt = _timeService.GetIstanbulTimeWithOffset()
            };

            _context.OrderInvestigationApprovals.Add(approval);
            await _context.SaveChangesAsync();

            var remainingCount = await BuildNewOrdersUnderInvestigationQuery(currentUser, openedOrderIds).CountAsync();

            return Json(new
            {
                success = true,
                approvedBy,
                remainingCount
            });
        }

        private IQueryable<OrderInvestigationApproval> ApplyInvestigationRatingDateFilter(
            IQueryable<OrderInvestigationApproval> query,
            DateTime? startDate,
            DateTime? endDate)
        {
            if (startDate.HasValue)
            {
                query = query.Where(x => x.ApprovedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                var endLimit = endDate.Value.TimeOfDay == TimeSpan.Zero
                    ? endDate.Value.Date.AddDays(1)
                    : endDate.Value;

                query = query.Where(x => x.ApprovedAt < endLimit);
            }

            return query;
        }

        [HttpGet]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment,CallCenter")]
        public async Task<IActionResult> InvestigationRatingSummary(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = ApplyInvestigationRatingDateFilter(
                ValidInvestigationApprovalsQuery().AsNoTracking(),
                startDate,
                endDate);

            var rawItems = await (
                from approval in query
                join employee in _context.Employees.AsNoTracking()
                    on approval.ApplicationUserId equals employee.ApplicationUserId into employeeJoin
                from employee in employeeJoin.DefaultIfEmpty()
                group approval by new
                {
                    EmployeeId = employee != null ? employee.Id : 0,
                    UserId = approval.ApplicationUserId,
                    EmployeeName = employee != null ? employee.DisplayName : approval.EmployeeName
                }
                into grouped
                select new
                {
                    employeeId = grouped.Key.EmployeeId,
                    userId = grouped.Key.UserId,
                    employeeName = grouped.Key.EmployeeName,
                    count = grouped.Count()
                })
                .ToListAsync();

            return Json(new
            {
                success = true,
                items = rawItems
            });
        }

        [HttpGet]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment,CallCenter")]
        public async Task<IActionResult> InvestigationRatingDetails(
            string? employeeId = null,
            string? employeeName = null,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var query = ApplyInvestigationRatingDateFilter(
                ValidInvestigationApprovalsQuery().AsNoTracking(),
                startDate,
                endDate);

            if (!string.IsNullOrWhiteSpace(employeeId))
            {
                string? employeeUserId = null;

                if (int.TryParse(employeeId, out var parsedEmployeeId))
                {
                    employeeUserId = await _context.Employees
                        .AsNoTracking()
                        .Where(e => e.Id == parsedEmployeeId)
                        .Select(e => e.ApplicationUserId)
                        .FirstOrDefaultAsync();
                }

                if (string.IsNullOrWhiteSpace(employeeUserId))
                {
                    employeeUserId = await _context.Employees
                        .AsNoTracking()
                        .Where(e => e.ApplicationUserId == employeeId)
                        .Select(e => e.ApplicationUserId)
                        .FirstOrDefaultAsync();
                }

                if (!string.IsNullOrWhiteSpace(employeeUserId))
                {
                    query = query.Where(x => x.ApplicationUserId == employeeUserId);
                }
                else
                {
                    query = query.Where(x => x.ApplicationUserId == employeeId);
                }
            }
            else if (!string.IsNullOrWhiteSpace(employeeName))
            {
                query = query.Where(x => x.EmployeeName == employeeName);
            }

            var rawItems = await (
                from approval in query
                join order in _context.Orders.AsNoTracking()
                    on approval.OrderId equals order.Id
                orderby approval.ApprovedAt descending
                select new
                {
                    approval.OrderId,
                    approval.EmployeeName,
                    approval.ApprovedAt,
                    order.CustomerName,
                    order.TelephoneNumber,
                    order.OrderStatus
                })
                .ToListAsync();

            var items = rawItems.Select(x => new
            {
                orderId = x.OrderId,
                employeeName = x.EmployeeName,
                approvedAt = x.ApprovedAt.ToString("yyyy-MM-dd HH:mm"),
                customerName = x.CustomerName,
                telephoneNumber = x.TelephoneNumber,
                orderStatus = x.OrderStatus.ToString().Replace("_", " ")
            }).ToList();

            return Json(new
            {
                success = true,
                items
            });
        }

        private sealed class SalesIndicatorProductRow
        {
            public int OrderId { get; set; }
            public decimal TotalPrice { get; set; }
            public decimal DeliveryPrice { get; set; }
            public int Amount { get; set; }
            public int? MainWarehouseId { get; set; }
        }

        private sealed class SalesIndicatorDisplayData
        {
            public string State { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;
            public decimal NetSellingPrice { get; set; }
            public decimal AverageSellingPrice { get; set; }
        }

        private async Task<Dictionary<int, SalesIndicatorDisplayData>> BuildSalesIndicatorDisplayMapAsync(IReadOnlyCollection<int> orderIds)
        {
            var result = new Dictionary<int, SalesIndicatorDisplayData>();

            if (orderIds == null || orderIds.Count == 0)
            {
                return result;
            }

            var productRows = await _context.Orders
                .AsNoTracking()
                .Where(o => orderIds.Contains(o.Id))
                .SelectMany(o => o.OrderWarehouses.Select(ow => new SalesIndicatorProductRow
                {
                    OrderId = o.Id,
                    TotalPrice = o.TotalPrice,
                    DeliveryPrice = o.DeliveryPrice,
                    Amount = ow.Amount,
                    MainWarehouseId = ow.Warehouse == null ? null : (int?)ow.Warehouse.MainWarehouseId
                }))
                .ToListAsync();

            if (!productRows.Any())
            {
                return result;
            }

            var mainWarehouseIds = productRows
                .Where(x => x.MainWarehouseId.HasValue)
                .Select(x => x.MainWarehouseId!.Value)
                .Distinct()
                .ToList();

            if (!mainWarehouseIds.Any())
            {
                return result;
            }

            var indicatorByMainWarehouse = await _context.Set<SalesIndicator>()
                .AsNoTracking()
                .Where(x => mainWarehouseIds.Contains(x.MainWarehouseId))
                .ToDictionaryAsync(x => x.MainWarehouseId);

            if (!indicatorByMainWarehouse.Any())
            {
                return result;
            }

            foreach (var group in productRows.GroupBy(x => x.OrderId))
            {
                var firstRow = group.First();
                var totalQuantity = group.Sum(x => x.Amount <= 0 ? 1 : x.Amount);
                var netSellingPrice = firstRow.TotalPrice - firstRow.DeliveryPrice;

                if (netSellingPrice < 0)
                {
                    netSellingPrice = 0;
                }

                var averageSellingPrice = totalQuantity > 0
                    ? netSellingPrice / totalQuantity
                    : netSellingPrice;

                var selectedState = string.Empty;
                var selectedPriority = int.MaxValue;

                foreach (var mainWarehouseId in group
                    .Where(x => x.MainWarehouseId.HasValue)
                    .Select(x => x.MainWarehouseId!.Value)
                    .Distinct())
                {
                    if (!indicatorByMainWarehouse.TryGetValue(mainWarehouseId, out var indicator))
                    {
                        continue;
                    }

                    var state = ResolveSalesIndicatorState(averageSellingPrice, indicator);
                    var priority = GetSalesIndicatorPriority(state);

                    if (priority < selectedPriority)
                    {
                        selectedPriority = priority;
                        selectedState = state;
                    }
                }

                if (string.IsNullOrWhiteSpace(selectedState))
                {
                    continue;
                }

                result[group.Key] = new SalesIndicatorDisplayData
                {
                    State = selectedState,
                    Text = GetSalesIndicatorText(selectedState),
                    NetSellingPrice = netSellingPrice,
                    AverageSellingPrice = averageSellingPrice
                };
            }

            return result;
        }

        private static string ResolveSalesIndicatorState(decimal averageSellingPrice, SalesIndicator indicator)
        {
            if (averageSellingPrice >= indicator.MinimumSellingFrom && averageSellingPrice <= indicator.MinimumSellingTo)
            {
                return "minimum";
            }

            if (averageSellingPrice >= indicator.MiddleSellingFrom && averageSellingPrice <= indicator.MiddleSellingTo)
            {
                return "middle";
            }

            if (averageSellingPrice >= indicator.BasicSellingFrom && averageSellingPrice <= indicator.BasicSellingTo)
            {
                return "basic";
            }

            if (averageSellingPrice > indicator.BasicSellingTo)
            {
                return "above-basic";
            }

            // Safe fallback for small gaps between configured ranges.
            if (averageSellingPrice < indicator.MinimumSellingFrom || averageSellingPrice <= indicator.MinimumSellingTo)
            {
                return "minimum";
            }

            if (averageSellingPrice > indicator.MinimumSellingTo && averageSellingPrice < indicator.BasicSellingFrom)
            {
                return "middle";
            }

            return string.Empty;
        }

        private static int GetSalesIndicatorPriority(string state)
        {
            return state switch
            {
                "minimum" => 1,
                "middle" => 2,
                "basic" => 3,
                "above-basic" => 4,
                _ => int.MaxValue
            };
        }

        private static string GetSalesIndicatorText(string state)
        {
            return state switch
            {
                "minimum" => "لقد بعت بالحد الأدنى",
                "middle" => "لقد بعت بالسعر العادي",
                "basic" => "لقد بعت بالسعر الأعلى",
                "above-basic" => "بعت الطلب بالحد الأعلى",
                _ => "لا يوجد مؤشر بيع"
            };
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetOrderSalesIndicator(int orderId)
        {
            if (orderId <= 0)
            {
                return Json(new
                {
                    success = false,
                    message = "رقم الطلب غير صحيح"
                });
            }

            var indicatorMap = await BuildSalesIndicatorDisplayMapAsync(new[] { orderId });

            if (!indicatorMap.TryGetValue(orderId, out var indicator))
            {
                return Json(new
                {
                    success = true,
                    hasIndicator = false,
                    state = "",
                    text = "لا يوجد مؤشر بيع"
                });
            }

            return Json(new
            {
                success = true,
                hasIndicator = true,
                state = indicator.State,
                text = indicator.Text,
                netSellingPrice = indicator.NetSellingPrice,
                averageSellingPrice = indicator.AverageSellingPrice
            });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment,CallCenter")]
        public async Task<IActionResult> ToggleOrderPin([FromBody] ToggleOrderPinRequest request)
        {
            if (request == null || request.OrderId <= 0)
            {
                return Json(new
                {
                    success = false,
                    message = "رقم الطلب غير صحيح"
                });
            }

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == request.OrderId);

            if (order == null)
            {
                return Json(new
                {
                    success = false,
                    message = "لم يتم العثور على الطلب"
                });
            }

            if (order.IsPinned)
            {
                order.IsPinned = false;
                order.PinnedAt = null;
                order.PinnedByUserId = null;
            }
            else
            {
                var currentPinnedCount = await _context.Orders
                    .CountAsync(o => o.IsPinned && o.Id != order.Id);

                if (currentPinnedCount >= 3)
                {
                    return Json(new
                    {
                        success = false,
                        message = "لا يمكن تثبيت أكثر من 3 طلبات. ألغِ تثبيت طلب أولاً ثم حاول مرة أخرى"
                    });
                }

                order.IsPinned = true;
                order.PinnedAt = _timeService.GetIstanbulTimeWithOffset();
                order.PinnedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                orderId = order.Id,
                isPinned = order.IsPinned
            });
        }

        public sealed class ToggleOrderPinRequest
        {
            public int OrderId { get; set; }
        }


        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment,CallCenter")]
        public IActionResult CreateLead()
        {
            return PartialView("_CreateLead");
        }

        [HttpPost]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment,CallCenter")]
        public async Task<IActionResult> CreateLead(string sourceName, int orderSource, string? chatUrl, string? phoneNumber)
        {
            bool isWhatsApp = orderSource == (int)OrderSourceEnum.واتساب;

            if (string.IsNullOrWhiteSpace(sourceName))
                return new JsonResult(new { message = "اسم الصفحة مطلوب" }) { StatusCode = 400 };

            if (!Enum.IsDefined(typeof(OrderSourceEnum), orderSource))
                return new JsonResult(new { message = "نوع الصفحة غير صالح" }) { StatusCode = 400 };

            if (isWhatsApp && string.IsNullOrWhiteSpace(phoneNumber))
                return new JsonResult(new { message = "رقم الهاتف مطلوب لطلبات واتساب" }) { StatusCode = 400 };

            if (!isWhatsApp && string.IsNullOrWhiteSpace(chatUrl))
                return new JsonResult(new { message = "رابط الصفحة مطلوب" }) { StatusCode = 400 };

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var now = _timeService.GetIstanbulTimeWithOffset();

            var lead = new Lead
            {
                SourceName = sourceName,
                OrderSource = (OrderSourceEnum)orderSource,
                PhoneNumber = isWhatsApp ? phoneNumber : null,
                ChatUrl = isWhatsApp ? null : chatUrl,
                CreatedDate = now,
                ApplicationUserId = userId
            };

            _context.Leads.Add(lead);
            await _context.SaveChangesAsync();

            return Json(new { id = lead.Id });
        }

    }


}
