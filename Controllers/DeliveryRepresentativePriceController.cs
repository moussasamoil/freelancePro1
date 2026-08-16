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
    public class DeliveryRepresentativePriceController : Controller
    {

        private readonly ApplicationDbContext _context; // Replace YourDbContext with your actual DbContext class name
        private readonly QueryFilteringService _queryFilteringService;
        public DeliveryRepresentativePriceController(ApplicationDbContext context, QueryFilteringService queryFilteringService)
        {
            _context = context;
            _queryFilteringService = queryFilteringService;
        }

        [Authorize(Roles = "Admin,DeliveryRepresentative,FollowUpDepartment,ExecutiveDirector")]
            public IActionResult Index(
         int page = 1,
         int pageSize = 10,
         int? deliveryRepresentativeId = null,
         Common.Countries? countryId = null,
         string? cityId = null)
            {
            var isDeliveryRepresentative = User.IsInRole("DeliveryRepresentative");
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var sessionPrefix = "DeliveryRepresentativeDeliveryPrice_Index_";


            // Retrieve filter values from session if not provided in the request
            countryId = countryId ?? (Enum.TryParse(HttpContext.Session.GetString($"{sessionPrefix}CountryId"), out Common.Countries storedCountryId) ? storedCountryId : (Common.Countries?)null);
            cityId = cityId ?? HttpContext.Session.GetString($"{sessionPrefix}CityId");
            deliveryRepresentativeId = deliveryRepresentativeId ?? HttpContext.Session.GetInt32($"{sessionPrefix}DeliveryRepresentativeId");

            // Initialize the query
            IQueryable<DeliveryCompanyPrice> query = _context.DeliveryCompanyPrices
                .Where(a => a.DeliveryCompany.IsRepresentative)
                .AsQueryable();

            // Apply the filter based on the user's role
            if (isDeliveryRepresentative)
            {
                query = query.Where(p => p.DeliveryCompany.UserId == userId);
            }

            // Apply filters using QueryFilteringService
            query = _queryFilteringService.ApplyFilters(
                query: query,
                countryId: countryId,
                cityId: cityId,
                deliveryRepresentativeId: deliveryRepresentativeId,
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

            if (deliveryRepresentativeId.HasValue)
            {
                HttpContext.Session.SetInt32("DeliveryRepresentativeId", deliveryRepresentativeId.Value);
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
        public JsonResult GetAvailableDeliveryRepresentatives(Common.Countries countryId, string cityId)
        {
            // Get all delivery representatives for the selected country and city
            var representativesInCity = _context.DeliveryCompanies
                                .Where(dcp => dcp.Country == countryId && dcp.City == cityId)

                .Where(dc => dc.Country == countryId && dc.IsRepresentative)
                .Select(dc => new { dc.Id, dc.Name })
                .ToList();

            // Get representatives who already have a price for the selected city
            var representativesWithPrices = _context.DeliveryCompanyPrices
                .Where(dcp => dcp.Country == countryId && dcp.City == cityId)
                .Select(dcp => dcp.DeliveryCompanyId)
                .ToList();

            // Filter out representatives who already have a price
            var availableRepresentatives = representativesInCity
                .Where(rep => !representativesWithPrices.Contains(rep.Id))
                .ToList();

            return Json(availableRepresentatives);
        }



    }
}
