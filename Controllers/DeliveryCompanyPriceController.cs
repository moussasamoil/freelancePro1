using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.Models.ViewModel;
using lotus_blue.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace lotus_blue.Controllers
{
    public class DeliveryCompanyPriceController : Controller
    {

        private readonly ApplicationDbContext _context; // Replace YourDbContext with your actual DbContext class name
        private readonly QueryFilteringService _queryFilteringService;
        public DeliveryCompanyPriceController(ApplicationDbContext context, QueryFilteringService queryFilteringService)
        {
            _context = context;
            _queryFilteringService = queryFilteringService;
        }

        [Authorize(Roles = "Admin,DeliveryCompany,FollowUpDepartment,ExecutiveDirector")]
        public IActionResult Index(
            int page = 1,
            int pageSize = 10,
            int? deliveryCompanyId = null,
            Common.Countries? countryId = null,
            string cityId = null)
        {
            var isDeliveryCompany = User.IsInRole("DeliveryCompany");
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var sessionPrefix = "DeliveryCompany_Index_";

            // Retrieve filter values from session if not provided in the request
            countryId = countryId ?? (Enum.TryParse(HttpContext.Session.GetString($"{sessionPrefix}CountryId"), out Common.Countries storedCountryId) ? storedCountryId : (Common.Countries?)null);
            cityId = cityId ?? HttpContext.Session.GetString($"{sessionPrefix}CityId");
            deliveryCompanyId = deliveryCompanyId ?? HttpContext.Session.GetInt32($"{sessionPrefix}DeliveryCompanyId");

            // Initialize the query
            IQueryable<DeliveryCompanyPrice> query = _context.DeliveryCompanyPrices.AsQueryable();

            // Apply the filter based on the user's role
            if (isDeliveryCompany)
            {
                query = query.Where(p => p.DeliveryCompany.UserId == userId);
            }

            // Apply filters using QueryFilteringService
            query = _queryFilteringService.ApplyFilters(
                query: query,
                countryId: countryId,
                cityId: cityId,
                deliveryCompanyId: deliveryCompanyId,
                sessionPrefix: sessionPrefix // Pass the session prefix here
            );

            // Save current filter values in the session for future requests
            if (countryId.HasValue)
            {
                HttpContext.Session.SetString("CountryId", countryId.ToString());
            }

            if (!string.IsNullOrEmpty(cityId))
            {
                HttpContext.Session.SetString("CityId", cityId);
            }

            if (deliveryCompanyId.HasValue)
            {
                HttpContext.Session.SetInt32("DeliveryCompanyId", deliveryCompanyId.Value);
            }

            // Retrieve the total number of prices after applying filters but before pagination
            var totalItems = query.Count();

            // Apply pagination
            var prices = query.Include(a => a.DeliveryCompany)
                              .Skip((page - 1) * pageSize)
                              .Take(pageSize)
                              .ToList();

            // Create a PaginationViewModel instance and populate it with data
            var paginationViewModel = new PaginationViewModel<DeliveryCompanyPrice>
            {
                Items = prices,
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };

            return View(paginationViewModel);
        }




        [Authorize(Roles = "Admin,FollowUpDepartment,ExecutiveDirector")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin,FollowUpDepartment,ExecutiveDirector")]
        public IActionResult Create(DeliveryCompanyPrice model, bool addForAllCities)
        {
            Console.WriteLine(addForAllCities);
            if (addForAllCities)
            {
                // Check if the selected country is in the dictionary
                if (Common.CitiesByCountry.ContainsKey(model.Country))
                {
                    // Get the list of cities for the selected country
                    var cities = Common.CitiesByCountry[model.Country];

                    foreach (var city in cities)
                    {
                        // Create a new DeliveryCompanyPrice for each city
                        var newModel = new DeliveryCompanyPrice
                        {
                            Price = model.Price,
                            City = city,
                            DeliveryCompanyId = model.DeliveryCompanyId,
                            Country = model.Country,
                        };
                        _context.DeliveryCompanyPrices.Add(newModel);
                    }
                    _context.SaveChanges(); // Save all at once after adding all cities
                }
            }
            else
            {
                _context.DeliveryCompanyPrices.Add(model);
                _context.SaveChanges();
            }

                return RedirectToAction("Index"); // Redirect to the index action (or wherever you want)
            

        }


        [HttpGet]
        [Authorize(Roles = "Admin,FollowUpDepartment,ExecutiveDirector")]
        public async Task<IActionResult> Edit(int id)
        {
            var deliveryCompanyPrice = await _context.DeliveryCompanyPrices
                .Include(dc => dc.DeliveryCompany)
                .FirstOrDefaultAsync(dc => dc.Id == id);

            if (deliveryCompanyPrice == null)
            {
                return NotFound();
            }

            return View(deliveryCompanyPrice);
        }



        [HttpPost]
        [Authorize(Roles = "Admin,FollowUpDepartment,ExecutiveDirector")]
        public async Task<IActionResult> Edit(int id, DeliveryCompanyPrice deliveryCompanyPrice)
        {
            if (id != deliveryCompanyPrice.Id)
            {
                return NotFound();
            }

          

            _context.Update(deliveryCompanyPrice);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Roles = "Admin,FollowUpDepartment,ExecutiveDirector")]
        public JsonResult GetAvailableCities(int deliveryCompanyId, string country)
        {
            // Try to parse the country string to the Common.Countries enum
            if (!Enum.TryParse<Common.Countries>(country, out var countryEnum))
            {
                // If parsing fails, return an empty list or handle the error as needed
                return Json(new List<string>());
            }

            // Get all cities for the selected country
            if (!Common.CitiesByCountry.ContainsKey(countryEnum))
            {
                return Json(new List<string>());
            }

            var allCities = Common.CitiesByCountry[countryEnum];

            // Get cities that already have a price
            var citiesWithPrices = _context.DeliveryCompanyPrices
                .Where(dcp => dcp.Country == countryEnum && dcp.DeliveryCompanyId == deliveryCompanyId)
                .Select(dcp => dcp.City)
                .ToList();

            // Filter out cities that already have a price
            var availableCities = allCities.Except(citiesWithPrices).ToList();

            return Json(availableCities);
        }



    }
}
