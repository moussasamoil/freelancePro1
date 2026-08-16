using jsreport.Types;
using lotus_blue.API;
using lotus_blue.Data;
using lotus_blue.Hubs;
using lotus_blue.Models;
using lotus_blue.Models.ViewModel;
using lotus_blue.OrderStatus;
using lotus_blue.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Z.EntityFramework.Plus;
using static lotus_blue.Models.Common;
using static NuGet.Packaging.PackagingConstants;

namespace lotus_blue.Controllers
{
    public class FinancialController : Controller
    {

        private readonly ApplicationDbContext _context; // Your DbContext
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly GetCurrentTimeInIstanbul _timeService;
        private readonly DeliveryCompanyService _deliveryCompanyService;
        private readonly GetCurrentTimeInIstanbul _getCurrentTimeInIstanbul;
        private readonly PdfReportGenerator _reportGenerator;
        private readonly FinancialService _financialService;
        private readonly CurrencyExchangeService _currencyExchangeService;
        private readonly DecimalFormattingService _decimalFormattingService;
        private readonly OrderService _orderService;
        private readonly DataCacheService _dataCacheService;
        private readonly RESTAPI _restApi;
        private readonly IHubContext<OrderHub> _hubContext;

        public FinancialController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, GetCurrentTimeInIstanbul timeService, DeliveryCompanyService deliveryCompanyService, GetCurrentTimeInIstanbul getCurrentTimeInIstanbul, PdfReportGenerator reportGenerator, FinancialService financialService,
            CurrencyExchangeService currencyExchangeService, DecimalFormattingService decimalFormattingService,
            OrderService orderService, DataCacheService dataCacheService, RESTAPI restApi,
             IHubContext<OrderHub> hubContext)
        {
            _context = context;
            _userManager = userManager;
            _timeService = timeService;
            _deliveryCompanyService = deliveryCompanyService;
            _getCurrentTimeInIstanbul = getCurrentTimeInIstanbul;
            _reportGenerator = reportGenerator;

            _financialService = financialService;
            _currencyExchangeService = currencyExchangeService;
            _decimalFormattingService = decimalFormattingService;
            _orderService = orderService;
            _dataCacheService = dataCacheService;
            _restApi = restApi;
            _hubContext = hubContext;
        }

        public IActionResult Index()
        {
            return View();
        }

        // done
        // حساباتي
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Countries()
        {
            try
            {
                // Get all orders with the specified order status
                var orders = await _context.Orders
                    .Where(o => o.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد)
                    .Select(o => new { o.Id, o.Country, o.TotalPrice, o.DeliveryPrice })
                    .AsNoTracking()
                    .ToListAsync();

                // Group orders by country and calculate total prices after subtracting delivery prices
                var ordersByCountry = orders
                    .GroupBy(o => o.Country)
                    .Select(group => new
                    {
                        Country = group.Key,
                        TotalPrice = group.Sum(o => o.TotalPrice - o.DeliveryPrice)
                    })
                    .ToList();

                // Get all countries from the orders
                var allCountries = Enum.GetValues(typeof(Common.Countries))
                    .Cast<Common.Countries>()
                    .Select(c => c.ToString())
                    .ToList();

                // Create view models for each country
                var ordersViewModel = allCountries.Select(country =>
                {
                    var orderForCountry = ordersByCountry.FirstOrDefault(o => o.Country.ToString() == country);
                    var totalPrice = orderForCountry?.TotalPrice ?? 0;

                    var totalPirceDollar = _currencyExchangeService.ConvertToUSD(totalPrice, country);
                    var totalPirceTl = _currencyExchangeService.ConvertToTurkishLira(totalPirceDollar);

                    return new OrderByCountryViewModel
                    {
                        SelectedCountry = country,
                        Currency = Common.GetCurrencyByCountryName(country),
                        TotalPrice = _decimalFormattingService.DecimalFormat(totalPrice),
                        TotalPirceDollar = _decimalFormattingService.DecimalFormat(totalPirceDollar),
                        TotalPirceTl = _decimalFormattingService.DecimalFormat(totalPirceTl)
                    };
                }).ToList();

                // Calculate total sum in dollars and TL
                var totalSumInDollars = ordersViewModel.Sum(o => decimal.Parse(o.TotalPirceDollar));
                var totalSumInTL = ordersViewModel.Sum(o => decimal.Parse(o.TotalPirceTl));

                // Set total sum for all countries
                ordersViewModel.ForEach(o =>
                {
                    o.TotalSumInDollars = _decimalFormattingService.DecimalFormat(totalSumInDollars);
                    o.TotalSumInTL = _decimalFormattingService.DecimalFormat(totalSumInTL);
                });

                return View(ordersViewModel);
            }
            catch (Exception ex)
            {
                // Log the exception if needed
                if (ex is InvalidOperationException || ex is ArgumentException)
                {
                    TempData["ErrorMessage"] = ex.Message;
                }
                else
                {
                    TempData["ErrorMessage"] = "An error occurred while processing your request. Please try again later.";
                    // Log the exception here if necessary
                }
                return View("Error");
            }
        }




        // حسابات المتاجر لشركات التوصيل 
        [Authorize(Roles = "Admin,Accountant,DeliveryCompany")]
        public async Task<IActionResult> OrderByManfacturingCompanyOnGoingDeliveryCompany(int? deliveryCompanyId, Common.Countries? countryId = null, int? storeId = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Get the current user's ID

            // Check if the user is an Admin or Accountant
            bool isAdminOrAccountant = User.IsInRole("Admin") || User.IsInRole("Accountant");

            // Ensure you are awaiting the asynchronous call
            var viewModel = await _financialService.GetFinancialManufacturingCompanyDataOnGoingDeliveryCompany(userId, isAdminOrAccountant, countryId, deliveryCompanyId, storeId);

            // Pass the result to the view
            return View(viewModel);
        }



        //done
        // حسابات المتاجر  للمندوبين 
        [Authorize(Roles = "Admin,Accountant,DeliveryRepresentative,FollowUpDepartment,ExecutiveDirector")]
        public async Task<IActionResult> OrderByManfacturingCompanyOnGoingDeliveryRepresntaitve(int? deliveryCompanyId, Common.Countries? countryId = null, string? cityId = null, int? storeId = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Get the current user's ID

            // Check if the user is an Admin or Accountant
            bool isAdminOrAccountant = User.IsInRole("Admin") || User.IsInRole("Accountant") || User.IsInRole("FollowUpDepartment") || User.IsInRole("ExecutiveDirector");

            // Ensure you are awaiting the asynchronous call
            var viewModel = await _financialService.GetFinancialManufacturingCompanyDataOnGoingDeliveryRepresntaitves(userId, isAdminOrAccountant, countryId, cityId, deliveryCompanyId, storeId);

            // Pass the result to the view
            return View(viewModel);
        }


        //done
        // حسابات المتاجر لشركات التوصيل والمندوبين التفصيلية 
        [Authorize(Roles = "Admin,Accountant")]
        public IActionResult OrderByManafactureCompanyThenDeliveryCompany(int? deliveryCompanyId, int? manafacturecompanyId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Get the current user's ID

            bool isAdminOrAccountant = User.IsInRole("Admin") || User.IsInRole("Accountant");

            var viewModel = _financialService.GetFinancialManfactureCompanythenDeliveryCompanyData(userId, isAdminOrAccountant, deliveryCompanyId, manafacturecompanyId);

            return View(viewModel);
        }





