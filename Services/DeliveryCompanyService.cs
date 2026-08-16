using System;
using System.Linq;
using System.Collections.Generic;
using lotus_blue.Data;
using lotus_blue.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using static lotus_blue.Models.Common;

namespace lotus_blue.Services
{
    public class DeliveryCompanyService
    {
        private readonly ApplicationDbContext _context;
        private readonly IDictionary<int, decimal> _deliveryCompanyPrices;
        private readonly IEnumerable<DeliveryCompany> _deliveryCompanies;
        private readonly UserManager<ApplicationUser> _userManager; // Add UserManager for Identity
        private readonly CurrencyExchangeService _currencyExchangeService;

        public DeliveryCompanyService(
             ApplicationDbContext context,
             IDictionary<int, decimal> deliveryCompanyPrices,
             IEnumerable<DeliveryCompany> deliveryCompanies,
             UserManager<ApplicationUser> userManager, // Inject UserManager
              CurrencyExchangeService currencyExchangeService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _deliveryCompanyPrices = deliveryCompanyPrices ?? throw new ArgumentNullException(nameof(deliveryCompanyPrices));
            _deliveryCompanies = deliveryCompanies ?? throw new ArgumentNullException(nameof(deliveryCompanies));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _currencyExchangeService = currencyExchangeService ?? throw new ArgumentNullException(nameof(currencyExchangeService));
        }


        // delvieryocmpany list id prices in dollar 
        public async Task<decimal> CalculateTotalDeliveryPricesInUSDForOrdersByCountryAsync(List<int> orderIds)
        {
            // Retrieve all relevant orders
            var orders = await _context.Orders
                .Where(o => orderIds.Contains(o.Id))
                .ToListAsync();

            // Group orders by country and sum the delivery prices
            var totalsByCountry = orders
                .GroupBy(o => o.Country)
                .Select(g => new
                {
                    Country = g.Key,
                    TotalPrice = g.Sum(o => o.DeliveryPrice )
                })
                .ToList();

            decimal grandTotalInUSD = 0;
            object lockObject = new object();

            Parallel.ForEach(totalsByCountry, countryTotal =>
            {
                var countryTotalInUSD = _currencyExchangeService.ConvertToUSD(countryTotal.TotalPrice, countryTotal.Country.ToString());

                lock (lockObject)
                {
                    grandTotalInUSD += countryTotalInUSD;
                }

                Console.WriteLine($"Country: {countryTotal.Country}, Total in Local Currency: {countryTotal.TotalPrice}, Total in USD: {countryTotalInUSD}");
            });

            Console.WriteLine($"Grand Total in USD: {grandTotalInUSD}");

            return grandTotalInUSD;
        }


        public decimal CalculateTotalPriceInUSDForOrdersWithOutDeliveryCompanyPrice(List<int> orderIds)
        {
            // Load orders that are relevant to the given order IDs
            var orders = _context.Orders
                .Where(o => orderIds.Contains(o.Id))
                .ToList();

            // Calculate total price minus delivery for each order and convert to USD
            var totalPriceInUSD = orders.Sum(order =>
            {
                var orderPriceMinusDelivery = order.TotalPrice - (order.DeliveryPrice);
                return _currencyExchangeService.ConvertToUSD(orderPriceMinusDelivery, order.Country.ToString());
            });

            return totalPriceInUSD;
        }



    }
}
