using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.Models.ViewModel;
using lotus_blue.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Linq;
using System.Diagnostics.Metrics;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using lotus_blue.Migrations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace lotus_blue.Controllers
{

    public class WarehouseController : Controller
    {

        private readonly ApplicationDbContext _context;  // replace 'YourDbContext' with your actual DbContext class name
        private readonly FileUploadService _fileUploadService;  // Assuming this is your upload service
        private readonly GetCurrentTimeInIstanbul _timeService;
        private readonly QueryFilteringService _queryFilteringService;
        private readonly PdfReportGeneratorWarehousesInvoice _pdfReportGeneratorWarehousesInvoice; 
        public WarehouseController(ApplicationDbContext context, FileUploadService fileUploadService, 
            GetCurrentTimeInIstanbul timeService,
            QueryFilteringService queryFilteringService,
            PdfReportGeneratorWarehousesInvoice pdfReportGeneratorWarehousesInvoice)
        {
            _context = context;
            _fileUploadService = fileUploadService;
            _timeService = timeService;
            _queryFilteringService = queryFilteringService;
            _pdfReportGeneratorWarehousesInvoice = pdfReportGeneratorWarehousesInvoice;
        }


        // Warehouse for delivery company
        [Authorize(Roles = "Admin,DeliveryCompany,Accountant,OrderPreparer,Observer,ExecutiveDirector,FollowUpDepartment")]
        public ActionResult Index(
     int page = 1,
     int? pageSize = null,
     Common.Countries? countryId = null,
     int? deliveryCompanyId = null,
     int? mainwarehouseId = null,
     int? storeId = null)
        {
            // Debug input parameters
            Console.WriteLine($"\n[DEBUG] New Request - Page: {page}, Size: {pageSize}, Country: {countryId}");
            Console.WriteLine($"[DEBUG] DeliveryCompanyId: {deliveryCompanyId}, MainWarehouseId: {mainwarehouseId}, StoreId: {storeId}");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
            Console.WriteLine($"[DEBUG] User Info - ID: {userId}, Role: {userRole}");

            // Base query with optimized includes
            var query = _context.Warehouses
                .AsNoTracking()
                .Include(a => a.DeliveryCompany)
                .Include(a => a.MainWarehouse)
                .Include(w => w.ManufacturingCompany)
                .Where(w => !w.DeliveryCompany.IsRepresentative);

            Console.WriteLine($"[DEBUG] Initial query count: {query.Count()}");

            // Apply role filter if needed
            if (userRole == "DeliveryCompany")
            {
                query = query.Where(w => w.DeliveryCompany.UserId == userId);
                Console.WriteLine($"[DEBUG] After role filter count: {query.Count()}");
            }

            // Apply country filter (only once)
            if (countryId.HasValue)
            {
                query = query.Where(x => x.Countries == countryId.Value);
                Console.WriteLine($"[DEBUG] After country filter count: {query.Count()}");
            }

            // Apply other filters via service
            query = _queryFilteringService.ApplyFilters(
                query,
                null, // Country filter already applied
                null, // No order status needed
                null, // No order source needed
                storeId,
                deliveryCompanyId,
                null, // No delivery representative needed
                null, // No product ID needed
                null, // No start date needed
                null, // No end date needed
                null, // No city ID needed
                null, // No search needed
                null, // No employee ID needed
                null, // No comments filter needed
                null, // No gender filter needed
                null, // No offers filter needed
                null, // No discount filter needed
                null, // No bonus filter needed
                null, // No special clients filter needed
                null, // No fixed and delivered filter needed
                null, // No hidden filter needed
                null, // No complaints filter needed
                null, // No paid filter needed
                mainwarehouseId,
                null
            );

            // Get total count after all filters
            int totalItems = query.Count();
            int effectivePageSize = pageSize ?? 10;

            Console.WriteLine($"[DEBUG] Total filtered items: {totalItems}");
            Console.WriteLine($"[DEBUG] Pagination - Page: {page}, Size: {effectivePageSize}");

            // Client-side evaluation for more reliable results
            var allFiltered = query
                .OrderByDescending(w => w.Id)
                .ToList();

            Console.WriteLine($"[DEBUG] Retrieved {allFiltered.Count} items after full query");

            // Manual pagination
            var warehouseList = allFiltered
                .Skip((page - 1) * effectivePageSize)
                .Take(effectivePageSize)
                .Select(w => new WarehouseViewModel
                {
                    Id = w.Id,
                    Name = w.Name ?? "Unknown",
                    ProductImage = w.MainWarehouse?.ImageUrl ?? "static/DefaultImage.svg",
                    Amount = w.Amount,
                    Price = w.Price,
                    DeliveryCompanyName = w.DeliveryCompany?.Name ?? "Unknown Delivery Company",
                    ManufacturingCompanyName = w.ManufacturingCompany?.Name ?? "Unknown Manufacturer",
                    DateAdded = w.DateAdded,
                    DateUpdated = w.DateUpdated,
                    Countries = w.Countries,
                    IsShown = w.IsShown
                })
                .ToList();

            Console.WriteLine($"[DEBUG] Final paginated items count: {warehouseList.Count}");

            // Debug first item if available
            if (warehouseList.Any())
            {
                var firstItem = warehouseList.First();
                Console.WriteLine($"[DEBUG] First item details - ID: {firstItem.Id}, Name: {firstItem.Name}");
                Console.WriteLine($"[DEBUG] Image: {firstItem.ProductImage}, Delivery: {firstItem.DeliveryCompanyName}");
            }

            var viewModel = new PaginationViewModel<WarehouseViewModel>
            {
                Items = warehouseList,
                CurrentPage = page,
                PageSize = effectivePageSize,
                TotalItems = totalItems,
            };

            return View(viewModel);
        }


        // Warehouse for delivery representative 
        [Authorize(Roles = "Admin,DeliveryRepresentative,Accountant,OrderPreparer,Observer,ExecutiveDirector,FollowUpDepartment")]
        public ActionResult IndexRepresentative(int page = 1, int? pageSize = null, Common.Countries? countryId = null, string CityId = null, int? deliveryRepresentativeId = null, int? mainWarehouseId = null, int? storeId = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

            var sessionPrefix = "DeliveryRepresentative_Index_";



            // Retrieve filter values from session if they are not provided by the request
            pageSize = pageSize ?? HttpContext.Session.GetInt32($"{sessionPrefix}PageSize") ?? 10;
            countryId = countryId ?? (Enum.TryParse(HttpContext.Session.GetString($"{sessionPrefix}CountryId"), out Common.Countries country) ? country : (Common.Countries?)null);
            deliveryRepresentativeId = deliveryRepresentativeId ?? HttpContext.Session.GetInt32($"{sessionPrefix}DeliveryRepresentativeId");
            mainWarehouseId = mainWarehouseId ?? HttpContext.Session.GetInt32($"{sessionPrefix}MainWarehouseId");
            storeId = storeId ?? HttpContext.Session.GetInt32($"{sessionPrefix}StoreId");


            IQueryable<Warehouse> query = _context.Warehouses
                .Include(a => a.DeliveryCompany)
                .Include(a => a.MainWarehouse)
                .Include(w => w.ManufacturingCompany)
                .Where(w => w.DeliveryCompany.IsRepresentative);

            if (userRole == "DeliveryRepresentative")
            {
                query = query.Where(w => w.DeliveryCompany.UserId == userId);
            }

            // Apply filters using the combined QueryFilteringService
            query = _queryFilteringService.ApplyFilters(
                query,
                countryId,
                null, // No order status needed
                null, // No order source needed
                storeId,
                null, // No delivery company filter (specific to DeliveryRepresentative)
                deliveryRepresentativeId,
                null, // No product ID needed
                null, // No start date needed
                null, // No end date needed
                CityId,
                null, // No search needed
                null, // No employee ID needed
                null, // No comments filter needed
                null, // No gender filter needed
                null, // No offers filter needed
                null, // No discount filter needed
                null, // No bonus filter needed
                null, // No special clients filter needed
                null, // No fixed and delivered filter needed
                null, // No hidden filter needed
                null, // No complaints filter needed
                null, // No paid filter needed
                mainWarehouseId, // Main warehouse ID
                sessionPrefix
            );

            int totalItems = query.Count();
            int effectivePageSize = pageSize ?? 10;
            int skip = (page - 1) * effectivePageSize;

            var warehouses = query.OrderByDescending(w => w.Id)
                                  .Skip(skip)
                                  .Take(effectivePageSize)
                                  .Select(w => new WarehouseViewModel
                                  {
                                      Id = w.Id,
                                      Name = w.Name,
                                      ProductImage = w.MainWarehouse.ImageUrl,
                                      Amount = w.Amount,
                                      Price = w.Price,
                                      DeliveryCompanyName = w.DeliveryCompany.Name,
                                      ManufacturingCompanyName = w.ManufacturingCompany.Name,
                                      DateAdded = w.DateAdded,
                                      DateUpdated = w.DateUpdated,
                                      Countries = w.Countries,
                                      IsShown = w.IsShown,
                                      City=w.City
                                  })
                                  .ToList();

            var viewModel = new PaginationViewModel<WarehouseViewModel>
            {
                Items = warehouses,
                CurrentPage = page,
                PageSize = effectivePageSize,
                TotalItems = totalItems
            };

            return View(viewModel);
        }


        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
        public IActionResult Create()
        {
            var viewModel = new WarehouseViewModel();
            return View(viewModel);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
        public async Task<IActionResult> Create(WarehouseViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
             
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    Console.WriteLine(error.ErrorMessage);
    
                }
                return View(viewModel);
            }
        

            // Load the SubWarehouse from the database based on SubWarehouseId
            var subWarehouse = await _context.SubWarehouses
                .FirstOrDefaultAsync(sw => sw.Id == viewModel.SubWarehouseId);

            if (subWarehouse == null)
            {
                // Handle the case where the SubWarehouse is not found
                ModelState.AddModelError(string.Empty, "The selected sub warehouse does not exist.");
                return View(viewModel);
            }

            // Create the warehouse for the specified delivery company
            var warehouse = new Warehouse
            {
                Name = subWarehouse.Name, // Use the Name from the loaded SubWarehouse
                Price = viewModel.Price,
                SubWarehouseId = viewModel.SubWarehouseId,
                Amount = viewModel.Amount, // Set amount from the view model
                DeliveryCompanyId = viewModel.DeliveryCompanyId,
                ManufacturingCompanyId = viewModel.ManufacturingCompanyId,
                Countries = viewModel.Countries, // Set countries from the view model
                City = viewModel.City, // Set city from the view model
                UnchangingAmount = viewModel.Amount, // Set unchanging amount from the view model
                MainWarehouseId = viewModel.MainWarehouseId,
                
            };

            _context.Add(warehouse);
            await _context.SaveChangesAsync();

            // Explicitly load the DeliveryCompany for the specified warehouse after saving
            var createdWarehouse = await _context.Warehouses
                .FirstOrDefaultAsync(w => w.Id == warehouse.Id);

            if (createdWarehouse != null)
            {
                await _context.Entry(createdWarehouse).Reference(w => w.DeliveryCompany).LoadAsync();

                if (createdWarehouse.DeliveryCompany != null && createdWarehouse.DeliveryCompany.IsRepresentative)
                {
                    return RedirectToAction(nameof(IndexRepresentative));
                }
            }

            return RedirectToAction(nameof(Index));
        }





        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
        public async Task<IActionResult> Edit(int id)
        {
            var warehouse = await _context.Warehouses
                .Include(a => a.DeliveryCompany)
                .Include(a => a.ManufacturingCompany)
                .Include(a => a.MainWarehouse)
                .Include(a=>a.SubWarehouse)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (warehouse == null)
            {
                return NotFound();
            }

            var viewModel = new WarehouseViewModel
            {
                Id = warehouse.Id,
                Name = warehouse.SubWarehouse.Name,
                Price = warehouse.Price,
                Total = warehouse.Price * warehouse.Amount,
                UnchangingAmount = warehouse.UnchangingAmount,
                Amount = warehouse.Amount,
                ProductImage = warehouse.MainWarehouse.ImageUrl,
                DeliveryCompanyId = warehouse.DeliveryCompanyId,
                ManufacturingCompanyId = warehouse.ManufacturingCompanyId ?? 0, // Default to 0 if null
                Countries = warehouse.Countries,
                City = warehouse.City,
                MainWarehouseId = warehouse.MainWarehouseId,
                DeliveryCompanyName = warehouse.DeliveryCompany.Name,
                ManufacturingCompanyName = warehouse.ManufacturingCompany.Name,
                SubWarehouseId = warehouse.SubWarehouseId ?? 0,
            };

            return View(viewModel);
        }




        [HttpPost]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
        public async Task<IActionResult> Edit(int id, WarehouseViewModel viewModel)
        {
            if (id != viewModel.Id)
            {
                return NotFound();
            }

            var existingWarehouse = await _context.Warehouses
                                                  .Include(a => a.DeliveryCompany)
                                                   .Include(a => a.SubWarehouse)

                                                  .FirstOrDefaultAsync(w => w.Id == id);
            if (existingWarehouse == null)
            {
                return NotFound();
            }

            int addedAmount = viewModel.Amount - existingWarehouse.Amount;
            if (addedAmount > 0)
            {
                // Increase UnchangingAmount by the difference added to the warehouse
                existingWarehouse.UnchangingAmount += addedAmount;
            }

            // Mapping all properties
            existingWarehouse.Name = viewModel.Name;
            existingWarehouse.Countries = existingWarehouse.Countries;
            existingWarehouse.City = existingWarehouse.City; // Add any other properties that need to be mapped
            existingWarehouse.Name = existingWarehouse.SubWarehouse.Name;
            existingWarehouse.Price = viewModel.Price;
            existingWarehouse.Amount = viewModel.Amount;
            existingWarehouse.DeliveryCompanyId = existingWarehouse.DeliveryCompanyId;
            existingWarehouse.ManufacturingCompanyId = viewModel.ManufacturingCompanyId;
            existingWarehouse.DateUpdated = _timeService.GetIstanbulTimeWithOffset();
            existingWarehouse.MainWarehouseId = viewModel.MainWarehouseId; // Add any other properties that need to be mapped

            if (addedAmount != 0)
            {
                var warehouseEditHistory = new WarehouseEditHistory
                {
                    WarehouseId = existingWarehouse.Id,
                    EditDate = DateTime.Now, // Or use _timeService.GetIstanbulTimeWithOffset() for timezone-specific time
                    AddedAmount = addedAmount,
                    ApplicationUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) // This requires using System.Security.Claims
                };

                _context.Add(warehouseEditHistory);
            }

            _context.Update(existingWarehouse);
            await _context.SaveChangesAsync();

            if (existingWarehouse.DeliveryCompany.IsRepresentative)
            {
                return RedirectToAction(nameof(IndexRepresentative));
            }

            return RedirectToAction(nameof(Index));
        }




        [Authorize(Roles = "Admin,DeliveryCompany,ExecutiveDirector,DeliveryRepresentative,FollowUpDepartment")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var warehouse = await _context.Warehouses
                                          .Include(w => w.DeliveryCompany)
                                          .Include(w=>w.SubWarehouse)
                                          .ThenInclude(aw=>aw.MainWarehouse)
                                          .Include(w => w.ManufacturingCompany)
                                          .Include(w => w.WarehouseEditHistories)
                                             .ThenInclude(a => a.ApplicationUser)
                                          .FirstOrDefaultAsync(m => m.Id == id);
            if (warehouse == null)
            {
                return NotFound();
            }

            // Retrieve orders related to the warehouse with specific statuses
            // Retrieve all relevant orders related to the warehouse with specific statuses
            var orders = await _context.Orders
                                       .Where(o => (o.OrderStatus == OrderStatusEnum.تم_التسليم ||
                                                    o.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد ||
                                                    o.OrderStatus == OrderStatusEnum.تم_الدفع ||
                                                    o.OrderStatus == OrderStatusEnum.فشل_التسليم ||
                                                    o.OrderStatus == OrderStatusEnum.أخطاء_الشركات_والمندوبين ||
                                                    o.OrderStatus == OrderStatusEnum.الطلبات_المرجعة ||
                                                    o.OrderStatus == OrderStatusEnum.أرشيف_المرجع ||
                                                    o.OrderStatus == OrderStatusEnum.انتظار_المعالجة) &&
                                                   o.OrderWarehouses.Any(ow => ow.WarehouseId == id))
                                       .Include(o => o.OrderWarehouses)
                                       .ToListAsync();

            // Flatten the OrderWarehouses and filter them
            var orderWarehouses = orders.SelectMany(o => o.OrderWarehouses)
                                        .Where(ow => ow.WarehouseId == id)
                                        .ToList();


            // Separate delivered and failed orders with null check
            var deliveredOrders = orderWarehouses.Where(ow => ow.Order != null &&
                                                              (ow.Order.OrderStatus == OrderStatusEnum.تم_التسليم ||
                                                               ow.Order.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد ||
                                                               ow.Order.OrderStatus == OrderStatusEnum.تم_الدفع)).ToList();

            var failedOrders = orderWarehouses.Where(ow => ow.Order != null &&
                                                           (ow.Order.OrderStatus == OrderStatusEnum.فشل_التسليم ||
                                                            ow.Order.OrderStatus == OrderStatusEnum.أخطاء_الشركات_والمندوبين ||
                                                            ow.Order.OrderStatus == OrderStatusEnum.الطلبات_المرجعة ||
                                                            ow.Order.OrderStatus == OrderStatusEnum.أرشيف_المرجع ||
                                                            ow.Order.OrderStatus == OrderStatusEnum.انتظار_المعالجة)).ToList();



            var viewModel = new WarehouseViewModel
            {
                Id = warehouse.Id,
                Name = warehouse.SubWarehouse?.Name ?? "N/A", // Null check for SubWarehouse
                Price = warehouse.Price,
                Amount = warehouse.Amount,
                UnchangingAmount = warehouse.UnchangingAmount,
                Total = warehouse.Price * warehouse.Amount,
                ProductImage = warehouse.MainWarehouse?.ImageUrl ?? "default_image.png", // Null check for MainWarehouse
                DeliveryCompanyName = warehouse.DeliveryCompany?.Name ?? "N/A", // Null check for DeliveryCompany
                ManufacturingCompanyName = warehouse.ManufacturingCompany?.Name ?? "N/A", // Null check for ManufacturingCompany
                DeliveryCompanyId = warehouse.DeliveryCompany?.Id ?? 0, // Null check for DeliveryCompanyId
                ManufacturingCompanyId = warehouse.ManufacturingCompany?.Id ?? 0, // Null check for ManufacturingCompanyId
                DateAdded = warehouse.DateAdded,
                DateUpdated = warehouse.DateUpdated,
                Countries = warehouse.Countries,
                City = warehouse.City,
                EditHistories = warehouse.WarehouseEditHistories.ToList(),
                TotalDeliveredItemsFromSpecificOrders = deliveredOrders.Sum(ow => ow.Amount),
                TotalFailedDeliveredItemsFromSpecificOrders = failedOrders.Sum(ow => ow.Amount),
            };


            return View(viewModel);

        }




        [HttpPost]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
        public async Task<IActionResult> SetIsShown(int WareHouseId, bool isShown)
        {
            var Warehouse = await _context.Warehouses.FindAsync(WareHouseId);
            if (Warehouse == null)
            {
                return NotFound();
            }

            Warehouse.IsShown = isShown;
            _context.Update(Warehouse);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
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

            // Filter out sub-warehouses that are already associated with the specified delivery company
            var associatedSubWarehouses = _context.Warehouses
                .Where(w => w.MainWarehouseId == mainWarehouseId)
                .Select(w => w.SubWarehouseId)
                .ToList();


            // Materialize the query to get the list of filtered warehouses
            var filteredWarehouses = query.Select(w => new
            {
                Id = w.Id,
                Name = w.Name,
            }).ToList();

            return Ok(filteredWarehouses); // Return filtered warehouses as JSON
        }



        [HttpPost]
        public async Task<IActionResult> AllWarehousesPdf(int deliveryCompanyId)
        {
            var warehouses = await _context.Warehouses
                                           .Where(w => w.DeliveryCompanyId == deliveryCompanyId)
                                           .ToListAsync();

            var deliveryCompany = await _context.DeliveryCompanies
                .Include(a=>a.User)
                                                .FirstOrDefaultAsync(dc => dc.Id == deliveryCompanyId);

            if (deliveryCompany == null || !warehouses.Any())
            {
                return NotFound();
            }

            var model = new PriceOfferViewModel
            {
                DeliveryCompanyName = deliveryCompany.Name,
                DeliveryCompanyAddress = deliveryCompany.Address,
                DeliveryCompanyPhoneNumber = deliveryCompany.PhoneNumber,
                DeliveryCompanyEmail = deliveryCompany.User.Email,
                Products = warehouses.Select(w => new ProductViewModel
                {
                    Name = w.Name,
                    Price = w.Price,
                    Amount = w.Amount,
                    UnchangingAmount=w.UnchangingAmount,
                    TotalSoldAmount = w.UnchangingAmount - w.Amount
                }).ToList()
            };

            model.TotalPriceOfAllProducts = model.Products.Sum(p => p.TotalPrice);
            model.CreatedDate = DateTime.Now;
            Random random = new Random();
            model.InvoiceId = random.Next(1000, 10000);

            var pdfBytes = await _pdfReportGeneratorWarehousesInvoice.CreatePdfReportAsync(
                model.DeliveryCompanyName,
                model.DeliveryCompanyAddress,
                model.DeliveryCompanyPhoneNumber,
                model.DeliveryCompanyEmail,
                model.CreatedDate.ToString("yyyy-MM-dd"),
                model.InvoiceId.ToString(),
                model.TotalPriceOfAllProducts.ToString(),
                model.Products.Select(p => new WarehouseDetail
                {
                    WarehouseName = p.Name,
                    WarehousePrice = p.Price,
                    Quantity = p.Amount,
                    UnchangingAmount=p.UnchangingAmount,
                    TotalSoldAmount=p.UnchangingAmount - p.Amount,
                }).ToList()
            );

            Response.Headers.Add("Content-Disposition", "inline; filename=PriceOffer.pdf");
            return File(pdfBytes, "application/pdf");
        }



    }
}
