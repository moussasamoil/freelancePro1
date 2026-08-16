using System.Collections.Generic;

namespace lotus_blue.Models.ViewModel
{
    public class EmployeeAccessViewModel
    {
        public int Id { get; set; }
        public string CompanyName { get; set; }
        public int EmployeeCount { get; set; }
        public List<EmployeeAccessEmployeeViewModel> Employees { get; set; }

        public EmployeeAccessViewModel()
        {
            Employees = new List<EmployeeAccessEmployeeViewModel>();
        }
    }

    public class EmployeeAccessEmployeeViewModel
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public bool CanSeeManufacturingCompany { get; set; }
    }
}
