using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.Models.AppViewModel;
using lotus_blue.Models.ViewModel;
using lotus_blue.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http.Filters;
using Microsoft.AspNetCore.Authorization;

namespace lotus_blue.Controllers
{
    public class ProductsPricesController : Controller
    {
        private readonly FileUploadService _fileUploadService;
        private readonly ApplicationDbContext _context;
        private readonly QueryFilteringService _queryFilteringService;

        public ProductsPricesController(FileUploadService fileUploadService, ApplicationDbContext context, QueryFilteringService queryFilteringService)
        {
            _fileUploadService = fileUploadService;
            _context = context;
            _queryFilteringService = queryFilteringService;
        }

        [Authorize]
        public IActionResult Index(Common.Countries? country, int? manufacturingCompanyId, int page = 1, int pageSize = 10)
        {
            var sessionPrefix = "ProductPrices_Index_";

            // Retrieve filter values from session if they are not provided by the request
            country = country ?? (Enum.TryParse(HttpContext.Session.GetString($"{sessionPrefix}CountryId"), out Common.Countries countryValue) ? countryValue : (Common.Countries?)null);
            manufacturingCompanyId = manufacturingCompanyId ?? HttpContext.Session.GetInt32($"{sessionPrefix}ManufacturingCompanyId");

            // Use _queryFilteringService to apply the filters and save them to the session
            var query = _context.MainProducts.Include(p => p.ManufacturingCompany).AsQueryable();

            // Call the ApplyFilters method to handle the filtering and session saving
            query = _queryFilteringService.ApplyFilters(
                query: query,
                countryId: country,
                storeId: manufacturingCompanyId,
                sessionPrefix: sessionPrefix // Pass the session prefix here
            );

            var totalItems = query.Count();

            var products = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductsPricesViewModel
                {
                    Id = p.Id,
                    Country = p.Country,
                    ProductName = p.Name,
                    ProductImage = p.ImageUrl,
                    ProductPrice = p.Price,
                    ManufacturingCompanyName = p.ManufacturingCompany.Name,
                    SelectedManufacturingCompanyId = p.ManufacturingCompanyId
                })
                .ToList();

            var viewModel = new PaginationViewModel<ProductsPricesViewModel>
            {
                Items = products,
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };

            return View(viewModel);
        }



        [Authorize]
        public IActionResult Create()
        {
            var viewModel = new ProductsPricesViewModel();
            return View(viewModel);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create(ProductsPricesViewModel viewModel, IFormFile? productImage)
        {
            if (ModelState.IsValid)
            {
                viewModel.ProductImage = await _fileUploadService.UploadFileAsync(productImage, "Products");

                var product = new MainProduct
                {
                    Name = viewModel.ProductName,
                    ImageUrl = viewModel.ProductImage,
                    Price = viewModel.ProductPrice,
                    Country = viewModel.Country,
                    ManufacturingCompanyId = viewModel.SelectedManufacturingCompanyId,
                };

                _context.MainProducts.Add(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(viewModel);
        }
     
        [Authorize]
  
        public IActionResult Edit(int id)
        {
            var existingProduct = _context.MainProducts
                          .Include(p => p.ManufacturingCompany)
                          .FirstOrDefault(p => p.Id == id);
            if (existingProduct == null)
            {
                return NotFound();
            }

            var viewModel = new ProductsPricesViewModel
            {
                Id = existingProduct.Id,
                ProductName = existingProduct.Name,
                ProductPrice = existingProduct.Price,
                ProductImage = existingProduct.ImageUrl,
                Country = existingProduct.Country,
                SelectedManufacturingCompanyId = existingProduct.ManufacturingCompanyId,
            };

            return View(viewModel);
        }
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
    
       
        public async Task<IActionResult> Edit(int id, ProductsPricesViewModel viewModel, IFormFile? productImage)
        {
            if (id != viewModel.Id)
            {
                return NotFound();
            }

            var existingProduct = _context.MainProducts.Find(id);
            if (existingProduct == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                existingProduct.Name = viewModel.ProductName;
                existingProduct.Price = viewModel.ProductPrice;
                existingProduct.Country = viewModel.Country;
                existingProduct.ManufacturingCompanyId = viewModel.SelectedManufacturingCompanyId;

                if (productImage != null)
                {
                    existingProduct.ImageUrl = await _fileUploadService.UpdateFileAsync(existingProduct.ImageUrl, productImage, "product-images");
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(viewModel);
        }
      
        [Authorize]
        [HttpPost, ActionName("Delete")]
        public IActionResult DeleteConfirmed(int id)
        {
            var product = _context.MainProducts.Find(id);
            if (product == null)
            {
                return NotFound();
            }

            _context.MainProducts.Remove(product);
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }
    }
}
