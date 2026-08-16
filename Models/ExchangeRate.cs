using System;
using System.ComponentModel.DataAnnotations;
using static lotus_blue.Models.Common;

namespace lotus_blue.Models
{
    public class ExchangeRate
    {
        public int Id { get; set; }

        [Display(Name = "العملة")]
        public Countries Country { get; set; }

        [Display(Name = "شراء مقابل الدولار")]
        public decimal BuyToUSD { get; set; }

        [Display(Name = "بيع مقابل الدولار")]
        public decimal SellToUSD { get; set; }
    }
}
