//using lotus_blue.Data;
//using lotus_blue.Models;
//using lotus_blue.Models.ViewModel;
//using lotus_blue.Services;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Identity;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.IdentityModel.Tokens;
//using System.IdentityModel.Tokens.Jwt;
//using System.Security.Claims;
//using System.Text;
//using lotus_blue.Models.AppViewModel;
//using lotus_blue.ApiToken;
//using Microsoft.AspNetCore.Cors;
//using Microsoft.AspNetCore.Authentication.JwtBearer;
//using static lotus_blue.Models.Common;
//using Microsoft.AspNetCore.SignalR;
//using Newtonsoft.Json;
//using lotus_blue.Hubs;
//using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
//using lotus_blue.API;
//using lotus_blue.OrderStatus;
//using lotus_blue.Roles;
//using System.Diagnostics.Metrics;
//using Microsoft.EntityFrameworkCore.Metadata.Internal;
//using Microsoft.EntityFrameworkCore.Metadata;
//using Microsoft.AspNetCore.Mvc.Rendering;
//using DotNetCorePdf.Enums;
//using Microsoft.AspNetCore.Mvc.RazorPages;
//namespace lotus_blue.Controllers
//{
//    [ApiController]
//    [Route("MobileAPI")] // Empty string specifies the base route for the controller
//    [EnableCors("AllowAllOrigins")] // Apply the CORS policy to this controller
//    public class ApiForMobileController : Controller
//    {


//        private readonly ApplicationDbContext _context;
//        private readonly UserManager<ApplicationUser> _userManager;
//        private readonly DecimalFormattingService _decimalFormattingService;
//        private readonly CurrencyExchangeService _currencyExchangeService;
//        private readonly FinancialService _financialService;
//        private readonly IConfiguration _configuration;
//        private readonly UserContextService _userContextService;
//        private readonly GetCurrentTimeInIstanbul _timeService;
//        private readonly DeliveryCompanyService _deliveryCompanyService;
//        private readonly IHubContext<OrderHub> _hubContext;
//        private readonly DynamicCommon _dynamicCommon;
//        private readonly RESTAPI _restApi;
//        private readonly RoleAuthorizationService _roleAuthService;
//        private readonly FileUploadService _fileUploadService;
//        private readonly PdfReportGenerator _reportGenerator;
//        private readonly PdfReportGeneratorShipmentInvoice _pdfReportGeneratorShipmentInvoice;
//        private readonly DataCacheService _dataCacheService;


//        public ApiForMobileController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, DecimalFormattingService decimalFormattingService, CurrencyExchangeService currencyExchangeService, FinancialService financialService, IConfiguration configuration, UserContextService userContextService, GetCurrentTimeInIstanbul timeService, DeliveryCompanyService deliveryCompanyService, IHubContext<OrderHub> hubContext, DynamicCommon dynamicCommon, RoleAuthorizationService roleAuthorizationService, FileUploadService fileUploadService, PdfReportGenerator reportGenerator, PdfReportGeneratorShipmentInvoice pdfReportGeneratorShipmentInvoice, DataCacheService dataCacheService)
//        {
//            _context = context;
//            _userManager = userManager;
//            _decimalFormattingService = decimalFormattingService;
//            _currencyExchangeService = currencyExchangeService;
//            _financialService = financialService;
//            _configuration = configuration;
//            _userContextService = userContextService;
//            _timeService = timeService;
//            _deliveryCompanyService = deliveryCompanyService;
//            _hubContext = hubContext;
//            _dynamicCommon = dynamicCommon;
//            _roleAuthService = roleAuthorizationService;
//            _fileUploadService = fileUploadService;
//            _reportGenerator = reportGenerator;
//            _pdfReportGeneratorShipmentInvoice = pdfReportGeneratorShipmentInvoice;
//            _dataCacheService = dataCacheService;
//        }
//        public ActionResult CustomJsonResponse(object data, string message = "Success", int statusCode = 200)
//        {
//            var response = new
//            {
//                StatusCode = statusCode,
//                Message = message,
//                Data = data
//            };
//            return Json(response);
//        }

//        public IActionResult Index()
//        {
//            return View();
//        }

//        // log in page 
//        [HttpPost("/login")]
//        public async Task<IActionResult> Login([FromBody] EmailPasswordLoginModel model)
//        {
//            var user = await _userManager.FindByEmailAsync(model.Email);
//            if (user != null && await _userManager.CheckPasswordAsync(user, model.Password))
//            {
//                JwtToken jwtToken = new JwtToken(); // Create an instance of JwtToken
//                var token = await jwtToken.GenerateJwtToken(user, _userManager, _configuration);
//                return Ok(new
//                {
//                    statusCode = 200,
//                    message = "Succeeded",
//                    token,
//                    expiration = DateTime.Now.AddHours(6)
//                });
//            }
//            return Unauthorized(new ApiResult(401, "Unauthorized access"));
//        }


//        // عرض اسم السمتخدم في صفحة تسجيل الدخول 
//        [HttpGet("/getusername")]
//        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]

//        public async Task<IActionResult> GetUserName()
//        {
//            var userId = _userContextService.GetCurrentUserId();

//            if (string.IsNullOrEmpty(userId))
//            {
//                return BadRequest(new ApiResult(400, "User ID is null or empty"));
//            }

//            // Assuming GetUserByIdAsync returns a User object with a Username property
//            var user = await _context.Users
//                .Where(u => u.Id == userId)
//                .Select(u => new { Username = u.Name })
//                .FirstOrDefaultAsync();
//            if (user == null)
//            {
//                return NotFound(new ApiResult(404, "User not found"));
//            }

//            if (string.IsNullOrEmpty(user.Username))
//            {
//                return Ok(new { Username = "N/A" }); // Or return an empty string instead of "N/A"
//            }

//            return Ok(new { Username = user.Username });
//        }


//        // drop down for all roles in system main page 
//        // موجوداتي للأدمن في الصفحة الرئيسية وحساباتي للادمن
//        [HttpGet("/home/existingdropdown")]
//        [Authorize(Roles = "Admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> OrderByCountry()
//        {
//            var claimsIdentity = User.Identity as ClaimsIdentity;
//            if (claimsIdentity == null)
//            {
//                return CustomJsonResponse(null, "Unauthorized", 401);
//            }



//            try
//            {
//                IQueryable<Order> ordersQuery = _context.Orders
//                    .Where(o => o.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد)
//                    .AsNoTracking();


//                var intermediateResults = await ordersQuery
//                    .GroupBy(o => o.Country)
//                    .Select(group => new
//                    {
//                        Country = group.Key,
//                        TotalPrice = group.Sum(o => o.TotalPrice)
//                    })
//                    .ToListAsync();

//                var ordersByCountry = intermediateResults.Select(g => new OrderByCountryViewModel
//                {
//                    SelectedCountry = g.Country.ToString(),
//                    Currency = Common.GetCurrencyByCountryName(g.Country.ToString()),
//                    TotalPrice =_decimalFormattingService.DecimalFormat( g.TotalPrice),
//                    TotalPirceDollar = DecimalFormattingService.FormatDecimal(_currencyExchangeService.ConvertToUSD(g.TotalPrice, g.Country.ToString())), // Adjusted call to static method
//                    TotalPirceTl = DecimalFormattingService.FormatDecimal(_currencyExchangeService.ConvertToTurkishLira(_currencyExchangeService.ConvertToUSD(g.TotalPrice, g.Country.ToString()))) // Adjusted call to static method
//                }).ToList();

//                var totalSumInDollars = ordersByCountry.Sum(o => decimal.Parse(o.TotalPirceDollar));
//                var totalSumInTL = ordersByCountry.Sum(o => decimal.Parse(o.TotalPirceTl));

//                // Adjusted the response to include both the detailed list and total sums
//                var responseData = new
//                {
//                    OrdersByCountry = ordersByCountry, // include the detailed list
//                    TotalSumInDollars = DecimalFormattingService.FormatDecimal(totalSumInDollars),
//                    TotalSumInTL = DecimalFormattingService.FormatDecimal(totalSumInTL)
//                };

//                return CustomJsonResponse(responseData, "Data retrieved successfully", 200);
//            }
//            catch (Exception ex)
//            {
//                // Log the exception details as necessary
//                return CustomJsonResponse(null, $"An error occurred: {ex.Message}", 500);
//            }
//        }


//        // حسابات المتاجر للأدمن في الصفحة الرئيسية وحساباتي للادمن
//        [HttpGet("/home/mystoredropdown")]
//        [Authorize(Roles = "Admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> OrderByManfacturingCompanyOnGoing([FromQuery] int? deliveryCompanyId, Common.Countries? selectedCountry = null)
//        {
//            var userId = _userContextService.GetCurrentUserId();


//            // Ensure you are awaiting the asynchronous call
//            var viewModel = await _financialService.GetFinancialManufacturingCompanyDataOnGoing(userId, true, selectedCountry, deliveryCompanyId);

//            // Pass the result to the view
//            return Json(viewModel);
//        }



//        // مدفوعاتي في الصفحة الرئيسية لشركة التوصيل والمندوب
//        [HttpGet("/home/mypaymentsdropdown")]
//        [Authorize(Roles = "DeliveryCompany,DeliveryRepresentative", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> PaidAccountsDeliveryCompany()
//        {
//            var userId = _userContextService.GetCurrentUserId();
//            var role = _userContextService.GetUserRole(); // Now just getting a single role
//            var claimsIdentity = User.Identity as ClaimsIdentity;
//            if (claimsIdentity == null)
//            {
//                return Unauthorized();
//            }

//            try
//            {
//                var viewModel = _financialService.GetFinancialDeliveryCompanyDataPaid(userId, false); // Always pass false

//                if (viewModel == null || !viewModel.Any())
//                {
//                    // Returning a "Not Found" response with status code 404
//                    return Json(new { });
//                }

//                // Serialize the viewModel before passing it to CustomJsonResponse
//                var serializedData = viewModel.Select(vm => new
//                {
//                    vm.Currency,
//                    PaidOrdersDeliveryCompanyPrice = vm.PaidOrdersDeliveryCompanyPrice,
//                    PaidOrdersDeliveryCompanyPriceDollar = vm.PaidOrdersDeliveryCompanyPriceDollar,

//                }).ToList();

//                return Json(serializedData);
//            }
//            catch (Exception ex)
//            {
//                // Log the exception details as necessary
//                return StatusCode(500, $"An error occurred: {ex.Message}");
//            }
//        }





//        // أخر 10 عمليات للأدمن وشركات التوصيل 
//        [HttpGet("/home/last10reportsdropdown")]
//        [Authorize(Roles = "Admin,DeliveryRepresentative,DeliveryCompany", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> GetOrderReports()
//        {
//            var userId = _userContextService.GetCurrentUserId(); // Using the consistent pattern for user ID retrieval
//            bool isAdmin = User.IsInRole("Admin");

//            IQueryable<OrderReport> query = _context.OrderReports
//                .Where(a => a.DeliveryCompanyId != null)
//                .Include(or => or.DeliveryCompany)
//                .Where(or => or.DeliveryCompany.UserId == userId || isAdmin) // Filter based on user role
//                .OrderByDescending(or => or.GeneratedTime); // Order by GeneratedTime descending

//            var orderReports = await query.Take(10).ToListAsync(); // Take only the last 10 records

//            // Assuming GetFinancialDeliveryCompanydeferredTotalPrice method is correctly implemented to fetch deferred prices
//            var deferredPrices = _financialService.GetFinancialDeliveryCompanydeferredTotalPrice(userId, isAdmin);

//            var combinedReports = orderReports.Select(or =>
//            {
//                var deliveryCompanyPriceInfo = deferredPrices.FirstOrDefault(dp => dp.DeliveryCompanyId == or.DeliveryCompany.Id);
//                var deferredTotalPrice = isAdmin
//                    ? deliveryCompanyPriceInfo?.DeferredDifference ?? "N/A"
//                    : deliveryCompanyPriceInfo?.DeliveryPrice ?? "N/A";

//                return new
//                {
//                    id = or.Id.ToString(),
//                    generatedTime = or.GeneratedTime.ToString("yyyy-MM-dd"),
//                    totalAmount = $"{Common.GetCurrencyByCountryName(or.Country.ToString())} {DecimalFormattingService.FormatDecimal(or.TotalAmount)}",
//                    country = or.Country.ToString(),
//                    deliveryCompanyName = or.DeliveryCompany.Name,
//                    deferredTotalPrice,
//                    currency = deliveryCompanyPriceInfo?.Currency ?? "N/A"
//                };
//            }).ToList();

//            return Json(combinedReports);
//        }

//        //حساباتي لشركات التوصيل والمندوبين
//        [HttpGet("/home/myfinancialdropdown")]
//        [Authorize(Roles = "DeliveryRepresentative,DeliveryCompany", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> OnGoingAccountDeliveryCompany()
//        {
//            var userId = _userContextService.GetCurrentUserId(); // Ensuring consistency in user ID retrieval

//            // Fetching ongoing account data for the delivery company
//            var viewModel = _financialService.GetFinancialDeliveryCompanyDataOnGoingOnly(userId, false);

//            if (viewModel != null && viewModel.Any())
//            {
//                // Selecting only the necessary fields before returning
//                var formattedViewModel = viewModel.Select(item => new
//                {
//                    DeliveryCompanyName = item.DeliveryCompany.Name,
//                    Currency = item.Currency,
//                    OnGoingAccountDeliveryCompanyPrice = item.OnGoingAccountDeliveryCompanyPrice,
//                    OnGoingAccountDeliveryCompanyPriceDollar = item.OnGoingAccountDeliveryCompanyPriceDollar
//                }).ToList();

//                return Json(formattedViewModel);
//            }
//            else
//            {
//                return Json(new { message = "No ongoing accounts found" });
//            }
//        }


//        // مدفوعاتي للموظفين 
//        [HttpGet("/home/paiddropdown")]
//        [Authorize(Roles = "CallCenter,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> ExistingsForEmployess()
//        {
//            var userId = _userContextService.GetCurrentUserId(); // Ensuring consistency in user ID retrieval

//            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.ApplicationUserId == userId);

//            if (employee == null)
//            {
//                return Json(new { message = "Employee not found" });
//            }

//            // Calculate months worked
//            int monthsWorked = ((DateTime.Now.Year - employee.DateAdded.Year) * 12) + DateTime.Now.Month - employee.DateAdded.Month;

//            // Calculate total earnings
//            decimal totalEarned = monthsWorked * employee.Salary;
//            decimal totalEarnedUSD = _currencyExchangeService.ConvertToUSD(totalEarned, "تركيا");

//            // Format the earnings using DecimalFormattingService
//            string formattedTotalEarned = _decimalFormattingService.DecimalFormat(totalEarned);
//            string formattedTotalEarnedUSD = _decimalFormattingService.DecimalFormat(totalEarnedUSD);

//            var totalEarnings = new
//            {
//                TotalEarned = formattedTotalEarned,
//                TotalEarnedUSD = formattedTotalEarnedUSD
//            };

//            return Json(totalEarnings);
//        }


//        //اخر 10 عمليات للموظفين
//        [HttpGet("/home/last10employeetransactionsdropdown")]
//        [Authorize(Roles = "CallCenter,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> Last10EmployeeTransactions()
//        {
//            // Get the current user's ID
//            var currentUserId = _userContextService.GetCurrentUserId(); // Assuming you have a consistent service for user ID retrieval.

//            // Find the employee based on the current user's ID
//            var employee = await _context.Employees
//                                         .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

//            if (employee == null)
//            {
//                return Json(new { Message = "Employee not found." });
//            }

//            // Query the last 10 transactions for this employee
//            var transactions = await _context.EmployeeTransactions
//                                             .Where(t => t.EmployeeId == employee.Id)
//                                             .OrderByDescending(t => t.Date)
//                                             .Take(10)
//                                             .Select(t => new
//                                             {
//                                                 Amount = _decimalFormattingService.DecimalFormat(t.Amount), // Assuming you want to format the amount
//                                                 TransactionType = t.TransactionType.ToString(),
//                                                 t.Reason,
//                                                 Date = t.Date.ToString("yyyy-MM-dd")
//                                             })
//                                             .ToListAsync();

//            return Json(transactions);
//        }

//        //حساباتي للموظفين 
//        [HttpGet("/home/myfinancialemployeedropdown")]
//        [Authorize(Roles = "CallCenter,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> CalculateAdjustedSalary()
//        {
//            var currentUserId = _userContextService.GetCurrentUserId(); // Using the consistent method for user ID retrieval.

//            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);
//            if (employee == null)
//            {
//                return Json(new { Message = "Employee not found." });
//            }

//            decimal baseSalary = employee.Salary;
//            decimal baseSalaryUsd = _currencyExchangeService.ConvertToUSD(baseSalary, "تركيا");
//            var now = DateTime.Now;
//            var transactions = await _context.EmployeeTransactions
//                                              .Where(t => t.EmployeeId == employee.Id &&
//                                                          t.Date.Month == now.Month &&
//                                                          t.Date.Year == now.Year)
//                                              .Select(t => new
//                                              {
//                                                  t.Amount,
//                                                  t.TransactionType,
//                                                  AmountUSD = _currencyExchangeService.ConvertToUSD(t.Amount, "تركيا"),
//                                              })
//                                              .ToListAsync();

//            decimal adjustedSalary = baseSalary + transactions.Sum(t =>
//                t.TransactionType == TransactionTypeEnum.مكافأة ? t.Amount :
//                (t.TransactionType == TransactionTypeEnum.خصم || t.TransactionType == TransactionTypeEnum.سلفة) ? -t.Amount :
//                0);

//            decimal adjustedSalaryUSD = _currencyExchangeService.ConvertToUSD(adjustedSalary, "تركيا");


//            string formattedbaseSalary = _decimalFormattingService.DecimalFormat(baseSalary);
//            string formattedbaseSalaryUsd = _decimalFormattingService.DecimalFormat(baseSalaryUsd);

//            string formattedAdjustedSalary = _decimalFormattingService.DecimalFormat(adjustedSalary);
//            string formattedAdjustedSalaryUSD = _decimalFormattingService.DecimalFormat(adjustedSalaryUSD);

//            // Creating anonymous objects for transactions with formatted amount, amount in USD, and transaction type
//            var formattedTransactions = transactions.Select(t => new
//            {
//                Amount = _decimalFormattingService.DecimalFormat(t.Amount),
//                AmountUSD = _decimalFormattingService.DecimalFormat(t.AmountUSD),
//                TransactionType = t.TransactionType // Assuming TransactionType is an enum
//            });

//            return Json(new
//            {
//                baseSalary = formattedbaseSalary,
//                baseSalaryUsd = formattedbaseSalaryUsd,
//                AdjustedSalary = formattedAdjustedSalary,
//                AdjustedSalaryUsd = formattedAdjustedSalaryUSD,
//                Transactions = formattedTransactions,
//                Commissions = 0, // You can add bonuses here if applicable
//                CommissionsUsd = 0, // You can add bonuses here if applicable

//            });
//        }


//        // finish the  drop down for all roles in system main page 


//        // قسم الطلبات  


//        // get all orders 
//        [HttpGet("/orders/getall")]
//        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]

//        public async Task<IActionResult> GetOrders([FromQuery] int page = 1, int pageSize = 50, int? orderId = null)
//        {
//            var userId = _userContextService.GetCurrentUserId();
//            var role = _userContextService.GetUserRole();
//            try
//            {
//                // Define the query to retrieve orders
//                var query = _context.Orders
//                    .Include(x => x.ManufacturingCompany)
//                    .Include(x => x.DeliveryCompany)
//                    .OrderByDescending(o => o.LastEditedDate)
//                    .AsQueryable();

//                // Apply filter by order ID if provided
//                if (orderId.HasValue)
//                {
//                    query = query.Where(o => o.Id == orderId.Value);
//                }

//                // Apply additional filtering based on user role
//                if (role == "DeliveryRepresentative" || role == "DeliveryCompany")
//                {
//                    query = query.Where(o => o.DeliveryCompany.UserId == userId);
//                }

//                // Calculate total number of items
//                var totalItems = await query.CountAsync();

//                // Apply pagination
//                query = query.Skip((page - 1) * pageSize).Take(pageSize);

//                // Retrieve orders from the database
//                var orders = await query.ToListAsync();

//                // Convert orders to the view model
//                var orderViewModels = orders.Select(o => new AppOrderListViewModel
//                {
//                    Id = o.Id,
//                    TelephoneNumber = o.TelephoneNumber,
//                    Country = o.Country,
//                    DeliveryCompanyName = o.DeliveryCompany.Name,
//                    OrderStatus = o.OrderStatus,
//                    CreatedDate = o.CreatedDate.ToString("yyyy-MM-dd")
//                }).ToList();

//                // Create a PaginationViewModel instance and populate it with data
//                var paginationViewModel = new PaginationViewModel<AppOrderListViewModel>
//                {
//                    Items = orderViewModels,
//                    CurrentPage = page,
//                    PageSize = pageSize,
//                    TotalItems = totalItems
//                };


//                // Return the pagination view model as JSON
//                return Ok(paginationViewModel);
//            }
//            catch (Exception ex)
//            {
//                // Log the error
//                Console.WriteLine($"An error occurred while retrieving orders: {ex.Message}");
//                return StatusCode(500, "An error occurred while processing your request.");
//            }
//        }


//        [HttpPost("/orders/create")]
//        [Authorize(Roles = "Admin,CallCenter,FollowUpDepartment,ExecutiveDirector", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> CreateOrder([FromBody] AppOrderCreateViewModel viewModel)
//        {
//            // Check if the model state is valid
//            if (!ModelState.IsValid)
//            {
//                return BadRequest(ModelState);
//            }

//            var userId = _userContextService.GetCurrentUserId();
//            var currentDate = _timeService.GetIstanbulTimeWithOffset();
//            var createdDate = viewModel.CreatedDate;
//            var hoursDifference = (createdDate - currentDate).TotalHours;


//            // Mapping viewModel to Order entity
//            var order = new Order
//            {
//                Country = viewModel.Country,
//                State = viewModel.State,
//                OrderSource = viewModel.OrderSource,
//                SourceName = viewModel.SourceName,
//                ManufacturingCompanyId = viewModel.ManufacturingCompanyId,
//                DeliveryCompanyId = viewModel.DeliveryCompanyId,
//                TelephoneNumber = viewModel.TelephoneNumber,
//                SecondTelephoneNumber = viewModel.SecondTelephoneNumber,
//                CustomerName = viewModel.CustomerName,
//                Notes = viewModel.Notes,
//                Gender = viewModel.Gender,
//                CreatedDate = viewModel.CreatedDate,
//                Address = viewModel.Address,
//                TotalPrice = viewModel.TotalPrice,
//                ApplicationUserId = userId,
//                LastEditedDate = _timeService.GetIstanbulTimeWithOffset(),
//                InstantAddedDate = _timeService.GetIstanbulTimeWithOffset(),

//            };

//            // Check if the created date is in the future and not today
//            if (hoursDifference > 48)
//            {
//                order.OrderStatus = OrderStatusEnum.الطلبات_المؤجلة;
//            }
//            else if (hoursDifference <= 48 && createdDate > currentDate)
//            {
//                order.OrderStatus = OrderStatusEnum.طلب_جديد;
//            }



//            try
//            {
//                _context.Add(order); // Add the order to the context
//                await _context.SaveChangesAsync();

//                // Additional business logic as per your existing code

//                foreach (var warehouseAmount in viewModel.SelectedWarehouses)
//                {
//                    var warehouse = await _context.Warehouses.FindAsync(warehouseAmount.WarehouseId);
//                    if (warehouse != null)
//                    {
//                        warehouse.Amount -= warehouseAmount.Amount;
//                        _context.Update(warehouse);

//                        var orderWarehouse = new OrderWarehouse
//                        {
//                            WarehouseId = warehouse.Id,
//                            OrderId = order.Id,
//                            Amount = warehouseAmount.Amount
//                        };
//                        _context.OrderWarehouses.Add(orderWarehouse);  // Add directly to the context
//                    }
//                }

//                var orderHistory = new OrderStatusHistory
//                {
//                    CreatedAt = _timeService.GetIstanbulTimeWithOffset(),
//                    Status = order.OrderStatus, // Use the same status as the order
//                    ApplicationUserId = userId, // Use the same user ID as the order
//                    OrderId = order.Id          // Link to the newly created order
//                };

//                _context.OrderStatusHistories.Add(orderHistory); // Add the order history to the context


//                await _context.SaveChangesAsync();

