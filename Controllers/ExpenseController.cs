using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.Models.ViewModel;
using Microsoft.AspNetCore.Authorization;

namespace lotus_blue.Controllers
{
    public class ExpenseController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ExpenseController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Expense
        [Authorize(Roles = "Admin,Accountant,Observer,ExecutiveDirector")]
        public IActionResult Index(int page = 1, int pageSize = 10)
        {
            var query = _context.Expenses.AsQueryable(); // Start with IQueryable for dynamic filtering

            // Project to view model
            var expenses = query
                .Select(e => new ExpenseViewModel
                {
                    Id = e.Id,
                    Description = e.Description,
                    Amount = e.Amount,
                    CreatedDate = e.CreatedDate
                });

            var totalItems = expenses.Count();

            // Apply pagination
            var paginatedExpenses = expenses
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var viewModel = new PaginationViewModel<ExpenseViewModel>
            {
                Items = paginatedExpenses,
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = totalItems
            };

            return View(viewModel);
        }

        // GET: Expense/Create
        [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Expense/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
        public IActionResult Create(ExpenseViewModel model)
        {
            if (ModelState.IsValid)
            {
                var expense = new Expense
                {
                    Description = model.Description,
                    Amount = model.Amount,
                    CreatedDate = model.CreatedDate
                };

                _context.Add(expense);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // GET: Expense/Edit/{id}
        [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
        public IActionResult Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var expense = _context.Expenses.Find(id);
            if (expense == null)
            {
                return NotFound();
            }

            var model = new ExpenseViewModel
            {
                Id = expense.Id,
                Description = expense.Description,
                Amount = expense.Amount,
                CreatedDate = expense.CreatedDate
            };

            return View(model);
        }

        // POST: Expense/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
        public IActionResult Edit(int id, ExpenseViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var expense = new Expense
                {
                    Id = model.Id,
                    Description = model.Description,
                    Amount = model.Amount,
                    CreatedDate = model.CreatedDate
                };

                _context.Update(expense);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // POST: Expense/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Accountant")]
        public IActionResult DeleteConfirmed(int id)
        {
            var expense = _context.Expenses.Find(id);
            if (expense == null)
            {
                return NotFound();
            }

            _context.Expenses.Remove(expense);
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Accountant")]
        public IActionResult DeleteAll()
        {
            _context.Expenses.RemoveRange(_context.Expenses);
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }

        // GET: Expense/Filter
        [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
        public IActionResult Filter()
        {
            return View();
        }

        // POST: Expense/Filter
        [HttpPost]
        [Authorize(Roles = "Admin,Accountant,ExecutiveDirector")]
        public IActionResult Filter(int selectedMonth, int selectedYear, int selectedDay)
        {
            var expenses = _context.Expenses
                .Where(e =>
                    e.CreatedDate.Day == selectedDay &&
                    e.CreatedDate.Month == selectedMonth &&
                    e.CreatedDate.Year == selectedYear)
                .Select(e => new ExpenseViewModel
                {
                    Id = e.Id,
                    Description = e.Description,
                    Amount = e.Amount,
                    CreatedDate = e.CreatedDate
                })
                .ToList();

            return View(expenses);
        }
    }
}
