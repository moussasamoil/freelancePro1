
namespace lotus_blue.Models.ViewModel
{
    public class ExchangeRateViewModel
    {
        public int Id { get; set; }
        public Common.Countries Country { get; set; }
        public string? Currency { get; set; }
        public decimal BuyToUSD { get; set; }
        public decimal SellToUSD { get; set; }
        public string? CountryFlagUrl { get; set; }
    }
}
