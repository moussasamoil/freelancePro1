using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.Models.ViewModel;

namespace lotus_blue.Controllers
{
    [Authorize(Roles = "Admin,FollowUpDepartment,ExecutiveDirector")]
    public class CitiesWithoutDeliveryPricesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CitiesWithoutDeliveryPricesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(Common.Countries? country, int? deliveryCompanyId)
        {
            var allActiveCompanies = await _context.DeliveryCompanies
                .AsNoTracking()
                .Where(company => company.IsShown && !company.IsRepresentative)
                .OrderBy(company => company.Country)
                .ThenBy(company => company.Name)
                .ToListAsync();

            var companiesQuery = allActiveCompanies.AsEnumerable();

            if (country.HasValue)
            {
                companiesQuery = companiesQuery.Where(company => company.Country == country.Value);
            }

            if (deliveryCompanyId.HasValue && deliveryCompanyId.Value > 0)
            {
                companiesQuery = companiesQuery.Where(company => company.Id == deliveryCompanyId.Value);
            }

            var companies = companiesQuery.ToList();
            var companyIds = companies.Select(company => company.Id).ToList();

            var existingPrices = await _context.DeliveryCompanyPrices
                .AsNoTracking()
                .Where(price => companyIds.Contains(price.DeliveryCompanyId))
                .Select(price => new
                {
                    price.DeliveryCompanyId,
                    price.Country,
                    price.City
                })
                .ToListAsync();

            var existingPriceKeys = existingPrices
                .Select(price => BuildPriceKey(price.DeliveryCompanyId, price.Country.ToString(), price.City))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var rows = new List<CityWithoutDeliveryPriceRowViewModel>();

            foreach (var company in companies)
            {
                var cities = Common.CitiesByCountry.ContainsKey(company.Country)
                    ? Common.CitiesByCountry[company.Country]
                    : new List<string>();

                foreach (var city in cities.Where(city => !string.IsNullOrWhiteSpace(city)).Distinct())
                {
                    var key = BuildPriceKey(company.Id, company.Country.ToString(), city);

                    if (existingPriceKeys.Contains(key))
                    {
                        continue;
                    }

                    rows.Add(new CityWithoutDeliveryPriceRowViewModel
                    {
                        DeliveryCompanyId = company.Id,
                        DeliveryCompanyName = company.Name ?? string.Empty,
                        DeliveryCompanyLogo = company.ImageUrl,
                        CountryName = company.Country.ToString(),
                        CountryImageUrl = Common.GetImageUrlByCountryName(company.Country.ToString()),
                        CityName = city
                    });
                }
            }

            var model = new CitiesWithoutDeliveryPricesViewModel
            {
                SelectedCountry = country?.ToString(),
                SelectedDeliveryCompanyId = deliveryCompanyId,
                Rows = rows
                    .OrderBy(row => row.CountryName)
                    .ThenBy(row => row.DeliveryCompanyName)
                    .ThenBy(row => row.CityName)
                    .ToList(),
                CountryList = Enum.GetValues(typeof(Common.Countries))
                    .Cast<Common.Countries>()
                    .Select(countryValue => new SelectListItem
                    {
                        Value = countryValue.ToString(),
                        Text = countryValue.ToString(),
                        Selected = country.HasValue && country.Value == countryValue
                    })
                    .ToList(),
                DeliveryCompanyList = allActiveCompanies
                    .Where(company => !country.HasValue || company.Country == country.Value)
                    .Select(company => new SelectListItem
                    {
                        Value = company.Id.ToString(),
                        Text = company.Name,
                        Selected = deliveryCompanyId.HasValue && deliveryCompanyId.Value == company.Id
                    })
                    .ToList()
            };

            return View(model);
        }

        private static string BuildPriceKey(int deliveryCompanyId, string country, string? city)
        {
            return $"{deliveryCompanyId}|{Normalize(country)}|{Normalize(city)}";
        }

        private static string Normalize(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
