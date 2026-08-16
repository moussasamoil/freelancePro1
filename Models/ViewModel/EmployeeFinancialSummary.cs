using static lotus_blue.Models.Common;

namespace lotus_blue.Models.ViewModel
{
    public class EmployeeFinancialSummary
    {
        public int Id { get; set; }
        public string EmployeeUserId { get; set; }
        public string EmployeeName { get; set; }
        public Countries? Country { get; set; }
        public string TotalSalary { get; set; }
        public string TotalDeductions { get; set; }
        public string TotalRewards { get; set; }
        public string TotalAdvances { get; set; }
        public string TotalBonuses { get; set; }
        public string TotalCurrentAccount { get; set; }
    }


}
