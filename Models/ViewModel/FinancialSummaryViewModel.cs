using static lotus_blue.Models.Common;

namespace lotus_blue.Models.ViewModel
{
    public class FinancialSummaryViewModel
    {
        public Countries Country { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal TotalLoss { get; set; }
        public decimal TotalProfitInUSD { get; set; }
        public decimal TotalLossInUSD { get; set; }
        public string Currency { get; set; }
    }

}
