using lotus_blue.Models;
using System.ComponentModel.DataAnnotations.Schema;

public class EmployeeManufacturingCompany
{
    public int EmployeeId { get; set; }  // Introduce this as the foreign key for Employee
    public Employee Employee { get; set; }

    public int ManufacturingCompanyId { get; set; }
    public ManufacturingCompany ManufacturingCompany { get; set; }

    [ForeignKey("ApplicationUser")]
    public string ApplicationUserId { get; set; }
    public ApplicationUser ApplicationUser { get; set; }  // Assuming ApplicationUser exists and has a string ID

    public bool CanSeeManufacturingCompany { get; set; } = true;
}