//                // singla r data
//                var settings = new JsonSerializerSettings
//                {
//                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore // or PreserveReferencesHandling.Objects
//                };

//                var userNamesForOrders = await _deliveryCompanyService.GetUserNameForOrderIdAsync(order.Id);
//                var deliveryCompanyPrice =await  _deliveryCompanyService.GetDeliveryCompanyPriceForOrderIdAsync(order.Id);
//                var manufacturingCompanyName = await _dynamicCommon.GetManufacturingCompanyNameByOrderIdAsync(order.Id);
//                var deliverycompanyname = await _dynamicCommon.GetDeliveryCompanyNameByOrderIdAsync(order.Id);
//                var manufacturingCompanyimage = await _dynamicCommon.GetManufacturingCompanyImageByOrderIdAsync(order.Id);
//                var deliverycompanyimage = await _dynamicCommon.GetDeliveryCompanyImageByOrderIdAsync(order.Id);
//                var orders = new
//                {
//                    Order = order,
//                    Username = userNamesForOrders, // Add other necessary data here
//                    DeliveryComapnyPrice = deliveryCompanyPrice,
//                    ManufacturingCompanyName = manufacturingCompanyName,
//                    Deliverycompanyname = deliverycompanyname,
//                    Deliverycompanyimage = deliverycompanyimage,
//                    ManufacturingCompanyimage = manufacturingCompanyimage
//                };
//                var orderJson = JsonConvert.SerializeObject(orders, settings);
//                await _hubContext.Clients.Group("UsersExpectDelivery").SendAsync("ReceiveOrderUpdate", orderJson);
//                var deliveryCompanyGroup = $"deliveryCompany_{order.DeliveryCompanyId}";
//                await _hubContext.Clients.Group(deliveryCompanyGroup).SendAsync("ReceiveOrderUpdate", orderJson);
//                await _hubContext.Clients.Group("UsersExpectDelivery").SendAsync("NewOrder", orderJson);




//                return Ok(new { orderId = order.Id }); // Return the ID of the created order
//            }

//            catch (Exception ex)
//            {
//                // Log the exception
//                return StatusCode(500, "An error occurred while creating the order.");
//            }
//        }


//        // get single order 
//        [HttpGet("/orders/details/{id}")]
//        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> OrderDetails(int? id)
//        {
//            AppOrderDetails appOrderDetails = null; // Initialize as null
//            List<OrderStatusHistoryModel> orderHistories;

//            string cancelReasonForCancellation, cancelReasonForPostponed, cancelReasonForDeliveryFailure;

//            if (id == null)
//            {
//                return NotFound();
//            }
//            var UserId = _userContextService.GetCurrentUserId();
//            var role = _userContextService.GetUserRole();

//            var OrderQuery = _context.Orders.AsQueryable();

//            var orderEditHistoryIds = await _context.OrderEditHistories
//                .Where(eh => eh.OrderId == id)
//                .Select(eh => eh.EditNumber)
//                .ToListAsync();


//            var order = await OrderQuery
//                .Include(o => o.ManufacturingCompany)
//                //  .Include(o=>o.OrderEditHistories)
//                .Include(o => o.DeliveryCompany)
//                .Include(o => o.OrderWarehouses)
//                .ThenInclude(ow => ow.Warehouse)
//                .FirstOrDefaultAsync(m => m.Id == id);

//            if (role == "DeliveryCompany" || role == "DeliveryRepresentative")
//            {
//                if (order.DeliveryCompany.UserId != UserId)
//                {
//                    return Forbid(); // Returns a 403 Forbidden response
//                }
//            }


//            if (order == null)
//            {
//                return NotFound();
//            }


//            var Allemployee = _context.Employees.AsQueryable();


//            var employee = Allemployee
//            .FirstOrDefault(e => e.ApplicationUserId == order.ApplicationUserId);

//            var employeeList = Allemployee
//                .Where(a => a.IsShown)
//                .Select(e => new EmployeNameAndId
//                {
//                    // Assuming EmployeNameAndId has properties like Id and Name
//                    Id = e.ApplicationUserId,
//                    Name = e.Name
//                })
//                .ToList();



//            var currentUser = await _userManager.GetUserAsync(User);

//            var deliveryCost = (from o in OrderQuery
//                                where o.Id == id
//                                join dcp in _context.DeliveryCompanyPrices
//                                on new { CompanyId = o.DeliveryCompanyId, o.State } equals new { CompanyId = dcp.DeliveryCompanyId, State = dcp.City }
//                                select dcp.Price)
//                     .FirstOrDefault();



//            var entries = OrderQuery
//                 .Count(o => o.TelephoneNumber == order.TelephoneNumber
//                  || o.SourceName == order.SourceName);



//            var warehouseInfo = order.OrderWarehouses
//                .Select(ow => new AppWarehouseAmountViewModel
//                {
//                    WarehouseId = ow.WarehouseId,
//                    WarehouseName = ow.Warehouse.Name,
//                    Amount = ow.Amount,
//                    WarehouseLogo = ow.Warehouse.MainWarehouse.ImageUrl,
//                }).ToList();


//            orderHistories = await _context.OrderStatusHistories
//                  .Where(oh => oh.OrderId == id)
//                                  .Include(oh => oh.User) // Include the User navigation property

//                .OrderByDescending(oh => oh.Id)
//                 .Select(oh => new OrderStatusHistoryModel
//                 {
//                     CreatedAt = oh.CreatedAt,
//                     Status = oh.Status,
//                     Reason = oh.Reason,
//                     UserName = oh.User.Name // or appropriate property
//                 })
//                 .ToListAsync();

//            cancelReasonForCancellation = orderHistories
//           .Where(oh => oh.Status == OrderStatusEnum.تم_الإلغاء)
//           .Select(oh => oh.Reason)
//           .FirstOrDefault();



//            cancelReasonForDeliveryFailure = orderHistories
//               .Where(oh => oh.Status == OrderStatusEnum.فشل_التسليم)
//               .Select(oh => oh.Reason)
//               .FirstOrDefault(); // Retrieves CancelReason for status فشل التسليم


//            var lastEditHistory = await _context.OrderStatusHistories
//            .Where(oh => oh.OrderId == id)
//            .Include(oh => oh.User) // Include the User navigation property
//            .OrderByDescending(oh => oh.CreatedAt) // Order by CreatedAt in descending order
//            .FirstOrDefaultAsync(); // Get the first (most recent) item or null if none found



//            appOrderDetails = new AppOrderDetails
//            {
//                Id = order.Id,
//                Country = order.Country,
//                State = order.State,
//                OrderSource = order.OrderSource,
//                SourceName = order.SourceName,
//                TelephoneNumber = order.TelephoneNumber,
//                SecondTelephoneNumber = order.SecondTelephoneNumber,
//                CustomerName = order.CustomerName,
//                Notes = order.Notes,
//                Address = order.Address,
//                LastEditedDate = order.LastEditedDate.ToString(),
//                OrderStatus = order.OrderStatus,
//                TotalPrice = order.TotalPrice,
//                ManufacturingCompany = new AppCompanyViewModel
//                {
//                    Id = order.ManufacturingCompany.Id,
//                    Name = order.ManufacturingCompany.Name,
//                    LogoUrl = order.ManufacturingCompany.ImageUrl
//                },
//                DeliveryCompany = new AppCompanyViewModel
//                {
//                    Id = order.DeliveryCompany.Id,
//                    Name = order.DeliveryCompany.Name,
//                    LogoUrl = order.DeliveryCompany.ImageUrl
//                },
//                DeliveryCost = deliveryCost,
//                NumberOfEntries = entries,
//                SelectedWarehouses = warehouseInfo,
//                CancelReasonForCancellation = cancelReasonForCancellation,
//                CancelReasonForDeliveryFailure = cancelReasonForDeliveryFailure,
//                CreatedBy = employee?.Name ?? "Unknown",
//                FromComments = order.FromComments,
//                OrderEditHistoryIds = orderEditHistoryIds,
//                EmployeeImage = employee.ImageUrl ?? "static/DefaultImage.svg",
//                RemainingPrice = (order.TotalPrice - deliveryCost),
//                Currency = Common.GetCurrencyByCountryName(order.Country.ToString()),
//            };


//            if (lastEditHistory != null && lastEditHistory.User != null && lastEditHistory.User.Name != null)
//            {
//                appOrderDetails.LastEditedBy = lastEditHistory.User.Name;
//                appOrderDetails.lastEditedByImage = (await _dynamicCommon.GetEmployeeImageByNameAsync(lastEditHistory.User.Id)) ?? "static/DefaultImage.svg";


//            }
//            else
//            {
//                appOrderDetails.LastEditedBy = "تحديث تلقائي";
//            }



//            // Handle other roles or scenarios here
//            return Json(appOrderDetails);

//        }

//        // update order status 
//        [HttpPost("/order/updatestatus/{id}")]
//        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> UpdateStatus(int id, [FromQuery] OrderStatusEnum orderStatus, string? reason = null)
//        {
//            var order = await _context.Orders
//                .Include(o => o.OrderWarehouses)
//                .FirstOrDefaultAsync(o => o.Id == id);

//            if (order == null)
//            {
//                return NotFound(new { success = false, message = "Order not found." });
//            }

//            var userId = _userContextService.GetCurrentUserId();
//            var roleName = _userContextService.GetUserRole();

//            if (!_roleAuthService.CanUpdateStatus(roleName, orderStatus))
//            {
//                return Json(new { success = true, message = "تم تغيير حالة الطلب بنجاح", orderId = id });
//            }

//            var currentStatus = order.OrderStatus;

//            if (currentStatus != OrderStatusEnum.أرشيف_المرجع)
//            {
//                if (!_roleAuthService.CanUpdateStatus(roleName, orderStatus))
//                {
//                    return BadRequest(new { success = false, message = "الحالة المطلوبة غير متاحة للتغيير إليها من الحالة الحالية." });
//                }
//            }

//            if (currentStatus == OrderStatusEnum.فشل_التسليم ||
//                currentStatus == OrderStatusEnum.الطلبات_المرجعة ||
//                currentStatus == OrderStatusEnum.انتظار_المعالجة)
//            {
//                if (orderStatus == OrderStatusEnum.تم_المعالجة ||
//                    orderStatus == OrderStatusEnum.الطلبات_المؤجلة)
//                {
//                    order.ApplicationUserId = userId;
//                    order.FixedOrderDate = _timeService.GetIstanbulTimeWithOffset();
//                }
//            }

//            order.OrderStatus = orderStatus;
//            order.LastEditedDate = _timeService.GetIstanbulTimeWithOffset();

//            var orderHistory = new OrderStatusHistory
//            {
//                OrderId = order.Id,
//                Status = orderStatus,
//                CreatedAt = _timeService.GetIstanbulTimeWithOffset(),
//                ApplicationUserId = userId
//            };

//            if (orderStatus == OrderStatusEnum.فشل_التسليم || orderStatus == OrderStatusEnum.تم_الإلغاء)
//            {
//                orderHistory.Reason = reason;
//            }

//            _context.OrderStatusHistories.Add(orderHistory);

//            if (currentStatus == OrderStatusEnum.فشل_التسليم &&
//                orderStatus != OrderStatusEnum.فشل_التسليم &&
//                orderStatus != OrderStatusEnum.الطلبات_المرجعة &&
//                orderStatus != OrderStatusEnum.انتظار_المعالجة)
//            {
//                foreach (var orderWarehouse in order.OrderWarehouses)
//                {
//                    var warehouse = orderWarehouse.Warehouse;
//                    warehouse.Amount -= orderWarehouse.Amount;
//                    _context.Update(warehouse);
//                }
//            }

//            if (OrderStatusHelper.ShouldRefundToWarehouse(orderStatus) && currentStatus != OrderStatusEnum.فشل_التسليم)
//            {
//                foreach (var orderWarehouse in order.OrderWarehouses)
//                {
//                    var warehouse = orderWarehouse.Warehouse;
//                    warehouse.Amount += orderWarehouse.Amount;
//                    _context.Update(warehouse);
//                }
//            }

//            await _context.SaveChangesAsync();

//            if (orderStatus == OrderStatusEnum.فشل_التسليم)
//            {
//                Console.WriteLine("Sending notification to all clients...");
//                await _hubContext.Clients.All.SendAsync("ReceiveOrderStatusUpdate", OrderStatusEnum.فشل_التسليم);
//            }

//            var externalOrderId = order.ExternalOrderId;

//            if (externalOrderId.HasValue)
//            {
//                var updateStatusRequest = new UpdateStatusRequest
//                {
//                    NewStatus = orderStatus,
//                };

//                var response = await _restApi.UpdateOrderStatusAsync(externalOrderId.Value, updateStatusRequest);
//                Console.WriteLine(externalOrderId);
//                Console.WriteLine(response);
//            }

//            return Ok(new { success = true, message = "تم تغيير حالة الطلب بنجاح", orderId = id });
//        }




//        //[HttpPut("/orders/edit/{id}")]
//        //[Authorize(Roles = "Admin,CallCenter,FollowUpDepartment,ExecutiveDirector", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        //public async Task<IActionResult> Edit(int id, AppOrderEditViewModel viewModel)
//        //{

//        //    var existingOrder = await _context.Orders
//        //        .Include(o => o.OrderWarehouses)
//        //        .FirstOrDefaultAsync(o => o.Id == id);

//        //    if (existingOrder == null)
//        //    {
//        //        return NotFound();
//        //    }




//        //    var orderStatusHistory = new OrderStatusHistory
//        //    {
//        //        OrderId = existingOrder.Id,
//        //        Status = existingOrder.OrderStatus,
//        //        CreatedAt = _timeService.GetIstanbulTimeWithOffset(),
//        //        ApplicationUserId = _userContextService.GetCurrentUserId() // Assuming _userManager is available in your context
//        //    };

//        //    _context.OrderStatusHistories.Add(orderStatusHistory);


//        //    var currentDate = _timeService.GetIstanbulTimeWithOffset();
//        //    var createdDate = viewModel.CreatedDate;
//        //    var totalDays = (createdDate - currentDate).TotalDays;

//        //    // Check if the created date is more than 48 hours from now
//        //    if (totalDays > 2)
//        //    {
//        //        existingOrder.OrderStatus = OrderStatusEnum.الطلبات_المؤجلة; // Deferred Orders
//        //    }
//        //    else
//        //    {
//        //        existingOrder.OrderStatus = OrderStatusEnum.طلب_جديد; // New Order
//        //    }


//        //    // Get last edit number
//        //    var lastEditNumber = await _context.OrderEditHistories
//        //        .Where(eh => eh.OrderId == id)
//        //        .Select(eh => eh.EditNumber)
//        //        .OrderByDescending(eh => eh)
//        //        .FirstOrDefaultAsync();

//        //    // Create a new instance of OrderEditHistory
//        //    var orderHistory = new OrderEditHistory
//        //    {
//        //        OrderId = existingOrder.Id,
//        //        Country = existingOrder.Country,
//        //        State = existingOrder.State,
//        //        OrderSource = existingOrder.OrderSource,
//        //        SourceName = existingOrder.SourceName,
//        //        ManufacturingCompanyId = existingOrder.ManufacturingCompanyId,
//        //        TelephoneNumber = existingOrder.TelephoneNumber,
//        //        DeliveryCompanyId = existingOrder.DeliveryCompanyId,
//        //        SecondTelephoneNumber = existingOrder.SecondTelephoneNumber,
//        //        CustomerName = existingOrder.CustomerName,
//        //        Notes = existingOrder.Notes,
//        //        Address = existingOrder.Address,
//        //        CreatedDate = existingOrder.CreatedDate,
//        //        LastEditedDate = existingOrder.LastEditedDate,
//        //        FixedOrderDate = existingOrder.FixedOrderDate,
//        //        InstantAddedDate = existingOrder.InstantAddedDate,
//        //        TotalPrice = existingOrder.TotalPrice,
//        //        ExternalOrderId = existingOrder.ExternalOrderId,
//        //        ApplicationUserId = existingOrder.ApplicationUserId,
//        //        FromComments = existingOrder.FromComments,
//        //        Gender = existingOrder.Gender,
//        //        EditNumber = lastEditNumber + 1
//        //    };

//        //    // Add the new OrderEditHistory record to the context
//        //    _context.OrderEditHistories.Add(orderHistory);

//        //    // Save changes to the database
//        //    await _context.SaveChangesAsync();


//        //    existingOrder.Country = viewModel.Country;
//        //    existingOrder.ManufacturingCompanyId = viewModel.ManufacturingCompanyId;
//        //    existingOrder.State = viewModel.State;
//        //    existingOrder.OrderSource = viewModel.OrderSource;
//        //    existingOrder.SourceName = viewModel.SourceName;
//        //    existingOrder.DeliveryCompanyId = viewModel.DeliveryCompanyId;
//        //    existingOrder.TelephoneNumber = viewModel.TelephoneNumber;
//        //    existingOrder.SecondTelephoneNumber = viewModel.SecondTelephoneNumber;
//        //    existingOrder.CustomerName = viewModel.CustomerName;
//        //    existingOrder.Notes = viewModel.Notes;
//        //    existingOrder.Address = viewModel.Address;
//        //    existingOrder.LastEditedDate = _timeService.GetIstanbulTimeWithOffset();
//        //    existingOrder.TotalPrice = viewModel.TotalPrice;
//        //    existingOrder.CreatedDate = createdDate;
//        //    existingOrder.Gender = viewModel.Gender;

//        //    _context.Update(existingOrder); // Marks the entity and its navigation properties as modified
//        //    await _context.SaveChangesAsync();



//        //    // Update existingOrder warehouses
//        //    var warehouseChanges = new Dictionary<int, int>(); // Dictionary to track warehouse changes
//        //    var orderWarehouseEditHistories = new List<OrderWarehouseEditHistory>(); // List to store OrderWarehouseEditHistory

//        //    // Save existing selected warehouses to history
//        //    foreach (var orderWarehouse in existingOrder.OrderWarehouses)
//        //    {
//        //        orderWarehouseEditHistories.Add(new OrderWarehouseEditHistory
//        //        {
//        //            OrderId = existingOrder.Id,
//        //            WarehouseId = orderWarehouse.WarehouseId,
//        //            Amount = orderWarehouse.Amount,
//        //            EditDate = _timeService.GetIstanbulTimeWithOffset(),
//        //            EditNumber = lastEditNumber,
//        //            OrderEditHistoryId = orderHistory.Id // Assuming orderHistory is already created
//        //        });
//        //    }


//        //    foreach (var selectedWarehouse in viewModel.SelectedWarehouses)
//        //    {
//        //        if (selectedWarehouse.Amount <= 0)
//        //            continue;

//        //        if (!warehouseChanges.ContainsKey(selectedWarehouse.WarehouseId))
//        //        {
//        //            warehouseChanges[selectedWarehouse.WarehouseId] = selectedWarehouse.Amount;
//        //        }
//        //        else
//        //        {
//        //            warehouseChanges[selectedWarehouse.WarehouseId] += selectedWarehouse.Amount;
//        //        }
//        //    }

//        //    foreach (var orderWarehouse in existingOrder.OrderWarehouses)
//        //    {
//        //        if (warehouseChanges.ContainsKey(orderWarehouse.WarehouseId))
//        //        {
//        //            orderWarehouse.Amount = warehouseChanges[orderWarehouse.WarehouseId];
//        //            warehouseChanges.Remove(orderWarehouse.WarehouseId);
//        //        }
//        //        else
//        //        {
//        //            _context.OrderWarehouses.Remove(orderWarehouse);
//        //        }
//        //    }

//        //    foreach (var warehouseChange in warehouseChanges)
//        //    {
//        //        existingOrder.OrderWarehouses.Add(new OrderWarehouse
//        //        {
//        //            WarehouseId = warehouseChange.Key,
//        //            Amount = warehouseChange.Value
//        //        });
//        //    }

//        //    _context.OrderWarehouseEditHistories.AddRange(orderWarehouseEditHistories);
//        //    await _context.SaveChangesAsync();


//        //    return Json("edited");
//        //}


//        ////   نهاية الطلبات 



//        // قسم حساباتي 

//        // حساباتي حسب الدول 
//        [HttpGet("/finance/bycountry")]
//        [Authorize(Roles = "Admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult myfinancebycountry()
//        {
//            // Start with a base query for shown orders.
//            IQueryable<Order> query = _context.Orders
//                .Where(o => o.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد)
//                .AsNoTracking()
//                .Include(o => o.DeliveryCompany);

//            // Group the orders by country and project the required data.
//            var ordersByCountry = query
//                .GroupBy(o => o.Country)
//                .Select(group => new
//                {
//                    CountryId = (int)group.Key, // Assuming Country is an enum
//                    CountryName = group.Key.ToString(), // Convert enum value to string
//                    Currency = Common.GetCurrencyByCountryName(group.Key.ToString()),
//                    TotalPrice = _decimalFormattingService.DecimalFormat(group.Sum(o => o.TotalPrice)),
//                    TotalPriceDollar = _decimalFormattingService.DecimalFormat(_currencyExchangeService.ConvertToUSD(group.Sum(o => o.TotalPrice), group.Key.ToString())),
//                    TotalPriceTl = _decimalFormattingService.DecimalFormat(_currencyExchangeService.ConvertToTurkishLira(_currencyExchangeService.ConvertToUSD(group.Sum(o => o.TotalPrice), group.Key.ToString())))
//                })
//                .ToList();

//            // Calculate the total sum of TotalPriceDollar and TotalPriceTl for all countries.
//            var totalDollarSum = _decimalFormattingService.DecimalFormat(ordersByCountry.Sum(o => decimal.Parse(o.TotalPriceDollar)));
//            var totalTlSum = _decimalFormattingService.DecimalFormat(ordersByCountry.Sum(o => decimal.Parse(o.TotalPriceTl)));

//            // Create a new object to hold the total sums.
//            var totalSums = new
//            {
//                TotalPriceDollarSum = totalDollarSum,
//                TotalPriceTlSum = totalTlSum
//            };

//            // Add the total sums to the response.
//            var response = new
//            {
//                OrdersByCountry = ordersByCountry,
//                TotalSums = totalSums
//            };

//            return Json(response);
//        }



//        // حساباتي حسب المتجر 
//        [HttpGet("/finance/bystore")]
//        [Authorize(Roles = "Admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> myfinancebystore(int? deliveryCompanyId, Common.Countries? selectedCountry = null)
//        {
//            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Get the current user's ID

//            // Check if the user is an Admin or Accountant
//            bool isAdminOrAccountant = User.IsInRole("Admin") || User.IsInRole("Accountant");

//            // Ensure you are awaiting the asynchronous call
//            var manufacturingCompanyFinancials = await _financialService.GetFinancialManufacturingCompanyDataOnGoing(userId, isAdminOrAccountant, selectedCountry, deliveryCompanyId);

//            // Calculate the combined total prices (TL and USD)
//            decimal totalTLCombined = manufacturingCompanyFinancials.Sum(mf => decimal.Parse(mf.TotalPriceTL));
//            decimal totalUSDCombined = manufacturingCompanyFinancials.Sum(mf => decimal.Parse(mf.TotalPriceUSD));

//            // Pass the result to the view model
//            var simplifiedViewModel = new
//            {
//                ManufacturingCompanies = manufacturingCompanyFinancials.Select(mf => new
//                {
//                    ManufacturingCompany = new
//                    {
//                        mf.ManufacturingCompanyName, // Extract the manufacturing company name
//                        mf.ManufacturingCompanyLogo,
//                        mf.TotalPriceTL, // Extract the total price TL for the manufacturing company
//                        mf.TotalPriceUSD // Extract the total price USD for the manufacturing company
//                    }
//                }),
//                TotalPriceTLCombined = _decimalFormattingService.DecimalFormat(totalTLCombined), // Format combined total price TL
//                TotalPriceUSDCombined = _decimalFormattingService.DecimalFormat(totalUSDCombined) // Format combined total price USD
//            };

//            // Pass the result to the view
//            return Json(simplifiedViewModel);
//        }




//        //// حساباتي حسب المتجر لشركة التوصيل 
//        //[HttpGet("/finance/bydeliverycompany")]
//        //[Authorize(Roles = "Admin,DeliveryCompany", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        //public async Task<IActionResult> OrderByManfacturingCompanyOnGoingDeliveryCompany(int? deliveryCompanyId, Common.Countries? selectedCountry = null)
//        //{
//        //    var userId = _userContextService.GetCurrentUserId();

//        //    var role = _userContextService.GetUserRole();

//        //    // Apply additional filtering based on user role

//        //    // Check if the user is an Admin or Accountant
//        //    bool isAdminOrAccountant = role == "Admin";

