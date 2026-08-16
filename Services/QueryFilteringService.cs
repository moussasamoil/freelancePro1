using lotus_blue.Data;
using lotus_blue.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;

namespace lotus_blue.Services
{
    public class QueryFilteringService
    {
        public static bool IsMetaSource(OrderSourceEnum selected) => selected == OrderSourceEnum.ميتا;

        private readonly ApplicationDbContext _context;
        private readonly GetCurrentTimeInIstanbul _timeService;
        private readonly OrderService _orderService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public QueryFilteringService(ApplicationDbContext context, GetCurrentTimeInIstanbul timeService, OrderService orderService, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _timeService = timeService;
            _orderService = orderService;
            _httpContextAccessor = httpContextAccessor;
        }

        public IQueryable<T> ApplyFilters<T>(
            IQueryable<T> query,
            Common.Countries? countryId = null,
            OrderStatusEnum? orderStatusId = null,
            OrderSourceEnum? orderSourceId = null,
            int? storeId = null,
            int? deliveryCompanyId = null,
            int? deliveryRepresentativeId = null,
            int? productId = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? cityId = null,
            string? search = null,
            string? employeeId = null,
            bool? fromComments = null,
            bool? gender = null,
            bool? isOffers = null,
            bool? isDiscount = null,
            bool? isBonus = null,
            bool? isSpecialClients = null,
            bool? isFixedAndDelivered = null,
            bool? isHidden = null,
            bool? isComplaints = null,
            bool? isPaid = null,
            int? mainWarehouseId = null ,
                string sessionPrefix = "", // Prefix for session keys
            string? failureReason = null,
            bool includeSourceNameSearch = false

        ) where T : class
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var now = DateTime.Now; // Define 'now' at the start of the method

