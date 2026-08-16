using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace lotus_blue.Controllers
{
    public class CountryMinimumPricesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CountryMinimumPricesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: CountryMinimumPrices
        public async Task<IActionResult> Index()
        {
            var countryMinimumPrices = await _context.CountryMinimumPrices
                .Include(c => c.ManufacturingCompany)
                .ToListAsync();
            var viewModelList = countryMinimumPrices.Select(cmp => new CountryMinimumPriceViewModel
            {
                Id = cmp.Id,
                Country = cmp.Country,
                ManufacturingCompanyId = cmp.ManufacturingCompanyId,
                ManufacturingCompanyName = cmp.ManufacturingCompany?.Name,
                MinimumPriceForOffers = cmp.MinimumPriceForOffers,
                MaximumPriceForOffers = cmp.MaximumPriceForOffers
            }).ToList();
            return View(viewModelList);
        }

        // GET: CountryMinimumPrices/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Stores = await _context.ManufacturingCompanies
                .Where(m => m.IsShown)
                .OrderBy(m => m.Name)
                .ToListAsync();
            return View();
        }

        // POST: CountryMinimumPrices/Create
        [HttpPost]
        public async Task<IActionResult> Create(CountryMinimumPriceViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                var countryMinimumPrice = new CountryMinimumPrice
                {
                    Country = viewModel.Country,
                    ManufacturingCompanyId = viewModel.ManufacturingCompanyId,
                    MinimumPriceForOffers = viewModel.MinimumPriceForOffers,
                    MaximumPriceForOffers = viewModel.MaximumPriceForOffers
                };

                _context.Add(countryMinimumPrice);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Stores = await _context.ManufacturingCompanies
                .Where(m => m.IsShown)
                .OrderBy(m => m.Name)
                .ToListAsync();
            return View(viewModel);
        }

        // GET: CountryMinimumPrices/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var countryMinimumPrice = await _context.CountryMinimumPrices.FindAsync(id);
            if (countryMinimumPrice == null)
            {
                return NotFound();
            }

            var viewModel = new CountryMinimumPriceViewModel
            {
                Id = countryMinimumPrice.Id,
                Country = countryMinimumPrice.Country,
                ManufacturingCompanyId = countryMinimumPrice.ManufacturingCompanyId,
                MinimumPriceForOffers = countryMinimumPrice.MinimumPriceForOffers,
                MaximumPriceForOffers = countryMinimumPrice.MaximumPriceForOffers
            };

            ViewBag.Stores = await _context.ManufacturingCompanies
                .Where(m => m.IsShown)
                .OrderBy(m => m.Name)
                .ToListAsync();
            return View(viewModel);
        }

        // POST: CountryMinimumPrices/Edit/5
        [HttpPost]
        public async Task<IActionResult> Edit(int id, CountryMinimumPriceViewModel viewModel)
        {
            if (id != viewModel.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var countryMinimumPrice = new CountryMinimumPrice
                {
                    Id = viewModel.Id,
                    Country = viewModel.Country,
                    ManufacturingCompanyId = viewModel.ManufacturingCompanyId,
                    MinimumPriceForOffers = viewModel.MinimumPriceForOffers,
                    MaximumPriceForOffers = viewModel.MaximumPriceForOffers
                };

                _context.Update(countryMinimumPrice);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Stores = await _context.ManufacturingCompanies
                .Where(m => m.IsShown)
                .OrderBy(m => m.Name)
                .ToListAsync();
            return View(viewModel);
        }
    }
}
