using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.Models.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using static lotus_blue.Models.Common;

namespace lotus_blue.Controllers
{
    [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
    public class SalesIndicatorsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SalesIndicatorsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? editId = null)
        {
            var viewModel = await BuildPageViewModelAsync(editId);
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(SalesIndicatorViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.CountryOptions = GetCountryOptions(model.Country);
                model.MainWarehouseList = await GetMainWarehouseSelectListAsync(model.MainWarehouseId);
                model.MainWarehouseOptions = await GetMainWarehouseProductOptionsAsync();
                model.Rows = await GetRowsAsync();
                return View("Index", model);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var now = DateTime.Now;

            var duplicateExists = await _context.SalesIndicators
                .AnyAsync(x =>
                    x.Country == model.Country.Value &&
                    x.MainWarehouseId == model.MainWarehouseId &&
                    x.Id != model.Id);

            if (duplicateExists)
            {
                ModelState.AddModelError(nameof(model.MainWarehouseId), "هذا المنتج الرئيسي له مؤشرات بيع مسجلة بالفعل في نفس الدولة. استخدمي تعديل بدل إضافة جديد.");
                model.CountryOptions = GetCountryOptions(model.Country);
                model.MainWarehouseList = await GetMainWarehouseSelectListAsync(model.MainWarehouseId);
                model.MainWarehouseOptions = await GetMainWarehouseProductOptionsAsync();
                model.Rows = await GetRowsAsync();
                return View("Index", model);
            }

            SalesIndicator entity;

            if (model.Id > 0)
            {
                entity = await _context.SalesIndicators.FirstOrDefaultAsync(x => x.Id == model.Id);

                if (entity == null)
                {
                    TempData["ErrorMessage"] = "لم يتم العثور على مؤشر البيع المطلوب تعديله";
                    return RedirectToAction(nameof(Index));
                }

                entity.UpdatedAt = now;
                entity.UpdatedByUserId = userId;
            }
            else
            {
                entity = new SalesIndicator
                {
                    CreatedAt = now,
                    CreatedByUserId = userId
                };

                _context.SalesIndicators.Add(entity);
            }

            entity.Country = model.Country.Value;
            entity.MainWarehouseId = model.MainWarehouseId;
            entity.MinimumSellingFrom = model.MinimumSellingFrom;
            entity.MinimumSellingTo = model.MinimumSellingTo;
            entity.BasicSellingFrom = model.BasicSellingFrom;
            entity.BasicSellingTo = model.BasicSellingTo;
            entity.MiddleSellingFrom = model.MiddleSellingFrom;
            entity.MiddleSellingTo = model.MiddleSellingTo;

            await _context.SaveChangesAsync();

            TempData["SuccessTitle"] = model.Id > 0 ? "تم التعديل بنجاح" : "تم الحفظ بنجاح";
            TempData["SuccessMessage"] = model.Id > 0
                ? "تم تعديل مؤشرات البيع بنجاح"
                : "تم حفظ مؤشرات البيع بنجاح";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _context.SalesIndicators.FirstOrDefaultAsync(x => x.Id == id);

            if (entity == null)
            {
                TempData["ErrorMessage"] = "لم يتم العثور على مؤشر البيع المطلوب حذفه";
                return RedirectToAction(nameof(Index));
            }

            _context.SalesIndicators.Remove(entity);
            await _context.SaveChangesAsync();

            TempData["SuccessTitle"] = "تم الحذف بنجاح";
            TempData["SuccessMessage"] = "تم حذف مؤشر البيع بنجاح";
            return RedirectToAction(nameof(Index));
        }

        private async Task<SalesIndicatorViewModel> BuildPageViewModelAsync(int? editId)
        {
            var model = new SalesIndicatorViewModel();

            if (editId.HasValue && editId.Value > 0)
            {
                var entity = await _context.SalesIndicators
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == editId.Value);

                if (entity != null)
                {
                    model.Id = entity.Id;
                    model.Country = entity.Country;
                    model.MainWarehouseId = entity.MainWarehouseId;
                    model.MinimumSellingFrom = entity.MinimumSellingFrom;
                    model.MinimumSellingTo = entity.MinimumSellingTo;
                    model.BasicSellingFrom = entity.BasicSellingFrom;
                    model.BasicSellingTo = entity.BasicSellingTo;
                    model.MiddleSellingFrom = entity.MiddleSellingFrom;
                    model.MiddleSellingTo = entity.MiddleSellingTo;
                }
            }

