using lotus_blue.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using static lotus_blue.Models.Common;

namespace lotus_blue.Services
{
    public class CurrencyExchangeService
    {
        private readonly Dictionary<Countries, (decimal BuyRate, decimal SellRate)> _exchangeRates;

        public CurrencyExchangeService(ApplicationDbContext context)
        {
            _exchangeRates = context.ExchangeRates.ToDictionary(
                rate => rate.Country,
                rate => (rate.BuyToUSD, rate.SellToUSD));
        }

        public decimal ConvertToUSD(decimal amount, string countryName)
        {
            if (string.IsNullOrWhiteSpace(countryName))
            {
                return amount;
            }

            if (Enum.TryParse(countryName.Trim(), out Countries country))
            {
                return ConvertToUSD(amount, country);
            }

            throw new ArgumentException($"Invalid country name: {countryName}");
        }

        public decimal ConvertToUSD(decimal amount, Countries country)
        {
            if (_exchangeRates.TryGetValue(country, out var rates))
            {
                if (rates.SellRate == 0)
                {
                    return 0;
                }

                // Convert local currency to USD using sell rate
                return amount / rates.SellRate;
            }

            return amount;
        }

        public decimal ConvertToUSD(decimal amount, Countries? country)
        {
            if (!country.HasValue)
            {
                // If no country is set, treat the amount as already USD
                return amount;
            }

            return ConvertToUSD(amount, country.Value);
        }

        public decimal ConvertFromUSD(decimal amount, string countryName)
        {
            if (string.IsNullOrWhiteSpace(countryName))
            {
                return amount;
            }

            if (Enum.TryParse(countryName.Trim(), out Countries country))
            {
                return ConvertFromUSD(amount, country);
            }

            throw new ArgumentException($"Invalid country name: {countryName}");
        }

        public decimal ConvertFromUSD(decimal amount, Countries country)
        {
            if (_exchangeRates.TryGetValue(country, out var rates))
            {
                // Convert USD to local currency using buy rate
                return amount * rates.BuyRate;
            }

            return amount;
        }

        public decimal ConvertFromUSD(decimal amount, Countries? country)
        {
            if (!country.HasValue)
            {
                // If no country is set, keep the amount in USD
                return amount;
            }

            return ConvertFromUSD(amount, country.Value);
        }

        public decimal ConvertToTurkishLira(decimal amount)
        {
            if (_exchangeRates.TryGetValue(Countries.تركيا, out var rates))
            {
                // Convert USD to Turkish Lira using buy rate
                return amount * rates.BuyRate;
            }

            throw new InvalidOperationException("Exchange rate data for Turkey not found.");
        }
    }
}