            if (typeof(T) == typeof(Order))
            {
                var orderQuery = query as IQueryable<Order>;

                if (countryId.HasValue)
                {
                    orderQuery = orderQuery.Where(x => x.Country == countryId.Value);
                    httpContext.Session.SetString($"{sessionPrefix}CountryId", countryId.ToString());
                }

                if (!string.IsNullOrEmpty(cityId))
                {
                    orderQuery = orderQuery.Where(x => x.State == cityId);
                    httpContext.Session.SetString($"{sessionPrefix}CityId", cityId);
                }

                if (storeId.HasValue)
                {
                    orderQuery = orderQuery.Where(x => x.ManufacturingCompanyId == storeId.Value);
                    httpContext.Session.SetInt32($"{sessionPrefix}StoreId", storeId.Value);
                }

                if (deliveryCompanyId.HasValue)
                {
                    orderQuery = orderQuery.Where(x => x.DeliveryCompanyId == deliveryCompanyId.Value);
                    httpContext.Session.SetInt32($"{sessionPrefix}DeliveryCompanyId", deliveryCompanyId.Value);
                }

                if (startDate.HasValue && endDate.HasValue)
                {

                    // Set default date range (10 AM cutoff logic)
                    if (startDate == null && endDate == null)
                    {
                        if (now.TimeOfDay < TimeSpan.FromHours(10))
                        {
                            // Before 10 AM: filter from 10 AM yesterday to 10 AM today
                            startDate = now.Date.AddDays(-1).AddHours(10); // Yesterday at 10 AM
                            endDate = now.Date.AddHours(10);               // Today at 10 AM
                        }
                        else
                        {
                            // After 10 AM: filter from 10 AM today to 10 AM tomorrow
                            startDate = now.Date.AddHours(10);             // Today at 10 AM
                            endDate = now.Date.AddDays(1).AddHours(10);    // Tomorrow at 10 AM
                        }
                    }
                    else
                    {
                        // If dates are provided, adjust them to 10 AM of their respective days
                        startDate = startDate?.Date.AddHours(10);
                        endDate = endDate?.Date.AddHours(10);
                    }

                    // Apply the date filter to the query
                    if (startDate.HasValue && endDate.HasValue)
                    {
                        orderQuery = orderQuery.Where(x =>
                            x.InstantAddedDate >= startDate.Value &&
                            x.InstantAddedDate < endDate.Value
                        );
                    }

                }


                if (deliveryRepresentativeId.HasValue)
                {
                    orderQuery = orderQuery.Where(x => x.DeliveryCompanyId == deliveryRepresentativeId.Value);
                    httpContext.Session.SetInt32($"{sessionPrefix}DeliveryRepresentativeId", deliveryRepresentativeId.Value);
                }

                if (orderStatusId.HasValue)
                {
                    orderQuery = orderQuery.Where(x => x.OrderStatus == orderStatusId.Value);
                    httpContext.Session.SetString($"{sessionPrefix}OrderStatusId", orderStatusId.ToString());
                }

                if (!string.IsNullOrEmpty(search))
                {
                    search = search.ToLower();
                    if (search.StartsWith("*") && int.TryParse(search.Trim('*'), out int searchId))
                    {
                        orderQuery = orderQuery.Where(o => o.Id == searchId);
                    }
                    else if (search.StartsWith("-") && int.TryParse(search.Trim('-'), out int searchIdsbs))
                    {
                        orderQuery = orderQuery.Where(o => o.ExternalOrderId == searchIdsbs);
                    }
                    else
                    {
                        var phoneTerm = Controllers.OrderController.NormalizePhone(search);
                        var nameTerm = search;
                        orderQuery = includeSourceNameSearch
                            ? orderQuery.Where(o => o.TelephoneNumber.Contains(phoneTerm) || (o.SourceName != null && o.SourceName.ToLower().Contains(nameTerm)))
                            : orderQuery.Where(o => o.TelephoneNumber.Contains(phoneTerm));
                    }
                }

                if (!string.IsNullOrEmpty(employeeId))
                {
                    orderQuery = orderQuery.Where(x => x.ApplicationUserId == employeeId);
                    httpContext.Session.SetString($"{sessionPrefix}EmployeeId", employeeId);
                }

                if (productId.HasValue)
                {
                    orderQuery = orderQuery.Where(o => o.OrderWarehouses.Any(ow => ow.Warehouse.MainWarehouseId == productId));
                    httpContext.Session.SetInt32($"{sessionPrefix}ProductId", productId.Value);
                }

                if (orderSourceId.HasValue)
                {
                    if (IsMetaSource(orderSourceId.Value))
                        orderQuery = orderQuery.Where(x => x.OrderSource == OrderSourceEnum.فيسبوك || x.OrderSource == OrderSourceEnum.انستغرام);
                    else
                        orderQuery = orderQuery.Where(x => x.OrderSource == orderSourceId.Value);
                    httpContext.Session.SetString($"{sessionPrefix}OrderSourceId", orderSourceId.ToString());
                }

                if (fromComments.HasValue)
                {
                    orderQuery = orderQuery.Where(x => x.FromComments == fromComments.Value);
                    httpContext.Session.SetString($"{sessionPrefix}FromComments", fromComments.ToString());
                }

                if (gender.HasValue)
                {
                    orderQuery = orderQuery.Where(x => x.Gender == gender.Value);
                    httpContext.Session.SetString($"{sessionPrefix}Gender", gender.ToString());
                }

                if (isOffers.HasValue)
                {
                    orderQuery = orderQuery.Where(o => o.OrderWarehouses.Any(ow => ow.Amount > 1) &&
                                                        o.OrderWarehouses.GroupBy(ow => ow.WarehouseId).Count() > 1);
                    httpContext.Session.SetString($"{sessionPrefix}IsOffers", isOffers.ToString());
                }

                if (isSpecialClients.HasValue)
                {
                    orderQuery = orderQuery.Where(x => x.IsClientSpecial == isSpecialClients.Value);
                    httpContext.Session.SetString($"{sessionPrefix}IsSpecialClients", isSpecialClients.ToString());
                }

                if (isComplaints.HasValue)
                {
                    orderQuery = orderQuery.Where(x => x.IsComplaints == isComplaints.Value);
                    httpContext.Session.SetString($"{sessionPrefix}IsComplaints", isComplaints.ToString());
                }

                if (isHidden.HasValue)
                {
                    orderQuery = orderQuery.Where(x => x.IsHidden == isHidden.Value);
                    httpContext.Session.SetString($"{sessionPrefix}IsHidden", isHidden.ToString());
                }

                if (isPaid.HasValue)
                {
                    orderQuery = orderQuery.Where(x => x.IsPaid == isPaid.Value);
                    httpContext.Session.SetString($"{sessionPrefix}IsPaid", isPaid.ToString());
                }

                if (isFixedAndDelivered.HasValue && isFixedAndDelivered.Value)
                {
                    orderQuery = from o in orderQuery
                                 join os in _context.OrderStatusHistories on o.Id equals os.OrderId
                                 where os.Status == OrderStatusEnum.تم_المعالجة &&
                                       (o.OrderStatus == OrderStatusEnum.تم_التسليم ||
                                        o.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد ||
                                        o.OrderStatus == OrderStatusEnum.تم_الدفع)
                                 select o;
                    httpContext.Session.SetString($"{sessionPrefix}IsFixedAndDelivered", isFixedAndDelivered.ToString());
                }

                if (isDiscount.HasValue)
                {
                    orderQuery = orderQuery.Where(x => x.IsDiscount == isDiscount.Value);
                    httpContext.Session.SetString($"{sessionPrefix}IsDiscount", isDiscount.ToString());
                }

                if (isBonus.HasValue)
                {
                    orderQuery = orderQuery.Where(o => o.IsBonus && !o.IsBonusPaidForEmployee &&
                        (o.OrderStatus == OrderStatusEnum.تم_التسليم ||
                         o.OrderStatus == OrderStatusEnum.تم_الدفع ||
                         o.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد));
                    httpContext.Session.SetString($"{sessionPrefix}IsBonus", isBonus.ToString());
                }

                if (!string.IsNullOrEmpty(failureReason))
                {
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
                        OrderStatusEnum.أرشيف_المرجع
                    };
                    var matchingOrderIds = _context.OrderStatusHistories
                        .Where(osh => osh.Reason == failureReason && failureStatuses.Contains(osh.Status.Value))
                        .Select(osh => osh.OrderId)
                        .Distinct()
                        .ToList();
                    orderQuery = orderQuery.Where(o => matchingOrderIds.Contains(o.Id));
                    httpContext.Session.SetString($"{sessionPrefix}FailureReason", failureReason);
                }
                else
                {
                    // Unlike other filters (country, status, store, etc.) which are bound to nullable enums/ints
                    // and fall back to session only when null, failureReason is a string that arrives as ""
                    // (empty string) when cleared — not null — so the session fallback in HomeController
                    // never triggers. However, the session still holds the old value from the previous
                    // selection, and since this filter materializes order IDs via .ToList(), those stale IDs
                    // get baked into the next query as WHERE Id IN (...), returning wrong results.
                    // We must explicitly remove the session entry when the filter is cleared.
                    httpContext.Session.Remove($"{sessionPrefix}FailureReason");
                }

