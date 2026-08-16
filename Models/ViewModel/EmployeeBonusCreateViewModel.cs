using System.Collections.Generic;

namespace lotus_blue.Models.ViewModel
{
    public class EmployeeBonusCreateViewModel
    {
        public List<EmployeeBonusRateRow> Rows { get; set; } = new List<EmployeeBonusRateRow>();
    }

    public class EmployeeBonusRateRow
    {
        public string EmployeeId { get; set; }

        public string EmployeeName { get; set; }

        public decimal BonusPercentage { get; set; }

        public decimal BonusProcessingPercentage { get; set; }

        public decimal ProBonusPercentage { get; set; }

        public decimal ProBonusProcessingPercentage { get; set; }

        public decimal ProThreshold { get; set; }

        public Common.Countries? Country { get; set; }
    }
}
