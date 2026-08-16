using static lotus_blue.Models.Common;
using System.ComponentModel.DataAnnotations;

namespace lotus_blue.Models.AppViewModel
{
    public class AppExchangeRateViewModel
    {
        public int Id { get; set; }

        [Display(Name = "Currency")]
        public string Currency { get; set; }

        public Common.Countries Country { get; set; }

        [Display(Name = "Buy to USD")]
        public decimal BuyToUSD { get; set; }

        [Display(Name = "Sell to USD")]
        public decimal SellToUSD { get; set; }
    }


    public class AppCreateExchangeRateViewModel
    {
        [Required(ErrorMessage = "Currency is required")]
        public Countries Currency { get; set; }

        [Required(ErrorMessage = "Buy to USD rate is required")]
        public decimal BuyToUSD { get; set; }

        [Required(ErrorMessage = "Sell to USD rate is required")]
        public decimal SellToUSD { get; set; }
    }

}
