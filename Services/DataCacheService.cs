using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using lotus_blue.Models;
using lotus_blue.Data;
using Microsoft.EntityFrameworkCore;
using lotus_blue.Models.ViewModel;
using static lotus_blue.Models.Common;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using static NuGet.Packaging.PackagingConstants;

namespace lotus_blue.Services
{
    public class DataCacheService
    {
        private readonly CacheService _cacheService;
        private readonly ApplicationDbContext _context;

        public DataCacheService(CacheService cacheService, ApplicationDbContext applicationDbContext )
        {
            _cacheService = cacheService;
            _context = applicationDbContext;
        }


        // cache countries 
        public List<Common.Countries> GetCachedCountries()
        {
            var cacheKey = "countriesList";
            TimeSpan cacheDuration = TimeSpan.FromHours(12);

            return _cacheService.GetOrCreate(
                key: cacheKey,
                createItem: () => Enum.GetValues(typeof(Common.Countries))
                                      .Cast<Common.Countries>()
                                      .ToList(),
                cacheDuration: cacheDuration
            );
        }
        // cache order status 
        public List<OrderStatusEnum> GetCachedOrderStatuses()
        {
            var cacheKey = "orderStatuses";
            TimeSpan cacheDuration = TimeSpan.FromHours(12);

            return _cacheService.GetOrCreate(
                key: cacheKey,
                createItem: () => Enum.GetValues(typeof(OrderStatusEnum))
                                      .Cast<OrderStatusEnum>()
                                      .ToList(),
                cacheDuration: cacheDuration
            );
        }

        // cache order status for delvierycomapyn and reprenstative 
        public List<OrderStatusesForDeliveryCompanyAndRepresentativeEnum> GetCachedOrderStatusesForDeliveryCompanyAndRepresentative()
        {
            var cacheKey = "orderStatusesForDeliveryCompanyAndRepresentative";
            TimeSpan cacheDuration = TimeSpan.FromHours(12);

            return _cacheService.GetOrCreate(
                key: cacheKey,
                createItem: () => Enum.GetValues(typeof(OrderStatusesForDeliveryCompanyAndRepresentativeEnum))
                                      .Cast<OrderStatusesForDeliveryCompanyAndRepresentativeEnum>()
                                      .ToList(),
                cacheDuration: cacheDuration
            );
        }


        public List<OrderStatusesForEmployeesEnum> GetCachedOrderStatusesForEmployees()
        {
            var cacheKey = "GetCachedOrderStatusesForEmployees";
            TimeSpan cacheDuration = TimeSpan.FromHours(12);

            return _cacheService.GetOrCreate(
                key: cacheKey,
                createItem: () => Enum.GetValues(typeof(OrderStatusesForEmployeesEnum))
                                      .Cast<OrderStatusesForEmployeesEnum>()
                                      .ToList(),
                cacheDuration: cacheDuration
            );
        }


     
        // get cahed country images 
        public Dictionary<string, string> GetCachedCountryImageUrls()
        {
            var cacheKey = "countryImageUrls";

            return _cacheService.GetOrCreate(cacheKey, () =>
            {
                var urls = Enum.GetValues(typeof(Countries))
                    .Cast<Countries>()
                    .ToDictionary(country => country.ToString(),
                                  country => Common.GetImageUrlByCountryName(country.ToString()));
                return urls;
            }, TimeSpan.FromDays(1)); // Adjust cache duration as needed
        }
        // get cahed socialmedia  images 

        public Dictionary<string, string> GetCachedSocialMediaIconUrls()
        {
            var cacheKey = "socialMediaIconUrls";

            return _cacheService.GetOrCreate(cacheKey, () =>
            {
                var urls = Enum.GetValues(typeof(OrderSourceEnum))
                    .Cast<OrderSourceEnum>()
                    .ToDictionary(orderSource => orderSource.ToString(),
                                  orderSource => Common.GetSocialMediaIconUrl(orderSource));
                return urls;
            }, TimeSpan.FromDays(1));
        }
        // get cahed orderstatus images 

        public Dictionary<string, string> GetCachedOrderStatusIconUrls()
        {
            var cacheKey = "orderStatusIconUrls";

            return _cacheService.GetOrCreate(cacheKey, () =>
            {
                var urls = Enum.GetValues(typeof(OrderStatusEnum))
                    .Cast<OrderStatusEnum>()
                    .Distinct()
                    .ToDictionary(orderStatus => orderStatus.ToString(),
                                  orderStatus => Common.GetStatusIconUrl(orderStatus));
                return urls;
            }, TimeSpan.FromDays(1));
        }

        // get cahed currecy symbols 
        public Dictionary<string, string> GetCachedCurrencySymbols()
        {
            var cacheKey = "currencySymbols";

            return _cacheService.GetOrCreate(cacheKey, () =>
            {
                var symbols = Enum.GetValues(typeof(Countries))
                    .Cast<Countries>()
                    .Distinct()
                    .ToDictionary(country => country.ToString(),
                                  country => Common.GetCurrencyByCountryName(country.ToString()));
                return symbols;
            }, TimeSpan.FromDays(1));
        }

        // get cahed country  infos 
        public List<CountryInfo> GetCachedCountryInfos()
        {
            var cacheKey = "countryInfos";

            return _cacheService.GetOrCreate(cacheKey, () =>
            {
                var infos = Enum.GetValues(typeof(Countries))
                    .Cast<Countries>()
                    .Select(country => new CountryInfo
                    {
                        CountryName = country.ToString(),
                        ImageUrl = Common.GetImageUrlByCountryName(country.ToString())
                    }).ToList();
                return infos;
            }, TimeSpan.FromDays(1));
        }

  
        // cach employee to list    
        public async Task<List<Employee>> GetCachedEmployeesAsync()
        {
            var cacheKey = "employees";

            // Adjust to async lambda
            return await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {

                // Fetch employees asynchronously
                var query = await _context.Employees
                    .Where(a => a.IsShown)
                    .Include(e => e.DeliveryCompany)
                    .Include(e => e.ApplicationUser)
                    .AsNoTracking()
                    .ToListAsync();

                return query; // Return the list of employees
            }, TimeSpan.FromHours(12));
        }

        // cach mainwarehouses to list    
        public async Task<List<MainWarehouse>> GetCachedMainWarehousesAsync()
        {
            var cacheKey = "mainWarehouses"; // Changed key as we are fetching warehouses

            // Adjust to async lambda
            return await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                // Fetch main warehouse data asynchronously
                var warehouses = await _context.MainWarehouses
                    .AsNoTracking()
                    .ToListAsync();

                return warehouses; // Return the list of main warehouses
            }, TimeSpan.FromHours(12));
        }


    }
}
