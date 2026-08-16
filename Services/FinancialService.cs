using lotus_blue.Data;
using lotus_blue.Models.ViewModel;
using Microsoft.EntityFrameworkCore;
using lotus_blue.Models;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using static NuGet.Packaging.PackagingConstants;
using System.Linq;
using lotus_blue.API;
using Newtonsoft.Json;
using Microsoft.IdentityModel.Tokens;
using lotus_blue.Models.AppViewModel;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.Metrics;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace lotus_blue.Services
{


    public class FinancialService
    {
        private readonly ApplicationDbContext _context; // Your DbContext
        private readonly DeliveryCompanyService _Deliverycompanyservice;

        private readonly CurrencyExchangeService _currencyExchangeService;
        private readonly RESTAPI _restApi;
        private readonly DecimalFormattingService _decimalFormattingService;
        private readonly DynamicCommon _dynamicCommon;
        public FinancialService(ApplicationDbContext context, DeliveryCompanyService deliverycompanyservice, CurrencyExchangeService currencyExchangeService, RESTAPI restApi, DecimalFormattingService decimalFormattingService, DynamicCommon dynamicCommon)
        {
            _context = context;
            _Deliverycompanyservice = deliverycompanyservice;
    
            _currencyExchangeService = currencyExchangeService;
            _restApi = restApi;
            _decimalFormattingService = decimalFormattingService;
            _dynamicCommon = dynamicCommon;
        }
        // حسابات شركات التوصيل
        public async Task<FinancialManufacturingCompanyViewModel> GetFinancialManufacturingCompanyDataOnGoingDeliveryCompany(string userId, bool isAdmin, Common.Countries? countryId, int? deliveryCompanyId, int? storeId)
        {
            // Get orders query 
            IQueryable<Order> ordersQuery = _context.Orders
                .Where(o => o.OrderStatus == OrderStatusEnum.تم_التجهيز ||
                            o.OrderStatus == OrderStatusEnum.قيد_التوصيل ||
                            o.OrderStatus == OrderStatusEnum.طلب_جديد ||
                            o.OrderStatus == OrderStatusEnum.الطلبات_المؤجلة ||
                            o.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد)
                .Include(o => o.DeliveryCompany)
                .Include(o => o.ManufacturingCompany)
                .Where(o => o.DeliveryCompany.IsShown)
                .Where(o => !o.DeliveryCompany.IsRepresentative)
                .AsQueryable();

            // Filter by country 
            if (countryId.HasValue)
            {
                ordersQuery = ordersQuery.Where(o => o.Country == countryId);
            }
            if (storeId.HasValue)
            {
                ordersQuery = ordersQuery.Where(o => o.ManufacturingCompanyId == storeId);
            }

            // Filter by delivery company 
            if (deliveryCompanyId.HasValue)
            {
                ordersQuery = ordersQuery.Where(o => o.DeliveryCompany.Id == deliveryCompanyId.Value);
            }

            // If the user is not admin, filter orders by user ID
            if (!isAdmin)
            {
                ordersQuery = ordersQuery.Where(o => o.DeliveryCompany.UserId == userId);
            }

            // Fetch orders data
            var orders = await ordersQuery.ToListAsync();

            // Filter out null ManufacturingCompany instances
            orders = orders.Where(o => o.ManufacturingCompany != null).ToList();

            // Fetch all delivery companies with filtering
            var allDeliveryCompanies = await _context.DeliveryCompanies
                .Where(dc => dc.IsShown && !dc.IsRepresentative)
                .Where(dc => !countryId.HasValue || dc.Country == countryId.Value)
                .Where(dc => !deliveryCompanyId.HasValue || dc.Id == deliveryCompanyId.Value)
                .Where(dc => !storeId.HasValue || dc.Id == storeId.Value)
                .ToListAsync();

            // Get all manufacturing companies
            var allManufacturingCompanies = await _context.ManufacturingCompanies.Where(a => a.IsShown).ToListAsync();

            // Group orders by manufacturing company, then by delivery company
            var groupedOrders = orders
                .GroupBy(o => o.ManufacturingCompany)
                .SelectMany(manufacturerGroup => manufacturerGroup.GroupBy(o => o.DeliveryCompany, (key, g) => new { Manufacturer = manufacturerGroup.Key, DeliveryCompany = key, Orders = g }));

            var numbersoforders = groupedOrders
                .SelectMany(group => group.Orders) // Flatten the groups to access individual orders
                .Where(o => o.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد)
                .Count();

            // Calculate total ongoing account difference for all delivery companies
            // Calculate total ongoing account difference for all delivery companies
            var totalOngoingAccountDifferenceResult = groupedOrders.SelectMany(group =>
            {
                var ordersByDeliveryCompany = group.Orders.GroupBy(o => o.DeliveryCompanyId);

                return ordersByDeliveryCompany.Select(deliveryGroup =>
                {
                    var deliveryCompany = deliveryGroup.First().DeliveryCompany;

                    var ongoingAccountOrdersTotalPrice = deliveryGroup
                        .Where(x => x.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد)
                        .Sum(o => o.DeliveryPrice);  // Use DeliveryPrice directly from Order

                    var OnGoingAccountPrice = deliveryGroup
                        .Where(x => x.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد)
                        .Sum(x => x.TotalPrice);

                    var difference = OnGoingAccountPrice - ongoingAccountOrdersTotalPrice;

                    return new
                    {
                        Difference = difference,
                        DifferenceInUSD = _currencyExchangeService.ConvertToUSD(difference, deliveryCompany.Country.ToString())
                    };
                });
            }).ToList();


            // Sum up the results
            var totalPriceDollar = totalOngoingAccountDifferenceResult.Sum(x => x.DifferenceInUSD);
            var totalPricelocal = totalOngoingAccountDifferenceResult.Sum(x => x.Difference);

            // Determine if TotalLocalCurrencyPrice should be calculated
            string totalLocalCurrencyPrice;
            string currency;
            if (countryId.HasValue)
            {
                totalLocalCurrencyPrice = _decimalFormattingService.DecimalFormat(totalPricelocal); // Already in local currency
                currency = Common.GetCurrencyByCountryName(countryId.Value.ToString()); // Ensure countryId is properly converted to string
            }
            else
            {
                totalLocalCurrencyPrice = ""; // Or set to 0 if you prefer
                currency = ""; // Set currency to "N/A" if no country is selected


            }

            // Construct GetFinancialManufacturingCompanyDataOnGoingDeliveryCompany list
            var result = groupedOrders.Select(group =>
            {
                var deliveryCompany = group.DeliveryCompany;
                var manufacturingCompany = group.Manufacturer;

                var deferredTotalDeliveryCompanyPrice = group.Orders
                    .Where(o => o.OrderStatus == OrderStatusEnum.تم_التجهيز ||
                                o.OrderStatus == OrderStatusEnum.قيد_التوصيل ||
                                o.OrderStatus == OrderStatusEnum.طلب_جديد ||
                                o.OrderStatus == OrderStatusEnum.الطلبات_المؤجلة)
                    .Sum(o => o.DeliveryPrice);  // Use DeliveryPrice directly from Order

                var deferredTotalPrice = group.Orders
                    .Where(x => x.OrderStatus == OrderStatusEnum.تم_التجهيز ||
                                x.OrderStatus == OrderStatusEnum.قيد_التوصيل ||
                                x.OrderStatus == OrderStatusEnum.طلب_جديد ||
                                x.OrderStatus == OrderStatusEnum.الطلبات_المؤجلة)
                    .Sum(x => x.TotalPrice);

                var ongoingAccountOrdersTotalPrice = group.Orders
                    .Where(x => x.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد)
                    .Sum(o => o.DeliveryPrice);  // Use DeliveryPrice directly from Order

                var OnGoingAccountPrice = group.Orders
                    .Where(x => x.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد)
                    .Sum(x => x.TotalPrice);

                return new GetFinancialManufacturingCompanyDataOnGoingDeliveryCompany
                {
                    TotalPriceDollar = _decimalFormattingService.DecimalFormat(totalPriceDollar),
                    ToTalPriceTl = _decimalFormattingService.DecimalFormat(_currencyExchangeService.ConvertToTurkishLira(totalPriceDollar)),
                    NumberOfOrders = numbersoforders,
                    ManufacturingCompany = new GetDataListViewModel
                    {
                        Id = manufacturingCompany.Id,
                        Name = manufacturingCompany.Name,
                        LogoUrl = manufacturingCompany?.ImageUrl ?? "static/DefaultImage.svg"
                    },
                    DeliveryCompanies = new List<FinancialDeliveryComapnyViewModelDataList>
            {
                new FinancialDeliveryComapnyViewModelDataList
                {
                    Id = deliveryCompany.Id,
                    Name = deliveryCompany.Name,
                    LogoUrl = deliveryCompany?.ImageUrl ?? "static/DefaultImage.svg",
                    Country = deliveryCompany.Country,
                    Currency = Common.GetCurrencyByCountryName(deliveryCompany.Country.ToString()),
                    DeferredDeliveryCompanyPrice= _decimalFormattingService.DecimalFormat(deferredTotalDeliveryCompanyPrice),
                    DeferredDifference = _decimalFormattingService.DecimalFormat(deferredTotalPrice - deferredTotalDeliveryCompanyPrice),
                    OnGoingAccountDifference = _decimalFormattingService.DecimalFormat(OnGoingAccountPrice - ongoingAccountOrdersTotalPrice),
                    OnGoingAccountDeliveryCompanyPrice = _decimalFormattingService.DecimalFormat(ongoingAccountOrdersTotalPrice),
                }
            }
                };
            }).ToList();

            if (isAdmin)
            {
                var deliveryCompaniesWithNoOrders = allManufacturingCompanies
                    .Select(manufacturingCompany => new GetFinancialManufacturingCompanyDataOnGoingDeliveryCompany
                    {
                        ManufacturingCompany = new GetDataListViewModel
                        {
                            Id = manufacturingCompany.Id,
                            Name = manufacturingCompany.Name ?? "Unknown",
                            LogoUrl = manufacturingCompany?.ImageUrl ?? "static/DefaultImage.svg"
                        },
                        DeliveryCompanies = allDeliveryCompanies
                            .Where(dc => !result.Any(r => r.ManufacturingCompany.Id == manufacturingCompany.Id && r.DeliveryCompanies.Any(d => d.Id == dc.Id)))
                            .Select(dc => new FinancialDeliveryComapnyViewModelDataList
                            {
                                Id = dc.Id,
                                Name = dc.Name,
                                LogoUrl = dc?.ImageUrl ?? "static/DefaultImage.svg",
                                Country = dc.Country,
                                City = dc.City,
                                Currency = Common.GetCurrencyByCountryName(dc.Country.ToString()),
                                DeferredDifference = _decimalFormattingService.DecimalFormat(0),
                                OnGoingAccountDifference = _decimalFormattingService.DecimalFormat(0),
                                OnGoingAccountDeliveryCompanyPrice = _decimalFormattingService.DecimalFormat(0),
                            }).ToList()
                    }).ToList();

                result.AddRange(deliveryCompaniesWithNoOrders);
            }

            var viewModel = new FinancialManufacturingCompanyViewModel
            {
                ManufacturingCompanyData = result,
                
                TotalPriceDollar = _decimalFormattingService.DecimalFormat(totalPriceDollar),
                ToTalPriceTl = _decimalFormattingService.DecimalFormat(_currencyExchangeService.ConvertToTurkishLira(totalPriceDollar)),
                TotalLocalCurrenyPrice = totalLocalCurrencyPrice,
                Currency=currency,
                NumberOfOrders = numbersoforders
            };

            return viewModel;
        }

        // حسابات المندوبين
        public async Task<FinancialManufacturingCompanyViewModel> GetFinancialManufacturingCompanyDataOnGoingDeliveryRepresntaitves(string userId, bool isAdmin, Common.Countries? countryId, string? CityId  ,int? deliveryCompanyId, int? storeId)
        {
            // Get orders query 
            IQueryable<Order> ordersQuery = _context.Orders
                .Where(o => o.OrderStatus == OrderStatusEnum.تم_التجهيز ||
                            o.OrderStatus == OrderStatusEnum.قيد_التوصيل ||
                            o.OrderStatus == OrderStatusEnum.طلب_جديد ||
                            o.OrderStatus == OrderStatusEnum.الطلبات_المؤجلة ||
                            o.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد)
                .Include(o => o.DeliveryCompany)
                .Include(o => o.ManufacturingCompany)
                .Where(o => o.DeliveryCompany.IsShown)
                .Where(o => o.DeliveryCompany.IsRepresentative)
                .AsQueryable();

            // Filter by country 
            if (countryId.HasValue)
            {
                ordersQuery = ordersQuery.Where(o => o.Country == countryId);
            }
            if (storeId.HasValue)
            {
                ordersQuery = ordersQuery.Where(o => o.ManufacturingCompanyId == storeId);
            }

            // Filter by delivery company 
            if (deliveryCompanyId.HasValue)
            {
                ordersQuery = ordersQuery.Where(o => o.DeliveryCompany.Id == deliveryCompanyId.Value);
            }

            // Filter by CityId if it's not null or empty
            if (!string.IsNullOrEmpty(CityId))
            {
                ordersQuery = ordersQuery.Where(o => o.State == CityId);
            }

            // If the user is not admin, filter orders by user ID
            if (!isAdmin)
            {
                ordersQuery = ordersQuery.Where(o => o.DeliveryCompany.UserId == userId);
            }

            // Fetch orders data
            var orders = await ordersQuery.ToListAsync();

            // Filter out null ManufacturingCompany instances
            orders = orders.Where(o => o.ManufacturingCompany != null).ToList();

            // Fetch all delivery companies with filtering
            var allDeliveryCompanies = await _context.DeliveryCompanies
                .Where(dc => dc.IsShown && !dc.IsRepresentative)
                .Where(dc => !countryId.HasValue || dc.Country == countryId.Value)
                .Where(dc => !deliveryCompanyId.HasValue || dc.Id == deliveryCompanyId.Value)
                .Where(dc => !storeId.HasValue || dc.Id == storeId.Value)
                .Where(dc=>dc.IsRepresentative)
                .ToListAsync();

            // Get all manufacturing companies
            var allManufacturingCompanies = await _context.ManufacturingCompanies.Where(a => a.IsShown ).ToListAsync();

            // Group orders by manufacturing company, then by delivery company
            var groupedOrders = orders
                .GroupBy(o => o.ManufacturingCompany)
                .SelectMany(manufacturerGroup => manufacturerGroup.GroupBy(o => o.DeliveryCompany, (key, g) => new { Manufacturer = manufacturerGroup.Key, DeliveryCompany = key, Orders = g }));

            var numbersoforders = groupedOrders
                .SelectMany(group => group.Orders) // Flatten the groups to access individual orders
                .Where(o => o.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد)
                .Count();

            // Calculate total ongoing account difference for all delivery companies
            // Combine and optimize the calculation of total ongoing account differences
            // Calculate total ongoing account difference for all delivery companies
            var totalOngoingAccountDifferenceResult = groupedOrders.SelectMany(group =>
            {
                var ordersByDeliveryCompany = group.Orders.GroupBy(o => o.DeliveryCompanyId);

                return ordersByDeliveryCompany.Select(deliveryGroup =>
                {
                    var deliveryCompany = deliveryGroup.First().DeliveryCompany;

                    var ongoingAccountOrdersTotalPrice = deliveryGroup
                        .Where(x => x.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد)
                        .Sum(o => o.DeliveryPrice);  // Use DeliveryPrice directly from Order

                    var OnGoingAccountPrice = deliveryGroup
                        .Where(x => x.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد)
                        .Sum(x => x.TotalPrice);

                    var difference = OnGoingAccountPrice - ongoingAccountOrdersTotalPrice;

                    return new
                    {
                        Difference = difference,
                        DifferenceInUSD = _currencyExchangeService.ConvertToUSD(difference, deliveryCompany.Country.ToString())
                    };
                });
            }).ToList();


            // Sum up the results
            var totalPriceDollar = totalOngoingAccountDifferenceResult.Sum(x => x.DifferenceInUSD);
            var totalPricelocal = totalOngoingAccountDifferenceResult.Sum(x => x.Difference);

            // Determine if TotalLocalCurrencyPrice should be calculated
            string totalLocalCurrencyPrice;
            string currency;
            // Ensure totalPricelocal is a decimal and pass it to the formatting service
            if (countryId.HasValue)
            {
                // totalPricelocal should already be a decimal, so no need to convert
                totalLocalCurrencyPrice = _decimalFormattingService.DecimalFormat(totalPricelocal);

                // Get the currency based on the countryId
                currency = Common.GetCurrencyByCountryName(countryId.Value.ToString()); // Ensure proper type conversion
            }
            else
            {
                // Set default values when no country filter is applied
                totalLocalCurrencyPrice = ""; // Or set to 0 if you prefer
                currency = ""; // Or some default value if no country is selected
            }




            // Construct GetFinancialManufacturingCompanyDataOnGoingDeliveryCompany list
            var result = groupedOrders.Select(group =>
            {
                var deliveryCompany = group.DeliveryCompany;
                var manufacturingCompany = group.Manufacturer;

                var deferredTotalDeliveryCompanyPrice = group.Orders
                    .Where(o => o.OrderStatus == OrderStatusEnum.تم_التجهيز ||
                                o.OrderStatus == OrderStatusEnum.قيد_التوصيل ||
                                o.OrderStatus == OrderStatusEnum.طلب_جديد ||
                                o.OrderStatus == OrderStatusEnum.الطلبات_المؤجلة)
                    .Sum(o => o.DeliveryPrice);  // Use DeliveryPrice directly from Order

                var deferredTotalPrice = group.Orders
                    .Where(x => x.OrderStatus == OrderStatusEnum.تم_التجهيز ||
                                x.OrderStatus == OrderStatusEnum.قيد_التوصيل ||
                                x.OrderStatus == OrderStatusEnum.طلب_جديد ||
                                x.OrderStatus == OrderStatusEnum.الطلبات_المؤجلة)
                    .Sum(x => x.TotalPrice);

                var ongoingAccountOrdersTotalPrice = group.Orders
                    .Where(x => x.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد)
                    .Sum(o => o.DeliveryPrice);  // Use DeliveryPrice directly from Order

                var OnGoingAccountPrice = group.Orders
                    .Where(x => x.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد)
                    .Sum(x => x.TotalPrice);

                return new GetFinancialManufacturingCompanyDataOnGoingDeliveryCompany
                {
                    TotalPriceDollar = _decimalFormattingService.DecimalFormat(totalPriceDollar),
                    ToTalPriceTl = _decimalFormattingService.DecimalFormat(_currencyExchangeService.ConvertToTurkishLira(totalPriceDollar)),
                    TotalLocalCurrenyPrice = (totalLocalCurrencyPrice),
                    NumberOfOrders = numbersoforders,
                    ManufacturingCompany = new GetDataListViewModel
                    {
                        Id = manufacturingCompany.Id,
                        Name = manufacturingCompany.Name,
                        LogoUrl = manufacturingCompany?.ImageUrl ?? "static/DefaultImage.svg"
                    },
                    DeliveryCompanies = new List<FinancialDeliveryComapnyViewModelDataList>
            {
                new FinancialDeliveryComapnyViewModelDataList
                {
                    Id = deliveryCompany.Id,
                    Name = deliveryCompany.Name,
                    LogoUrl = deliveryCompany?.ImageUrl ?? "static/DefaultImage.svg",
                    Country = deliveryCompany.Country,
                    DeferredDeliveryCompanyPrice= _decimalFormattingService.DecimalFormat(deferredTotalDeliveryCompanyPrice),
                    Currency = Common.GetCurrencyByCountryName(deliveryCompany.Country.ToString()),
                    DeferredDifference = _decimalFormattingService.DecimalFormat(deferredTotalPrice - deferredTotalDeliveryCompanyPrice),
                    OnGoingAccountDifference = _decimalFormattingService.DecimalFormat(OnGoingAccountPrice - ongoingAccountOrdersTotalPrice),
                    OnGoingAccountDeliveryCompanyPrice = _decimalFormattingService.DecimalFormat(ongoingAccountOrdersTotalPrice),
                }
            }
                };
            }).ToList();

            if (isAdmin)
            {
                var deliveryCompaniesWithNoOrders = allManufacturingCompanies
                    .Select(manufacturingCompany => new GetFinancialManufacturingCompanyDataOnGoingDeliveryCompany
                    {
                        ManufacturingCompany = new GetDataListViewModel
                        {
                            Id = manufacturingCompany.Id,
                            Name = manufacturingCompany.Name ?? "Unknown",
                            LogoUrl = manufacturingCompany?.ImageUrl ?? "static/DefaultImage.svg"
                        },
                        DeliveryCompanies = allDeliveryCompanies
                            .Where(dc => !result.Any(r => r.ManufacturingCompany.Id == manufacturingCompany.Id && r.DeliveryCompanies.Any(d => d.Id == dc.Id)))
                            .Select(dc => new FinancialDeliveryComapnyViewModelDataList
                            {
                                Id = dc.Id,
                                Name = dc.Name,
                                LogoUrl = dc?.ImageUrl ?? "static/DefaultImage.svg",
                                Country = dc.Country,
                                City = dc.City,
                                Currency = Common.GetCurrencyByCountryName(dc.Country.ToString()),
                                DeferredDifference = _decimalFormattingService.DecimalFormat(0),
                                OnGoingAccountDifference = _decimalFormattingService.DecimalFormat(0),
                                OnGoingAccountDeliveryCompanyPrice = _decimalFormattingService.DecimalFormat(0),
                            }).ToList()
                    }).ToList();

                result.AddRange(deliveryCompaniesWithNoOrders);
            }

            var viewModel = new FinancialManufacturingCompanyViewModel
            {
                ManufacturingCompanyData = result,
                TotalPriceDollar = _decimalFormattingService.DecimalFormat(totalPriceDollar),
                ToTalPriceTl = _decimalFormattingService.DecimalFormat(_currencyExchangeService.ConvertToTurkishLira(totalPriceDollar)),
                TotalLocalCurrenyPrice =(totalLocalCurrencyPrice),
                Currency=currency,
                NumberOfOrders = numbersoforders
            };

            return viewModel;
        }

        public FinancialDeliveryCompanyViewModel GetFinancialManfactureCompanythenDeliveryCompanyData(string userId, bool isAdmin, int? deliveryCompanyId, int? manufacturingCompanyId)
        {

            if (!deliveryCompanyId.HasValue)
            {
                // If no delivery company ID is provided, return null or handle the error accordingly.
                return null;
            }

            // Query to get orders for a single delivery company
            IQueryable<Order> ordersQuery = _context.Orders
                .Include(o => o.DeliveryCompany)
                .Where(o => o.DeliveryCompany.IsShown && o.DeliveryCompany.Id == deliveryCompanyId.Value)
                .AsQueryable();

            // If the user is not an admin, filter by their user ID
            if (!isAdmin)
            {
                ordersQuery = ordersQuery.Where(o => o.DeliveryCompany.UserId == userId);
            }

            // Fetch orders
            var orders = ordersQuery.ToList();

            // If no orders exist for the given filters, return null or empty result.
            if (!orders.Any())
            {
                return null;
            }

            var deliveryCompany = orders.FirstOrDefault()?.DeliveryCompany;

            // Calculate the sum of delivery prices and total prices for different statuses
            var deliveredDeliveryCompanyTotalPrice = orders
                .Where(o => o.OrderStatus == OrderStatusEnum.تم_التسليم)
                .Sum(o => o.DeliveryPrice);

            var deferredTotalDeliveryCompanyPrice = orders
                .Where(o => o.OrderStatus == OrderStatusEnum.تم_التجهيز ||
                            o.OrderStatus == OrderStatusEnum.طلب_جديد ||
                            o.OrderStatus == OrderStatusEnum.الطلبات_المؤجلة ||
                            o.OrderStatus == OrderStatusEnum.قيد_التوصيل)
                .Sum(o => o.DeliveryPrice);

            var paidOrdersTotalPriceDeliveryCompanyPrice = orders
                .Where(o => o.OrderStatus == OrderStatusEnum.تم_الدفع)
                .Sum(o => o.DeliveryPrice);

            var ongoingAccountOrdersTotalPrice = orders
                .Where(x => x.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد)
                .Sum(o => o.DeliveryPrice);

            // Total prices for orders based on different statuses
            var pendingTotalPrice = orders
                .Where(x => x.OrderStatus == OrderStatusEnum.طلب_جديد)
                .Sum(x => x.TotalPrice);

            var deliveredTotalPrice = orders
                .Where(o => o.OrderStatus == OrderStatusEnum.تم_التسليم)
                .Sum(x => x.TotalPrice);

            var returnedTotalPrice = orders
                .Where(x => x.OrderStatus == OrderStatusEnum.الطلبات_المرجعة)
                .Sum(x => x.TotalPrice);

            var assignedTotalPrice = orders
                .Where(x => x.OrderStatus == OrderStatusEnum.أخطاء_الشركات_والمندوبين)
                .Sum(x => x.TotalPrice);

            var deferredTotalPrice = orders
                .Where(x => x.OrderStatus == OrderStatusEnum.تم_التجهيز ||
                            x.OrderStatus == OrderStatusEnum.طلب_جديد ||
                            x.OrderStatus == OrderStatusEnum.الطلبات_المؤجلة ||
                            x.OrderStatus == OrderStatusEnum.قيد_التوصيل)
                .Sum(x => x.TotalPrice);

            var underProcessTotalPrice = orders
                .Where(x => x.OrderStatus == OrderStatusEnum.الطلبات_المؤجلة)
                .Sum(x => x.TotalPrice);

            var OnGoingAccountPrice = orders
                .Where(x => x.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد)
                .Sum(x => x.TotalPrice);

            var PaidOrdersPrice = orders
                .Where(x => x.OrderStatus == OrderStatusEnum.تم_الدفع)
                .Sum(x => x.TotalPrice);

            // Counting orders based on statuses
            var pendingOrdersCount = orders.Count(x => x.OrderStatus == OrderStatusEnum.طلب_جديد);
            var deliveredOrdersCount = orders.Count(x => x.OrderStatus == OrderStatusEnum.تم_التسليم);
            var returnedOrdersCount = orders.Count(x => x.OrderStatus == OrderStatusEnum.الطلبات_المرجعة);
            var assignedOrdersCount = orders.Count(x => x.OrderStatus == OrderStatusEnum.أخطاء_الشركات_والمندوبين);
            // Count preparing orders
            var countPreparing = orders.Count(x => x.OrderStatus == OrderStatusEnum.تم_التجهيز);
            Console.WriteLine($"Preparing Orders Count: {countPreparing}");
            Console.WriteLine($"Preparing Orders Count: {countPreparing}");
            Console.WriteLine($"Preparing Orders Count: {countPreparing}");

            // Count orders in delivery
            var countInDelivery = orders.Count(x => x.OrderStatus == OrderStatusEnum.قيد_التوصيل);
            Console.WriteLine($"In Delivery Orders Count: {countInDelivery}");
            Console.WriteLine($"In Delivery Orders Count: {countInDelivery}");
            Console.WriteLine($"In Delivery Orders Count: {countInDelivery}");
            Console.WriteLine($"In Delivery Orders Count: {countInDelivery}");

            // Sum of preparing and in-delivery orders
            var deferredOrdersCount = countPreparing + countInDelivery;
            Console.WriteLine($"Deferred Orders Count (Preparing + In Delivery): {deferredOrdersCount}");
            Console.WriteLine($"Deferred Orders Count (Preparing + In Delivery): {deferredOrdersCount}");
            Console.WriteLine($"Deferred Orders Count (Preparing + In Delivery): {deferredOrdersCount}");


            // Total orders count
            var totalOrders = orders.Count();

            // Under process and ongoing account count
            var underProcessOrdersCount = orders.Count(x => x.OrderStatus == OrderStatusEnum.الطلبات_المؤجلة);
            var OnGoingAccountCount = orders.Count(x => x.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد);
            var PaidOrdersCount = orders.Count(x => x.OrderStatus == OrderStatusEnum.تم_الدفع);

            // Build the view model for the single delivery company
            var viewModel = new FinancialDeliveryCompanyViewModel
            {
                DeliveryCompany = new GetDataListViewModel
                {
                    Id = deliveryCompany.Id,
                    Name = deliveryCompany.Name,
                    Country = deliveryCompany.Country.ToString(),
                    LogoUrl = deliveryCompany.ImageUrl ?? "static/DefaultImage.svg"
                },
                DeliveryCompanyId = deliveryCompanyId.Value,
                DeliveredTotalDelvieryCompanyPrice = _decimalFormattingService.DecimalFormat(deliveredDeliveryCompanyTotalPrice),
                deferredTotalDeliveryCompanyPrice = _decimalFormattingService.DecimalFormat(deferredTotalDeliveryCompanyPrice),
                PaidOrdersDeliveryCompanyPrice = _decimalFormattingService.DecimalFormat(paidOrdersTotalPriceDeliveryCompanyPrice),
                OnGoingAccountDeliveryCompanyPrice = _decimalFormattingService.DecimalFormat(ongoingAccountOrdersTotalPrice),
                deferredOrdersCount = deferredOrdersCount,
                DeliveredTotalPrice = _decimalFormattingService.DecimalFormat(deliveredTotalPrice),
                PendingTotalPrice = _decimalFormattingService.DecimalFormat(pendingTotalPrice),
                ReturnedTotalPrice = _decimalFormattingService.DecimalFormat(returnedTotalPrice),
                AssignedTotalPrice = _decimalFormattingService.DecimalFormat(assignedTotalPrice),
                deferredTotalPrice = _decimalFormattingService.DecimalFormat(deferredTotalPrice),
                OnGoingAccountPrice = _decimalFormattingService.DecimalFormat(OnGoingAccountPrice),
                PaidOrdersPrice = _decimalFormattingService.DecimalFormat(PaidOrdersPrice),
                PendingOrdersCount = pendingOrdersCount,
                DeliveredOrdersCount = deliveredOrdersCount,
                ReturnedOrdersCount = returnedOrdersCount,
                AssignedOrdersCount = assignedOrdersCount,
                UnderProcessOrdersCount = underProcessOrdersCount,
                UnderProcessTotalPrice = _decimalFormattingService.DecimalFormat(underProcessTotalPrice),
                OnGoingAccountCount = OnGoingAccountCount,
                TotalOrdersCount = totalOrders,
                PaidOrdersCount = PaidOrdersCount,
                OnGoingAccountDifference = _decimalFormattingService.DecimalFormat(OnGoingAccountPrice - ongoingAccountOrdersTotalPrice),
                deferredDifference = _decimalFormattingService.DecimalFormat(deferredTotalPrice - deferredTotalDeliveryCompanyPrice),
                PaidDifference = _decimalFormattingService.DecimalFormat(PaidOrdersPrice - paidOrdersTotalPriceDeliveryCompanyPrice),
                DeliveredDifference = _decimalFormattingService.DecimalFormat(deliveredTotalPrice - deliveredDeliveryCompanyTotalPrice),
                Currency = Common.GetCurrencyByCountryName(deliveryCompany.Country.ToString())
            };

            return viewModel;
        }

        public List<FinancialDeliveryCompanyViewModel> GetFinancialDeliveryCompanyDataPaid(string userId, bool isAdmin)
        {
            // get orders query 
            IQueryable<Order> ordersQuery = _context.Orders
                .Where(o => o.OrderStatus == OrderStatusEnum.تم_الدفع)
                .Include(o => o.DeliveryCompany)
                .AsQueryable();

            ordersQuery = ordersQuery.Where(o => o.DeliveryCompany.IsShown);

            Console.WriteLine($"UserID: {userId}, Role: {isAdmin}");

            // not admin view his details only 
            if (!isAdmin)
            {
                ordersQuery = ordersQuery.Where(o => o.DeliveryCompany.UserId == userId);
            }


            // order query 
            var orders = ordersQuery.ToList();



            // Group orders by delivery company
            var groupedOrders = orders.GroupBy(o => o.DeliveryCompany);



            var deliveryCompanyInfoList = groupedOrders.Select(group =>
            {
                var deliveryCompany = group.Key;
                var deliveryCompanyId = deliveryCompany.Id;



                var PaidTotalDeliveryCompanyPrice = group
                 .Where(o => o.OrderStatus == OrderStatusEnum.تم_الدفع

                          )
            .Sum(o => o.DeliveryPrice);


                var deferredTotalPrice = group
                  .Where(x =>
                              x.OrderStatus == OrderStatusEnum.تم_الدفع)
                  .Sum(x => x.TotalPrice);


                return new FinancialDeliveryCompanyViewModel
                {
                    DeliveryCompany = new GetDataListViewModel { Id = deliveryCompany.Id, Name = deliveryCompany.Name, Country = deliveryCompany.Country.ToString() },
                    DeliveryCompanyId = deliveryCompanyId,
                    Currency = Common.GetCurrencyByCountryName(deliveryCompany.Country.ToString()),
                    PaidOrdersDeliveryCompanyPrice = _decimalFormattingService.DecimalFormat(PaidTotalDeliveryCompanyPrice),
                    PaidOrdersDeliveryCompanyPriceDollar = DecimalFormattingService.FormatDecimal(_currencyExchangeService.ConvertToUSD(PaidTotalDeliveryCompanyPrice, deliveryCompany.Country.ToString())), // Adjusted call to static method

                };
            })
                 .ToList();

            return deliveryCompanyInfoList;


        }

        public List<FinancialDeliveryCompanyViewModel> GetFinancialDeliveryCompanyDataOnGoingOnly(string userId, bool isAdmin)
        {
            // get orders query 
            IQueryable<Order> ordersQuery = _context.Orders
                .Where(o => o.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد)
                .Include(o => o.DeliveryCompany)
                .AsQueryable();

            ordersQuery = ordersQuery.Where(o => o.DeliveryCompany.IsShown);


            // not admin view his details only 
            if (!isAdmin)
            {
                ordersQuery = ordersQuery.Where(o => o.DeliveryCompany.UserId == userId);
            }


            // order query 
            var orders = ordersQuery.ToList();

    
            // Group orders by delivery company
            var groupedOrders = orders.GroupBy(o => o.DeliveryCompany);



            var deliveryCompanyInfoList = groupedOrders.Select(group =>
            {
                var deliveryCompany = group.Key;
                var deliveryCompanyId = deliveryCompany.Id;



                var PaidTotalDeliveryCompanyPrice = group
                 .Where(o => o.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد

                          )
            .Sum(o => o.DeliveryPrice);


                var deferredTotalPrice = group
                  .Where(x =>
                              x.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد)
                  .Sum(x => x.TotalPrice);


                return new FinancialDeliveryCompanyViewModel
                {
                    DeliveryCompany = new GetDataListViewModel { Id = deliveryCompany.Id, Name = deliveryCompany.Name, Country = deliveryCompany.Country.ToString() },
                    DeliveryCompanyId = deliveryCompanyId,
                    Currency = Common.GetCurrencyByCountryName(deliveryCompany.Country.ToString()),
                    OnGoingAccountDeliveryCompanyPrice = _decimalFormattingService.DecimalFormat(PaidTotalDeliveryCompanyPrice),
                    OnGoingAccountDeliveryCompanyPriceDollar = DecimalFormattingService.FormatDecimal(_currencyExchangeService.ConvertToUSD(PaidTotalDeliveryCompanyPrice, deliveryCompany.Country.ToString())), // Adjusted call to static method
                };
            })
                 .ToList();

            return deliveryCompanyInfoList;


        }

        public List<FinancialDeliveryCompanydeferredTotalPriceViewModel> GetFinancialDeliveryCompanydeferredTotalPrice(string userId, bool isAdmin)
        {
            var ordersQuery = _context.Orders
                .Where(o => o.OrderStatus == OrderStatusEnum.تم_التجهيز ||
                            o.OrderStatus == OrderStatusEnum.قيد_التوصيل ||
                            o.OrderStatus == OrderStatusEnum.طلب_جديد
                              || o.OrderStatus == OrderStatusEnum.الطلبات_المؤجلة

                        )
                .Include(o => o.DeliveryCompany)
                .Where(o => o.DeliveryCompany.IsShown)
                .ToList();

            // not admin view his details only 
            if (!isAdmin)
            {
                // Convert the filtered IEnumerable back to a List
                ordersQuery = ordersQuery.Where(o => o.DeliveryCompany.UserId == userId).ToList();
            }

            var OrderPrices = ordersQuery.Sum(x => x.TotalPrice);

            var groupedOrders = ordersQuery.GroupBy(o => o.DeliveryCompany);

            var deliveryCompanyInfoList = groupedOrders.Select(group =>
            {
                var groupTotalPrice = group.Sum(o => o.TotalPrice);
                var groupDeliveryPriceSum = group.Sum(o => o.DeliveryPrice);

                Console.WriteLine(groupTotalPrice + "+++++++" + groupDeliveryPriceSum);
                return new FinancialDeliveryCompanydeferredTotalPriceViewModel
                {
                    DeliveryCompanyId = group.Key.Id,
                    Currency = Common.GetCurrencyByCountryName(group.Key.Country.ToString()),
                    DeferredDifference = _decimalFormattingService.DecimalFormat(groupTotalPrice - groupDeliveryPriceSum),
                    DeliveryPrice = _decimalFormattingService.DecimalFormat(groupDeliveryPriceSum),
                };
            }).ToList();

            return deliveryCompanyInfoList;
        }


    }


    public class FinancialDeliveryCompanydeferredTotalPriceViewModel
    {
        public int DeliveryCompanyId { get; set; }
        public string Currency { get; set; }
        public string DeferredDifference { get; set; }
        // Add other properties as necessary
        public string DeliveryPrice { get; set; }

    }

}


