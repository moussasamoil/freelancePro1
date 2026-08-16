using lotus_blue.API;
using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.Models.ViewModel;
using lotus_blue.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static lotus_blue.Models.Common;
using System.Security.Claims;
using System.Collections.Generic;
using System.Linq;
using lotus_blue.Models.ViewModel.Rating;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace lotus_blue.Controllers
{
    public class RatingController : Controller
    {

        private readonly ApplicationDbContext _context;
        private readonly CurrencyExchangeService _currencyExchangeService;
        private readonly RESTAPI _Restapi;
        private readonly UserManager<ApplicationUser> _userManager; // Add UserManager<ApplicationUser>
        private readonly GetCurrentTimeInIstanbul _timeService;
        private readonly DecimalFormattingService _decimalFormattingService;
        private readonly DeliveryCompanyService _deliveryCompanyService;
        private readonly DynamicCommon _dynamicCommon;
        private readonly DataCacheService _dataCacheService;
        private readonly OrderService _orderService;
        public readonly QueryFilteringService _queryFilteringService;
        public RatingController(ApplicationDbContext context, CurrencyExchangeService currencyExchangeService, RESTAPI restapi, UserManager<ApplicationUser> userManager, DecimalFormattingService decimalFormattingService, DeliveryCompanyService deliveryCompanyService, DynamicCommon dynamicCommon, DataCacheService dataCacheService, OrderService orderService, GetCurrentTimeInIstanbul timeService, QueryFilteringService queryFilteringService)
        {
            _context = context;
            _currencyExchangeService = currencyExchangeService;
            _Restapi = restapi;
            _userManager = userManager;
            _decimalFormattingService = decimalFormattingService;
            _dynamicCommon = dynamicCommon;
            _deliveryCompanyService = deliveryCompanyService;
            _dataCacheService = dataCacheService;
            _orderService = orderService;
            _timeService = timeService;
            _queryFilteringService = queryFilteringService;
        }


        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public async Task<IActionResult> Employelist(
      Common.Countries? countyId = null,
      bool? genderId = null,
      int? storeId = null,
      int? mainwarehouseId = null,
      Common.Countries? countryId = null,
      OrderSourceEnum? orderSourceId = null,
      DateTime? startDay = null, DateTime? endDay = null,
      string? employeeId = null,
      bool? workShift = null,
      bool? fromComments = null)
        {
            IEnumerable<Employee> employeeQuery = await _dataCacheService.GetCachedEmployeesAsync();
            var filteredOrdersQuery = _context.Orders.Include(a => a.OrderWarehouses).ThenInclude(o => o.Warehouse).AsQueryable();

            if (!string.IsNullOrEmpty(employeeId))
            {
                filteredOrdersQuery = filteredOrdersQuery.Where(e => e.ApplicationUserId == employeeId);
            }

            var now = _timeService.GetIstanbulTimeWithOffset();

            if (startDay == null && endDay == null)
            {
                if (now.TimeOfDay < new TimeSpan(10, 30, 0))
                {
                    startDay = now.Date.AddDays(-1).AddHours(10).AddMinutes(30);
                    endDay = now.Date.AddHours(10).AddMinutes(30);
                }
                else
                {
                    startDay = now.Date.AddHours(10).AddMinutes(30);
                    endDay = now.Date.AddDays(1).AddHours(10).AddMinutes(30);
                }
            }
            else
            {
                startDay = startDay?.Date.AddHours(10).AddMinutes(30);
                endDay = endDay?.Date.AddHours(10).AddMinutes(30);
            }


            var fixedOrders = filteredOrdersQuery
                .Where(o => o.FixedOrderDate >= startDay && o.FixedOrderDate <= endDay)
                .ToList();

            filteredOrdersQuery = filteredOrdersQuery
                .Where(x => x.InstantAddedDate >= startDay && x.InstantAddedDate < endDay);

            if (workShift.HasValue)
            {
                if (workShift == true) // Morning shift
                {
                    filteredOrdersQuery = filteredOrdersQuery.Where(o =>
                        o.InstantAddedDate.HasValue &&
                        o.InstantAddedDate.Value.TimeOfDay >= TimeSpan.FromHours(9) &&
                        o.InstantAddedDate.Value.TimeOfDay <= TimeSpan.FromHours(18).Add(TimeSpan.FromMinutes(45)));
                }
                else if (workShift == false) // Evening shift (Night)
                {
                    filteredOrdersQuery = filteredOrdersQuery.Where(o =>
                        o.InstantAddedDate.HasValue &&
                        (o.InstantAddedDate.Value.TimeOfDay >= TimeSpan.FromHours(18).Add(TimeSpan.FromMinutes(45)) ||
                        o.InstantAddedDate.Value.TimeOfDay <= TimeSpan.FromHours(4)));
                }
            }

            if (mainwarehouseId.HasValue)
                filteredOrdersQuery = filteredOrdersQuery.Where(o => o.OrderWarehouses.Any(ow => ow.Warehouse.MainWarehouseId == mainwarehouseId));

            if (genderId.HasValue)
                filteredOrdersQuery = filteredOrdersQuery.Where(o => o.Gender == genderId);

            if (countryId.HasValue)
                filteredOrdersQuery = filteredOrdersQuery.Where(o => o.Country == countryId);

            if (storeId.HasValue)
                filteredOrdersQuery = filteredOrdersQuery.Where(o => o.ManufacturingCompanyId == storeId);

            if (orderSourceId.HasValue)
            {
                if (QueryFilteringService.IsMetaSource(orderSourceId.Value))
                    filteredOrdersQuery = filteredOrdersQuery.Where(x => x.OrderSource == OrderSourceEnum.فيسبوك || x.OrderSource == OrderSourceEnum.انستغرام);
                else
                    filteredOrdersQuery = filteredOrdersQuery.Where(x => x.OrderSource == orderSourceId.Value);
            }

            if (countyId.HasValue)
                filteredOrdersQuery = filteredOrdersQuery.Where(x => x.Country == countyId.Value);

            if (fromComments.HasValue)
                filteredOrdersQuery = filteredOrdersQuery.Where(x => x.FromComments == fromComments.Value);

            var employees = employeeQuery.ToList();

            var orderData = await filteredOrdersQuery
                .Include(o => o.OrderWarehouses)
                    .ThenInclude(ow => ow.Warehouse)
                .Select(o => new
                {
                    o.ApplicationUserId,
                    o.Fixedby,
                    o.Id,
                    o.TotalPrice,
                    o.DeliveryCompanyId,
                    o.Country,
                    o.IsBonus,
                    HasWarehouseWithMoreThanOneItem = o.OrderWarehouses.Any(ow => ow.Amount > 1),
                    HasMoreThanOneWarehouse = o.OrderWarehouses.GroupBy(ow => ow.WarehouseId).Count() > 1,
                    o.FixedOrderDate,
                    o.Gender,
                    o.FromComments,
                    o.CampaignId,
                    TotalProductsCount = o.OrderWarehouses.Sum(ow => ow.Amount),
                    o.IsDiscount,
                    o.DeliveryPrice
                })
                .ToListAsync();

            int totalNumberOfOrders = orderData.Count;

            // بدون إعلان يتبع نفس فلتر اليوم/الفترة المختارة مثل باقي أرقام التقييمات.
            var noAdSummaryCountryId = countryId ?? countyId;
            var noAdDailySummary = await BuildNoAdRatingOrdersQuery(
                    startDay,
                    endDay,
                    employeeId,
                    noAdSummaryCountryId,
                    orderSourceId,
                    genderId,
                    storeId,
                    mainwarehouseId,
                    workShift)
                .Where(o => !string.IsNullOrEmpty(o.ApplicationUserId))
                .GroupBy(o => o.ApplicationUserId)
                .Select(g => new
                {
                    EmployeeId = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            ViewBag.NoAdOrdersCount = noAdDailySummary.Sum(x => x.Count);
            ViewBag.NoAdEmployeesCount = noAdDailySummary.Count;

            var allOrderIds = orderData.Select(o => o.Id).ToList();

            // PotentialOrders query for Employelist
            var potentialOrdersQuery = _context.PotentialOrders
                .Where(po => po.CreatedDate >= startDay && po.CreatedDate < endDay);
            if (!string.IsNullOrEmpty(employeeId))
                potentialOrdersQuery = potentialOrdersQuery.Where(po => po.ApplicationUserId == employeeId);
            if (countryId.HasValue)
                potentialOrdersQuery = potentialOrdersQuery.Where(po => po.Country == countryId.Value);
            if (countyId.HasValue)
                potentialOrdersQuery = potentialOrdersQuery.Where(po => po.Country == countyId.Value);
            if (storeId.HasValue)
            {
                var storeName = _context.ManufacturingCompanies.Where(m => m.Id == storeId.Value).Select(m => m.Name).FirstOrDefault();
                if (storeName != null)
                    potentialOrdersQuery = potentialOrdersQuery.Where(po => po.StoreName == storeName);
            }
            if (orderSourceId.HasValue)
            {
                if (QueryFilteringService.IsMetaSource(orderSourceId.Value))
                    potentialOrdersQuery = potentialOrdersQuery.Where(po => po.OrderSource == OrderSourceEnum.فيسبوك || po.OrderSource == OrderSourceEnum.انستغرام);
                else
                    potentialOrdersQuery = potentialOrdersQuery.Where(po => po.OrderSource == orderSourceId.Value);
            }

            var potentialOrdersByEmployee = await potentialOrdersQuery
                .GroupBy(po => po.ApplicationUserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.UserId, g => g.Count);
            int totalPotentialOrdersCount = potentialOrdersByEmployee.Values.Sum();

            // Create dictionaries to store prices by order ID
            var priceInUSDByOrderId = orderData.ToDictionary(o => o.Id, o => _deliveryCompanyService.CalculateTotalPriceInUSDForOrdersWithOutDeliveryCompanyPrice(new List<int> { o.Id }));
            var priceInTLByOrderId = priceInUSDByOrderId.ToDictionary(kvp => kvp.Key, kvp => _currencyExchangeService.ConvertToTurkishLira(kvp.Value));

            var tasks = employees.Select(async employee =>
            {
                var employeeOrders = orderData.Where(o => o.ApplicationUserId == employee.ApplicationUserId).ToList();
                var employeeOrdersfixed = fixedOrders.Where(o => o.ApplicationUserId == employee.ApplicationUserId).ToList();

                var employeeOrderIds = employeeOrders.Select(o => o.Id).ToList();
                var employeeBonuses = employeeOrders.Count(o => o.IsBonus);

                var ordersWithOffers = employeeOrders.Count(o => o.HasWarehouseWithMoreThanOneItem || o.HasMoreThanOneWarehouse);
                var ordersFromOffers = employeeOrders.Count(o => o.IsDiscount);
                var ordersFromComments = employeeOrders.Count(o => o.FromComments);
                var ordersFromMales = employeeOrders.Count(o => o.Gender);
                var ordersFromFemales = employeeOrders.Count(o => !o.Gender);
                var fixedOrdersCount = employeeOrdersfixed.Count;

                var totalOrdersPriceInUSDByEmployees = employeeOrderIds.Sum(id => priceInUSDByOrderId[id]);
                var totalOrdersPriceInTLByEmployees = employeeOrderIds.Sum(id => priceInTLByOrderId[id]);

                decimal rating = (totalNumberOfOrders > 0) ? (employeeOrders.Count / (decimal)totalNumberOfOrders) * 100 : 0;

                return new RatingViewModel
                {
                    Id = employee.ApplicationUserId,
                    Name = employee.Name,
                    Image = employee.ImageUrl,
                    NumberOfBonuses = employeeBonuses,
                    OrdersCount = employeeOrders.Count,
                    OrderFromOffers = ordersFromOffers,
                    FixedOrdersCount = fixedOrdersCount,
                    OrderFromMales = ordersFromMales,
                    OrderFromFemales = ordersFromFemales,
                    OrderFromComments = ordersFromComments,
                    TotalProductsCount = employeeOrders.Sum(o => o.TotalProductsCount),
                    TotalOrdersWithWarehouseItemMoreThanOne = ordersWithOffers,
                    TotalPriceUSD = _decimalFormattingService.DecimalFormat(totalOrdersPriceInUSDByEmployees),
                    TotalPriceTRY = _decimalFormattingService.DecimalFormat(totalOrdersPriceInTLByEmployees),
                    Rating = _decimalFormattingService.DecimalFormat(rating),
                    PotentialOrdersCount = potentialOrdersByEmployee.GetValueOrDefault(employee.ApplicationUserId, 0)
                };
            }).ToList();

            var employeeRatings = await Task.WhenAll(tasks);

            var sortedEmployeeRatings = employeeRatings
                .OrderByDescending(e => e.OrdersCount)
                .ToList();

            var compositeViewModel = new EmployeeRatingCompositeViewModel
            {
                EmployeeRatings = sortedEmployeeRatings,
                OrdersByCountry = new List<OrderCountByCountry>(), // Empty list
                OrdersByStore = new List<OrderCountByStore>(), // Empty list
                NumberOfBouneses = orderData.Count(o => o.IsBonus),
                OrderFromOfferDiscountsCount = _decimalFormattingService.DecimalFormat(orderData.Count(o => o.IsDiscount)),
                TotalNumberOfOrders = _decimalFormattingService.DecimalFormat(totalNumberOfOrders),
                OrderFromCommentsCount = _decimalFormattingService.DecimalFormat(orderData.Count(o => o.FromComments)),
                OrderFromMalesCount = _decimalFormattingService.DecimalFormat(orderData.Count(o => o.Gender)),
                OrderFromFemalesCount = _decimalFormattingService.DecimalFormat(orderData.Count(o => !o.Gender)),
                FixedOrdersCount = _decimalFormattingService.DecimalFormat(fixedOrders.Count),
                OrderFromOffersCount = _decimalFormattingService.DecimalFormat(orderData.Count(o =>
                    o.HasWarehouseWithMoreThanOneItem || o.HasMoreThanOneWarehouse)),
                TotalOrdersPriceInDollar = _decimalFormattingService.DecimalFormat(
                    allOrderIds.Sum(id => priceInUSDByOrderId[id])),
                TotalOrdersPriceInTL = _decimalFormattingService.DecimalFormat(
                    allOrderIds.Sum(id => priceInTLByOrderId[id])),
                TotalProductsCount = _decimalFormattingService.DecimalFormat(orderData.Sum(o => o.TotalProductsCount)),
                PotentialOrdersCount = _decimalFormattingService.DecimalFormat(totalPotentialOrdersCount)
            };

            return View(compositeViewModel);
        }


        private (DateTime StartDay, DateTime EndDay) GetRatingDateRange(DateTime? startDate, DateTime? endDate)
        {
            var now = _timeService.GetIstanbulTimeWithOffset();
            DateTime startDay;
            DateTime endDay;

            if (startDate == null && endDate == null)
            {
                if (now.TimeOfDay < new TimeSpan(10, 30, 0))
                {
                    startDay = now.Date.AddDays(-1).AddHours(10).AddMinutes(30);
                    endDay = now.Date.AddHours(10).AddMinutes(30);
                }
                else
                {
                    startDay = now.Date.AddHours(10).AddMinutes(30);
                    endDay = now.Date.AddDays(1).AddHours(10).AddMinutes(30);
                }
            }
            else
            {
                startDay = (startDate ?? now.Date).Date.AddHours(10).AddMinutes(30);
                endDay = (endDate ?? startDay.AddDays(1)).Date.AddHours(10).AddMinutes(30);

                if (endDay <= startDay)
                {
                    endDay = startDay.AddDays(1);
                }
            }

            return (startDay, endDay);
        }

        private IQueryable<Order> BuildNoAdRatingOrdersQuery(
            DateTime? startDate,
            DateTime? endDate,
            string? employeeId,
            Common.Countries? countryId,
            OrderSourceEnum? orderSourceId,
            bool? genderId,
            int? storeId,
            int? mainwarehouseId,
            bool? workShift)
        {
            // بدون إعلان يتبع نفس فترة التقييم المختارة: اليوم الحالي افتراضيًا أو التاريخ المختار من الفلتر.
            var query = _context.Orders
                .AsNoTracking()
                .Where(o => o.CampaignId == null);

            if (startDate.HasValue)
            {
                query = query.Where(o => o.InstantAddedDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(o => o.InstantAddedDate < endDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(employeeId))
            {
                query = query.Where(o => o.ApplicationUserId == employeeId);
            }

            if (countryId.HasValue)
            {
                query = query.Where(o => o.Country == countryId.Value);
            }

            if (genderId.HasValue)
            {
                query = query.Where(o => o.Gender == genderId.Value);
            }

            if (storeId.HasValue)
            {
                query = query.Where(o => o.ManufacturingCompanyId == storeId.Value);
            }

            if (mainwarehouseId.HasValue)
            {
                query = query.Where(o => o.OrderWarehouses.Any(ow => ow.Warehouse.MainWarehouseId == mainwarehouseId.Value));
            }

            if (orderSourceId.HasValue)
            {
                if (QueryFilteringService.IsMetaSource(orderSourceId.Value))
                {
                    query = query.Where(o => o.OrderSource == OrderSourceEnum.فيسبوك || o.OrderSource == OrderSourceEnum.انستغرام);
                }
                else
                {
                    query = query.Where(o => o.OrderSource == orderSourceId.Value);
                }
            }

            if (workShift.HasValue)
            {
                if (workShift.Value)
                {
                    query = query.Where(o =>
                        o.InstantAddedDate.HasValue &&
                        o.InstantAddedDate.Value.TimeOfDay >= TimeSpan.FromHours(9) &&
                        o.InstantAddedDate.Value.TimeOfDay <= TimeSpan.FromHours(18).Add(TimeSpan.FromMinutes(45)));
                }
                else
                {
                    query = query.Where(o =>
                        o.InstantAddedDate.HasValue &&
                        (o.InstantAddedDate.Value.TimeOfDay >= TimeSpan.FromHours(18).Add(TimeSpan.FromMinutes(45)) ||
                         o.InstantAddedDate.Value.TimeOfDay <= TimeSpan.FromHours(4)));
                }
            }

            return query;
        }

        [Authorize(Roles = "Admin,ExecutiveDirector")]
        [HttpGet]
        public async Task<IActionResult> NoAdRatingSummary(
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? employeeId = null,
            Common.Countries? countryId = null,
            OrderSourceEnum? orderSourceId = null,
            bool? genderId = null,
            int? storeId = null,
            int? mainwarehouseId = null,
            bool? workShift = null)
        {
            var noAdRange = GetRatingDateRange(startDate, endDate);

            var query = BuildNoAdRatingOrdersQuery(
                noAdRange.StartDay,
                noAdRange.EndDay,
                employeeId,
                countryId,
                orderSourceId,
                genderId,
                storeId,
                mainwarehouseId,
                workShift);

            var items = await query
                .Where(o => !string.IsNullOrEmpty(o.ApplicationUserId))
                .GroupBy(o => o.ApplicationUserId)
                .Select(g => new
                {
                    employeeId = g.Key,
                    userId = g.Key,
                    count = g.Count()
                })
                .ToListAsync();

            return Json(new
            {
                success = true,
                totalEmployees = items.Count,
                totalOrders = items.Sum(i => i.count),
                items
            });
        }

        [Authorize(Roles = "Admin,ExecutiveDirector")]
        [HttpGet]
        public async Task<IActionResult> NoAdRatingDetails(
            string? employeeId,
            string? employeeName,
            DateTime? startDate = null,
            DateTime? endDate = null,
            Common.Countries? countryId = null,
            OrderSourceEnum? orderSourceId = null,
            bool? genderId = null,
            int? storeId = null,
            int? mainwarehouseId = null,
            bool? workShift = null)
        {
            var noAdRange = GetRatingDateRange(startDate, endDate);

            var query = BuildNoAdRatingOrdersQuery(
                noAdRange.StartDay,
                noAdRange.EndDay,
                employeeId,
                countryId,
                orderSourceId,
                genderId,
                storeId,
                mainwarehouseId,
                workShift);

            var rows = await query
                .OrderByDescending(o => o.InstantAddedDate)
                .ThenByDescending(o => o.Id)
                .Select(o => new
                {
                    o.Id,
                    o.ApplicationUserId,
                    o.CustomerName,
                    o.TelephoneNumber,
                    o.OrderStatus,
                    o.OrderSource,
                    o.TotalPrice,
                    o.InstantAddedDate,
                    o.CreatedDate,
                    StoreName = o.ManufacturingCompany == null ? "" : o.ManufacturingCompany.Name,
                    EmployeeName = _context.Employees
                        .Where(e => e.ApplicationUserId == o.ApplicationUserId)
                        .Select(e => e.DisplayName == null || e.DisplayName == "" ? e.Name : e.DisplayName)
                        .FirstOrDefault()
                })
                .Take(1000)
                .ToListAsync();

            var items = rows.Select(o => new
            {
                orderId = o.Id,
                employeeId = o.ApplicationUserId,
                employeeName = string.IsNullOrWhiteSpace(o.EmployeeName) ? (employeeName ?? "-") : o.EmployeeName,
                customerName = string.IsNullOrWhiteSpace(o.CustomerName) ? "-" : o.CustomerName,
                telephoneNumber = string.IsNullOrWhiteSpace(o.TelephoneNumber) ? "-" : o.TelephoneNumber,
                orderStatus = o.OrderStatus.ToString(),
                orderSource = o.OrderSource.ToString(),
                storeName = string.IsNullOrWhiteSpace(o.StoreName) ? "-" : o.StoreName,
                totalPrice = o.TotalPrice,
                createdAt = (o.InstantAddedDate.HasValue ? o.InstantAddedDate.Value : Convert.ToDateTime(o.CreatedDate)).ToString("yyyy/MM/dd HH:mm")
            }).ToList();

            return Json(new
            {
                success = true,
                items
            });
        }


        [Authorize]
        [HttpGet]
        public async Task<IActionResult> CurrentShiftOrdersCount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Json(new { count = 0 });

            var filteredOrdersQuery = _context.Orders.AsQueryable();
            filteredOrdersQuery = filteredOrdersQuery.Where(e => e.ApplicationUserId == userId);

            var now = _timeService.GetIstanbulTimeWithOffset();
            DateTime startDay, endDay;
            if (now.TimeOfDay < new TimeSpan(10, 30, 0))
            {
                startDay = now.Date.AddDays(-1).AddHours(10).AddMinutes(30);
                endDay = now.Date.AddHours(10).AddMinutes(30);
            }
            else
            {
                startDay = now.Date.AddHours(10).AddMinutes(30);
                endDay = now.Date.AddDays(1).AddHours(10).AddMinutes(30);
            }

            filteredOrdersQuery = filteredOrdersQuery
                .Where(x => x.InstantAddedDate >= startDay && x.InstantAddedDate < endDay);

            var count = await filteredOrdersQuery.CountAsync();
            return Json(new { count });
        }


        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public async Task<IActionResult> StoreList(
            Common.Countries? countyId = null,
            bool? genderId = null,
            int? storeId = null,
            int? mainwarehouseId = null,
            Common.Countries? countryId = null,
            OrderSourceEnum? orderSourceId = null,
            DateTime? startDay = null,
            DateTime? endDay = null,
            string? employeeId = null,
            bool? workShift = null,
            bool? fromComments = null)
        {
            // Keep employee query (but won't use it)
            IEnumerable<Employee> employeeQuery = await _dataCacheService.GetCachedEmployeesAsync();
            var mainWarehouses = await _dataCacheService.GetCachedMainWarehousesAsync();

            var filteredOrdersQuery = _context.Orders
                .Include(a => a.OrderWarehouses)
                .ThenInclude(o => o.Warehouse)
                .AsQueryable();

            // Same filtering logic (even unused filters)
            if (!string.IsNullOrEmpty(employeeId))
            {
                filteredOrdersQuery = filteredOrdersQuery.Where(e => e.ApplicationUserId == employeeId);
            }

            // Same date/time logic (10 AM rule)
            var now = _timeService.GetIstanbulTimeWithOffset();
            if (startDay == null && endDay == null)
            {
                if (now.TimeOfDay < new TimeSpan(10, 30, 0))
                {
                    startDay = now.Date.AddDays(-1).AddHours(10).AddMinutes(30);
                    endDay = now.Date.AddHours(10).AddMinutes(30);
                }
                else
                {
                    startDay = now.Date.AddHours(10).AddMinutes(30);
                    endDay = now.Date.AddDays(1).AddHours(10).AddMinutes(30);
                }
            }
            else
            {
                startDay = startDay?.Date.AddHours(10).AddMinutes(30);
                endDay = endDay?.Date.AddHours(10).AddMinutes(30);
            }


            var fixedOrders = filteredOrdersQuery
                .Where(o => o.FixedOrderDate >= startDay && o.FixedOrderDate <= endDay)
                .ToList();

            filteredOrdersQuery = filteredOrdersQuery
                .Where(x => x.InstantAddedDate >= startDay && x.InstantAddedDate < endDay);

            // Same shift filtering
            if (workShift.HasValue)
            {
                if (workShift == true) // Morning shift
                {
                    filteredOrdersQuery = filteredOrdersQuery.Where(o =>
                        o.InstantAddedDate.HasValue &&
                        o.InstantAddedDate.Value.TimeOfDay >= TimeSpan.FromHours(9) &&
                        o.InstantAddedDate.Value.TimeOfDay <= TimeSpan.FromHours(18).Add(TimeSpan.FromMinutes(45)));
                }
                else if (workShift == false) // Evening shift (Night)
                {
                    filteredOrdersQuery = filteredOrdersQuery.Where(o =>
                        o.InstantAddedDate.HasValue &&
                        (o.InstantAddedDate.Value.TimeOfDay >= TimeSpan.FromHours(18).Add(TimeSpan.FromMinutes(45)) ||
                        o.InstantAddedDate.Value.TimeOfDay <= TimeSpan.FromHours(4)));
                }
            }

            // Same warehouse/store filtering
            if (mainwarehouseId.HasValue)
                filteredOrdersQuery = filteredOrdersQuery.Where(o => o.OrderWarehouses.Any(ow => ow.Warehouse.MainWarehouseId == mainwarehouseId));

            if (genderId.HasValue)
                filteredOrdersQuery = filteredOrdersQuery.Where(o => o.Gender == genderId);

            if (countryId.HasValue)
                filteredOrdersQuery = filteredOrdersQuery.Where(o => o.Country == countryId);

            if (storeId.HasValue)
                filteredOrdersQuery = filteredOrdersQuery.Where(o => o.ManufacturingCompanyId == storeId);

            if (orderSourceId.HasValue)
            {
                if (QueryFilteringService.IsMetaSource(orderSourceId.Value))
                    filteredOrdersQuery = filteredOrdersQuery.Where(x => x.OrderSource == OrderSourceEnum.فيسبوك || x.OrderSource == OrderSourceEnum.انستغرام);
                else
                    filteredOrdersQuery = filteredOrdersQuery.Where(x => x.OrderSource == orderSourceId.Value);
            }

            if (countyId.HasValue)
                filteredOrdersQuery = filteredOrdersQuery.Where(x => x.Country == countyId.Value);

            if (fromComments.HasValue)
                filteredOrdersQuery = filteredOrdersQuery.Where(x => x.FromComments == fromComments.Value);

            // Same order data projection (EXACTLY as in Employeelist)
            var orderData = await filteredOrdersQuery
                .Include(o => o.OrderWarehouses)
                    .ThenInclude(ow => ow.Warehouse)
                .Include(o => o.ManufacturingCompany)
                .Select(o => new
                {
                    o.ApplicationUserId,
                    o.Fixedby,
                    o.Id,
                    o.TotalPrice,
                    o.DeliveryCompanyId,
                    o.Country,
                    o.IsBonus,
                    HasWarehouseWithMoreThanOneItem = o.OrderWarehouses.Any(ow => ow.Amount > 1),
                    HasMoreThanOneWarehouse = o.OrderWarehouses.GroupBy(ow => ow.WarehouseId).Count() > 1,
                    OrderWarehouses = o.OrderWarehouses.Select(ow => new
                    {
                        ow.WarehouseId,
                        WarehouseName = ow.Warehouse.Name,
                        ow.Amount,
                        MainWarehouseId = ow.Warehouse.MainWarehouseId
                    }).ToList(),
                    o.FixedOrderDate,
                    o.Gender,
                    o.FromComments,
                    TotalProductsCount = o.OrderWarehouses.Sum(ow => ow.Amount),
                    Storename = o.ManufacturingCompany.Name,
                    StoreId = o.ManufacturingCompany.Id,
                    StoreImage = o.ManufacturingCompany.ImageUrl,
                    o.IsDiscount,
                    o.DeliveryPrice
                })
                .ToListAsync();

            // Same calculations (EXACTLY as in Employeelist)
            int totalNumberOfOrders = orderData.Count;
            var orderFromComments = orderData.Count(o => o.FromComments);
            var orderFromMales = orderData.Count(o => o.Gender);
            var orderFromFemales = orderData.Count(o => !o.Gender);
            var orderFromOffers = orderData.Count(o => o.HasWarehouseWithMoreThanOneItem || o.HasMoreThanOneWarehouse);
            var orderTotalProductCount = orderData.Sum(o => o.TotalProductsCount);
            var fixedOrdersCount = fixedOrders.Count;
            int DiscountOrderCount = orderData.Count(o => o.IsDiscount);

            var allOrderIds = orderData.Select(o => o.Id).ToList();

            // Same price calculations (EXACTLY as in Employeelist)
            var totalOrdersPriceInUSD = _deliveryCompanyService.CalculateTotalPriceInUSDForOrdersWithOutDeliveryCompanyPrice(allOrderIds);
            var totalOrdersPriceInTL = _currencyExchangeService.ConvertToTurkishLira(totalOrdersPriceInUSD);

            var totalOrderBonuses = orderData.Count(o => o.IsBonus);

            // Same price dictionaries (EXACTLY as in Employeelist)
            var priceInUSDByOrderId = orderData.ToDictionary(
                o => o.Id,
                o => _deliveryCompanyService.CalculateTotalPriceInUSDForOrdersWithOutDeliveryCompanyPrice(new List<int> { o.Id }));

            var priceInTLByOrderId = priceInUSDByOrderId.ToDictionary(
                kvp => kvp.Key,
                kvp => _currencyExchangeService.ConvertToTurkishLira(kvp.Value));

            // Get all stores (EXACTLY as in Employeelist)
            var allStores = _context.ManufacturingCompanies
                .Where(a => a.IsShown)
                .Select(mc => new { mc.Id, mc.Name, mc.ImageUrl })
                .ToList();

            // PotentialOrders query for StoreList
            var potentialOrdersQuery = _context.PotentialOrders
                .Where(po => po.CreatedDate >= startDay && po.CreatedDate < endDay);
            if (!string.IsNullOrEmpty(employeeId))
                potentialOrdersQuery = potentialOrdersQuery.Where(po => po.ApplicationUserId == employeeId);
            if (countryId.HasValue)
                potentialOrdersQuery = potentialOrdersQuery.Where(po => po.Country == countryId.Value);
            if (countyId.HasValue)
                potentialOrdersQuery = potentialOrdersQuery.Where(po => po.Country == countyId.Value);
            if (storeId.HasValue)
            {
                var storeName = allStores.FirstOrDefault(s => s.Id == storeId.Value)?.Name;
                if (storeName != null)
                    potentialOrdersQuery = potentialOrdersQuery.Where(po => po.StoreName == storeName);
            }
            if (orderSourceId.HasValue)
            {
                if (QueryFilteringService.IsMetaSource(orderSourceId.Value))
                    potentialOrdersQuery = potentialOrdersQuery.Where(po => po.OrderSource == OrderSourceEnum.فيسبوك || po.OrderSource == OrderSourceEnum.انستغرام);
                else
                    potentialOrdersQuery = potentialOrdersQuery.Where(po => po.OrderSource == orderSourceId.Value);
            }

            var potentialOrdersByStore = await potentialOrdersQuery
                .GroupBy(po => po.StoreName)
                .Select(g => new { StoreName = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.StoreName, g => g.Count);
            int totalPotentialOrdersCount = potentialOrdersByStore.Values.Sum();

            // Same store processing (EXACTLY as in Employeelist)
            var totalPriceWithoutDeliveryTasksByStores = await Task.WhenAll(allStores.Select(async store =>
            {
                var storeOrders = orderData.Where(o => o.StoreId == store.Id).ToList();
                var storeOrderIds = storeOrders.Select(o => o.Id).ToList();

                var totalPriceWithoutDeliveryUSD = storeOrderIds.Sum(id => priceInUSDByOrderId[id]);
                var totalPriceWithoutDeliveryTL = storeOrderIds.Sum(id => priceInTLByOrderId[id]);

                return new OrderCountByStore
                {
                    Name = store.Name,
                    StoreId = store.Id,
                    StoreImage = store.ImageUrl,
                    NumberOfBonuses = storeOrders.Count(o => o.IsBonus),
                    OrderFromCommentsCount = storeOrders.Count(o => o.FromComments),
                    OrderCount = storeOrders.Count,
                    OrderFromOffersCount = storeOrders.Count(o =>
                        o.HasWarehouseWithMoreThanOneItem || o.HasMoreThanOneWarehouse),
                    OrderFromMalesCount = _decimalFormattingService.DecimalFormat(orderFromMales),
                    OrderFromFemalesCount = _decimalFormattingService.DecimalFormat(orderFromFemales),
                    FixedOrderCount = _decimalFormattingService.DecimalFormat(fixedOrdersCount),
                    TotalPriceWithoutDeliveryUSD = _decimalFormattingService.DecimalFormat(totalPriceWithoutDeliveryUSD),
                    TotalPriceWithoutDeliveryTl = _decimalFormattingService.DecimalFormat(totalPriceWithoutDeliveryTL),
                    Rating = (totalNumberOfOrders > 0) ?
                        Math.Round((storeOrders.Count / (decimal)totalNumberOfOrders) * 100, 2) : 0,
                    PotentialOrdersCount = potentialOrdersByStore.GetValueOrDefault(store.Name, 0)
                };
            }));

            var ordersByStore = totalPriceWithoutDeliveryTasksByStores
                .OrderByDescending(s => s.OrderCount)
                .ToList();

            // Return the SAME EmployeeRatingCompositeViewModel (with empty employee/country lists)
            var compositeViewModel = new EmployeeRatingCompositeViewModel
            {
                EmployeeRatings = new List<RatingViewModel>(), // Empty list
                OrdersByCountry = new List<OrderCountByCountry>(), // Empty list
                OrdersByStore = ordersByStore,
                NumberOfBouneses = totalOrderBonuses,
                OrderFromOfferDiscountsCount = _decimalFormattingService.DecimalFormat(DiscountOrderCount),
                TotalNumberOfOrders = _decimalFormattingService.DecimalFormat(totalNumberOfOrders),
                OrderFromCommentsCount = _decimalFormattingService.DecimalFormat(orderFromComments),
                OrderFromMalesCount = _decimalFormattingService.DecimalFormat(orderFromMales),
                OrderFromFemalesCount = _decimalFormattingService.DecimalFormat(orderFromFemales),
                FixedOrdersCount = _decimalFormattingService.DecimalFormat(fixedOrdersCount),
                OrderFromOffersCount = _decimalFormattingService.DecimalFormat(orderFromOffers),
                TotalOrdersPriceInDollar = _decimalFormattingService.DecimalFormat(totalOrdersPriceInUSD),
                TotalOrdersPriceInTL = _decimalFormattingService.DecimalFormat(totalOrdersPriceInTL),
                TotalProductsCount = _decimalFormattingService.DecimalFormat(orderTotalProductCount),
                PotentialOrdersCount = _decimalFormattingService.DecimalFormat(totalPotentialOrdersCount)
            };

            return View(compositeViewModel); // Reuse same view
        }


        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public async Task<IActionResult> CountryList(
            Common.Countries? countyId = null,
            bool? genderId = null,
            int? storeId = null,
            int? mainwarehouseId = null,
            Common.Countries? countryId = null,
            OrderSourceEnum? orderSourceId = null,
            DateTime? startDay = null,
            DateTime? endDay = null,
            string? employeeId = null,
            bool? workShift = null,
            bool? fromComments = null)
        {
            // Keep all the same initialization
            IEnumerable<Employee> employeeQuery = await _dataCacheService.GetCachedEmployeesAsync();
            var mainWarehouses = await _dataCacheService.GetCachedMainWarehousesAsync();

            var filteredOrdersQuery = _context.Orders
                .Include(a => a.OrderWarehouses)
                .ThenInclude(o => o.Warehouse)
                .AsQueryable();

            // Keep all the same filtering logic
            if (!string.IsNullOrEmpty(employeeId))
            {
                filteredOrdersQuery = filteredOrdersQuery.Where(e => e.ApplicationUserId == employeeId);
            }

            // Same date/time logic
            var now = _timeService.GetIstanbulTimeWithOffset();
            if (startDay == null && endDay == null)
            {
                if (now.TimeOfDay < new TimeSpan(10, 30, 0))
                {
                    startDay = now.Date.AddDays(-1).AddHours(10).AddMinutes(30);
                    endDay = now.Date.AddHours(10).AddMinutes(30);
                }
                else
                {
                    startDay = now.Date.AddHours(10).AddMinutes(30);
                    endDay = now.Date.AddDays(1).AddHours(10).AddMinutes(30);
                }
            }
            else
            {
                startDay = startDay?.Date.AddHours(10).AddMinutes(30);
                endDay = endDay?.Date.AddHours(10).AddMinutes(30);
            }


            var fixedOrders = filteredOrdersQuery
                .Where(o => o.FixedOrderDate >= startDay && o.FixedOrderDate <= endDay)
                .ToList();

            filteredOrdersQuery = filteredOrdersQuery
                .Where(x => x.InstantAddedDate >= startDay && x.InstantAddedDate < endDay);

            // Same shift filtering
            if (workShift.HasValue)
            {
                if (workShift == true)
                {
                    filteredOrdersQuery = filteredOrdersQuery.Where(o =>
                        o.InstantAddedDate.HasValue &&
                        o.InstantAddedDate.Value.TimeOfDay >= TimeSpan.FromHours(9) &&
                        o.InstantAddedDate.Value.TimeOfDay <= TimeSpan.FromHours(18).Add(TimeSpan.FromMinutes(45)));
                }
                else if (workShift == false)
                {
                    filteredOrdersQuery = filteredOrdersQuery.Where(o =>
                        o.InstantAddedDate.HasValue &&
                        (o.InstantAddedDate.Value.TimeOfDay >= TimeSpan.FromHours(18).Add(TimeSpan.FromMinutes(45)) ||
                        o.InstantAddedDate.Value.TimeOfDay <= TimeSpan.FromHours(4)));
                }
            }

            // Same filtering
            if (mainwarehouseId.HasValue)
                filteredOrdersQuery = filteredOrdersQuery.Where(o => o.OrderWarehouses.Any(ow => ow.Warehouse.MainWarehouseId == mainwarehouseId));

            if (genderId.HasValue)
                filteredOrdersQuery = filteredOrdersQuery.Where(o => o.Gender == genderId);

            if (countryId.HasValue)
                filteredOrdersQuery = filteredOrdersQuery.Where(o => o.Country == countryId);

            if (storeId.HasValue)
                filteredOrdersQuery = filteredOrdersQuery.Where(o => o.ManufacturingCompanyId == storeId);

            if (orderSourceId.HasValue)
            {
                if (QueryFilteringService.IsMetaSource(orderSourceId.Value))
                    filteredOrdersQuery = filteredOrdersQuery.Where(x => x.OrderSource == OrderSourceEnum.فيسبوك || x.OrderSource == OrderSourceEnum.انستغرام);
                else
                    filteredOrdersQuery = filteredOrdersQuery.Where(x => x.OrderSource == orderSourceId.Value);
            }

            if (countyId.HasValue)
                filteredOrdersQuery = filteredOrdersQuery.Where(x => x.Country == countyId.Value);

            if (fromComments.HasValue)
                filteredOrdersQuery = filteredOrdersQuery.Where(x => x.FromComments == fromComments.Value);

            // Same order data projection
            var orderData = await filteredOrdersQuery
                .Include(o => o.OrderWarehouses)
                    .ThenInclude(ow => ow.Warehouse)
                .Include(o => o.ManufacturingCompany)
                .Select(o => new
                {
                    o.ApplicationUserId,
                    o.Fixedby,
                    o.Id,
                    o.TotalPrice,
                    o.DeliveryCompanyId,
                    o.Country,
                    o.IsBonus,
                    HasWarehouseWithMoreThanOneItem = o.OrderWarehouses.Any(ow => ow.Amount > 1),
                    HasMoreThanOneWarehouse = o.OrderWarehouses.GroupBy(ow => ow.WarehouseId).Count() > 1,
                    OrderWarehouses = o.OrderWarehouses.Select(ow => new
                    {
                        ow.WarehouseId,
                        WarehouseName = ow.Warehouse.Name,
                        ow.Amount,
                        MainWarehouseId = ow.Warehouse.MainWarehouseId
                    }).ToList(),
                    o.FixedOrderDate,
                    o.Gender,
                    o.FromComments,
                    TotalProductsCount = o.OrderWarehouses.Sum(ow => ow.Amount),
                    Storename = o.ManufacturingCompany.Name,
                    StoreId = o.ManufacturingCompany.Id,
                    StoreImage = o.ManufacturingCompany.ImageUrl,
                    o.IsDiscount,
                    o.DeliveryPrice
                })
                .ToListAsync();

            // Same calculations
            int totalNumberOfOrders = orderData.Count;
            var orderFromComments = orderData.Count(o => o.FromComments);
            var orderFromMales = orderData.Count(o => o.Gender);
            var orderFromFemales = orderData.Count(o => !o.Gender);
            var orderFromOffers = orderData.Count(o => o.HasWarehouseWithMoreThanOneItem || o.HasMoreThanOneWarehouse);
            var orderTotalProductCount = orderData.Sum(o => o.TotalProductsCount);
            var fixedOrdersCount = fixedOrders.Count;
            int DiscountOrderCount = orderData.Count(o => o.IsDiscount);

            var allOrderIds = orderData.Select(o => o.Id).ToList();

            // Same price calculations
            var totalOrdersPriceInUSD = _deliveryCompanyService.CalculateTotalPriceInUSDForOrdersWithOutDeliveryCompanyPrice(allOrderIds);
            var totalOrdersPriceInTL = _currencyExchangeService.ConvertToTurkishLira(totalOrdersPriceInUSD);

            var totalOrderBonuses = orderData.Count(o => o.IsBonus);

            // Same price dictionaries
            var priceInUSDByOrderId = orderData.ToDictionary(
                o => o.Id,
                o => _deliveryCompanyService.CalculateTotalPriceInUSDForOrdersWithOutDeliveryCompanyPrice(new List<int> { o.Id }));

            var priceInTLByOrderId = priceInUSDByOrderId.ToDictionary(
                kvp => kvp.Key,
                kvp => _currencyExchangeService.ConvertToTurkishLira(kvp.Value));

            // Get all countries (EXACTLY as in original)
            var allCountries = Enum.GetValues(typeof(Countries)).Cast<Countries>();
            var excludedCountries = new HashSet<Countries>
    {
        Countries.تونس,
        Countries.السعودية,
        Countries.الأردن,
        Countries.لبنان,
        Countries.الجزائر,
        Countries.المغرب
    };
            var filteredCountries = allCountries.Except(excludedCountries);

            // PotentialOrders query for CountryList
            var potentialOrdersQuery = _context.PotentialOrders
                .Where(po => po.CreatedDate >= startDay && po.CreatedDate < endDay);
            if (!string.IsNullOrEmpty(employeeId))
                potentialOrdersQuery = potentialOrdersQuery.Where(po => po.ApplicationUserId == employeeId);
            if (countryId.HasValue)
                potentialOrdersQuery = potentialOrdersQuery.Where(po => po.Country == countryId.Value);
            if (countyId.HasValue)
                potentialOrdersQuery = potentialOrdersQuery.Where(po => po.Country == countyId.Value);
            if (storeId.HasValue)
            {
                var storeName = _context.ManufacturingCompanies.Where(m => m.Id == storeId.Value).Select(m => m.Name).FirstOrDefault();
                if (storeName != null)
                    potentialOrdersQuery = potentialOrdersQuery.Where(po => po.StoreName == storeName);
            }
            if (orderSourceId.HasValue)
            {
                if (QueryFilteringService.IsMetaSource(orderSourceId.Value))
                    potentialOrdersQuery = potentialOrdersQuery.Where(po => po.OrderSource == OrderSourceEnum.فيسبوك || po.OrderSource == OrderSourceEnum.انستغرام);
                else
                    potentialOrdersQuery = potentialOrdersQuery.Where(po => po.OrderSource == orderSourceId.Value);
            }

            var potentialOrdersByCountry = await potentialOrdersQuery
                .GroupBy(po => po.Country)
                .Select(g => new { Country = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Country, g => g.Count);
            int totalPotentialOrdersCount = potentialOrdersByCountry.Values.Sum();

            // Process country data (EXACTLY as in original)
            var totalPriceWithoutDeliveryTasksByCountries = await Task.WhenAll(filteredCountries.Select(async country =>
            {
                var countryOrders = orderData.Where(o => o.Country == country).ToList();
                var countryOrderIds = countryOrders.Select(o => o.Id).ToList();

                var totalPriceWithoutDeliveryUSD = countryOrderIds.Sum(id => priceInUSDByOrderId[id]);
                var totalPriceWithoutDeliveryTL = countryOrderIds.Sum(id => priceInTLByOrderId[id]);
                var totalPriceWithoutDeliveryLocal = countryOrders.Sum(o => o.TotalPrice - (o.DeliveryPrice));

                return new OrderCountByCountry
                {
                    Country = country.ToString(),
                    TotalPriceWithoutDeliverylocal = _decimalFormattingService.DecimalFormat(totalPriceWithoutDeliveryLocal),
                    CountryId = (int)country,
                    OrderCount = countryOrders.Count,
                    NumberOfBonuses = countryOrders.Count(o => o.IsBonus),
                    OrderFromOffersCount = countryOrders.Count(o => o.HasWarehouseWithMoreThanOneItem || o.HasMoreThanOneWarehouse),
                    OrderFromCommentsCount = _decimalFormattingService.DecimalFormat(countryOrders.Count(o => o.FromComments)),
                    OrderFromMalesCount = _decimalFormattingService.DecimalFormat(countryOrders.Count(o => o.Gender)),
                    OrderFromFemalesCount = _decimalFormattingService.DecimalFormat(countryOrders.Count(o => !o.Gender)),
                    FixedOrderCount = _decimalFormattingService.DecimalFormat(countryOrders.Count(o => o.FixedOrderDate != null)),
                    TotalPriceWithoutDeliveryUSD = _decimalFormattingService.DecimalFormat(totalPriceWithoutDeliveryUSD),
                    TotalPriceWithoutDeliveryTl = _decimalFormattingService.DecimalFormat(totalPriceWithoutDeliveryTL),
                    Rating = Math.Round((totalNumberOfOrders > 0) ? (countryOrders.Count / (decimal)totalNumberOfOrders) * 100 : 0, 2),
                    TotalProductsCount = countryOrders.Sum(o => o.TotalProductsCount),
                    PotentialOrdersCount = potentialOrdersByCountry.GetValueOrDefault(country, 0)
                };
            }));

            var ordersByCountry = totalPriceWithoutDeliveryTasksByCountries
                .OrderByDescending(c => c.OrderCount)
                .ToList();

            // Return the SAME EmployeeRatingCompositeViewModel (with empty employee/store lists)
            var compositeViewModel = new EmployeeRatingCompositeViewModel
            {
                EmployeeRatings = new List<RatingViewModel>(), // Empty
                OrdersByCountry = ordersByCountry, // Only countries populated
                OrdersByStore = new List<OrderCountByStore>(), // Empty
                NumberOfBouneses = totalOrderBonuses,
                OrderFromOfferDiscountsCount = _decimalFormattingService.DecimalFormat(DiscountOrderCount),
                TotalNumberOfOrders = _decimalFormattingService.DecimalFormat(totalNumberOfOrders),
                OrderFromCommentsCount = _decimalFormattingService.DecimalFormat(orderFromComments),
                OrderFromMalesCount = _decimalFormattingService.DecimalFormat(orderFromMales),
                OrderFromFemalesCount = _decimalFormattingService.DecimalFormat(orderFromFemales),
                FixedOrdersCount = _decimalFormattingService.DecimalFormat(fixedOrdersCount),
                OrderFromOffersCount = _decimalFormattingService.DecimalFormat(orderFromOffers),
                TotalOrdersPriceInDollar = _decimalFormattingService.DecimalFormat(totalOrdersPriceInUSD),
                TotalOrdersPriceInTL = _decimalFormattingService.DecimalFormat(totalOrdersPriceInTL),
                TotalProductsCount = _decimalFormattingService.DecimalFormat(orderTotalProductCount),
                PotentialOrdersCount = _decimalFormattingService.DecimalFormat(totalPotentialOrdersCount)
            };

            return View(compositeViewModel); // Reuse same view
        }

        //  done
        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public async Task<IActionResult> ManufactureCompanyDetails(

            Common.Countries? countryId = null,
            bool? genderId = null,
            int? storeId = null,
            int? mainwarehouseId = null,
            OrderSourceEnum? orderSourceId = null,
            DateTime? startDay = null,
            DateTime? endDay = null,
            string? employeeId = null,
            bool? workShift = null,
            bool? fromComments = null)

        {
            var filteredOrdersQuery = _context.Orders.AsNoTracking()
                .Include(o => o.ManufacturingCompany)
                .Include(o => o.OrderWarehouses).ThenInclude(ow => ow.Warehouse)
                .AsQueryable();


            // Handle date range filtering directly in the action
            var now = _timeService.GetIstanbulTimeWithOffset(); // Get the current Istanbul time
            if (startDay == null && endDay == null)
            {
                if (now.TimeOfDay < new TimeSpan(10, 30, 0))
                {
                    startDay = now.Date.AddDays(-1).AddHours(10).AddMinutes(30);
                    endDay = now.Date.AddHours(10).AddMinutes(30);
                }
                else
                {
                    startDay = now.Date.AddHours(10).AddMinutes(30);
                    endDay = now.Date.AddDays(1).AddHours(10).AddMinutes(30);
                }
            }
            else
            {
                startDay = startDay?.Date.AddHours(10).AddMinutes(30);
                endDay = endDay?.Date.AddHours(10).AddMinutes(30);
            }


            // Filter orders based on the provided date range
            filteredOrdersQuery = filteredOrdersQuery
                .Where(x => x.InstantAddedDate >= startDay && x.InstantAddedDate < endDay);

            // Handle work shift filtering directly in the action
            if (workShift.HasValue)
            {
                filteredOrdersQuery = workShift == true
                    ? filteredOrdersQuery.Where(o =>
                        o.InstantAddedDate.HasValue &&
                        o.InstantAddedDate.Value.TimeOfDay >= TimeSpan.FromHours(9) &&
                        o.InstantAddedDate.Value.TimeOfDay <= TimeSpan.FromHours(18).Add(TimeSpan.FromMinutes(45)))
                    : filteredOrdersQuery.Where(o =>
                        o.InstantAddedDate.HasValue &&
                        (o.InstantAddedDate.Value.TimeOfDay >= TimeSpan.FromHours(18).Add(TimeSpan.FromMinutes(45)) ||
                        o.InstantAddedDate.Value.TimeOfDay <= TimeSpan.FromHours(4)));
            }

            // Apply filters using the QueryFilteringService
            filteredOrdersQuery = _queryFilteringService.ApplyFilters(
                filteredOrdersQuery,
                countryId,
                null, // No order status filter needed here
                orderSourceId,
                storeId,
                null, // No delivery company filter needed here
                null, // No delivery representative filter needed here
                null, // No product ID filter needed here
                startDay,
                endDay,
                null, // No city filter needed here
                null, // No search term filter needed here
                employeeId, // Apply the employee ID filter
                fromComments,
                genderId,
                null, // No isOffers filter needed here
                null, // No isDiscount filter needed here
                null, // No isBonus filter needed here
                null, // No isSpecialClients filter needed here
                null, // No isFixedAndDelivered filter needed here
                null, // No isHidden filter needed here
                null, // No isComplaints filter needed here
                null, // No isPaid filter needed here
                mainwarehouseId // Apply the main warehouse ID filter
            );


            var numberOfFixedOrders = await _orderService.GetNumberOfFixedOrdersAsync(filteredOrdersQuery, startDay, endDay);



            var allOrders = await filteredOrdersQuery
                .Select(order => new
                {
                    order.Id,
                    order.ManufacturingCompanyId,
                    CompanyName = order.ManufacturingCompany.Name,
                    CompanyImage = order.ManufacturingCompany.ImageUrl,
                    order.Country,
                    order.OrderSource,
                    order.TotalPrice,
                    order.FromComments,
                    order.Gender,
                    HasWarehouseWithMoreThanOneItem = order.OrderWarehouses.Any(ow => ow.Amount > 1),
                    HasMoreThanOneWarehouse = order.OrderWarehouses.GroupBy(ow => ow.WarehouseId).Count() > 1,
                    TotalProductsCount = order.OrderWarehouses.Sum(ow => ow.Amount),
                    order.IsDiscount,
                    order.State,
                    order.DeliveryCompanyId,
                    order.IsBonus
                })
                .ToListAsync();

            var totalPricesBySource = await _orderService.CalculateTotalPricesForOrdersWithOutDeliveryCompanyAsync(
                allOrders.Select(o => new Order
                {
                    ManufacturingCompanyId = o.ManufacturingCompanyId,
                    Country = o.Country,
                    OrderSource = o.OrderSource,
                    TotalPrice = o.TotalPrice,
                    State = o.State,
                    DeliveryCompanyId = o.DeliveryCompanyId
                }).ToList());

            var manufacturingCompanyOrders = new List<ManufacturingCompanyOrderDetailsViewModel>();

            foreach (var group in allOrders.GroupBy(order => order.ManufacturingCompanyId))
            {
                var firstOrder = group.First();

                var countriesOrderInfo = new List<CountryOrderInfo>();

                foreach (var countryGroup in group.GroupBy(order => order.Country))
                {
                    var sourcePrices = countryGroup
                        .GroupBy(o => o.OrderSource)
                        .ToDictionary(
                            srcGroup => (OrderSourceEnum)srcGroup.Key,
                            srcGroup => totalPricesBySource[
                                new
                                {
                                    ManufacturingCompanyId = srcGroup.First().ManufacturingCompanyId,
                                    Country = srcGroup.First().Country,
                                    OrderSource = (OrderSourceEnum)srcGroup.Key
                                }
                            ]);

                    var ordersBySource = countryGroup
                        .GroupBy(o => o.OrderSource)
                        .ToDictionary(
                            srcGroup => (OrderSourceEnum)srcGroup.Key,
                            srcGroup => srcGroup.Count());

                    var filteredOrdersByCountryQuery = filteredOrdersQuery
                        .Where(o => o.Country == countryGroup.Key)
                        .AsQueryable();

                    var numberOfFixedOrdersdetails = await _orderService.GetNumberOfFixedOrdersAsync(filteredOrdersByCountryQuery, startDay, endDay);

                    countriesOrderInfo.Add(new CountryOrderInfo
                    {
                        CountryId = (int)countryGroup.Key,
                        Country = countryGroup.Key,
                        TotalOrders = countryGroup.Count(),
                        OrdersBySource = ordersBySource,
                        TotalPriceBySourceUSD = sourcePrices.ToDictionary(pair => pair.Key, pair => _decimalFormattingService.DecimalFormat(pair.Value.TotalPriceUSD)),
                        TotalPriceBySourceTRY = sourcePrices.ToDictionary(pair => pair.Key, pair => _decimalFormattingService.DecimalFormat(_currencyExchangeService.ConvertToTurkishLira(pair.Value.TotalPriceUSD))),
                        TotalPriceLocalCurrency = sourcePrices.ToDictionary(pair => pair.Key, pair => _decimalFormattingService.DecimalFormat(pair.Value.TotalPriceLocalCurrency)),
                        TotalLocalCurrencyPriceSum = _decimalFormattingService.DecimalFormat(sourcePrices.Values.Sum(v => v.TotalPriceLocalCurrency)),
                        TotalUsdPriceSum = _decimalFormattingService.DecimalFormat(sourcePrices.Values.Sum(v => v.TotalPriceUSD)),

                        // New values for each country
                        FixedOrdersCount = numberOfFixedOrdersdetails.ToString(),
                        OrderFromOffersCount = countryGroup.Count(o => o.HasWarehouseWithMoreThanOneItem || o.HasMoreThanOneWarehouse).ToString(),
                        OrderFromOfferDiscountsCount = countryGroup.Count(o => o.IsDiscount).ToString(),
                        NumberOfBonuses = countryGroup.Count(o => o.IsBonus),
                        TotalProductsCount = countryGroup.Sum(o => o.TotalProductsCount).ToString(),
                        OrderFromCommentsCount = countryGroup.Count(o => o.FromComments).ToString(),
                        OrderFromFemalesCount = countryGroup.Count(o => !o.Gender).ToString(),
                        OrderFromMalesCount = countryGroup.Count(o => o.Gender).ToString(),
                    });
                }

                manufacturingCompanyOrders.Add(new ManufacturingCompanyOrderDetailsViewModel
                {
                    StoreId = firstOrder.ManufacturingCompanyId,
                    StoreName = firstOrder.CompanyName,
                    StoreImage = firstOrder.CompanyImage,
                    CountriesOrderInfo = countriesOrderInfo,
                });
            }

            // Calculate total prices
            var totalUsdPriceSum = manufacturingCompanyOrders
                .Sum(company => company.CountriesOrderInfo
                    .Sum(country => decimal.Parse(country.TotalUsdPriceSum)));

            var totalTryPriceSum = manufacturingCompanyOrders
                .Sum(company => company.CountriesOrderInfo
                    .Sum(country => _currencyExchangeService.ConvertToTurkishLira(decimal.Parse(country.TotalUsdPriceSum))));

            // Create summary data
            var summaryModel = new ManufacturingOrderSummaryViewModel
            {
                TotalOrders = allOrders.Count,
                FixedOrdersCount = manufacturingCompanyOrders
                .Sum(m => m.CountriesOrderInfo
                    .Sum(c => int.Parse(c.FixedOrdersCount)))
                .ToString(),
                OrderFromOffersCount = allOrders.Count(o => o.HasWarehouseWithMoreThanOneItem || o.HasMoreThanOneWarehouse).ToString(),
                OrderFromOfferDiscountsCount = allOrders.Count(o => o.IsDiscount).ToString(),
                NumberOfBonuses = allOrders.Count(o => o.IsBonus),
                TotalProductsCount = allOrders.Sum(o => o.TotalProductsCount).ToString(),
                OrderFromCommentsCount = allOrders.Count(o => o.FromComments).ToString(),
                OrderFromFemalesCount = allOrders.Count(o => !o.Gender).ToString(),
                OrderFromMalesCount = allOrders.Count(o => o.Gender).ToString(),
                TotalPriceUSD = totalUsdPriceSum,
                TotalPriceTRY = totalTryPriceSum,
                ManufacturingCompanyOrders = manufacturingCompanyOrders
            };

            return View(summaryModel);
        }








        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public async Task<IActionResult> CityByCountryDetails(
            string selectedDateRange = null,
            bool? gender = null,
            string employeeUserId = null,
            Countries? countryId = null
        )
        {
            var ordersQuery = _context.Orders.AsNoTracking();


            if (gender.HasValue)
            {
                ordersQuery = ordersQuery.Where(o => o.Gender == gender);
            }

            if (!string.IsNullOrEmpty(selectedDateRange))
            {
                var dateRangeParts = selectedDateRange.Split(new[] { "إلى" }, StringSplitOptions.RemoveEmptyEntries);
                if (dateRangeParts.Length == 2)
                {
                    if (DateTime.TryParse(dateRangeParts[0].Trim(), out var startDate) &&
                        DateTime.TryParse(dateRangeParts[1].Trim(), out var endDate))
                    {
                        // Adjust startDate to the beginning of the day
                        // Adjust startDate to 10:00 AM of the selected date
                        // Adjust startDate to 10:00 AM of the selected date
                        startDate = startDate.Date.AddHours(10);
                        // Adjust endDate to 10:00 AM of the day after the selected end date
                        endDate = endDate.Date.AddHours(10);

                        ordersQuery = ordersQuery.Where(x => (x.InstantAddedDate >= startDate && x.InstantAddedDate <= endDate) || (x.FixedOrderDate >= startDate && x.FixedOrderDate <= endDate));

                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(employeeUserId))
                ordersQuery = ordersQuery.Where(order => order.ApplicationUserId == employeeUserId);
            if (countryId.HasValue)
                ordersQuery = ordersQuery.Where(order => order.Country == countryId.Value);
            var allOrders = await ordersQuery
                .Select(order => new OrderBySocialMediaCampaignsViewModel
                {
                    State = order.State,
                    Country = order.Country,
                    TotalPrice = order.TotalPrice,
                    OrderSource = order.OrderSource
                })
                .ToListAsync();

            // Calculate total number of orders across all countries for percentage calculation
            var totalOrdersAllCountries = allOrders.Count;


            var groupedByCountry = allOrders
                .GroupBy(order => order.Country)
                .Select(countryGroup =>
                {
                    var totalOrdersInCountry = countryGroup.Count();

                    // Calculate percentage of total orders for this country
                    var percentageOfAllOrders = totalOrdersAllCountries > 0 ?
                        (decimal)totalOrdersInCountry / totalOrdersAllCountries * 100 : 0;

                    var citiesOrderInfo = CitiesByCountry[countryGroup.Key].Select(city =>
                    {
                        var totalOrdersInCity = countryGroup.Count(o => o.State == city);
                        var percentageOfTotalOrders = totalOrdersInCountry > 0 ?
                            (decimal)totalOrdersInCity / totalOrdersInCountry * 100 : 0;

                        return new CityOrderInfo
                        {
                            CityName = city,
                            TotalOrders = totalOrdersInCity,
                            PercentageOfTotalOrders = _decimalFormattingService.DecimalFormat(percentageOfTotalOrders),

                            OrdersBySource = countryGroup.Where(o => o.State == city).GroupBy(o => o.OrderSource)
                                .ToDictionary(srcGroup => srcGroup.Key, srcGroup => srcGroup.Count()),
                            TotalPriceBySourceUSD = countryGroup.Where(o => o.State == city).GroupBy(o => o.OrderSource)
                                .ToDictionary(srcGroup => srcGroup.Key, srcGroup => srcGroup.Sum(order => _currencyExchangeService.ConvertToUSD(order.TotalPrice, order.Country.ToString())))
                        };
                    }).ToList();




                    return new CountryOrderDetailsViewModel
                    {
                        Country = countryGroup.Key,
                        CitiesOrderInfo = citiesOrderInfo,
                        TotalOrders = totalOrdersInCountry,
                        TotalPriceUSD = countryGroup.Sum(order => _currencyExchangeService.ConvertToUSD(order.TotalPrice, order.Country.ToString())),
                        PercentageOfAllOrders = _decimalFormattingService.DecimalFormat(percentageOfAllOrders) // Add this property to the ViewModel if it doesn't exist

                    };
                }).ToList();

            return View(groupedByCountry);
        }



        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public async Task<IActionResult> FailedAndDeliveredOrders(
             Common.Countries? countyId = null,
             bool? genderId = null,
             int? storeId = null,
             int? mainwarehouseId = null,
             Common.Countries? countryId = null,
             OrderSourceEnum? orderSourceId = null,
             DateTime? startDay = null, DateTime? endDay = null,
             string? employeeId = null,
             bool? workShift = null,
             bool? fromComments = null,
             bool? IsFixed = null)
        {
            var filteredOrdersQuery = _context.Orders.Include(a => a.ManufacturingCompany).AsQueryable();

            if (!string.IsNullOrEmpty(employeeId))
            {
                filteredOrdersQuery = filteredOrdersQuery.Where(e => e.ApplicationUserId == employeeId);
            }


            var now = _timeService.GetIstanbulTimeWithOffset(); // Get the current Istanbul time


            if (startDay == null && endDay == null)
            {
                if (now.TimeOfDay < TimeSpan.FromHours(10))
                {
                    startDay = now.Date.AddDays(-1); // Yesterday at 10 AM
                    endDay = now.Date; // Today at 10 AM
                }
                else
                {
                    startDay = now.Date; // Today at 10 AM
                    endDay = now.Date.AddDays(1); // Tomorrow at 10 AM
                }
            }
            else
            {
                // Provided startDay and endDay are used, adjusted to 10 AM
                startDay = startDay?.Date;
                endDay = endDay?.Date;
            }


            filteredOrdersQuery = filteredOrdersQuery
                .Where(x => x.InstantAddedDate >= startDay && x.InstantAddedDate < endDay);

            if (workShift.HasValue)
            {
                // Filter orders based on work shift
                if (workShift == true) // Morning shift
                {
                    filteredOrdersQuery = filteredOrdersQuery.Where(o =>
                        o.InstantAddedDate.HasValue &&
                        o.InstantAddedDate.Value.TimeOfDay >= TimeSpan.FromHours(9) && // 9 AM
                        o.InstantAddedDate.Value.TimeOfDay <= TimeSpan.FromHours(18).Add(TimeSpan.FromMinutes(45))); // 6:30 PM
                }
                else if (workShift == false) // Evening shift (Night)
                {
                    filteredOrdersQuery = filteredOrdersQuery.Where(o =>
                        o.InstantAddedDate.HasValue &&
                        (o.InstantAddedDate.Value.TimeOfDay >= TimeSpan.FromHours(18).Add(TimeSpan.FromMinutes(45)) || // 6:30 PM
                        o.InstantAddedDate.Value.TimeOfDay <= TimeSpan.FromHours(4))); // 4 AM
                }
            }
            if (mainwarehouseId.HasValue)
                filteredOrdersQuery = filteredOrdersQuery.Where(o => o.OrderWarehouses.Any(ow => ow.Warehouse.MainWarehouseId == mainwarehouseId));

            if (genderId.HasValue)
                filteredOrdersQuery = filteredOrdersQuery.Where(o => o.Gender == genderId);

            if (countryId.HasValue)
                filteredOrdersQuery = filteredOrdersQuery.Where(o => o.Country == countryId);

            if (storeId.HasValue)
                filteredOrdersQuery = filteredOrdersQuery.Where(o => o.ManufacturingCompanyId == storeId);

            if (orderSourceId.HasValue)
            {
                if (QueryFilteringService.IsMetaSource(orderSourceId.Value))
                    filteredOrdersQuery = filteredOrdersQuery.Where(x => x.OrderSource == OrderSourceEnum.فيسبوك || x.OrderSource == OrderSourceEnum.انستغرام);
                else
                    filteredOrdersQuery = filteredOrdersQuery.Where(x => x.OrderSource == orderSourceId.Value);
            }

            if (mainwarehouseId.HasValue)
            {
                if (mainwarehouseId == 3)
                {
                    // Only get orders with exactly one warehouse and that warehouse has mainwarehouseId of 3
                    filteredOrdersQuery = filteredOrdersQuery.Where(x => x.OrderWarehouses.Count() == 1 && x.OrderWarehouses.Any(ow => ow.Warehouse.MainWarehouse.Id == mainwarehouseId));
                }
                else
                {
                    // Other logic for different mainwarehouseIds can go here if needed
                    filteredOrdersQuery = filteredOrdersQuery.Where(x => x.OrderWarehouses.All(ow => ow.Warehouse.MainWarehouse.Id == mainwarehouseId));
                }
            }

            if (IsFixed == true)
                filteredOrdersQuery = filteredOrdersQuery.Where(x => x.FixedOrderDate != null);


            if (countyId.HasValue)
                filteredOrdersQuery = filteredOrdersQuery.Where(x => x.Country == countyId.Value);

            if (fromComments.HasValue)
                filteredOrdersQuery = filteredOrdersQuery.Where(x => x.FromComments == fromComments.Value);

            // Apply the date range filter based on the adjusted Istanbul 'day'
            DateTime startDate = new DateTime(2021, 1, 1);
            DateTime endDate = new DateTime(2025, 12, 31);



            // Apply the date range filter based on the adjusted Istanbul 'day'
            filteredOrdersQuery = filteredOrdersQuery.Where(x =>
                // InstantAddedDate within the date range
                (x.InstantAddedDate >= startDate && x.InstantAddedDate <= endDate)

            );

            var orderData = filteredOrdersQuery
                .Include(o => o.OrderWarehouses)
                .ThenInclude(ow => ow.Warehouse)
                .Select(o => new
                {
                    o.Id,
                    o.TotalPrice,
                    o.DeliveryCompanyId,
                    o.Country,
                    o.IsBonus,
                    o.OrderStatus,
                    Storename = o.ManufacturingCompany.Name,
                    StoreId = o.ManufacturingCompany.Id,
                    StoreImage = o.ManufacturingCompany.ImageUrl,
                    o.DeliveryPrice
                })
                .ToList();

            var deliveredOrders = orderData.Where(o =>
                o.OrderStatus == OrderStatusEnum.تم_التسليم ||
                o.OrderStatus == OrderStatusEnum.تم_الدفع ||
                o.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد
            ).ToList();
            var failedOrders = orderData.Where(o =>
                o.OrderStatus == OrderStatusEnum.فشل_التسليم ||
                o.OrderStatus == OrderStatusEnum.انتظار_المعالجة ||
                o.OrderStatus == OrderStatusEnum.الطلبات_المرجعة ||
                o.OrderStatus == OrderStatusEnum.تم_الإلغاء
            ).ToList();

            var totalOrders = orderData.Count;
            var totalDeliveredOrders = deliveredOrders.Count;
            var totalFailedOrders = failedOrders.Count;

            var totalDeliveredPriceUSD = _deliveryCompanyService.CalculateTotalPriceInUSDForOrdersWithOutDeliveryCompanyPrice(deliveredOrders.Select(o => o.Id).ToList());
            var totalFailedPriceUSD = _deliveryCompanyService.CalculateTotalPriceInUSDForOrdersWithOutDeliveryCompanyPrice(failedOrders.Select(o => o.Id).ToList());

            var totalDeliveredPriceTL = _currencyExchangeService.ConvertToTurkishLira(totalDeliveredPriceUSD);
            var totalFailedPriceTL = _currencyExchangeService.ConvertToTurkishLira(totalFailedPriceUSD);

            var deliveredPercentage = Math.Round((totalDeliveredOrders / (decimal)totalOrders) * 100, 2);
            var failedPercentage = Math.Round((totalFailedOrders / (decimal)totalOrders) * 100, 2);

            var totalOrdersByCountry = orderData.GroupBy(o => o.Country)
                .Select(g => new
                {
                    Country = g.Key,
                    IsBonus = g.Count(g => g.IsBonus),
                    TotalOrders = g.Count(),
                    DeliveredOrders = g.Count(o => o.OrderStatus == OrderStatusEnum.تم_التسليم || o.OrderStatus == OrderStatusEnum.تم_الدفع || o.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد),
                    FailedOrders = g.Count(o => o.OrderStatus == OrderStatusEnum.فشل_التسليم || o.OrderStatus == OrderStatusEnum.انتظار_المعالجة || o.OrderStatus == OrderStatusEnum.الطلبات_المرجعة || o.OrderStatus == OrderStatusEnum.تم_الإلغاء)
                })
                .ToList();

            var totalOrdersByStore = orderData.GroupBy(o => o.StoreId)
                .Select(g => new
                {
                    StoreId = g.Key,
                    IsBonus = g.Count(g => g.IsBonus),

                    Storename = g.First().Storename,
                    StoreImage = g.First().StoreImage,
                    TotalOrders = g.Count(),
                    DeliveredOrders = g.Count(o => o.OrderStatus == OrderStatusEnum.تم_التسليم || o.OrderStatus == OrderStatusEnum.تم_الدفع || o.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد),
                    FailedOrders = g.Count(o => o.OrderStatus == OrderStatusEnum.فشل_التسليم || o.OrderStatus == OrderStatusEnum.انتظار_المعالجة || o.OrderStatus == OrderStatusEnum.الطلبات_المرجعة || o.OrderStatus == OrderStatusEnum.تم_الإلغاء)
                })
                .ToList();

            var ordersByCountryDelivered = await Task.WhenAll(totalOrdersByCountry.Select(async country =>
            {
                var deliveredOrdersInCountry = deliveredOrders.Where(o => o.Country == country.Country).ToList();
                var totalPriceWithoutDelivery = _deliveryCompanyService.CalculateTotalPriceInUSDForOrdersWithOutDeliveryCompanyPrice(deliveredOrdersInCountry.Select(o => o.Id).ToList());
                var TotalOrderBonuses = deliveredOrders.Where(o => o.IsBonus).ToList();
                var totalPriceWithoutDeliveryLocal = deliveredOrdersInCountry.Sum(o => o.TotalPrice - (o.DeliveryPrice));


                return new OrderCountByCountry
                {
                    Country = country.Country.ToString(),
                    CountryId = (int)country.Country,
                    OrderCount = country.DeliveredOrders,
                    NumberOfBonuses = TotalOrderBonuses.Count,
                    TotalPriceWithoutDeliveryUSD = _decimalFormattingService.DecimalFormat(totalPriceWithoutDelivery),
                    TotalPriceWithoutDeliverylocal = _decimalFormattingService.DecimalFormat(totalPriceWithoutDeliveryLocal),
                    Rating = Math.Round((country.DeliveredOrders / (decimal)country.TotalOrders) * 100, 2)
                };
            }));

            var ordersByCountryFailed = await Task.WhenAll(totalOrdersByCountry.Select(async country =>
            {
                var failedOrdersInCountry = failedOrders.Where(o => o.Country == country.Country).ToList();
                var totalPriceWithoutDelivery = _deliveryCompanyService.CalculateTotalPriceInUSDForOrdersWithOutDeliveryCompanyPrice(failedOrdersInCountry.Select(o => o.Id).ToList());
                var TotalOrderBonuses = failedOrders.Where(o => o.IsBonus).ToList();
                var totalPriceWithoutDeliveryLocal = failedOrdersInCountry.Sum(o => o.TotalPrice - (o.DeliveryPrice));
                return new OrderCountByCountry
                {
                    Country = country.Country.ToString(),
                    CountryId = (int)country.Country,
                    OrderCount = country.FailedOrders,
                    NumberOfBonuses = TotalOrderBonuses.Count,
                    TotalPriceWithoutDeliveryUSD = _decimalFormattingService.DecimalFormat(totalPriceWithoutDelivery),
                    TotalPriceWithoutDeliverylocal = _decimalFormattingService.DecimalFormat(totalPriceWithoutDeliveryLocal),
                    Rating = Math.Round((country.FailedOrders / (decimal)country.TotalOrders) * 100, 2)
                };
            }));

            var ordersByStoreDelivered = await Task.WhenAll(totalOrdersByStore.Select(async store =>
            {
                var deliveredOrdersInStore = deliveredOrders.Where(o => o.StoreId == store.StoreId).ToList();
                var totalPriceWithoutDelivery = _deliveryCompanyService.CalculateTotalPriceInUSDForOrdersWithOutDeliveryCompanyPrice(deliveredOrdersInStore.Select(o => o.Id).ToList());

                return new OrderCountByStore
                {
                    Name = store.Storename,
                    StoreId = store.StoreId,
                    StoreImage = store.StoreImage,
                    OrderCount = store.DeliveredOrders,
                    TotalPriceWithoutDeliveryUSD = _decimalFormattingService.DecimalFormat(totalPriceWithoutDelivery),
                    TotalPriceWithoutDeliveryTl = _decimalFormattingService.DecimalFormat(_currencyExchangeService.ConvertToTurkishLira(totalPriceWithoutDelivery)),
                    Rating = Math.Round((store.DeliveredOrders / (decimal)store.TotalOrders) * 100, 2)
                };
            }));

            var ordersByStoreFailed = await Task.WhenAll(totalOrdersByStore.Select(async store =>
            {
                var failedOrdersInStore = failedOrders.Where(o => o.StoreId == store.StoreId).ToList();
                var totalPriceWithoutDelivery = _deliveryCompanyService.CalculateTotalPriceInUSDForOrdersWithOutDeliveryCompanyPrice(failedOrdersInStore.Select(o => o.Id).ToList());

                return new OrderCountByStore
                {
                    Name = store.Storename,
                    StoreId = store.StoreId,
                    StoreImage = store.StoreImage,
                    OrderCount = store.FailedOrders,
                    TotalPriceWithoutDeliveryUSD = _decimalFormattingService.DecimalFormat(totalPriceWithoutDelivery),
                    TotalPriceWithoutDeliveryTl = _decimalFormattingService.DecimalFormat(_currencyExchangeService.ConvertToTurkishLira(totalPriceWithoutDelivery)),
                    Rating = Math.Round((store.FailedOrders / (decimal)store.TotalOrders) * 100, 2)
                };
            }));

            // Create the composite view model
            var compositeViewModel = new FailedAndDeliveredOrdersViewModel
            {
                DeliveredOrdersByCountry = ordersByCountryDelivered.ToList(),
                FailedOrdersByCountry = ordersByCountryFailed.ToList(),
                DeliveredOrdersByStore = ordersByStoreDelivered.ToList(),
                FailedOrdersByStore = ordersByStoreFailed.ToList(),
                TotalDeliveredOrdersPriceUSD = _decimalFormattingService.DecimalFormat(totalDeliveredPriceUSD),
                TotalFailedOrdersPriceUSD = _decimalFormattingService.DecimalFormat(totalFailedPriceUSD),
                TotalDeliveredOrdersPriceTL = _decimalFormattingService.DecimalFormat(totalDeliveredPriceTL),
                TotalFailedOrdersPriceTL = _decimalFormattingService.DecimalFormat(totalFailedPriceTL),
                DeliveredPercentage = _decimalFormattingService.DecimalFormat(deliveredPercentage),
                FailedPercentage = _decimalFormattingService.DecimalFormat(failedPercentage),
                TotalDeliveredOrders = totalDeliveredOrders,
                TotalFailedOrders = totalFailedOrders
            };

            // Pass the composite model to the view for rendering.
            return View(compositeViewModel);
        }




        private static IQueryable<Order> ApplyEmployeeOrderStatusGroupFilter(IQueryable<Order> query, string? orderStatusGroup)
        {
            if (string.IsNullOrWhiteSpace(orderStatusGroup))
            {
                return query;
            }

            var deliveredStatuses = new[]
            {
                OrderStatusEnum.تم_التسليم,
                OrderStatusEnum.تم_الدفع,
                OrderStatusEnum.تم_تحديث_الرصيد
            };

            var failedStatuses = new[]
            {
                OrderStatusEnum.فشل_التسليم,
                OrderStatusEnum.فشل_التسليم_2,
                OrderStatusEnum.فشل_التسليم_3,
                OrderStatusEnum.فشل_التسليم_4,
                OrderStatusEnum.فشل_التسليم_5,
                OrderStatusEnum.فشل_التسليم_6,
                OrderStatusEnum.فشل_التسليم_7
            };

            return orderStatusGroup.Trim().ToLowerInvariant() switch
            {
                "delivered" => query.Where(o => deliveredStatuses.Contains(o.OrderStatus)),
                "failed" => query.Where(o => failedStatuses.Contains(o.OrderStatus)),
                "delayed" => query.Where(o => o.OrderStatus == OrderStatusEnum.الطلبات_المؤجلة),
                _ => query
            };
        }

        private IQueryable<Order> BuildEmployeeStatusGroupRatingQuery(
            DateTime? startDay,
            DateTime? endDay,
            string? employeeId,
            Common.Countries? countryId,
            OrderSourceEnum? orderSourceId,
            bool? genderId,
            int? storeId,
            int? mainwarehouseId,
            bool? workShift)
        {
            IQueryable<Order> query = _context.Orders.AsNoTracking();

            // أزرار تم التسليم / فشل التسليم / الطلبات المؤجلة تعتمد على تاريخ آخر تحديث للحالة،
            // وليس تاريخ إنشاء الطلب، لأن الطلب ممكن يتسلم اليوم وهو مضاف من يوم سابق.
            if (startDay.HasValue)
            {
                query = query.Where(o => o.LastEditedDate.HasValue && o.LastEditedDate.Value >= startDay.Value);
            }

            if (endDay.HasValue)
            {
                query = query.Where(o => o.LastEditedDate.HasValue && o.LastEditedDate.Value < endDay.Value);
            }

            if (!string.IsNullOrWhiteSpace(employeeId))
            {
                query = query.Where(o => o.ApplicationUserId == employeeId);
            }

            if (countryId.HasValue)
            {
                query = query.Where(o => o.Country == countryId.Value);
            }

            if (storeId.HasValue)
            {
                query = query.Where(o => o.ManufacturingCompanyId == storeId.Value);
            }

            if (mainwarehouseId.HasValue)
            {
                query = query.Where(o =>
                    o.OrderWarehouses.Any(ow =>
                        ow.Warehouse != null &&
                        ow.Warehouse.MainWarehouseId == mainwarehouseId.Value));
            }

            if (genderId.HasValue)
            {
                query = query.Where(o => o.Gender == genderId.Value);
            }

            if (orderSourceId.HasValue)
            {
                if (QueryFilteringService.IsMetaSource(orderSourceId.Value))
                {
                    query = query.Where(o => o.OrderSource == OrderSourceEnum.فيسبوك || o.OrderSource == OrderSourceEnum.انستغرام);
                }
                else
                {
                    query = query.Where(o => o.OrderSource == orderSourceId.Value);
                }
            }

            if (workShift.HasValue)
            {
                if (workShift.Value)
                {
                    query = query.Where(o =>
                        o.LastEditedDate.HasValue &&
                        o.LastEditedDate.Value.TimeOfDay >= TimeSpan.FromHours(9) &&
                        o.LastEditedDate.Value.TimeOfDay <= TimeSpan.FromHours(18).Add(TimeSpan.FromMinutes(45)));
                }
                else
                {
                    query = query.Where(o =>
                        o.LastEditedDate.HasValue &&
                        (o.LastEditedDate.Value.TimeOfDay >= TimeSpan.FromHours(18).Add(TimeSpan.FromMinutes(45)) ||
                         o.LastEditedDate.Value.TimeOfDay <= TimeSpan.FromHours(4)));
                }
            }

            return query;
        }

        [Authorize(Roles = "Admin,ExecutiveDirector")]
        [HttpGet]
        public async Task<IActionResult> EmployeeOrderStatusGroupSummary(
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? employeeId = null,
            Common.Countries? countryId = null,
            int? orderSourceId = null,
            string? genderId = null,
            int? storeId = null,
            int? mainwarehouseId = null,
            bool? workShift = null)
        {
            var ratingRange = GetRatingDateRange(startDate, endDate);

            OrderSourceEnum? parsedOrderSource = null;
            if (orderSourceId.HasValue && Enum.IsDefined(typeof(OrderSourceEnum), orderSourceId.Value))
            {
                parsedOrderSource = (OrderSourceEnum)orderSourceId.Value;
            }

            bool? parsedGender = null;
            if (!string.IsNullOrWhiteSpace(genderId))
            {
                if (bool.TryParse(genderId, out var genderBool))
                {
                    parsedGender = genderBool;
                }
                else if (int.TryParse(genderId, out var genderInt))
                {
                    if (genderInt == 1)
                    {
                        parsedGender = true;
                    }
                    else if (genderInt == 2)
                    {
                        parsedGender = false;
                    }
                }
            }

            var baseQuery = BuildEmployeeStatusGroupRatingQuery(
                ratingRange.StartDay,
                ratingRange.EndDay,
                employeeId,
                countryId,
                parsedOrderSource,
                parsedGender,
                storeId,
                mainwarehouseId,
                workShift);

            var deliveredStatuses = new[]
            {
                OrderStatusEnum.تم_التسليم,
                OrderStatusEnum.تم_الدفع,
                OrderStatusEnum.تم_تحديث_الرصيد
            };

            var failedStatuses = new[]
            {
                OrderStatusEnum.فشل_التسليم,
                OrderStatusEnum.فشل_التسليم_2,
                OrderStatusEnum.فشل_التسليم_3,
                OrderStatusEnum.فشل_التسليم_4,
                OrderStatusEnum.فشل_التسليم_5,
                OrderStatusEnum.فشل_التسليم_6,
                OrderStatusEnum.فشل_التسليم_7
            };

            var items = await (
                from order in baseQuery
                join employee in _context.Employees.AsNoTracking()
                    on order.ApplicationUserId equals employee.ApplicationUserId
                where employee.IsActive == true
                group order by new
                {
                    employee.Id,
                    employee.ApplicationUserId,
                    EmployeeName = employee.DisplayName == null || employee.DisplayName == "" ? employee.Name : employee.DisplayName
                }
                into grouped
                select new
                {
                    employeeId = grouped.Key.Id,
                    userId = grouped.Key.ApplicationUserId,
                    employeeName = grouped.Key.EmployeeName,
                    deliveredCount = grouped.Count(o => deliveredStatuses.Contains(o.OrderStatus)),
                    failedCount = grouped.Count(o => failedStatuses.Contains(o.OrderStatus)),
                    delayedCount = grouped.Count(o => o.OrderStatus == OrderStatusEnum.الطلبات_المؤجلة)
                })
                .ToListAsync();

            return Json(new
            {
                success = true,
                items
            });
        }


        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public async Task<IActionResult> EmployeOrdersDetails(
             int page = 1,
             int? pageSize = null,

              // filters
              DateTime? startDate = null,
              DateTime? endDate = null,
              string? employeeId = null,
              int? mainwarehouseId = null,
              int? warehouseId = null,

              int? storeId = null,
              int? deliveryCompanyId = null,
              bool? workshift = null,

               // shared 
               bool? genderId = null,

                 // conditions 
                 bool? isFixed = null,
                 bool? isOffer = null,
                 bool? isDiscount = null,
                 bool? isEmployeebonus = null,
                 bool? isComments = null,
                 bool? isMale = null,
                 bool? isFemale = null,
                 bool? IsFixed = null,
                 // country
                 bool? isDeliverd = null,
                 OrderSourceEnum? ordersourceId = null,

                 Common.Countries? countryId = null,
                List<OrderStatusEnum>? orderStatusesFilter = null, // Changed to support multiple order statuses
                string? orderStatusGroup = null,
                    bool ignoreDateFilter = false // New boolean to ignore date filters

         )

        {
            // sum total prices based on country filter 
            Dictionary<int, decimal> totalDeliveryCompanyPrice;
            decimal totalOrderPrice = 0;
            string? SelectedCoutnryfromfilter = null;
            decimal totalOrderPriceDollar = 0;
            decimal totalOrderPriceTRY = 0;
            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // query
            IQueryable<Order> query = _context.Orders.AsNoTracking();

            var now = _timeService.GetIstanbulTimeWithOffset(); // Get the current Istanbul time

            // Date filters
            if (!ignoreDateFilter)
            {
                if (startDate == null && endDate == null)
                {
                    if (now.TimeOfDay < TimeSpan.FromHours(10))
                    {
                        // Before 10 AM: filter from 10 AM yesterday to 10 AM today
                        startDate = now.Date.AddDays(-1).AddHours(10).AddMinutes(30); // Yesterday at 10 AM
                        endDate = now.Date.AddHours(10).AddMinutes(30);               // Today at 10 AM
                    }
                    else
                    {
                        // After 10 AM: filter from 10 AM today to 10 AM tomorrow
                        startDate = now.Date.AddHours(10).AddMinutes(30);             // Today at 10 AM
                        endDate = now.Date.AddDays(1).AddHours(10).AddMinutes(30);    // Tomorrow at 10 AM
                    }
                }
                else
                {
                    // If dates are provided, adjust them to 10 AM of their respective days
                    startDate = startDate?.Date.AddHours(10).AddMinutes(30);
                    endDate = endDate?.Date.AddHours(10).AddMinutes(30);
                }



                // في حالة أزرار حالات الموظف نعتمد على تاريخ آخر تحديث للحالة.
                if (!string.IsNullOrWhiteSpace(orderStatusGroup))
                {
                    query = query.Where(x => x.LastEditedDate.HasValue && x.LastEditedDate.Value >= startDate.Value && x.LastEditedDate.Value < endDate.Value);
                }
                else
                {
                    query = query.Where(x => x.InstantAddedDate >= startDate && x.InstantAddedDate <= endDate);
                }
            }



            Console.WriteLine($"Start Day: {startDate}, End Day: {endDate}");

            // Date filters
            if (!ignoreDateFilter)
            {

                // Apply the date range filter based on the adjusted Istanbul 'day'
                if (isFixed != true && warehouseId == null && string.IsNullOrWhiteSpace(orderStatusGroup))
                {
                    query = query.Where(x =>
                    // InstantAddedDate within the date range
                    (x.InstantAddedDate >= startDate && x.InstantAddedDate <= endDate)

                );
                }
            }

            if (IsFixed == true)
                query = query.Where(x => x.FixedOrderDate != null);

            if (isDeliverd.HasValue)
            {
                if (isDeliverd == true)
                {
                    query = query.Where(o =>
                    o.OrderStatus == OrderStatusEnum.تم_التسليم ||
                    o.OrderStatus == OrderStatusEnum.تم_الدفع ||
                    o.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد

                );
                }
                if (isDeliverd != true)
                {
                    query = query.Where(o =>
                    o.OrderStatus == OrderStatusEnum.فشل_التسليم ||
                    o.OrderStatus == OrderStatusEnum.انتظار_المعالجة ||
                    o.OrderStatus == OrderStatusEnum.الطلبات_المرجعة ||
                    o.OrderStatus == OrderStatusEnum.تم_الإلغاء

                );
                }
            }

            if (!string.IsNullOrEmpty(employeeId) && isFixed != true)
                query = query.Where(x => x.ApplicationUserId == employeeId);

            if (countryId.HasValue)
                query = query.Where(x => x.Country == countryId);

            if (warehouseId.HasValue)
            {
                query = query.Where(x => x.OrderWarehouses.Any(ow => ow.WarehouseId == warehouseId.Value));
            }

            if (deliveryCompanyId.HasValue)
            {
                query = query.Where(x => x.DeliveryCompanyId == deliveryCompanyId);
            }

            // Apply order status filtering for multiple statuses
            if (orderStatusesFilter != null && orderStatusesFilter.Any())
            {
                query = query.Where(x => orderStatusesFilter.Contains(x.OrderStatus));
            }

            if (mainwarehouseId.HasValue)
            {
                if (mainwarehouseId == 3)
                {
                    // Only get orders with exactly one warehouse and that warehouse has mainwarehouseId of 3
                    query = query.Where(x => x.OrderWarehouses.Count() == 1 && x.OrderWarehouses.Any(ow => ow.Warehouse.MainWarehouse.Id == mainwarehouseId));
                }
                else
                {
                    // Other logic for different mainwarehouseIds can go here if needed
                    query = query.Where(x => x.OrderWarehouses.All(ow => ow.Warehouse.MainWarehouse.Id == mainwarehouseId));
                }
            }

            if (workshift.HasValue)
            {
                // في حالة فلاتر الحالات نعتمد على وقت آخر تحديث للحالة، وإلا نترك سلوك الصفحة كما هو.
                if (!string.IsNullOrWhiteSpace(orderStatusGroup))
                {
                    if (workshift == true)
                    {
                        query = query.Where(o =>
                            o.LastEditedDate.HasValue &&
                            o.LastEditedDate.Value.TimeOfDay >= TimeSpan.FromHours(9) &&
                            o.LastEditedDate.Value.TimeOfDay <= TimeSpan.FromHours(18).Add(TimeSpan.FromMinutes(45)));
                    }
                    else if (workshift == false)
                    {
                        query = query.Where(o =>
                            o.LastEditedDate.HasValue &&
                            (o.LastEditedDate.Value.TimeOfDay >= TimeSpan.FromHours(18).Add(TimeSpan.FromMinutes(45)) ||
                            o.LastEditedDate.Value.TimeOfDay <= TimeSpan.FromHours(4)));
                    }
                }
                else
                {
                    // Filter orders based on work shift
                    if (workshift == true) // Morning shift
                    {
                        query = query.Where(o =>
                            o.InstantAddedDate.HasValue &&
                            o.InstantAddedDate.Value.TimeOfDay >= TimeSpan.FromHours(9) && // 9 AM
                            o.InstantAddedDate.Value.TimeOfDay <= TimeSpan.FromHours(18).Add(TimeSpan.FromMinutes(45))); // 6:30 PM
                    }
                    else if (workshift == false) // Evening shift (Night)
                    {
                        query = query.Where(o =>
                            o.InstantAddedDate.HasValue &&
                            (o.InstantAddedDate.Value.TimeOfDay >= TimeSpan.FromHours(18).Add(TimeSpan.FromMinutes(45)) || // 6:30 PM
                            o.InstantAddedDate.Value.TimeOfDay <= TimeSpan.FromHours(4))); // 4 AM
                    }
                }
            }

            if (isFixed == true)
            {
                if (!string.IsNullOrEmpty(employeeId))
                {
                    query = query.Where(x =>
                        // InstantAddedDate within the date range and Fixedby matches employeeId
                        (x.FixedOrderDate >= startDate && x.FixedOrderDate <= endDate && x.Fixedby == employeeId)
                    );
                }
                else
                {
                    query = query.Where(x =>
                        // InstantAddedDate within the date range
                        (x.FixedOrderDate >= startDate && x.FixedOrderDate <= endDate)
                    );
                }
            }

            if (isOffer == true)
            {
                query = query.Where(o =>
                    o.OrderWarehouses.Any(ow => ow.Amount > 1) ||
                    o.OrderWarehouses.GroupBy(ow => ow.WarehouseId).Count() > 1
                );
            }

            if (isDiscount == true)
            {
                query = query.Where(o => o.IsDiscount);
            }

            if (isEmployeebonus.HasValue)
            {

                query = query.Where(o => o.IsBonus);
            }

            if (isMale.HasValue)
            {
                query = query.Where(o => o.Gender == true);
            }

            if (isFemale.HasValue)
            {
                query = query.Where(o => o.Gender == false);
            }

            if (isComments.HasValue)
            {
                query = query.Where(x => x.FromComments == isComments.Value);
            }

            if (genderId.HasValue)
            {
                query = query.Where(x => x.Gender == genderId.Value);
            }

            if (ordersourceId.HasValue)
            {
                if (QueryFilteringService.IsMetaSource(ordersourceId.Value))
                    query = query.Where(x => x.OrderSource == OrderSourceEnum.فيسبوك || x.OrderSource == OrderSourceEnum.انستغرام);
                else
                    query = query.Where(x => x.OrderSource == ordersourceId.Value);
            }

            if (storeId.HasValue)

                query = query.Where(x => x.ManufacturingCompanyId == storeId.Value);

            query = ApplyEmployeeOrderStatusGroupFilter(query, orderStatusGroup);






            var orders = query
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
                     SourceName = o.SourceName,
                     Gender = o.Gender,
                     HasWarehouseWithMoreThanOneItem = o.OrderWarehouses.Any(ow => ow.Amount > 1),
                     HasMoreThanOneWarehouse = o.OrderWarehouses.GroupBy(ow => ow.WarehouseId).Count() > 1,
                     TotalProductsCount = o.OrderWarehouses.Sum(ow => ow.Amount),

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
                     SelectedWarehouses = o.OrderWarehouses.Select(ow => new WarehouseAmountViewModel
                     {
                         WarehouseName = ow.Warehouse.Name,
                         Image = ow.Warehouse.MainWarehouse.ImageUrl,
                         Amount = ow.Warehouse.Amount
                     }).ToList(),
                     TotalAmountOfOrderWarehouses = o.OrderWarehouses.Sum(ow => ow.Amount)

                 })
                  .ToList();

            var totalItems = await query.CountAsync(); // Asynchronous operation to count total items


            var AllOrders = query.ToList();

            var allOrderIds = AllOrders.Select(order => order.Id).ToList();


            // Extract order IDs from the filtered orders

            // Calculate the sums for all orders

            decimal totalorderpriceholder = await _orderService.CalculateTotalPriceInUSDForOrdersAsync(allOrderIds);
            decimal totalorderdeliveryCompanyPricepriceholder = await _deliveryCompanyService.CalculateTotalDeliveryPricesInUSDForOrdersByCountryAsync(allOrderIds);
            totalOrderPriceDollar = totalorderpriceholder - totalorderdeliveryCompanyPricepriceholder;
            totalOrderPriceTRY = _currencyExchangeService.ConvertToTurkishLira(totalOrderPriceDollar);


            // Fetch total prices in USD and TL for all orders
            var totalOrdersPriceInUSD = _deliveryCompanyService.CalculateTotalPriceInUSDForOrdersWithOutDeliveryCompanyPrice(allOrderIds);
            var totalOrdersPriceInTL = _currencyExchangeService.ConvertToTurkishLira(totalOrdersPriceInUSD);

            int totalNumberOfOrders = orders.Count();


            var orderFromComments = orders.Count(o => o.FromComments);
            var orderFromMales = orders.Count(o => o.Gender);
            var orderFromFemales = orders.Count(o => !o.Gender);
            var orderFromOffers = orders.Count(o =>
                o.HasWarehouseWithMoreThanOneItem || o.HasMoreThanOneWarehouse);
            var orderTotalProductCount = orders.Sum(o => o.TotalProductsCount);
            var fixedOrdersCount = orders.Count;
            int offersOrdersCount = orders.Count(o => o.IsDiscount);


            var totalOrderBonuses = orders.Count(o => o.IsBonus);


            // Create a PaginationViewModel instance and populate it with data
            var paginationViewModel = new PaginationViewModel<OrderViewModel>
            {
                Items = orders.OrderByDescending(o => o.LastEditedDate)
                .Skip((page - 1) * (pageSize ?? 10))
                .Take(pageSize ?? 10)
                .ToList(),

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



            // Create an instance of the EmployeeRatingCompositeViewModel and populate it with data
            var compositeViewModel = new EmployeeRatingCompositeViewModel
            {
                TotalOrdersPriceInTL = _decimalFormattingService.DecimalFormat(totalOrdersPriceInTL),
                TotalOrdersPriceInDollar = _decimalFormattingService.DecimalFormat(totalOrdersPriceInUSD),
                TotalNumberOfOrders = _decimalFormattingService.DecimalFormat(totalNumberOfOrders),
                FixedOrdersCount = _decimalFormattingService.DecimalFormat(fixedOrdersCount),
                OrderFromOffersCount = _decimalFormattingService.DecimalFormat(orderFromOffers),
                OrderFromOfferDiscountsCount = _decimalFormattingService.DecimalFormat(offersOrdersCount),
                NumberOfBouneses = totalOrderBonuses,
                TotalProductsCount = _decimalFormattingService.DecimalFormat(orderTotalProductCount),
                OrderFromCommentsCount = _decimalFormattingService.DecimalFormat(orderFromComments),
                OrderFromFemalesCount = _decimalFormattingService.DecimalFormat(orderFromFemales),
                OrderFromMalesCount = _decimalFormattingService.DecimalFormat(orderFromMales),
            };

            // Create an instance of the HomeViewModel and populate it
            var viewModel = new HomeViewModel
            {
                PaginationViewModel = paginationViewModel,
                EmployeeRatingCompositeViewModel = compositeViewModel,
                OrderStatuses = orderStatuses,
                OrderStatusesForDeliveryCompanyAndRepresentative = orderStatusesForDeliveryCompanyAndRepresentative,
                Countries = countries,
                TotalOrderPrice = _decimalFormattingService.DecimalFormat(totalOrderPrice),
                TotalOrderPriceDollar = _decimalFormattingService.DecimalFormat(totalOrderPriceDollar),
                TotalOrderPriceTRY = _decimalFormattingService.DecimalFormat(totalOrderPriceTRY),
                SelectedCoutnry = SelectedCoutnryfromfilter,
                OrderStatusIconUrls = orderStatusIconUrls,
                CountryImageUrls = countryImageUrls,
                SocialMediaIconUrls = socialMediaIconUrls,
                CurrencySymbols = currencySymbols
            };


            var employee = _userManager.GetUserAsync(User).Result.Name;
            viewModel.UserName = employee;
            return View(viewModel); // Pass the viewModel to the view
        }


    }
}
