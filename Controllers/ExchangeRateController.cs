using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.Models.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace lotus_blue.Controllers
{
    public class ExchangeRateController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ExchangeRateController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: ExchangeRates
        [Authorize(Roles = "Admin,Accountant,Observer,DeliveryCompany,ExecutiveDirector,DeliveryRepresentative")]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            var query = _context.ExchangeRates.AsQueryable(); // Start with IQueryable for dynamic filtering

            // Project to view model
            var exchangeRates = await query
                .Select(rate => new ExchangeRateViewModel
                {
                    Id = rate.Id,
                    Country = rate.Country,
                    Currency = Common.GetCurrencyByCountryName(rate.Country.ToString()),
                    BuyToUSD = rate.BuyToUSD,
                    SellToUSD = rate.SellToUSD,
                    CountryFlagUrl = Common.GetImageUrlByCountryName(rate.Country.ToString())
                })
                .ToListAsync();

            var totalItems = exchangeRates.Count;

            // Apply pagination
            var paginatedExchangeRates = exchangeRates
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var viewModel = new PaginationViewModel<ExchangeRateViewModel>
            {
                Items = paginatedExchangeRates,
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };

            return View(viewModel);
        }

        [Authorize(Roles = "Admin,Accountant")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(ExchangeRateViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                var exchangeRate = new ExchangeRate
                {
                    Country = viewModel.Country,
                    BuyToUSD = viewModel.BuyToUSD,
                    SellToUSD = viewModel.SellToUSD
                };

                _context.Add(exchangeRate);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(viewModel);
        }


        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var exchangeRate = await _context.ExchangeRates.FindAsync(id);
            if (exchangeRate == null)
            {
                return NotFound();
            }

            var viewModel = new ExchangeRateViewModel
            {
                Id = exchangeRate.Id,
                Country = exchangeRate.Country,
                Currency = Common.GetCurrencyByCountryName(exchangeRate.Country.ToString()),
                BuyToUSD = exchangeRate.BuyToUSD,
                SellToUSD = exchangeRate.SellToUSD,
                CountryFlagUrl = Common.GetImageUrlByCountryName(exchangeRate.Country.ToString())
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, ExchangeRateViewModel viewModel)
        {
            if (id != viewModel.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var exchangeRate = await _context.ExchangeRates.FindAsync(id);
                if (exchangeRate == null)
                {
                    return NotFound();
                }

                exchangeRate.Country = viewModel.Country;
                exchangeRate.BuyToUSD = viewModel.BuyToUSD;
                exchangeRate.SellToUSD = viewModel.SellToUSD;

                _context.Update(exchangeRate);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            else
            {
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                  
                    Console.WriteLine(error.ErrorMessage);
                }
            }
            return View(viewModel);
        }
    }
}
