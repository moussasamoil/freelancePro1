using lotus_blue.Data;
using lotus_blue.Models.AppViewModel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using lotus_blue.Models;
using lotus_blue.Models.ViewModel;
using System.Security.Claims;
using static lotus_blue.Models.Common;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace lotus_blue.Controllers
{
    public class DataListController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DataListController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult GetAllCountries()
        {
            var countries = Enum.GetValues(typeof(Countries))
                .Cast<Countries>()
                .Select(country => new
                {
                    id = (int)country,
                    name = country.ToString(),
                    imageUrl = Common.GetImageUrlByCountryName(country.ToString())
                }).ToList();

            return Ok(countries);
        }

        [HttpGet]
        public IActionResult GetPfdCountries()
        {
            var allowed = new[] { Countries.العراق, Countries.ليبيا, Countries.سلطنة_عمان, Countries.الإمارات };
            var countries = allowed.Select(c => new
            {
                id = (int)c,
                name = c.ToString(),
                imageUrl = Common.GetImageUrlByCountryName(c.ToString())
            }).ToList();
            return Ok(countries);
        }

        [HttpGet]
        public IActionResult GetAllOrderStatuses()
        {
            if (User.IsInRole("Admin") || User.IsInRole("ExecutiveDirector"))
                return Ok(GetOrderedStatuses(typeof(OrderStatusEnum)));
            if (User.IsInRole("DeliveryCompany") || User.IsInRole("DeliveryRepresentative"))
            {
                var allowed = new[]
                {
                    OrderStatusEnum.طلب_جديد,
                    OrderStatusEnum.تم_التجهيز,
                    OrderStatusEnum.قيد_التوصيل,
                    OrderStatusEnum.تم_التسليم,
                    OrderStatusEnum.فشل_التسليم,
                };
                return Ok(allowed.Select(s => (object)new
                {
                    id = (int)s,
                    name = s.ToString(),
                    imageUrl = Common.GetStatusIconUrl(s)
                }).ToList());
            }
            if (User.IsInRole("CallCenter"))
                return Ok(GetOrderedStatuses(typeof(OrderStatusesForEmployeesEnum)));
            if (User.IsInRole("FollowUpDepartment"))
                return Ok(GetOrderedStatuses(typeof(OrderStatusesForFollowUpDepartmentEnum)));

            return Forbid();
        }

        private static List<object> GetOrderedStatuses(Type enumType)
        {
            return enumType
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .OrderBy(f => f.MetadataToken)
                .Select(f =>
                {
                    var id = (int)f.GetRawConstantValue();
                    return (object)new
                    {
                        id = id,
                        name = f.Name,
                        imageUrl = Common.GetStatusIconUrl((OrderStatusEnum)id)
                    };
                })
                .ToList();
        }


        [HttpGet]
        public IActionResult GetAllFailureReasons()
        {
            var reasons = Enum.GetValues(typeof(FailureReasonEnum))
                .Cast<FailureReasonEnum>()
                .Select(r => new
                {
                    id = (int)r,
                    name = typeof(FailureReasonEnum)
                        .GetMember(r.ToString())[0]
                        .GetCustomAttribute<DisplayAttribute>()?.Name ?? r.ToString()
                }).ToList();
            return Ok(reasons);
        }

        // normal select cuz id is string
        [Authorize]
        public IActionResult GetAllEmployees()
        {
            var employees = _context.Employees
                .Where(a => a.IsShown)
                .Select(e => new
                {
                    Id = e.ApplicationUserId,
                    Name = e.Name,
                    LogoUrl = e.ImageUrl ?? "static/DefaultImage.svg"
                })
                .ToList();

            return Ok(employees);
        }

        [Authorize]
        public IActionResult GetAllEmployeesintId()
        {
            var employees = _context.Employees
                .Where(a => a.IsShown)
                .Select(e => new
                {
                    Id = e.Id,
                    Name = e.Name,
                    LogoUrl = e.ImageUrl ?? "static/DefaultImage.svg"
                })
                .ToList();

            return Ok(employees);
        }

        [HttpGet]
        public IActionResult GetAllStores()
        {
            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Start with the base query
            var query = _context.ManufacturingCompanies.Where(a=>a.IsShown).AsQueryable();

            if (User.IsInRole("FollowUpDepartment") || User.IsInRole("CallCenter"))
            {
                query = query.Where(o => o.EmployeeManufacturingCompanies.Any(a => a.ApplicationUserId == currentUser && a.CanSeeManufacturingCompany));

              
            }

            // Execute the query and select the desired data
            var companies = query
                .Select(mc => new
                {
                    id = mc.Id,
                    name = mc.Name,
                    logoUrl = mc.ImageUrl ?? "static/DefaultImage.svg",
                    mainWarehouseId = mc.MainWarehouseId
                })
                .ToList();

            return Ok(companies);
        }

        [Authorize]
        public IActionResult GetAllDeliveryCompanies(List<Common.Countries>? countryIds)
        {
            IQueryable<DeliveryCompany> query = _context.DeliveryCompanies
                .Where(a => a.IsShown && !a.IsRepresentative);

            // Apply countryIds filter if provided
            if (countryIds != null && countryIds.Any())
            {
                query = query.Where(dc => countryIds.Contains(dc.Country));
            }

            var companies = query
                .Select(mc => new GetDataListViewModel
                {
                    Id = mc.Id,
                    Name = mc.Name,
                    LogoUrl = mc.ImageUrl ?? "static/DefaultImage.svg"
                })
                .ToList();

            return Ok(companies);
        }


        [Authorize]
        public IActionResult GetAllDeliveryRepresentatives(List<Common.Countries>? countryIds, List<string>? cityIds)
        {
            IQueryable<DeliveryCompany> query = _context.DeliveryCompanies
                .Where(a => a.IsShown && a.IsRepresentative);

            // Apply countryIds filter if provided
            if (countryIds != null && countryIds.Any())
            {
                query = query.Where(dc => countryIds.Contains(dc.Country));
            }

            // Apply cityIds filter if provided and if there are non-empty values
            if (cityIds != null && cityIds.Any(id => !string.IsNullOrWhiteSpace(id)))
            {
                query = query.Where(dc => cityIds.Contains(dc.City));
            }

            var companies = query
                .Select(mc => new GetDataListViewModel
                {
                    Id = mc.Id,
                    Name = mc.Name,
                    LogoUrl = mc.ImageUrl ?? "static/DefaultImage.svg"
                })
                .ToList();

            return Ok(companies);
        }



        [Authorize]
        public IActionResult GetAllDeliveryCompaniesAndRepresentatives(Common.Countries? countryId, string cityId = null)
        {
            IQueryable<DeliveryCompany> query = _context.DeliveryCompanies
                .Where(a => a.IsShown);

            // Apply countryId filter if provided
            if (countryId != null)
            {
                query = query.Where(dc => dc.Country == countryId);
            }

            // Create two separate queries to combine later: one for companies and one for representatives
            var nonRepresentatives = query
                .Where(a => !a.IsRepresentative);

            var representatives = query
                .Where(a => a.IsRepresentative);

            // Apply cityId filter if provided
            if (!string.IsNullOrEmpty(cityId))
            {
                representatives = representatives.Where(dc => dc.City == cityId);
            }

            // Combine non-representatives and representatives into one list
            var companies = nonRepresentatives
                .Concat(representatives)
                .Select(mc => new GetDataListViewModel
                {
                    Id = mc.Id,
                    Name = mc.Name,
                    LogoUrl = mc.ImageUrl ?? "static/DefaultImage.svg",
                    IsRepresentative = mc.IsRepresentative
                })
                .ToList();

            return Ok(companies);
        }

        [Authorize]
        public IActionResult GetDeliveryPrice(int deliveryCompanyId, Common.Countries countryId, string cityId = null)
        {
            var price = _context.DeliveryCompanyPrices
                .Where(dcp => dcp.DeliveryCompanyId == deliveryCompanyId
                              && dcp.Country == countryId
                              && (dcp.City == null || dcp.City == cityId || cityId == null))
                .OrderByDescending(dcp => dcp.City == cityId)
                .Select(dcp => (decimal?)dcp.Price)
                .FirstOrDefault();

            return Ok(new { price = price ?? 0 });
        }

        [Authorize]
        public IActionResult GetFilteredWarehouses(int? deliveryCompanyId)
        {
            IQueryable<Warehouse> query = _context.Warehouses.Where(a => a.IsShown);

        

            // Apply delivery company filter
            if (deliveryCompanyId.HasValue)
            {
                query = query.Where(w => w.DeliveryCompanyId == deliveryCompanyId.Value);
            }

            // Exclude warehouses with a quantity of 0
            query = query.Where(w => w.Amount > 0); // Adjust 'Quantity' to the actual property name that holds the warehouse quantity

            // Materialize the query to get the list of filtered warehouses
            var filteredWarehouses = query.Select(w => new
            {
                id = w.Id,
                name = w.Name,
                productImage = w.MainWarehouse.ImageUrl ?? "static/DefaultImage.svg",
                amount = w.Amount,
                mainWarehouseId = w.MainWarehouseId
            }).ToList();

            return Ok(filteredWarehouses);
        }


        [Authorize]
        public IActionResult GetAllOrderSources()
        {
            var sources = Enum.GetValues(typeof(OrderSourceEnum))
                              .Cast<OrderSourceEnum>()
                              .Select(source => new { Id = source, Name = source.ToString(),LogoUrl=Common.GetSocialMediaIconUrl(source)})
                              .ToList();

            return Ok(sources);
        }

        [Authorize]
        public IActionResult GetMainWarehouses()
        {
            IQueryable<MainWarehouse> query = _context.MainWarehouses;

            // Materialize the query to get the list of filtered warehouses
            var filteredWarehouses = query.Select(w => new
            {
                Id = w.Id,
                Name = w.Name,
                LogoUrl = w.ImageUrl ?? "static/DefaultImage.svg"

            }).ToList();

            return Ok(filteredWarehouses); // Return filtered warehouses as JSON or customize as needed
        }

        [Authorize]
        public IActionResult GetSubWarehouses(int? mainWarehouseId)
        {
            IQueryable<SubWarehouse> query = _context.SubWarehouses;

            // Filter by MainWarehouseId if provided
            if (mainWarehouseId.HasValue)
            {
                query = query.Where(w => w.MainWarehouseId == mainWarehouseId.Value);
            }

            // Materialize the query to get the list of filtered warehouses
            var filteredWarehouses = query.Select(w => new
            {
                Id = w.Id,
                Name = w.Name,
            }).ToList();

            return Ok(filteredWarehouses); // Return filtered warehouses as JSON
        }


        [HttpGet]
        public ActionResult GetCitiesByCountry(List<Common.Countries>? countryIds)
        {
            List<string> cities = new List<string>();

            // Check if countryIds is provided and has elements
            if (countryIds != null && countryIds.Any())
            {
                foreach (var countryId in countryIds)
                {
                    if (Common.CitiesByCountry.TryGetValue(countryId, out var countryCities))
                    {
                        cities.AddRange(countryCities);
                    }
                }
            }

            // Log the retrieved cities for debugging
            Console.WriteLine("Retrieved cities: " + string.Join(", ", cities));

            // Return the distinct list of cities as JSON to avoid duplicates
            return Ok(cities.Distinct().ToList());
        }

        [Authorize]
        public async Task<IActionResult> GetCampaignsByCountry(Countries countryId)
        {
            var campaigns = await _context.Campaigns
                .Include(c => c.MainWarehouse)
                .Where(c => c.Country == countryId && c.IsActive)
                .Select(c => new
                {
                    c.Id,
                    c.ImageUrl,
                    Name = c.Name,
                    ManufacturingCompanyId = c.ManufacturingCompanyId
                })
                .OrderBy(c => c.Name)
                .ToListAsync();

            return Ok(campaigns); // Return filtered warehouses as JSON
        }


        // Returns users in CallCenter or FollowUpDepartment roles, for the
        // تعيين الموظف بالنيابة dropdown on Order/Index.
        [HttpGet]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
        public async Task<IActionResult> GetAssignableUsers()
        {
            var targetRoles = new[] { "CallCenter", "FollowUpDepartment" };

            var roleIds = await _context.Roles
                .Where(r => targetRoles.Contains(r.Name))
                .Select(r => r.Id)
                .ToListAsync();

            var userIds = await _context.UserRoles
                .Where(ur => roleIds.Contains(ur.RoleId))
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync();

            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .Join(_context.Employees.Where(e => e.IsShown),
                    u => u.Id,
                    e => e.ApplicationUserId,
                    (u, e) => new { u.Id, Name = e.DisplayName ?? e.Name })
                .OrderBy(x => x.Name)
                .ToListAsync();

            return Ok(users);
        }


    }
}
