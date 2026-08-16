using lotus_blue.Data;
using lotus_blue.Models;
using Microsoft.AspNetCore.Mvc;
using lotus_blue.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using lotus_blue.Models.ViewModel;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Linq;

namespace lotus_blue.Controllers
{
    public class DeliveryCompanyController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly FileUploadService _fileUploadService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly GetCurrentTimeInIstanbul _timeService;

        public DeliveryCompanyController(ApplicationDbContext context, FileUploadService fileUploadService, UserManager<ApplicationUser> userManager, GetCurrentTimeInIstanbul timeService)
        {
            _context = context;
            _fileUploadService = fileUploadService;
            _userManager = userManager;
            _timeService = timeService;
        }

        [Authorize(Roles = "Admin,Accountant,Observer,ExecutiveDirector,FollowUpDepartment")]
        public IActionResult Index(int page = 1, int pageSize = 10, Common.Countries? countryId = null, int? deliverycompanyId = null)
        {
            IQueryable<DeliveryCompanyViewModel> query = _context.DeliveryCompanies
                .Where(a => !a.IsRepresentative)
                .Include(a => a.User)
                .Select(e => new DeliveryCompanyViewModel
                {
                    Id = e.Id,
                    Logo = e.ImageUrl,
                    Name = e.Name,
                    PhoneNumber = e.PhoneNumber,
                    Email = e.User.Email,
                    Specialty = e.specialty,
                    Country = e.Country,
                    IsShown = e.IsShown,
                    IsActive = e.User.EmailConfirmed,
                    City = e.City,
                    IsAllOrdersHidden = e.IsAllOrdersHidden
                });

            if (countryId.HasValue)
                query = query.Where(a => a.Country == countryId.Value);

            if (deliverycompanyId.HasValue)
                query = query.Where(a => a.Id == deliverycompanyId.Value);

            int totalItems = query.Count();

            var viewModel = new PaginationViewModel<DeliveryCompanyViewModel>
            {
                Items = query.Skip((page - 1) * pageSize)
                             .Take(pageSize)
                             .ToList(),
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };

            return View(viewModel);
        }

        [HttpGet]
        [Authorize(Roles = "Admin,FollowUpDepartment,ExecutiveDirector")]
        public IActionResult Create()
        {
            var model = new DeliveryCompanyViewModel();
            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,FollowUpDepartment,ExecutiveDirector")]
        public async Task<IActionResult> Create(DeliveryCompanyViewModel model, IFormFile? logoFile, IFormFile? infoFile)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    Name = model.DisplayName
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "DeliveryCompany");

                    var deliveryCompany = new DeliveryCompany
                    {
                        Name = model.Name,
                        TaxRegistrationNumber = model.TaxRegistrationNumber,
                        IdNumber = model.IdNumber,
                        Address = model.Address,
                        PhoneNumber = model.PhoneNumber,
                        specialty = model.Specialty,
                        Website = model.Website,
                        Notes = model.Notes,
                        CreatedDate = _timeService.GetIstanbulTimeWithOffset(),
                        Country = model.Country,
                        City = model.City,
                        UserId = user.Id,
                        IsRepresentative = false,
                        DisplayName = model.DisplayName,
                    };

                    if (logoFile != null)
                        deliveryCompany.ImageUrl = await _fileUploadService.UploadFileAsync(logoFile, "deliverycompanies");

                    if (infoFile != null)
                        deliveryCompany.InformationUrl = await _fileUploadService.UploadFileAsync(infoFile, "deliverycompanies");

                    _context.Add(deliveryCompany);
                    await _context.SaveChangesAsync();

                    user.AcessId = deliveryCompany.Id;
                    await _userManager.UpdateAsync(user);

                    // Find the delivery company with the most warehouses
                    var companyWithMostWarehouses = _context.Warehouses
                        .GroupBy(w => w.DeliveryCompanyId)
                        .OrderByDescending(g => g.Count())
                        .Select(g => g.Key)
                        .FirstOrDefault();

                    if (companyWithMostWarehouses != 0)
                    {
                        var warehousesToCopy = _context.Warehouses
                                                        .Include(a => a.SubWarehouse)

                            .Where(w => w.DeliveryCompanyId == companyWithMostWarehouses)
                            .ToList();

                        foreach (var warehouse in warehousesToCopy)
                        {
                            var newWarehouse = new Warehouse
                            {
                                Name = warehouse.Name,
                                SubWarehouseId= warehouse.SubWarehouseId,
                                Price = warehouse.Price,
                                UnchangingAmount = 0,
                                Amount = 0, // Set amount to 0
                                DeliveryCompanyId = deliveryCompany.Id,
                                ManufacturingCompanyId = warehouse.ManufacturingCompanyId,
                                DateAdded = DateTime.Now,
                                DateUpdated = DateTime.Now,
                                MainWarehouseId = warehouse.SubWarehouse.MainWarehouseId ?? 0,
                                Countries = deliveryCompany.Country,
                                City = deliveryCompany.City,
                                IsShown = true,
                            };
                            _context.Warehouses.Add(newWarehouse);
                        }
                        await _context.SaveChangesAsync();
                    }

