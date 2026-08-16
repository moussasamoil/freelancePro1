using lotus_blue.API;
using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.Models.ViewModel;
using lotus_blue.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace lotus_blue.Controllers
{
    public class ManufacturingCompanyController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly FileUploadService _fileUploadService;
        private readonly RESTAPI _restApi;

        public ManufacturingCompanyController(ApplicationDbContext context, FileUploadService fileUploadService, RESTAPI restApi)
        {
            _context = context;
            _fileUploadService = fileUploadService;
            _restApi = restApi;
        }

        [Authorize(Roles = "Admin,Accountant,ExecutiveDirector,Observer,OrderPreparer,DeliveryCompany,DeliveryRepresentative")]
        public IActionResult Index(int page = 1, int pageSize = 10, int? storeId = null)
        {
            var query = _context.ManufacturingCompanies.AsQueryable();

            if (storeId.HasValue)
            {
                query = query.Where(a => a.Id == storeId.Value);
            }

            int totalItems = query.Count();
            int skip = (page - 1) * pageSize;

            List<ManufacturingCompanyViewModel> pagedItems = query
                .OrderBy(mc => mc.Id)
                .Skip(skip)
                .Take(pageSize)
                .Select(mc => new ManufacturingCompanyViewModel
                {
                    Id = mc.Id,
                    Name = mc.Name,
                    Logo = mc.ImageUrl,
                    IsShown = mc.IsShown,
                    InvoiceImage = mc.InvoiceImage,
                    ImageUrl2 = mc.ImageUrl2,
                    PhoneNumber = mc.PhoneNumber
                })
                .ToList();

            var viewModel = new PaginationViewModel<ManufacturingCompanyViewModel>
            {
                Items = pagedItems,
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };

            return View(viewModel);
        }

        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public IActionResult Create()
        {
            ViewBag.SelectedMainWarehouseIds = new List<int>();
            return View(new ManufacturingCompanyViewModel());
        }

        [HttpPost]
        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public async Task<IActionResult> Create(
            ManufacturingCompanyViewModel viewModel,
            List<int>? MainWarehouseIds,
            IFormFile logoFile,
            IFormFile logoFile2,
            IFormFile invoiceFile)
        {
            if (ModelState.IsValid)
            {
                var selectedMainWarehouseIds = (MainWarehouseIds ?? new List<int>())
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();

                // Backward compatibility:
                // MainWarehouseId القديم يفضل موجود ويتخزن فيه أول اختيار فقط
                // عشان أي كود قديم معتمد عليه ما يتكسرش.
                if (!selectedMainWarehouseIds.Any() && viewModel.MainWarehouseId > 0)
                {
                    selectedMainWarehouseIds.Add(viewModel.MainWarehouseId.Value);
                }

                var manufacturingCompany = new ManufacturingCompany
                {
                    Name = viewModel.Name,
                    PhoneNumber = viewModel.PhoneNumber,
                    MainWarehouseId = selectedMainWarehouseIds.Any() ? selectedMainWarehouseIds.First() : null
                };

                if (logoFile != null)
                {
                    string logoFilePath = await _fileUploadService.UploadFileAsync(logoFile, "Stores");
                    manufacturingCompany.ImageUrl = logoFilePath;
                }
                if (logoFile2 != null)
                {
                    string logoFile2Path = await _fileUploadService.UploadFileAsync(logoFile2, "Stores");
                    manufacturingCompany.ImageUrl2 = logoFile2Path;
                }
                if (invoiceFile != null)
                {
                    string invoiceImagePath = await _fileUploadService.UploadFileAsync(invoiceFile, "Stores");
                    manufacturingCompany.InvoiceImage = invoiceImagePath;
                }

                _context.Add(manufacturingCompany);
                await _context.SaveChangesAsync();

                foreach (var mainWarehouseId in selectedMainWarehouseIds)
                {
                    _context.Set<ManufacturingCompanyMainWarehouse>().Add(new ManufacturingCompanyMainWarehouse
                    {
                        ManufacturingCompanyId = manufacturingCompany.Id,
                        MainWarehouseId = mainWarehouseId
                    });
                }

                // Create EmployeeManufacturingCompany entries for each existing Employee
                var employees = await _context.Employees.ToListAsync();
                foreach (var employee in employees)
                {
                    var employeeManufacturingCompany = new EmployeeManufacturingCompany
                    {
                        EmployeeId = employee.Id,
                        ManufacturingCompanyId = manufacturingCompany.Id,
                        ApplicationUserId = employee.ApplicationUserId,
                        CanSeeManufacturingCompany = false
                    };

                    _context.Entry(employeeManufacturingCompany).State = EntityState.Added;
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, redirectUrl = Url.Action("Index") });
            }
            return Json(new { success = false, message = "البيانات المدخلة غير صالحة" });
        }


        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var manufacturingCompany = await _context.ManufacturingCompanies.FindAsync(id);

            if (manufacturingCompany == null)
            {
                return NotFound();
            }

            var selectedMainWarehouseIds = await _context.Set<ManufacturingCompanyMainWarehouse>()
                .AsNoTracking()
                .Where(x => x.ManufacturingCompanyId == id)
                .Select(x => x.MainWarehouseId)
                .ToListAsync();

            // لو متجر قديم مش متسجل في جدول الاختيارات الجديدة
            // نرجع الاختيار القديم MainWarehouseId عشان يظهر في صفحة التعديل.
            if (!selectedMainWarehouseIds.Any() && manufacturingCompany.MainWarehouseId > 0)
            {
                selectedMainWarehouseIds.Add(manufacturingCompany.MainWarehouseId.Value);
            }

            ViewBag.SelectedMainWarehouseIds = selectedMainWarehouseIds;

            var viewModel = new ManufacturingCompanyViewModel
            {
                Id = manufacturingCompany.Id,
                Name = manufacturingCompany.Name,
                Logo = manufacturingCompany.ImageUrl,
                IsShown = manufacturingCompany.IsShown,
                InvoiceImage = manufacturingCompany.InvoiceImage,
                ImageUrl2 = manufacturingCompany.ImageUrl2,
                PhoneNumber = manufacturingCompany.PhoneNumber,
                MainWarehouseId = manufacturingCompany.MainWarehouseId
            };

            return View(viewModel);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(
            int id,
            ManufacturingCompanyViewModel viewModel,
            List<int>? MainWarehouseIds,
            IFormFile? logoFile,
            IFormFile? logoFile2,
            IFormFile? invoiceFile)
        {
            if (id != viewModel.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var existingCompany = await _context.ManufacturingCompanies.FindAsync(id);
                if (existingCompany == null)
                {
                    return NotFound();
                }

                var selectedMainWarehouseIds = (MainWarehouseIds ?? new List<int>())
                    .Where(x => x > 0)
                    .Distinct()
                    .ToList();

                if (!selectedMainWarehouseIds.Any() && viewModel.MainWarehouseId > 0)
                {
                    selectedMainWarehouseIds.Add(viewModel.MainWarehouseId.Value);
                }

                if (logoFile != null)
                {
                    string logoFilePath = await _fileUploadService.UpdateFileAsync(existingCompany.ImageUrl, logoFile, "Stores");
                    existingCompany.ImageUrl = logoFilePath;
                }

                if (logoFile2 != null)
                {
                    string logoFile2Path = await _fileUploadService.UpdateFileAsync(existingCompany.ImageUrl2, logoFile2, "Stores");
                    existingCompany.ImageUrl2 = logoFile2Path;
                }

                if (invoiceFile != null)
                {
                    string invoiceFilePath = await _fileUploadService.UpdateFileAsync(existingCompany.InvoiceImage, invoiceFile, "Stores");
                    existingCompany.InvoiceImage = invoiceFilePath;
                }

                existingCompany.Name = viewModel.Name;
                existingCompany.IsShown = existingCompany.IsShown;
                existingCompany.PhoneNumber = viewModel.PhoneNumber;
                existingCompany.MainWarehouseId = selectedMainWarehouseIds.Any() ? selectedMainWarehouseIds.First() : null;

                _context.Update(existingCompany);

                var oldRows = await _context.Set<ManufacturingCompanyMainWarehouse>()
                    .Where(x => x.ManufacturingCompanyId == id)
                    .ToListAsync();

                if (oldRows.Any())
                {
                    _context.Set<ManufacturingCompanyMainWarehouse>().RemoveRange(oldRows);
                }

                foreach (var mainWarehouseId in selectedMainWarehouseIds)
                {
                    _context.Set<ManufacturingCompanyMainWarehouse>().Add(new ManufacturingCompanyMainWarehouse
                    {
                        ManufacturingCompanyId = existingCompany.Id,
                        MainWarehouseId = mainWarehouseId
                    });
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, redirectUrl = Url.Action("Index") });
            }

            return Json(new { success = false, message = "البيانات المدخلة غير صالحة" });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public async Task<IActionResult> SetIsShown(int manufacturingcompanyId, bool IsShown)
        {
            var manufacturing = await _context.ManufacturingCompanies.FindAsync(manufacturingcompanyId);
            if (manufacturing == null)
            {
                return Json(new { success = false, message = "Manufacturing company not found." });
            }

            manufacturing.IsShown = IsShown;
            _context.Update(manufacturing);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
    }
}