//        //    // Ensure you are awaiting the asynchronous call
//        //    var viewModel = await _financialService.GetFinancialManufacturingCompanyDataOnGoingDeliveryCompany(userId, isAdminOrAccountant, selectedCountry, deliveryCompanyId);


//        //    var simplifiedViewModel = new
//        //    {
//        //        ManufacturingCompanies = viewModel.Select(mf => new
//        //        {
//        //            ManufacturingCompany = new
//        //            {
//        //                mf.ManufacturingCompany.Id,
//        //                mf.ManufacturingCompany.Name,
//        //                mf.ManufacturingCompany.Logo
//        //            },
//        //            DeliveryCompanyFinancials = mf.DeliveryCompanyFinancials.Select(dc => new
//        //            {
//        //                DeliveryCompanyId = dc.DeliveryCompanyId,
//        //                Country = dc.DeliveryCompany.SelectedCountry,
//        //                LogoUrl = dc.DeliveryCompany.LogoUrl,
//        //                Name = dc.DeliveryCompany.Name,
//        //                OnGoingAccountDeliveryCompanyPrice = dc.OnGoingAccountDeliveryCompanyPrice,
//        //                OnGoingAccountDifference = dc.OnGoingAccountDifference,
//        //                DeferredDifference = dc.deferredDifference,
//        //                CurrencyByCountry = Common.GetCurrencyByCountryName(dc.DeliveryCompany.SelectedCountry.ToString())
//        //            })
//        //        })
//        //    };


//        //    // Pass the result to the view
//        //    return Json(simplifiedViewModel);
//        //}


//        // حساباتي حسب المتجر لمندوب التوصيل 

//        //[HttpGet("/finance/bydeliveryRepresentative")]
//        //[Authorize(Roles = "Admin,DeliveryCompany", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        //public async Task<IActionResult> OrderByManfacturingCompanyOnGoingDeliveryRepresentative(int? deliveryCompanyId, Common.Countries? selectedCountry = null)
//        //{
//        //    var userId = _userContextService.GetCurrentUserId();

//        //    var role = _userContextService.GetUserRole();

//        //    // Apply additional filtering based on user role

//        //    // Check if the user is an Admin or Accountant
//        //    bool isAdminOrAccountant = role == "Admin";

//        //    // Ensure you are awaiting the asynchronous call
//        //    var viewModel = await _financialService.GetFinancialManufacturingCompanyDataOnGoingDeliveryRepresntaitves(userId, isAdminOrAccountant, selectedCountry, deliveryCompanyId);


//        //    var simplifiedViewModel = new
//        //    {
//        //        ManufacturingCompanies = viewModel.Select(mf => new
//        //        {
//        //            ManufacturingCompany = new
//        //            {
//        //                mf.ManufacturingCompany.Id,
//        //                mf.ManufacturingCompany.Name,
//        //                mf.ManufacturingCompany.Logo
//        //            },
//        //            DeliveryCompanyFinancials = mf.DeliveryCompanyFinancials.Select(dc => new
//        //            {
//        //                DeliveryRepresentativeId = dc.DeliveryCompanyId,
//        //                Country = dc.DeliveryCompany.SelectedCountry,
//        //                LogoUrl = dc.DeliveryCompany.LogoUrl,
//        //                Name = dc.DeliveryCompany.Name,
//        //                OnGoingAccountDeliveryRepresentativePrice = dc.OnGoingAccountDeliveryCompanyPrice,
//        //                OnGoingAccountDifference = dc.OnGoingAccountDifference,
//        //                DeferredDifference = dc.deferredDifference,
//        //                CurrencyByCountry = Common.GetCurrencyByCountryName(dc.DeliveryCompany.SelectedCountry.ToString())
//        //            })
//        //        })
//        //    };


//        //    // Pass the result to the view
//        //    return Json(simplifiedViewModel);
//        //}






//        // تحميل الفاتورة  من كشوفات الحسابات لشرطات التوصيل والمندوبين
//        [HttpGet("/finance/getorderreport")]
//        [Authorize(Roles = "Admin,DeliveryCompany,Accountant", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]

//        // [HourlyOutputCacheAttribute(1)] // Cache for 1 hour
//        public async Task<IActionResult> DownloadOrderReport(int orderReportId)
//        {
//            var currentUserId = _userContextService.GetCurrentUserId();
//            var isDeliveryCompanyRole = User.IsInRole("DeliveryCompany");
//            int? userDeliveryCompanyId = null;


//            IQueryable<OrderReport> orderReportQuery = _context.OrderReports
//                                                      .Include(or => or.Orders)
//                                                      .Include(or => or.DeliveryCompany);


//            if (isDeliveryCompanyRole)
//            {
//                // If the user is in the DeliveryCompany role, filter the order reports accordingly
//                orderReportQuery = orderReportQuery.Where(or => or.DeliveryCompany.UserId == currentUserId);
//            }

//            var orderReport = await orderReportQuery.FirstOrDefaultAsync(or => or.Id == orderReportId);

//            if (orderReport == null)
//            {
//                return NotFound("Order report not found.");
//            }

//            if (!orderReport.Orders.Any())
//            {
//                return NotFound("No orders found in the report.");
//            }


//            var orders = orderReport.Orders.ToList();
//            // Retrieve Order IDs
//            List<int> orderIds = orders.Select(order => order.Id).ToList();
//            Console.WriteLine(orderIds);
//            // Get delivery prices for these orders
//            Dictionary<int, decimal> deliveryPrices = _deliveryCompanyService.GetDeliveryCompanyPricesForOrderIds(orderIds);

//            // Calculate RemaningValue and totalDeliveryPrice using LINQ
//            decimal RemaningValue = orders.Sum(order => order.TotalPrice - deliveryPrices.GetValueOrDefault(order.Id, 0));
//            decimal totalDeliveryPrice = deliveryPrices.Values.Sum();


//            var headers = new List<string> { " كود الشحنة", "التاريخ", "اسم العميل", "رقم الهاتف", "المدينة", "المبلغ الإجمالي ", "سعر التوصيل", "صافي المبلغ" };
//            var valueSelectors = new List<Func<Order, string>> {
//                        o => o.Id.ToString(),
//                        o => o.CreatedDate.ToString("yyyy-MM-dd"),
//                        o => o.CustomerName,
//                        o => o.TelephoneNumber,
//                        o => o.State,
//                        o => o.TotalPrice.ToString() // Consider formatting this as currency
//                    };


//            // Assuming you want the country of the first order
//            string countryName = orders.FirstOrDefault()?.Country.ToString();
//            string currencyCode = Common.GetCurrencyByCountryName(countryName);
//            decimal totalValue = orders.Sum(o => o.TotalPrice);
//            string totalAmount = $"{totalValue:N2}\u200E {currencyCode}";

//            string deliveryAmount = $"{totalDeliveryPrice:N2}\u200E {currencyCode}";
//            // Calculate the total amount
//            string remaningAmount = $"{RemaningValue:N2}\u200E {currencyCode}";
//            // Other details
//            var deliveryCompanyName = orderReport.DeliveryCompany?.Name ?? "DefaultCompanyName";
//            var deliveryCompanyAddress = orderReport.DeliveryCompany?.Address ?? "DefaultCompanyAddress";
//            var deliveryCompanyPhoneNumber = orderReport.DeliveryCompany?.PhoneNumber ?? "DefaultCompanyPhoneNumber";
//            var createdDateString = orderReport.GeneratedTime.ToString("yyyy-MM-dd");
//            var reportIdString = orderReport.Id.ToString();
//            var totalOrderNumberString = orders.Count.ToString();



//            var pdfBytes = await _reportGenerator.CreatePdfReportAsync(
//                orders, headers, valueSelectors,
//                deliveryCompanyName, deliveryCompanyAddress, deliveryCompanyPhoneNumber,
//                createdDateString, reportIdString, totalAmount, deliveryAmount, remaningAmount, totalOrderNumberString, countryName);

//            Response.Headers.Add("Content-Disposition", "inline; filename=OrdersReport.pdf");


//            return File(pdfBytes, "application/pdf");
//        }



//        // حسابات الموظف 
//        [HttpGet("/finance/employeetransactions")]
//        [Authorize(Roles = "Admin,Accountant,CallCenter,FollowUpDepartment,ExecutiveDirector", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> GetEmployeeTransactionTotals(DateTime? filterMonth, int? EmployeeId = null)
//        {
//            // If filterMonth is not provided, use the current month
//            DateTime selectedMonth = filterMonth ?? DateTime.Now.Date;

//            // Determine if the current user is an admin or accountant
//            bool isAdminOrAccountant = User.IsInRole("Admin") || User.IsInRole("Accountant");
//            // Get the current user's ID
//            var currentUserId = _userContextService.GetCurrentUserId();
//            // Get the current user's employee ID (assuming the user's identity name is the employee ID)
//            var currentEmployeeId = User.Identity?.Name;

//            // Conditionally fetch transactions
//            var employeeTransactionsQuery = _context.EmployeeTransactions.Include(a => a.Employee)

//             .Where(et => et.Date.Year == selectedMonth.Year && et.Date.Month == selectedMonth.Month);


//            var employeesQuery = _context.Employees.Where(e => e.IsShown).AsQueryable();

//            if (EmployeeId.HasValue)
//            {
//                employeesQuery = employeesQuery.Where(et => et.Id == EmployeeId);
//            }

//            if (!isAdminOrAccountant)
//            {
//                employeesQuery = employeesQuery.Where(e => e.ApplicationUserId == currentUserId);
//            }

//            var allEmployees = await employeesQuery.ToListAsync();




//            // If the user is not an admin or accountant, filter for their transactions only
//            if (!isAdminOrAccountant && currentEmployeeId != null)
//            {
//                employeeTransactionsQuery = employeeTransactionsQuery.Where(et => et.EmployeeId.ToString() == currentEmployeeId);
//            }

//            // Fetch transactions with related employee data
//            var employeeTransactions = await employeeTransactionsQuery
//                    .Include(et => et.Employee)
//                    .AsNoTracking()
//                    .ToListAsync();

//            // Fetch bonus configurations
//            var bonusConfigurations = await _context.OrderBonusConfigurations.ToListAsync();
//            var minOrderThreshold = bonusConfigurations.Min(bc => bc.OrderThreshold);

//            // Fetch orders that are eligible for bonuses

//            var filteredOrdersQuery = _context.Orders
//                .Where(a => a.OrderStatus == OrderStatusEnum.تم_التسليم ||
//                            a.OrderStatus == OrderStatusEnum.تم_الدفع ||
//                            a.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد)
//                .AsQueryable();


//            var filteredOrders = await filteredOrdersQuery
//               .Where(order => order.TotalPrice >= minOrderThreshold)
//               .ToListAsync();


//            // Calculate bonuses for each order and employee
//            var applicableBonuses = from order in filteredOrders
//                                    from bc in bonusConfigurations
//                                    where bc.OrderThreshold <= order.TotalPrice && bc.countries == order.Country
//                                    select new { order, bc };



//            // Group and aggregate transactions and bonuses by employee
//            // Group and aggregate transactions by employee
//            var employeeFinancialSummaries = allEmployees
//             .GroupJoin(employeeTransactions, // Join employees with transactions
//                 e => e.Id,
//                 t => t.Employee.Id,
//                 (employee, transactions) => new { Employee = employee, Transactions = transactions })
//             .Select(group =>
//             {
//                 var totalSalary = group.Employee.Salary;
//                 var totalDeductions = group.Transactions.Where(t => t.TransactionType == TransactionTypeEnum.خصم).Sum(t => t.Amount);
//                 var totalRewards = group.Transactions.Where(t => t.TransactionType == TransactionTypeEnum.مكافأة).Sum(t => t.Amount);
//                 var totalAdvances = group.Transactions.Where(t => t.TransactionType == TransactionTypeEnum.سلفة).Sum(t => t.Amount);

//                 var totalCurrentAccount = totalSalary - totalDeductions + totalRewards - totalAdvances;

//                 return new EmployeeFinancialSummary
//                 {
//                     EmployeeName = group.Employee.Name, // Selecting only the employee's name
//                     TotalSalary = _decimalFormattingService.DecimalFormat(totalSalary),
//                     TotalDeductions = _decimalFormattingService.DecimalFormat(totalDeductions),
//                     TotalRewards = _decimalFormattingService.DecimalFormat(totalRewards),
//                     TotalAdvances = _decimalFormattingService.DecimalFormat(totalAdvances),
//                     TotalBonuses = _decimalFormattingService.DecimalFormat(0M), // Setting total bonuses to 0
//                     TotalCurrentAccount = _decimalFormattingService.DecimalFormat(totalCurrentAccount)
//                 };
//             })
//             .ToList();


//            return Json(employeeFinancialSummaries);

//        }


//        //كشوف الحسابات
//        [HttpGet("/finance/deliverycompanyreports")]
//        [Authorize(Roles = "Admin,Accountant,DeliveryCompany", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> OrderReports(string? search = null, int page = 1, int pageSize = 10)
//        {
//            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Get logged-in user's ID
//            var isDeliveryCompanyRole = User.IsInRole("DeliveryCompany") || User.IsInRole("DeliveryRepresentative");
//            int? deliveryCompanyId = null;

//            if (isDeliveryCompanyRole)
//            {
//                deliveryCompanyId = _context.DeliveryCompanies
//                    .Where(dc => dc.UserId == currentUserId)
//                    .Select(dc => dc.Id)
//                    .FirstOrDefault();
//            }

//            var orderReportsQuery = _context.OrderReports
//                .Where(or => or.DeliveryCompanyId != null && !or.DeliveryCompany.IsRepresentative);

//            if (isDeliveryCompanyRole && deliveryCompanyId.HasValue)
//            {
//                orderReportsQuery = orderReportsQuery.Where(or => or.DeliveryCompanyId == deliveryCompanyId.Value);
//            }

//            if (!string.IsNullOrEmpty(search))
//            {
//                // Try to parse the search term as an integer for Id
//                if (int.TryParse(search, out int searchId))
//                {
//                    orderReportsQuery = orderReportsQuery.Where(or => or.Id == searchId);
//                }
//                else if (DateTime.TryParse(search, out DateTime searchDate))
//                {
//                    // Filter by GeneratedTime within a date range starting with searchDate
//                    var nextDay = searchDate.AddDays(1);
//                    orderReportsQuery = orderReportsQuery.Where(or => or.GeneratedTime >= searchDate && or.GeneratedTime < nextDay);
//                }
//                else
//                {
//                    // If not a country, Id, or DateTime, filter by other criteria
//                    orderReportsQuery = orderReportsQuery.Where(or =>
//                        or.DeliveryCompany.Name.Contains(search));

//                }
//            }

//            var orderReportsCount = await orderReportsQuery.CountAsync();

//            var orderReports = await orderReportsQuery
//                .Include(or => or.DeliveryCompany)
//                .OrderByDescending(or => or.GeneratedTime)
//                .Skip((page - 1) * pageSize)
//                .Take(pageSize)
//                .Select(or => new OrderReportViewModel
//                {
//                    Id = or.Id,
//                    GeneratedTime = or.GeneratedTime.ToString("yyyy-MM-dd"), // Corrected format string
//                    TotalAmount = _decimalFormattingService.DecimalFormat(or.TotalAmount),
//                    Country = or.Country.ToString(), // Assuming Country is an enum
//                    DeliveryCompanyName = or.DeliveryCompany.Name,
//                    Currency = Common.GetCurrencyByCountryName(or.Country.ToString()),
//                })
//                .ToListAsync();

//            var paginationViewModel = new PaginationViewModel<OrderReportViewModel>
//            {
//                Items = orderReports,
//                CurrentPage = page,
//                PageSize = pageSize,
//                TotalItems = orderReportsCount
//            };

//            return Json(paginationViewModel);
//        }


//        //كشوف الحسابات للمندوبين
//        [HttpGet("/finance/deliveryrepresentativereports")]
//        [Authorize(Roles = "Admin,Accountant,DeliveryRepresentative", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> OrderReportsRepresentative(string? search = null, int page = 1, int pageSize = 10)
//        {
//            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Get logged-in user's ID
//            var isDeliveryCompanyRole = User.IsInRole("DeliveryCompany") || User.IsInRole("DeliveryRepresentative");
//            int? deliveryCompanyId = null;

//            if (isDeliveryCompanyRole)
//            {
//                deliveryCompanyId = _context.DeliveryCompanies
//                    .Where(dc => dc.UserId == currentUserId)
//                    .Select(dc => dc.Id)
//                    .FirstOrDefault();
//            }

//            var orderReportsQuery = _context.OrderReports
//                .Where(or => or.DeliveryCompanyId != null && or.DeliveryCompany.IsRepresentative);

//            if (isDeliveryCompanyRole && deliveryCompanyId.HasValue)
//            {
//                orderReportsQuery = orderReportsQuery.Where(or => or.DeliveryCompanyId == deliveryCompanyId.Value);
//            }

//            if (!string.IsNullOrEmpty(search))
//            {
//                // Try to parse the search term as an integer for Id
//                if (int.TryParse(search, out int searchId))
//                {
//                    orderReportsQuery = orderReportsQuery.Where(or => or.Id == searchId);
//                }
//                else if (DateTime.TryParse(search, out DateTime searchDate))
//                {
//                    // Filter by GeneratedTime within a date range starting with searchDate
//                    var nextDay = searchDate.AddDays(1);
//                    orderReportsQuery = orderReportsQuery.Where(or => or.GeneratedTime >= searchDate && or.GeneratedTime < nextDay);
//                }
//                else
//                {
//                    // If not a country, Id, or DateTime, filter by other criteria
//                    orderReportsQuery = orderReportsQuery.Where(or =>
//                        or.DeliveryCompany.Name.Contains(search));

//                }
//            }

//            var orderReportsCount = await orderReportsQuery.CountAsync();

//            var orderReports = await orderReportsQuery
//                .Include(or => or.DeliveryCompany)
//                .OrderByDescending(or => or.GeneratedTime)
//                .Skip((page - 1) * pageSize)
//                .Take(pageSize)
//                .Select(or => new OrderReportViewModel
//                {
//                    Id = or.Id,
//                    GeneratedTime = or.GeneratedTime.ToString("yyyy-MM-dd"), // Corrected format string
//                    TotalAmount = _decimalFormattingService.DecimalFormat(or.TotalAmount),
//                    Country = or.Country.ToString(), // Assuming Country is an enum
//                    DeliveryCompanyName = or.DeliveryCompany.Name,
//                    Currency = Common.GetCurrencyByCountryName(or.Country.ToString()),
//                })
//                .ToListAsync();

//            var paginationViewModel = new PaginationViewModel<OrderReportViewModel>
//            {
//                Items = orderReports,
//                CurrentPage = page,
//                PageSize = pageSize,
//                TotalItems = orderReportsCount
//            };

//            return Json(paginationViewModel);
//        }



//        //تقارير الأرباح والخسائر للدول  
//        [HttpGet("/finance/profitandlossreprots")]
//        [Authorize(Roles = "Admin,Accountant", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult ProfitAndLoss()
//        {
//            var financialSummary = _financialService.GetTotalProfitAndLossByCountry();

//            return Json(financialSummary);
//        }

//        // نهاية قسم حساباتي 


//        // المصروفات 
//        [HttpGet("/expenses/getall")]  // Removed the leading slash
//        [Authorize(Roles = "Admin,Accountant", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult GetAllExpenses(string? searchQuery = null)
//        {
//            IQueryable<Expense> query = _context.Expenses;

//            if (!string.IsNullOrWhiteSpace(searchQuery))
//            {
//                query = query.Where(e =>
//                    EF.Functions.Like(e.Description, $"%{searchQuery}%") ||
//                    EF.Functions.Like(e.Amount.ToString(), $"%{searchQuery}%") ||
//                    EF.Functions.Like(e.CreatedDate.ToString(), $"%{searchQuery}%")
//                );
//            }

//            var expenses = query
//                .Select(e => new AppExpensesViewModel
//                {
//                    Id = e.Id,
//                    Description = e.Description,
//                    Amount = _decimalFormattingService.DecimalFormat(e.Amount),
//                    DateAdded = e.CreatedDate.ToString("yyyy MM dd")
//                })
//                .ToList();

//            return Ok(expenses); // Returning 200 OK status along with the list of expenses
//        }




//        [HttpPost("/expenses/create")]
//        [Authorize(Roles = "Admin,Accountant", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult CreateExpense([FromBody] AppCreateExpenseViewModel model)
//        {
//            if (!ModelState.IsValid)
//            {
//                return BadRequest(ModelState);
//            }

//            var expense = new Expense
//            {
//                Description = model.Description,
//                Amount = model.Amount,
//                CreatedDate = _timeService.GetIstanbulTimeWithOffset(),
//            };

//            _context.Expenses.Add(expense);
//            _context.SaveChanges();

//            return Ok("Expense created successfully");
//        }

//        [HttpGet("/expenses/details/{id}")]
//        [Authorize(Roles = "Admin,Accountant", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult GetExpenseDetails(int id)
//        {
//            var expense = _context.Expenses.FirstOrDefault(e => e.Id == id);

//            if (expense == null)
//            {
//                return NotFound("Expense not found");
//            }

//            var expenseViewModel = new AppExpensesViewModel
//            {
//                Id = expense.Id,
//                Description = expense.Description,
//                Amount = _decimalFormattingService.DecimalFormat(expense.Amount),
//                DateAdded = expense.CreatedDate.ToString("yyyy MM dd")
//            };

//            return Ok(expenseViewModel); // Returning 200 OK status along with the expense details
//        }


//        [HttpPut("/expenses/edit/{id}")]
//        [Authorize(Roles = "Admin,Accountant", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult EditExpense(int id, [FromBody] AppCreateExpenseViewModel model)
//        {
//            if (!ModelState.IsValid)
//            {
//                return BadRequest(ModelState);
//            }

//            var expense = _context.Expenses.FirstOrDefault(e => e.Id == id);

//            if (expense == null)
//            {
//                return NotFound("Expense not found");
//            }

//            // Update expense properties
//            expense.Description = model.Description;
//            expense.Amount = model.Amount;
//            expense.CreatedDate = expense.CreatedDate;

//            _context.SaveChanges();

//            return Ok("Expense updated successfully");
//        }

//        [HttpDelete("/expenses/delete/{id}")]
//        [Authorize(Roles = "Admin,Accountant", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult DeleteExpense(int id)
//        {
//            var expense = _context.Expenses.FirstOrDefault(e => e.Id == id);

//            if (expense == null)
//            {
//                return NotFound("Expense not found");
//            }

//            _context.Expenses.Remove(expense);
//            _context.SaveChanges();

//            return Ok("Expense deleted successfully");
//        }

//        // نهاية مصروفات 


//        // الأجرائات Qr


//        // نهاية الأجرائات

//        // تتبع الحشنات 

//        // نهاية تعتبع اشحنات

//        //  طريقة الدفع 

//        // نهاية طريقة الدفع 

//        //   تسجيلات الدخول 

//        //  نهاية تسجيلات الدخول 


//        //    المستودعات 

//        [HttpGet("/warehouses/getallfordelvierycompany")]  // Removed the leading slash
//        [Authorize(Roles = "Admin,DeliveryCompany,Accountant,OrderPreparer,Observer,ExecutiveDirector,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult GetWarehousesForDeliveryCompany([FromQuery] int page = 1, [FromQuery] int? pageSize = null, [FromQuery] string? search = null)
//        {
//            IQueryable<Warehouse> query = _context.Warehouses
//                .Include(w => w.DeliveryCompany)
//                .Include(w => w.ManufacturingCompany)
//                .Where(w => !w.DeliveryCompany.IsRepresentative);

//            // Parse search string to country enum
//            if (Enum.TryParse(search, true, out Common.Countries country))
//            {
//                query = query.Where(w => w.Countries == country);
//            }
//            else if (!string.IsNullOrEmpty(search)) // If search is not a country, search other fields
//            {
//                query = query.Where(w => w.Name.Contains(search)
//                                    || w.DeliveryCompany.Name.Contains(search)
//                                    || w.ManufacturingCompany.Name.Contains(search));
//            }

//            // Pagination
//            int totalItems = query.Count();
//            int effectivePageSize = pageSize ?? 10;
//            int skip = (page - 1) * effectivePageSize;

