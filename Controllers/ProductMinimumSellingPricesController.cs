using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace lotus_blue.Controllers
{
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public class ProductMinimumSellingPricesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductMinimumSellingPricesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var items = await _context.ProductMinimumSellingPrices
                .AsNoTracking()
                .Include(x => x.ManufacturingCompany)
                .Include(x => x.MainWarehouse)
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadDropdownsAsync();

            return View(new ProductMinimumSellingPriceViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductMinimumSellingPriceViewModel model)
        {
            ValidateSingleSelectModel(model);

            if (!ModelState.IsValid)
            {
                await LoadDropdownsAsync();
                return View(model);
            }

            var country = model.Country!.Value;
            var manufacturingCompanyId = model.ManufacturingCompanyId!.Value;
            var mainWarehouseId = model.MainWarehouseId!.Value;
            var minimumSellingPrice = model.MinimumSellingPrice!.Value;

            var duplicateExists = await _context.ProductMinimumSellingPrices.AnyAsync(x =>
                x.Country == country &&
                x.ManufacturingCompanyId == manufacturingCompanyId &&
                x.MainWarehouseId == mainWarehouseId);

            if (duplicateExists)
            {
                ModelState.AddModelError(string.Empty, "يوجد حد أدنى للبيع لنفس البلد والمتجر والمنتج من قبل.");
                await LoadDropdownsAsync();
                return View(model);
            }

            var entity = new ProductMinimumSellingPrice
            {
                Country = country,
                ManufacturingCompanyId = manufacturingCompanyId,
                MainWarehouseId = mainWarehouseId,
                MinimumSellingPrice = minimumSellingPrice,
                CreatedAt = DateTime.Now
            };

            _context.ProductMinimumSellingPrices.Add(entity);
            await _context.SaveChangesAsync();

            TempData["success"] = "تم إضافة الحد الأدنى للبيع بنجاح.";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var entity = await _context.ProductMinimumSellingPrices
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity == null)
            {
                return NotFound();
            }

            var model = new ProductMinimumSellingPriceViewModel
            {
                Id = entity.Id,
                Country = entity.Country,
                ManufacturingCompanyId = entity.ManufacturingCompanyId,
                MainWarehouseId = entity.MainWarehouseId,
                MinimumSellingPrice = entity.MinimumSellingPrice
            };

            await LoadDropdownsAsync();

            return View("Create", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProductMinimumSellingPriceViewModel model)
        {
            ValidateSingleSelectModel(model);

            if (!ModelState.IsValid)
            {
                await LoadDropdownsAsync();
                return View("Create", model);
            }

            var entity = await _context.ProductMinimumSellingPrices
                .FirstOrDefaultAsync(x => x.Id == model.Id);

            if (entity == null)
            {
                return NotFound();
            }

            var country = model.Country!.Value;
            var manufacturingCompanyId = model.ManufacturingCompanyId!.Value;
            var mainWarehouseId = model.MainWarehouseId!.Value;
            var minimumSellingPrice = model.MinimumSellingPrice!.Value;

            var duplicateExists = await _context.ProductMinimumSellingPrices.AnyAsync(x =>
                x.Id != model.Id &&
                x.Country == country &&
                x.ManufacturingCompanyId == manufacturingCompanyId &&
                x.MainWarehouseId == mainWarehouseId);

            if (duplicateExists)
            {
                ModelState.AddModelError(string.Empty, "يوجد حد أدنى للبيع لنفس البلد والمتجر والمنتج من قبل.");
                await LoadDropdownsAsync();
                return View("Create", model);
            }

            entity.Country = country;
            entity.ManufacturingCompanyId = manufacturingCompanyId;
            entity.MainWarehouseId = mainWarehouseId;
            entity.MinimumSellingPrice = minimumSellingPrice;
            entity.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["success"] = "تم تعديل الحد الأدنى للبيع بنجاح.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _context.ProductMinimumSellingPrices
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity == null)
            {
                return NotFound();
            }

            _context.ProductMinimumSellingPrices.Remove(entity);
            await _context.SaveChangesAsync();

            TempData["success"] = "تم حذف الحد الأدنى للبيع بنجاح.";

            return RedirectToAction(nameof(Index));
        }

        private void ValidateSingleSelectModel(ProductMinimumSellingPriceViewModel model)
        {
            if (!model.Country.HasValue)
            {
                ModelState.AddModelError(nameof(model.Country), "يرجى اختيار البلد.");
            }

            if (!model.ManufacturingCompanyId.HasValue || model.ManufacturingCompanyId.Value <= 0)
            {
                ModelState.AddModelError(nameof(model.ManufacturingCompanyId), "يرجى اختيار المتجر.");
            }

            if (!model.MainWarehouseId.HasValue || model.MainWarehouseId.Value <= 0)
            {
                ModelState.AddModelError(nameof(model.MainWarehouseId), "يرجى اختيار المنتج الرئيسي.");
            }

            if (!model.MinimumSellingPrice.HasValue || model.MinimumSellingPrice.Value <= 0)
            {
                ModelState.AddModelError(nameof(model.MinimumSellingPrice), "الحد الأدنى للبيع يجب أن يكون أكبر من صفر.");
            }
        }

        private async Task LoadDropdownsAsync()
        {
            ViewBag.Stores = await _context.ManufacturingCompanies
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync();

            ViewBag.MainProducts = await _context.MainWarehouses
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync();
        }
    }
}
