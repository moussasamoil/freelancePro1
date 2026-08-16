using Crm_LotusBlue.Models;
using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.Models.ViewModel;
using lotus_blue.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
public class EmployeeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly FileUploadService _fileUploadService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly GetCurrentTimeInIstanbul _timeService;

    public EmployeeController(
        ApplicationDbContext context,
        FileUploadService fileUploadService,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        GetCurrentTimeInIstanbul timeService)
    {
        _context = context;
        _fileUploadService = fileUploadService;
        _userManager = userManager;
        _roleManager = roleManager;
        _timeService = timeService;
    }

    private static string SafeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string GetJobTitleFromRole(string? role)
    {
        return role switch
        {
            "Admin" => "Admin",
            "ExecutiveDirector" => "Executive Director",
            "CallCenter" => "Call Center",
            "FollowUpDepartment" => "Follow Up Department",
            "DeliveryCompany" => "Delivery Company",
            "DeliveryRepresentative" => "Delivery Representative",
            _ => SafeText(role)
        };
    }

    private static string NormalizeAllowedIpAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var ips = value
            .Split(new[] { ',', ';', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(ip => ip.Trim())
            .Where(ip => !string.IsNullOrWhiteSpace(ip))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return string.Join(",", ips);
    }



    private static string NormalizeEmployeeCountry(string? value)
    {
        var text = SafeText(value);

        return text switch
        {
            "Egypt" => "Egypt",
            "Turkey" => "Turkey",
            "Iraq" => "Iraq",
            "Jordan" => "Jordan",
            "Libya" => "Libya",
            "Kuwait" => "Kuwait",
            "Qatar" => "Qatar",
            "Oman" => "Oman",
            "Bahrain" => "Bahrain",
            "Tunisia" => "Tunisia",
            _ => text
        };
    }

    private static string GetCurrencyByEmployeeCountry(string? country)
    {
        var text = SafeText(country).ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        if (text.Contains("egypt") || text.Contains("مصر") || text.Contains("egp"))
        {
            return "EGP";
        }

        if (text.Contains("turkey") || text.Contains("ترك") || text.Contains("try"))
        {
            return "TRY";
        }

        if (text.Contains("iraq") || text.Contains("العراق") || text.Contains("عراق") || text.Contains("iqd"))
        {
            return "IQD";
        }

        if (text.Contains("jordan") || text.Contains("الأردن") || text.Contains("اردن") || text.Contains("jod"))
        {
            return "JOD";
        }

        if (text.Contains("libya") || text.Contains("ليبيا") || text.Contains("lyd"))
        {
            return "LYD";
        }

        if (text.Contains("kuwait") || text.Contains("الكويت") || text.Contains("kwd"))
        {
            return "KWD";
        }

        if (text.Contains("qatar") || text.Contains("قطر") || text.Contains("qar"))
        {
            return "QAR";
        }

        if (text.Contains("oman") || text.Contains("عمان") || text.Contains("omr"))
        {
            return "OMR";
        }

        if (text.Contains("bahrain") || text.Contains("البحرين") || text.Contains("bhd"))
        {
            return "BHD";
        }

        if (text.Contains("tunisia") || text.Contains("تونس") || text.Contains("tnd"))
        {
            return "TND";
        }

        return "";
    }




    private static readonly HashSet<string> AllowedWeeklyOffDays = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Saturday", "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday"
    };

    private static string NormalizeWeeklyOffDays(IEnumerable<string>? weeklyOffDays)
    {
        if (weeklyOffDays == null)
        {
            return string.Empty;
        }

        return string.Join(",", weeklyOffDays
            .Select(day => SafeText(day))
            .Where(day => AllowedWeeklyOffDays.Contains(day))
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static bool IsShiftAccessExcludedRole(string? role)
    {
        return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "ExecutiveDirector", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ResolveApplyShiftAccessByRole(string? role, bool applyShiftAccess)
    {
        // Admin and Executive Director are always excluded from shift access blocking.
        return IsShiftAccessExcludedRole(role) ? false : applyShiftAccess;
    }

    private void SetEmployeeAttendanceOptionsViewBag(IEnumerable<string>? weeklyOffDays, bool applyShiftAccess)
    {
        ViewBag.WeeklyOffDays = NormalizeWeeklyOffDays(weeklyOffDays);
        ViewBag.ApplyShiftAccess = applyShiftAccess;
    }

    private async Task EnsureEmployeeAttendanceOptionsColumnsAsync()
    {
        await _context.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('dbo.Employees', 'WeeklyOffDays') IS NULL
BEGIN
    ALTER TABLE dbo.Employees ADD WeeklyOffDays NVARCHAR(100) NULL;
END;

IF COL_LENGTH('dbo.Employees', 'ApplyShiftAccess') IS NULL
BEGIN
    ALTER TABLE dbo.Employees ADD ApplyShiftAccess BIT NOT NULL CONSTRAINT DF_Employees_ApplyShiftAccess DEFAULT(1);
END;
");
    }

    private async Task SaveEmployeeAttendanceOptionsAsync(int employeeId, IEnumerable<string>? weeklyOffDays, bool applyShiftAccess)
    {
        await EnsureEmployeeAttendanceOptionsColumnsAsync();

        var weeklyOffDaysText = NormalizeWeeklyOffDays(weeklyOffDays);

        await _context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE dbo.Employees
SET WeeklyOffDays = {weeklyOffDaysText},
    ApplyShiftAccess = {applyShiftAccess}
WHERE Id = {employeeId};
");
    }

    private async Task<string> GetEmployeeWeeklyOffDaysAsync(int employeeId)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != ConnectionState.Open;

            if (shouldCloseConnection)
            {
                await connection.OpenAsync();
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = @"
IF COL_LENGTH('dbo.Employees', 'WeeklyOffDays') IS NULL
BEGIN
    SELECT CAST(N'' AS NVARCHAR(100));
END
ELSE
BEGIN
    SELECT TOP 1 CAST(ISNULL(WeeklyOffDays, N'') AS NVARCHAR(100))
    FROM dbo.Employees
    WHERE Id = @EmployeeId;
END";

                var parameter = command.CreateParameter();
                parameter.ParameterName = "@EmployeeId";
                parameter.Value = employeeId;
                command.Parameters.Add(parameter);

                var result = await command.ExecuteScalarAsync();
                return result == null || result == DBNull.Value ? string.Empty : result.ToString() ?? string.Empty;
            }
            finally
            {
                if (shouldCloseConnection)
                {
                    await connection.CloseAsync();
                }
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<bool> GetEmployeeApplyShiftAccessAsync(int employeeId)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != ConnectionState.Open;

            if (shouldCloseConnection)
            {
                await connection.OpenAsync();
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = @"
IF COL_LENGTH('dbo.Employees', 'ApplyShiftAccess') IS NULL
BEGIN
    SELECT CAST(1 AS BIT);
END
ELSE
BEGIN
    SELECT TOP 1 CAST(ISNULL(ApplyShiftAccess, 1) AS BIT)
    FROM dbo.Employees
    WHERE Id = @EmployeeId;
END";

                var parameter = command.CreateParameter();
                parameter.ParameterName = "@EmployeeId";
                parameter.Value = employeeId;
                command.Parameters.Add(parameter);

                var result = await command.ExecuteScalarAsync();

                if (result == null || result == DBNull.Value)
                {
                    return true;
                }

                return Convert.ToBoolean(result);
            }
            finally
            {
                if (shouldCloseConnection)
                {
                    await connection.CloseAsync();
                }
            }
        }
        catch
        {
            return true;
        }
    }

    private const string CheckInMethodPhoto = "Photo";
    private const string CheckInMethodQuestion = "Question";

    private static bool IsCheckInMethodExcludedRole(string? role)
    {
        return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "DeliveryCompany", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "DeliveryRepresentative", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "OrderPreparer", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeCheckInVerificationMethod(string? value, string? role = null)
    {
        if (IsCheckInMethodExcludedRole(role))
        {
            return CheckInMethodPhoto;
        }

        var text = SafeText(value);

        if (text.Equals(CheckInMethodQuestion, StringComparison.OrdinalIgnoreCase) ||
            text.Equals("Question", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("سؤال"))
        {
            return CheckInMethodQuestion;
        }

        return CheckInMethodPhoto;
    }

    private async Task SaveEmployeeCheckInVerificationMethodAsync(int employeeId, string? checkInVerificationMethod, string? role = null)
    {
        var normalizedMethod = NormalizeCheckInVerificationMethod(checkInVerificationMethod, role);

        try
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE [Employees]
                   SET [CheckInVerificationMethod] = {normalizedMethod}
                   WHERE [Id] = {employeeId}");
        }
        catch
        {
            // لو الميجريشن لسه متطبقتش أو العمود مش موجود، ما نوقفش إنشاء/تعديل الموظف.
            // بعد تشغيل Update-Database هيتحفظ الاختيار طبيعي.
        }
    }

    private async Task<string> GetEmployeeCheckInVerificationMethodAsync(int employeeId, string? role = null)
    {
        if (IsCheckInMethodExcludedRole(role))
        {
            return CheckInMethodPhoto;
        }

        try
        {
            var connection = _context.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != ConnectionState.Open;

            if (shouldCloseConnection)
            {
                await connection.OpenAsync();
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    IF COL_LENGTH('Employees', 'CheckInVerificationMethod') IS NULL
                    BEGIN
                        SELECT CAST(N'Photo' AS nvarchar(20));
                    END
                    ELSE
                    BEGIN
                        SELECT TOP 1 CAST(ISNULL(NULLIF([CheckInVerificationMethod], ''), N'Photo') AS nvarchar(20))
                        FROM [Employees]
                        WHERE [Id] = @employeeId;
                    END";

                var employeeIdParameter = command.CreateParameter();
                employeeIdParameter.ParameterName = "@employeeId";
                employeeIdParameter.Value = employeeId;
                command.Parameters.Add(employeeIdParameter);

                var result = await command.ExecuteScalarAsync();

                return NormalizeCheckInVerificationMethod(result?.ToString(), role);
            }
            finally
            {
                if (shouldCloseConnection)
                {
                    await connection.CloseAsync();
                }
            }
        }
        catch
        {
            return CheckInMethodPhoto;
        }
    }

    private void ClearRemovedEmployeeFieldsFromModelState()
    {
        ModelState.Remove(nameof(EmployeeViewModel.JobTitle));
        ModelState.Remove(nameof(EmployeeViewModel.AcademicLevel));
        ModelState.Remove(nameof(EmployeeViewModel.DateOfBirth));
    }

    private void ClearEmployeeCreateOptionalFieldsFromModelState()
    {
        // الحقول دي ملفات أو قيم اختيارية، فمينفعش توقف إنشاء الموظف لو فاضية.
        ModelState.Remove(nameof(EmployeeViewModel.Cv));
        ModelState.Remove(nameof(EmployeeViewModel.Img));
        ModelState.Remove(nameof(EmployeeViewModel.IdCardFrontImage));
        ModelState.Remove(nameof(EmployeeViewModel.IdCardBackImage));
        ModelState.Remove(nameof(EmployeeViewModel.NewPassword));
        ModelState.Remove(nameof(EmployeeViewModel.ConfirmNewPassword));
        ModelState.Remove(nameof(EmployeeViewModel.DeliveryCompanyId));
        ModelState.Remove(nameof(EmployeeViewModel.IsShown));
        ModelState.Remove(nameof(EmployeeViewModel.IsActive));
    }

    private static void SetDefaultRemovedEmployeeFields(EmployeeViewModel model)
    {
        model.JobTitle = !string.IsNullOrWhiteSpace(model.Role)
            ? GetJobTitleFromRole(model.Role)
            : "موظف";

        model.AcademicLevel = string.IsNullOrWhiteSpace(model.AcademicLevel)
            ? string.Empty
            : model.AcademicLevel;

        if (model.DateOfBirth == default)
        {
            model.DateOfBirth = new DateTime(2000, 1, 1);
        }
    }

    private async Task LoadEmployeeShiftViewBagAsync(int employeeId)
    {
        var activeShift = await _context.EmployeeWorkShifts
            .AsNoTracking()
            .Where(s => s.EmployeeId == employeeId && s.IsActive)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        ViewBag.ShiftStartTime = activeShift?.ShiftStartTime.ToString(@"hh\:mm") ?? "";
        ViewBag.ShiftEndTime = activeShift?.ShiftEndTime.ToString(@"hh\:mm") ?? "";
        ViewBag.AllowedIpAddress = activeShift?.AllowedIpAddress ?? "";
        ViewBag.ShiftNotes = activeShift?.Notes ?? "";
    }

    private async Task SaveEmployeeStorePermissionsAsync(Employee employee, ApplicationUser user, List<int>? selectedStoreIds)
    {
        var selectedStoreSet = (selectedStoreIds ?? new List<int>()).ToHashSet();

        var oldPermissions = await _context.Set<EmployeeManufacturingCompany>()
            .Where(x => x.EmployeeId == employee.Id || x.ApplicationUserId == user.Id)
            .ToListAsync();

        if (oldPermissions.Any())
        {
            _context.Set<EmployeeManufacturingCompany>().RemoveRange(oldPermissions);
        }

        var manufacturingCompanies = await _context.ManufacturingCompanies
            .AsNoTracking()
            .ToListAsync();

        foreach (var manufacturingCompany in manufacturingCompanies)
        {
            var employeeManufacturingCompany = new EmployeeManufacturingCompany
            {
                EmployeeId = employee.Id,
                ManufacturingCompanyId = manufacturingCompany.Id,
                ApplicationUserId = user.Id,
                CanSeeManufacturingCompany = selectedStoreSet.Contains(manufacturingCompany.Id)
            };

            _context.Entry(employeeManufacturingCompany).State = EntityState.Added;
        }
    }

    private async Task CreateEmployeeShiftAsync(
        int employeeId,
        TimeSpan shiftStartTime,
        TimeSpan shiftEndTime,
        string? allowedIpAddress,
        string? shiftNotes)
    {
        var previousActiveShifts = await _context.EmployeeWorkShifts
            .Where(s => s.EmployeeId == employeeId && s.IsActive)
            .ToListAsync();

        foreach (var oldShift in previousActiveShifts)
        {
            oldShift.IsActive = false;
        }

        var employeeShift = new EmployeeWorkShift
        {
            EmployeeId = employeeId,
            ShiftStartTime = shiftStartTime,
            ShiftEndTime = shiftEndTime,
            AllowedIpAddress = NormalizeAllowedIpAddress(allowedIpAddress),
            Notes = SafeText(shiftNotes),
            IsActive = true,
            CreatedAt = _timeService.GetIstanbulTimeWithOffset()
        };

        _context.EmployeeWorkShifts.Add(employeeShift);
    }

    private async Task PrepareEmployeeCreateListsAsync(EmployeeViewModel viewModel)
    {
        viewModel.Roles = await _roleManager.Roles
            .Select(r => r.Name)
            .Where(r => r != null)
            .Select(r => r!)
            .ToListAsync();

        ViewBag.ManufacturingCompanies = await _context.ManufacturingCompanies
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    private async Task PrepareEmployeeEditListsAsync(EmployeeViewModel viewModel)
    {
        viewModel.Roles = await _roleManager.Roles
            .Select(r => r.Name)
            .Where(r => r != null)
            .Select(r => r!)
            .ToListAsync();

        ViewBag.ManufacturingCompanies = await _context.ManufacturingCompanies
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync();

        if (User.IsInRole("Admin"))
        {
            viewModel.DeliveryCompanies = await _context.DeliveryCompanies.ToListAsync();
        }
    }

    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public async Task<IActionResult> Index(int page = 1, int pageSize = 10, string? employeeId = null)
    {
        IQueryable<Employee> query = _context.Employees.Include(e => e.ApplicationUser);

        if (!string.IsNullOrEmpty(employeeId))
        {
            query = query.Where(e => e.ApplicationUserId == employeeId);
        }

        int totalItems = await query.CountAsync();

        var employeesViewModel = await query
            .OrderByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EmployeeViewModel
            {
                Id = e.Id,
                Name = e.Name,
                JobTitle = e.JobTitle,
                Nationality = e.Nationality,
                PhoneNumber = e.PhoneNumber,
                IsShown = e.IsShown,
                IsActive = e.ApplicationUser.EmailConfirmed
            })
            .ToListAsync();

        var paginationViewModel = new PaginationViewModel<EmployeeViewModel>
        {
            Items = employeesViewModel,
            CurrentPage = page,
            PageSize = pageSize,
            TotalItems = totalItems
        };

        ViewBag.EmployeeMobileOrTabletAccessMap =
            await GetEmployeeMobileOrTabletAccessMapAsync(paginationViewModel.Items.Select(e => e.Id));

        return View(paginationViewModel);
    }

    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public async Task<IActionResult> Create()
    {
        var viewModel = new EmployeeViewModel();
        await PrepareEmployeeCreateListsAsync(viewModel);
        SetEmployeeAttendanceOptionsViewBag(null, true);
        return View(viewModel);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public async Task<IActionResult> Create(
        EmployeeViewModel model,
        IFormFile? cvFile,
        IFormFile? imgFile,
        IFormFile? idCardFrontFile,
        IFormFile? idCardBackFile,
        TimeSpan? shiftStartTime,
        TimeSpan? shiftEndTime,
        string? allowedIpAddress = "",
        string? shiftNotes = "",
        string? country = "",
        string? checkInVerificationMethod = "Photo",
        List<string>? weeklyOffDays = null,
        bool applyShiftAccess = true)
    {
        // مهم: نمسح ModelState هنا لأن ViewModel فيه حقول قديمة/مخفية مش موجودة في صفحة إنشاء الموظف
        // وكانت بتوقف الحفظ بدون ما يظهر سبب واضح.
        ModelState.Clear();

        ClearRemovedEmployeeFieldsFromModelState();
        ClearEmployeeCreateOptionalFieldsFromModelState();
        SetDefaultRemovedEmployeeFields(model);
        checkInVerificationMethod = NormalizeCheckInVerificationMethod(checkInVerificationMethod, model.Role);
        applyShiftAccess = ResolveApplyShiftAccessByRole(model.Role, applyShiftAccess);

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(EmployeeViewModel.Name), "اسم الموظف مطلوب.");
        }

        if (string.IsNullOrWhiteSpace(model.Nationality))
        {
            ModelState.AddModelError(nameof(EmployeeViewModel.Nationality), "الجنسية مطلوبة.");
        }

        if (string.IsNullOrWhiteSpace(country))
        {
            ModelState.AddModelError("country", "الدولة مطلوبة.");
        }

        if (string.IsNullOrWhiteSpace(model.PhoneNumber))
        {
            ModelState.AddModelError(nameof(EmployeeViewModel.PhoneNumber), "رقم الهاتف مطلوب.");
        }

        if (string.IsNullOrWhiteSpace(model.IdNumber))
        {
            ModelState.AddModelError(nameof(EmployeeViewModel.IdNumber), "رقم الهوية مطلوب.");
        }

        if (string.IsNullOrWhiteSpace(model.Address))
        {
            ModelState.AddModelError(nameof(EmployeeViewModel.Address), "العنوان مطلوب.");
        }

        if (model.Salary <= 0)
        {
            ModelState.AddModelError(nameof(EmployeeViewModel.Salary), "الراتب مطلوب ويجب أن يكون أكبر من صفر.");
        }

        if (string.IsNullOrWhiteSpace(model.Email))
        {
            ModelState.AddModelError(nameof(EmployeeViewModel.Email), "البريد الإلكتروني مطلوب.");
        }

        if (string.IsNullOrWhiteSpace(model.Password))
        {
            ModelState.AddModelError(nameof(EmployeeViewModel.Password), "كلمة المرور مطلوبة.");
        }

        if (string.IsNullOrWhiteSpace(model.ConfirmPassword))
        {
            ModelState.AddModelError(nameof(EmployeeViewModel.ConfirmPassword), "تأكيد كلمة المرور مطلوب.");
        }

        if (!string.IsNullOrWhiteSpace(model.Password) && model.Password != model.ConfirmPassword)
        {
            ModelState.AddModelError(nameof(EmployeeViewModel.ConfirmPassword), "كلمة المرور وتأكيد كلمة المرور غير متطابقين.");
        }

        if (string.IsNullOrWhiteSpace(model.Role))
        {
            ModelState.AddModelError(nameof(EmployeeViewModel.Role), "صلاحية الموظف مطلوبة.");
        }

        if (!shiftStartTime.HasValue)
        {
            ModelState.AddModelError("shiftStartTime", "وقت بداية الدوام مطلوب.");
        }

        if (!shiftEndTime.HasValue)
        {
            ModelState.AddModelError("shiftEndTime", "وقت نهاية الدوام مطلوب.");
        }

        if (string.IsNullOrWhiteSpace(model.DisplayName))
        {
            ModelState.AddModelError(nameof(EmployeeViewModel.DisplayName), "اسم الظهور مطلوب.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.CheckInVerificationMethod = checkInVerificationMethod;
            ViewBag.EmployeeCountry = NormalizeEmployeeCountry(country);
            SetEmployeeAttendanceOptionsViewBag(weeklyOffDays, applyShiftAccess);
            await PrepareEmployeeCreateListsAsync(model);
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = SafeText(model.Email),
            Email = SafeText(model.Email),
            Name = SafeText(model.DisplayName),
            EmailConfirmed = true
        };

        var userCreationResult = await _userManager.CreateAsync(user, model.Password);

        if (!userCreationResult.Succeeded)
        {
            foreach (var error in userCreationResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            ViewBag.CheckInVerificationMethod = checkInVerificationMethod;
            ViewBag.EmployeeCountry = NormalizeEmployeeCountry(country);
            SetEmployeeAttendanceOptionsViewBag(weeklyOffDays, applyShiftAccess);
            await PrepareEmployeeCreateListsAsync(model);
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(model.Role))
        {
            var roleAssignmentResult = await _userManager.AddToRoleAsync(user, model.Role);

            if (!roleAssignmentResult.Succeeded)
            {
                foreach (var error in roleAssignmentResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                ViewBag.CheckInVerificationMethod = checkInVerificationMethod;
                ViewBag.EmployeeCountry = NormalizeEmployeeCountry(country);
                SetEmployeeAttendanceOptionsViewBag(weeklyOffDays, applyShiftAccess);
                await PrepareEmployeeCreateListsAsync(model);
                return View(model);
            }
        }

        if (cvFile != null)
        {
            model.Cv = await _fileUploadService.UploadFileAsync(cvFile, "Employees");
        }

        if (imgFile != null)
        {
            model.Img = await _fileUploadService.UploadFileAsync(imgFile, "Employees");
        }

        if (idCardFrontFile != null)
        {
            model.IdCardFrontImage = await _fileUploadService.UploadFileAsync(idCardFrontFile, "Employees");
        }

        if (idCardBackFile != null)
        {
            model.IdCardBackImage = await _fileUploadService.UploadFileAsync(idCardBackFile, "Employees");
        }

        var employee = new Employee
        {
            Cv = model.Cv,
            ImageUrl = model.Img,
            IdCardFrontImage = model.IdCardFrontImage,
            IdCardBackImage = model.IdCardBackImage,
            Name = SafeText(model.Name),
            IdNumber = SafeText(model.IdNumber),
            Nationality = SafeText(model.Nationality),
            Country = NormalizeEmployeeCountry(country),
            PhoneNumber = SafeText(model.PhoneNumber),
            Address = SafeText(model.Address),
            Salary = model.Salary,
            AcademicLevel = SafeText(model.AcademicLevel),
            JobTitle = !string.IsNullOrWhiteSpace(model.Role) ? GetJobTitleFromRole(model.Role) : SafeText(model.JobTitle),
            DateOfBirth = model.DateOfBirth,
            Gender = model.Gender,
            DateAdded = _timeService.GetIstanbulTimeWithOffset(),
            DeliveryCompanyId = model.DeliveryCompanyId,
            ApplicationUserId = user.Id,
            DisplayName = SafeText(model.DisplayName),
            IsShown = true,
            IsActive = true
        };

        if (User.IsInRole("DeliveryCompany"))
        {
            employee.DeliveryCompanyId = user.AcessId;
        }

        _context.Add(employee);
        await _context.SaveChangesAsync();

        await SaveEmployeeCheckInVerificationMethodAsync(employee.Id, checkInVerificationMethod, model.Role);
        await SaveEmployeeAttendanceOptionsAsync(employee.Id, weeklyOffDays, applyShiftAccess);

        await CreateEmployeeShiftAsync(
            employee.Id,
            shiftStartTime!.Value,
            shiftEndTime!.Value,
            allowedIpAddress,
            shiftNotes);

        user.AcessId = employee.Id;
        await _userManager.UpdateAsync(user);

        await SaveEmployeeStorePermissionsAsync(employee, user, model.SelectedManufacturingCompanyIds);

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "تم إضافة الموظف بنجاح";
        return RedirectToAction(nameof(Index), new { employeeId = user.Id });
    }

    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var employee = await _context.Employees
            .Include(e => e.ApplicationUser)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (employee == null || employee.ApplicationUser == null)
        {
            return NotFound();
        }

        var currentUser = await _userManager.GetUserAsync(User);

        if (User.IsInRole("DeliveryCompany") && currentUser != null && employee.DeliveryCompanyId != currentUser.AcessId)
        {
            return Forbid();
        }

        var currentRoles = await _userManager.GetRolesAsync(employee.ApplicationUser);
        var currentRole = currentRoles.FirstOrDefault();

        var selectedStoreIds = await _context.Set<EmployeeManufacturingCompany>()
            .AsNoTracking()
            .Where(x => (x.EmployeeId == employee.Id || x.ApplicationUserId == employee.ApplicationUserId)
                && x.CanSeeManufacturingCompany)
            .Select(x => x.ManufacturingCompanyId)
            .Distinct()
            .ToListAsync();

        var viewModel = new EmployeeViewModel
        {
            Id = employee.Id,
            Name = employee.Name,
            JobTitle = employee.JobTitle,
            Nationality = employee.Nationality,
            Cv = employee.Cv,
            Img = employee.ImageUrl,
            IdCardFrontImage = employee.IdCardFrontImage,
            IdCardBackImage = employee.IdCardBackImage,
            IdNumber = employee.IdNumber,
            Address = employee.Address,
            Salary = employee.Salary,
            AcademicLevel = employee.AcademicLevel,
            DateOfBirth = employee.DateOfBirth,
            Gender = employee.Gender,
            DeliveryCompanyId = employee.DeliveryCompanyId,
            IsShown = employee.IsShown,
            IsActive = employee.ApplicationUser.EmailConfirmed,
            Email = employee.ApplicationUser.Email,
            DisplayName = employee.DisplayName,
            PhoneNumber = employee.PhoneNumber,
            Role = currentRole,
            SelectedManufacturingCompanyIds = selectedStoreIds
        };

        await LoadEmployeeShiftViewBagAsync(employee.Id);
        await PrepareEmployeeEditListsAsync(viewModel);
        ViewBag.CheckInVerificationMethod = await GetEmployeeCheckInVerificationMethodAsync(employee.Id, currentRole);
        ViewBag.EmployeeCountry = SafeText(employee.Country);
        ViewBag.WeeklyOffDays = await GetEmployeeWeeklyOffDaysAsync(employee.Id);
        var savedApplyShiftAccess = await GetEmployeeApplyShiftAccessAsync(employee.Id);
        ViewBag.ApplyShiftAccess = ResolveApplyShiftAccessByRole(currentRole, savedApplyShiftAccess);

        return View(viewModel);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public async Task<IActionResult> Edit(
        int id,
        EmployeeViewModel viewModel,
        IFormFile? cvFile,
        IFormFile? imgFile,
        IFormFile? idCardFrontFile,
        IFormFile? idCardBackFile,
        TimeSpan? shiftStartTime,
        TimeSpan? shiftEndTime,
        string? allowedIpAddress = "",
        string? shiftNotes = "",
        string? country = "",
        string? checkInVerificationMethod = "Photo",
        List<string>? weeklyOffDays = null,
        bool applyShiftAccess = true,
        string? newPassword = "",
        string? ConfirmNewPassword = "")
    {
        if (id != viewModel.Id)
        {
            return NotFound();
        }

        ClearRemovedEmployeeFieldsFromModelState();
        SetDefaultRemovedEmployeeFields(viewModel);
        applyShiftAccess = ResolveApplyShiftAccessByRole(viewModel.Role, applyShiftAccess);

        // كلمة المرور في صفحة التعديل اختيارية وليست مطلوبة إلا لو الإدارة كتبت كلمة جديدة.
        ModelState.Remove(nameof(EmployeeViewModel.Password));
        ModelState.Remove(nameof(EmployeeViewModel.ConfirmPassword));
        ModelState.Remove(nameof(EmployeeViewModel.NewPassword));
        ModelState.Remove(nameof(EmployeeViewModel.ConfirmNewPassword));

        if (!shiftStartTime.HasValue)
        {
            ModelState.AddModelError("shiftStartTime", "وقت بداية الدوام مطلوب.");
        }

        if (!shiftEndTime.HasValue)
        {
            ModelState.AddModelError("shiftEndTime", "وقت نهاية الدوام مطلوب.");
        }

        if (string.IsNullOrWhiteSpace(country))
        {
            ModelState.AddModelError("country", "الدولة مطلوبة.");
        }

        var currentUser = await _userManager.GetUserAsync(User);

        var existingEmployee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == id);

        if (existingEmployee == null)
        {
            return NotFound();
        }

        var user = await _userManager.FindByIdAsync(existingEmployee.ApplicationUserId);

        if (user == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            await LoadEmployeeShiftViewBagAsync(existingEmployee.Id);
            await PrepareEmployeeEditListsAsync(viewModel);
            ViewBag.CheckInVerificationMethod = NormalizeCheckInVerificationMethod(checkInVerificationMethod, viewModel.Role);
            ViewBag.EmployeeCountry = NormalizeEmployeeCountry(country);
            SetEmployeeAttendanceOptionsViewBag(weeklyOffDays, applyShiftAccess);
            return View(viewModel);
        }

        if (cvFile != null)
        {
            viewModel.Cv = await _fileUploadService.UpdateFileAsync(existingEmployee.Cv, cvFile, "Employees");
        }
        else
        {
            viewModel.Cv = existingEmployee.Cv;
        }

        if (imgFile != null)
        {
            viewModel.Img = await _fileUploadService.UpdateFileAsync(existingEmployee.ImageUrl, imgFile, "Employees");
        }
        else
        {
            viewModel.Img = existingEmployee.ImageUrl;
        }

        if (idCardFrontFile != null)
        {
            viewModel.IdCardFrontImage = await _fileUploadService.UpdateFileAsync(existingEmployee.IdCardFrontImage, idCardFrontFile, "Employees");
        }
        else
        {
            viewModel.IdCardFrontImage = existingEmployee.IdCardFrontImage;
        }

        if (idCardBackFile != null)
        {
            viewModel.IdCardBackImage = await _fileUploadService.UpdateFileAsync(existingEmployee.IdCardBackImage, idCardBackFile, "Employees");
        }
        else
        {
            viewModel.IdCardBackImage = existingEmployee.IdCardBackImage;
        }

        if (User.IsInRole("DeliveryCompany") && currentUser != null)
        {
            viewModel.DeliveryCompanyId = currentUser.AcessId;
        }

        existingEmployee.Name = SafeText(viewModel.Name);
        existingEmployee.Nationality = SafeText(viewModel.Nationality);
        existingEmployee.Country = NormalizeEmployeeCountry(country);
        existingEmployee.Cv = viewModel.Cv;
        existingEmployee.ImageUrl = viewModel.Img;
        existingEmployee.IdCardFrontImage = viewModel.IdCardFrontImage;
        existingEmployee.IdCardBackImage = viewModel.IdCardBackImage;
        existingEmployee.IdNumber = SafeText(viewModel.IdNumber);
        existingEmployee.Address = SafeText(viewModel.Address);
        existingEmployee.Salary = viewModel.Salary;
        existingEmployee.Gender = viewModel.Gender;
        existingEmployee.DeliveryCompanyId = viewModel.DeliveryCompanyId;
        existingEmployee.IsShown = viewModel.IsShown;
        existingEmployee.DisplayName = SafeText(viewModel.DisplayName);
        existingEmployee.PhoneNumber = SafeText(viewModel.PhoneNumber);

        // الحقول دي اتشالت من واجهة الإضافة والتعديل.
        // بنسيب القيم القديمة كما هي، ولو كانت فاضية بنحط قيم آمنة عشان قواعد الداتا بيز.
        existingEmployee.AcademicLevel = string.IsNullOrWhiteSpace(existingEmployee.AcademicLevel)
            ? string.Empty
            : existingEmployee.AcademicLevel;

        if (existingEmployee.DateOfBirth == default)
        {
            existingEmployee.DateOfBirth = new DateTime(2000, 1, 1);
        }

        user.EmailConfirmed = viewModel.IsActive;
        user.Name = SafeText(viewModel.DisplayName);

        if (!string.IsNullOrWhiteSpace(viewModel.Email) && user.Email != viewModel.Email)
        {
            user.Email = SafeText(viewModel.Email);
            user.UserName = SafeText(viewModel.Email);
        }

        var passwordToUse = !string.IsNullOrWhiteSpace(newPassword)
            ? newPassword
            : viewModel.NewPassword;

        var confirmPasswordToUse = !string.IsNullOrWhiteSpace(ConfirmNewPassword)
            ? ConfirmNewPassword
            : viewModel.ConfirmNewPassword;

        if (!string.IsNullOrWhiteSpace(passwordToUse))
        {
            if (passwordToUse != confirmPasswordToUse)
            {
                ModelState.AddModelError("PasswordMismatch", "كلمة المرور الجديدة وتأكيد كلمة المرور غير متطابقين.");
                await LoadEmployeeShiftViewBagAsync(existingEmployee.Id);
                await PrepareEmployeeEditListsAsync(viewModel);
                ViewBag.CheckInVerificationMethod = NormalizeCheckInVerificationMethod(checkInVerificationMethod, viewModel.Role);
                ViewBag.EmployeeCountry = NormalizeEmployeeCountry(country);
                SetEmployeeAttendanceOptionsViewBag(weeklyOffDays, applyShiftAccess);
                return View(viewModel);
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var passwordChangeResult = await _userManager.ResetPasswordAsync(user, token, passwordToUse);

            if (!passwordChangeResult.Succeeded)
            {
                foreach (var error in passwordChangeResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                await LoadEmployeeShiftViewBagAsync(existingEmployee.Id);
                await PrepareEmployeeEditListsAsync(viewModel);
                ViewBag.CheckInVerificationMethod = NormalizeCheckInVerificationMethod(checkInVerificationMethod, viewModel.Role);
                ViewBag.EmployeeCountry = NormalizeEmployeeCountry(country);
                SetEmployeeAttendanceOptionsViewBag(weeklyOffDays, applyShiftAccess);
                return View(viewModel);
            }
        }

        var selectedRole = viewModel.Role;

        if (!string.IsNullOrWhiteSpace(selectedRole))
        {
            var currentRoles = await _userManager.GetRolesAsync(user);

            if (!currentRoles.Contains(selectedRole))
            {
                if (currentRoles.Any())
                {
                    var removeRoleResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);

                    if (!removeRoleResult.Succeeded)
                    {
                        foreach (var error in removeRoleResult.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }

                        await LoadEmployeeShiftViewBagAsync(existingEmployee.Id);
                        await PrepareEmployeeEditListsAsync(viewModel);
                        ViewBag.CheckInVerificationMethod = NormalizeCheckInVerificationMethod(checkInVerificationMethod, viewModel.Role);
                        ViewBag.EmployeeCountry = NormalizeEmployeeCountry(country);
                        SetEmployeeAttendanceOptionsViewBag(weeklyOffDays, applyShiftAccess);
                        return View(viewModel);
                    }
                }

                var addRoleResult = await _userManager.AddToRoleAsync(user, selectedRole);

                if (!addRoleResult.Succeeded)
                {
                    foreach (var error in addRoleResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }

                    await LoadEmployeeShiftViewBagAsync(existingEmployee.Id);
                    await PrepareEmployeeEditListsAsync(viewModel);
                    ViewBag.CheckInVerificationMethod = NormalizeCheckInVerificationMethod(checkInVerificationMethod, viewModel.Role);
                    ViewBag.EmployeeCountry = NormalizeEmployeeCountry(country);
                    SetEmployeeAttendanceOptionsViewBag(weeklyOffDays, applyShiftAccess);
                    return View(viewModel);
                }
            }

            existingEmployee.JobTitle = GetJobTitleFromRole(selectedRole);
        }

        await CreateEmployeeShiftAsync(
            existingEmployee.Id,
            shiftStartTime!.Value,
            shiftEndTime!.Value,
            allowedIpAddress,
            shiftNotes);

        await SaveEmployeeStorePermissionsAsync(existingEmployee, user, viewModel.SelectedManufacturingCompanyIds);

        await SaveEmployeeCheckInVerificationMethodAsync(existingEmployee.Id, checkInVerificationMethod, selectedRole);
        await SaveEmployeeAttendanceOptionsAsync(existingEmployee.Id, weeklyOffDays, applyShiftAccess);

        var userUpdateResult = await _userManager.UpdateAsync(user);

        if (!userUpdateResult.Succeeded)
        {
            foreach (var error in userUpdateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await LoadEmployeeShiftViewBagAsync(existingEmployee.Id);
            await PrepareEmployeeEditListsAsync(viewModel);
            ViewBag.CheckInVerificationMethod = NormalizeCheckInVerificationMethod(checkInVerificationMethod, viewModel.Role);
            ViewBag.EmployeeCountry = NormalizeEmployeeCountry(country);
            SetEmployeeAttendanceOptionsViewBag(weeklyOffDays, applyShiftAccess);
            return View(viewModel);
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "تم التعديل بنجاح";
        return RedirectToAction(nameof(Edit), new { id = existingEmployee.Id });
    }

    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var employee = await _context.Employees
            .Include(e => e.ApplicationUser)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (employee == null)
        {
            return NotFound();
        }

        return View(employee);
    }

    private async Task<Dictionary<int, bool>> GetEmployeeMobileOrTabletAccessMapAsync(IEnumerable<int> employeeIds)
    {
        var ids = (employeeIds ?? Enumerable.Empty<int>())
            .Distinct()
            .ToList();

        if (!ids.Any())
        {
            return new Dictionary<int, bool>();
        }

        return await _context.Employees
            .AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .Select(e => new
            {
                e.Id,
                e.AllowMobileOrTabletLogin
            })
            .ToDictionaryAsync(e => e.Id, e => e.AllowMobileOrTabletLogin);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public async Task<IActionResult> SetAllowMobileOrTabletLogin(int Id, bool isAllowed)
    {
        var employee = await _context.Employees.FindAsync(Id);

        if (employee == null)
        {
            return Json(new { success = false, message = "Employee not found." });
        }

        employee.AllowMobileOrTabletLogin = isAllowed;
        _context.Update(employee);
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public async Task<IActionResult> SetIsActive(int Id, bool isActive)
    {
        var employee = await _context.Employees
            .Include(a => a.ApplicationUser)
            .FirstOrDefaultAsync(e => e.Id == Id);

        if (employee == null || employee.ApplicationUser == null)
        {
            return Json(new { success = false, message = "Employee not found." });
        }

        employee.ApplicationUser.EmailConfirmed = isActive;
        _context.Update(employee);
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public async Task<IActionResult> SetIsShown(int Id, bool isShown)
    {
        var employee = await _context.Employees.FindAsync(Id);

        if (employee == null)
        {
            return Json(new { success = false, message = "Employee not found." });
        }

        employee.IsShown = isShown;
        _context.Update(employee);
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }
}