//            var warehouses = query.OrderByDescending(w => w.Id)
//                                .Skip(skip)
//                                .Take(effectivePageSize)
//                                .Select(w => new AppWarehouseViewModel
//                                {
//                                    Id = w.Id,
//                                    Name = w.Name,
//                                    Price = _decimalFormattingService.DecimalFormat(w.Price),
//                                    UnchangingAmount = w.UnchangingAmount,
//                                    Amount = w.Amount,
//                                    Total = _decimalFormattingService.DecimalFormat(w.Total),
//                                    ProductImage = w.MainWarehouse.ImageUrl,
//                                    DeliveryCompany = new AppCompanyViewModel
//                                    {
//                                        Id = w.DeliveryCompany.Id,
//                                        Name = w.DeliveryCompany.Name,
//                                        LogoUrl = w.DeliveryCompany.ImageUrl
//                                    },
//                                    ManufacturingCompany = new AppCompanyViewModel
//                                    {
//                                        Id = w.ManufacturingCompany.Id,
//                                        Name = w.ManufacturingCompany.Name,
//                                        LogoUrl = w.ManufacturingCompany.ImageUrl
//                                    },
//                                    DateAdded = w.DateAdded.ToString("yyyy-MM-dd"),
//                                    DateUpdated = w.DateUpdated.ToString("yyyy-MM-dd"),
//                                    CountryId = (int)w.Countries,
//                                    City = w.City,
//                                    IsShown = w.IsShown
//                                })
//                                .ToList();

//            var response = new
//            {
//                TotalItems = totalItems,
//                PageSize = effectivePageSize,
//                CurrentPage = page,
//                Warehouses = warehouses
//            };

//            return Ok(response);
//        }


//        [HttpGet("/warehouses/getallfordelvieryrepresentative")]
//        [Authorize(Roles = "Admin,DeliveryCompany,Accountant,OrderPreparer,Observer,ExecutiveDirector,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult GetWarehousesForDeliveryRepresentative([FromQuery] int page = 1, [FromQuery] int? pageSize = null, [FromQuery] string? search = null)
//        {
//            IQueryable<Warehouse> query = _context.Warehouses
//                .Include(w => w.DeliveryCompany)
//                .Include(w => w.ManufacturingCompany)
//                .Where(w => w.DeliveryCompany.IsRepresentative);

//            // Parse search string to country enum
//            if (Enum.TryParse(search, true, out Common.Countries country))
//            {
//                query = query.Where(w => w.Countries == country);
//            }
//            else if (!string.IsNullOrEmpty(search)) // If search is not a country, search other fields
//            {
//                query = query.Where(w => w.Name.Contains(search)
//                                    || w.DeliveryCompany.Name.Contains(search)
//                                    || w.ManufacturingCompany.Name.Contains(search));
//            }

//            // Pagination
//            int totalItems = query.Count();
//            int effectivePageSize = pageSize ?? 10;
//            int skip = (page - 1) * effectivePageSize;

//            var warehouses = query.OrderByDescending(w => w.Id)
//                                .Skip(skip)
//                                .Take(effectivePageSize)
//                                .Select(w => new AppWarehouseViewModel
//                                {
//                                    Id = w.Id,
//                                    Name = w.Name,
//                                    Price = _decimalFormattingService.DecimalFormat(w.Price),
//                                    UnchangingAmount = w.UnchangingAmount,
//                                    Amount = w.Amount,
//                                    Total = _decimalFormattingService.DecimalFormat(w.Total),
//                                    ProductImage = w.MainWarehouse.ImageUrl,
//                                    DeliveryCompany = new AppCompanyViewModel
//                                    {
//                                        Id = w.DeliveryCompany.Id,
//                                        Name = w.DeliveryCompany.Name,
//                                        LogoUrl = w.DeliveryCompany.ImageUrl
//                                    },
//                                    ManufacturingCompany = new AppCompanyViewModel
//                                    {
//                                        Id = w.ManufacturingCompany.Id,
//                                        Name = w.ManufacturingCompany.Name,
//                                        LogoUrl = w.ManufacturingCompany.ImageUrl
//                                    },
//                                    DateAdded = w.DateAdded.ToString("yyyy-MM-dd"),
//                                    DateUpdated = w.DateUpdated.ToString("yyyy-MM-dd"),
//                                    CountryId = (int)w.Countries,
//                                    City = w.City,
//                                    IsShown = w.IsShown
//                                })
//                                .ToList();

//            var response = new
//            {
//                TotalItems = totalItems,
//                PageSize = effectivePageSize,
//                CurrentPage = page,
//                Warehouses = warehouses
//            };

//            return Ok(response);
//        }


//        [HttpPost("/warehouses/create")]
//        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> createwarehouse([FromForm] AppCreateWarehouseViewModel viewModel)
//        {




//            var countryEnum = (Common.Countries)viewModel.CountryId;
//            // Now use countryEnum as needed

//            Console.WriteLine(viewModel.CountryId); // Use proper logging as per your application's configuration


//            if (!ModelState.IsValid)
//            {
//                return BadRequest(ModelState);
//            }

//            // Handling nullable Company IDs
//            var warehouse = new Warehouse
//            {
//                Name = viewModel.Name,
//                Price = viewModel.Price,
//                Amount = viewModel.Amount,
//                DeliveryCompanyId = (int)viewModel.DeliveryCompanyId,
//                ManufacturingCompanyId = (int)viewModel.ManufacturingCompanyId,
//                Countries = countryEnum, // Ensure this maps correctly
//                City = viewModel.City,
//                DateAdded = _timeService.GetIstanbulTimeWithOffset(),
//                DateUpdated = _timeService.GetIstanbulTimeWithOffset(),
//            };

            
//            _context.Add(warehouse);
//            await _context.SaveChangesAsync();

//            return Ok("Warehouse created successfully");
//        }



//        [HttpPut("/warehouses/edit/{id}")]
//        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> EditWarehouse(int id, [FromForm] AppCreateWarehouseViewModel viewModel, [FromForm] IFormFile? productImage)
//        {
//            if (!ModelState.IsValid)
//            {
//                return BadRequest(ModelState);
//            }

//            var warehouse = await _context.Warehouses.FindAsync(id);
//            if (warehouse == null)
//            {
//                return NotFound("Warehouse not found.");
//            }

//            // Update properties
//            warehouse.Name = viewModel.Name;
//            warehouse.Price = viewModel.Price;
//            warehouse.Amount = viewModel.Amount;
//            warehouse.DeliveryCompanyId = (int)viewModel.DeliveryCompanyId;
//            warehouse.ManufacturingCompanyId = (int)viewModel.ManufacturingCompanyId;
//            warehouse.Countries = (Common.Countries)viewModel.CountryId; // Assuming CountryId maps directly to Common.Countries enum
//            warehouse.City = viewModel.City;
//            warehouse.DateUpdated = _timeService.GetIstanbulTimeWithOffset();

//            try
//            {
//                _context.Update(warehouse);
//                await _context.SaveChangesAsync();
//            }
//            catch (DbUpdateConcurrencyException)
//            {
//                if (!_context.Warehouses.Any(e => e.Id == id))
//                {
//                    return NotFound("Warehouse not found.");
//                }
//                else
//                {
//                    throw;
//                }
//            }

//            return Ok("Warehouse updated successfully.");
//        }



//        [HttpGet("/warehouses/details/{id}")]
//        [Authorize(Roles = "Admin,DeliveryCompany,Accountant,OrderPreparer,Observer,ExecutiveDirector,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult warehousedetails(int id)
//        {
//            var warehouse = _context.Warehouses
//                .Include(w => w.DeliveryCompany)
//                .Include(w => w.ManufacturingCompany)
//                .Where(w => w.Id == id)
//                .Select(w => new AppWarehouseViewModel
//                {
//                    Id = w.Id,
//                    Name = w.Name,
//                    Price = _decimalFormattingService.DecimalFormat(w.Price),
//                    UnchangingAmount = w.UnchangingAmount,
//                    Amount = w.Amount,
//                    Total = _decimalFormattingService.DecimalFormat(w.Total),
//                    ProductImage = w.MainWarehouse.ImageUrl,
//                    DeliveryCompany = new AppCompanyViewModel
//                    {
//                        Id = w.DeliveryCompany.Id,
//                        Name = w.DeliveryCompany.Name,
//                        LogoUrl = w.DeliveryCompany.ImageUrl
//                    },
//                    ManufacturingCompany = new AppCompanyViewModel
//                    {
//                        Id = w.ManufacturingCompany.Id,
//                        Name = w.ManufacturingCompany.Name,
//                        LogoUrl = w.ManufacturingCompany.ImageUrl
//                    },
//                    DateAdded = w.DateAdded.ToString("yyyy-MM-dd"),
//                    DateUpdated = w.DateUpdated.ToString("yyyy-MM-dd"),
//                    CountryId = (int)w.Countries,
//                    City = w.City,
//                    IsShown = w.IsShown
//                })
//                .FirstOrDefault();

//            if (warehouse == null)
//            {
//                return NotFound("Warehouse not found.");
//            }

//            return Ok(warehouse);
//        }


//        [HttpPost("/warehousesinvoice/create")]
//        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult CreateProductShipmentInvoice([FromBody] AppCreateProductShipmentInvoiceViewModel model)
//        {
//            // Check model state for validation errors
//            if (!ModelState.IsValid)
//            {
//                // Return BadRequest with ModelState errors
//                return BadRequest(ModelState);
//            }

//            Random rnd = new Random();
//            // Generate a random number between 10000 and 99999.
//            int randomNumber = rnd.Next(10000, 100000);

//            var invoice = new ProductShipmentInvoice
//            {
//                CustomId = randomNumber, // Assign the generated 5-digit random number as ID.
//                Countries = (Countries)model.Countries,
//                City = model.City,
//                DeliveryCompanyId = model.DeliveryCompanyId,
//                CreatedDate = _timeService.GetIstanbulTimeWithOffset(),
//                TotalPrice = model.TotalPrice,
//            };
//            invoice.ProductShipmentInvoiceWarehouses = new List<ProductShipmentInvoiceWarehouse>();

//            foreach (var warehouseQuantity in model.WarehouseQuantities)
//            {
//                // Verify each WarehouseId exists to prevent the ForeignKey constraint error
//                if (_context.Warehouses.Any(w => w.Id == warehouseQuantity.WarehouseId))
//                {
//                    // Add each warehouse quantity to the invoice
//                    invoice.ProductShipmentInvoiceWarehouses.Add(new ProductShipmentInvoiceWarehouse
//                    {
//                        WarehouseId = warehouseQuantity.WarehouseId,
//                        Quantity = warehouseQuantity.Quantity,
//                    });
//                }
//                else
//                {
//                    // If the warehouse doesn't exist, add a model error
//                    ModelState.AddModelError(nameof(model.WarehouseQuantities), $"Warehouse with ID {warehouseQuantity.WarehouseId} does not exist.");
//                }
//            }

//            // Check model state again after adding errors
//            if (!ModelState.IsValid)
//            {
//                // Return BadRequest with ModelState errors
//                return BadRequest(ModelState);
//            }

//            _context.ProductShipmentInvoices.Add(invoice);
//            _context.SaveChanges();

//            // Instead of redirecting (which is for MVC), return a success response
//            // You might return the ID of the newly created invoice, or a URI to access it, or just a success message
//            return Ok(new { Message = "Product shipment invoice created successfully", InvoiceId = randomNumber });
//        }



//        [HttpGet("/warehousesinvoice")]
//        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult GetProductShipmentInvoices([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
//        {
//            // Filter invoices based on search criteria
//            var invoicesQuery = _context.ProductShipmentInvoices
//                .Include(p => p.DeliveryCompany)
//                .Include(p => p.ProductShipmentInvoiceWarehouses)
//                .Where(invoice =>
//                    search == null ||
//                    invoice.CustomId.ToString().Contains(search) || // Adjust the conditions based on your search requirements
//                    invoice.City.Contains(search, StringComparison.OrdinalIgnoreCase) ||
//                    invoice.DeliveryCompany.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
//                    invoice.ProductShipmentInvoiceWarehouses.Any(psiw => psiw.Warehouse.Name.Contains(search, StringComparison.OrdinalIgnoreCase)))
//                .OrderBy(invoice => invoice.CustomId) // Or any other ordering you see fit
//                .Skip((page - 1) * pageSize)
//                .Take(pageSize)
//                .Select(invoice => new AppProductShipmentInvoiceViewModel
//                {
//                    Id = invoice.Id,
//                    CustomId = invoice.CustomId,
//                    Country = invoice.Countries.ToString(), // Assuming Countries is an enum
//                    City = invoice.City,
//                    TotalPrice = invoice.TotalPrice,
//                    CreatedDate = invoice.CreatedDate.ToString("yyyy-MM-dd"), // Corrected format
//                    DeliveryCompany = new AppCompanyViewModel
//                    {
//                        Id = invoice.DeliveryCompany.Id,
//                        Name = invoice.DeliveryCompany.Name
//                    },
//                    ProductShipmentInvoiceWarehouses = invoice.ProductShipmentInvoiceWarehouses
//                        .Select(psiw => new WarehouseQuantityViewModel
//                        {
//                            WarehouseId = psiw.WarehouseId,
//                            Quantity = psiw.Quantity,
//                            Name = psiw.Warehouse.Name // Ensure you have navigation property set up for this
//                        }).ToList()
//                });

//            var invoices = invoicesQuery.ToList(); // Materialize the query

//            var totalItems = invoicesQuery.Count(); // Count after materialization

//            var response = new PaginationViewModel<AppProductShipmentInvoiceViewModel>
//            {
//                Items = invoices,
//                CurrentPage = page,
//                PageSize = pageSize,
//                TotalItems = totalItems
//            };

//            return Ok(response);
//        }





//        [HttpGet("/warehousesinvoice/details/{id}")]
//        [Authorize(Roles = "Admin,DeliveryCompany,Accountant,DeliveryRepresentative,ExecutiveDirector,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> DownloadShipmentReport(int? id)
//        {
//            if (id == null)
//            {
//                return NotFound();
//            }

//            var invoice = _context.ProductShipmentInvoices
//                .Include(i => i.ProductShipmentInvoiceWarehouses)
//                    .ThenInclude(w => w.Warehouse)
//                .Include(i => i.DeliveryCompany)
//                   .ThenInclude(w => w.User)
//                .FirstOrDefault(m => m.Id == id);

//            if (invoice == null)
//            {
//                return NotFound();
//            }

//            // Map the invoice to the ViewModel
//            var viewModel = new ProductShipmentInvoiceDetailsViewModel
//            {
//                InvoiceId = invoice.CustomId,
//                Country = invoice.Countries.ToString(), // Assuming Countries is an enum or similar
//                City = invoice.City,
//                DeliveryCompanyName = invoice.DeliveryCompany.Name,
//                DeliveryCompanyPhoneNumber = invoice.DeliveryCompany.PhoneNumber,
//                DeliveryCompanyAddress = invoice.DeliveryCompany.Address,
//                DeliveryCompanyEmail = invoice.DeliveryCompany.User.Email,
//                CreatedDate = invoice.CreatedDate,
//                TotalPrice = invoice.TotalPrice,
//                WarehouseDetails = invoice.ProductShipmentInvoiceWarehouses.Select(w => new WarehouseDetail
//                {
//                    WarehouseName = w.Warehouse.Name,
//                    WarehousePrice = w.Warehouse.Price,
//                    Quantity = w.Quantity
//                }).ToList()
//            };

//            var pdfBytes = await _pdfReportGeneratorShipmentInvoice.CreatePdfReportAsync(
//                    DeliveryCompanyName: viewModel.DeliveryCompanyName,
//                    DeliveryCompanyAddress: viewModel.DeliveryCompanyAddress,
//                    DeliveryCompanyPhoneNumber: viewModel.DeliveryCompanyPhoneNumber,
//                    DeliveryCompanyEmail: viewModel.DeliveryCompanyEmail,
//                    createdDate: viewModel.CreatedDate.ToString("yyyy:M:dd"), // Convert to string if necessary
//                    reportId: viewModel.InvoiceId.ToString(), // Convert to string if necessary
//                    totalAmount: viewModel.TotalPrice.ToString(), // Convert to string if necessary
//                    warehouseItems: viewModel.WarehouseDetails
//                );

//            Response.Headers.Add("Content-Disposition", "inline; filename=OrdersReport.pdf");


//            return File(pdfBytes, "application/pdf");
//        }


//        [HttpPost("/warehouses/priceoffer")]
//        [Authorize(Roles = "Admin,Accountant,ExecutiveDirector,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> CreatePriceOffer(PriceOfferViewModel model)
//        {
//            if (!ModelState.IsValid)
//            {
//                // In an API context, return a BadRequest with the ModelState to inform what went wrong
//                return BadRequest(ModelState);
//            }
//            // Calculate the total price
//            model.TotalPriceOfAllProducts = model.Products.Sum(p => p.TotalPrice);
//            model.CreatedDate = DateTime.Now; // Set the created date to now or from the form
//            Random random = new Random();
//            model.InvoiceId = random.Next(1000, 10000); // This will generate a number between 1000 and 9999

//            var pdfBytes = await _pdfReportGeneratorShipmentInvoice.CreatePdfReportAsync(
//                DeliveryCompanyName: model.DeliveryCompanyName,
//                DeliveryCompanyAddress: model.DeliveryCompanyAddress,
//                DeliveryCompanyPhoneNumber: model.DeliveryCompanyPhoneNumber,
//                DeliveryCompanyEmail: model.DeliveryCompanyEmail,
//                createdDate: _timeService.GetIstanbulTimeWithOffset().ToString("yyyy-MM-dd"), // Corrected format
//                reportId: model.InvoiceId.ToString(),
//                totalAmount: model.TotalPriceOfAllProducts.ToString(),
//                warehouseItems: model.Products.Select(p => new WarehouseDetail
//                {
//                    // Assuming you have a WarehouseItem class that you need to map to
//                    WarehouseName = p.Name,
//                    WarehousePrice = p.Price,
//                    Quantity = p.Amount,
//                }).ToList()
//            );

//            Response.Headers.Add("Content-Disposition", "inline; filename=OrdersReport.pdf");
//            return File(pdfBytes, "application/pdf");
//        }


//        //  نهاية المستودعات   


//        // المنتجات 

//        [HttpGet("/productlist/getall")]
//        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult Index(Common.Countries? country, string? productNameStartsWith, int? manufacturingCompanyId, int page = 1, int pageSize = 10)
//        {
//            // Retrieve the list of products from the database and include the related ManufacturingCompany
//            IQueryable<AppProductsPricesViewModel> query = _context.MainProducts
//                .Include(p => p.ManufacturingCompany)
//                .Where(p => !country.HasValue || p.Country == country.Value) // Filter by country if specified
//                .Where(p => string.IsNullOrEmpty(productNameStartsWith) || p.Name.StartsWith(productNameStartsWith)) // Filter by product name prefix if specified
//                .Where(p => !manufacturingCompanyId.HasValue || p.ManufacturingCompanyId == manufacturingCompanyId.Value) // Filter by manufacturing company ID if specified
//                .Select(p => new AppProductsPricesViewModel
//                {
//                    Id = p.Id,
//                    Country = p.Country,
//                    ProductName = p.Name,
//                    ProductImage = p.ImageUrl,
//                    ProductPrice = _decimalFormattingService.DecimalFormat(Convert.ToDecimal(p.Price)), // Convert string to decimal
//                    ManufacturingCompanyName = p.ManufacturingCompany != null ? p.ManufacturingCompany.Name : "", // Handle possible null value
//                    SelectedManufacturingCompanyId = p.ManufacturingCompanyId
//                });

//            // Calculate total items count
//            int totalItems = query.Count();

//            // Apply pagination
//            var paginatedData = query.Skip((page - 1) * pageSize)
//                                    .Take(pageSize)
//                                    .ToList();

//            // Create and populate the PaginationViewModel
//            var paginationViewModel = new PaginationViewModel<AppProductsPricesViewModel>
//            {
//                Items = paginatedData,
//                CurrentPage = page,
//                PageSize = pageSize,
//                TotalItems = totalItems
//            };

//            return Json(paginationViewModel);
//        }


//        // POST: ProductsPrices/Create
//        [HttpPost("/productlist/create")]
//        [Authorize(Roles = "Admin,ExecutiveDirector", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> Create([FromForm] AppCreateProductsPricesViewModel viewModel, IFormFile productImage)
//        {
//            // Check if productImage is provided
//            if (productImage == null || productImage.Length == 0)
//            {
//                ModelState.AddModelError("ProductImage", "Product image is required");
//                return BadRequest(ModelState);
//            }

//            // Upload the image
//            viewModel.ProductImage = await _fileUploadService.UploadFileAsync(productImage, "Products");

//            // Save the model to the database
//            var product = new MainProduct
//            {
//                Name = viewModel.ProductName,
//                ImageUrl = viewModel.ProductImage, // No need to use FileName here
//                Price = viewModel.ProductPrice,
//                Country = viewModel.Country,
//                ManufacturingCompanyId = viewModel.SelectedManufacturingCompanyId, // Associate with selected manufacturing company
//            };

//            // If ModelState is not valid, return bad request
//            if (!ModelState.IsValid)
//            {
//                return BadRequest(ModelState);
//            }

//            // Save the product to the database
//            _context.MainProducts.Add(product);
//            await _context.SaveChangesAsync();

//            // Return the created product as JSON
//            return Json("Sucess");
//        }


//        // PUT: ProductsPrices/Edit
//        [HttpPut("/productlist/edit/{id}")]
//        [Authorize(Roles = "Admin,ExecutiveDirector", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> Edit(int id, [FromForm] AppCreateProductsPricesViewModel viewModel, IFormFile productImage)
//        {


//            // Fetch the existing product from the database
//            var product = await _context.MainProducts.FindAsync(id);

//            // Check if the product exists
//            if (product == null)
//            {
//                return NotFound();
//            }

//            // Check if productImage is provided
//            if (productImage == null || productImage.Length == 0)
//            {
//                ModelState.AddModelError("ProductImage", "Product image is required");
//                return BadRequest(ModelState);
//            }

//            // Upload the new image
//            product.ImageUrl = await _fileUploadService.UploadFileAsync(productImage, "Products");

//            // Update the product properties
//            product.Name = viewModel.ProductName;
//            product.Price = viewModel.ProductPrice;
//            product.Country = viewModel.Country;
//            product.ManufacturingCompanyId = viewModel.SelectedManufacturingCompanyId;

//            // If ModelState is not valid, return bad request
//            if (!ModelState.IsValid)
//            {
//                return BadRequest(ModelState);
//            }

//            // Save changes to the database
//            await _context.SaveChangesAsync();

//            // Return success message
//            return Ok("Success");
//        }


//        // GET: ProductsPrices/Details/{id}
//        [HttpGet("/productlist/details/{id}")]
//        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult productlistDetails(int id)
//        {
//            // Retrieve the product details from the database including the related ManufacturingCompany
//            var productDetails = _context.MainProducts
//                .Include(p => p.ManufacturingCompany)
//                .FirstOrDefault(p => p.Id == id);

//            // Check if the product exists
//            if (productDetails == null)
//            {
//                return NotFound();
//            }

//            // Map the product details to a view model
//            var viewModel = new AppProductsPricesViewModel
//            {
//                Id = productDetails.Id,
//                Country = productDetails.Country,
//                ProductName = productDetails.Name,
//                ProductImage = productDetails.ImageUrl,
//                ProductPrice = _decimalFormattingService.DecimalFormat(productDetails.Price),
//                ManufacturingCompanyName = productDetails.ManufacturingCompany.Name,
//                SelectedManufacturingCompanyId = productDetails.ManufacturingCompanyId
//            };

//            return Json(viewModel);
//        }





//        // نهاية المنتجات 

//        // ادارة الموظفين
//        [HttpGet("/employees/getall")]
//        [Authorize(Roles = "Admin,ExecutiveDirector", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> employeegetall(string? search = null, int page = 1, int pageSize = 10)
//        {
//            // Initialize the query
//            IQueryable<AppEmployeeViewmodel> query = _context.Employees
//                .Include(a => a.ApplicationUser)
//                .Where(e => string.IsNullOrEmpty(search) || e.Name.Contains(search)) // Filter by name if searchName provided
//                .Select(e => new AppEmployeeViewmodel
//                {
//                    Id = e.Id,
//                    Name = e.Name,
//                    PhoneNumber = e.PhoneNumber,
//                    IsShown = e.IsShown,
//                    IsActive = e.ApplicationUser.EmailConfirmed,
//                });


//            // No additional filter for Admin role, as they should see all employees

//            // Execute the count on the filtered query
//            int totalItems = await query.CountAsync();

