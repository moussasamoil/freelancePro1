using lotus_blue.Data;
using lotus_blue.Models.ViewModel;
using lotus_blue.Models;
using lotus_blue.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace lotus_blue.Controllers
{
    public class MainWareHouseController : Controller
    {

        private readonly ApplicationDbContext _context;  // replace 'YourDbContext' with your actual DbContext class name
        private readonly FileUploadService _fileUploadService;  // Assuming this is your upload service

        public MainWareHouseController(ApplicationDbContext context, FileUploadService fileUploadService)
        {
            _context = context;
            _fileUploadService = fileUploadService;
        }

        // ware hosue for delviery company 
        [HttpGet]
        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public ActionResult Index(int page = 1, int? pageSize = null, int? mainwarehouseId = null)
        {
            IQueryable<MainWarehouse> query = _context.MainWarehouses;

            if (mainwarehouseId.HasValue)
            {
                query = query.Where(x => x.Id == mainwarehouseId.Value);
            }

            int totalItems = query.Count();

            // Set a default pageSize if it's not provided
            int effectivePageSize = pageSize ?? 10; // Default pageSize to 10 if null

            // Calculate the number of items to skip
            int skip = (page - 1) * effectivePageSize;

            // Retrieve the paginated subset of items and map to view models
            var mainwarehouseViewModels = query
                                          .OrderByDescending(w => w.Id)
                                          .Skip(skip)
                                          .Take(effectivePageSize)
                                          .Select(w => new MainWarehouseViewModel
                                          {
                                              Id = w.Id,
                                              Name = w.Name,
                                              ImageUrl = w.ImageUrl
                                              // Add other properties here as needed
                                          })
                                          .ToList();

            // Assuming PaginationViewModel is set up to handle generic types
            var viewModel = new PaginationViewModel<MainWarehouseViewModel>
            {
                Items = mainwarehouseViewModels, // Now passing List<MainWarehouseViewModel>
                CurrentPage = page,
                PageSize = effectivePageSize,
                TotalItems = totalItems
            };

            return View(viewModel);
        }

        [HttpGet]

        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public IActionResult Create()
        {
   
            return View();
        }


        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public async Task<IActionResult> Create(MainWarehouseViewModel viewModel, IFormFile prodtuctimage)
        {
            if (ModelState.IsValid)
            {
                if (prodtuctimage != null)
                {
                    viewModel.ImageUrl = await _fileUploadService.UploadFileAsync(prodtuctimage, "MainWarehouseImages");
                }

                var warehouse = new MainWarehouse
                {
                    Name = viewModel.Name,
                    ImageUrl = viewModel.ImageUrl  // Set the ImageUrl property here
                };

                _context.Add(warehouse);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(viewModel); // If ModelState is not valid, return the view with validation errors
        }


        [HttpGet]
        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public async Task<IActionResult> Edit(int id)
        {
            var warehouse = await _context.MainWarehouses
                .FirstOrDefaultAsync(w => w.Id == id);

            if (warehouse == null)
            {
                return NotFound();
            }

            var viewModel = new MainWarehouseViewModel
            {
                Id = warehouse.Id,
                Name = warehouse.Name,
                ImageUrl = warehouse.ImageUrl, // Corrected property name
            };

            return View(viewModel); // Correctly passing viewModel instead of warehouse
        }

        [HttpPost]
        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public async Task<IActionResult> Edit(int id, MainWarehouseViewModel viewModel, IFormFile productImage)
        {
            if (id != viewModel.Id)
            {
                return NotFound();
            }

            var existingWarehouse = await _context.MainWarehouses.FindAsync(id);
            if (existingWarehouse == null)
            {
                return NotFound();
            }

            // Check if a new image file is provided
            if (productImage != null)
            {
                // Update the file if a new one is uploaded and delete the old file
                existingWarehouse.ImageUrl = await _fileUploadService.UpdateFileAsync(existingWarehouse.ImageUrl, productImage, "MainWarehouseImages");
            }

            // Mapping all properties
            existingWarehouse.Name = viewModel.Name;

            _context.Update(existingWarehouse);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }




    }

}
