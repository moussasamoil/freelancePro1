using lotus_blue.Data;
using lotus_blue.Models.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace lotus_blue.Controllers
{
    public class EmployeeAccessController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EmployeeAccessController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public IActionResult Index()
        {
            var manufacturingCompanies = _context.ManufacturingCompanies
                .Select(mc => new EmployeeAccessViewModel
                {
                    Id = mc.Id,
                    CompanyName = mc.Name,
                    EmployeeCount = mc.EmployeeManufacturingCompanies.Count(emc => emc.Employee.IsShown)
                })
                .ToList();

            return View(manufacturingCompanies);
        }

        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public IActionResult Details(int id)
        {
            var company = _context.ManufacturingCompanies
                .Include(mc => mc.EmployeeManufacturingCompanies)
                .ThenInclude(emc => emc.Employee)
                .Where(mc => mc.Id == id)
                .Select(mc => new EmployeeAccessViewModel
                {
                    Id = mc.Id,
                    CompanyName = mc.Name,
                    Employees = mc.EmployeeManufacturingCompanies
                        .Where(emc => emc.Employee.IsShown)
                        .Select(emc => new EmployeeAccessEmployeeViewModel
                        {
                            EmployeeId = emc.EmployeeId,
                            EmployeeName = emc.Employee.Name,
                            CanSeeManufacturingCompany = emc.CanSeeManufacturingCompany
                        })
                        .ToList()
                })
                .FirstOrDefault();

            if (company == null)
            {
                return NotFound();
            }

            return View(company);
        }

        [Authorize(Roles = "Admin,ExecutiveDirector")]
        [HttpPost]
        public IActionResult UpdateEmployeeStatusForStores(int companyId, int employeeId, bool canSeeManufacturingCompany)
        {
            var employeeManufacturingCompany = _context.EmployeeManufacturingCompany
                .FirstOrDefault(emc => emc.ManufacturingCompanyId == companyId && emc.EmployeeId == employeeId);

            if (employeeManufacturingCompany == null)
            {
                return NotFound();
            }

            employeeManufacturingCompany.CanSeeManufacturingCompany = canSeeManufacturingCompany;
            _context.SaveChanges();

            return Ok();
        }
    }
}