//            // Retrieve the list of employees for the current page
//            List<AppEmployeeViewmodel> employeesViewModel = await query
//                .Skip((page - 1) * pageSize)
//                .Take(pageSize)
//                .ToListAsync();

//            // Create and populate the PaginationViewModel
//            var paginationViewModel = new PaginationViewModel<AppEmployeeViewmodel>
//            {
//                Items = employeesViewModel,
//                CurrentPage = page,
//                PageSize = pageSize,
//                TotalItems = totalItems
//            };

//            return Json(paginationViewModel);
//        }


//        [HttpPost("/employees/create")]
//        [Authorize(Roles = "Admin,ExecutiveDirector", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> Createemployee([FromForm] AppCreateEmployeeViewmodel model, IFormFile? cvFile, IFormFile? imgFile)
//        {
//            // Create the ApplicationUser first
//            var user = new ApplicationUser
//            {
//                UserName = model.Email,
//                Email = model.Email,
//                Name = model.Name
//            };

//            var userCreationResult = await _userManager.CreateAsync(user, model.Password);

//            if (userCreationResult.Succeeded)
//            {
//                var roleAssignmentResult = await _userManager.AddToRoleAsync(user, model.Role);



//                // Now create the Employee
//                var employee = new Employee
//                {
//                    Name = model.Name,
//                    IdNumber = model.IdNumber,
//                    Nationality = model.Nationality,
//                    PhoneNumber = model.PhoneNumber,
//                    Address = model.Address,
//                    Salary = model.Salary,
//                    AcademicLevel = model.AcademicLevel,
//                    JobTitle = model.JobTitle,
//                    DateOfBirth = model.DateOfBirth,
//                    Gender = model.Gender,
//                    DateAdded = _timeService.GetIstanbulTimeWithOffset(), // Assuming you wish to set this as the current date and time.
//                    DeliveryCompanyId = model.DeliveryCompanyId, // Allow null to be assigned
//                    ApplicationUserId = user.Id // Setting the foreign key to link Employee with ApplicationUser
//                };

//                // Handle CV file upload
//                if (cvFile != null)
//                {
//                    employee.Cv = await _fileUploadService.UploadFileAsync(cvFile, "Employees");
//                }

//                // Handle image file upload
//                if (imgFile != null)
//                {
//                    employee.ImageUrl = await _fileUploadService.UploadFileAsync(imgFile, "Employees");
//                }

//                // If user is in the "DeliveryCompany" role, set the DeliveryCompanyId
//                if (User.IsInRole("DeliveryCompany"))
//                {
//                    employee.DeliveryCompanyId = user.AcessId;
//                }

//                _context.Add(employee);
//                await _context.SaveChangesAsync();

//                // Now, set the AcessId of the ApplicationUser
//                user.AcessId = employee.Id;  // Assuming you wish to set the AcessId as the Employee's Id.
//                await _userManager.UpdateAsync(user);

//                // Return success JSON response
//                return Json(new { success = true });
//            }
//            else
//            {
//                // Extract error messages
//                var errors = userCreationResult.Errors.Select(e => e.Description).ToList();

//                // Return error JSON response
//                return Json(new { success = false, errors });
//            }
//        }

//        [HttpPut("/employees/edit/{id}")]
//        [Authorize(Roles = "Admin,ExecutiveDirector", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> Editemployee(int id, [FromForm] AppEditEmployeeViewmodel model, IFormFile? cvFile, IFormFile? imgFile)
//        {
//            // Retrieve the employee from the database
//            var employee = await _context.Employees.FindAsync(id);
//            if (employee == null)
//            {
//                // If employee not found, return NotFound
//                return NotFound();
//            }

//            // Update the ApplicationUser associated with the employee
//            var user = await _userManager.FindByIdAsync(employee.ApplicationUserId);
//            if (user == null)
//            {
//                // If user not found, return NotFound
//                return NotFound();
//            }

//            // Update user details
//            user.UserName = model.Email;
//            user.Email = model.Email;
//            user.Name = model.Name;

//            var userUpdateResult = await _userManager.UpdateAsync(user);
//            if (!userUpdateResult.Succeeded)
//            {
//                // If user update fails, return error response
//                var errors = userUpdateResult.Errors.Select(e => e.Description).ToList();
//                return Json(new { success = false, errors });
//            }

//            // Update other employee details
//            employee.Name = model.Name;
//            employee.IdNumber = model.IdNumber;
//            employee.Nationality = model.Nationality;
//            employee.PhoneNumber = model.PhoneNumber;
//            employee.Address = model.Address;
//            employee.Salary = model.Salary;
//            employee.AcademicLevel = model.AcademicLevel;
//            employee.JobTitle = model.JobTitle;
//            employee.DateOfBirth = model.DateOfBirth;
//            employee.Gender = model.Gender;

//            // Handle CV file upload
//            if (cvFile != null)
//            {
//                string cvPath = await _fileUploadService.UploadFileAsync(cvFile, "Employees");
//                employee.Cv = cvPath;
//            }

//            // Handle image file upload
//            if (imgFile != null)
//            {
//                string imgPath = await _fileUploadService.UploadFileAsync(imgFile, "Employees");
//                employee.ImageUrl = imgPath;
//            }

//            // If user is in the "DeliveryCompany" role, update the DeliveryCompanyId
//            if (User.IsInRole("DeliveryCompany"))
//            {
//                employee.DeliveryCompanyId = user.AcessId;
//            }

//            await _context.SaveChangesAsync();

//            // Return success JSON response
//            return Json(new { success = true });
//        }

//        [HttpGet("/employees/details/{id}")]
//        [Authorize(Roles = "Admin,ExecutiveDirector", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> EmployeeDetails(int id)
//        {
//            // Retrieve the employee details by id
//            var employeeViewModel = await _context.Employees
//                .Include(e => e.ApplicationUser)
//                .Where(e => e.Id == id)
//                .Select(e => new AppEmployeeViewmodel
//                {
//                    Id = e.Id,
//                    Cv = e.Cv,
//                    Img = e.ImageUrl,
//                    Name = e.Name,
//                    IdNumber = e.IdNumber,
//                    Nationality = e.Nationality,
//                    PhoneNumber = e.PhoneNumber,
//                    Address = e.Address,
//                    Salary = _decimalFormattingService.DecimalFormat(e.Salary),
//                    AcademicLevel = e.AcademicLevel,
//                    JobTitle = e.JobTitle,
//                    DateOfBirth = e.DateOfBirth.ToString("yyyy-MM-dd"),
//                    Gender = e.Gender,
//                    AddedDate = e.DateAdded.ToString("yyyy-MM-dd"), // Assuming you wish to set this as the current date and time.
//                    DeliveryCompanyName = e.DeliveryCompany.Name, // Allow null to be assigned
//                })
//                .FirstOrDefaultAsync();

//            if (employeeViewModel == null)
//            {
//                // If employee not found, return NotFound
//                return NotFound();
//            }

//            return Json(employeeViewModel);
//        }




//        [HttpGet("/orderbonusesforcountries/getall")]
//        [Authorize(Roles = "Admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> GetAllorderbonusesforcountries(int page = 1, int pageSize = 10)
//        {
//            // Initialize the query
//            IQueryable<AppOrderBonusConfigurationViewModel> query = _context.OrderBonusConfigurations

//                .Select(ob => new AppOrderBonusConfigurationViewModel
//                {
//                    Id = ob.Id,
//                    OrderThreshold = _decimalFormattingService.DecimalFormat(ob.OrderThreshold),
//                    FlatBonusAmount = _decimalFormattingService.DecimalFormat(ob.FlatBonusAmount),
//                    PercentageBonus = ob.PercentageBonus.HasValue ? _decimalFormattingService.DecimalFormat(ob.PercentageBonus.Value) + "%" : null,
//                    countries = (Countries)ob.countries,
//                    Employeename = ob.Employee.Name
//                });

//            // Execute the count on the filtered query
//            int totalItems = await query.CountAsync();

//            // Retrieve the list of configurations for the current page
//            List<AppOrderBonusConfigurationViewModel> configurationsViewModel = await query
//                .Skip((page - 1) * pageSize)
//                .Take(pageSize)
//                .ToListAsync();

//            // Create and populate the PaginationViewModel
//            var paginationViewModel = new PaginationViewModel<AppOrderBonusConfigurationViewModel>
//            {
//                Items = configurationsViewModel,
//                CurrentPage = page,
//                PageSize = pageSize,
//                TotalItems = totalItems
//            };

//            return Ok(paginationViewModel);
//        }



//        [HttpPost("/orderbonusesforcountries/create")]
//        [Authorize(Roles = "Admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult createorderbonusesforcountries(AppCreateOrderBonusConfigurationViewModel model)
//        {
//            if (ModelState.IsValid)
//            {
//                var orderBonusConfig = new OrderBonusConfiguration
//                {
//                    OrderThreshold = model.OrderThreshold,
//                    FlatBonusAmount = model.FlatBonusAmount,
//                    PercentageBonus = model.PercentageBonus,
//                    EmployeeId = model.EmployeeId,
//                    countries = (Countries)model.Countries,

//                };

//                _context.OrderBonusConfigurations.Add(orderBonusConfig);
//                _context.SaveChanges();

//                return Json(new { success = true, message = "Order bonus configuration created successfully" });
//            }

//            return Json(new { success = false, message = "Failed to create order bonus configuration", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
//        }


//        [HttpGet("/orderbonusesforcountries/details/{id}")]
//        [Authorize(Roles = "Admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult Getorderbonusesforcountries(int id)
//        {
//            var orderBonusConfig = _context.OrderBonusConfigurations
//                                          .Include(config => config.Employee) // Assuming there's a navigation property to the Employee entity
//                                          .FirstOrDefault(config => config.Id == id);

//            if (orderBonusConfig == null)
//            {
//                return NotFound(); // Return 404 Not Found if the order bonus configuration with the specified ID is not found
//            }

//            // Map the retrieved order bonus configuration to the view model
//            var viewModel = new AppOrderBonusConfigurationViewModel
//            {
//                Id = orderBonusConfig.Id,
//                OrderThreshold = _decimalFormattingService.DecimalFormat(orderBonusConfig.OrderThreshold),
//                FlatBonusAmount = _decimalFormattingService.DecimalFormat(orderBonusConfig.FlatBonusAmount),
//                PercentageBonus = orderBonusConfig.PercentageBonus.HasValue ? _decimalFormattingService.DecimalFormat(orderBonusConfig.PercentageBonus.Value) : null,
//                countries = orderBonusConfig.countries,
//                Employeename = orderBonusConfig.Employee?.Name // Assuming there's a Name property in the Employee entity
//            };

//            return Json(viewModel); // Return the details of the order bonus configuration as JSON
//        }


//        [HttpPut("/orderbonusesforcountries/edit/{id}")]
//        [Authorize(Roles = "Admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult EditOrderBonusConfiguration(int id, AppCreateOrderBonusConfigurationViewModel model)
//        {
//            if (ModelState.IsValid)
//            {
//                var orderBonusConfig = _context.OrderBonusConfigurations.Find(id);

//                if (orderBonusConfig == null)
//                {
//                    return NotFound(); // Return 404 Not Found if the order bonus configuration with the specified ID is not found
//                }

//                // Update the order bonus configuration with the new values
//                orderBonusConfig.OrderThreshold = model.OrderThreshold;
//                orderBonusConfig.FlatBonusAmount = model.FlatBonusAmount;
//                orderBonusConfig.PercentageBonus = model.PercentageBonus;
//                orderBonusConfig.EmployeeId = model.EmployeeId;
//                orderBonusConfig.countries = (Countries)model.Countries;

//                _context.SaveChanges();

//                return Json(new { success = true, message = "Order bonus configuration updated successfully" });
//            }

//            return Json(new { success = false, message = "Failed to update order bonus configuration", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
//        }


//        [HttpDelete("/orderbonusesforcountries/delete/{id}")]
//        [Authorize(Roles = "Admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult DeleteOrderBonusConfiguration(int id)
//        {
//            var orderBonusConfig = _context.OrderBonusConfigurations.Find(id);

//            if (orderBonusConfig == null)
//            {
//                return NotFound(); // Return 404 Not Found if the order bonus configuration with the specified ID is not found
//            }

//            _context.OrderBonusConfigurations.Remove(orderBonusConfig);
//            _context.SaveChanges();

//            return Json(new { success = true, message = "Order bonus configuration deleted successfully" });
//        }


//        [HttpGet("/employees/getalltransactions")]
//        [Authorize(Roles = "Admin,ExecutiveDirector", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<ActionResult<IEnumerable<AppEmployeeTransactionViewModel>>> GetAllEmployeeTransactions(int? employeeId, string? search = null)
//        {
//            IQueryable<EmployeeTransaction> transactionsQuery = _context.EmployeeTransactions.Include(t => t.Employee);

//            // Filter transactions based on the provided parameters
//            if (employeeId.HasValue)
//            {
//                transactionsQuery = transactionsQuery.Where(t => t.EmployeeId == employeeId);
//            }

//            if (!string.IsNullOrEmpty(search))
//            {
//                transactionsQuery = transactionsQuery.Where(t =>
//                    t.TransactionType.ToString().Contains(search) ||
//                    t.Reason.Contains(search) ||
//                    t.Employee.Name.Contains(search));
//            }

//            var transactions = await transactionsQuery.ToListAsync();

//            if (transactions == null || transactions.Count == 0)
//            {
//                return NotFound(); // Return 404 Not Found if no transactions found
//            }

//            // Map EmployeeTransaction entities to AppEmployeeTransactionViewModel
//            var transactionViewModels = transactions.Select(t => new AppEmployeeTransactionViewModel
//            {
//                Id = t.Id,
//                Amount = _decimalFormattingService.DecimalFormat(t.Amount),
//                TransactionType = t.TransactionType.ToString(),
//                TransactionTypeEnum = t.TransactionType,
//                Reason = t.Reason,
//                DateCreated = t.Date.ToString("yyyy-MM-dd"), // Convert DateTime to string in desired format
//                EmployeeId = t.EmployeeId,
//                EmployeeName = t.Employee.Name, // Assuming there's a Name property in the Employee entity
//            });

//            return Ok(transactionViewModels);
//        }



//        [HttpGet("/employees/gettransaction/{id}")]
//        [Authorize(Roles = "Admin,ExecutiveDirector", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<ActionResult<AppEmployeeTransactionViewModel>> GetEmployeeTransaction(int id)
//        {
//            var transaction = await _context.EmployeeTransactions
//                .Include(t => t.Employee)
//                .FirstOrDefaultAsync(t => t.Id == id);

//            if (transaction == null)
//            {
//                return NotFound(); // Return 404 Not Found if the transaction is not found
//            }

//            // Map EmployeeTransaction entity to AppEmployeeTransactionViewModel
//            var transactionViewModel = new AppEmployeeTransactionViewModel
//            {
//                Id = transaction.Id,
//                Amount = _decimalFormattingService.DecimalFormat(transaction.Amount),
//                TransactionType = transaction.TransactionType.ToString(),
//                TransactionTypeEnum = transaction.TransactionType,
//                Reason = transaction.Reason,
//                DateCreated = transaction.Date.ToString("yyyy-MM-dd"), // Convert DateTime to string in desired format
//                EmployeeId = transaction.EmployeeId,
//                EmployeeName = transaction.Employee?.Name, // Assuming there's a Name property in the Employee entity
//            };

//            return Ok(transactionViewModel);
//        }



//        [HttpPost("/employees/createtransaction")]
//        [Authorize(Roles = "Admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult CreateEmployeeTransaction(AppCreateEmployeeTransactionViewModel model)
//        {
//            if (ModelState.IsValid)
//            {
//                var transaction = new EmployeeTransaction
//                {
//                    Amount = (model.Amount),
//                    TransactionType = model.TransactionType,
//                    Reason = model.Reason,
//                    EmployeeId = model.EmployeeId
//                };

//                _context.EmployeeTransactions.Add(transaction);
//                _context.SaveChanges();

//                return Json(new { success = true, message = "Employee transaction created successfully" });
//            }

//            return Json(new { success = false, message = "Failed to create employee transaction", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
//        }



//        [HttpPut("/employees/edittransaction/{id}")]
//        [Authorize(Roles = "Admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult UpdateEmployeeTransaction(int id, AppCreateEmployeeTransactionViewModel model)
//        {
//            var transaction = _context.EmployeeTransactions.Find(id);

//            if (transaction == null)
//            {
//                return NotFound(); // Return 404 Not Found if the transaction is not found
//            }

//            if (ModelState.IsValid)
//            {
//                transaction.Amount = model.Amount;
//                transaction.TransactionType = model.TransactionType;
//                transaction.Reason = model.Reason;
//                transaction.EmployeeId = model.EmployeeId;

//                _context.Entry(transaction).State = EntityState.Modified;
//                _context.SaveChanges();

//                return Json(new { success = true, message = "Employee transaction updated successfully" });
//            }

//            return Json(new { success = false, message = "Failed to update employee transaction", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });
//        }


//        [HttpDelete("/employees/transaction/{id}")]
//        [Authorize(Roles = "Admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult DeleteEmployeeTransaction(int id)
//        {
//            var transaction = _context.EmployeeTransactions.Find(id);

//            if (transaction == null)
//            {
//                return NotFound(); // Return 404 Not Found if the transaction is not found
//            }

//            _context.EmployeeTransactions.Remove(transaction);
//            _context.SaveChanges();

//            return Json(new { success = true, message = "Employee transaction deleted successfully" });
//        }




//        // نهاية ادارة الموظفين


//        //// التقييمات 

//        //[HttpGet("/rating/byemployees")]
//        //[Authorize(Roles = "Admin,ExecutiveDirector", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        //public async Task<IActionResult> Employelist(
//        //   bool? gender = null,
//        //   string? selectedDateRange = null,
//        //   int? SelectedHour = null,
//        //   string? selectedAMPM = null,
//        //   string? employeeId = null,
//        //   bool? fromcomments = null)
//        //{
//        //    // Fetch employees
//        //    IEnumerable<Employee> employeeQuery = await _dataCacheService.GetCachedEmployeesAsync();

//        //    if (!string.IsNullOrEmpty(employeeId))
//        //    {
//        //        employeeQuery = employeeQuery.Where(e => e.ApplicationUserId == employeeId);
//        //    }

//        //    // Execute the query asynchronously
//        //    var employees = employeeQuery.ToList();


//        //    // Fetch all relevant OrderWarehouses entries in one query
//        //    var allRelevantOrderWarehouses = await _context.OrderWarehouses
//        //           .Include(ow => ow.Warehouse)
//        //           .ToListAsync();


//        //    // Filtering orders based on date criteriaistanbulTodayStart
//        //    // Filtering orders based on date criteria
//        //    var filteredOrdersQuery = _context.Orders.AsQueryable();

//        //    if (string.IsNullOrEmpty(selectedDateRange))
//        //    {
//        //        var now = _timeService.GetIstanbulTimeWithOffset(); // Get the current Istanbul time
//        //        DateTime istanbulTodayStart;
//        //        DateTime istanbulTodayEnd;

//        //        if (now.TimeOfDay < TimeSpan.FromHours(10))
//        //        {
//        //            // If before 10:00 AM, set start to 10:00 AM the previous day and end to 10:00 AM today
//        //            istanbulTodayStart = now.Date.AddDays(-1).AddHours(10);
//        //            istanbulTodayEnd = now.Date.AddHours(10);
//        //        }
//        //        else
//        //        {
//        //            // If after 10:00 AM, set start to 10:00 AM today and end to 10:00 AM the next day
//        //            istanbulTodayStart = now.Date.AddHours(10);
//        //            istanbulTodayEnd = now.Date.AddDays(1).AddHours(10);
//        //        }

//        //        Console.WriteLine("Start of 'today': " + istanbulTodayStart.ToString("yyyy-MM-dd HH:mm"));
//        //        Console.WriteLine("End of 'today': " + istanbulTodayEnd.ToString("yyyy-MM-dd HH:mm"));

//        //        // Apply the date range filter based on the adjusted Istanbul 'day'
//        //        filteredOrdersQuery = filteredOrdersQuery.Where(x =>
//        //            (x.InstantAddedDate >= istanbulTodayStart && x.InstantAddedDate < istanbulTodayEnd) ||
//        //            (x.FixedOrderDate >= istanbulTodayStart && x.FixedOrderDate < istanbulTodayEnd));
//        //    }



//        //    if (gender.HasValue)
//        //    {
//        //        filteredOrdersQuery = filteredOrdersQuery.Where(o => o.Gender == gender);
//        //    }
//        //    if (fromcomments.HasValue)
//        //    {
//        //        filteredOrdersQuery = filteredOrdersQuery.Where(o => o.FromComments == fromcomments);
//        //    }



//        //    if (!string.IsNullOrEmpty(selectedDateRange))
//        //    {
//        //        var dateRangeParts = selectedDateRange.Split(new[] { "إلى" }, StringSplitOptions.RemoveEmptyEntries);
//        //        if (dateRangeParts.Length == 2)
//        //        {
//        //            if (DateTime.TryParse(dateRangeParts[0].Trim(), out var startDate) &&
//        //                DateTime.TryParse(dateRangeParts[1].Trim(), out var endDate))
//        //            {
//        //                // Adjust startDate to 10:00 AM of the selected date
//        //                startDate = startDate.Date.AddHours(10);

//        //                // Adjust endDate to 10:00 AM of the day after the selected end date
//        //                endDate = endDate.Date.AddHours(10);

//        //                Console.WriteLine(startDate.ToString("yyyy-MM-dd HH:mm"));
//        //                Console.WriteLine(endDate.ToString("yyyy-MM-dd HH:mm"));

//        //                filteredOrdersQuery = filteredOrdersQuery.Where(x => (x.InstantAddedDate >= startDate && x.InstantAddedDate <= endDate) || (x.FixedOrderDate >= startDate && x.FixedOrderDate <= endDate));
//        //                if (SelectedHour.HasValue && !string.IsNullOrEmpty(selectedAMPM))
//        //                {
//        //                    int hour = SelectedHour.Value;
//        //                    // Adjust hour for 12-hour clock if needed
//        //                    if (selectedAMPM.ToLower() == "pm" && hour < 12)
//        //                    {
//        //                        hour += 12;
//        //                    }
//        //                    else if (selectedAMPM.ToLower() == "am" && hour == 12)
//        //                    {
//        //                        hour = 0;
//        //                    }
//        //                    filteredOrdersQuery = filteredOrdersQuery.Where(x => x.InstantAddedDate.Value.Hour == hour);
//        //                }
//        //            }
//        //        }
//        //    }






//        //    // Fetching necessary order data in one go
//        //    var orderData = filteredOrdersQuery
//        //            .Include(o => o.OrderWarehouses)
//        //            .Select(o => new
//        //            {
//        //                o.ApplicationUserId,
//        //                o.Id,
//        //                o.TotalPrice,
//        //                o.DeliveryCompanyId,
//        //                o.Country,
//        //                o.State,
//        //                o.OrderStatus,
//        //                o.ExternalOrderId,
//        //                HasWarehouseWithMoreThanOneItem = o.OrderWarehouses.Any(ow => ow.Amount > 1),
//        //                HasMoreThanOneWarehouse = o.OrderWarehouses.Count() > 1,
//        //                OrderWarehouses = o.OrderWarehouses.Select(ow => new { ow.WarehouseId, ow.Warehouse.Name, ow.Amount }).ToList()

//        //            })
//        //            .ToList();
//        //    // After fetching orderData, find order ID 7048
//        //    var order7048 = orderData.FirstOrDefault(o => o.Id == 7048);

//        //    if (order7048 != null)
//        //    {
//        //        Console.WriteLine($"Order ID: {order7048.Id}, Application User ID: {order7048.ApplicationUserId}, Total Price: {order7048.TotalPrice}, Delivery Company ID: {order7048.DeliveryCompanyId}, Country: {order7048.Country}, State: {order7048.State}, Order Status: {order7048.OrderStatus}, External Order ID: {order7048.ExternalOrderId}");

//        //        // Check if it has any warehouse with more than one item or involves more than one warehouse
//        //        bool hasWarehouseWithMoreThanOneItem = order7048.OrderWarehouses.Any(ow => ow.Amount > 1);
//        //        bool hasMoreThanOneWarehouse = order7048.OrderWarehouses.Select(ow => ow.WarehouseId).Distinct().Count() > 1;

//        //        Console.WriteLine($"Has Warehouse With More Than One Item: {hasWarehouseWithMoreThanOneItem}");
//        //        Console.WriteLine($"Has More Than One Warehouse: {hasMoreThanOneWarehouse}");

