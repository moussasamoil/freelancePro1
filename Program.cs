using lotus_blue.Services;
using lotus_blue.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using lotus_blue.Models;
using lotus_blue.API;
using DinkToPdf.Contracts;
using DinkToPdf;
using lotus_blue.Hubs;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using lotus_blue.Roles;
using Microsoft.AspNetCore.ResponseCompression;
using lotus_blue.MiddleWear;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// Connection String
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Database
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString);

    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
    }
}, ServiceLifetime.Transient);

builder.Services.AddScoped<IDbContextFactory<ApplicationDbContext>, DbContextFactory<ApplicationDbContext>>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidAudience = configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(configuration["Jwt:Key"]))
        };
    });

// Response Compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<GzipCompressionProvider>();
});

// Identity
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// Application Cookie Settings
// مهم:
// - قفل المتصفح لا يعمل Logout.
// - الحساب يفضل مفتوح لحد ما الموظف يعمل Logout بإيده.
// - بعد Logout، أي Login جديد يطلب صورة حضور من Login.cshtml.cs + Home/Index.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";

    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;

    options.Cookie.Name = ".AspNetCore.Identity.Application";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// SecurityStamp Validation
// مهم: لما الموظف يعمل Logout نعمل UpdateSecurityStamp، والاختيار ده يخلي باقي الأجهزة والبراوزرات تخرج فورًا.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});

// Password Policy
builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
});

// Data Protection
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(
        new DirectoryInfo(
            Path.Combine(builder.Environment.ContentRootPath,
            "App_Data",
            "DataProtection-Keys")))
    .SetApplicationName("LotusBlueCRM");

// Localization
var supportedCultures = new[] { "en-US" }
    .Select(c => new CultureInfo(c))
    .ToList();

var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en-US"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
};

// MVC + Razor Pages
builder.Services.AddControllersWithViews()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling =
            Newtonsoft.Json.ReferenceLoopHandling.Ignore;
    });

builder.Services.AddRazorPages();

// Session & Cache
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(7);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddHttpContextAccessor();

// SignalR
// تم توحيد AddSignalR مرة واحدة فقط وإضافة JsonStringEnumConverter للـ listener.
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
})
.AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters
        .Add(new JsonStringEnumConverter());
});

// Services
builder.Services.AddScoped<QueryFilteringService>();
builder.Services.AddScoped<OrderHub>();

builder.Services.AddTransient<FileUploadService>();
builder.Services.AddTransient<CurrencyExchangeService>();
builder.Services.AddTransient<DeliveryCompanyService>();
builder.Services.AddTransient<GetCurrentTimeInIstanbul>();
builder.Services.AddTransient<PdfReportGenerator>();
builder.Services.AddTransient<FinancialService>();
builder.Services.AddTransient<DecimalFormattingService>();
builder.Services.AddTransient<RoleAuthorizationService>();
builder.Services.AddTransient<PdfReportGeneratorWarehousesInvoice>();
builder.Services.AddTransient<PdfReportGenertorOrderDetails>();
builder.Services.AddTransient<PdfReportGeneratorShipmentInvoice>();
builder.Services.AddTransient<OrderService>();

builder.Services.AddTransient<lotus_blue.Services.Bonus.BonusWindowService>();
builder.Services.AddTransient<lotus_blue.Services.Bonus.BonusCalculationService>();
builder.Services.AddTransient<lotus_blue.Services.Bonus.BonusHomePanelService>();

builder.Services.AddScoped<DynamicCommon>();
builder.Services.AddScoped<CacheService>();
builder.Services.AddScoped<DataCacheService>();

builder.Services.AddSingleton(typeof(IConverter),
    new SynchronizedConverter(new PdfTools()));

builder.Services.AddSingleton<IDictionary<int, decimal>>(
    new Dictionary<int, decimal>());

builder.Services.AddSingleton<RESTAPI>();

builder.Services.AddHostedService<BackgroundServiceScheduled>();

var app = builder.Build();

// Middleware
app.UseResponseCompression();

app.UseRequestLocalization(localizationOptions);

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseMiddleware<ArabicTimeFormatMiddleware>();

app.UseSession();

app.UseRouting();

// CORS لازم يكون بعد UseRouting وقبل Authentication/Authorization عشان SignalR endpoints.
app.UseCors("AllowAllOrigins");

app.UseAuthentication();

app.UseAuthorization();

// SignalR Hubs
app.MapHub<OrderHub>("/orderHub");
app.MapHub<MessageHub>("/messageHub");

// Routes
app.MapControllerRoute(
    name: "EditHistory",
    pattern: "EditHistory/orderdetails/{id}/{editNumber}",
    defaults: new { controller = "EditHistory", action = "OrderDetails" });

app.MapControllerRoute(
    name: "pagination_and_filtering",
    pattern: "Home/Index",
    defaults: new { controller = "Home", action = "Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Razor Pages
app.MapRazorPages();

app.Run();
