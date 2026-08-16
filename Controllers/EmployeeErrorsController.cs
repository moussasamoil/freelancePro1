using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace lotus_blue.Controllers
{
    [Authorize(Roles = "Admin")]
    public class EmployeeErrorsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly FileUploadService _fileUploadService;

        public EmployeeErrorsController(ApplicationDbContext context, FileUploadService fileUploadService)
        {
            _context = context;
            _fileUploadService = fileUploadService;
        }

        [HttpGet]
        public async Task<IActionResult> ActiveEmployees()
        {
            var employees = await _context.Employees
                .Where(e => e.IsActive)
                .OrderBy(e => e.Name)
                .Select(e => new
                {
                    id = e.Id,
                    name = e.Name
                })
                .ToListAsync();

            return Json(new { success = true, items = employees });
        }

        [HttpGet]
        public async Task<IActionResult> Panel()
        {
            var items = await _context.EmployeeErrors
                .Include(x => x.Employee)
                .Where(x => !x.IsDeleted)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new
                {
                    id = x.Id,
                    employeeId = x.EmployeeId,
                    employeeName = x.Employee != null ? x.Employee.Name : "بدون اسم",
                    errorText = x.ErrorText,
                    imageUrl = x.ImageUrl,
                    createdAt = x.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                })
                .ToListAsync();

            return Json(new { success = true, items });
        }

        [HttpGet]
        public async Task<IActionResult> Get(int id)
        {
            var item = await _context.EmployeeErrors
                .Include(x => x.Employee)
                .Where(x => x.Id == id && !x.IsDeleted)
                .Select(x => new
                {
                    id = x.Id,
                    employeeId = x.EmployeeId,
                    employeeName = x.Employee != null ? x.Employee.Name : "بدون اسم",
                    errorText = x.ErrorText,
                    imageUrl = x.ImageUrl,
                    createdAt = x.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                })
                .FirstOrDefaultAsync();

            if (item == null)
            {
                return Json(new { success = false, message = "لم يتم العثور على الخطأ" });
            }

            return Json(new { success = true, item });
        }

        [HttpPost]
        public async Task<IActionResult> Create(int employeeId, string errorText, IFormFile? imageFile)
        {
            if (employeeId <= 0)
            {
                return Json(new { success = false, message = "يجب اختيار الموظف" });
            }

            if (string.IsNullOrWhiteSpace(errorText))
            {
                return Json(new { success = false, message = "يجب كتابة الخطأ" });
            }

            var employeeExists = await _context.Employees.AnyAsync(e => e.Id == employeeId && e.IsActive);
            if (!employeeExists)
            {
                return Json(new { success = false, message = "الموظف غير موجود أو غير فعال" });
            }

            string? imageUrl = null;
            if (imageFile != null && imageFile.Length > 0)
            {
                imageUrl = await _fileUploadService.UploadFileAsync(imageFile, "EmployeeErrors");
            }

            var item = new EmployeeError
            {
                EmployeeId = employeeId,
                ErrorText = errorText.Trim(),
                ImageUrl = imageUrl,
                CreatedAt = DateTime.Now,
                CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                CreatedByUserName = User.Identity?.Name
            };

            _context.EmployeeErrors.Add(item);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> Update(int id, int employeeId, string errorText, IFormFile? imageFile)
        {
            var item = await _context.EmployeeErrors.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (item == null)
            {
                return Json(new { success = false, message = "لم يتم العثور على الخطأ" });
            }

            if (employeeId <= 0)
            {
                return Json(new { success = false, message = "يجب اختيار الموظف" });
            }

            if (string.IsNullOrWhiteSpace(errorText))
            {
                return Json(new { success = false, message = "يجب كتابة الخطأ" });
            }

            var oldText = item.ErrorText;
            var oldImage = item.ImageUrl;
            var oldEmployeeId = item.EmployeeId;

            string? newImageUrl = item.ImageUrl;
            if (imageFile != null && imageFile.Length > 0)
            {
                newImageUrl = await _fileUploadService.UploadFileAsync(imageFile, "EmployeeErrors");
            }

            item.EmployeeId = employeeId;
            item.ErrorText = errorText.Trim();
            item.ImageUrl = newImageUrl;
            item.UpdatedAt = DateTime.Now;
            item.UpdatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            item.UpdatedByUserName = User.Identity?.Name;

            _context.EmployeeErrorEditHistories.Add(new EmployeeErrorEditHistory
            {
                EmployeeErrorId = item.Id,
                EmployeeId = oldEmployeeId,
                OldErrorText = oldText,
                NewErrorText = item.ErrorText,
                OldImageUrl = oldImage,
                NewImageUrl = newImageUrl,
                CreatedAt = DateTime.Now,
                EditedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                EditedByUserName = User.Identity?.Name
            });

            _context.EmployeeErrors.Update(item);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.EmployeeErrors.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (item == null)
            {
                return Json(new { success = false, message = "لم يتم العثور على الخطأ" });
            }

            item.IsDeleted = true;
            item.DeletedAt = DateTime.Now;
            item.DeletedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            item.DeletedByUserName = User.Identity?.Name;

            _context.EmployeeErrors.Update(item);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAll()
        {
            var items = await _context.EmployeeErrors.Where(x => !x.IsDeleted).ToListAsync();
            foreach (var item in items)
            {
                item.IsDeleted = true;
                item.DeletedAt = DateTime.Now;
                item.DeletedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                item.DeletedByUserName = User.Identity?.Name;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> EditHistory()
        {
            var items = await _context.EmployeeErrorEditHistories
                .Include(x => x.Employee)
                .OrderByDescending(x => x.CreatedAt)
                .Take(200)
                .Select(x => new
                {
                    id = x.Id,
                    employeeName = x.Employee != null ? x.Employee.Name : "بدون اسم",
                    oldErrorText = x.OldErrorText,
                    newErrorText = x.NewErrorText,
                    imageUrl = x.NewImageUrl,
                    createdAt = x.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                })
                .ToListAsync();

            return Json(new { success = true, items });
        }

        [HttpGet]
        public async Task<IActionResult> DeletedHistory()
        {
            var items = await _context.EmployeeErrors
                .Include(x => x.Employee)
                .Where(x => x.IsDeleted)
                .OrderByDescending(x => x.DeletedAt ?? x.CreatedAt)
                .Take(200)
                .Select(x => new
                {
                    id = x.Id,
                    employeeName = x.Employee != null ? x.Employee.Name : "بدون اسم",
                    errorText = x.ErrorText,
                    imageUrl = x.ImageUrl,
                    deletedAt = (x.DeletedAt ?? x.CreatedAt).ToString("yyyy-MM-dd HH:mm")
                })
                .ToListAsync();

            return Json(new { success = true, items });
        }
    }
}