                return orderQuery as IQueryable<T>;
            }

            if (typeof(T) == typeof(Warehouse))
            {
                var warehouseQuery = query as IQueryable<Warehouse>;

                if (countryId.HasValue)
                {
                    warehouseQuery = warehouseQuery.Where(x => x.Countries == countryId.Value);
                }

                if (!string.IsNullOrEmpty(cityId))
                {
                    warehouseQuery = warehouseQuery.Where(x => x.City == cityId);
                }

                if (storeId.HasValue)
                {
                    warehouseQuery = warehouseQuery.Where(x => x.ManufacturingCompanyId == storeId.Value);
                }

                if (deliveryCompanyId.HasValue)
                {
                    warehouseQuery = warehouseQuery.Where(x => x.DeliveryCompanyId == deliveryCompanyId.Value);
                }

                if (deliveryRepresentativeId.HasValue)
                {
                    warehouseQuery = warehouseQuery.Where(x => x.DeliveryCompanyId == deliveryRepresentativeId.Value);
                }

                if (mainWarehouseId.HasValue)
                {
                    warehouseQuery = warehouseQuery.Where(x => x.MainWarehouseId == mainWarehouseId.Value);
                }

                return warehouseQuery as IQueryable<T>;
            }

            if (typeof(T) == typeof(MainProduct))
            {
                var productQuery = query as IQueryable<MainProduct>;

                if (countryId.HasValue)
                {
                    productQuery = productQuery.Where(x => x.Country == countryId.Value);
                    httpContext.Session.SetString($"{sessionPrefix}CountryId", countryId.ToString());
                }

                if (storeId.HasValue)
                {
                    productQuery = productQuery.Where(x => x.ManufacturingCompanyId == storeId.Value);
                    httpContext.Session.SetInt32($"{sessionPrefix}StoreId", storeId.Value);
                }

                return productQuery as IQueryable<T>;
            }

            if (typeof(T) == typeof(DeliveryCompanyPrice))
            {
                var priceQuery = query as IQueryable<DeliveryCompanyPrice>;

                if (countryId.HasValue)
                {
                    priceQuery = priceQuery.Where(p => p.Country == countryId.Value);
                    httpContext.Session.SetString($"{sessionPrefix}CountryId", countryId.ToString());
                }

                if (!string.IsNullOrEmpty(cityId))
                {
                    priceQuery = priceQuery.Where(p => p.City == cityId);
                    httpContext.Session.SetString($"{sessionPrefix}CityId", cityId);
                }

                if (deliveryRepresentativeId.HasValue)
                {
                    priceQuery = priceQuery.Where(p => p.DeliveryCompanyId == deliveryRepresentativeId.Value);
                    httpContext.Session.SetInt32($"{sessionPrefix}DeliveryRepresentativeId", deliveryRepresentativeId.Value);
                }

                if (deliveryCompanyId.HasValue)
                {
                    priceQuery = priceQuery.Where(p => p.DeliveryCompanyId == deliveryCompanyId.Value);
                    httpContext.Session.SetInt32($"{sessionPrefix}DeliveryCompanyId", deliveryCompanyId.Value);
                }

                return priceQuery as IQueryable<T>;
            }

            throw new NotSupportedException($"Filtering is not supported for type {typeof(T).Name}");
        }
    }
}