            model.CountryOptions = GetCountryOptions(model.Country);
            model.MainWarehouseList = await GetMainWarehouseSelectListAsync(model.MainWarehouseId);
            model.MainWarehouseOptions = await GetMainWarehouseProductOptionsAsync();
            model.Rows = await GetRowsAsync();

            return model;
        }

        private List<SalesIndicatorCountryOptionViewModel> GetCountryOptions(Countries? selectedCountry = null)
        {
            return Enum.GetValues(typeof(Countries))
                .Cast<Countries>()
                .OrderBy(x => x.ToString())
                .Select(x => new SalesIndicatorCountryOptionViewModel
                {
                    Id = (int)x,
                    Name = x.ToString(),
                    ImageUrl = string.IsNullOrWhiteSpace(Common.GetImageUrlByCountryName(x.ToString()))
                        ? "static/earth-americas-sharp-solid.svg"
                        : Common.GetImageUrlByCountryName(x.ToString()),
                    Selected = selectedCountry.HasValue && selectedCountry.Value == x
                })
                .ToList();
        }

        private async Task<List<SelectListItem>> GetMainWarehouseSelectListAsync(int selectedId = 0)
        {
            var items = await _context.Set<MainWarehouse>()
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = x.Name,
                    Selected = selectedId > 0 && x.Id == selectedId
                })
                .ToListAsync();

            items.Insert(0, new SelectListItem
            {
                Value = "",
                Text = "اختاري المنتج الرئيسي"
            });

            return items;
        }

        private async Task<List<SalesIndicatorProductOptionViewModel>> GetMainWarehouseProductOptionsAsync()
        {
            return await _context.Set<MainWarehouse>()
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new SalesIndicatorProductOptionViewModel
                {
                    Id = x.Id,
                    Name = x.Name,
                    ImageUrl = string.IsNullOrWhiteSpace(x.ImageUrl)
                        ? "static/DefaultImage.svg"
                        : x.ImageUrl
                })
                .ToListAsync();
        }

        private async Task<List<SalesIndicatorRowViewModel>> GetRowsAsync()
        {
            return await _context.SalesIndicators
                .AsNoTracking()
                .Include(x => x.MainWarehouse)
                .OrderBy(x => x.Country)
                .ThenBy(x => x.MainWarehouse == null ? "" : x.MainWarehouse.Name)
                .Select(x => new SalesIndicatorRowViewModel
                {
                    Id = x.Id,
                    Country = x.Country,
                    CountryName = x.Country.ToString(),
                    CountryImageUrl = string.IsNullOrWhiteSpace(Common.GetImageUrlByCountryName(x.Country.ToString()))
                        ? "static/earth-americas-sharp-solid.svg"
                        : Common.GetImageUrlByCountryName(x.Country.ToString()),
                    MainWarehouseId = x.MainWarehouseId,
                    MainWarehouseName = x.MainWarehouse == null ? "غير معروف" : x.MainWarehouse.Name,
                    MainWarehouseImageUrl = x.MainWarehouse == null || string.IsNullOrWhiteSpace(x.MainWarehouse.ImageUrl)
                        ? "static/DefaultImage.svg"
                        : x.MainWarehouse.ImageUrl,
                    MinimumSellingFrom = x.MinimumSellingFrom,
                    MinimumSellingTo = x.MinimumSellingTo,
                    BasicSellingFrom = x.BasicSellingFrom,
                    BasicSellingTo = x.BasicSellingTo,
                    MiddleSellingFrom = x.MiddleSellingFrom,
                    MiddleSellingTo = x.MiddleSellingTo
                })
                .ToListAsync();
        }
    }
}