//        //        // Log details of each warehouse in this order
//        //        foreach (var warehouse in order7048.OrderWarehouses)
//        //        {
//        //            // Print out details for each OrderWarehouse
//        //            Console.WriteLine($"Warehouse ID: {warehouse.WarehouseId}, Name: {warehouse.Name}, Amount: {warehouse.Amount}");
//        //        }
//        //    }
//        //    else
//        //    {
//        //        Console.WriteLine("Order ID 7048 not found.");
//        //    }

//        //    var orderFromComments = filteredOrdersQuery.Where(o => o.FromComments).ToList();


//        //    // number of faild orders 
//        //    var failedDeliveryOrders = filteredOrdersQuery
//        //     .Where(o => o.OrderStatus == OrderStatusEnum.فشل_التسليم ||
//        //                 o.OrderStatus == OrderStatusEnum.انتظار_المعالجة ||
//        //                 o.OrderStatus == OrderStatusEnum.الطلبات_المرجعة ||
//        //                 o.OrderStatus == OrderStatusEnum.تم_الإلغاء)
//        //     .ToList();

//        //    // Filter for delivered orders
//        //    var deliveredOrders = filteredOrdersQuery
//        //        .Where(o => o.OrderStatus == OrderStatusEnum.تم_التسليم ||
//        //                    o.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد ||
//        //                    o.OrderStatus == OrderStatusEnum.تم_الدفع)
//        //        .ToList();
//        //    // Initialize an empty dictionary to hold external order details



//        //    // Fetch delivery company prices
//        //    var deliveryCompanyPrices = _context.DeliveryCompanyPrices
//        //        .ToList()
//        //        .GroupBy(dcp => new { dcp.DeliveryCompanyId, dcp.Country, dcp.City })
//        //        .ToDictionary(
//        //            g => g.Key,
//        //            g => g.FirstOrDefault()?.Price ?? 0
//        //        );

//        //    // Collect all unique ExternalOrderId

//        //    int totalOrdersHandled = orderData.Count;

//        //    var employeeExternalOrdersRequests = employees.Select(employee => new EmployeeOrderIdsRequest
//        //    {
//        //        EmployeeId = employee.Id.ToString(), // Make sure the ID is in the correct format
//        //        ExternalOrderIds = filteredOrdersQuery
//        // .Where(o => o.ApplicationUserId == employee.ApplicationUserId && o.ExternalOrderId.HasValue)
//        // .Select(o => o.ExternalOrderId.Value) // Ensure this is a non-nullable int
//        // .ToList()
//        //    }).ToList();

//        //    var employeeIdToOrderIdsMap = employeeExternalOrdersRequests.ToDictionary(
//        //            keySelector: request => request.EmployeeId,
//        //            elementSelector: request => request.ExternalOrderIds
//        //        );



//        //    // Ensure the response is successful or handle errors/failed status codes appropriately


//        //    // Now you can deserialize the JSON string to your object



//        //    var employeeRating = employees.Select(e => new EmployeNameAndId
//        //    {
//        //        Id = e.ApplicationUserId,
//        //        Name = e.Name
//        //    }).Distinct().ToList();


//        //    // Creating a list of employee ratings by transforming each employee in the employees collection.
//        //    var employeeRatings = employees
//        //        .Where(employee =>
//        //            orderData.Any(order => order.ApplicationUserId == employee.ApplicationUserId))
//        //        .Select(employee =>
//        //        {
//        //            {
//        //                int failedDeliveryOrdersCount = 0, deliveredOrdersCount = 0;
//        //                decimal failedDeliveryOrdersPriceUSD = 0, deliveredOrdersPriceUSD = 0;

//        //                // Filtering orders related to the current employee and converting the result into a List.
//        //                var employeeOrders = orderData
//        //                        .Where(o => o.ApplicationUserId == employee.ApplicationUserId)
//        //                        .ToList();/* Console.WriteLine($"Found {employeeOrders.Count} employee orders for user {specificUserId}.");*/


//        //                int totalProductsCount = 0;
//        //                int totalOrdersWithWarehouseItemMoreThanOne = 0;






//        //                var internalOrders = employeeOrders
//        //                    .SelectMany(eo => eo.OrderWarehouses)
//        //                     .Select(ow => new { ow.WarehouseId, ow.Name, ow.Amount })
//        //                    .ToList();


//        //                // Combine internal and external orders
//        //                var combinedOrders = internalOrders.ToList();



//        //                var ordersWithOffers = employeeOrders.Where(o =>
//        //                   (o.OrderWarehouses.Any(ow => ow.Amount > 1) || // Any warehouse with more than one item
//        //                   o.OrderWarehouses.GroupBy(ow => ow.WarehouseId).Count() > 1) // Involves more than one warehouse
//        //               && o.ExternalOrderId == null).ToList();


//        //                // Print details of each order that qualifies as an offer
//        //                foreach (var order in ordersWithOffers)
//        //                {
//        //                    Console.WriteLine($"Order ID: {order.Id}, Application User ID: {order.ApplicationUserId},  External Order ID: {order.ExternalOrderId}");
//        //                }

//        //                // Calculate the total number of offers for logging or further processing
//        //                var totalOffers = ordersWithOffers.Count;


//        //                // Calculate the total number of orders handled by this employee
//        //                int employeeTotalOrders = employeeOrders.Count(eo => eo.ApplicationUserId == employee.ApplicationUserId);






//        //                // Calculate the number of failed delivery orders
//        //                failedDeliveryOrdersCount = failedDeliveryOrders.Count;

//        //                // Summing up prices for failed delivery orders
//        //                failedDeliveryOrdersPriceUSD = failedDeliveryOrders
//        //                    .Sum(o =>
//        //                    {
//        //                        var key = new { o.DeliveryCompanyId, Country = o.Country, City = o.State };
//        //                        var deliveryPrice = deliveryCompanyPrices.TryGetValue(key, out var price) ? price : 0;
//        //                        return _currencyExchangeService.ConvertToUSD(o.TotalPrice - deliveryPrice, o.Country.ToString());
//        //                    });


//        //                // Calculate the number of delivered orders
//        //                deliveredOrdersCount = deliveredOrders.Count;

//        //                // Summing up prices for delivered orders
//        //                deliveredOrdersPriceUSD = deliveredOrders
//        //                    .Sum(o =>
//        //                    {
//        //                        var key = new { o.DeliveryCompanyId, Country = o.Country, City = o.State };
//        //                        var deliveryPrice = deliveryCompanyPrices.TryGetValue(key, out var price) ? price : 0;
//        //                        return _currencyExchangeService.ConvertToUSD(o.TotalPrice - deliveryPrice, o.Country.ToString());
//        //                    });

//        //                var failedDeliveryOrdersPriceTRY = _currencyExchangeService.ConvertToTurkishLira((decimal)failedDeliveryOrdersPriceUSD);
//        //                var deliveredOrdersPriceTRY = _currencyExchangeService.ConvertToTurkishLira((decimal)deliveredOrdersPriceUSD);





//        //                // Calculating the total price of all orders in USD.
//        //                // This is done by summing up the converted order prices after deducting the delivery prices.
//        //                decimal totalOrdersPriceInUSD = employeeOrders.Sum(order =>
//        //                {
//        //                    var key = new { DeliveryCompanyId = order.DeliveryCompanyId, Country = order.Country, City = order.State };
//        //                    var deliveryPrice = deliveryCompanyPrices.TryGetValue(key, out var price) ? price : 0;

//        //                    return _currencyExchangeService.ConvertToUSD(order.TotalPrice - deliveryPrice, order.Country.ToString());
//        //                });



//        //                // Getting the total price in Turkish TL
//        //                decimal totalOrdersPriceInTRY = _currencyExchangeService.ConvertToTurkishLira(totalOrdersPriceInUSD);


//        //                // Ensure to avoid division by zero
//        //                decimal rating = totalOrdersHandled > 0 ? ((decimal)employeeTotalOrders / totalOrdersHandled) * 100 : 0;

//        //                // Count the items
//        //                // Count the items
//        //                var queenOrdersCount = combinedOrders.Count(ow => ow.Name.StartsWith("فاونديشن كوين"));
//        //                var roziOrdersCount = combinedOrders.Count(ow => ow.Name.StartsWith("فاونديشن روز"));
//        //                var powderOrdersCount = combinedOrders.Count(ow => ow.Name.StartsWith("توب باودر"));
//        //                var royalOrdersCount = combinedOrders.Count(ow => ow.Name.StartsWith("فاونديشن رويال"));
//        //                var vilveltOrdersCount = combinedOrders.Count(ow => ow.Name.StartsWith("كريم فلفيت الطبي"));
//        //                var pansiOrdersCount = combinedOrders.Count(ow => ow.Name.StartsWith("كريم بانسي الطبي"));
//        //                var brushOrdersCount = combinedOrders.Count(ow => ow.Name.StartsWith("فرشاة"));
//        //                var mascaraOrdersCount = combinedOrders.Count(ow => ow.Name.StartsWith("ماسكارا"));

//        //                totalProductsCount = queenOrdersCount + roziOrdersCount + powderOrdersCount + royalOrdersCount + vilveltOrdersCount + pansiOrdersCount
//        //               + brushOrdersCount + mascaraOrdersCount;


//        //                // Creating a new RatingViewModel object for the employee with the calculated values.
//        //                return new AppRatingViewmodel
//        //                {
//        //                    Id = employee.ApplicationUserId,
//        //                    Name = employee.Name,
//        //                    OrdersCount = employeeOrders.Count,
//        //                    TotalProductsCount = totalProductsCount,
//        //                    TotalOrdersWithWarehouseItemMoreThanOne = totalOffers,
//        //                    TotalPriceUSD = _decimalFormattingService.DecimalFormat(totalOrdersPriceInUSD),
//        //                    TotalPriceTRY = _decimalFormattingService.DecimalFormat(totalOrdersPriceInTRY),
//        //                    QueenProductCount = queenOrdersCount,
//        //                    RoziProductCount = roziOrdersCount,
//        //                    PowderProductCount = powderOrdersCount,
//        //                    RoyalProductCount = royalOrdersCount,
//        //                    VilveltCreamProductCount = vilveltOrdersCount,
//        //                    PansiCreamProductCount = pansiOrdersCount,
//        //                    BrushProductCount = brushOrdersCount,
//        //                    MascaraProductCount = mascaraOrdersCount,
//        //                    Rating = _decimalFormattingService.DecimalFormat(rating),
//        //                    FailedDeliveryOrdersCount = failedDeliveryOrdersCount,
//        //                    DeliveredOrdersCount = deliveredOrdersCount,
//        //                    FailedDeliveryOrdersPriceUSD = (failedDeliveryOrdersPriceUSD),
//        //                    FailedDeliveryOrdersPriceTRY = (failedDeliveryOrdersPriceTRY),
//        //                    DeliveredOrdersPriceUSD = (deliveredOrdersPriceUSD),
//        //                    DeliveredOrdersPriceTRY = (deliveredOrdersPriceTRY),
//        //                    FailedDeliverPercentage = (deliveredOrders.Count > 0
//        //                    ? ((decimal)failedDeliveryOrdersCount / filteredOrdersQuery.Count()) * 100
//        //                    : 0m),

//        //                    DeliveredPercentage = (deliveredOrders.Count > 0
//        //                    ? ((decimal)deliveredOrdersCount / filteredOrdersQuery.Count()) * 100
//        //                    : 0m),

//        //                    OrderFromComments = orderFromComments.Count(),
//        //                };
//        //            }
//        //        }).ToList(); // Converting the resulting IEnumerable to a List.

//        //    // Pass the list of employee ratings to the view for rendering.
//        //    return Json(employeeRatings);
//        //}



//        //[HttpGet("/rating/bystores")]
//        //[Authorize(Roles = "Admin,ExecutiveDirector", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        //public async Task<IActionResult> ManufactureCompanyDetails(
//        //    string? employeeId = null,
//        //    string? productId = null,
//        //    int? storeId = null,
//        //    Common.Countries? countryId = null,
//        //    string? sourceId = null,
//        //    bool? gender = null,
//        //    DateTime? fromDate = null,
//        //    DateTime? tillDate = null)
//        //{
//        //    var storeToCompanyAndCountryMapping = new Dictionary<int, (int companyId, Countries country)>
//        //    {
//        //        { 40, (1, Countries.العراق) },
//        //        { 38, (2, Countries.العراق) },
//        //    };


//        //    var ordersQuery = _context.Orders
//        //        .AsNoTracking();


//        //    if (gender.HasValue)
//        //    {
//        //        ordersQuery = ordersQuery.Where(o => o.Gender == gender);
//        //    }

//        //    var now = _timeService.GetIstanbulTimeWithOffset();
//        //    if (!fromDate.HasValue || !tillDate.HasValue)
//        //    {
//        //        // Set default values if fromDate and tillDate are not provided
//        //        fromDate = now.Date.AddHours(10); // Start of the day at 10 AM
//        //        tillDate = now.Date.AddDays(1).AddHours(10); // Till 10 AM the next day
//        //    }


//        //    ordersQuery = ordersQuery.Where(x => x.InstantAddedDate >= fromDate && x.InstantAddedDate < tillDate);


//        //    if (!string.IsNullOrWhiteSpace(employeeId))
//        //        ordersQuery = ordersQuery.Where(order => order.ApplicationUserId == employeeId);


//        //    if (storeId.HasValue)
//        //        ordersQuery = ordersQuery.Where(order => order.ManufacturingCompanyId == storeId.Value);

//        //    if (countryId != null && !string.IsNullOrEmpty(countryId.ToString()))
//        //    {
//        //        if (Enum.TryParse(typeof(Common.Countries), countryId.ToString(), out var countryEnum))
//        //        {
//        //            ordersQuery = ordersQuery.Where(order => order.Country == (Common.Countries)countryEnum);
//        //        }

//        //    }


//        //    if (sourceId != null && !string.IsNullOrEmpty(sourceId))
//        //    {
//        //        if (Enum.TryParse(typeof(OrderSourceEnum), sourceId.ToString(), out var parsedOrderSource))
//        //        {
//        //            // Corrected type cast
//        //            ordersQuery = ordersQuery.Where(order => order.OrderSource == (OrderSourceEnum)parsedOrderSource);
//        //        }
//        //    }




//        //    var allOrders = await ordersQuery
//        //        .Select(order => new OrderBySocialMediaCampaignsViewModel
//        //        {
//        //            ManufacturingCompanyId = order.ManufacturingCompanyId,
//        //            ManufacturingCompanyName = order.ManufacturingCompany.Name,
//        //            StoreName = null, // You can set this based on your logic
//        //            Country = order.Country,
//        //            OrderSource = order.OrderSource,
//        //            TotalPrice = order.TotalPrice,
//        //            CreatedDate = order.CreatedDate,

//        //        })
//        //        .ToListAsync();



//        //    // Reassign orders from specific stores to the specified manufacturing companies and countries
//        //    foreach (var order in allOrders)
//        //    {
//        //        if (order.StoreIdFromSbs.HasValue && storeToCompanyAndCountryMapping.TryGetValue(order.StoreIdFromSbs.Value, out var mapping))
//        //        {
//        //            order.ManufacturingCompanyId = mapping.companyId;
//        //            order.Country = mapping.country;
//        //        }
//        //    }

//        //    // Group orders by manufacturing company
//        //    var groupedByCompany = allOrders
//        //         .Where(order => order.ManufacturingCompanyId.HasValue && (!storeId.HasValue || order.ManufacturingCompanyId == storeId.Value)) // Double-check this condition
//        //         .GroupBy(order => order.ManufacturingCompanyId.Value)
//        //         .Select(group => new
//        //         {
//        //             CompanyId = group.Key,
//        //             Orders = group.ToList(),
//        //             TotalPriceUSD = group.Sum(order => _currencyExchangeService.ConvertToUSD(order.TotalPrice, order.Country.ToString())),
//        //             TotalPriceTRY = _decimalFormattingService.DecimalFormat(
//        //                _currencyExchangeService.ConvertToTurkishLira(group.Sum(order => _currencyExchangeService.ConvertToUSD(order.TotalPrice, order.Country.ToString()))))

//        //         });

//        //    var manufacturingCompanyDetails = await _context.ManufacturingCompanies
//        //         .Where(mc => !storeId.HasValue || mc.Id == storeId.Value) // Correct filtering
//        //         .ToDictionaryAsync(mc => mc.Id, mc => mc);
//        //    // Convert manufacturingCompanyDetails to SelectListItem
//        //    var manufacturingCompaniesSelectList = manufacturingCompanyDetails
//        //.Select(kvp => new SelectListItem
//        //{
//        //    Value = kvp.Key.ToString(),
//        //    Text = kvp.Value.Name
//        //})
//        //.Distinct() // Ensure distinct entries based on Value or Text
//        //.ToList();

//        //    var countryImageUrls = Enum.GetValues(typeof(Countries))
//        //                  .Cast<Countries>()
//        //                  .ToDictionary(
//        //                      country => country.ToString(), // Use the string representation of the enum
//        //                      country => Common.GetImageUrlByCountryName(country.ToString())
//        //                  );
//        //    var socialMediaIconUrls = Enum.GetValues(typeof(OrderSourceEnum))
//        //                       .Cast<OrderSourceEnum>()
//        //                       .ToDictionary(
//        //                           orderSource => orderSource.ToString(), // Use the order source as the key
//        //                           orderSource => Common.GetSocialMediaIconUrl(orderSource) // Get the URL for the order source icon
//        //                       );



//        //    var currencySymbols = Enum.GetValues(typeof(Countries))
//        //          .Cast<Countries>()
//        //          .Distinct() // Remove duplicates, if any
//        //          .ToDictionary(
//        //              country => country.ToString(), // Convert the country enum to a string
//        //              country => Common.GetCurrencyByCountryName(country.ToString()) // Get the currency symbol for the country as a string
//        //          );

//        //    var employeesList = await _context.Employees
//        //         .Select(e => new EmployeNameAndId
//        //         {
//        //             Id = e.ApplicationUserId,
//        //             Name = e.Name
//        //         }).ToListAsync();

//        //    // Calculate sums for total orders, totalPriceUSD, and totalPriceTRY
//        //    int totalOrdersSum = allOrders.Count();
//        //    decimal totalPriceUSDSum = allOrders.Sum(order => _currencyExchangeService.ConvertToUSD(order.TotalPrice, order.Country.ToString()));
//        //    decimal totalPriceTRYSum = _currencyExchangeService.ConvertToTurkishLira(totalPriceUSDSum);
//        //    var result = new List<AppManufacturingCompanyOrderDetailsViewModel>();
//        //    // grouping by manafacture company
//        //    foreach (var companyGroup in groupedByCompany)
//        //    {
//        //        if (manufacturingCompanyDetails.TryGetValue(companyGroup.CompanyId, out var companyDetails))
//        //        {
//        //            var countriesOrderInfo = companyGroup.Orders
//        //                     .GroupBy(order => order.Country)
//        //                     .Select(countryGroup =>
//        //                     {
//        //                         // Calculate the total price in USD for each order source and sum those amounts
//        //                         var totalPriceBySourceUSD = countryGroup
//        //                             .GroupBy(o => o.OrderSource)
//        //                             .ToDictionary(
//        //                                 srcGroup => srcGroup.Key,
//        //                                 srcGroup => srcGroup.Sum(order => _currencyExchangeService.ConvertToUSD(order.TotalPrice, order.Country.ToString()))
//        //                             );

//        //                         // Convert the summed total price in USD to TRY for each order source
//        //                         var totalPriceBySourceTRY = totalPriceBySourceUSD
//        //                             .ToDictionary(
//        //                                 pair => pair.Key,
//        //                                 pair => _decimalFormattingService.DecimalFormat(_currencyExchangeService.ConvertToTurkishLira(pair.Value)) + " TRY"
//        //                             );

//        //                         var countryOrderInfo = new AppCountryOrderInfo
//        //                         {
//        //                             Country = countryGroup.Key,
//        //                             Currency = Common.GetCurrencyByCountryName(countryGroup.Key.ToString()),
//        //                             TotalOrders = countryGroup.Count(),
//        //                             OrdersBySource = countryGroup
//        //             .GroupBy(o => (int)o.OrderSource)
//        //             .ToDictionary(
//        //                 srcGroup => (int)srcGroup.Key,
//        //                 srcGroup => srcGroup.Count()
//        //             ),

//        //                             TotalPriceLocalCurrency = countryGroup
//        //             .GroupBy(o => o.OrderSource)
//        //             .ToDictionary(srcGroup => (int)srcGroup.Key,
//        //                           srcGroup => _decimalFormattingService.DecimalFormat(srcGroup.Sum(order => order.TotalPrice))),

//        //                             TotalPriceBySourceUSD = totalPriceBySourceUSD
//        //             .ToDictionary(pair => (int)pair.Key,
//        //                           pair => _decimalFormattingService.DecimalFormat(pair.Value)),

//        //                             TotalPriceBySourceTRY = totalPriceBySourceTRY
//        //                    .ToDictionary(pair => (int)pair.Key,
//        //                                  pair =>
//        //                                  {
//        //                                      decimal.TryParse(pair.Value.Replace(" TRY", ""), out decimal result);
//        //                                      return _decimalFormattingService.DecimalFormat(result);
//        //                                  }


//        //                         )
//        //                         };


//        //                         // Calculate totals
//        //                         // Calculate totals
//        //                         countryOrderInfo.TotalOrdersCount = countryOrderInfo.OrdersBySource.Sum(os => os.Value);
//        //                         countryOrderInfo.TotalLocalCurrencyPriceSum = _decimalFormattingService.DecimalFormat(
//        //                             countryOrderInfo.TotalPriceLocalCurrency.Values.Sum(price => Decimal.Parse(price.Replace(",", "")))
//        //                         );
//        //                         countryOrderInfo.TotalTryPriceSum = _decimalFormattingService.DecimalFormat(
//        //                             countryOrderInfo.TotalPriceBySourceTRY.Values.Sum(price => Decimal.Parse(price.Replace(" TRY", "").Replace(",", "")))
//        //                         );
//        //                         countryOrderInfo.TotalUsdPriceSum = _decimalFormattingService.DecimalFormat(
//        //                             countryOrderInfo.TotalPriceBySourceUSD.Values.Sum(price => Decimal.Parse(price.Replace(" USD", "").Replace(",", "")))
//        //                         );


//        //                         return countryOrderInfo;
//        //                     }).ToList();



//        //            var manufacturingCompanyOrderDetailsViewModel = new AppManufacturingCompanyOrderDetailsViewModel
//        //            {
//        //                CompanyId = companyGroup.CompanyId,
//        //                CompanyName = companyDetails.Name, // Get the name from the dictionary
//        //                companyimage = companyDetails.ImageUrl,
//        //                CountriesOrderInfo = countriesOrderInfo,
//        //                TotalOrders = companyGroup.Orders.Count(),
//        //                TotalPriceUSD = _decimalFormattingService.DecimalFormat(companyGroup.TotalPriceUSD),
//        //                TotalPriceTRY = _decimalFormattingService.DecimalFormat(
//        //                _currencyExchangeService.ConvertToTurkishLira(companyGroup.TotalPriceUSD)

//        //            ),
//        //            };

//        //            if (!storeId.HasValue || companyGroup.CompanyId == storeId.Value)
//        //            {
//        //                result.Add(manufacturingCompanyOrderDetailsViewModel);
//        //            }





//        //        }
//        //    }
//        //    var totalSummaryViewModel = new AppManufacturingCompanyOrderDetailsViewModel
//        //    {
//        //        TotalOrders = totalOrdersSum,
//        //        TotalPriceUSD = _decimalFormattingService.DecimalFormat(totalPriceUSDSum),
//        //        TotalPriceTRY = _decimalFormattingService.DecimalFormat(totalPriceTRYSum)
//        //    };
//        //    result.Add(totalSummaryViewModel);

//        //    return Json(result);
//        //}

//        [HttpGet("/rating/bycountries")]
//        [Authorize(Roles = "Admin,ExecutiveDirector", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> CityByCountryDetails(
//        string? selectedDateRange = null,
//        bool? gender = null,
//        string? employeeUserId = null,
//        Countries? country = null
//        )
//        {
//            var ordersQuery = _context.Orders.AsNoTracking();


//            if (gender.HasValue)
//            {
//                ordersQuery = ordersQuery.Where(o => o.Gender == gender);
//            }