        // done
        // حسابات الموظف وعملياته 
        [Authorize(Roles = "Admin,Accountant")]
        public IActionResult Employees(string userId, DateTime? startDay, DateTime? endDay)
        {
            // Fetch all employees, filter by userId if provided
            var employeesQuery = _context.Employees.Include(e => e.ApplicationUser).Where(e => e.IsShown);
            if (!string.IsNullOrEmpty(userId))
            {
                employeesQuery = employeesQuery.Where(e => e.ApplicationUserId == userId);
            }
            var allEmployees = employeesQuery.ToList();

            // Fetch transactions within the specified date range
            var transactionsQuery = _context.EmployeeTransactions.AsQueryable();
            if (startDay.HasValue && endDay.HasValue)
            {
                transactionsQuery = transactionsQuery.Where(et => et.Date >= startDay && et.Date <= endDay);
            }
            var allTransactionsList = transactionsQuery.ToList();

            var employeeFinancialSummaries = allEmployees.Select(employee =>
            {
                // Get the transactions related to the current employee
                var employeeTransactions = allTransactionsList.Where(et => et.EmployeeId == employee.Id).ToList();

                // Check if the employee has been paid for the previous month
                DateTime previousMonth = DateTime.Now.AddMonths(-1);
                var previousMonthPaymentSummary = _context.EmployeePaymentSummaries
                    .FirstOrDefault(eps => eps.EmployeeId == employee.Id && eps.Month.Month == previousMonth.Month && eps.Month.Year == previousMonth.Year);

                if (previousMonthPaymentSummary == null)
                {
                    // Employee has not been paid for the previous month
                    decimal totalBonus = _orderService.GetEmployeeBonusTotal(employee.ApplicationUserId);
                    decimal totalDeductions = employeeTransactions.Where(t => t.TransactionType == TransactionTypeEnum.خصم).Sum(t => t.Amount);
                    decimal totalRewards = employeeTransactions.Where(t => t.TransactionType == TransactionTypeEnum.مكافأة).Sum(t => t.Amount);
                    decimal totalAdvances = employeeTransactions.Where(t => t.TransactionType == TransactionTypeEnum.سلفة).Sum(t => t.Amount);
                    decimal accumulatedSalary = employee.Salary;

                    decimal totalCurrentAccount = accumulatedSalary - totalDeductions + totalRewards - totalAdvances + totalBonus;

                    return new EmployeeFinancialSummary
                    {
                        Id = employee.Id,
                        EmployeeUserId = employee.ApplicationUserId,
                        EmployeeName = employee.Name,
                        Country = employee.ApplicationUser?.Country,
                        TotalSalary = _decimalFormattingService.DecimalFormat(accumulatedSalary),
                        TotalDeductions = _decimalFormattingService.DecimalFormat(totalDeductions),
                        TotalRewards = _decimalFormattingService.DecimalFormat(totalRewards),
                        TotalAdvances = _decimalFormattingService.DecimalFormat(totalAdvances),
                        TotalBonuses = _decimalFormattingService.DecimalFormat(totalBonus),
                        TotalCurrentAccount = _decimalFormattingService.DecimalFormat(totalCurrentAccount)
                    };
                }
                else
                {
                    // Employee has been paid for the previous month, check the current month's payment status
                    DateTime currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                    var currentMonthPaymentSummary = _context.EmployeePaymentSummaries
                        .FirstOrDefault(eps => eps.EmployeeId == employee.Id && eps.Month.Month == currentMonth.Month && eps.Month.Year == currentMonth.Year);

                    if (currentMonthPaymentSummary != null)
                    {
                        // Employee has been paid for the current month
                        return new EmployeeFinancialSummary
                        {
                            Id = employee.Id,
                            EmployeeName = employee.Name,
                            Country = employee.ApplicationUser?.Country,
                            TotalSalary = _decimalFormattingService.DecimalFormat(0),
                            TotalDeductions = _decimalFormattingService.DecimalFormat(currentMonthPaymentSummary.TotalDeductions),
                            TotalRewards = _decimalFormattingService.DecimalFormat(currentMonthPaymentSummary.TotalBonuses),
                            TotalAdvances = _decimalFormattingService.DecimalFormat(currentMonthPaymentSummary.TotalAdvances),
                            TotalBonuses = _decimalFormattingService.DecimalFormat(currentMonthPaymentSummary.TotalCommissions),
                            TotalCurrentAccount = _decimalFormattingService.DecimalFormat(currentMonthPaymentSummary.OngoingAccount)
                        };
                    }
                    else
                    {
                        // Employee has not been paid for the current month but was paid for the previous month
                        return new EmployeeFinancialSummary
                        {
                            Id = employee.Id,
                            EmployeeName = employee.Name,
                            Country = employee.ApplicationUser?.Country,
                            TotalSalary = _decimalFormattingService.DecimalFormat(0),
                            TotalDeductions = _decimalFormattingService.DecimalFormat(0),
                            TotalRewards = _decimalFormattingService.DecimalFormat(0),
                            TotalAdvances = _decimalFormattingService.DecimalFormat(0),
                            TotalBonuses = _decimalFormattingService.DecimalFormat(0),
                            TotalCurrentAccount = _decimalFormattingService.DecimalFormat(0)
                        };
                    }
                }
            }).ToList();

            return View(employeeFinancialSummaries);
        }




        [HttpPost]
        [Authorize(Roles = "Admin,Accountant")]
        public IActionResult PayEmployee(int employeeId)
        {
            var employee = _context.Employees.FirstOrDefault(e => e.Id == employeeId);
            if (employee == null)
            {
                return NotFound("Employee not found.");
            }

            DateTime previousMonth = DateTime.Now.AddMonths(-1);

            var totalDeductions = _context.EmployeeTransactions
                .Where(t => t.EmployeeId == employeeId && t.TransactionType == TransactionTypeEnum.خصم && t.Date.Month == previousMonth.Month && t.Date.Year == previousMonth.Year)
                .Sum(t => (decimal?)t.Amount) ?? 0;

            var totalRewards = _context.EmployeeTransactions
                .Where(t => t.EmployeeId == employeeId && t.TransactionType == TransactionTypeEnum.مكافأة && t.Date.Month == previousMonth.Month && t.Date.Year == previousMonth.Year)
                .Sum(t => (decimal?)t.Amount) ?? 0;

            var totalAdvances = _context.EmployeeTransactions
                .Where(t => t.EmployeeId == employeeId && t.TransactionType == TransactionTypeEnum.سلفة && t.Date.Month == previousMonth.Month && t.Date.Year == previousMonth.Year)
                .Sum(t => (decimal?)t.Amount) ?? 0;

            var totalBonus = _orderService.GetEmployeeBonusTotal(employee.ApplicationUserId);

            int daysInMonth = DateTime.DaysInMonth(previousMonth.Year, previousMonth.Month);
            decimal dailySalary = employee.Salary / daysInMonth;
            decimal accumulatedSalary = dailySalary * daysInMonth;

            var totalCurrentAccount = accumulatedSalary - totalDeductions + totalRewards + totalAdvances + totalBonus;

            var paymentSummary = new EmployeePaymentSummary
            {
                EmployeeId = employee.Id,
                Month = previousMonth,
                TotalPaid = totalCurrentAccount,
                TotalDeductions = totalDeductions,
                TotalBonuses = totalRewards,
                TotalAdvances = totalAdvances,
                TotalSalaryPaid = accumulatedSalary,
                OngoingAccount = totalCurrentAccount,
                TotalCommissions = totalBonus // New field
            };

            _context.EmployeePaymentSummaries.Add(paymentSummary);
            _context.SaveChanges();

            // Update all related employee transactions to link them to the new EmployeePaymentSummary
            var employeeTransactions = _context.EmployeeTransactions
                .Where(t => t.EmployeeId == employeeId && t.Date.Month == previousMonth.Month && t.Date.Year == previousMonth.Year)
                .ToList();

            foreach (var transaction in employeeTransactions)
            {
                transaction.EmployeePaymentSummaryId = paymentSummary.Id;
            }

            _context.SaveChanges();

            // Update all orders related to this employee's bonuses to mark them as paid
            var orders = _context.Orders
                .Where(o => o.ApplicationUserId == employee.ApplicationUserId && !o.IsBonusPaidForEmployee)
                .ToList();

            foreach (var order in orders)
            {
                order.IsBonusPaidForEmployee = true;
            }

            _context.SaveChanges();

            return RedirectToAction("Employees");
        }


