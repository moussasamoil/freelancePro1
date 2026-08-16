
using DinkToPdf;
using DinkToPdf.Contracts;
using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.Models.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Security.Claims;
using static System.Net.Mime.MediaTypeNames;

namespace lotus_blue.Controllers
{
    public class PdfController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IViewEngine _viewEngine;
        private IConverter _converter;


        public PdfController(ApplicationDbContext context, ICompositeViewEngine viewEngine, IConverter converter)
        {
            _context = context;
            _viewEngine = viewEngine;
            _converter = converter;
        }
        public IActionResult Index()
        {
            return View();
        }


        [Authorize]
        public async Task<IActionResult> PrintOrder(int[] ids)
        {
            try
            {
                var ordersViewModels = await BuildOrderViewModels(ids);
                var htmlContent = await RenderViewAsync("PrintOrder", ordersViewModels);

                var globalSettings = new GlobalSettings
                {
                    ColorMode = ColorMode.Color,
                    Orientation = Orientation.Portrait,
                    PaperSize = PaperKind.A4,
                    Margins = new MarginSettings { Top = 10 },
                    DocumentTitle = "PDF Report",
                };

                var objectSettings = new ObjectSettings
                {
                    HtmlContent = htmlContent,
                };

                var pdf = new HtmlToPdfDocument()
                {
                    GlobalSettings = globalSettings,
                    Objects = { objectSettings }
                };

                var fileBytes = _converter.Convert(pdf);

                // This is where we specify the file should be displayed inline.
                Response.Headers.Add("Content-Disposition", "inline; filename=Orders.pdf");
                return File(fileBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return Content($"An error occurred: {ex.Message}\nFor more details, check the application console.");
            }
        }


        [Authorize]
        public async Task<IActionResult> PrintOrdersForDelivery(int Id)
        {
            try
            {
                // Fetch the OrderReportOrder data with related OrderReport and Orders
                var orderReportOrders = _context.OrderReportOrders
                                                .AsNoTracking()
                                                .Include(oro => oro.OrderReport)
                                                .Include(oro => oro.Order)

                                                    .ThenInclude(o => o.ManufacturingCompany)
                                                      .Include(oro => oro.Order)

                                                    .ThenInclude(o => o.DeliveryCompany)
                                                .Include(oro => oro.Order)
                                                    .ThenInclude(o => o.OrderWarehouses)
                                                        .ThenInclude(ow => ow.Warehouse)
                                                        .ThenInclude(ow => ow.MainWarehouse)
                                                .Where(oro => oro.OrderReportId == Id)
                                                .ToList();

                if (!orderReportOrders.Any())
                {
                    return NotFound($"No orders found for OrderReport with ID {Id}.");
                }

                // Extract the GeneratedTime from the first OrderReportOrder
                var orderReport = orderReportOrders.First().OrderReport;
                var generatedTime = orderReport.GeneratedTime;

                // Build the order view models
                var ordersViewModels = new List<OrderViewModel>();
                DeliveryCompanyViewModel firstDeliveryCompany = null;
                string deliveryCompanyCountry = string.Empty;

                foreach (var orderReportOrder in orderReportOrders)
                {
                    var order = orderReportOrder.Order;

                    if (order != null)
                    {
                        var qrCodeUrl = Url.Action("Details", "Order", new { id = order.Id }, protocol: Request.Scheme);

                        var orderViewModel = new OrderViewModel
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
                            ManufacturingCompanyName = order.ManufacturingCompany.Name,
                            QRCodeUrl = qrCodeUrl,
                            SelectedWarehouses = order.OrderWarehouses.Select(ow => new WarehouseAmountViewModel
                            {
                                WarehouseId = ow.WarehouseId,
                                WarehouseName = ow.Warehouse?.Name ?? "N/A",
                                Amount = ow.Amount,
                                Image = ow.Warehouse.MainWarehouse.ImageUrl,
                            }).ToList(),
                            DeliveryCompany = new DeliveryCompanyViewModel
                            {
                                Name = order.DeliveryCompany.Name,
                            },
                            TotalPrice = order.IsPaid ? 0 : order.TotalPrice,
                            IsPaid = order.IsPaid,
                        };

                        // Capture first order's delivery company and country
                        if (firstDeliveryCompany == null)
                        {
                            firstDeliveryCompany = new DeliveryCompanyViewModel
                            {
                                Name = order.DeliveryCompany.Name,
                            };
                            deliveryCompanyCountry = order.Country.ToString();
                        }

                        using (var qrGenerator = new QRCodeGenerator())
                        {
                            var qrCodeData = qrGenerator.CreateQrCode(qrCodeUrl, QRCodeGenerator.ECCLevel.Q);

                            using (var qrCode = new QRCode(qrCodeData))
                            {
                                using (var qrCodeImage = qrCode.GetGraphic(20))
                                {
                                    using (var ms = new MemoryStream())
                                    {
                                        qrCodeImage.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                                        orderViewModel.QRCodeImageBase64 = Convert.ToBase64String(ms.ToArray());
                                    }
                                }
                            }
                        }

                        ordersViewModels.Add(orderViewModel);
                    }
                }

                // Create the view model for PDF generation
                var printViewModel = new PrintOrdersForDeliveryViewModel
                {
                    OrderReportId = Id,
                    FirstDeliveryCompanyName = firstDeliveryCompany?.Name,
                    DeliveryCompanyCountry = deliveryCompanyCountry,
                    TodayDate = generatedTime, // Use GeneratedTime from OrderReport
                    Orders = ordersViewModels
                };

                // Render the view as HTML content
                var htmlContent = await RenderViewAsync("PrintOrdersForDelivery", printViewModel);

                // Define global settings for the PDF
                var globalSettings = new GlobalSettings
                {
                    ColorMode = ColorMode.Color,
                    Orientation = Orientation.Portrait,
                    PaperSize = PaperKind.A4,
                    Margins = new MarginSettings { Top = 10 },
                    DocumentTitle = "Orders PDF Report",
                };

                // Define object settings with the rendered HTML content
                var objectSettings = new ObjectSettings
                {
                    HtmlContent = htmlContent,
                };

                var pdf = new HtmlToPdfDocument()
                {
                    GlobalSettings = globalSettings,
                    Objects = { objectSettings }
                };

                // Convert HTML to PDF
                var fileBytes = _converter.Convert(pdf);

                // Set headers to display PDF inline
                Response.Headers.Add("Content-Disposition", "inline; filename=Orders.pdf");
                return File(fileBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return Content($"An error occurred: {ex.Message}\nFor more details, check the application console.");
            }
        }

        [HttpGet]
        [Route("/Pdf/PrintOrdersForDeliveryDetails")]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment,DeliveryCompany,DeliveryRepresentative")]
        public async Task<IActionResult> PrintOrdersForDeliveryDetails(string ids)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ids))
                {
                    return BadRequest("No order IDs provided.");
                }

                var orderIds = ids
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(id => int.TryParse(id.Trim(), out var num) ? num : -1)
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();

                if (!orderIds.Any())
                {
                    return BadRequest("Invalid order IDs format.");
                }

                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                IQueryable<Order> ordersQuery = _context.Orders
                    .AsNoTracking()
                    .Include(o => o.ManufacturingCompany)
                    .Include(o => o.DeliveryCompany)
                    .Include(o => o.OrderWarehouses)
                        .ThenInclude(ow => ow.Warehouse)
                        .ThenInclude(ow => ow.MainWarehouse)
                    .Where(o => orderIds.Contains(o.Id));

                if (User.IsInRole("FollowUpDepartment"))
                {
                    ordersQuery = ordersQuery.Where(o =>
                        o.ManufacturingCompany != null &&
                        o.ManufacturingCompany.EmployeeManufacturingCompanies.Any(access =>
                            access.ApplicationUserId == currentUserId &&
                            access.CanSeeManufacturingCompany));
                }

                if (User.IsInRole("DeliveryCompany") || User.IsInRole("DeliveryRepresentative"))
                {
                    ordersQuery = ordersQuery.Where(o =>
                        o.DeliveryCompany != null &&
                        o.DeliveryCompany.UserId == currentUserId);
                }

                var orders = await ordersQuery.ToListAsync();

                if (!orders.Any())
                {
                    return Forbid();
                }

                var ordersViewModels = new List<OrderViewModel>();
                DeliveryCompanyViewModel firstDeliveryCompany = null;
                string deliveryCompanyCountry = string.Empty;

                foreach (var order in orders)
                {
                    var qrCodeUrl = Url.Action("Details", "Order", new { id = order.Id }, protocol: Request.Scheme);

                    var orderViewModel = new OrderViewModel
                    {
                        Id = order.Id,
                        CustomerName = order.CustomerName ?? string.Empty,
                        Country = order.Country,
                        State = order.State ?? string.Empty,
                        Address = order.Address ?? string.Empty,
                        TelephoneNumber = order.TelephoneNumber ?? string.Empty,
                        CreatedDate = order.CreatedDate,
                        ManufacturingCompanyId = order.ManufacturingCompanyId,
                        ManufacturingCompany = order.ManufacturingCompany != null ? new ManufacturingCompanyViewModel
                        {
                            Id = order.ManufacturingCompany.Id,
                            Name = order.ManufacturingCompany.Name ?? "N/A"
                        } : null,
                        ManufacturingCompanyName = order.ManufacturingCompany?.Name ?? "N/A",
                        QRCodeUrl = qrCodeUrl,
                        SelectedWarehouses = order.OrderWarehouses?.Select(ow => new WarehouseAmountViewModel
                        {
                            WarehouseId = ow.WarehouseId,
                            WarehouseName = ow.Warehouse?.Name ?? "N/A",
                            Amount = ow.Amount,
                            Image = ow.Warehouse?.MainWarehouse?.ImageUrl ?? string.Empty,
                        }).ToList() ?? new List<WarehouseAmountViewModel>(),
                        DeliveryCompany = order.DeliveryCompany != null ? new DeliveryCompanyViewModel
                        {
                            Name = order.DeliveryCompany.Name ?? "N/A",
                        } : null,
                        TotalPrice = order.IsPaid ? 0 : order.TotalPrice,
                        IsPaid = order.IsPaid,
                    };

                    if (firstDeliveryCompany == null)
                    {
                        firstDeliveryCompany = orderViewModel.DeliveryCompany;
                        deliveryCompanyCountry = order.Country.ToString();
                    }

                    if (!string.IsNullOrWhiteSpace(qrCodeUrl))
                    {
                        using (var qrGenerator = new QRCodeGenerator())
                        {
                            var qrCodeData = qrGenerator.CreateQrCode(qrCodeUrl, QRCodeGenerator.ECCLevel.Q);
                            using (var qrCode = new QRCode(qrCodeData))
                            using (var qrCodeImage = qrCode.GetGraphic(20))
                            using (var ms = new MemoryStream())
                            {
                                qrCodeImage.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                                orderViewModel.QRCodeImageBase64 = Convert.ToBase64String(ms.ToArray());
                            }
                        }
                    }

                    ordersViewModels.Add(orderViewModel);
                }

                var printViewModel = new PrintOrdersForDeliveryViewModel
                {
                    OrderReportId = 1,
                    FirstDeliveryCompanyName = firstDeliveryCompany?.Name ?? "N/A",
                    DeliveryCompanyCountry = deliveryCompanyCountry,
                    TodayDate = DateTime.Now,
                    Orders = ordersViewModels
                };

                var htmlContent = await RenderViewAsync("PrintOrdersForDelivery", printViewModel);

                var globalSettings = new GlobalSettings
                {
                    ColorMode = ColorMode.Color,
                    Orientation = Orientation.Portrait,
                    PaperSize = PaperKind.A4,
                    Margins = new MarginSettings { Top = 10 },
                    DocumentTitle = "Orders PDF Report",
                };

                var objectSettings = new ObjectSettings
                {
                    HtmlContent = htmlContent,
                    WebSettings =
                    {
                        DefaultEncoding = "utf-8",
                        LoadImages = true
                    }
                };

                var pdf = new HtmlToPdfDocument()
                {
                    GlobalSettings = globalSettings,
                    Objects = { objectSettings }
                };

                var fileBytes = _converter.Convert(pdf);

                Response.Headers.Add("Content-Disposition", "inline; filename=Orders.pdf");
                return File(fileBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return Content($"An error occurred: {ex.Message}\nFor more details, check the application console.");
            }
        }


        [HttpGet]
        [Route("/Pdf/PrintEmployeeTransactionStatement")]
        [Authorize(Roles = "Admin,Accountant,Observer,ExecutiveDirector")]
        public async Task<IActionResult> PrintEmployeeTransactionStatement(int id)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest("Invalid transaction ID.");
                }

                var employeeTransaction = await _context.EmployeeTransactions
                    .AsNoTracking()
                    .Include(transaction => transaction.Employee)
                    .FirstOrDefaultAsync(transaction => transaction.Id == id && !transaction.IsDeleted);

                if (employeeTransaction == null)
                {
                    return NotFound("Employee transaction not found.");
                }

                var dayStart = employeeTransaction.Date.Date;
                var dayEnd = dayStart.AddDays(1);

                var sameDayTransactions = await _context.EmployeeTransactions
                    .AsNoTracking()
                    .Where(transaction =>
                        !transaction.IsDeleted &&
                        transaction.EmployeeId == employeeTransaction.EmployeeId &&
                        transaction.TransactionType == employeeTransaction.TransactionType &&
                        transaction.Date >= dayStart &&
                        transaction.Date < dayEnd)
                    .OrderBy(transaction => transaction.Id)
                    .ToListAsync();

                if (!sameDayTransactions.Any())
                {
                    sameDayTransactions.Add(employeeTransaction);
                }

                var employee = employeeTransaction.Employee;
                var employeeName = employee == null
                    ? "بدون اسم"
                    : (!string.IsNullOrWhiteSpace(employee.DisplayName)
                        ? employee.DisplayName
                        : (!string.IsNullOrWhiteSpace(employee.Name) ? employee.Name : "بدون اسم"));

                var employeeCode = employee == null ? BuildEmployeeCode(employeeTransaction.EmployeeId, "") : BuildEmployeeCode(employee.Id, employee.IdNumber);
                var employeeIdNumber = employee == null || string.IsNullOrWhiteSpace(employee.IdNumber) ? "-" : employee.IdNumber;
                var employeePhone = employee == null || string.IsNullOrWhiteSpace(employee.PhoneNumber) ? "-" : employee.PhoneNumber;
                var employeeAddress = employee == null || string.IsNullOrWhiteSpace(employee.Address) ? "-" : employee.Address;
                var employeeSalary = employee == null ? "-" : FormatEmployeeSalary(employee.Salary);
                var employeeAcademicLevel = employee == null || string.IsNullOrWhiteSpace(employee.AcademicLevel) ? "-" : employee.AcademicLevel;
                var employeeJobTitle = employee == null || string.IsNullOrWhiteSpace(employee.JobTitle) ? "-" : employee.JobTitle;
                var employeeDateOfBirth = employee == null ? "-" : employee.DateOfBirth.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
                var employeeGender = employee == null ? "-" : BuildGenderLabel(employee.Gender);
                var employeeCountry = employee == null || string.IsNullOrWhiteSpace(employee.Nationality) ? "-" : employee.Nationality;
                var employeeDateAdded = employee == null ? "-" : employee.DateAdded.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
                var employeeStatus = employee == null ? "-" : (employee.IsActive ? "نشط" : "غير نشط");
                var employeeAccountType = employee == null ? "-" : BuildEmployeeAccountType(employee.Salary, employee.Nationality, employee.Address);

                var shift = await _context.EmployeeWorkShifts
                    .AsNoTracking()
                    .Where(item => item.EmployeeId == employeeTransaction.EmployeeId)
                    .OrderByDescending(item => item.IsActive)
                    .ThenByDescending(item => item.CreatedAt)
                    .ThenByDescending(item => item.Id)
                    .Select(item => new
                    {
                        item.ShiftStartTime,
                        item.ShiftEndTime
                    })
                    .FirstOrDefaultAsync();

                var totalAmount = sameDayTransactions.Sum(transaction => transaction.Amount);
                var reasonText = BuildEmployeeTransactionPdfReasonText(sameDayTransactions.Select(transaction => transaction.Reason));

                if (string.IsNullOrWhiteSpace(reasonText))
                {
                    reasonText = employeeTransaction.TransactionType == TransactionTypeEnum.خصم
                        ? "خصم"
                        : employeeTransaction.TransactionType == TransactionTypeEnum.سلفة
                            ? "سلفة"
                            : "مكافأة";
                }

                var deductionAmount = employeeTransaction.TransactionType == TransactionTypeEnum.خصم ? totalAmount : 0m;
                var advanceAmount = employeeTransaction.TransactionType == TransactionTypeEnum.سلفة ? totalAmount : 0m;
                var bonusAmount = employeeTransaction.TransactionType == TransactionTypeEnum.مكافأة ? totalAmount : 0m;

                var row = new EmployeeTransactionStatementPdfRowViewModel
                {
                    EmployeeName = employeeName,
                    ShiftStartTime = shift == null ? "-" : FormatEmployeeTransactionPdfTime(shift.ShiftStartTime),
                    ShiftEndTime = shift == null ? "-" : FormatEmployeeTransactionPdfTime(shift.ShiftEndTime),
                    Reason = reasonText,
                    DeductionAmount = deductionAmount,
                    AdvanceAmount = advanceAmount,
                    BonusAmount = bonusAmount,
                    TransactionDate = dayStart.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                    TransactionType = employeeTransaction.TransactionType.ToString()
                };

                var viewModel = new EmployeeTransactionStatementPdfViewModel
                {
                    GeneratedAt = DateTime.Now,
                    StatementDate = DateTime.Now,
                    AccountOwner = "LUXIRA",
                    AccountNumber = "530000917",
                    EntryNumber = employeeTransaction.Id.ToString(CultureInfo.InvariantCulture),
                    IsSingleEmployee = true,
                    EmployeeName = employeeName,
                    EmployeeCode = employeeCode,
                    EmployeeIdNumber = employeeIdNumber,
                    EmployeePhoneNumber = employeePhone,
                    EmployeeAddress = employeeAddress,
                    EmployeeSalary = employeeSalary,
                    EmployeeAcademicLevel = employeeAcademicLevel,
                    EmployeeTasks = employeeJobTitle,
                    EmployeeDateOfBirth = employeeDateOfBirth,
                    EmployeeGender = employeeGender,
                    EmployeeCountry = employeeCountry,
                    EmployeeDateAdded = employeeDateAdded,
                    EmployeeStatus = employeeStatus,
                    AccountType = employeeAccountType,
                    Rows = new List<EmployeeTransactionStatementPdfRowViewModel> { row },
                    TotalDeductions = deductionAmount,
                    TotalBonuses = bonusAmount,
                    TotalAdvances = advanceAmount
                };

                var htmlContent = await RenderViewAsync("PrintEmployeeTransactionStatement", viewModel);
                var footerUrl = Url.Action("AttendancePdfFooter", "Pdf", null, Request.Scheme);

                var globalSettings = new GlobalSettings
                {
                    ColorMode = ColorMode.Color,
                    Orientation = Orientation.Portrait,
                    PaperSize = PaperKind.A4,
                    Margins = new MarginSettings
                    {
                        Top = 10,
                        Bottom = 14,
                        Left = 0,
                        Right = 0
                    },
                    DocumentTitle = "Employee Transaction Statement",
                };

                var objectSettings = new ObjectSettings
                {
                    HtmlContent = htmlContent,
                    WebSettings =
                    {
                        DefaultEncoding = "utf-8",
                        LoadImages = true
                    },
                    HeaderSettings =
                    {
                        FontSize = 8,
                        Right = "[page] / [toPage] صفحة",
                        Spacing = 3,
                        Line = false
                    },
                    FooterSettings =
                    {
                        HtmUrl = footerUrl,
                        Spacing = 0,
                        Line = false
                    }
                };

                var pdf = new HtmlToPdfDocument()
                {
                    GlobalSettings = globalSettings,
                    Objects = { objectSettings }
                };

                var fileBytes = _converter.Convert(pdf);

                Response.Headers.Add("Content-Disposition", $"inline; filename=EmployeeTransaction_{employeeTransaction.Id}.pdf");
                return File(fileBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return Content($"An error occurred: {ex.Message}\nFor more details, check the application console.");
            }
        }


        [HttpGet]
        [Route("/Pdf/PrintEmployeeTransactionsStatement")]
        [Authorize(Roles = "Admin,Accountant,Observer,ExecutiveDirector")]
        public async Task<IActionResult> PrintEmployeeTransactionsStatement(
            string ids = "",
            int? employeeId = null,
            bool formerEmployees = false,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            try
            {
                var selectedIds = (ids ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => int.TryParse(value.Trim(), out var parsedId) ? parsedId : 0)
                    .Where(value => value > 0)
                    .Distinct()
                    .ToList();

                if (!selectedIds.Any())
                {
                    return BadRequest("لازم تحدد حركة واحدة على الأقل.");
                }

                var selectedTransactions = await _context.EmployeeTransactions
                    .AsNoTracking()
                    .Where(transaction => selectedIds.Contains(transaction.Id) && !transaction.IsDeleted)
                    .Select(transaction => new
                    {
                        transaction.EmployeeId,
                        Day = transaction.Date.Date,
                        transaction.TransactionType
                    })
                    .ToListAsync();

                if (!selectedTransactions.Any())
                {
                    return BadRequest("لا توجد حركات محددة للطباعة.");
                }

                var selectedEmployeeIds = selectedTransactions.Select(transaction => transaction.EmployeeId).Distinct().ToList();
                var selectedTypes = selectedTransactions.Select(transaction => transaction.TransactionType).Distinct().ToList();
                var minSelectedDate = selectedTransactions.Min(transaction => transaction.Day);
                var maxSelectedDateExclusive = selectedTransactions.Max(transaction => transaction.Day).AddDays(1);

                IQueryable<EmployeeTransaction> query = _context.EmployeeTransactions
                    .AsNoTracking()
                    .Include(transaction => transaction.Employee)
                    .Where(transaction => !transaction.IsDeleted);

                query = query.Where(transaction =>
                    selectedEmployeeIds.Contains(transaction.EmployeeId) &&
                    selectedTypes.Contains(transaction.TransactionType) &&
                    transaction.Date >= minSelectedDate &&
                    transaction.Date < maxSelectedDateExclusive);

                var possibleTransactions = await query
                    .OrderByDescending(transaction => transaction.Date)
                    .ThenByDescending(transaction => transaction.Id)
                    .ToListAsync();

                var transactions = possibleTransactions
                    .Where(transaction => selectedTransactions.Any(selected =>
                        selected.EmployeeId == transaction.EmployeeId &&
                        selected.Day == transaction.Date.Date &&
                        selected.TransactionType == transaction.TransactionType))
                    .ToList();

                var employeeIds = transactions
                    .Select(transaction => transaction.EmployeeId)
                    .Distinct()
                    .ToList();

                var shifts = await _context.EmployeeWorkShifts
                    .AsNoTracking()
                    .Where(shift => employeeIds.Contains(shift.EmployeeId))
                    .OrderByDescending(shift => shift.IsActive)
                    .ThenByDescending(shift => shift.CreatedAt)
                    .ThenByDescending(shift => shift.Id)
                    .Select(shift => new
                    {
                        shift.EmployeeId,
                        shift.ShiftStartTime,
                        shift.ShiftEndTime
                    })
                    .ToListAsync();

                var shiftMap = shifts
                    .GroupBy(shift => shift.EmployeeId)
                    .ToDictionary(group => group.Key, group => group.First());

                var groupedRows = transactions
                    .GroupBy(transaction => new
                    {
                        transaction.EmployeeId,
                        Day = transaction.Date.Date,
                        transaction.TransactionType
                    })
                    .Select(group =>
                    {
                        var ordered = group.OrderByDescending(transaction => transaction.Date).ThenByDescending(transaction => transaction.Id).ToList();
                        var primary = ordered.First();
                        var employee = primary.Employee;
                        var employeeName = employee == null
                            ? "بدون اسم"
                            : (!string.IsNullOrWhiteSpace(employee.DisplayName)
                                ? employee.DisplayName
                                : (!string.IsNullOrWhiteSpace(employee.Name) ? employee.Name : "بدون اسم"));

                        shiftMap.TryGetValue(primary.EmployeeId, out var shift);

                        var totalAmount = ordered.Sum(transaction => transaction.Amount);
                        var reasonText = BuildEmployeeTransactionPdfReasonText(ordered.Select(transaction => transaction.Reason ?? string.Empty));

                        if (string.IsNullOrWhiteSpace(reasonText))
                        {
                            reasonText = primary.TransactionType == TransactionTypeEnum.خصم
                                ? "خصم"
                                : primary.TransactionType == TransactionTypeEnum.سلفة
                                    ? "سلفة"
                                    : "مكافأة";
                        }

                        return new EmployeeTransactionStatementPdfRowViewModel
                        {
                            EmployeeName = employeeName,
                            ShiftStartTime = shift == null ? "-" : FormatEmployeeTransactionPdfTime(shift.ShiftStartTime),
                            ShiftEndTime = shift == null ? "-" : FormatEmployeeTransactionPdfTime(shift.ShiftEndTime),
                            Reason = reasonText,
                            DeductionAmount = primary.TransactionType == TransactionTypeEnum.خصم ? totalAmount : 0m,
                            AdvanceAmount = primary.TransactionType == TransactionTypeEnum.سلفة ? totalAmount : 0m,
                            BonusAmount = primary.TransactionType == TransactionTypeEnum.مكافأة ? totalAmount : 0m,
                            TransactionDate = group.Key.Day.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                            TransactionType = primary.TransactionType.ToString()
                        };
                    })
                    .OrderByDescending(row => DateTime.TryParse(row.TransactionDate, out var parsedDate) ? parsedDate : DateTime.MinValue)
                    .ThenBy(row => row.EmployeeName)
                    .ToList();

                var singleEmployeeId = employeeIds.Distinct().Count() == 1 ? employeeIds.Distinct().FirstOrDefault() : 0;
                var singleEmployee = singleEmployeeId > 0
                    ? await _context.Employees.AsNoTracking().FirstOrDefaultAsync(employee => employee.Id == singleEmployeeId)
                    : null;

                var employeeNameToShow = "كل الموظفين";
                var employeeCodeToShow = "-";
                var employeeIdNumberToShow = "-";
                var employeePhoneToShow = "-";
                var employeeAddressToShow = "-";
                var employeeSalaryToShow = "-";
                var employeeAcademicLevelToShow = "-";
                var employeeJobTitleToShow = "-";
                var employeeDateOfBirthToShow = "-";
                var employeeGenderToShow = "-";
                var employeeCountryToShow = "-";
                var employeeDateAddedToShow = "-";
                var employeeStatusToShow = "-";
                var employeeAccountTypeToShow = "كشف عام";

                if (singleEmployee != null)
                {
                    employeeNameToShow = !string.IsNullOrWhiteSpace(singleEmployee.DisplayName)
                        ? singleEmployee.DisplayName
                        : (!string.IsNullOrWhiteSpace(singleEmployee.Name) ? singleEmployee.Name : "-");

                    employeeCodeToShow = BuildEmployeeCode(singleEmployee.Id, singleEmployee.IdNumber);
                    employeeIdNumberToShow = string.IsNullOrWhiteSpace(singleEmployee.IdNumber) ? "-" : singleEmployee.IdNumber;
                    employeePhoneToShow = string.IsNullOrWhiteSpace(singleEmployee.PhoneNumber) ? "-" : singleEmployee.PhoneNumber;
                    employeeAddressToShow = string.IsNullOrWhiteSpace(singleEmployee.Address) ? "-" : singleEmployee.Address;
                    employeeSalaryToShow = FormatEmployeeSalary(singleEmployee.Salary);
                    employeeAcademicLevelToShow = string.IsNullOrWhiteSpace(singleEmployee.AcademicLevel) ? "-" : singleEmployee.AcademicLevel;
                    employeeJobTitleToShow = string.IsNullOrWhiteSpace(singleEmployee.JobTitle) ? "-" : singleEmployee.JobTitle;
                    employeeDateOfBirthToShow = singleEmployee.DateOfBirth.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
                    employeeGenderToShow = BuildGenderLabel(singleEmployee.Gender);
                    employeeCountryToShow = string.IsNullOrWhiteSpace(singleEmployee.Nationality) ? "-" : singleEmployee.Nationality;
                    employeeDateAddedToShow = singleEmployee.DateAdded.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
                    employeeStatusToShow = singleEmployee.IsActive ? "نشط" : "غير نشط";
                    employeeAccountTypeToShow = BuildEmployeeAccountType(singleEmployee.Salary, singleEmployee.Nationality, singleEmployee.Address);
                }

                var viewModel = new EmployeeTransactionStatementPdfViewModel
                {
                    GeneratedAt = DateTime.Now,
                    StatementDate = DateTime.Now,
                    AccountOwner = "LUXIRA",
                    AccountNumber = "530000917",
                    EntryNumber = "1",
                    IsSingleEmployee = singleEmployee != null,
                    EmployeeName = employeeNameToShow,
                    EmployeeCode = employeeCodeToShow,
                    EmployeeIdNumber = employeeIdNumberToShow,
                    EmployeePhoneNumber = employeePhoneToShow,
                    EmployeeAddress = employeeAddressToShow,
                    EmployeeSalary = employeeSalaryToShow,
                    EmployeeAcademicLevel = employeeAcademicLevelToShow,
                    EmployeeTasks = employeeJobTitleToShow,
                    EmployeeDateOfBirth = employeeDateOfBirthToShow,
                    EmployeeGender = employeeGenderToShow,
                    EmployeeCountry = employeeCountryToShow,
                    EmployeeDateAdded = employeeDateAddedToShow,
                    EmployeeStatus = employeeStatusToShow,
                    AccountType = employeeAccountTypeToShow,
                    Rows = groupedRows,
                    TotalDeductions = groupedRows.Sum(row => row.DeductionAmount),
                    TotalBonuses = groupedRows.Sum(row => row.BonusAmount),
                    TotalAdvances = groupedRows.Sum(row => row.AdvanceAmount)
                };

                var htmlContent = await RenderViewAsync("PrintEmployeeTransactionStatement", viewModel);
                var footerUrl = Url.Action("AttendancePdfFooter", "Pdf", null, Request.Scheme);

                var globalSettings = new GlobalSettings
                {
                    ColorMode = ColorMode.Color,
                    Orientation = Orientation.Portrait,
                    PaperSize = PaperKind.A4,
                    Margins = new MarginSettings
                    {
                        Top = 10,
                        Bottom = 14,
                        Left = 0,
                        Right = 0
                    },
                    DocumentTitle = "Employee Transactions Statement",
                };

                var objectSettings = new ObjectSettings
                {
                    HtmlContent = htmlContent,
                    WebSettings =
                    {
                        DefaultEncoding = "utf-8",
                        LoadImages = true
                    },
                    HeaderSettings =
                    {
                        FontSize = 8,
                        Right = "[page] / [toPage] صفحة",
                        Spacing = 3,
                        Line = false
                    },
                    FooterSettings =
                    {
                        HtmUrl = footerUrl,
                        Spacing = 0,
                        Line = false
                    }
                };

                var pdf = new HtmlToPdfDocument()
                {
                    GlobalSettings = globalSettings,
                    Objects = { objectSettings }
                };

                var fileBytes = _converter.Convert(pdf);

                Response.Headers.Add("Content-Disposition", "inline; filename=EmployeeTransactionsStatement.pdf");
                return File(fileBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return Content($"An error occurred: {ex.Message}\nFor more details, check the application console.");
            }
        }

        private static string FormatEmployeeTransactionPdfTime(TimeSpan time)
        {
            return DateTime.Today.Add(time).ToString("hh:mm", CultureInfo.InvariantCulture) + " " + (time.Hours < 12 ? "ص" : "م");
        }

        private static string BuildEmployeeTransactionPdfReasonText(IEnumerable<string> reasons)
        {
            var result = new List<string>();
            var seenCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawReason in reasons ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(rawReason))
                {
                    continue;
                }

                var parts = rawReason
                    .Split(new[] { " + ", " / ", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => part.Trim())
                    .Where(part => !string.IsNullOrWhiteSpace(part));

                foreach (var part in parts)
                {
                    var category = BuildEmployeeTransactionPdfReasonCategory(part);

                    if (!string.IsNullOrWhiteSpace(category))
                    {
                        if (seenCategories.Contains(category))
                        {
                            continue;
                        }

                        seenCategories.Add(category);
                        result.Add(part);
                        continue;
                    }

                    if (seenTexts.Add(part))
                    {
                        result.Add(part);
                    }
                }
            }

            return result.Any() ? string.Join(" + ", result) : "";
        }

        private static string BuildEmployeeTransactionPdfReasonCategory(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return "";
            }

            if (reason.Contains("خروج مبكر", StringComparison.OrdinalIgnoreCase))
            {
                return "خروج مبكر";
            }

            if (reason.Contains("تأخر", StringComparison.OrdinalIgnoreCase) ||
                reason.Contains("تاخر", StringComparison.OrdinalIgnoreCase))
            {
                return "تأخر";
            }

            if (reason.Contains("غياب", StringComparison.OrdinalIgnoreCase) ||
                reason.Contains("غائب", StringComparison.OrdinalIgnoreCase))
            {
                return "غياب";
            }

            return "";
        }

        [AllowAnonymous]
        public IActionResult AttendancePdfFooter()
        {
            var footerHtml = @"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"" />
    <style>
        html, body {
            margin: 0;
            padding: 0;
            width: 100%;
            height: 44px;
            overflow: hidden;
            font-family: Arial, Tahoma, sans-serif;
        }

        .footer-bar {
            width: 100%;
            height: 44px;
            background: #a3c8be;
            background: -webkit-linear-gradient(left, #ffffff 0%, #f4faf8 16%, #e7f2ef 32%, #d7e9e4 52%, #bfdad2 74%, #a3c8be 100%);
            background: linear-gradient(to right, #ffffff 0%, #f4faf8 16%, #e7f2ef 32%, #d7e9e4 52%, #bfdad2 74%, #a3c8be 100%);
            color: #07111f;
            display: table;
            table-layout: fixed;
            font-weight: 700;
            box-sizing: border-box;
        }

        .footer-left,
        .footer-right {
            display: table-cell;
            vertical-align: middle;
            padding: 0 16px;
            white-space: nowrap;
        }

        .footer-left {
            text-align: left;
            font-size: 10px;
        }

        .footer-right {
            text-align: right;
            font-size: 12px;
            font-weight: 900;
        }
    </style>
</head>
<body>
    <div class=""footer-bar"">
        <div class=""footer-left"">LUXIRA HOLDING - Employee Account Statement</div>
        <div class=""footer-right"">+90 538 646 66 63</div>
    </div>
</body>
</html>";

            return Content(footerHtml, "text/html; charset=utf-8");
        }

        [Authorize]
        public async Task<IActionResult> PrintAttendanceLog(string ids)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ids))
                {
                    return BadRequest("No attendance log IDs provided.");
                }

                var attendanceLogIds = ids
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(id => int.TryParse(id.Trim(), out var parsedId) ? parsedId : -1)
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();

                if (!attendanceLogIds.Any())
                {
                    return BadRequest("Invalid attendance log IDs format.");
                }

                var attendanceLogs = await _context.EmployeeAttendanceLogs
                    .AsNoTracking()
                    .Where(log => attendanceLogIds.Contains(log.Id))
                    .OrderBy(log => log.CheckInAt)
                    .Select(log => new
                    {
                        log.Id,
                        log.EmployeeId,
                        EmployeeName = log.EmployeeName == null ? "" : log.EmployeeName,
                        EmployeeEmail = log.EmployeeEmail == null ? "" : log.EmployeeEmail,
                        log.CheckInAt,
                        log.CheckOutAt,
                        log.DeductionAmount,
                        DeductionReason = log.DeductionReason == null ? "" : log.DeductionReason,
                        CheckInIpAddress = log.CheckInIpAddress == null ? "" : log.CheckInIpAddress,
                        CheckInLocation = log.CheckInLocation == null ? "" : log.CheckInLocation
                    })
                    .ToListAsync();

                if (!attendanceLogs.Any())
                {
                    return NotFound("No attendance logs found with the provided IDs.");
                }

                var employeeIds = attendanceLogs
                    .Where(log => log.EmployeeId.HasValue)
                    .Select(log => log.EmployeeId.Value)
                    .Distinct()
                    .ToList();

                var uniqueEmployeeNamesFromLogs = attendanceLogs
                    .Select(log => (log.EmployeeName ?? "").Trim())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var singleEmployeeNameFromLogs = uniqueEmployeeNamesFromLogs.Count == 1
                    ? uniqueEmployeeNamesFromLogs.First().Trim()
                    : "";

                var hasSingleEmployeeNameFromLogs = !string.IsNullOrWhiteSpace(singleEmployeeNameFromLogs);

                var employeesQuery = _context.Employees.AsNoTracking();

                if (hasSingleEmployeeNameFromLogs)
                {
                    employeesQuery = employeesQuery.Where(employee =>
                        employeeIds.Contains(employee.Id) ||
                        (employee.Name != null && employee.Name.Trim() == singleEmployeeNameFromLogs) ||
                        (employee.DisplayName != null && employee.DisplayName.Trim() == singleEmployeeNameFromLogs));
                }
                else
                {
                    employeesQuery = employeesQuery.Where(employee => employeeIds.Contains(employee.Id));
                }

                var employees = await employeesQuery
                    .Select(employee => new
                    {
                        employee.Id,
                        Name = employee.Name == null ? "" : employee.Name,
                        DisplayName = employee.DisplayName == null ? "" : employee.DisplayName,
                        IdNumber = employee.IdNumber == null ? "" : employee.IdNumber,
                        Nationality = employee.Nationality == null ? "" : employee.Nationality,
                        PhoneNumber = employee.PhoneNumber == null ? "" : employee.PhoneNumber,
                        Address = employee.Address == null ? "" : employee.Address,
                        employee.Salary,
                        AcademicLevel = employee.AcademicLevel == null ? "" : employee.AcademicLevel,
                        JobTitle = employee.JobTitle == null ? "" : employee.JobTitle,
                        employee.DateOfBirth,
                        employee.Gender,
                        employee.DateAdded,
                        employee.IsActive,
                        employee.IsShown
                    })
                    .ToListAsync();

                var employeeNames = employees
                    .GroupBy(employee => employee.Id)
                    .ToDictionary(
                        group => group.Key,
                        group =>
                        {
                            var employee = group.First();
                            return !string.IsNullOrWhiteSpace(employee.DisplayName)
                                ? employee.DisplayName
                                : employee.Name;
                        });

                var singleEmployee = employeeIds.Count == 1
                    ? employees.FirstOrDefault(employee => employee.Id == employeeIds.First())
                    : null;

                if (singleEmployee == null && !string.IsNullOrWhiteSpace(singleEmployeeNameFromLogs))
                {
                    singleEmployee = employees.FirstOrDefault(employee =>
                        string.Equals((employee.DisplayName ?? "").Trim(), singleEmployeeNameFromLogs, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals((employee.Name ?? "").Trim(), singleEmployeeNameFromLogs, StringComparison.OrdinalIgnoreCase));
                }

                if (singleEmployee == null && employees.Select(employee => employee.Id).Distinct().Count() == 1)
                {
                    singleEmployee = employees.FirstOrDefault();
                }

                var singleEmployeeId = singleEmployee == null ? (int?)null : singleEmployee.Id;

                var shiftEmployeeIds = employeeIds.ToList();

                if (singleEmployeeId.HasValue && !shiftEmployeeIds.Contains(singleEmployeeId.Value))
                {
                    shiftEmployeeIds.Add(singleEmployeeId.Value);
                }

                var shifts = await _context.EmployeeWorkShifts
                    .AsNoTracking()
                    .Where(shift => shift.IsActive && shiftEmployeeIds.Contains(shift.EmployeeId))
                    .OrderByDescending(shift => shift.Id)
                    .Select(shift => new
                    {
                        shift.EmployeeId,
                        shift.ShiftStartTime,
                        shift.ShiftEndTime
                    })
                    .ToListAsync();

                var maxAttendanceDateExclusive = attendanceLogs.Any()
                    ? attendanceLogs.Max(log => log.CheckInAt.Date).AddDays(1)
                    : DateTime.Today.AddDays(1);

                var employeeTransactions = await _context.EmployeeTransactions
                    .AsNoTracking()
                    .Where(transaction =>
                        employeeIds.Contains(transaction.EmployeeId) &&
                        !transaction.IsDeleted &&
                        transaction.Date < maxAttendanceDateExclusive &&
                        (
                            (int)transaction.TransactionType == 0 || // خصم
                            (int)transaction.TransactionType == 1 || // مكافأة
                            (int)transaction.TransactionType == 2    // سلفة
                        ))
                    .Select(transaction => new
                    {
                        transaction.EmployeeId,
                        transaction.Amount,
                        TransactionType = (int)transaction.TransactionType,
                        transaction.Date
                    })
                    .ToListAsync();

                decimal SumTransactionsUpToAttendanceDate(int? currentEmployeeId, DateTime attendanceDate, int transactionType)
                {
                    if (!currentEmployeeId.HasValue)
                    {
                        return 0m;
                    }

                    var dateLimitExclusive = attendanceDate.Date.AddDays(1);

                    return employeeTransactions
                        .Where(transaction =>
                            transaction.EmployeeId == currentEmployeeId.Value &&
                            transaction.TransactionType == transactionType &&
                            transaction.Date < dateLimitExclusive)
                        .Sum(transaction => transaction.Amount);
                }

                var totalCumulativeBonuses = employeeTransactions
                    .Where(transaction => transaction.TransactionType == 1)
                    .Sum(transaction => transaction.Amount);

                var totalCumulativeAdvances = employeeTransactions
                    .Where(transaction => transaction.TransactionType == 2)
                    .Sum(transaction => transaction.Amount);

                var totalBasicLateMinutes = 0;

                var rows = attendanceLogs.Select(log =>
                {
                    var shift = log.EmployeeId.HasValue
                        ? shifts.FirstOrDefault(shiftItem => shiftItem.EmployeeId == log.EmployeeId.Value)
                        : null;

                    var employeeName = log.EmployeeName;

                    if (string.IsNullOrWhiteSpace(employeeName) && log.EmployeeId.HasValue && employeeNames.ContainsKey(log.EmployeeId.Value))
                    {
                        employeeName = employeeNames[log.EmployeeId.Value];
                    }

                    if (string.IsNullOrWhiteSpace(employeeName))
                    {
                        employeeName = log.EmployeeEmail;
                    }

                    if (string.IsNullOrWhiteSpace(employeeName))
                    {
                        employeeName = "بدون اسم";
                    }

                    var shiftHours = shift == null
                        ? "-"
                        : $"{shift.ShiftStartTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture)} - {shift.ShiftEndTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture)}";

                    var attendanceStatus = "بدون شيفت";

                    var basicLateMinutes = 0;

                    if (shift != null)
                    {
                        basicLateMinutes = CalculateBasicLateMinutes(log.CheckInAt, shift.ShiftStartTime);

                        attendanceStatus = basicLateMinutes > 0
                            ? "متأخر"
                            : "منضبط";
                    }

                    totalBasicLateMinutes += basicLateMinutes;

                    var deductionAmount = log.DeductionAmount ?? 0m;

                    var deductionReason = log.DeductionReason;

                    if (string.IsNullOrWhiteSpace(deductionReason))
                    {
                        deductionReason = deductionAmount > 0
                            ? "خصم تأخير"
                            : "لا يوجد تأخير";
                    }

                    // الخصم في الـ PDF مطلوب يوم بيومه، لذلك نعرض خصم سجل الحضور نفسه فقط.
                    // المكافآت والسلف تظل تراكمية حتى تاريخ سجل الحضور.
                    var dailyDeductionAmount = deductionAmount;
                    var cumulativeBonusAmount = SumTransactionsUpToAttendanceDate(log.EmployeeId, log.CheckInAt, 1);
                    var cumulativeAdvanceAmount = SumTransactionsUpToAttendanceDate(log.EmployeeId, log.CheckInAt, 2);

                    return new AttendanceLogPdfRowViewModel
                    {
                        EmployeeName = employeeName,
                        Date = log.CheckInAt.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                        ShiftHours = shiftHours,
                        CheckInTime = log.CheckInAt.ToString("HH:mm", CultureInfo.InvariantCulture),
                        CheckOutTime = log.CheckOutAt.HasValue
                            ? log.CheckOutAt.Value.ToString("HH:mm", CultureInfo.InvariantCulture)
                            : "-",
                        AttendanceStatus = attendanceStatus,
                        DeductionAmount = dailyDeductionAmount,
                        BonusAmount = cumulativeBonusAmount,
                        AdvanceAmount = cumulativeAdvanceAmount,
                        DeductionReason = deductionReason,
                        IpAddress = string.IsNullOrWhiteSpace(log.CheckInIpAddress) ? "-" : log.CheckInIpAddress,
                        Location = string.IsNullOrWhiteSpace(log.CheckInLocation) ? "-" : log.CheckInLocation
                    };
                }).ToList();

                var isSingleEmployeeStatement = singleEmployee != null || !string.IsNullOrWhiteSpace(singleEmployeeNameFromLogs);

                var employeeNameToShow = "-";
                var employeeCodeToShow = "-";
                var employeeIdNumberToShow = "-";
                var employeePhoneToShow = "-";
                var employeeAddressToShow = "-";
                var employeeSalaryToShow = "-";
                var employeeAcademicLevelToShow = "-";
                var employeeJobTitleToShow = "-";
                var employeeDateOfBirthToShow = "-";
                var employeeGenderToShow = "-";
                var employeeCountryToShow = "-";
                var employeeDateAddedToShow = "-";
                var employeeStatusToShow = "-";
                var employeeAccountTypeToShow = "-";

                if (singleEmployee != null)
                {
                    employeeNameToShow = !string.IsNullOrWhiteSpace(singleEmployee.DisplayName)
                        ? singleEmployee.DisplayName
                        : (!string.IsNullOrWhiteSpace(singleEmployee.Name) ? singleEmployee.Name : "-");

                    employeeCodeToShow = BuildEmployeeCode(singleEmployee.Id, singleEmployee.IdNumber);
                    employeeIdNumberToShow = string.IsNullOrWhiteSpace(singleEmployee.IdNumber) ? "-" : singleEmployee.IdNumber;
                    employeePhoneToShow = string.IsNullOrWhiteSpace(singleEmployee.PhoneNumber) ? "-" : singleEmployee.PhoneNumber;
                    employeeAddressToShow = string.IsNullOrWhiteSpace(singleEmployee.Address) ? "-" : singleEmployee.Address;
                    employeeSalaryToShow = FormatEmployeeSalary(singleEmployee.Salary);
                    employeeAcademicLevelToShow = string.IsNullOrWhiteSpace(singleEmployee.AcademicLevel) ? "-" : singleEmployee.AcademicLevel;
                    employeeJobTitleToShow = string.IsNullOrWhiteSpace(singleEmployee.JobTitle) ? "-" : singleEmployee.JobTitle;
                    employeeDateOfBirthToShow = singleEmployee.DateOfBirth.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
                    employeeGenderToShow = BuildGenderLabel(singleEmployee.Gender);
                    employeeCountryToShow = string.IsNullOrWhiteSpace(singleEmployee.Nationality) ? "-" : singleEmployee.Nationality;
                    employeeDateAddedToShow = singleEmployee.DateAdded.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
                    employeeStatusToShow = singleEmployee.IsActive ? "نشط" : "غير نشط";
                    employeeAccountTypeToShow = BuildEmployeeAccountType(singleEmployee.Salary, singleEmployee.Nationality, singleEmployee.Address);
                }
                else if (!string.IsNullOrWhiteSpace(singleEmployeeNameFromLogs))
                {
                    employeeNameToShow = singleEmployeeNameFromLogs;
                }
                else if (employeeIds.Count == 1)
                {
                    employeeNameToShow = rows.FirstOrDefault()?.EmployeeName ?? "-";
                    employeeCodeToShow = BuildEmployeeCode(employeeIds.First(), "");
                }

                var viewModel = new AttendanceLogPdfViewModel
                {
                    GeneratedAt = DateTime.Now,
                    StatementDate = DateTime.Now,
                    AccountOwner = "LUXIRA",
                    AccountNumber = "530000917",
                    EntryNumber = "1",
                    IsSingleEmployee = isSingleEmployeeStatement,
                    EmployeeName = employeeNameToShow,
                    EmployeeCode = employeeCodeToShow,
                    EmployeeIdNumber = employeeIdNumberToShow,
                    EmployeePhoneNumber = employeePhoneToShow,
                    EmployeeAddress = employeeAddressToShow,
                    EmployeeSalary = employeeSalaryToShow,
                    EmployeeAcademicLevel = employeeAcademicLevelToShow,
                    EmployeeTasks = employeeJobTitleToShow,
                    EmployeeDateOfBirth = employeeDateOfBirthToShow,
                    EmployeeGender = employeeGenderToShow,
                    EmployeeCountry = employeeCountryToShow,
                    EmployeeDateAdded = employeeDateAddedToShow,
                    EmployeeStatus = employeeStatusToShow,
                    AccountType = employeeAccountTypeToShow,
                    Rows = rows,
                    TotalDeductions = rows.Sum(row => row.DeductionAmount),
                    TotalBonuses = totalCumulativeBonuses,
                    TotalAdvances = totalCumulativeAdvances,
                    TotalBasicLateMinutes = totalBasicLateMinutes,
                    TotalBasicLateTimeText = FormatBasicLateTime(totalBasicLateMinutes)
                };

                var htmlContent = await RenderViewAsync("PrintAttendanceLog", viewModel);
                var footerUrl = Url.Action("AttendancePdfFooter", "Pdf", null, Request.Scheme);

                var globalSettings = new GlobalSettings
                {
                    ColorMode = ColorMode.Color,
                    Orientation = Orientation.Portrait,
                    PaperSize = PaperKind.A4,
                    Margins = new MarginSettings
                    {
                        Top = 10,
                        Bottom = 14,
                        Left = 0,
                        Right = 0
                    },
                    DocumentTitle = "Employee Account PDF Report",
                };

                var objectSettings = new ObjectSettings
                {
                    HtmlContent = htmlContent,
                    WebSettings =
                    {
                        DefaultEncoding = "utf-8",
                        LoadImages = true
                    },
                    HeaderSettings =
                    {
                        FontSize = 8,
                        Right = "[page] / [toPage] صفحة",
                        Spacing = 3,
                        Line = false
                    },
                    FooterSettings =
                    {
                        HtmUrl = footerUrl,
                        Spacing = 0,
                        Line = false
                    }
                };

                var pdf = new HtmlToPdfDocument()
                {
                    GlobalSettings = globalSettings,
                    Objects = { objectSettings }
                };

                var fileBytes = _converter.Convert(pdf);

                Response.Headers.Add("Content-Disposition", "inline; filename=EmployeeAccount.pdf");
                return File(fileBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return Content($"An error occurred: {ex.Message}\nFor more details, check the application console.");
            }
        }


        private string BuildEmployeeCode(int? employeeId, string? idNumber)
        {
            if (!string.IsNullOrWhiteSpace(idNumber))
            {
                return idNumber.Trim();
            }

            return employeeId.HasValue && employeeId.Value > 0
                ? $"EMP-{employeeId.Value:D6}"
                : "-";
        }

        private string FormatEmployeeSalary(decimal salary)
        {
            return salary.ToString("#,##0.00", CultureInfo.InvariantCulture);
        }

        private string BuildGenderLabel(bool gender)
        {
            return gender ? "ذكر" : "أنثى";
        }

        private static int CalculateBasicLateMinutes(DateTime checkInAt, TimeSpan shiftStartTime)
        {
            var checkInTime = checkInAt.TimeOfDay;

            if (checkInTime <= shiftStartTime)
            {
                return 0;
            }

            return (int)Math.Ceiling((checkInTime - shiftStartTime).TotalMinutes);
        }

        private static string FormatBasicLateTime(int totalMinutes)
        {
            if (totalMinutes <= 0)
            {
                return "0 دقيقة";
            }

            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;

            if (hours > 0 && minutes > 0)
            {
                return $"{hours} ساعة و {minutes} دقيقة";
            }

            if (hours > 0)
            {
                return $"{hours} ساعة";
            }

            return $"{minutes} دقيقة";
        }

        private string BuildEmployeeAccountType(decimal salary, string? nationality, string? address)
        {
            var searchText = $"{nationality ?? ""} {address ?? ""}".ToLowerInvariant();

            if (searchText.Contains("turkey") ||
                searchText.Contains("turkiye") ||
                searchText.Contains("türkiye") ||
                searchText.Contains("تركيا"))
            {
                return "Cari Hesap, TL";
            }

            return "Cari Hesap, EGP";
        }


        private async Task<List<OrderViewModel>> BuildOrderViewModels(int[] ids)
        {
            var orders = await _context.Orders
                .Where(o => ids.Contains(o.Id))
                .Include(o => o.ManufacturingCompany)
                .Include(o => o.OrderWarehouses)
                    .ThenInclude(ow => ow.Warehouse)
                                        .ThenInclude(ow => ow.MainWarehouse)

                .ToListAsync();

            var tasks = orders.Select(async order =>
            {
                var qrCodeUrl = Url.Action("PrintOrder", "Order", new { id = order.Id }, protocol: Request.Scheme);
                var qrCodeBase64 = GenerateQRCodeBase64(qrCodeUrl);

                return new OrderViewModel
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
                    ManufacturingCompanyName = order.ManufacturingCompany.Name,
                    QRCodeUrl = qrCodeUrl,
                    QRCodeImageBase64 = qrCodeBase64,
                    SelectedWarehouses = order.OrderWarehouses.Select(ow => new WarehouseAmountViewModel
                    {
                        WarehouseId = ow.WarehouseId,
                        WarehouseName = ow.Warehouse?.Name ?? "N/A",
                        Amount = ow.Amount,
                        Image = ow.Warehouse?.MainWarehouse.ImageUrl,
                    }).ToList(),
                    TotalPrice = order.IsPaid ? 0 : order.TotalPrice
                };
            });

            var orderViewModels = await Task.WhenAll(tasks);
            return orderViewModels.ToList(); // Convert the array to a list before returning
        }



        private async Task<string> RenderViewAsync<TModel>(string viewName, TModel model)
        {
            ViewData.Model = model;
            using (var writer = new StringWriter())
            {
                var viewResult = _viewEngine.FindView(ControllerContext, viewName, true);
                if (viewResult.View == null)
                {
                    throw new ArgumentNullException($"View {viewName} not found.");
                }

                var viewContext = new ViewContext(
                    ControllerContext,
                    viewResult.View,
                    ViewData,
                    TempData,
                    writer,
                    new HtmlHelperOptions()
                );

                await viewResult.View.RenderAsync(viewContext);
                return writer.ToString();
            }
        }

        private string GenerateQRCodeBase64(string data)
        {
            using (var qrGenerator = new QRCodeGenerator())
            {
                var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
                using (var qrCode = new QRCode(qrCodeData))
                {
                    using (var qrCodeImage = qrCode.GetGraphic(20))
                    {
                        using (var ms = new MemoryStream())
                        {
                            qrCodeImage.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                            return Convert.ToBase64String(ms.ToArray());
                        }
                    }
                }
            }
        }



    }

    public class AttendanceLogPdfViewModel
    {
        public DateTime GeneratedAt { get; set; }

        public DateTime StatementDate { get; set; }

        public string AccountOwner { get; set; } = "LUXIRA";

        public string AccountNumber { get; set; } = "530000917";

        public string EntryNumber { get; set; } = "1";

        public bool IsSingleEmployee { get; set; }

        public string EmployeeName { get; set; } = "-";

        public string EmployeeCode { get; set; } = "-";

        public string EmployeeIdNumber { get; set; } = "-";

        public string EmployeePhoneNumber { get; set; } = "-";

        public string EmployeeAddress { get; set; } = "-";

        public string EmployeeSalary { get; set; } = "-";

        public string EmployeeAcademicLevel { get; set; } = "-";

        public string EmployeeTasks { get; set; } = "-";

        public string EmployeeDateOfBirth { get; set; } = "-";

        public string EmployeeGender { get; set; } = "-";

        public string EmployeeCountry { get; set; } = "-";

        public string EmployeeDateAdded { get; set; } = "-";

        public string EmployeeStatus { get; set; } = "-";

        public string AccountType { get; set; } = "-";

        public List<AttendanceLogPdfRowViewModel> Rows { get; set; } = new List<AttendanceLogPdfRowViewModel>();

        public decimal TotalDeductions { get; set; }

        public decimal TotalBonuses { get; set; }

        public decimal TotalAdvances { get; set; }

        public int TotalBasicLateMinutes { get; set; }

        public string TotalBasicLateTimeText { get; set; } = "0 دقيقة";
    }

    public class AttendanceLogPdfRowViewModel
    {
        public string EmployeeName { get; set; } = "";

        public string Date { get; set; } = "";

        public string ShiftHours { get; set; } = "";

        public string CheckInTime { get; set; } = "";

        public string CheckOutTime { get; set; } = "";

        public string AttendanceStatus { get; set; } = "";

        public decimal DeductionAmount { get; set; }

        public decimal BonusAmount { get; set; }

        public decimal AdvanceAmount { get; set; }

        public string DeductionReason { get; set; } = "";

        public string IpAddress { get; set; } = "";

        public string Location { get; set; } = "";
    }


    public class EmployeeTransactionStatementPdfViewModel
    {
        public DateTime GeneratedAt { get; set; }

        public DateTime StatementDate { get; set; }

        public string AccountOwner { get; set; } = "LUXIRA";

        public string AccountNumber { get; set; } = "530000917";

        public string EntryNumber { get; set; } = "1";

        public bool IsSingleEmployee { get; set; }

        public string EmployeeName { get; set; } = "-";

        public string EmployeeCode { get; set; } = "-";

        public string EmployeeIdNumber { get; set; } = "-";

        public string EmployeePhoneNumber { get; set; } = "-";

        public string EmployeeAddress { get; set; } = "-";

        public string EmployeeSalary { get; set; } = "-";

        public string EmployeeAcademicLevel { get; set; } = "-";

        public string EmployeeTasks { get; set; } = "-";

        public string EmployeeDateOfBirth { get; set; } = "-";

        public string EmployeeGender { get; set; } = "-";

        public string EmployeeCountry { get; set; } = "-";

        public string EmployeeDateAdded { get; set; } = "-";

        public string EmployeeStatus { get; set; } = "-";

        public string AccountType { get; set; } = "-";

        public List<EmployeeTransactionStatementPdfRowViewModel> Rows { get; set; } = new List<EmployeeTransactionStatementPdfRowViewModel>();

        public decimal TotalDeductions { get; set; }

        public decimal TotalBonuses { get; set; }

        public decimal TotalAdvances { get; set; }
    }

    public class EmployeeTransactionStatementPdfRowViewModel
    {
        public string EmployeeName { get; set; } = "";

        public string ShiftStartTime { get; set; } = "";

        public string ShiftEndTime { get; set; } = "";

        public string Reason { get; set; } = "";

        public decimal DeductionAmount { get; set; }

        public decimal AdvanceAmount { get; set; }

        public decimal BonusAmount { get; set; }

        public string TransactionDate { get; set; } = "";

        public string TransactionType { get; set; } = "";
    }

}