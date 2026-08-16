using lotus_blue.Data;
using lotus_blue.Models.ViewModel;
using lotus_blue.Models;
using lotus_blue.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq;

namespace lotus_blue.Controllers
{
    public class OrderBonusConfigurationController : Controller
    {
        private readonly ApplicationDbContext _context;
        public OrderBonusConfigurationController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: OrderBonusConfiguration
        [Authorize(Roles = "Admin")]
        public IActionResult Index(Common.Countries? country, int? employeeId, int page = 1, int pageSize = 10)
        {
            var query = _context.OrderBonusConfigurations
                                .Include(ob => ob.Employee)
                                .AsQueryable(); // Start with IQueryable for dynamic filtering

            // Apply filters if provided
            if (country.HasValue)
            {
                query = query.Where(ob => ob.Country == country.Value);
            }

            if (employeeId.HasValue)
            {
                query = query.Where(ob => ob.EmployeeId == employeeId.Value);
            }

            // Project to view model
            var orderBonusConfigurations = query
                                            .Select(ob => new OrderBonusConfigurationViewModel
                                            {
                                                Id = ob.Id,
                                                OrderThreshold = ob.OrderThreshold,
                                                FlatBonusAmount = ob.FlatBonusAmount,
                                                PercentageBonus = ob.PercentageBonus,
                                                Country = ob.Country,
                                                EmployeeId = ob.EmployeeId,
                                                EmployeeName = ob.Employee != null ? ob.Employee.Name : string.Empty
                                            });

            var totalItems = orderBonusConfigurations.Count();

            // Apply pagination
            var paginatedConfigurations = orderBonusConfigurations
                                            .Skip((page - 1) * pageSize)
                                            .Take(pageSize)
                                            .ToList();

            var viewModel = new PaginationViewModel<OrderBonusConfigurationViewModel>
            {
                Items = paginatedConfigurations,
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };

            return View(viewModel);
        }



        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            var model = new OrderBonusConfigurationViewModel();
           
            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public IActionResult Create(OrderBonusConfigurationViewModel model)
        {
            if (ModelState.IsValid)
            {
                var orderBonusConfiguration = new OrderBonusConfiguration
                {
                    OrderThreshold = model.OrderThreshold,
                    FlatBonusAmount = model.FlatBonusAmount,
                    PercentageBonus = model.PercentageBonus,
                    Country = model.Country,
                    EmployeeId = model.EmployeeId
                };

                _context.OrderBonusConfigurations.Add(orderBonusConfiguration);
                _context.SaveChanges();

                return RedirectToAction("Index");
            }

            return View(model);
        }

        // GET: OrderBonusConfiguration/Edit/{id}
        [Authorize(Roles = "Admin")]
        public IActionResult Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var orderBonus = _context.OrderBonusConfigurations.Find(id);

            if (orderBonus == null)
            {
                return NotFound();
            }

            var model = new OrderBonusConfigurationViewModel
            {
                Id = orderBonus.Id,
                OrderThreshold = orderBonus.OrderThreshold,
                FlatBonusAmount = orderBonus.FlatBonusAmount,
                PercentageBonus = orderBonus.PercentageBonus,
                Country = orderBonus.Country,
                EmployeeId = orderBonus.EmployeeId,
            };

            return View(model);
        }

        // POST: OrderBonusConfiguration/Edit/{id}
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public IActionResult Edit(int id, OrderBonusConfigurationViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var orderBonus = new OrderBonusConfiguration
                {
                    Id = model.Id,
                    OrderThreshold = model.OrderThreshold,
                    FlatBonusAmount = model.FlatBonusAmount,
                    PercentageBonus = model.PercentageBonus,
                    Country = model.Country,
                    EmployeeId = model.EmployeeId
                };

                _context.Update(orderBonus);
                _context.SaveChanges();

                return RedirectToAction("Index");
            }

            return View(model);
        }
    }
}