        [Authorize(Roles = "Admin,Accountant")]
        public async Task<IActionResult> EmployeeBonusDetails(
           int page = 1,
           int? pageSize = null,
           string? employeeId = null,
               bool? isEmployeebonus = null

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
            IQueryable<Order> query = _context.Orders
                .Where(a => !a.IsBonusPaidForEmployee)
                .Where(o => o.OrderStatus== OrderStatusEnum.تم_الدفع ||
                            o.OrderStatus ==  OrderStatusEnum.تم_التسليم ||
                            o.OrderStatus ==  OrderStatusEnum.تم_تحديث_الرصيد)
                .AsNoTracking();



            if (!string.IsNullOrEmpty(employeeId))
                query = query.Where(x => x.ApplicationUserId == employeeId);



            if (isEmployeebonus.HasValue)
            {
                query = query.Where(x => x.IsBonus);

            }


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
                     IsBonus = o.IsBonus,
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



            // Create an instance of the HomeViewModel and populate it
            var viewModel = new HomeViewModel
            {
                PaginationViewModel = paginationViewModel,
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




        [HttpPost]
        [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
        public async Task<IActionResult> MarkAsPaid(int deliveryCompanyId, int? manufactureCompanyId)
        {
            var ordersToMarkAsPaid = await _context.Orders
                .Include(a => a.DeliveryCompany)
                .Where(o => o.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد &&
                            o.DeliveryCompanyId == deliveryCompanyId &&
                            o.ManufacturingCompanyId == manufactureCompanyId)
                .ToListAsync();

            if (!ordersToMarkAsPaid.Any())
            {
                return Json(new { success = false, message = "No orders found to update." });
            }

            string userId = _userManager.GetUserId(User);
            DateTime currentTime = _getCurrentTimeInIstanbul.GetIstanbulTimeWithOffset();

            var orderHistories = ordersToMarkAsPaid.Select(order => new OrderStatusHistory
            {
                OrderId = order.Id,
                Status = OrderStatusEnum.تم_الدفع,
                CreatedAt = currentTime,
                ApplicationUserId = userId
            }).ToList();

            // Add all OrderHistory records in one operation
            _context.OrderStatusHistories.AddRange(orderHistories);

            // Perform a batch update using EF Plus
            await _context.Orders
                .Where(o => ordersToMarkAsPaid.Select(order => order.Id).Contains(o.Id))
                .UpdateAsync(o => new Order { OrderStatus = OrderStatusEnum.تم_الدفع, LastEditedDate = currentTime, IsPaid = true });

            await _context.SaveChangesAsync();

            // Broadcast the status update to SignalR groups
            foreach (var orderHistory in orderHistories)
            {
                var orderJson = JsonConvert.SerializeObject(new
                {
                    orderHistory.OrderId,
                    orderHistory.Status,
                    orderHistory.CreatedAt,
                    orderHistory.ApplicationUserId,
                    UserName = orderHistory.User?.UserName ?? "Unknown",
                    StatusPhrase = OrderStatusHelper.GetOrderStatusPhrase(orderHistory.Status ?? OrderStatusEnum.تم_الدفع),
                    ColorStyle = OrderStatusHelper.StatusColorMapping.GetValueOrDefault<OrderStatusEnum, string>(orderHistory.Status ?? OrderStatusEnum.تم_الدفع, "")
                }, new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    Converters = new List<JsonConverter> { new Newtonsoft.Json.Converters.StringEnumConverter() }
                });

                // Broadcast to the UsersExpectDelivery group
                await _hubContext.Clients.Group("UsersExpectDelivery").SendAsync("OrderStatusUpdated", orderJson);

                // Broadcast to the specific delivery company group
                var targetGroup = $"deliveryCompany_{ordersToMarkAsPaid.FirstOrDefault(o => o.Id == orderHistory.OrderId)?.DeliveryCompanyId}";
                if (targetGroup != null)
                {
                    await _hubContext.Clients.Group(targetGroup).SendAsync("OrderStatusUpdated", orderJson);
                }
            }

            // Optionally update status in external system
            foreach (var order in ordersToMarkAsPaid)
            {
                var externalOrderId = order.ExternalOrderId;
                if (externalOrderId.HasValue)
                {
                    var updateStatusRequest = new UpdateStatusRequest
                    {
                        NewStatus = OrderStatusEnum.تم_الدفع,
                    };

                    var response = await _restApi.UpdateOrderStatusAsync(externalOrderId.Value, updateStatusRequest);

                    Console.WriteLine($"External order ID: {externalOrderId}, Response: {response}");
                }
            }

            // Generate PDF Report
            string countryName = ordersToMarkAsPaid.FirstOrDefault()?.Country.ToString();
            decimal totalAmountValue = ordersToMarkAsPaid.Sum(o => o.TotalPrice);
            string currencyCode = Common.GetCurrencyByCountryName(countryName);
            string totalAmount = $"{totalAmountValue:N2}\u200E {currencyCode}";
            var totalOrderNumber = ordersToMarkAsPaid.Count.ToString();

            var filteredOrders = ordersToMarkAsPaid
                .Where(order => order.DeliveryCompany != null)
                .ToList();

            var deliveryCompanyName = filteredOrders.FirstOrDefault()?.DeliveryCompany?.Name;
            var deliveryCompanyAddress = filteredOrders.FirstOrDefault()?.DeliveryCompany?.Address;
            var deliveryCompanyPhoneNumber = filteredOrders.FirstOrDefault()?.DeliveryCompany?.PhoneNumber;

            // Define headers and value selectors
            var headers = new List<string> { " كود الشحنة", "التاريخ", "اسم العميل", "رقم الهاتف", "المدينة", "المبلغ الإجمالي ", "سعر التوصيل", "صافي المبلغ" };
            var valueSelectors = new List<Func<Order, string>> {
        o => o.Id.ToString(),
        o => o.CreatedDate.ToString("yyyy-MM-dd"),
        o => o.CustomerName,
        o => o.TelephoneNumber,
        o => o.State,
        o => o.TotalPrice.ToString()
    };


            // Calculate RemaningValue and totalDeliveryPrice
            decimal remainingValue = ordersToMarkAsPaid.Sum(order => order.TotalPrice - order.DeliveryPrice);
            decimal totalDeliveryPrice = ordersToMarkAsPaid.Sum(order => order.DeliveryPrice);

            // Create Order Report
            var orderReport = new OrderReport
            {
                OrderIds = ordersToMarkAsPaid.Select(o => o.Id).ToList(),
                GeneratedTime = currentTime,
                TotalAmount = ordersToMarkAsPaid.Sum(o => o.TotalPrice),
                Country = ordersToMarkAsPaid.FirstOrDefault()?.Country,
                DeliveryCompanyId = ordersToMarkAsPaid.FirstOrDefault()?.DeliveryCompanyId,
                DeliveryCompany = ordersToMarkAsPaid.FirstOrDefault()?.DeliveryCompany,
                Orders = ordersToMarkAsPaid,
                OrderStatus = OrderStatusEnum.تم_الدفع
            };

            _context.OrderReports.Add(orderReport);
            await _context.SaveChangesAsync();

            var reportId = orderReport.Id;

            // Insert into OrderReportOrders
            var orderReportOrders = ordersToMarkAsPaid.Select(order => new OrderReportOrder
            {
                OrderReportId = reportId,
                OrderId = order.Id
            }).ToList();

            _context.OrderReportOrders.AddRange(orderReportOrders);
            await _context.SaveChangesAsync();

            string deliveryAmount = $"{totalDeliveryPrice:N2}\u200E {currencyCode}";
            string remaningAmount = $"{remainingValue:N2}\u200E {currencyCode}";

            var pdfBytes = await _reportGenerator.CreatePdfReportAsync(
                ordersToMarkAsPaid, headers, valueSelectors,
                deliveryCompanyName, deliveryCompanyAddress, deliveryCompanyPhoneNumber,
                currentTime.ToString("yyyy-MM-dd"), reportId.ToString(), totalAmount, deliveryAmount, remaningAmount, totalOrderNumber, countryName);

            Response.Headers.Add("Content-Disposition", "inline; filename=OrdersReport.pdf");

            return File(pdfBytes, "application/pdf");
        }


        [HttpPost]
        [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
        public async Task<IActionResult> MarkAsPaidAll(int deliveryCompanyId, List<int>? manufacturingCompanyIds)
        {
            if (manufacturingCompanyIds == null || !manufacturingCompanyIds.Any())
            {
                return Json(new { success = false, message = "No manufacturing companies selected." });
            }

            // Query the orders to be marked as paid
            var ordersToMarkAsPaid = await _context.Orders
               .Include(o => o.DeliveryCompany)
               .Where(o => o.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد &&
                           o.DeliveryCompanyId == deliveryCompanyId &&
                           o.ManufacturingCompanyId.HasValue && // Ensure ManufacturingCompanyId is not null
                           manufacturingCompanyIds.Contains(o.ManufacturingCompanyId.Value)) // Compare the value
               .ToListAsync();




            if (!ordersToMarkAsPaid.Any())
            {
                return Json(new { success = false, message = "No orders found to update." });
            }

            string userId = _userManager.GetUserId(User);
            DateTime currentTime = _getCurrentTimeInIstanbul.GetIstanbulTimeWithOffset();

            // Group orders by ManufactureCompanyId
            var groupedOrders = ordersToMarkAsPaid
                .GroupBy(o => o.ManufacturingCompanyId)
                .ToList();

            foreach (var manufactureGroup in groupedOrders)
            {
                var orders = manufactureGroup.ToList();

                // Create order history for the grouped orders
                var orderHistories = orders.Select(order => new OrderStatusHistory
                {
                    OrderId = order.Id,
                    Status = OrderStatusEnum.تم_الدفع,
                    CreatedAt = currentTime,
                    ApplicationUserId = userId
                }).ToList();

                _context.OrderStatusHistories.AddRange(orderHistories);

                // Perform batch update for the grouped orders
                await _context.Orders
                    .Where(o => orders.Select(order => order.Id).Contains(o.Id))
                    .UpdateAsync(o => new Order { OrderStatus = OrderStatusEnum.تم_الدفع, LastEditedDate = currentTime, IsPaid = true });

                await _context.SaveChangesAsync();

                // SignalR broadcast for each order group
                foreach (var orderHistory in orderHistories)
                {
                    var orderJson = JsonConvert.SerializeObject(new
                    {
                        orderHistory.OrderId,
                        orderHistory.Status,
                        orderHistory.CreatedAt,
                        orderHistory.ApplicationUserId,
                        UserName = orderHistory.User?.UserName ?? "Unknown",
                        StatusPhrase = OrderStatusHelper.GetOrderStatusPhrase(orderHistory.Status ?? OrderStatusEnum.تم_الدفع),
                        ColorStyle = OrderStatusHelper.StatusColorMapping.GetValueOrDefault<OrderStatusEnum, string>(orderHistory.Status ?? OrderStatusEnum.تم_الدفع, "")
                    }, new JsonSerializerSettings
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                        Converters = new List<JsonConverter> { new Newtonsoft.Json.Converters.StringEnumConverter() }
                    });

                    await _hubContext.Clients.Group("UsersExpectDelivery").SendAsync("OrderStatusUpdated", orderJson);

                    var targetGroup = $"deliveryCompany_{orders.FirstOrDefault(o => o.Id == orderHistory.OrderId)?.DeliveryCompanyId}";
                    if (targetGroup != null)
                    {
                        await _hubContext.Clients.Group(targetGroup).SendAsync("OrderStatusUpdated", orderJson);
                    }
                }

                // Optionally update status in external system for each order group
                foreach (var order in orders)
                {
                    var externalOrderId = order.ExternalOrderId;
                    if (externalOrderId.HasValue)
                    {
                        var updateStatusRequest = new UpdateStatusRequest
                        {
                            NewStatus = OrderStatusEnum.تم_الدفع,
                        };

                        var response = await _restApi.UpdateOrderStatusAsync(externalOrderId.Value, updateStatusRequest);
                        Console.WriteLine($"External order ID: {externalOrderId}, Response: {response}");
                    }
                }

                // Generate PDF Report for each manufacture company group
                string countryName = orders.FirstOrDefault()?.Country.ToString();
                decimal totalAmountValue = orders.Sum(o => o.TotalPrice);
                string currencyCode = Common.GetCurrencyByCountryName(countryName);
                string totalAmount = $"{totalAmountValue:N2}\u200E {currencyCode}";
                var totalOrderNumber = orders.Count.ToString();

                var deliveryCompanyName = orders.FirstOrDefault()?.DeliveryCompany?.Name;
                var deliveryCompanyAddress = orders.FirstOrDefault()?.DeliveryCompany?.Address;
                var deliveryCompanyPhoneNumber = orders.FirstOrDefault()?.DeliveryCompany?.PhoneNumber;

                var headers = new List<string> { " كود الشحنة", "التاريخ", "اسم العميل", "رقم الهاتف", "المدينة", "المبلغ الإجمالي ", "سعر التوصيل", "صافي المبلغ" };
                var valueSelectors = new List<Func<Order, string>> {
            o => o.Id.ToString(),
            o => o.CreatedDate.ToString("yyyy-MM-dd"),
            o => o.CustomerName,
            o => o.TelephoneNumber,
            o => o.State,
            o => o.TotalPrice.ToString()
        };

                decimal remainingValue = orders.Sum(order => order.TotalPrice - order.DeliveryPrice);
                decimal totalDeliveryPrice = orders.Sum(order => order.DeliveryPrice);

                var orderReport = new OrderReport
                {
                    OrderIds = orders.Select(o => o.Id).ToList(),
                    GeneratedTime = currentTime,
                    TotalAmount = orders.Sum(o => o.TotalPrice),
                    Country = orders.FirstOrDefault()?.Country,
                    DeliveryCompanyId = orders.FirstOrDefault()?.DeliveryCompanyId,
                    DeliveryCompany = orders.FirstOrDefault()?.DeliveryCompany,
                    Orders = orders,
                    OrderStatus = OrderStatusEnum.تم_الدفع
                };

                _context.OrderReports.Add(orderReport);
                await _context.SaveChangesAsync();

                var reportId = orderReport.Id;

                var orderReportOrders = orders.Select(order => new OrderReportOrder
                {
                    OrderReportId = reportId,
                    OrderId = order.Id
                }).ToList();

                _context.OrderReportOrders.AddRange(orderReportOrders);
                await _context.SaveChangesAsync();

                string deliveryAmount = $"{totalDeliveryPrice:N2}\u200E {currencyCode}";
                string remaningAmount = $"{remainingValue:N2}\u200E {currencyCode}";

                var pdfBytes = await _reportGenerator.CreatePdfReportAsync(
                    orders, headers, valueSelectors,
                    deliveryCompanyName, deliveryCompanyAddress, deliveryCompanyPhoneNumber,
                    currentTime.ToString("yyyy-MM-dd"), reportId.ToString(), totalAmount, deliveryAmount, remaningAmount, totalOrderNumber, countryName);

                Response.Headers.Add("Content-Disposition", "inline; filename=OrdersReport.pdf");

                // Optionally return each PDF separately or combine them as needed
            }

            return Json(new { success = true, message = "Orders have been updated and reports generated." });
        }



        [HttpPost]
        [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
        public async Task<IActionResult> MarkAsPaidAllByCountry(Common.Countries countryId, List<int>? deliveryCompanyIds, List<int>? manufacturingCompanyIds)
        {
            // Ensure there are selected delivery companies or manufacturing companies
            if (deliveryCompanyIds == null || !deliveryCompanyIds.Any() || manufacturingCompanyIds == null || !manufacturingCompanyIds.Any())
            {
                return Json(new { success = false, message = "No delivery companies or manufacturing companies selected." });
            }

            // Get all selected delivery companies under the specified country
            var deliveryCompanies = await _context.DeliveryCompanies
                .Where(dc => dc.Country == countryId && !dc.IsRepresentative && deliveryCompanyIds.Contains(dc.Id))
                .ToListAsync();

            if (!deliveryCompanies.Any())
            {
                return Json(new { success = false, message = "No matching delivery companies found in the specified country." });
            }

            // Filter orders by selected delivery companies, manufacturing companies, and OrderStatus
            var orders = await _context.Orders
                .Include(o => o.ManufacturingCompany)
                .Include(o => o.DeliveryCompany)
                .Where(o => o.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد &&
                            deliveryCompanyIds.Contains(o.DeliveryCompanyId) &&
                            manufacturingCompanyIds.Contains(o.ManufacturingCompanyId ?? 0))  // Ensure filtering by ManufacturingCompanyId
                .ToListAsync();

            Console.WriteLine("Processing manufacturing companies: " + string.Join(", ", manufacturingCompanyIds));
            Console.WriteLine("Processing manufacturing companies: " + string.Join(", ", manufacturingCompanyIds));
            Console.WriteLine("Processing manufacturing companies: " + string.Join(", ", manufacturingCompanyIds));
            Console.WriteLine("Processing manufacturing companies: " + string.Join(", ", manufacturingCompanyIds));
            Console.WriteLine("Processing manufacturing companies: " + string.Join(", ", manufacturingCompanyIds));
            Console.WriteLine("Processing manufacturing companies: " + string.Join(", ", manufacturingCompanyIds));
            Console.WriteLine("Processing manufacturing companies: " + string.Join(", ", manufacturingCompanyIds));
            Console.WriteLine("Processing manufacturing companies: " + string.Join(", ", manufacturingCompanyIds));
            Console.WriteLine("Processing manufacturing companies: " + string.Join(", ", manufacturingCompanyIds));
            Console.WriteLine("Processing manufacturing companies: " + string.Join(", ", manufacturingCompanyIds));


            if (!orders.Any())
            {
                return Json(new { success = false, message = "No orders found for the selected criteria." });
            }

            string userId = _userManager.GetUserId(User);
            DateTime currentTime = _getCurrentTimeInIstanbul.GetIstanbulTimeWithOffset();

            // Group orders first by DeliveryCompanyId, then by ManufacturingCompanyId
            var groupedOrdersByDeliveryCompany = orders
                .GroupBy(o => o.DeliveryCompanyId)
                .ToList();

            foreach (var deliveryCompanyGroup in groupedOrdersByDeliveryCompany)
            {
                var deliveryCompanyId = deliveryCompanyGroup.Key;
                var deliveryCompany = deliveryCompanyGroup.FirstOrDefault()?.DeliveryCompany;

                // Ensure the grouping by ManufacturingCompanyId works correctly
                var groupedOrdersByManufacturingCompany = deliveryCompanyGroup
                    .GroupBy(o => o.ManufacturingCompanyId)
                    .ToList();

                foreach (var manufactureGroup in groupedOrdersByManufacturingCompany)
                {
                    var ordersForManufacture = manufactureGroup.ToList();

                    // Create order history for each group of manufacturing company orders
                    var orderHistories = ordersForManufacture.Select(order => new OrderStatusHistory
                    {
                        OrderId = order.Id,
                        Status = OrderStatusEnum.تم_الدفع,
                        CreatedAt = currentTime,
                        ApplicationUserId = userId
                    }).ToList();

                    _context.OrderStatusHistories.AddRange(orderHistories);
                    Console.WriteLine($"Updating orders for manufacturing company {manufactureGroup.Key}: {ordersForManufacture.Count}");
                    Console.WriteLine($"Updating orders for manufacturing company {manufactureGroup.Key}: {ordersForManufacture.Count}");
                    Console.WriteLine($"Updating orders for manufacturing company {manufactureGroup.Key}: {ordersForManufacture.Count}");
                    Console.WriteLine($"Updating orders for manufacturing company {manufactureGroup.Key}: {ordersForManufacture.Count}");
                    Console.WriteLine($"Updating orders for manufacturing company {manufactureGroup.Key}: {ordersForManufacture.Count}");
                    Console.WriteLine($"Updating orders for manufacturing company {manufactureGroup.Key}: {ordersForManufacture.Count}");
                    Console.WriteLine($"Updating orders for manufacturing company {manufactureGroup.Key}: {ordersForManufacture.Count}");
                    Console.WriteLine($"Updating orders for manufacturing company {manufactureGroup.Key}: {ordersForManufacture.Count}");
                    Console.WriteLine($"Updating orders for manufacturing company {manufactureGroup.Key}: {ordersForManufacture.Count}");
                    Console.WriteLine($"Updating orders for manufacturing company {manufactureGroup.Key}: {ordersForManufacture.Count}");
                    Console.WriteLine($"Updating orders for manufacturing company {manufactureGroup.Key}: {ordersForManufacture.Count}");
                    Console.WriteLine($"Updating orders for manufacturing company {manufactureGroup.Key}: {ordersForManufacture.Count}");
                    Console.WriteLine($"Updating orders for manufacturing company {manufactureGroup.Key}: {ordersForManufacture.Count}");
                    Console.WriteLine($"Updating orders for manufacturing company {manufactureGroup.Key}: {ordersForManufacture.Count}");

                    // Perform batch update for each group of manufacturing company orders
                    await _context.Orders
                        .Where(o => ordersForManufacture.Select(order => order.Id).Contains(o.Id))
                        .UpdateAsync(o => new Order
                        {
                            OrderStatus = OrderStatusEnum.تم_الدفع,
                            LastEditedDate = currentTime,
                            IsPaid = true
                        });

                    await _context.SaveChangesAsync();
                    Console.WriteLine($"Orders updated for manufacturing company {manufactureGroup.Key}");
                    Console.WriteLine($"Orders updated for manufacturing company {manufactureGroup.Key}");
                    Console.WriteLine($"Orders updated for manufacturing company {manufactureGroup.Key}");
                    Console.WriteLine($"Orders updated for manufacturing company {manufactureGroup.Key}");
                    Console.WriteLine($"Orders updated for manufacturing company {manufactureGroup.Key}");
                    Console.WriteLine($"Orders updated for manufacturing company {manufactureGroup.Key}");
                    Console.WriteLine($"Orders updated for manufacturing company {manufactureGroup.Key}");
                    Console.WriteLine($"Orders updated for manufacturing company {manufactureGroup.Key}");
                    Console.WriteLine($"Orders updated for manufacturing company {manufactureGroup.Key}");

                    // SignalR broadcasting for each order in the group
                    foreach (var orderHistory in orderHistories)
                    {
                        var orderJson = JsonConvert.SerializeObject(new
                        {
                            orderHistory.OrderId,
                            orderHistory.Status,
                            orderHistory.CreatedAt,
                            orderHistory.ApplicationUserId,
                            UserName = orderHistory.User?.UserName ?? "Unknown",
                            StatusPhrase = OrderStatusHelper.GetOrderStatusPhrase(orderHistory.Status ?? OrderStatusEnum.تم_الدفع),
                            ColorStyle = OrderStatusHelper.StatusColorMapping.GetValueOrDefault<OrderStatusEnum, string>(orderHistory.Status ?? OrderStatusEnum.تم_الدفع, "")
                        }, new JsonSerializerSettings
                        {
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                            Converters = new List<JsonConverter> { new Newtonsoft.Json.Converters.StringEnumConverter() }
                        });

                        await _hubContext.Clients.Group("UsersExpectDelivery").SendAsync("OrderStatusUpdated", orderJson);

                        var targetGroup = $"deliveryCompany_{deliveryCompanyId}";
                        if (targetGroup != null)
                        {
                            await _hubContext.Clients.Group(targetGroup).SendAsync("OrderStatusUpdated", orderJson);
                        }
                    }

                    // Optionally update status in external system for each order group (if applicable)
                    foreach (var order in ordersForManufacture)
                    {
                        var externalOrderId = order.ExternalOrderId;
                        if (externalOrderId.HasValue)
                        {
                            var updateStatusRequest = new UpdateStatusRequest
                            {
                                NewStatus = OrderStatusEnum.تم_الدفع,
                            };

                            var response = await _restApi.UpdateOrderStatusAsync(externalOrderId.Value, updateStatusRequest);
                            Console.WriteLine($"External order ID: {externalOrderId}, Response: {response}");
                        }
                    }

                    // Generate PDF Report for each manufacturing company group
                    string totalAmount = $"{ordersForManufacture.Sum(o => o.TotalPrice - o.DeliveryPrice):N2}";
                    var headers = new List<string> { " كود الشحنة", "التاريخ", "اسم العميل", "رقم الهاتف", "المدينة", "المبلغ الإجمالي ", "سعر التوصيل", "صافي المبلغ" };
                    var valueSelectors = new List<Func<Order, string>> {
                o => o.Id.ToString(),
                o => o.CreatedDate.ToString("yyyy-MM-dd"),
                o => o.CustomerName,
                o => o.TelephoneNumber,
                o => o.State,
                o => o.TotalPrice.ToString()
            };

                    decimal remainingValue = ordersForManufacture.Sum(order => order.TotalPrice - order.DeliveryPrice);
                    decimal totalDeliveryPrice = ordersForManufacture.Sum(order => order.DeliveryPrice);

                    var orderReport = new OrderReport
                    {
                        OrderIds = ordersForManufacture.Select(o => o.Id).ToList(),
                        GeneratedTime = currentTime,
                        TotalAmount = ordersForManufacture.Sum(o => o.TotalPrice),
                        Country = deliveryCompany.Country,
                        DeliveryCompanyId = deliveryCompany.Id,
                        DeliveryCompany = deliveryCompany,
                        Orders = ordersForManufacture,
                        OrderStatus = OrderStatusEnum.تم_الدفع
                    };

                    _context.OrderReports.Add(orderReport);
                    await _context.SaveChangesAsync();

                    var reportId = orderReport.Id;

                    var orderReportOrders = ordersForManufacture.Select(order => new OrderReportOrder
                    {
                        OrderReportId = reportId,
                        OrderId = order.Id
                    }).ToList();

                    _context.OrderReportOrders.AddRange(orderReportOrders);
                    await _context.SaveChangesAsync();

                    string deliveryAmount = $"{totalDeliveryPrice:N2}\u200E {Common.GetCurrencyByCountryName(countryId.ToString())}";
                    string remainingAmount = $"{remainingValue:N2}\u200E {Common.GetCurrencyByCountryName(countryId.ToString())}";

                    var pdfBytes = await _reportGenerator.CreatePdfReportAsync(
                        ordersForManufacture, headers, valueSelectors,
                        deliveryCompany.Name, deliveryCompany.Address, deliveryCompany.PhoneNumber,
                        currentTime.ToString("yyyy-MM-dd"), reportId.ToString(), totalAmount, deliveryAmount, remainingAmount, ordersForManufacture.Count.ToString(), countryId.ToString());

                    // Check if the Content-Disposition header already exists before adding it
                    if (!Response.Headers.ContainsKey("Content-Disposition"))
                    {
                        // Generate a unique filename by appending the report ID or current time
                        string uniqueFileName = $"OrdersReport_{reportId}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                        Response.Headers.Add("Content-Disposition", $"inline; filename={uniqueFileName}");
                    }
                }
            }

            return Json(new { success = true, message = "Orders have been updated and reports generated." });
        }


        [HttpPost]
        [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
        public async Task<IActionResult> MarkAsPaidAllByCity(Common.Countries countryId, List<int>? deliveryCompanyIds, List<int>? manufacturingCompanyIds, string? cityId)
        {
            // Ensure there are selected delivery companies or manufacturing companies
            if (deliveryCompanyIds == null || !deliveryCompanyIds.Any() || manufacturingCompanyIds == null || !manufacturingCompanyIds.Any())
            {
                return Json(new { success = false, message = "No delivery companies or manufacturing companies selected." });
            }

            // Get all selected delivery companies under the specified country and city
            var deliveryCompanies = await _context.DeliveryCompanies
                .Where(dc => dc.Country == countryId && dc.IsRepresentative && deliveryCompanyIds.Contains(dc.Id) && (string.IsNullOrEmpty(cityId) || dc.City == cityId))
                .ToListAsync();

            if (!deliveryCompanies.Any())
            {
                return Json(new { success = false, message = "No matching delivery companies found in the specified country and city." });
            }

            // Filter orders by selected delivery companies, manufacturing companies, city, and OrderStatus
            var orders = await _context.Orders
                .Include(o => o.ManufacturingCompany)
                .Include(o => o.DeliveryCompany)
                .Where(o => o.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد &&
                            deliveryCompanyIds.Contains(o.DeliveryCompanyId) &&
                            manufacturingCompanyIds.Contains(o.ManufacturingCompanyId ?? 0) &&
                            (string.IsNullOrEmpty(cityId) || o.State == cityId))  // Filter by city (state in orders)
                .ToListAsync();

            if (!orders.Any())
            {
                return Json(new { success = false, message = "No orders found for the selected criteria." });
            }

            string userId = _userManager.GetUserId(User);
            DateTime currentTime = _getCurrentTimeInIstanbul.GetIstanbulTimeWithOffset();

            // Group orders first by DeliveryCompanyId, then by ManufacturingCompanyId
            var groupedOrdersByDeliveryCompany = orders
                .GroupBy(o => o.DeliveryCompanyId)
                .ToList();

            foreach (var deliveryCompanyGroup in groupedOrdersByDeliveryCompany)
            {
                var deliveryCompanyId = deliveryCompanyGroup.Key;
                var deliveryCompany = deliveryCompanyGroup.FirstOrDefault()?.DeliveryCompany;

                var groupedOrdersByManufacturingCompany = deliveryCompanyGroup
                    .GroupBy(o => o.ManufacturingCompanyId)
                    .ToList();

                foreach (var manufactureGroup in groupedOrdersByManufacturingCompany)
                {
                    var ordersForManufacture = manufactureGroup.ToList();

                    // Create order history for the grouped orders
                    var orderHistories = ordersForManufacture.Select(order => new OrderStatusHistory
                    {
                        OrderId = order.Id,
                        Status = OrderStatusEnum.تم_الدفع,
                        CreatedAt = currentTime,
                        ApplicationUserId = userId
                    }).ToList();

                    _context.OrderStatusHistories.AddRange(orderHistories);

                    // Perform batch update for the grouped orders
                    await _context.Orders
                        .Where(o => ordersForManufacture.Select(order => order.Id).Contains(o.Id))
                        .UpdateAsync(o => new Order
                        {
                            OrderStatus = OrderStatusEnum.تم_الدفع,
                            LastEditedDate = currentTime,
                            IsPaid = true
                        });

                    await _context.SaveChangesAsync();

                    // SignalR broadcast for each order group
                    foreach (var orderHistory in orderHistories)
                    {
                        var orderJson = JsonConvert.SerializeObject(new
                        {
                            orderHistory.OrderId,
                            orderHistory.Status,
                            orderHistory.CreatedAt,
                            orderHistory.ApplicationUserId,
                            UserName = orderHistory.User?.UserName ?? "Unknown",
                            StatusPhrase = OrderStatusHelper.GetOrderStatusPhrase(orderHistory.Status ?? OrderStatusEnum.تم_الدفع),
                            ColorStyle = OrderStatusHelper.StatusColorMapping.GetValueOrDefault<OrderStatusEnum, string>(orderHistory.Status ?? OrderStatusEnum.تم_الدفع, "")
                        }, new JsonSerializerSettings
                        {
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                            Converters = new List<JsonConverter> { new Newtonsoft.Json.Converters.StringEnumConverter() }
                        });

                        await _hubContext.Clients.Group("UsersExpectDelivery").SendAsync("OrderStatusUpdated", orderJson);

                        var targetGroup = $"deliveryCompany_{deliveryCompanyId}";
                        if (targetGroup != null)
                        {
                            await _hubContext.Clients.Group(targetGroup).SendAsync("OrderStatusUpdated", orderJson);
                        }
                    }

                    // Optionally update status in external system for each order group (if applicable)
                    foreach (var order in ordersForManufacture)
                    {
                        var externalOrderId = order.ExternalOrderId;
                        if (externalOrderId.HasValue)
                        {
                            var updateStatusRequest = new UpdateStatusRequest
                            {
                                NewStatus = OrderStatusEnum.تم_الدفع,
                            };

                            var response = await _restApi.UpdateOrderStatusAsync(externalOrderId.Value, updateStatusRequest);
                            Console.WriteLine($"External order ID: {externalOrderId}, Response: {response}");
                        }
                    }

                    // Generate PDF Report for each manufacturing company group
                    string totalAmount = $"{ordersForManufacture.Sum(o => o.TotalPrice - o.DeliveryPrice):N2}";
                    var headers = new List<string> { " كود الشحنة", "التاريخ", "اسم العميل", "رقم الهاتف", "المدينة", "المبلغ الإجمالي ", "سعر التوصيل", "صافي المبلغ" };
                    var valueSelectors = new List<Func<Order, string>> {
                o => o.Id.ToString(),
                o => o.CreatedDate.ToString("yyyy-MM-dd"),
                o => o.CustomerName,
                o => o.TelephoneNumber,
                o => o.State,  // City is referred to as State in Orders
                o => o.TotalPrice.ToString()
            };

                    decimal remainingValue = ordersForManufacture.Sum(order => order.TotalPrice - order.DeliveryPrice);
                    decimal totalDeliveryPrice = ordersForManufacture.Sum(order => order.DeliveryPrice);

                    var orderReport = new OrderReport
                    {
                        OrderIds = ordersForManufacture.Select(o => o.Id).ToList(),
                        GeneratedTime = currentTime,
                        TotalAmount = ordersForManufacture.Sum(o => o.TotalPrice),
                        Country = deliveryCompany.Country,
                        DeliveryCompanyId = deliveryCompany.Id,
                        DeliveryCompany = deliveryCompany,
                        Orders = ordersForManufacture,
                        OrderStatus = OrderStatusEnum.تم_الدفع
                    };

                    _context.OrderReports.Add(orderReport);
                    await _context.SaveChangesAsync();

                    var reportId = orderReport.Id;

                    var orderReportOrders = ordersForManufacture.Select(order => new OrderReportOrder
                    {
                        OrderReportId = reportId,
                        OrderId = order.Id
                    }).ToList();

                    _context.OrderReportOrders.AddRange(orderReportOrders);
                    await _context.SaveChangesAsync();

                    string deliveryAmount = $"{totalDeliveryPrice:N2}\u200E {Common.GetCurrencyByCountryName(countryId.ToString())}";
                    string remainingAmount = $"{remainingValue:N2}\u200E {Common.GetCurrencyByCountryName(countryId.ToString())}";

                    var pdfBytes = await _reportGenerator.CreatePdfReportAsync(
                        ordersForManufacture, headers, valueSelectors,
                        deliveryCompany.Name, deliveryCompany.Address, deliveryCompany.PhoneNumber,
                        currentTime.ToString("yyyy-MM-dd"), reportId.ToString(), totalAmount, deliveryAmount, remainingAmount, ordersForManufacture.Count.ToString(), countryId.ToString());

                    Response.Headers.Add("Content-Disposition", "inline; filename=OrdersReport.pdf");
                }
            }

            return Json(new { success = true, message = "Orders have been updated and reports generated." });
        }


        //done
        //كشوف الحسابات
        [HttpGet]
        [Authorize(Roles = "Admin,Accountant,DeliveryCompany,DeliveryRepresentative,ExecutiveDirector")]
        public async Task<IActionResult> OrderReports(int? storeId, int? page, int? pagesize, int? deliveryCompanyIdFilter, DateTime? startDay = null, DateTime? endDay = null, Common.Countries? CountryId = null)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Get logged-in user's ID
            var isDeliveryCompanyRole = User.IsInRole("DeliveryCompany") || User.IsInRole("DeliveryRepresentative");
            int? deliveryCompanyId = null;

            if (isDeliveryCompanyRole)
            {
                deliveryCompanyId = await _context.DeliveryCompanies
                    .Where(dc => dc.UserId == currentUserId)
                    .Select(dc => dc.Id)
                    .FirstOrDefaultAsync();
            }

            var orderReportsQuery = _context.OrderReports
                .Where(or => or.DeliveryCompanyId != null && !or.DeliveryCompany.IsRepresentative && or.OrderStatus == OrderStatusEnum.تم_الدفع);

            if (isDeliveryCompanyRole && deliveryCompanyId.HasValue)
            {
                orderReportsQuery = orderReportsQuery.Where(or => or.DeliveryCompanyId == deliveryCompanyId.Value);
            }

            if (storeId.HasValue)
            {
                orderReportsQuery = orderReportsQuery.Where(or => or.Orders.Any(o => o.ManufacturingCompanyId == storeId.Value));
            }

            if (deliveryCompanyIdFilter.HasValue)
            {
                orderReportsQuery = orderReportsQuery.Where(or => or.DeliveryCompanyId == deliveryCompanyIdFilter.Value);
            }

            if (CountryId.HasValue)
            {
                orderReportsQuery = orderReportsQuery.Where(or => or.Country == CountryId.Value);
            }

            if (startDay.HasValue && endDay.HasValue)
            {
                orderReportsQuery = orderReportsQuery.Where(or => or.GeneratedTime >= startDay.Value && or.GeneratedTime <= endDay.Value);
            }

            // Apply pagination before materializing the query result
            page = page ?? 1; // Default page number
            pagesize = pagesize ?? 10; // Default page size
            var totalItems = await orderReportsQuery.CountAsync(); // Get the total count before pagination

            var orderReports = await orderReportsQuery
                .OrderByDescending(or => or.GeneratedTime)
                .Skip((page.Value - 1) * pagesize.Value)
                .Take(pagesize.Value)
                .Select(or => new OrderReportViewModel
                {
                    Id = or.Id,
                    GeneratedTime = or.GeneratedTime.ToString("yyyy-MM-dd"),
                    TotalAmount = _decimalFormattingService.DecimalFormat(or.Orders.Sum(o => o.TotalPrice - o.DeliveryPrice)),
                    Country = or.Country.ToString(),
                    DeliveryCompanyName = or.DeliveryCompany.Name,
                    Currency = Common.GetCurrencyByCountryName(or.Country.ToString()),

                    // Extract the manufacturing company name from the first order
                    // Move null-checking logic outside of the expression tree
                    StoreName = or.Orders.Any() && or.Orders.FirstOrDefault().ManufacturingCompany != null
                              ? or.Orders.First().ManufacturingCompany.Name
                              : "Unknown"
                })
                .ToListAsync();

            var paginationViewModel = new PaginationViewModel<OrderReportViewModel>
            {
                Items = orderReports,
                CurrentPage = page.Value,
                PageSize = pagesize.Value,
                TotalItems = totalItems // Total count before pagination
            };

            return View(paginationViewModel);
        }



        // done
        //فواتير المندوبين
        [HttpGet]
        [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
        public async Task<IActionResult> OrderReportsRepresentative(int? storeId, int? page, int? pagesize, int? deliveryCompanyIdFilter, DateTime? startDay = null, DateTime? endDay = null, Common.Countries? CountryId = null)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Get logged-in user's ID

            var orderReportsQuery = _context.OrderReports
                .Where(or => or.DeliveryCompanyId != null && or.DeliveryCompany.IsRepresentative && or.OrderStatus == OrderStatusEnum.تم_الدفع);

            if (deliveryCompanyIdFilter.HasValue)
            {
                orderReportsQuery = orderReportsQuery.Where(or => or.DeliveryCompanyId == deliveryCompanyIdFilter.Value);
            }

            if (storeId.HasValue)
            {
                orderReportsQuery = orderReportsQuery.Where(a => a.Orders.First().ManufacturingCompanyId == storeId);
            }

            if (startDay != null && endDay != null)
            {
                orderReportsQuery = orderReportsQuery.Where(or => or.GeneratedTime >= startDay && or.GeneratedTime <= endDay);
            }

            if (CountryId.HasValue)
            {
                orderReportsQuery = orderReportsQuery.Where(or => or.Country == CountryId.Value);
            }

            // Apply pagination before materializing the query result
            page = page ?? 1; // Default page number
            pagesize = pagesize ?? 10; // Default page size
            var totalItems = await orderReportsQuery.CountAsync(); // Get the total count before pagination

            var orderReports = await orderReportsQuery
                .OrderByDescending(or => or.GeneratedTime)
                .Skip((page.Value - 1) * pagesize.Value)
                .Take(pagesize.Value)
                .Select(or => new OrderReportViewModel
                {
                    Id = or.Id,
                    GeneratedTime = or.GeneratedTime.ToString("yyyy-MM-dd"),
                    TotalAmount = _decimalFormattingService.DecimalFormat(or.Orders.Sum(o => o.TotalPrice - (o.DeliveryPrice))),
                    Country = or.Country.ToString(),
                    DeliveryCompanyName = or.DeliveryCompany.Name,
                    Currency = Common.GetCurrencyByCountryName(or.Country.ToString()),
                    City = or.DeliveryCompany.City,
                    StoreName = or.Orders.First().ManufacturingCompany.Name,
                })
                .ToListAsync();

            var paginationViewModel = new PaginationViewModel<OrderReportViewModel>
            {
                Items = orderReports,
                CurrentPage = page.Value,
                PageSize = pagesize.Value,
                TotalItems = totalItems // Total count before pagination
            };

            return View(paginationViewModel);
        }





        [Authorize(Roles = "Admin,Accountant,DeliveryCompany,ExecutiveDirector")]
        public async Task<IActionResult> DownloadOrderReport(int orderReportId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Get logged-in user's ID
            var isDeliveryCompanyRole = User.IsInRole("DeliveryCompany") || User.IsInRole("DeliveryRepresentative");

            // Query to get the order report
            var orderReport = await _context.OrderReports
                .Include(or => or.DeliveryCompany)
                .Include(or => or.OrderReportOrders)
                    .ThenInclude(oro => oro.Order)
                        .ThenInclude(o => o.ManufacturingCompany)
                .FirstOrDefaultAsync(or => or.Id == orderReportId);

            if (orderReport == null)
            {
                return NotFound("Order report not found.");
            }

            // Get the orders associated with the order report
            var orders = orderReport.OrderReportOrders.Select(oro => oro.Order).ToList();

            if (isDeliveryCompanyRole)
            {
                // Filter the orders based on the user's delivery company
                orders = orders.Where(o => o.DeliveryCompany.UserId == currentUserId).ToList();
            }

            if (!orders.Any())
            {
                return NotFound("No orders found for the current user in the report.");
            }

            // Calculate totals directly using DeliveryPrice from orders
            decimal remainingValue = orders.Sum(order => order.TotalPrice - order.DeliveryPrice);
            decimal totalDeliveryPrice = orders.Sum(order => order.DeliveryPrice);

            // Define headers and value selectors for the report
            var headers = new List<string> { "كود الشحنة", "التاريخ", "اسم العميل", "رقم الهاتف", "المدينة", "المبلغ الإجمالي", "سعر التوصيل", "صافي المبلغ" };
            var valueSelectors = new List<Func<Order, string>> {
        o => o.Id.ToString(),
        o => o.CreatedDate.ToString("yyyy-MM-dd"),
        o => o.CustomerName,
        o => o.TelephoneNumber,
        o => o.State,
        o => o.TotalPrice.ToString() // Consider formatting this as currency
    };

            // Prepare additional report details
            string countryName = orders.FirstOrDefault()?.Country.ToString();
            string currencyCode = Common.GetCurrencyByCountryName(countryName);
            decimal totalValue = orders.Sum(o => o.TotalPrice);
            string totalAmount = $"{totalValue:N2}\u200E {currencyCode}";
            string deliveryAmount = $"{totalDeliveryPrice:N2}\u200E {currencyCode}";
            string remainingAmount = $"{remainingValue:N2}\u200E {currencyCode}";
            var deliveryCompanyName = orderReport.DeliveryCompany?.Name ?? "DefaultCompanyName";
            var deliveryCompanyAddress = orderReport.DeliveryCompany?.Address ?? "DefaultCompanyAddress";
            var deliveryCompanyPhoneNumber = orderReport.DeliveryCompany?.PhoneNumber ?? "DefaultCompanyPhoneNumber";
            var createdDateString = orderReport.GeneratedTime.ToString("yyyy-MM-dd");
            var reportIdString = orderReport.Id.ToString();
            var totalOrderNumberString = orders.Count.ToString();

            // Generate the PDF report
            var pdfBytes = await _reportGenerator.CreatePdfReportAsync(
                orders, headers, valueSelectors,
                deliveryCompanyName, deliveryCompanyAddress, deliveryCompanyPhoneNumber,
                createdDateString, reportIdString, totalAmount, deliveryAmount, remainingAmount, totalOrderNumberString, countryName);

            Response.Headers.Add("Content-Disposition", "inline; filename=OrdersReport.pdf");

            return File(pdfBytes, "application/pdf");
        }


        [HttpPost]
        [Authorize(Roles = "Admin,Accountant,DeliveryCompany,ExecutiveDirector")]
        public async Task<IActionResult> GenerateCombinedReport([FromBody] List<int> reportIds)
        {
            if (reportIds == null || reportIds.Count == 0)
            {
                return BadRequest("No reports selected.");
            }

            // Fetch the order reports and include related orders
            var orderReports = await _context.OrderReports
                .Include(or => or.DeliveryCompany)
                .Include(or => or.OrderReportOrders)
                    .ThenInclude(oro => oro.Order)
                        .ThenInclude(o => o.ManufacturingCompany)
                .Where(or => reportIds.Contains(or.Id))
                .ToListAsync();

            if (orderReports == null || orderReports.Count == 0)
            {
                return NotFound("No reports found for the selected IDs.");
            }

            // Collect all the orders related to the selected reports
            var orders = orderReports.SelectMany(or => or.OrderReportOrders.Select(oro => oro.Order)).ToList();

            if (!orders.Any())
            {
                return NotFound("No orders found for the selected reports.");
            }

            // Calculate totals for the combined report
            decimal remainingValue = orders.Sum(order => order.TotalPrice - order.DeliveryPrice);
            decimal totalDeliveryPrice = orders.Sum(order => order.DeliveryPrice);
            decimal totalValue = orders.Sum(o => o.TotalPrice);
            string currencyCode = Common.GetCurrencyByCountryName(orders.FirstOrDefault()?.Country.ToString());

            // Prepare the report data
            var headers = new List<string> { "كود الشحنة", "التاريخ", "اسم العميل", "رقم الهاتف", "المدينة", "المبلغ الإجمالي", "سعر التوصيل", "صافي المبلغ" };
            var valueSelectors = new List<Func<Order, string>> {
        o => o.Id.ToString(),
        o => o.CreatedDate.ToString("yyyy-MM-dd"),
        o => o.CustomerName,
        o => o.TelephoneNumber,
        o => o.State,
        o => o.TotalPrice.ToString()
    };

            // Generate the PDF
            var pdfBytes = await _reportGenerator.CreatePdfReportAsync(
                orders, headers, valueSelectors,
                "Combined Delivery Company", "Combined Address", "Combined Phone",
                DateTime.Now.ToString("yyyy-MM-dd"), "CombinedReport",
                $"{totalValue:N2} {currencyCode}", $"{totalDeliveryPrice:N2} {currencyCode}", $"{remainingValue:N2} {currencyCode}",
                orders.Count.ToString(), orders.FirstOrDefault()?.Country.ToString());

            Response.Headers.Add("Content-Disposition", "inline; filename=CombinedOrdersReport.pdf");

            return File(pdfBytes, "application/pdf");
        }



    }
}
