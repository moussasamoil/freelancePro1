using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.Models.ViewModel;
using lotus_blue.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using lotus_blue.Models.AppViewModel;
using System.Linq;
using System.Text.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
namespace lotus_blue.Controllers
{
    public class ApiCollectionJsonController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly DecimalFormattingService _decimalFormattingService;
        private readonly CurrencyExchangeService _currencyExchangeService;
        private readonly FinancialService _financialService;
        private readonly IConfiguration _configuration;


        public ApiCollectionJsonController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, DecimalFormattingService decimalFormattingService, CurrencyExchangeService currencyExchangeService, FinancialService financialService, IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _decimalFormattingService = decimalFormattingService;
            _currencyExchangeService = currencyExchangeService;
            _financialService = financialService;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            return View();
        }
        // موجوداني وحساباتي للأدمن
        [Authorize(Roles = "Admin")]
        public async Task<JsonResult> OrderByCountry(Common.Countries? countryId)
        {
            IQueryable<Order> ordersQuery = _context.Orders;

            // Check if countryId is null, if not, apply the filter
            if (countryId != null)
            {
                // First, get the basic country grouping and total prices
                ordersQuery = ordersQuery.Where(o => o.Country == countryId);
            }

            // Filter by order status
            ordersQuery = ordersQuery.Where(o => o.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد);

            // Execute the query and include related entities
            var intermediateResults = await ordersQuery
                .AsNoTracking()
                .Include(o => o.DeliveryCompany)
                .GroupBy(o => o.Country)
                .Select(group => new
                {
                    Country = group.Key,
                    TotalPrice = group.Sum(o => o.TotalPrice)
                })
                .ToListAsync();

            // Then, calculate currency conversions outside the initial query
            var ordersByCountry = intermediateResults.Select(g => new OrderByCountryViewModel
            {
                SelectedCountry = g.Country.ToString(),
                Currency = Common.GetCurrencyByCountryName(g.Country.ToString()),
                TotalPrice = _decimalFormattingService.DecimalFormat(g.TotalPrice),
                TotalPirceDollar = DecimalFormattingService.FormatDecimal(_currencyExchangeService.ConvertToUSD(g.TotalPrice, g.Country.ToString())), // Adjusted call to static method
                TotalPirceTl = DecimalFormattingService.FormatDecimal(_currencyExchangeService.ConvertToTurkishLira(_currencyExchangeService.ConvertToUSD(g.TotalPrice, g.Country.ToString()))) // Adjusted call to static method
            }).ToList();

            return Json(ordersByCountry);
        }




        // اخر 10 عمليات للأدمن وشركات اتوصيل ومندوبين
        [HttpGet]
        [Authorize(Roles = "Admin,DeliveryRepresentative,DeliveryCompany")]
        public async Task<JsonResult> GetOrderReports(Common.Countries? countryId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Get the current user's ID

            IQueryable<OrderReport> query = _context.OrderReports
                .Where(a => a.DeliveryCompanyId != null)
                .Include(or => or.DeliveryCompany)
                .Where(or => or.DeliveryCompany != null) // Ensure DeliveryCompany is not null
                .OrderByDescending(or => or.GeneratedTime); // Order by GeneratedTime descending

            bool isAdmin = User.IsInRole("Admin");

            if (!User.IsInRole("Admin"))
            {
                query = query.Where(or => or.DeliveryCompany.UserId == userId);
            }

            // Apply countryId filter if provided
            if (countryId != null)
            {
                query = query.Where(or => or.Country == countryId);
            }

            var orderReports = await query.Take(10) // Take only the last 10 records
                .ToListAsync();

            // Fetch deferred prices for all delivery companies
            var deferredPrices = _financialService.GetFinancialDeliveryCompanydeferredTotalPrice(userId, isAdmin);

            var combinedReports = orderReports.Select(or =>
            {
                // Use DeferredDifference for non-admin users, and DeliveryPrice for admin users
                var deferredTotalPrice = isAdmin
                    ? deferredPrices.FirstOrDefault(dp => dp.DeliveryCompanyId == or.DeliveryCompany.Id)?.DeferredDifference ?? "N/A"
                    : deferredPrices.FirstOrDefault(dp => dp.DeliveryCompanyId == or.DeliveryCompany.Id)?.DeliveryPrice ?? "N/A";

                return new
                {
                    id = or.Id.ToString(),
                    generatedTime = or.GeneratedTime.ToString("yyyy-MM-dd"),
                    totalAmount = Common.GetCurrencyByCountryName(or.Country.ToString()) + " " + DecimalFormattingService.FormatDecimal(or.TotalAmount),
                    country = or.Country.ToString(),
                    deliveryCompanyName = or.DeliveryCompany.Name,
                    deferredTotalPrice, // This dynamically switches based on user role
                    currency = deferredPrices.FirstOrDefault(dp => dp.DeliveryCompanyId == or.DeliveryCompany.Id)?.Currency ?? "N/A"
                };
            }).ToList();

            return Json(combinedReports);
        }



        //  مدفوعاتي في الصفحة الرئيسية لشركة التوصيل والمندوب
        [Authorize(Roles = "DeliveryRepresentative,DeliveryCompany")]
        public async Task<JsonResult> PaidAccountsDeliveryCompany()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Get the current user's ID

            // Check if the user is an Admin or Accountant
            bool isAdminOrAccountant = User.IsInRole("Admin") || User.IsInRole("Accountant");

            // Call the method with parameters based on the user's role
            // If the user is an Admin or Accountant, pass isAdminOrAccountant as true, else false
            var viewModel = _financialService.GetFinancialDeliveryCompanyDataPaid(userId, isAdminOrAccountant);

            return Json(viewModel);

        }

        //  حساباتي في الصفحة الرئيسية لشركة التوصيل والمندوب
        [Authorize(Roles = "DeliveryRepresentative,DeliveryCompany")]
        public async Task<JsonResult> OnGoingAccountDeliveryCompany()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Get the current user's ID

            // Check if the user is an Admin or Accountant
            bool isAdminOrAccountant = User.IsInRole("Admin") || User.IsInRole("Accountant");

            // Call the method with parameters based on the user's role
            // If the user is an Admin or Accountant, pass isAdminOrAccountant as true, else false
            var viewModel = _financialService.GetFinancialDeliveryCompanyDataOnGoingOnly(userId, isAdminOrAccountant);

            return Json(viewModel);

        }

        // موجوداتي للموظفين 
        [Authorize(Roles = "CallCenter,FollowUpDepartment,ExecutiveDirector")]
        public async Task<JsonResult> ExistingsForEmployess()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Get the current user's ID

            var employee = _context.Employees.FirstOrDefault(e => e.ApplicationUserId == userId);

            Dictionary<string, string> totalEarnings = new Dictionary<string, string>();

            if (employee != null)
            {
                // Calculate months worked
                int monthsWorked = ((DateTime.Now.Year - employee.DateAdded.Year) * 12) + DateTime.Now.Month - employee.DateAdded.Month;

                // Calculate total earnings
                decimal totalEarned = monthsWorked * employee.Salary;
                decimal totalEarnedUSD = _currencyExchangeService.ConvertToUSD(totalEarned, "تركيا");

                // Format the earnings using DecimalFormattingService
                string formattedTotalEarned = _decimalFormattingService.DecimalFormat(totalEarned);
                string formattedTotalEarnedUSD = _decimalFormattingService.DecimalFormat(totalEarnedUSD);

                // Add total earnings to the dictionary
                totalEarnings.Add("TotalEarned", formattedTotalEarned);
                totalEarnings.Add("TotalEarnedUSD", formattedTotalEarnedUSD);
            }

            return Json(totalEarnings);
        }

        //اخر 10 عمليات للأدمن وشركات اتوصيل ومندوبين
        [HttpGet]
        [Authorize(Roles = "CallCenter,FollowUpDepartment,ExecutiveDirector")]
        public async Task<IActionResult> Last10EmployeeTransactions()
        {

            // Get the current user's ID
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Find the employee ID based on the current user's ID
            var employee = await _context.Employees
                                         .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

            if (employee == null)
            {
                return Json(new { Message = "Employee not found." });
            }

            // Query the last 10 transactions for this employee
            var transactions = await _context.EmployeeTransactions
                                             .Where(t => t.EmployeeId == employee.Id)
                                             .OrderByDescending(t => t.Date)
                                             .Take(10)
                                             .Select(t => new
                                             {
                                                 t.Amount,
                                                 TransactionType = t.TransactionType.ToString(),
                                                 t.Reason,
                                                 Date = t.Date.ToString("yyyy-MM-dd")
                                             })
                                             .ToListAsync();

            return Json(transactions);


        }

        [Authorize(Roles = "CallCenter,FollowUpDepartment,ExecutiveDirector")]
        public async Task<IActionResult> CalculateAdjustedSalary()
        {
            if (User.Identity.IsAuthenticated)
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var employee = await _context.Employees
                                             .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

                if (employee == null)
                {
                    return Json(new { Message = "Employee not found." });
                }

                decimal baseSalary = employee.Salary;
                var now = DateTime.Now;
                var currentMonth = now.Month;
                var currentYear = now.Year;

                var transactions = await _context.EmployeeTransactions
                    .Where(t => t.EmployeeId == employee.Id && t.Date.Month == currentMonth && t.Date.Year == currentYear)
                    .Select(t => new
                    {
                        t.Id,
                        t.Amount,
                        TransactionType = t.TransactionType.ToString(),
                        t.Reason,
                        Date = t.Date.ToString("yyyy-MM-dd")
                    })
                    .ToListAsync();

                decimal totalBonus = transactions
                    .Where(t => t.TransactionType == TransactionTypeEnum.مكافأة.ToString())
                    .Sum(t => t.Amount);

                decimal totalAdvance = transactions
                    .Where(t => t.TransactionType == TransactionTypeEnum.سلفة.ToString())
                    .Sum(t => t.Amount);

                decimal totalDeduction = transactions
                    .Where(t => t.TransactionType == TransactionTypeEnum.خصم.ToString())
                    .Sum(t => t.Amount);

                decimal adjustedSalary = baseSalary + totalBonus - totalAdvance - totalDeduction;
                decimal adjustedSalaryUSD = _currencyExchangeService.ConvertToUSD(adjustedSalary, "تركيا");



                // Format the earnings using DecimalFormattingService
                string formattedAdjustedSalary = _decimalFormattingService.DecimalFormat(adjustedSalary);
                string formattedAdjustedSalaryUSD = _decimalFormattingService.DecimalFormat(adjustedSalaryUSD);
                string formattedbaseSalary= _decimalFormattingService.DecimalFormat(baseSalary);
                return Json(new
                {
                    AdjustedSalary = formattedAdjustedSalary,
                    Transactions = transactions,
                    AdjustedSalaryUSD = formattedAdjustedSalaryUSD,
                    TotalSalary = formattedbaseSalary,
                    TotalBonus = _decimalFormattingService.DecimalFormat(totalBonus),
                    TotalAdvance = _decimalFormattingService.DecimalFormat(totalAdvance),
                    TotalDeduction = _decimalFormattingService.DecimalFormat(totalDeduction),
                    TotalOngoingAccount = _decimalFormattingService.DecimalFormat(adjustedSalary) // Assuming ongoing account is same as adjusted salary
                });
            }

            return Json(new { Message = "User is not authenticated." });
        }

    }
}



