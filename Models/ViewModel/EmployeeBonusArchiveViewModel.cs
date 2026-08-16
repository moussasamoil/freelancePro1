using System;
using System.Collections.Generic;

namespace lotus_blue.Models.ViewModel
{
    public class EmployeeBonusArchiveViewModel
    {
        public string SelectedEmployeeId { get; set; }
        public DateTime? FilterStart { get; set; }
        public DateTime? FilterEnd { get; set; }

        public List<EmployeeBonusArchiveRow> Rows { get; set; } = new List<EmployeeBonusArchiveRow>();

        public List<EmployeeOption> EmployeeOptions { get; set; } = new List<EmployeeOption>();
    }

    public class EmployeeBonusArchiveRow
    {
        public int PaymentId { get; set; }
        public string EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public DateTime DatePaid { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal ProExtraAmount { get; set; }
        public int TotalOrderCount { get; set; }
        public int ProOrderCount { get; set; }
        public int SuccessOrderCount { get; set; }
        public int ProcessingOrderCount { get; set; }
        public decimal ProcessingAmount { get; set; }
        public decimal SuccessAmount { get; set; }
    }
}
