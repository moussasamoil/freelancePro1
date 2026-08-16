using Z.Expressions.Compiler;
using static lotus_blue.Models.Common;

namespace lotus_blue.Models.ViewModel
{
    public class OrderByCountryViewModel
    {
        public string? Currency { get; set; }
        public string? TotalPrice { get; set; }
        public string? TotalPirceDollar { get; set; }
        public string? TotalPirceTl { get; set; }
        public string? SelectedCountry { get; set; }
        public string? TotalSumInDollars { get; set; } // Add this line
        public string? TotalSumInTL { get; set; } // Add this line
    }


}