//            if (!string.IsNullOrEmpty(selectedDateRange))
//            {
//                var dateRangeParts = selectedDateRange.Split(new[] { "إلى" }, StringSplitOptions.RemoveEmptyEntries);
//                if (dateRangeParts.Length == 2)
//                {
//                    if (DateTime.TryParse(dateRangeParts[0].Trim(), out var startDate) &&
//                        DateTime.TryParse(dateRangeParts[1].Trim(), out var endDate))
//                    {
//                        // Adjust startDate to the beginning of the day
//                        // Adjust startDate to 10:00 AM of the selected date
//                        // Adjust startDate to 10:00 AM of the selected date
//                        startDate = startDate.Date.AddHours(10);
//                        // Adjust endDate to 10:00 AM of the day after the selected end date
//                        endDate = endDate.Date.AddHours(10);

//                        ordersQuery = ordersQuery.Where(x => (x.InstantAddedDate >= startDate && x.InstantAddedDate <= endDate) || (x.FixedOrderDate >= startDate && x.FixedOrderDate <= endDate));

//                    }
//                }
//            }

//            if (!string.IsNullOrWhiteSpace(employeeUserId))
//                ordersQuery = ordersQuery.Where(order => order.ApplicationUserId == employeeUserId);
//            if (country.HasValue)
//                ordersQuery = ordersQuery.Where(order => order.Country == country.Value);
//            var allOrders = await ordersQuery
//                .Select(order => new OrderBySocialMediaCampaignsViewModel
//                {
//                    State = order.State,
//                    Country = order.Country,
//                    TotalPrice = order.TotalPrice,
//                    OrderSource = order.OrderSource
//                })
//                .ToListAsync();

//            // Calculate total number of orders across all countries for percentage calculation
//            var totalOrdersAllCountries = allOrders.Count;


//            var groupedByCountry = allOrders
//                .GroupBy(order => order.Country)
//                .Select(countryGroup =>
//                {
//                    var totalOrdersInCountry = countryGroup.Count();

//                    // Calculate percentage of total orders for this country
//                    var percentageOfAllOrders = totalOrdersAllCountries > 0 ?
//                        (decimal)totalOrdersInCountry / totalOrdersAllCountries * 100 : 0;

//                    var citiesOrderInfo = CitiesByCountry[countryGroup.Key].Select(city =>
//                    {
//                        var totalOrdersInCity = countryGroup.Count(o => o.State == city);
//                        var percentageOfTotalOrders = totalOrdersInCountry > 0 ?
//                            (decimal)totalOrdersInCity / totalOrdersInCountry * 100 : 0;

//                        return new CityOrderInfo
//                        {
//                            CityName = city,
//                            TotalOrders = totalOrdersInCity,
//                            PercentageOfTotalOrders = _decimalFormattingService.DecimalFormat(percentageOfTotalOrders),

//                            OrdersBySource = countryGroup.Where(o => o.State == city).GroupBy(o => o.OrderSource)
//                                .ToDictionary(srcGroup => srcGroup.Key, srcGroup => srcGroup.Count()),
//                            TotalPriceBySourceUSD = countryGroup.Where(o => o.State == city).GroupBy(o => o.OrderSource)
//                                .ToDictionary(srcGroup => srcGroup.Key, srcGroup => srcGroup.Sum(order => _currencyExchangeService.ConvertToUSD(order.TotalPrice, order.Country.ToString())))
//                        };
//                    }).ToList();




//                    return new CountryOrderDetailsViewModel
//                    {
//                        Country = countryGroup.Key,
//                        CitiesOrderInfo = citiesOrderInfo,
//                        TotalOrders = totalOrdersInCountry,
//                        TotalPriceUSD = countryGroup.Sum(order => _currencyExchangeService.ConvertToUSD(order.TotalPrice, order.Country.ToString())),
//                        PercentageOfAllOrders = _decimalFormattingService.DecimalFormat(percentageOfAllOrders), // Add this property to the ViewModel if it doesn't exist
//                        PercentageOfAllOrdersdecimal = (percentageOfAllOrders) // Add this property to the ViewModel if it doesn't exist

//                    };
//                }).ToList();

//            return Json(groupedByCountry);
//        }

//        // نهاية التقييمات

//        // شركات اتلوصيل 
//        [HttpGet("/deliverycompany/getall")]
//        [Authorize(Roles = "Admin,Accountant,Observer,ExecutiveDirector,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult deliverycompanylist(string? search = null, int page = 1, int pageSize = 10)
//        {
//            IQueryable<AppDeliveryCompanyViewModel> query = _context.DeliveryCompanies
//                .Where(a => !a.IsRepresentative)
//                .Include(a => a.User)
//                .Select(e => new AppDeliveryCompanyViewModel
//                {
//                    Id = e.Id,
//                    LogoUrl = e.ImageUrl,
//                    Name = e.Name,
//                    PhoneNumber = e.PhoneNumber,
//                    Email = e.User.Email,
//                    Specialty = e.specialty,
//                    Country = e.Country,
//                    IsShown = e.IsShown,
//                    IsActive = e.User.EmailConfirmed
//                });

//            // Filtering by name if search parameter is provided
//            if (!string.IsNullOrEmpty(search))
//            {
//                query = query.Where(a => a.Name.Contains(search));
//            }

//            // Filtering by country if search parameter is provided
//            if (Enum.TryParse(typeof(Countries), search, out object countryObj) && countryObj is Countries country)
//            {
//                query = query.Where(a => a.Country == country);
//            }

//            int totalItems = query.Count();

//            var viewModel = new PaginationViewModel<AppDeliveryCompanyViewModel>
//            {
//                Items = query.Skip((page - 1) * pageSize)
//                             .Take(pageSize)
//                             .ToList(),
//                CurrentPage = page,
//                PageSize = pageSize,
//                TotalItems = totalItems
//            };

//            return Json(viewModel);
//        }

//        [HttpPost("/deliverycompanies/create")]
//        [Authorize(Roles = "Admin,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> deliverycompanyCreate([FromForm] AppCreateDeliveryCompanyViewModel model, IFormFile logoFile, IFormFile infoFile)
//        {

//            if (!ModelState.IsValid)
//            {
//                // Return a structured JSON response with model state errors
//                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
//                return BadRequest(new { success = false, errors = errors });
//            }
//            // Create the ApplicationUser
//            var user = new ApplicationUser
//            {
//                UserName = model.Email,
//                Email = model.Email,
//                Name = model.Name,
//            };

//            // Create the user and add them to the "DeliveryCompany" role
//            var result = await _userManager.CreateAsync(user, model.Password);
//            if (result.Succeeded)
//            {
//                await _userManager.AddToRoleAsync(user, "DeliveryCompany");

//                // Create the DeliveryCompany
//                var deliveryCompany = new DeliveryCompany
//                {
//                    Name = model.Name,
//                    TaxRegistrationNumber = model.TaxRegistrationNumber,
//                    IdNumber = model.IdNumber,
//                    Address = model.Address,
//                    PhoneNumber = model.PhoneNumber,
//                    specialty = model.Specialty,
//                    Website = model.Website,
//                    Notes = model.Notes,
//                    CreatedDate = _timeService.GetIstanbulTimeWithOffset(),
//                    Country = model.SelectedCountry,
//                    UserId = user.Id, // This is where you set the UserId
//                    IsRepresentative = false,

//                };

//                // Save the logo and information files
//                if (logoFile != null)
//                    deliveryCompany.ImageUrl = await _fileUploadService.UploadFileAsync(logoFile, "deliverycompanies");

//                if (infoFile != null)
//                    deliveryCompany.InformationUrl = await _fileUploadService.UploadFileAsync(infoFile, "deliverycompanies");

//                // Add and save the DeliveryCompany
//                _context.Add(deliveryCompany);
//                await _context.SaveChangesAsync();

//                // Assign the AccessId here based on the newly created DeliveryCompany's Id
//                user.AcessId = deliveryCompany.Id;


//                // Update the user with the AccessId
//                await _userManager.UpdateAsync(user);

//                // Redirect to a success page or perform any other necessary actions
//                return Json("delivery company added");
//            }
//            else
//            {
//                var errors = result.Errors.Select(e => e.Description);

//                // Handle user registration errors
//                return Json(new { success = false, errors = errors });
//            }

//        }

//        [HttpPut("/deliverycompanies/edit/{id}")]
//        [Authorize(Roles = "Admin,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> deliverycompanyEdit(int id, [FromForm] AppEditDeliveryCompanyViewModel model, IFormFile? logoFile, IFormFile? infoFile)
//        {
//            if (!ModelState.IsValid)
//            {
//                return BadRequest(ModelState);
//            }

//            if (id != model.Id)
//            {
//                return BadRequest("Invalid id provided.");
//            }

//            var existingCompany = await _context.DeliveryCompanies
//                .Include(a => a.User)
//                .FirstOrDefaultAsync(c => c.Id == id);
//            if (existingCompany == null)
//            {
//                return NotFound("Delivery company not found.");
//            }

//            // Update the existingCompany properties
//            existingCompany.Name = model.Name;
//            existingCompany.TaxRegistrationNumber = model.TaxRegistrationNumber;
//            existingCompany.IdNumber = model.IdNumber;
//            existingCompany.Address = model.Address;
//            existingCompany.PhoneNumber = model.PhoneNumber;
//            existingCompany.specialty = model.Specialty;
//            existingCompany.Website = model.Website;
//            existingCompany.Notes = model.Notes;
//            existingCompany.Country = model.SelectedCountry;

//            if (logoFile != null)
//            {
//                existingCompany.ImageUrl = await _fileUploadService.UpdateFileAsync(existingCompany.ImageUrl, logoFile, "deliverycompanies");
//            }

//            if (infoFile != null)
//            {
//                existingCompany.InformationUrl = await _fileUploadService.UpdateFileAsync(existingCompany.InformationUrl, infoFile, "deliverycompanies");
//            }

//            // Change password if NewPassword is provided
//            if (!string.IsNullOrWhiteSpace(model.NewPassword))
//            {
//                if (model.NewPassword != model.ConfirmNewPassword)
//                {
//                    return BadRequest("New password and confirmation password do not match.");
//                }

//                var user = await _userManager.FindByIdAsync(existingCompany.UserId);
//                if (user != null)
//                {
//                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
//                    var passwordChangeResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

//                    if (!passwordChangeResult.Succeeded)
//                    {
//                        // Handle errors
//                        return BadRequest("Failed to reset password.");
//                    }
//                }
//            }

//            // Save changes to the database
//            await _context.SaveChangesAsync();
//            return Ok("Delivery company updated successfully.");
//        }


//        [HttpGet("/deliverycompanies/details/{id}")]
//        [Authorize(Roles = "Admin,Accountant,Observer,ExecutiveDirector,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult deliverycompanyDetailsById(int id)
//        {
//            var deliveryCompany = _context.DeliveryCompanies
//                .Include(a => a.User)
//                .FirstOrDefault(e => e.Id == id && !e.IsRepresentative);

//            if (deliveryCompany == null)
//            {
//                return NotFound("Delivery company not found.");
//            }

//            var details = new AppDeliveryCompanyViewModel
//            {
//                Id = deliveryCompany.Id,
//                LogoUrl = deliveryCompany.ImageUrl,
//                Name = deliveryCompany.Name,
//                PhoneNumber = deliveryCompany.PhoneNumber,
//                Email = deliveryCompany.User.Email,
//                Specialty = deliveryCompany.specialty,
//                Country = deliveryCompany.Country,
//                Notes = deliveryCompany.Notes,
//                TaxRegistrationNumber = deliveryCompany.TaxRegistrationNumber,
//                IdNumber = deliveryCompany.IdNumber,
//                Address = deliveryCompany.Address,

//            };

//            return Json(details);
//        }


//        [HttpGet("/deliverycompanyprices/index")]
//        [Authorize(Roles = "Admin,DeliveryCompany,ExecutiveDirector,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult Index(string? search = null, int page = 1, int pageSize = 10)
//        {
//            var role = _userContextService.GetUserRole(); // Call function to get user role
//            var isDeliveryCompany = role == "DeliveryCompany";
//            var userId = _userContextService.GetCurrentUserId(); // Call function to get current user ID

//            // Initialize the query
//            IQueryable<AppDeliveryCompanyPriceViewModel> query = _context.DeliveryCompanyPrices
//                .Where(p => !isDeliveryCompany || p.DeliveryCompany.UserId == userId)
//                .Include(p => p.DeliveryCompany)
//                 .Where(p => !p.DeliveryCompany.IsRepresentative)

//                .Select(p => new AppDeliveryCompanyPriceViewModel
//                {
//                    Id = p.Id,
//                    SelectedCountry = p.Country,
//                    Currency = Common.GetCurrencyByCountryName(p.Country.ToString()),
//                    Price = (p.Price),
//                    City = p.City,
//                    DeliveryCompanyId = p.DeliveryCompanyId,
//                    DeliveryCompanyName = p.DeliveryCompany.Name
//                });

//            // Apply search term if available
//            if (!string.IsNullOrEmpty(search))
//            {
//                query = query.Where(p => p.City.Contains(search));
//            }

//            // Retrieve the total number of prices after applying filters but before pagination
//            var totalItems = query.Count();

//            // Apply pagination
//            var prices = query.Skip((page - 1) * pageSize)
//                              .Take(pageSize)
//                              .ToList();

//            // Create a ViewModel instance and populate it with data
//            var paginationViewModel = new PaginationViewModel<AppDeliveryCompanyPriceViewModel>
//            {
//                Items = prices,
//                CurrentPage = page,
//                PageSize = pageSize,
//                TotalItems = totalItems
//            };

//            return Json(paginationViewModel);
//        }

//        [HttpGet("/deliverycompanyprices/details/{id}")]
//        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult Details(int id)
//        {
//            var role = _userContextService.GetUserRole(); // Call function to get user role
//            var isDeliveryCompany = role == "DeliveryCompany";
//            var userId = _userContextService.GetCurrentUserId(); // Call function to get current user ID

//            // Retrieve the delivery company price by ID
//            var deliveryCompanyPrice = _context.DeliveryCompanyPrices
//                .Include(p => p.DeliveryCompany)
//                .FirstOrDefault(p => p.Id == id);

//            // Check if the delivery company price exists and the user has permission to access it
//            if (deliveryCompanyPrice == null || (isDeliveryCompany && deliveryCompanyPrice.DeliveryCompany.UserId != userId))
//            {
//                return NotFound(); // Or you can return Unauthorized() depending on your requirements
//            }

//            // Map to view model
//            var viewModel = new AppDeliveryCompanyPriceViewModel
//            {
//                Id = deliveryCompanyPrice.Id,
//                SelectedCountry = deliveryCompanyPrice.Country,
//                Currency = Common.GetCurrencyByCountryName(deliveryCompanyPrice.Country.ToString()),
//                Price = deliveryCompanyPrice.Price,
//                City = deliveryCompanyPrice.City,
//                DeliveryCompanyId = deliveryCompanyPrice.DeliveryCompanyId,
//                DeliveryCompanyName = deliveryCompanyPrice.DeliveryCompany.Name
//            };

//            return Json(viewModel);
//        }


//        [HttpPost("/deliverycompanyprices/create")]
//        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult Create(AppCreateDeliveryCompanyPriceViewModel model, bool addForAllCities)
//        {
//            if (!ModelState.IsValid)
//            {
//                return BadRequest(ModelState);
//            }

//            if (addForAllCities)
//            {
//                if (Common.CitiesByCountry.ContainsKey(model.Country.Value))
//                {
//                    var cities = Common.CitiesByCountry[model.Country.Value];

//                    foreach (var city in cities)
//                    {
//                        var newModel = new DeliveryCompanyPrice
//                        {
//                            Price = model.Price,
//                            City = city,
//                            DeliveryCompanyId = model.DeliveryCompanyId,
//                            Country = model.Country.Value,
//                        };
//                        _context.DeliveryCompanyPrices.Add(newModel);
//                    }
//                    _context.SaveChanges(); // Save all at once after adding all cities
//                }
//            }
//            else
//            {
//                var deliveryCompanyPrice = new DeliveryCompanyPrice
//                {
//                    Price = model.Price,
//                    City = model.City,
//                    DeliveryCompanyId = model.DeliveryCompanyId,
//                    Country = model.Country.Value,
//                };

//                _context.DeliveryCompanyPrices.Add(deliveryCompanyPrice);
//                _context.SaveChanges();
//            }

//            return Json("sucess"); // Redirect to the index action (or wherever you want)
//        }


//        [HttpPut("/deliverycompanyprices/edit/{id}")]
//        [Authorize(Roles = "Admin,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> Edit(int id, AppCreateDeliveryCompanyPriceViewModel model, bool addForAllCities)
//        {
//            if (!ModelState.IsValid)
//            {
//                return BadRequest(ModelState);
//            }

//            var existingPrice = _context.DeliveryCompanyPrices.FirstOrDefault(p => p.Id == id);
//            if (existingPrice == null)
//            {
//                return NotFound();
//            }

//            if (addForAllCities)
//            {
//                if (Common.CitiesByCountry.ContainsKey(model.Country.Value))
//                {
//                    var cities = Common.CitiesByCountry[model.Country.Value];

//                    foreach (var city in cities)
//                    {
//                        var newModel = new DeliveryCompanyPrice
//                        {
//                            Price = model.Price,
//                            City = city,
//                            DeliveryCompanyId = model.DeliveryCompanyId,
//                            Country = model.Country.Value,
//                        };
//                        _context.DeliveryCompanyPrices.Add(newModel);
//                    }
//                    _context.SaveChanges(); // Save all at once after adding all cities
//                }
//            }
//            else
//            {
//                existingPrice.Price = model.Price;
//                existingPrice.City = model.City;
//                existingPrice.DeliveryCompanyId = model.DeliveryCompanyId;
//                existingPrice.Country = model.Country.Value;

//                _context.SaveChanges();
//            }

//            return Json("edited"); // Redirect to the index action (or wherever you want)
//        }

//        [HttpGet("/deliverycompanyprices/bydeliverycompanyid/{id}")]
//        [Authorize(Roles = "Admin,DeliveryCompany,ExecutiveDirector,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult ByDeliveryCompanyId(int id, string? search = null, int page = 1, int pageSize = 10)
//        {
//            var role = _userContextService.GetUserRole(); // Call function to get user role
//            var isDeliveryCompany = role == "DeliveryCompany";
//            var userId = _userContextService.GetCurrentUserId(); // Call function to get current user ID

//            // Initialize the query
//            IQueryable<DeliveryCompanyPrice> query = _context.DeliveryCompanyPrices
//                .Where(p => !isDeliveryCompany || p.DeliveryCompany.UserId == userId)
//                .Include(p => p.DeliveryCompany)
//                .Where(p => !p.DeliveryCompany.IsRepresentative);

//            // Apply filtering by DeliveryCompanyId
//            query = query.Where(p => p.DeliveryCompanyId == id);

//            // Select fields and project to AppDeliveryCompanyPriceViewModel
//            var prices = query.Select(p => new AppDeliveryCompanyPriceViewModel
//            {
//                Currency = Common.GetCurrencyByCountryName(p.Country.ToString()),
//                Price = p.Price,
//                City = p.City,
//            });

//            // Apply search term if available
//            if (!string.IsNullOrEmpty(search))
//            {
//                prices = prices.Where(p => p.City.Contains(search));
//            }

//            // Retrieve the total number of prices after applying filters but before pagination
//            var totalItems = prices.Count();

//            // Apply pagination
//            var paginatedPrices = prices.Skip((page - 1) * pageSize)
//                                        .Take(pageSize)
//                                        .ToList();

//            // Create a ViewModel instance and populate it with data
//            var paginationViewModel = new PaginationViewModel<AppDeliveryCompanyPriceViewModel>
//            {
//                Items = paginatedPrices,
//                CurrentPage = page,
//                PageSize = pageSize,
//                TotalItems = totalItems
//            };

//            return Json(paginationViewModel);
//        }


//        // نهاية شركات التوصيل 


//        // المندوبين 

//        [HttpGet("/deliveryrepresentative/getall")]
//        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult deliveryrepresentativelist(string? search = null, int page = 1, int pageSize = 10)
//        {
//            IQueryable<AppDeliveryCompanyViewModel> query = _context.DeliveryCompanies
//                .Where(a => a.IsRepresentative)
//                .Include(a => a.User)
//                .Select(e => new AppDeliveryCompanyViewModel
//                {
//                    Id = e.Id,
//                    LogoUrl = e.ImageUrl,
//                    Name = e.Name,
//                    PhoneNumber = e.PhoneNumber,
//                    Email = e.User.Email,
//                    Specialty = e.specialty,
//                    Country = e.Country,
//                    IsShown = e.IsShown,
//                    IsActive = e.User.EmailConfirmed
//                });

//            // Filtering by name if search parameter is provided
//            if (!string.IsNullOrEmpty(search))
//            {
//                query = query.Where(a => a.Name.Contains(search));
//            }

//            // Filtering by country if search parameter is provided
//            if (Enum.TryParse(typeof(Countries), search, out object countryObj) && countryObj is Countries country)
//            {
//                query = query.Where(a => a.Country == country);
//            }

//            int totalItems = query.Count();

//            var viewModel = new PaginationViewModel<AppDeliveryCompanyViewModel>
//            {
//                Items = query.Skip((page - 1) * pageSize)
//                             .Take(pageSize)
//                             .ToList(),
//                CurrentPage = page,
//                PageSize = pageSize,
//                TotalItems = totalItems
//            };

//            return Json(viewModel);
//        }


//        [HttpPost("/deliveryrepresentatives/create")]
//        [Authorize(Roles = "Admin,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> deliveryrepresentativesCreate([FromForm] AppCreateDeliveryCompanyViewModel model, IFormFile logoFile, IFormFile infoFile)
//        {

//            if (!ModelState.IsValid)
//            {
//                // Return a structured JSON response with model state errors
//                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
//                return BadRequest(new { success = false, errors = errors });
//            }
//            // Create the ApplicationUser
//            var user = new ApplicationUser
//            {
//                UserName = model.Email,
//                Email = model.Email,
//                Name = model.Name,
//            };

//            // Create the user and add them to the "DeliveryCompany" role
//            var result = await _userManager.CreateAsync(user, model.Password);
//            if (result.Succeeded)
//            {
//                await _userManager.AddToRoleAsync(user, "DeliveryRepresentative");

//                // Create the DeliveryCompany
//                var deliveryCompany = new DeliveryCompany
//                {
//                    Name = model.Name,
//                    TaxRegistrationNumber = model.TaxRegistrationNumber,
//                    IdNumber = model.IdNumber,
//                    Address = model.Address,
//                    PhoneNumber = model.PhoneNumber,
//                    specialty = model.Specialty,
//                    Website = model.Website,
//                    Notes = model.Notes,
//                    CreatedDate = _timeService.GetIstanbulTimeWithOffset(),
//                    Country = model.SelectedCountry,
//                    UserId = user.Id, // This is where you set the UserId
//                    IsRepresentative = true,

//                };

//                // Save the logo and information files
//                if (logoFile != null)
//                    deliveryCompany.ImageUrl = await _fileUploadService.UploadFileAsync(logoFile, "deliverycompanies");

//                if (infoFile != null)
//                    deliveryCompany.InformationUrl = await _fileUploadService.UploadFileAsync(infoFile, "deliverycompanies");

//                // Add and save the DeliveryCompany
//                _context.Add(deliveryCompany);
//                await _context.SaveChangesAsync();

//                // Assign the AccessId here based on the newly created DeliveryCompany's Id
//                user.AcessId = deliveryCompany.Id;


//                // Update the user with the AccessId
//                await _userManager.UpdateAsync(user);

//                // Redirect to a success page or perform any other necessary actions
//                return Json("delivery Representative added");
//            }
//            else
//            {
//                var errors = result.Errors.Select(e => e.Description);

//                // Handle user registration errors
//                return Json(new { success = false, errors = errors });
//            }

//        }

//        [HttpPut("/deliveryrepresentatives/edit/{id}")]
//        [Authorize(Roles = "Admin,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> deliveryrepresentativesEdit(int id, [FromForm] AppEditDeliveryCompanyViewModel model, IFormFile? logoFile, IFormFile? infoFile)
//        {
//            if (!ModelState.IsValid)
//            {
//                return BadRequest(ModelState);
//            }

//            if (id != model.Id)
//            {
//                return BadRequest("Invalid id provided.");
//            }

//            var existingCompany = await _context.DeliveryCompanies
//                .Include(a => a.User)
//                .FirstOrDefaultAsync(c => c.Id == id);
//            if (existingCompany == null)
//            {
//                return NotFound("Delivery representative not found.");
//            }

