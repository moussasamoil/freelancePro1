using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.Models.ViewModel;
using lotus_blue.Services;
using lotus_blue.OrderStatus;
using lotus_blue.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using static lotus_blue.Models.Common;
using Z.EntityFramework.Plus;

namespace lotus_blue.Controllers
{
    [Authorize(Roles = "Admin,CallCenter,FollowUpDepartment,ExecutiveDirector")]
    public class PrepareForDeliveryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly GetCurrentTimeInIstanbul _timeService;
        private readonly IHubContext<OrderHub> _hubContext;
        private readonly DynamicCommon _dynamicCommon;
        private readonly DataCacheService _dataCacheService;

        public PrepareForDeliveryController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            GetCurrentTimeInIstanbul timeService,
            IHubContext<OrderHub> hubContext,
            DynamicCommon dynamicCommon,
            DataCacheService dataCacheService)
        {
            _context = context;
            _userManager = userManager;
            _timeService = timeService;
            _hubContext = hubContext;
            _dynamicCommon = dynamicCommon;
            _dataCacheService = dataCacheService;
        }

        private bool IsFullAccessRole() =>
            User.IsInRole("Admin") || User.IsInRole("FollowUpDepartment") || User.IsInRole("ExecutiveDirector");

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier).Value;

            var query = BuildBaseQuery(userId);

            var orderQueue = await query
                .OrderBy(o => o.IsDelayed)
                .ThenByDescending(o => o.CreatedDate)
                .ToListAsync();

            var currentOrder = orderQueue.FirstOrDefault();

            var cutoff24h = _timeService.GetIstanbulTimeWithOffset().AddHours(-24);
            var recentQuery = _context.Orders
                .Where(o => (o.OrderStatus == OrderStatusEnum.قيد_التوصيل || o.OrderStatus == OrderStatusEnum.تم_المعالجة)
                    && o.LastEditedDate >= cutoff24h
                    && PfdAllowedCountries.Contains(o.Country));
            if (!IsFullAccessRole())
                recentQuery = recentQuery.Where(o => o.ApplicationUserId == userId);
            if (User.IsInRole("CallCenter"))
                recentQuery = recentQuery.Where(o => o.DeliveryCompanyId != CallCenterHiddenDeliveryCompanyId);
            var recentlySubmitted = await recentQuery
                .OrderByDescending(o => o.LastEditedDate)
                .Include(o => o.ManufacturingCompany)
                .Include(o => o.DeliveryCompany)
                .ToListAsync();

            var viewModel = new PrepareForDeliveryViewModel
            {
                CurrentOrder = currentOrder,
                OrderProducts = currentOrder?.OrderWarehouses?.ToList() ?? new List<OrderWarehouse>(),
                OrderQueue = orderQueue,
                RecentlySubmittedOrders = recentlySubmitted,
                RemainingDownloadCount = orderQueue.Count
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetOrderData(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.ManufacturingCompany)
                .Include(o => o.DeliveryCompany)
                .Include(o => o.OrderWarehouses)
                    .ThenInclude(ow => ow.Warehouse)
                    .ThenInclude(w => w.MainWarehouse)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
                return Json(new { success = false, message = "الطلب غير موجود" });

            if (!IsFullAccessRole() && order.ApplicationUserId != User.FindFirst(ClaimTypes.NameIdentifier).Value)
                return Json(new { success = false, message = "لا يمكنك الوصول لهذا الطلب" });

            var fixedOrderTimes = await _context.OrderStatusHistories
                .CountAsync(oh => oh.OrderId == orderId && oh.Status == OrderStatusEnum.تم_المعالجة);

            var wasDeferred = await _context.OrderStatusHistories
                .AnyAsync(oh => oh.OrderId == orderId && oh.Status == OrderStatusEnum.الطلبات_المؤجلة);

            var products = order.OrderWarehouses?.Select(ow => new
            {
                name = ow.Warehouse?.Name ?? "غير معروف",
                amount = ow.Amount,
                image = ow.Warehouse?.MainWarehouse?.ImageUrl ?? "static/DefaultImage.svg"
            }).ToList();

            var employee = await _context.Employees
                .Where(e => e.ApplicationUserId == order.ApplicationUserId)
                .Select(e => new { name = e.DisplayName, logoUrl = e.ImageUrl })
                .FirstOrDefaultAsync();

            return Json(new
            {
                success = true,
                order = new
                {
                    id = order.Id,
                    customerName = order.CustomerName,
                    country = order.Country.ToString(),
                    countryId = (int)order.Country,
                    state = order.State,
                    address = order.Address,
                    telephoneNumber = order.TelephoneNumber,
                    deliveryCompanyName = order.DeliveryCompany?.Name,
                    deliveryCompanyLogo = order.DeliveryCompany?.ImageUrl,
                    manufacturingCompanyName = order.ManufacturingCompany?.Name,
                    manufacturingCompanyLogo = order.ManufacturingCompany?.ImageUrl,
                    orderStatus = order.OrderStatus.ToString(),
                    orderStatusInt = (int)order.OrderStatus,
                    statusColor = OrderStatusHelper.StatusColorMapping.GetValueOrDefault(order.OrderStatus, ""),
                    fixedOrderTimes = fixedOrderTimes,
                    totalPrice = order.TotalPrice,
                    deliveryPrice = order.DeliveryPrice,
                    currency = Common.CurrencyByCountry.GetValueOrDefault(order.Country),
                    notes = order.Notes,
                    isPaid = order.IsPaid,
                    isDelayed = order.IsDelayed,
                    wasDeferred = wasDeferred,
                    createdDate = order.CreatedDate.ToString("yyyy-MM-dd"),
                    employeeName = employee?.name,
                    employeeLogoUrl = employee?.logoUrl,
                },
                products = products
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployeesWithOrdersForDropdown(int? countryId, int? storeId, int? deliveryCompanyId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Json(new
                {
                    success = false,
                    employees = Array.Empty<object>()
                });
            }

            var query = BuildBaseQuery(userId);

            if (countryId.HasValue && Enum.IsDefined(typeof(Countries), countryId.Value))
            {
                var country = (Countries)countryId.Value;
                query = query.Where(o => o.Country == country);
            }

            if (storeId.HasValue)
            {
                query = query.Where(o => o.ManufacturingCompanyId == storeId.Value);
            }

            if (deliveryCompanyId.HasValue)
            {
                query = query.Where(o => o.DeliveryCompanyId == deliveryCompanyId.Value);
            }

            var ordersByEmployee = await query
                .Where(o => o.ApplicationUserId != null && o.ApplicationUserId != "")
                .GroupBy(o => o.ApplicationUserId)
                .Select(g => new
                {
                    ApplicationUserId = g.Key,
                    OrdersCount = g.Count()
                })
                .ToListAsync();

            var employeeUserIds = ordersByEmployee
                .Select(x => x.ApplicationUserId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            if (!employeeUserIds.Any())
            {
                return Json(new
                {
                    success = true,
                    employees = Array.Empty<object>()
                });
            }

            /*
                مهم:
                لا نستخدم IsActive هنا.
                المطلوب أن يظهر في Dropdown كل موظف له طلبات في صفحة تنزيل الطلبات
                حتى لو كان غير نشط حاليًا.
            */
            var employees = await _context.Employees
                .AsNoTracking()
                .Where(e =>
                    e.ApplicationUserId != null &&
                    e.ApplicationUserId != "" &&
                    employeeUserIds.Contains(e.ApplicationUserId))
                .Select(e => new
                {
                    id = e.ApplicationUserId,
                    name = string.IsNullOrWhiteSpace(e.DisplayName) ? e.Name : e.DisplayName,
                    imageUrl = e.ImageUrl,
                    isActive = e.IsActive
                })
                .ToListAsync();

            var employeesByUserId = employees
                .Where(e => !string.IsNullOrWhiteSpace(e.id))
                .GroupBy(e => e.id)
                .ToDictionary(g => g.Key, g => g.First());

            var result = ordersByEmployee
                .Select(item =>
                {
                    employeesByUserId.TryGetValue(item.ApplicationUserId ?? "", out var employee);

                    return new
                    {
                        id = item.ApplicationUserId,
                        name = employee == null || string.IsNullOrWhiteSpace(employee.name)
                            ? "موظف غير معروف"
                            : employee.name,
                        imageUrl = employee?.imageUrl ?? "",
                        isActive = employee?.isActive ?? false,
                        ordersCount = item.OrdersCount
                    };
                })
                .OrderByDescending(e => e.isActive)
                .ThenBy(e => e.name)
                .ToList();

            return Json(new
            {
                success = true,
                employees = result
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetFilteredQueue(int? countryId, int? storeId, int? deliveryCompanyId, string search, string employeeId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier).Value;
            var query = BuildBaseQuery(userId);

            // Full-access roles can filter to view as a specific employee
            if (IsFullAccessRole() && !string.IsNullOrWhiteSpace(employeeId))
            {
                query = query.Where(o => o.ApplicationUserId == employeeId);
            }

            // Filter by country
            if (countryId.HasValue && Enum.IsDefined(typeof(Countries), countryId.Value))
            {
                var country = (Countries)countryId.Value;
                query = query.Where(o => o.Country == country);
            }

            // Filter by store (manufacturing company)
            if (storeId.HasValue)
            {
                query = query.Where(o => o.ManufacturingCompanyId == storeId.Value);
            }

            // Filter by delivery company
            if (deliveryCompanyId.HasValue)
            {
                query = query.Where(o => o.DeliveryCompanyId == deliveryCompanyId.Value);
            }

            // Global search
            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{search}%";

                var matchingCountries = Enum.GetValues(typeof(Countries)).Cast<Countries>()
                    .Where(c => c.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                query = query.Where(o =>
                    EF.Functions.Like(o.CustomerName, pattern) ||
                    (o.Address != null && EF.Functions.Like(o.Address, pattern)) ||
                    (o.TelephoneNumber != null && EF.Functions.Like(o.TelephoneNumber, pattern)) ||
                    (o.Notes != null && EF.Functions.Like(o.Notes, pattern)) ||
                    (o.State != null && EF.Functions.Like(o.State, pattern)) ||
                    matchingCountries.Contains(o.Country));
            }

            var orders = await query
                .OrderBy(o => o.IsDelayed)
                .ThenByDescending(o => o.CreatedDate)
                .ToListAsync();

            var result = orders.Select(o => new
            {
                id = o.Id,
                customerName = o.CustomerName,
                country = o.Country.ToString(),
                countryId = (int)o.Country,
                state = o.State,
                address = o.Address,
                telephoneNumber = o.TelephoneNumber,
                deliveryCompanyName = o.DeliveryCompany?.Name,
                deliveryCompanyLogo = o.DeliveryCompany?.ImageUrl,
                manufacturingCompanyName = o.ManufacturingCompany?.Name,
                manufacturingCompanyLogo = o.ManufacturingCompany?.ImageUrl,
                totalPrice = o.TotalPrice,
                isDelayed = o.IsDelayed,
            }).ToList();

            return Json(new
            {
                success = true,
                orders = result,
                remainingDownloadsCount = result.Count
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetRemainingDownloadsCount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Json(new
                {
                    success = false,
                    count = 0,
                    message = "جلسة المستخدم غير صالحة."
                });
            }

            var count = await BuildBaseQuery(userId).CountAsync();

            return Json(new
            {
                success = true,
                count
            });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitOrder(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.DeliveryCompany)
                .Include(o => o.ManufacturingCompany)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
                return Json(new { success = false, message = "الطلب غير موجود" });

            if (!IsFullAccessRole() && order.ApplicationUserId != User.FindFirst(ClaimTypes.NameIdentifier).Value)
                return Json(new { success = false, message = "لا يمكنك الوصول لهذا الطلب" });

            if (order.OrderStatus != OrderStatusEnum.طلب_جديد)
                return Json(new { success = false, message = "حالة الطلب لا تسمح بهذا الإجراء" });

            var now = _timeService.GetIstanbulTimeWithOffset();
            string userId = _userManager.GetUserId(User);

            // ── Step 1: طلب_جديد → تم_التجهيز (reuse MarkAsPrepared logic) ──

            // Create OrderStatusHistory for تم_التجهيز
            var preparedHistory = new OrderStatusHistory
            {
                OrderId = order.Id,
                Status = OrderStatusEnum.تم_التجهيز,
                CreatedAt = now,
                ApplicationUserId = userId
            };
            _context.OrderStatusHistories.Add(preparedHistory);
            await _context.SaveChangesAsync();

            // Create OrderReport for تم_التجهيز
            var orderReport = new OrderReport
            {
                GeneratedTime = now,
                TotalAmount = order.TotalPrice,
                Country = order.Country,
                DeliveryCompanyId = order.DeliveryCompanyId,
                DeliveryCompany = order.DeliveryCompany,
                OrderStatus = OrderStatusEnum.تم_التجهيز,
                Orders = new List<Order> { order },
            };
            _context.OrderReports.Add(orderReport);
            await _context.SaveChangesAsync();

            // Create OrderReportOrder junction
            var orderReportOrder = new OrderReportOrder
            {
                OrderId = order.Id,
                OrderReportId = orderReport.Id
            };
            _context.OrderReportOrders.Add(orderReportOrder);
            await _context.SaveChangesAsync();

            // Update order status to تم_التجهيز
            await _context.Orders
                .Where(o => o.Id == orderId)
                .UpdateAsync(o => new Order { OrderStatus = OrderStatusEnum.تم_التجهيز, LastEditedDate = now });

            // Broadcast تم_التجهيز to SignalR
            var preparedData = new
            {
                preparedHistory.OrderId,
                preparedHistory.Status,
                preparedHistory.CreatedAt,
                preparedHistory.ApplicationUserId,
                UserName = await _dynamicCommon.GetUserNameByIdAsync(userId),
                StatusPhrase = OrderStatusHelper.GetOrderStatusPhrase(OrderStatusEnum.تم_التجهيز),
                ColorStyle = OrderStatusHelper.StatusColorMapping.GetValueOrDefault(OrderStatusEnum.تم_التجهيز, "")
            };
            await _hubContext.Clients.Group("UsersExpectDelivery").SendAsync("OrderStatusUpdated", preparedData);
            var deliveryGroup = $"deliveryCompany_{order.DeliveryCompanyId}";
            await _hubContext.Clients.Group(deliveryGroup).SendAsync("OrderStatusUpdated", preparedData);

            // ── Step 2: تم_التجهيز → قيد_التوصيل ──

            var deliveryHistory = new OrderStatusHistory
            {
                OrderId = order.Id,
                Status = OrderStatusEnum.قيد_التوصيل,
                CreatedAt = now,
                ApplicationUserId = userId
            };
            _context.OrderStatusHistories.Add(deliveryHistory);

            // Update order status to قيد_التوصيل
            await _context.Orders
                .Where(o => o.Id == orderId)
                .UpdateAsync(o => new Order { OrderStatus = OrderStatusEnum.قيد_التوصيل, LastEditedDate = now });

            await _context.SaveChangesAsync();

            // Broadcast قيد_التوصيل to SignalR
            var userName = await _dynamicCommon.GetUserNameByIdAsync(userId);
            var deliveryData = new
            {
                deliveryHistory.OrderId,
                deliveryHistory.Status,
                deliveryHistory.CreatedAt,
                deliveryHistory.ApplicationUserId,
                UserName = userName,
                StatusPhrase = OrderStatusHelper.GetOrderStatusPhrase(OrderStatusEnum.قيد_التوصيل),
                ColorStyle = OrderStatusHelper.StatusColorMapping.GetValueOrDefault(OrderStatusEnum.قيد_التوصيل, "")
            };
            await _hubContext.Clients.Group("UsersExpectDelivery").SendAsync("OrderStatusUpdated", deliveryData);
            await _hubContext.Clients.Group(deliveryGroup).SendAsync("OrderStatusUpdated", deliveryData);
            var mfgGroup = $"manufacturingCompany_{order.ManufacturingCompanyId}";
            await _hubContext.Clients.Group(mfgGroup).SendAsync("OrderStatusUpdated", deliveryData);

            var remainingDownloadsCount = await BuildBaseQuery(userId).CountAsync();

            return Json(new
            {
                success = true,
                message = "تم تأكيد إدخال الطلب بنجاح",
                remainingDownloadsCount,
                submittedOrder = new
                {
                    id = order.Id,
                    time = now.ToString("HH:mm"),
                    country = order.Country.ToString(),
                    countryId = (int)order.Country,
                    deliveryCompanyName = order.DeliveryCompany?.Name,
                    deliveryCompanyLogo = order.DeliveryCompany?.ImageUrl,
                    manufacturingCompanyName = order.ManufacturingCompany?.Name,
                }
            });
        }

        [HttpPost]
        public async Task<IActionResult> SetDelayed(int orderId, bool isDelayed)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null)
                return Json(new { success = false, message = "الطلب غير موجود" });

            if (!IsFullAccessRole() && order.ApplicationUserId != User.FindFirst(ClaimTypes.NameIdentifier).Value)
                return Json(new { success = false, message = "لا يمكنك الوصول لهذا الطلب" });

            if (order.OrderStatus != OrderStatusEnum.طلب_جديد)
                return Json(new { success = false, message = "حالة الطلب لا تسمح بهذا الإجراء" });

            order.IsDelayed = isDelayed;
            await _context.SaveChangesAsync();

            return Json(new { success = true, isDelayed });
        }

        [HttpGet]
        public async Task<IActionResult> GetRecentlySubmitted()
        {
            var cutoff24h = _timeService.GetIstanbulTimeWithOffset().AddHours(-24);
            var userId = User.FindFirst(ClaimTypes.NameIdentifier).Value;

            var recentQuery = _context.Orders
                .Include(o => o.DeliveryCompany)
                .Include(o => o.ManufacturingCompany)
                .Where(o => (o.OrderStatus == OrderStatusEnum.قيد_التوصيل || o.OrderStatus == OrderStatusEnum.تم_المعالجة)
                    && o.LastEditedDate >= cutoff24h
                    && PfdAllowedCountries.Contains(o.Country));
            if (!IsFullAccessRole())
                recentQuery = recentQuery.Where(o => o.ApplicationUserId == userId);
            if (User.IsInRole("CallCenter"))
                recentQuery = recentQuery.Where(o => o.DeliveryCompanyId != CallCenterHiddenDeliveryCompanyId);

            var orders = (await recentQuery
                .OrderByDescending(o => o.LastEditedDate)
                .ToListAsync())
                .Select(o => new
                {
                    id = o.Id,
                    time = o.LastEditedDate?.ToString("HH:mm") ?? "",
                    country = o.Country.ToString(),
                    countryId = (int)o.Country,
                    orderStatus = o.OrderStatus.ToString(),
                    orderStatusInt = (int)o.OrderStatus,
                    statusColor = OrderStatusHelper.StatusColorMapping.GetValueOrDefault(o.OrderStatus, ""),
                    deliveryCompanyName = o.DeliveryCompany?.Name,
                    deliveryCompanyLogo = o.DeliveryCompany?.ImageUrl,
                    manufacturingCompanyName = o.ManufacturingCompany?.Name,
                })
                .ToList();

            return Json(new { success = true, orders = orders });
        }

        [HttpGet]
        public async Task<IActionResult> SearchRecentlySubmitted(string search)
        {
            var cutoff24h = _timeService.GetIstanbulTimeWithOffset().AddHours(-24);
            var userId = User.FindFirst(ClaimTypes.NameIdentifier).Value;

            var query = _context.Orders
                .Include(o => o.DeliveryCompany)
                .Include(o => o.ManufacturingCompany)
                .Where(o => (o.OrderStatus == OrderStatusEnum.قيد_التوصيل || o.OrderStatus == OrderStatusEnum.تم_المعالجة)
                    && o.LastEditedDate >= cutoff24h
                    && PfdAllowedCountries.Contains(o.Country));
            if (!IsFullAccessRole())
                query = query.Where(o => o.ApplicationUserId == userId);
            if (User.IsInRole("CallCenter"))
                query = query.Where(o => o.DeliveryCompanyId != CallCenterHiddenDeliveryCompanyId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var matchingCountries = Enum.GetValues(typeof(Countries)).Cast<Countries>()
                    .Where(c => c.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var idPattern = $"%{search}%";
                query = query.Where(o =>
                    EF.Functions.Like(o.Id.ToString(), idPattern) ||
                    matchingCountries.Contains(o.Country));
            }

            var orders = (await query
                .OrderByDescending(o => o.LastEditedDate)
                .ToListAsync())
                .Select(o => new
                {
                    id = o.Id,
                    time = o.LastEditedDate?.ToString("HH:mm") ?? "",
                    country = o.Country.ToString(),
                    countryId = (int)o.Country,
                    orderStatus = o.OrderStatus.ToString(),
                    orderStatusInt = (int)o.OrderStatus,
                    statusColor = OrderStatusHelper.StatusColorMapping.GetValueOrDefault(o.OrderStatus, ""),
                    deliveryCompanyName = o.DeliveryCompany?.Name,
                    deliveryCompanyLogo = o.DeliveryCompany?.ImageUrl,
                    manufacturingCompanyName = o.ManufacturingCompany?.Name,
                })
                .ToList();

            return Json(new { success = true, orders = orders });
        }

        [HttpGet]
        public async Task<IActionResult> GetLastSubmittedOrder()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier).Value;

            var query = _context.Orders
                .Where(o => (o.OrderStatus == OrderStatusEnum.قيد_التوصيل || o.OrderStatus == OrderStatusEnum.تم_المعالجة)
                    && PfdAllowedCountries.Contains(o.Country));

            if (IsFullAccessRole())
            {
                // No time constraint for full-access roles
            }
            else
            {
                // CallCenter: own orders, last 24h only, hide Amyal (id 77)
                var cutoff24h = _timeService.GetIstanbulTimeWithOffset().AddHours(-24);
                query = query.Where(o => o.ApplicationUserId == userId
                    && o.LastEditedDate >= cutoff24h
                    && o.DeliveryCompanyId != CallCenterHiddenDeliveryCompanyId);
            }

            var order = await query
                .OrderByDescending(o => o.LastEditedDate)
                .Select(o => new { id = o.Id })
                .FirstOrDefaultAsync();

            if (order == null)
                return Json(new { success = false });

            return Json(new { success = true, orderId = order.id });
        }

        private static readonly Countries[] PfdAllowedCountries =
            { Countries.العراق, Countries.ليبيا, Countries.سلطنة_عمان, Countries.الإمارات };

        // Delivery company hidden from CallCenter role on the Prepare-for-Delivery page
        // (filter dropdown + backend results). Other roles see it normally.
        public const int CallCenterHiddenDeliveryCompanyId = 77;

        private IQueryable<Order> BuildBaseQuery(string userId)
        {
            var query = _context.Orders
                .Where(o => o.OrderStatus == OrderStatusEnum.طلب_جديد
                    && PfdAllowedCountries.Contains(o.Country))
                .Include(o => o.ManufacturingCompany)
                .Include(o => o.DeliveryCompany)
                .Include(o => o.OrderWarehouses)
                    .ThenInclude(ow => ow.Warehouse)
                    .ThenInclude(w => w.MainWarehouse)
                .AsQueryable();

            // Full-access roles see all orders; CallCenter sees only their own
            if (!IsFullAccessRole())
            {
                query = query.Where(o => o.ApplicationUserId == userId);
            }

            // Manufacturing company access scoping for CallCenter + hide Amyal (id 77)
            if (User.IsInRole("CallCenter"))
            {
                query = query.Where(a => a.ManufacturingCompany.EmployeeManufacturingCompanies
                    .Any(m => m.ApplicationUserId == userId && m.CanSeeManufacturingCompany));
                query = query.Where(o => o.DeliveryCompanyId != CallCenterHiddenDeliveryCompanyId);
            }

            return query;
        }
    }
}