                    return RedirectToAction("Index");
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }
            return View(model);
        }



        [Authorize(Roles = "Admin,FollowUpDepartment,ExecutiveDirector")]
        public IActionResult Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var deliveryCompany = _context.DeliveryCompanies
                .Include(dc => dc.User)
                .FirstOrDefault(dc => dc.Id == id);
            if (deliveryCompany == null)
            {
                return NotFound();
            }

            var viewModel = new DeliveryCompanyViewModel
            {
                Id = deliveryCompany.Id,
                Name = deliveryCompany.Name,
                Address = deliveryCompany.Address,
                TaxRegistrationNumber = deliveryCompany.TaxRegistrationNumber,
                PhoneNumber = deliveryCompany.PhoneNumber,
                IdNumber = deliveryCompany.IdNumber,
                Website = deliveryCompany.Website,
                Specialty = deliveryCompany.specialty,
                Notes = deliveryCompany.Notes,
                Email = deliveryCompany.User.Email,
                Country = deliveryCompany.Country,
                IsActive = deliveryCompany.User.EmailConfirmed,
                IsShown = deliveryCompany.IsShown,
                DisplayName = deliveryCompany.DisplayName,
                Logo = deliveryCompany.ImageUrl,
                InformationUrl = deliveryCompany.InformationUrl
            };

            return View(viewModel);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,FollowUpDepartment,ExecutiveDirector")]
        public async Task<IActionResult> Edit(int id, DeliveryCompanyViewModel model, IFormFile? logoFile, IFormFile? infoFile, string newPassword, string ConfirmNewPassword, string newEmail)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            var existingCompany = await _context.DeliveryCompanies
                .Include(a => a.User)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (existingCompany == null)
            {
                return NotFound();
            }

            if (logoFile != null)
            {
                model.Logo = await _fileUploadService.UpdateFileAsync(existingCompany.ImageUrl, logoFile, "deliverycompanies");
            }

            if (infoFile != null)
            {
                model.InformationUrl = await _fileUploadService.UpdateFileAsync(existingCompany.InformationUrl, infoFile, "deliverycompanies");
            }

            existingCompany.Name = model.Name;
            existingCompany.Address = model.Address;
            existingCompany.TaxRegistrationNumber = model.TaxRegistrationNumber;
            existingCompany.PhoneNumber = model.PhoneNumber;
            existingCompany.IdNumber = model.IdNumber;
            existingCompany.Website = model.Website;
            existingCompany.specialty = model.Specialty;
            existingCompany.Notes = model.Notes;
            existingCompany.Country = model.Country;
            existingCompany.City = model.City;
            existingCompany.DisplayName = model.DisplayName;
            existingCompany.ImageUrl = model.Logo;
            existingCompany.InformationUrl = model.InformationUrl;

            var user = await _userManager.FindByIdAsync(existingCompany.UserId);

            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                if (newPassword != ConfirmNewPassword)
                {
                    ModelState.AddModelError("PasswordMismatch", "New password and confirmation password do not match.");
                    return View(model);
                }

                if (user != null)
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var passwordChangeResult = await _userManager.ResetPasswordAsync(user, token, newPassword);
                    if (!passwordChangeResult.Succeeded)
                    {
                        foreach (var error in passwordChangeResult.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }
                        return View(model);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(newEmail) && user.Email != newEmail)
            {
                user.Email = newEmail;
                user.UserName = newEmail;
                var emailChangeResult = await _userManager.UpdateAsync(user);
                if (!emailChangeResult.Succeeded)
                {
                    foreach (var error in emailChangeResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    return View(model);
                }
            }

            _context.Update(existingCompany);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Admin,FollowUpDepartment,ExecutiveDirector")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var deliveryCompany = await _context.DeliveryCompanies
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (deliveryCompany == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            if (!isAdmin)
            {
                var isDeliveryCompanyRole = await _userManager.IsInRoleAsync(user, "DeliveryCompany");
                if (!isDeliveryCompanyRole || user.AcessId != deliveryCompany.Id)
                {
                    return Unauthorized();
                }
            }

            var orders = await _context.Orders
                .Where(o => o.DeliveryCompanyId == id)
                .ToListAsync();

            var totalOrders = orders.Count();
            double scale = 100.0;

            var viewModel = new DeliveryCompanyViewModel
            {
                Id = deliveryCompany.Id,
                Name = deliveryCompany.Name,
                Address = deliveryCompany.Address,
                TaxRegistrationNumber = deliveryCompany.TaxRegistrationNumber,
                PhoneNumber = deliveryCompany.PhoneNumber,
                IdNumber = deliveryCompany.IdNumber,
                Website = deliveryCompany.Website,
                Specialty = deliveryCompany.specialty,
                Notes = deliveryCompany.Notes,
                Email = deliveryCompany.User.Email,
                Country = deliveryCompany.Country,
                City = deliveryCompany.City,
                IsActive = deliveryCompany.User.EmailConfirmed,
                IsShown = deliveryCompany.IsShown,
                WaitingForPreparationCount = totalOrders > 0 ? orders.Count(o => o.OrderStatus == OrderStatusEnum.طلب_جديد) * scale / totalOrders : 0,
                PreparedCount = totalOrders > 0 ? orders.Count(o => o.OrderStatus == OrderStatusEnum.تم_التجهيز) * scale / totalOrders : 0,
                InDeliveryCount = totalOrders > 0 ? orders.Count(o => o.OrderStatus == OrderStatusEnum.قيد_التوصيل) * scale / totalOrders : 0,
                DeliveredCount = totalOrders > 0 ? orders.Count(o => o.OrderStatus == OrderStatusEnum.تم_التسليم) * scale / totalOrders : 0,
                DeliveryFailedCount = totalOrders > 0 ? orders.Count(o => o.OrderStatus == OrderStatusEnum.فشل_التسليم) * scale / totalOrders : 0,
                WaitingForProcessingCount = totalOrders > 0 ? orders.Count(o => o.OrderStatus == OrderStatusEnum.انتظار_المعالجة) * scale / totalOrders : 0,
                ReturnedOrdersCount = totalOrders > 0 ? orders.Count(o => o.OrderStatus == OrderStatusEnum.الطلبات_المرجعة) * scale / totalOrders : 0,
                PaidCount = totalOrders > 0 ? orders.Count(o => o.OrderStatus == OrderStatusEnum.تم_الدفع) * scale / totalOrders : 0
            };

            return View(viewModel);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,FollowUpDepartment,ExecutiveDirector")]
        public async Task<IActionResult> SetIsActive(int Id, bool isActive)
        {
            var deliveryCompany = await _context.DeliveryCompanies
                .Include(a => a.User)
                .FirstOrDefaultAsync(e => e.Id == Id);
            if (deliveryCompany == null)
            {
                return NotFound();
            }

            deliveryCompany.User.EmailConfirmed = isActive;
            deliveryCompany.IsActive = isActive;
            _context.Update(deliveryCompany);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,FollowUpDepartment,ExecutiveDirector")]
        public async Task<IActionResult> SetIsShown(int Id, bool isShown)
        {
            var deliveryCompany = await _context.DeliveryCompanies.FindAsync(Id);
            if (deliveryCompany == null)
            {
                return NotFound();
            }

            deliveryCompany.IsShown = isShown;
            _context.Update(deliveryCompany);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }


        [HttpPost]
        public IActionResult HideNewOrders(int Id, bool hideOrders)
        {
            try
            {
                // Fetch the delivery company by Id
                var deliveryCompany = _context.DeliveryCompanies.FirstOrDefault(dc => dc.Id == Id);
                if (deliveryCompany == null)
                {
                    return Json(new { success = false, message = "Delivery company not found." });
                }

                // Fetch all orders related to the delivery company with status "طلب جديد"
                var newOrders = _context.Orders
                    .Where(o => o.DeliveryCompanyId == Id && o.OrderStatus == OrderStatusEnum.طلب_جديد)
                    .ToList();

                // Update the status of these orders to hide/unhide them based on hideOrders value
                foreach (var order in newOrders)
                {
                    order.IsHidden = hideOrders; // Set IsHidden based on hideOrders
                    _context.Update(order);
                }

                // Update the IsAllOrdersHidden flag in the DeliveryCompany entity
                deliveryCompany.IsAllOrdersHidden = hideOrders;

                // Save changes to the database
                _context.SaveChanges();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Log error if needed
                return Json(new { success = false, message = ex.Message });
            }
        }

    }
}