//            // Update the existingCompany properties
//            existingCompany.Name = model.Name;
//            existingCompany.TaxRegistrationNumber = model.TaxRegistrationNumber;
//            existingCompany.IdNumber = model.IdNumber;
//            existingCompany.Address = model.Address;
//            existingCompany.PhoneNumber = model.PhoneNumber;
//            existingCompany.specialty = model.Specialty;
//            existingCompany.Website = model.Website;
//            existingCompany.Notes = model.Notes;
//            existingCompany.Country = model.SelectedCountry;

//            if (logoFile != null)
//            {
//                existingCompany.ImageUrl = await _fileUploadService.UpdateFileAsync(existingCompany.ImageUrl, logoFile, "deliverycompanies");
//            }

//            if (infoFile != null)
//            {
//                existingCompany.InformationUrl = await _fileUploadService.UpdateFileAsync(existingCompany.InformationUrl, infoFile, "deliverycompanies");
//            }

//            // Change password if NewPassword is provided
//            if (!string.IsNullOrWhiteSpace(model.NewPassword))
//            {
//                if (model.NewPassword != model.ConfirmNewPassword)
//                {
//                    return BadRequest("New password and confirmation password do not match.");
//                }

//                var user = await _userManager.FindByIdAsync(existingCompany.UserId);
//                if (user != null)
//                {
//                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
//                    var passwordChangeResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

//                    if (!passwordChangeResult.Succeeded)
//                    {
//                        // Handle errors
//                        return BadRequest("Failed to reset password.");
//                    }
//                }
//            }

//            // Save changes to the database
//            await _context.SaveChangesAsync();
//            return Ok("Delivery representative updated successfully.");
//        }


//        [HttpGet("/deliveryrepresentatives/details/{id}")]
//        [Authorize(Roles = "Admin,Accountant,Observer,ExecutiveDirector,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult deliveryrepresentativesDetailsById(int id)
//        {
//            var deliveryCompany = _context.DeliveryCompanies
//                .Include(a => a.User)
//                .FirstOrDefault(e => e.Id == id && e.IsRepresentative);

//            if (deliveryCompany == null)
//            {
//                return NotFound("Delivery representative not found.");
//            }

//            var details = new AppDeliveryCompanyViewModel
//            {
//                Id = deliveryCompany.Id,
//                LogoUrl = deliveryCompany.ImageUrl,
//                Name = deliveryCompany.Name,
//                PhoneNumber = deliveryCompany.PhoneNumber,
//                Email = deliveryCompany.User.Email,
//                Specialty = deliveryCompany.specialty,
//                Country = deliveryCompany.Country,
//                Notes = deliveryCompany.Notes,
//                TaxRegistrationNumber = deliveryCompany.TaxRegistrationNumber,
//                IdNumber = deliveryCompany.IdNumber,
//                Address = deliveryCompany.Address,

//            };

//            return Json(details);
//        }


//        [HttpGet("/deliveryrepresentativeprices/index")]
//        [Authorize(Roles = "Admin,DeliveryRepresentative,ExecutiveDirector,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult deliveryrepresentativeprices(string? search = null, int page = 1, int pageSize = 10)
//        {
//            var role = _userContextService.GetUserRole(); // Call function to get user role
//            var isDeliveryCompany = role == "DeliveryRepresentative";
//            var userId = _userContextService.GetCurrentUserId(); // Call function to get current user ID

//            // Initialize the query
//            IQueryable<AppDeliveryCompanyPriceViewModel> query = _context.DeliveryCompanyPrices
//                .Where(p => !isDeliveryCompany || p.DeliveryCompany.UserId == userId)
//                .Include(p => p.DeliveryCompany)
//                 .Where(p => p.DeliveryCompany.IsRepresentative)

//                .Select(p => new AppDeliveryCompanyPriceViewModel
//                {
//                    Id = p.Id,
//                    SelectedCountry = p.Country,
//                    Currency = Common.GetCurrencyByCountryName(p.Country.ToString()),
//                    Price = (p.Price),
//                    City = p.City,
//                    DeliveryCompanyId = p.DeliveryCompanyId,
//                    DeliveryCompanyName = p.DeliveryCompany.Name
//                });

//            // Apply search term if available
//            if (!string.IsNullOrEmpty(search))
//            {
//                query = query.Where(p => p.City.Contains(search));
//            }

//            // Retrieve the total number of prices after applying filters but before pagination
//            var totalItems = query.Count();

//            // Apply pagination
//            var prices = query.Skip((page - 1) * pageSize)
//                              .Take(pageSize)
//                              .ToList();

//            // Create a ViewModel instance and populate it with data
//            var paginationViewModel = new PaginationViewModel<AppDeliveryCompanyPriceViewModel>
//            {
//                Items = prices,
//                CurrentPage = page,
//                PageSize = pageSize,
//                TotalItems = totalItems
//            };

//            return Json(paginationViewModel);
//        }



//        [HttpGet("/deliveryrepresentativesprices/create")]
//        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult deliveryrepresentativepricesCreate(AppCreateDeliveryCompanyPriceViewModel model, bool addForAllCities)
//        {
//            if (!ModelState.IsValid)
//            {
//                return BadRequest(ModelState);
//            }

//            if (addForAllCities)
//            {
//                if (Common.CitiesByCountry.ContainsKey(model.Country.Value))
//                {
//                    var cities = Common.CitiesByCountry[model.Country.Value];

//                    foreach (var city in cities)
//                    {
//                        var newModel = new DeliveryCompanyPrice
//                        {
//                            Price = model.Price,
//                            City = city,
//                            DeliveryCompanyId = model.DeliveryCompanyId,
//                            Country = model.Country.Value,
//                        };
//                        _context.DeliveryCompanyPrices.Add(newModel);
//                    }
//                    _context.SaveChanges(); // Save all at once after adding all cities
//                }
//            }
//            else
//            {
//                var deliveryCompanyPrice = new DeliveryCompanyPrice
//                {
//                    Price = model.Price,
//                    City = model.City,
//                    DeliveryCompanyId = model.DeliveryCompanyId,
//                    Country = model.Country.Value,
//                };

//                _context.DeliveryCompanyPrices.Add(deliveryCompanyPrice);
//                _context.SaveChanges();
//            }

//            return Json("sucess"); // Redirect to the index action (or wherever you want)
//        }


//        [HttpPut("/deliveryrepresentativeprices/edit/{id}")]
//        [Authorize(Roles = "Admin,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]

//        public async Task<IActionResult> deliveryrepresentativepricesEdit(int id, AppCreateDeliveryCompanyPriceViewModel model, bool addForAllCities)
//        {
//            if (!ModelState.IsValid)
//            {
//                return BadRequest(ModelState);
//            }

//            var existingPrice = _context.DeliveryCompanyPrices.FirstOrDefault(p => p.Id == id);
//            if (existingPrice == null)
//            {
//                return NotFound();
//            }

//            if (addForAllCities)
//            {
//                if (Common.CitiesByCountry.ContainsKey(model.Country.Value))
//                {
//                    var cities = Common.CitiesByCountry[model.Country.Value];

//                    foreach (var city in cities)
//                    {
//                        var newModel = new DeliveryCompanyPrice
//                        {
//                            Price = model.Price,
//                            City = city,
//                            DeliveryCompanyId = model.DeliveryCompanyId,
//                            Country = model.Country.Value,
//                        };
//                        _context.DeliveryCompanyPrices.Add(newModel);
//                    }
//                    _context.SaveChanges(); // Save all at once after adding all cities
//                }
//            }
//            else
//            {
//                existingPrice.Price = model.Price;
//                existingPrice.City = model.City;
//                existingPrice.DeliveryCompanyId = model.DeliveryCompanyId;
//                existingPrice.Country = model.Country.Value;

//                _context.SaveChanges();
//            }

//            return Json("edited"); // Redirect to the index action (or wherever you want)
//        }


//        [HttpGet("/deliveryrepresentativeprices/bydeliveryrepresentativeid/{id}")]
//        [Authorize(Roles = "Admin,deliveryrepresentative,ExecutiveDirector,FollowUpDepartment", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult BydeliveryrepresentativeId(int id, string? search = null, int page = 1, int pageSize = 10)
//        {
//            var role = _userContextService.GetUserRole(); // Call function to get user role
//            var isDeliveryCompany = role == "DeliveryRepresentative";
//            var userId = _userContextService.GetCurrentUserId(); // Call function to get current user ID

//            // Initialize the query
//            IQueryable<DeliveryCompanyPrice> query = _context.DeliveryCompanyPrices
//                .Where(p => !isDeliveryCompany || p.DeliveryCompany.UserId == userId)
//                .Include(p => p.DeliveryCompany)
//                .Where(p => p.DeliveryCompany.IsRepresentative);

//            // Apply filtering by DeliveryCompanyId
//            query = query.Where(p => p.DeliveryCompanyId == id);

//            // Select fields and project to AppDeliveryCompanyPriceViewModel
//            var prices = query.Select(p => new AppDeliveryCompanyPriceViewModel
//            {
//                Currency = Common.GetCurrencyByCountryName(p.Country.ToString()),
//                Price = p.Price,
//                City = p.City,
//            });

//            // Apply search term if available
//            if (!string.IsNullOrEmpty(search))
//            {
//                prices = prices.Where(p => p.City.Contains(search));
//            }

//            // Retrieve the total number of prices after applying filters but before pagination
//            var totalItems = prices.Count();

//            // Apply pagination
//            var paginatedPrices = prices.Skip((page - 1) * pageSize)
//                                        .Take(pageSize)
//                                        .ToList();

//            // Create a ViewModel instance and populate it with data
//            var paginationViewModel = new PaginationViewModel<AppDeliveryCompanyPriceViewModel>
//            {
//                Items = paginatedPrices,
//                CurrentPage = page,
//                PageSize = pageSize,
//                TotalItems = totalItems
//            };

//            return Json(paginationViewModel);
//        }

//        // نهاية المندوبين 

//        // المتاجر


//        [HttpGet("/stores/getall")]
//        // GET: ManufacturingCompanies
//        [Authorize(Roles = "Admin,Accountant,ExecutiveDirector,Observer,OrderPreparer,DeliveryCompany,DeliveryRepresentative", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]

//        public IActionResult storeIndex(int page = 1, int pageSize = 10)
//        {
//            var query = _context.ManufacturingCompanies.AsQueryable();

//            int totalItems = query.Count();
//            int skip = (page - 1) * pageSize;

//            var pagedItems = query
//                .OrderBy(mc => mc.Id)
//                .Skip(skip)
//                .Take(pageSize)
//                .Select(mc => new AppStoresViewModel
//                {
//                    Id = mc.Id,
//                    Name = mc.Name,
//                    Logo = mc.ImageUrl,
//                    IsShown = mc.IsHidden,
//                    InvoiceImage = mc.InvoiceImage
//                })
//                .ToList();

//            var viewModel = new PaginationViewModel<AppStoresViewModel>
//            {
//                Items = pagedItems, // Corrected property name
//                CurrentPage = page,
//                PageSize = pageSize,
//                TotalItems = totalItems
//            };

//            return Json(viewModel);
//        }



//        [HttpPost("/stores/create")]
//        [Authorize(Roles = "Admin,ExecutiveDirector", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> Create([FromForm] AppCreateStoresViewModel model, IFormFile logoFile)
//        {
//            if (!ModelState.IsValid)
//            {
//                // Return the view with validation errors
//                return BadRequest(ModelState);
//            }

//            var manufacturingCompany = new ManufacturingCompany
//            {
//                Name = model.Name,
//                IsHidden = false,
//            };

//            if (logoFile != null && logoFile.Length > 0)
//            {
//                // Upload the logo file using the provided service
//                manufacturingCompany.ImageUrl = await _fileUploadService.UploadFileAsync(logoFile, "deliverycompanies");
//            }

//            _context.Add(manufacturingCompany);
//            await _context.SaveChangesAsync();

//            return Ok(" store added "); // or return a success message as needed
//        }


//        [HttpPut("/stores/edit/{id}")]
//        [Authorize(Roles = "Admin,ExecutiveDirector", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> Edit(int id, [FromForm] AppCreateStoresViewModel model, IFormFile logoFile)
//        {
//            if (!ModelState.IsValid)
//            {
//                // Return the view with validation errors
//                return BadRequest(ModelState);
//            }

//            var existingCompany = await _context.ManufacturingCompanies.FindAsync(id);
//            if (existingCompany == null)
//            {
//                return NotFound("Company not found");
//            }

//            existingCompany.Name = model.Name;
//            existingCompany.IsHidden = false; // Adjust as needed

//            if (logoFile != null && logoFile.Length > 0)
//            {
//                // Upload the logo file using the provided service
//                existingCompany.ImageUrl = await _fileUploadService.UploadFileAsync(logoFile, "deliverycompanies");
//            }

//            await _context.SaveChangesAsync();

//            return Ok("Store updated"); // or return a success message as needed
//        }


//        [HttpPost("/stores/setisshown/{id}")]
//        [Authorize(Roles = "Admin,ExecutiveDirector", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> SetIsShown(int id, [FromBody] AppCompanyStatusViewModel model)
//        {
//            var existingCompany = await _context.ManufacturingCompanies.FindAsync(id);
//            if (existingCompany == null)
//            {
//                return NotFound("Company not found");
//            }

//            existingCompany.IsHidden = model.IsShown;
//            await _context.SaveChangesAsync();

//            return Ok($"Company status updated for the company with ID {id}");
//        }


//        // GET: api/manufacturingcompanies/getbyid/5
//        [HttpGet("/stores/details/{id}")]
//        [Authorize(Roles = "Admin,ExecutiveDirector", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> GetStoreById(int id)
//        {
//            var manufacturingCompany = await _context.ManufacturingCompanies.FindAsync(id);

//            if (manufacturingCompany == null)
//            {
//                return NotFound("Manufacturing company not found");
//            }

//            var viewModel = new AppStoresViewModel
//            {
//                Id = manufacturingCompany.Id,
//                Name = manufacturingCompany.Name,
//                Logo = manufacturingCompany.ImageUrl,
//                IsShown = manufacturingCompany.IsHidden,
//                InvoiceImage = manufacturingCompany.InvoiceImage
//            };

//            return Ok(viewModel);
//        }





//        // نهاية المتاجر

//        // اسعار الصرف

//        // GET: api/exchangerates/getall
//        [HttpGet("/exchangerates/getall")]
//        public async Task<IActionResult> exchangerategetall(int page = 1, int pageSize = 10)
//        {
//            var query = _context.ExchangeRates.AsQueryable();

//            int totalItems = await query.CountAsync();
//            int skip = (page - 1) * pageSize;

//            var pagedItems = await query
//                .OrderBy(rate => rate.Id)
//                .Skip(skip)
//                .Take(pageSize)
//                .Select(rate => new AppExchangeRateViewModel
//                {
//                    Id = rate.Id,
//                    Country = rate.Country,
//                    Currency = Common.GetCurrencyByCountryName(rate.Country.ToString()),
//                    BuyToUSD = rate.BuyToUSD,
//                    SellToUSD = rate.SellToUSD
//                })
//                .ToListAsync();



//            return Ok(pagedItems);
//        }


//        // POST: api/exchangerates/create
//        [HttpPost("/exchangerates/create")]
//        [Authorize(Roles = "Admin,Accountant", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]

//        public async Task<IActionResult> Create(AppCreateExchangeRateViewModel model)
//        {
//            if (!ModelState.IsValid)
//            {
//                return BadRequest(ModelState);
//            }

//            var exchangeRate = new ExchangeRate
//            {
//                Country = model.Currency,
//                BuyToUSD = model.BuyToUSD,
//                SellToUSD = model.SellToUSD
//            };

//            _context.Add(exchangeRate);
//            await _context.SaveChangesAsync();

//            return Ok("Exchange rate created successfully");
//        }


//        // PUT: api/exchangerates/edit/5
//        [HttpPut("/exchangerates/edit/{id}")]
//        [Authorize(Roles = "Admin,Accountant", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public async Task<IActionResult> Edit(int id, AppCreateExchangeRateViewModel model)
//        {
//            if (!ModelState.IsValid)
//            {
//                return BadRequest(ModelState);
//            }

//            var exchangeRate = await _context.ExchangeRates.FindAsync(id);
//            if (exchangeRate == null)
//            {
//                return NotFound("Exchange rate not found");
//            }

//            exchangeRate.Country = model.Currency;
//            exchangeRate.BuyToUSD = model.BuyToUSD;
//            exchangeRate.SellToUSD = model.SellToUSD;

//            await _context.SaveChangesAsync();

//            return Ok("Exchange rate updated successfully");
//        }

//        // GET: api/exchangerates/getbyid/5
//        [HttpGet("/exchangerates/details/{id}")]
//        public async Task<IActionResult> GetExchangeRateById(int id)
//        {
//            var exchangeRate = await _context.ExchangeRates.FindAsync(id);

//            if (exchangeRate == null)
//            {
//                return NotFound("Exchange rate not found");
//            }

//            var viewModel = new AppExchangeRateViewModel
//            {
//                Id = exchangeRate.Id,
//                Country = exchangeRate.Country,
//                Currency = Common.GetCurrencyByCountryName(exchangeRate.Country.ToString()),
//                BuyToUSD = exchangeRate.BuyToUSD,
//                SellToUSD = exchangeRate.SellToUSD
//            };

//            return Ok(viewModel);
//        }
//        // نهاية اسعار الصرف 



//        // list of data to filter 

//        [HttpGet("/getmanufacturingcompanies")]
//        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult GetAllManufacturingCompanies()
//        {
//            var companies = _context.ManufacturingCompanies
//                .Where(a => a.IsHidden)
//                .Select(mc => new AppCompanyViewModel
//                {
//                    Id = mc.Id,
//                    Name = mc.Name,
//                    LogoUrl = mc.ImageUrl
//                })
//                .ToList();

//            return Json(companies);
//        }


//        [HttpGet("/getdeliverycompanies")]
//        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult GetAllDeliveryCompanies()
//        {
//            var companies = _context.DeliveryCompanies
//                .Where(a => a.IsShown && !a.IsRepresentative)
//                .Select(mc => new AppCompanyViewModel
//                {
//                    Id = mc.Id,
//                    Name = mc.Name,
//                    LogoUrl = mc.ImageUrl
//                })
//                .ToList();

//            return Json(companies);
//        }

//        [HttpGet("/getdeliveryrepresentative")]
//        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult GetAllDeliveryRepresentatives()
//        {
//            var companies = _context.DeliveryCompanies
//                .Where(a => a.IsShown && a.IsRepresentative)
//                .Select(mc => new AppCompanyViewModel
//                {
//                    Id = mc.Id,
//                    Name = mc.Name,
//                    LogoUrl = mc.ImageUrl
//                })
//                .ToList();

//            return Json(companies);
//        }


//        [HttpGet("/getemployees")]
//        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult GetAllEmployees()
//        {
//            var companies = _context.Employees
//                .Where(a => a.IsShown)
//                .Select(mc => new AppCompanyViewModel
//                {
//                    Id = mc.Id,
//                    Name = mc.Name,
//                    LogoUrl = mc.ImageUrl
//                })
//                .ToList();

//            return Json(companies);
//        }


//        private readonly string[] roles = { "Admin", "DeliveryCompany", "DeliveryRepresentative", "CallCenter", "FollowUpDepartment", "Accountant", "ExecutiveDirector", "Observer", "OrderPreparer", "WareHouse" };

//        [HttpGet("/roles")]
//        [Authorize(Roles = "Admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult GetRoles()
//        {
//            return Ok(roles);
//        }


//        [HttpGet("/productlist")]
//        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult GetProductPrefixes()
//        {
//            var productPrefixes = new List<string>
//            {
//                "فاونديشن كوين",
//                "فاونديشن روز",
//                "توب باودر",
//                "فاونديشن رويال",
//                "كريم فلفيت الطبي",
//                "كريم بانسي الطبي",
//                "فرشاة",
//                "ماسكارا"
//            };

//            return Ok(productPrefixes);
//        }


//        // end of list of data to filter 




//        // list of data by filterting 
//        [HttpGet("/getfilterDeliveryCompanies")]
//        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] // Ensure access is restricted to authenticated users.
//        public IActionResult FilterDeliveryCompanies([FromQuery] Common.Countries? countryId)
//        {
//            if (!countryId.HasValue)
//            {
//                return BadRequest("Country is required.");
//            }

//            var filteredDeliveryCompanies = _context.DeliveryCompanies
//                .Where(dc => dc.Country == countryId.Value && !dc.IsRepresentative && dc.IsShown)
//                .Select(dc => new
//                {
//                    dc.Id,
//                    dc.Name,
//                    dc.ImageUrl
//                })
//                .ToList();

//            return Ok(filteredDeliveryCompanies);
//        }


//        [HttpGet("/getfilterDeliveryRepresentatives")]
//        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] // Ensure access is restricted to authenticated users.
//        public IActionResult FilterDeliveryRepresentative([FromQuery] Common.Countries? countryId, string city)
//        {
//            if (!countryId.HasValue)
//            {
//                return BadRequest("Country is required.");
//            }
//            if (string.IsNullOrEmpty(city))
//            {
//                return BadRequest("City is required.");
//            }

//            var filteredDeliveryRepresentatives = _context.DeliveryCompanies
//                .Where(dc => dc.Country == countryId.Value && dc.IsRepresentative && dc.City == city && dc.IsShown)
//                .Select(dc => new
//                {
//                    dc.Id,
//                    dc.Name,
//                    dc.ImageUrl
//                })
//                .ToList();

//            return Ok(filteredDeliveryRepresentatives);
//        }


//        [HttpGet("/getfilteredWarehouses")]
//        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult GetFilteredWarehousesByDeliveryCompanyId([FromQuery] int? deliveryCompanyId)
//        {
//            // Start with a base query for shown warehouses.
//            IQueryable<Warehouse> query = _context.Warehouses.Where(w => w.IsShown && w.Amount > 0);

//            // Apply the delivery company filter if provided.
//            if (deliveryCompanyId.HasValue)
//            {
//                query = query.Where(w => w.DeliveryCompanyId == deliveryCompanyId.Value);
//            }

//            // Project the required data to avoid sending unnecessary information to the client.
//            var filteredWarehouses = query.Select(w => new
//            {
//                w.Id,
//                w.Name,
//                w.Amount,// Adjust if the property name for quantity is different.
//                w.MainWarehouse.ImageUrl
//            })
//            .ToList();

//            return Ok(filteredWarehouses);
//        }


//        // end of list of data by filtering 



//        // enums 
//        // common shared things 
//        [HttpGet("/countries")]
//        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult GetCountries()
//        {
//            var countries = Enum.GetValues(typeof(Countries))
//                                .Cast<Countries>()
//                                .Select(c => new { Id = (int)c, Name = c.ToString() });

//            return Ok(countries);
//        }

//        [HttpGet("/cities")]
//        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult GetCitiesByCountry([FromQuery] int countryId)
//        {
//            if (!Enum.IsDefined(typeof(Countries), countryId))
//            {
//                return NotFound("Country not found.");
//            }

//            var country = (Countries)countryId;

//            if (CitiesByCountry.TryGetValue(country, out List<string> cities))
//            {
//                return Ok(new { Country = country.ToString(), Cities = cities });
//            }
//            else
//            {
//                return NotFound("Cities for the specified country not found.");
//            }
//        }

//        [HttpGet("/ordersources")]
//        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] // Ensure access is restricted to authenticated users.
//        public IActionResult GetOrderSources()
//        {
//            var orderSources = Enum.GetValues(typeof(OrderSourceEnum))
//                                   .Cast<OrderSourceEnum>()
//                                   .Select(os => new { Id = (int)os, Name = os.ToString() });

//            return Ok(orderSources);
//        }


//        [HttpGet("/orderstatuses")]
//        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] // Ensure access is restricted to authenticated users.
//        public IActionResult GetOrderStatuses()
//        {
//            var orderStatuses = Enum.GetValues(typeof(OrderStatusEnum))
//                                    .Cast<OrderStatusEnum>()
//                                    .Select(os => new { Id = (int)os, Name = os.ToString() });

//            return Ok(orderStatuses);
//        }

//        [HttpGet("/gettransactiontypes")]
//        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
//        public IActionResult GetTransactionTypes()
//        {
//            var transactionTypes = Enum.GetValues(typeof(TransactionTypeEnum))
//                                       .Cast<TransactionTypeEnum>()
//                                       .Select(t => new { Id = (int)t, Name = t.ToString() });

//            return Ok(transactionTypes);
//        }



//    }
//}



