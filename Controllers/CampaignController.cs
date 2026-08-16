using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static lotus_blue.Models.Common;
using lotus_blue.Models.ViewModel;
using lotus_blue.Services;

namespace lotus_blue.Controllers
{
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public class CampaignController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly GetCurrentTimeInIstanbul _timeService;
        private readonly DeliveryCompanyService _deliveryCompanyService;
        private readonly CurrencyExchangeService _currencyExchangeService;

        public CampaignController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment, GetCurrentTimeInIstanbul timeService, DeliveryCompanyService deliveryCompanyService, CurrencyExchangeService currencyExchangeService)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _timeService = timeService;
            _deliveryCompanyService = deliveryCompanyService;
            _currencyExchangeService = currencyExchangeService;
        }

        // GET: Campaign
        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            var query = _context.Campaigns
                .Include(c => c.MainWarehouse)
                .Include(c => c.ManufacturingCompany)
                .OrderByDescending(c => c.CreatedAt);

            var totalItems = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CampaignViewModel
                {
                    Id = c.Id,
                    ImageUrl = c.ImageUrl,
                    Country = c.Country,
                    MainWarehouseId = c.MainWarehouseId ?? 0,
                    WarehouseName = c.MainWarehouse.Name,
                    ManufacturingCompanyId = c.ManufacturingCompanyId,
                    ManufacturingCompanyName = c.ManufacturingCompany != null ? c.ManufacturingCompany.Name : null,
                    IsActive = c.IsActive,
                })
                .ToListAsync();

            var viewModel = new PaginationViewModel<CampaignViewModel>
            {
                Items = items,
                TotalItems = totalItems,
                CurrentPage = page,
                PageSize = pageSize
            };

            return View(viewModel);
        }


        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public async Task<IActionResult> CampaignAnalytics(
     Countries? countryId = null,
     int? campaignId = null,
     DateTime? startDay = null,
     DateTime? endDay = null,
     int? productId = null)
        {
            var now = _timeService.GetIstanbulTimeWithOffset();

            // Date logic (same as before)
            if (startDay == null || endDay == null)
            {
                startDay = DateTime.Today;
                endDay = DateTime.Today.AddDays(1);
            }
            else
            {
                startDay = startDay?.Date;
                endDay = endDay?.Date;
            }

            // Build the query
            var query = _context.Orders
                .Include(o => o.Campaign)
                    .ThenInclude(c => c.MainWarehouse)
                .Where(o => o.Campaign != null)
                .AsQueryable();

            // Apply filters
            if (countryId.HasValue)
            {
                query = query.Where(o => o.Campaign.Country == countryId.Value);
            }

            if (campaignId.HasValue && campaignId.Value > 0)
            {
                query = query.Where(o => o.CampaignId == campaignId.Value);
            }

            if (productId.HasValue && productId.Value > 0)
            {
                query = query.Where(o => o.Campaign.MainWarehouseId == productId.Value);
            }

            // Apply date range filter
            query = query.Where(x => x.InstantAddedDate >= startDay && x.InstantAddedDate < endDay);

            // Get all order IDs for total price calculations
            var orderIds = await query.Select(o => o.Id).ToListAsync();

            // Calculate total prices in USD and TL using your existing services
            var totalOrdersPriceInUSD = _deliveryCompanyService.CalculateTotalPriceInUSDForOrdersWithOutDeliveryCompanyPrice(orderIds);
            var totalOrdersPriceInTL = _currencyExchangeService.ConvertToTurkishLira(totalOrdersPriceInUSD);

            // Get distinct campaign IDs from the filtered orders
            var campaignIds = await query
                .Where(o => o.CampaignId.HasValue)
                .Select(o => o.CampaignId.Value)
                .Distinct()
                .ToListAsync();

            var campaignSales = new List<CampaignSalesViewModel>();

            // For each campaign, calculate its USD total using the same service
            foreach (var cId in campaignIds)
            {
                // Get orders for this specific campaign
                var campaignOrderIds = await query
                    .Where(o => o.CampaignId == cId)
                    .Select(o => o.Id)
                    .ToListAsync();

                if (campaignOrderIds.Any())
                {
                    // Get campaign details
                    var campaign = await _context.Campaigns
                        .Where(c => c.Id == cId)
                        .Select(c => new { c.Name, c.ImageUrl })
                        .FirstOrDefaultAsync();

                    // Calculate USD total for this campaign using the same service
                    var campaignUSDTotal = _deliveryCompanyService.CalculateTotalPriceInUSDForOrdersWithOutDeliveryCompanyPrice(campaignOrderIds);

                    campaignSales.Add(new CampaignSalesViewModel
                    {
                        CampaignId = cId,
                        CampaignName = campaign?.Name ?? "Unknown",
                        ImageUrl = campaign?.ImageUrl ?? "",
                        TotalOrders = campaignOrderIds.Count,
                        TotalSalesUSD = campaignUSDTotal,
                        TotalSalesTRY = _currencyExchangeService.ConvertToTurkishLira(campaignUSDTotal)
                    });
                }
            }

            // Get analytics data
            var analytics = new CampaignAnalyticsViewModel
            {
                TotalOrders = orderIds.Count,
                TotalPriceUSD = totalOrdersPriceInUSD,
                TotalPriceTRY = totalOrdersPriceInTL,
                CampaignSales = campaignSales
            };

            // Get campaigns for dropdown
            var campaigns = await _context.Campaigns
                .Include(c => c.MainWarehouse)
                .Where(c => c.IsActive)
                .OrderBy(c => c.MainWarehouse.Name)
                .ToListAsync();

            var viewModel = new CampaignAnalyticsFilterViewModel
            {
                Analytics = analytics,
                Campaigns = campaigns,
                SelectedCountryId = countryId,
                SelectedCampaignId = campaignId,
                StartDay = startDay?.ToString("yyyy-MM-dd"),
                EndDay = endDay?.AddDays(-1).ToString("yyyy-MM-dd")
            };

            return View(viewModel);
        }


        // GET: Campaign/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Campaign/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CampaignViewModel viewModel)
        {
            viewModel.SelectedCountries ??= new List<Countries>();
            viewModel.ImageFiles ??= new List<IFormFile>();

            if (ModelState.IsValid)
            {
                var imageUrls = new List<string>();

                var filesToUpload = viewModel.ImageFiles.Where(f => f != null && f.Length > 0).ToList();

                if (filesToUpload.Count == 0 && viewModel.ImageFile != null && viewModel.ImageFile.Length > 0)
                    filesToUpload.Add(viewModel.ImageFile);

                try
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "campaigns");
                    Directory.CreateDirectory(uploadsFolder);

                    foreach (var file in filesToUpload)
                    {
                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(fileStream);
                        }
                        imageUrls.Add("/images/campaigns/" + uniqueFileName);
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "حدث خطأ أثناء رفع الصور: " + ex.Message);
                    return View(viewModel);
                }

                if (imageUrls.Count == 0)
                    imageUrls.Add(null);

                var mainWarehouse = await _context.MainWarehouses
                    .FirstOrDefaultAsync(m => m.Id == viewModel.MainWarehouseId);

                try
                {
                    var countriesToCreate = viewModel.SelectedCountries.Count > 0
                        ? viewModel.SelectedCountries
                        : new List<Countries> { (Countries)0 };

                    foreach (var country in countriesToCreate)
                    {
                        foreach (var imageUrl in imageUrls)
                        {
                            _context.Add(new Campaign
                            {
                                ImageUrl = imageUrl,
                                Country = country,
                                MainWarehouseId = viewModel.MainWarehouseId,
                                ManufacturingCompanyId = viewModel.ManufacturingCompanyId,
                                Name = mainWarehouse?.Name,
                                IsActive = true,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }

                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "حدث خطأ أثناء حفظ البيانات: " + ex.Message);
                }
            }

            return View(viewModel);
        }


        // GET: Campaign/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var campaign = await _context.Campaigns.FindAsync(id);
            if (campaign == null)
            {
                return NotFound();
            }

            var viewModel = new CampaignViewModel
            {
                Id = campaign.Id,
                ImageUrl = campaign.ImageUrl,
                Country = campaign.Country,
                SelectedCountries = (int)campaign.Country == 0
                    ? new List<Countries>()
                    : new List<Countries> { campaign.Country },
                MainWarehouseId = campaign.MainWarehouseId ?? 0,
                ManufacturingCompanyId = campaign.ManufacturingCompanyId,
                IsActive = campaign.IsActive
            };

            return View(viewModel);
        }

        // POST: Campaign/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CampaignViewModel viewModel)
        {
            if (id != viewModel.Id)
            {
                return NotFound();
            }

            viewModel.SelectedCountries ??= new List<Countries>();

            if (ModelState.IsValid)
            {
                try
                {
                    var campaign = await _context.Campaigns.FindAsync(id);
                    if (campaign == null)
                    {
                        return NotFound();
                    }

                    // Handle file upload
                    if (viewModel.ImageFile != null && viewModel.ImageFile.Length > 0)
                    {
                        // Delete old image if exists
                        if (!string.IsNullOrEmpty(campaign.ImageUrl))
                        {
                            string oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, campaign.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldFilePath))
                            {
                                System.IO.File.Delete(oldFilePath);
                            }
                        }

                        // Save new image
                        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "campaigns");
                        Directory.CreateDirectory(uploadsFolder);

                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(viewModel.ImageFile.FileName);
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await viewModel.ImageFile.CopyToAsync(fileStream);
                        }

                        campaign.ImageUrl = "/images/campaigns/" + uniqueFileName;
                    }

                    var firstCountry = viewModel.SelectedCountries.Count > 0
                        ? viewModel.SelectedCountries[0]
                        : (Countries)0;

                    campaign.Country = firstCountry;
                    campaign.MainWarehouseId = viewModel.MainWarehouseId;
                    campaign.ManufacturingCompanyId = viewModel.ManufacturingCompanyId;
                    campaign.IsActive = viewModel.IsActive;

                    _context.Update(campaign);

                    // Create extra campaign rows for additional countries (sharing same image/warehouse/store)
                    foreach (var extraCountry in viewModel.SelectedCountries.Skip(1))
                    {
                        _context.Add(new Campaign
                        {
                            ImageUrl = campaign.ImageUrl,
                            Country = extraCountry,
                            MainWarehouseId = campaign.MainWarehouseId,
                            ManufacturingCompanyId = campaign.ManufacturingCompanyId,
                            Name = campaign.Name,
                            IsActive = campaign.IsActive,
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CampaignExists(viewModel.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return View(viewModel);
        }

        // POST: Campaign/Delete/5
        [HttpPost]
        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public async Task<IActionResult> Delete(int campaignId)
        {
            var campaign = await _context.Campaigns.FindAsync(campaignId);
            if (campaign == null)
            {
                return NotFound($"Campaign with ID {campaignId} not found.");
            }

            // Delete image file if exists
            if (!string.IsNullOrEmpty(campaign.ImageUrl))
            {
                string filePath = Path.Combine(_webHostEnvironment.WebRootPath, campaign.ImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            _context.Campaigns.Remove(campaign);
            await _context.SaveChangesAsync();

            return Ok($"Campaign with ID {campaignId} has been deleted.");
        }

        // POST: Campaign/ToggleStatus/5
        [HttpPost]
        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public async Task<IActionResult> ToggleStatus(int campaignId, bool isActive)
        {
            var campaign = await _context.Campaigns.FindAsync(campaignId);

            if (campaign == null)
            {
                return NotFound($"Campaign with ID {campaignId} not found.");
            }

            campaign.IsActive = isActive;
            await _context.SaveChangesAsync();

            string statusMessage = isActive ? "activated" : "deactivated";
            return Ok($"Campaign with ID {campaignId} has been {statusMessage}.");
        }

        // GET: Campaign/GetActiveCampaigns
        public async Task<IActionResult> GetActiveCampaigns()
        {
            var campaigns = await _context.Campaigns
                .Include(c => c.MainWarehouse)
                .Where(c => c.IsActive == true)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new
                {
                    c.Id,
                    c.ImageUrl,
                    Country = c.Country.ToString(),
                    c.MainWarehouseId,
                    ProductName = c.MainWarehouse.Name,
                    c.IsActive,
                    c.CreatedAt
                })
                .ToListAsync();

            return Json(campaigns);
        }

        // GET: Campaign/GetCampaignsByCountry
        public async Task<IActionResult> GetCampaignsByCountry(Countries country)
        {
            var campaigns = await _context.Campaigns
                .Include(c => c.MainWarehouse)
                .Where(c => c.Country == country && c.IsActive == true)
                .Select(c => new
                {
                    c.Id,
                    c.ImageUrl,
                    Country = c.Country.ToString(),
                    c.MainWarehouseId,
                    ProductName = c.MainWarehouse.Name,
                    c.IsActive,
                    c.CreatedAt
                })
                .ToListAsync();

            return Json(campaigns);
        }

        private bool CampaignExists(int id)
        {
            return _context.Campaigns.Any(e => e.Id == id);
        }
    }
}