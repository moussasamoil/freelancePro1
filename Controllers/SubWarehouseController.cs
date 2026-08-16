using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.Models.ViewModel;
using lotus_blue.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace lotus_blue.Controllers
{
    public class SubWarehouseController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly FileUploadService _fileUploadService;

        public SubWarehouseController(ApplicationDbContext context, FileUploadService fileUploadService)
        {
            _context = context;
            _fileUploadService = fileUploadService;
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public ActionResult Index(int page = 1, int? pageSize = null, int? mainwarehouseId = null)
        {
            IQueryable<SubWarehouse> query = _context.SubWarehouses;

            if (mainwarehouseId.HasValue)
            {
                query = query.Where(x => x.MainWarehouseId == mainwarehouseId.Value);
            }


            int totalItems = query.Count();
            int effectivePageSize = pageSize ?? 10; // Default pageSize to 10 if null
            int skip = (page - 1) * effectivePageSize;

            var subWarehouseViewModels = query
                .OrderByDescending(w => w.Id)
                .Skip(skip)
                .Take(effectivePageSize)
                .Select(w => new SubWarehouseViewModel
                {
                    Id = w.Id,
                    Name = w.Name,
                    ProductCode=w.ProductCode,
                })
                .ToList();

            var viewModel = new PaginationViewModel<SubWarehouseViewModel>
            {
                Items = subWarehouseViewModels,
                CurrentPage = page,
                PageSize = effectivePageSize,
                TotalItems = totalItems
            };

            return View(viewModel);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(SubWarehouseViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            // Create and save the SubWarehouse first
            var subWarehouse = new SubWarehouse
            {
                Name = viewModel.Name,
                MainWarehouseId = viewModel.MainWarehouseId
            };

            _context.Add(subWarehouse);
            await _context.SaveChangesAsync();

            // Fetch all existing delivery companies
            var deliveryCompanies = await _context.DeliveryCompanies.ToListAsync();

            // Create a Warehouse for each DeliveryCompany
            foreach (var deliveryCompany in deliveryCompanies)
            {
                var warehouse = new Warehouse
                {
                    Name = subWarehouse.Name, // Use the SubWarehouse name directly
                    SubWarehouseId = subWarehouse.Id,
                    DeliveryCompanyId = deliveryCompany.Id,
                    MainWarehouseId = subWarehouse.MainWarehouseId ?? 0,
                    ManufacturingCompanyId = null,
                    Amount = 0, // Set the initial amount to 0
                    Countries = deliveryCompany.Country,
                    City = deliveryCompany.City,
                    DateAdded = DateTime.Now,
                    DateUpdated = DateTime.Now,
                    IsShown = true // Assuming you want the warehouse to be shown by default
                };

                _context.Warehouses.Add(warehouse);
            }

            // Save all new Warehouses to the database
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }


        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var subWarehouse = await _context.SubWarehouses.FirstOrDefaultAsync(w => w.Id == id);

            if (subWarehouse == null)
            {
                return NotFound();
            }

            var viewModel = new SubWarehouseViewModel
            {
                Id = subWarehouse.Id,
                Name = subWarehouse.Name,
                ProductCode=subWarehouse.ProductCode,
                MainWarehouseId = subWarehouse.MainWarehouseId // Ensure this is set
            };

            return View(viewModel);
        }


        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, SubWarehouseViewModel viewModel)
        {
            if (id != viewModel.Id)
            {
                return NotFound();
            }

            var existingSubWarehouse = await _context.SubWarehouses.FindAsync(id);
            if (existingSubWarehouse == null)
            {
                return NotFound();
            }

            existingSubWarehouse.Name = viewModel.Name;
            existingSubWarehouse.ProductCode = viewModel.ProductCode;
            _context.Update(existingSubWarehouse);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        //[HttpPost]
        //[Authorize(Roles = "Admin")]
        //public async Task<IActionResult> Delete(int id)
        //{
        //    var subWarehouse = await _context.SubWarehouses.FindAsync(id);
        //    if (subWarehouse == null)
        //    {
        //        return NotFound();
        //    }

        //    _context.SubWarehouses.Remove(subWarehouse);
        //    await _context.SaveChangesAsync();

        //    return RedirectToAction(nameof(Index));
        //}
    }
}
