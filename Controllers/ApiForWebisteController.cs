//using lotus_blue.API;
//using lotus_blue.Data;
//using lotus_blue.Models;
//using lotus_blue.Models.ViewModel;
//using lotus_blue.Services;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Newtonsoft.Json;
//using System;
//using System.Threading.Tasks;
//using Microsoft.AspNetCore.Mvc;
//using Newtonsoft.Json;
//using lotus_blue.Models.ViewModel;
//using lotus_blue.API;
//using System.Text;
//using System.Net.Http;
//using System.Net;
//using lotus_blue.ApiToken;
//using Microsoft.EntityFrameworkCore;
//using System.Linq;
//using Microsoft.AspNetCore.Identity;
//using Microsoft.AspNetCore.SignalR;
//using lotus_blue.Hubs;
//using System.Security.Claims;

//namespace lotus_blue.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    public class ApiForWebisteController : ControllerBase
//    {
//        private readonly ApplicationDbContext _context;
//        private readonly GetCurrentTimeInIstanbul _timeService;
//        private readonly RESTAPI _restApi;
//        private readonly DeliveryCompanyService _deliveryCompanyService;
//        private readonly DynamicCommon _dynamicCommon;
//        private readonly IHubContext<OrderHub> _hubContext;

//        public ApiForWebisteController(
//            ApplicationDbContext context,
//            GetCurrentTimeInIstanbul timeService,
//            RESTAPI restApi,
//            DeliveryCompanyService deliveryCompanyService,
//            DynamicCommon dynamicCommon,
//            IHubContext<OrderHub> hubContext)
//        {
//            _context = context;
//            _timeService = timeService;
//            _restApi = restApi;
//            _deliveryCompanyService = deliveryCompanyService;
//            _dynamicCommon = dynamicCommon;
//            _hubContext = hubContext;
//        }

//        // POST: api/OrdersApi/CreateOrder
//        [HttpPost("CreateOrder")]
//        [TokenAuthorize]
//        public async Task<IActionResult> CreateOrder(OrderFromWebsiteViewModel viewModel)
//        {
//            // Define the tokens and corresponding ManufacturingCompanyId
//            var tokenToManufacturingCompanyMap = new Dictionary<string, int>
//            {
//                { "e62a36ef2abbd7cf", 1 },
//                { "949d484c836c28f9", 2 }
//            };

//            // Get the token from the request headers or authorization context
//            var token = HttpContext.Request.Headers["Authorization"].ToString();

//            // Set the default ManufacturingCompanyId to 1 or get it from the map
//            int manufacturingCompanyId = tokenToManufacturingCompanyMap.ContainsKey(token)
//                                        ? tokenToManufacturingCompanyMap[token]
//                                        : 1;

//            var deliveryCompany = _context.DeliveryCompanies
//                .Where(a => a.IsShown && a.IsActive &&
//                            a.Country == viewModel.Country &&
//                            (a.City == viewModel.City || a.City == null))
//                .OrderByDescending(a => a.City == viewModel.City) // Ensure exact city match has higher priority
//                .FirstOrDefault();

//            if (deliveryCompany == null)
//            {
//                return new JsonResult(new { message = "Delivery company not found for the specified country and city." }) { StatusCode = 404 };
//            }

//            var warehouses = _context.Warehouses
//                .Include(a => a.SubWarehouse)
//                .Where(w => w.DeliveryCompanyId == deliveryCompany.Id &&
//                            viewModel.SelectedWarehouses.Select(sw => sw.ProductCode).Contains(w.SubWarehouse.ProductCode))
//                .ToList();

//            foreach (var selectedWarehouse in viewModel.SelectedWarehouses)
//            {
//                var warehouse = warehouses.FirstOrDefault(w => w.SubWarehouse.ProductCode == selectedWarehouse.ProductCode);
//                if (warehouse == null)
//                {
//                    var existingWarehouse = _context.Warehouses
//                                                    .Where(w => w.SubWarehouse.ProductCode == selectedWarehouse.ProductCode)
//                                                    .FirstOrDefault();

//                    var newWarehouse = new Warehouse
//                    {
//                        Name = existingWarehouse?.Name ?? "Default Name", // Fetch name from API or sender
//                        Price = 0,
//                        UnchangingAmount = 0,
//                        Amount = 10,
//                        DeliveryCompanyId = deliveryCompany.Id,
//                        ManufacturingCompanyId = manufacturingCompanyId, // Set ManufacturingCompanyId based on the token
//                        DateAdded = _timeService.GetIstanbulTimeWithOffset(),
//                        DateUpdated = _timeService.GetIstanbulTimeWithOffset(),
//                        MainWarehouseId = existingWarehouse?.MainWarehouseId ?? 1, // Set MainWarehouseId from existing warehouse or default
//                        Countries = viewModel.Country,
//                        City = viewModel.City,
//                        IsShown = true,
//                    };

//                    _context.Warehouses.Add(newWarehouse);
//                    await _context.SaveChangesAsync();
//                    warehouses.Add(newWarehouse); // Add to the local list for further processing
//                }
//            }

//            if (warehouses == null || warehouses.Count == 0)
//            {
//                return new JsonResult(new { message = "Warehouses not found for the specified product codes and delivery company." }) { StatusCode = 404 };
//            }

//            try
//            {
//                var order = new Order
//                {
//                    Country = viewModel.Country,
//                    State = viewModel.City,
//                    OrderSource = OrderSourceEnum.ويبسات,
//                    SourceName = viewModel.CustomerName,
//                    ManufacturingCompanyId = manufacturingCompanyId, // Set ManufacturingCompanyId based on the token
//                    DeliveryCompanyId = deliveryCompany.Id,
//                    TelephoneNumber = viewModel.TelephoneNumber,
//                    CustomerName = viewModel.CustomerName,
//                    Notes = viewModel.Notes,
//                    Address = viewModel.Address,
//                    CreatedDate = _timeService.GetIstanbulTimeWithOffset(),
//                    LastEditedDate = _timeService.GetIstanbulTimeWithOffset(),
//                    OrderStatus = OrderStatusEnum.طلب_جديد,
//                    TotalPrice = viewModel.TotalPrice,
//                    ApplicationUserId = "d841d7cd-4fa4-46ea-9219-f91a94e9601c",
//                    InstantAddedDate = _timeService.GetIstanbulTimeWithOffset(),
//                    Gender = false,
//                };

//                if (viewModel.Country == Common.Countries.العراق)
//                {
//                    var externalOrderData = new OrderPostApi
//                    {
//                        Country = (int)viewModel.Country,
//                        State = viewModel.City,
//                        OrderSource = (int)OrderSourceEnum.ويبسات,
//                        SourceName = viewModel.CustomerName,
//                        TelephoneNumber = viewModel.TelephoneNumber,
//                        SecondTelephoneNumber = "",
//                        CustomerName = viewModel.CustomerName,
//                        Notes = viewModel.Notes ?? "",
//                        Address = viewModel.Address,
//                        CreatedDate = _timeService.GetIstanbulTimeWithOffset().ToString("yyyy-MM-ddTHH:mm:ss"),
//                        TotalPrice = viewModel.TotalPrice,
//                        StoreId = 40,
//                    };

//                    var apiResponse = await _restApi.CreateOrderAsync(externalOrderData);
//                    if (!apiResponse.IsSuccessStatusCode)
//                    {
//                        var responseString = await apiResponse.Content.ReadAsStringAsync();
//                        var apiErrorMessage = $"API call failed with status code: {apiResponse.StatusCode}. Response: {responseString}";
//                        return new JsonResult(new { message = "Failed to create external order.", details = apiErrorMessage }) { StatusCode = 500 };
//                    }

//                    var responseContent = await apiResponse.Content.ReadAsStringAsync();
//                    var apiResult = JsonConvert.DeserializeObject<OrderPostApi>(responseContent);
//                    order.ExternalOrderId = apiResult.OrderId;
//                }

//                _context.Add(order);

//                if (await _context.SaveChangesAsync() == 0)
//                {
//                    var saveErrorMessage = "خطأ في تنزيل الطلب على العراق. أرجو المحاولة لاحقا.";
//                    return new JsonResult(new { message = saveErrorMessage }) { StatusCode = 500 };
//                }

//                foreach (var selectedWarehouse in viewModel.SelectedWarehouses)
//                {
//                    var warehouse = warehouses.FirstOrDefault(w => w.SubWarehouse.ProductCode == selectedWarehouse.ProductCode);
//                    if (warehouse != null)
//                    {
//                        var existingOrderWarehouse = _context.OrderWarehouses
//                            .FirstOrDefault(ow => ow.OrderId == order.Id && ow.WarehouseId == warehouse.Id);

//                        if (existingOrderWarehouse == null)
//                        {
//                            var orderWarehouse = new OrderWarehouse
//                            {
//                                WarehouseId = warehouse.Id,
//                                OrderId = order.Id,
//                                Amount = selectedWarehouse.Amount
//                            };
//                            _context.OrderWarehouses.Add(orderWarehouse);
//                        }
//                        else
//                        {
//                            existingOrderWarehouse.Amount += selectedWarehouse.Amount;
//                            _context.OrderWarehouses.Update(existingOrderWarehouse);
//                        }
//                    }
//                }

//                await _context.SaveChangesAsync();

//                var orderHistory = new OrderStatusHistory
//                {
//                    CreatedAt = _timeService.GetIstanbulTimeWithOffset(),
//                    Status = OrderStatusEnum.طلب_جديد,
//                    ApplicationUserId = "d841d7cd-4fa4-46ea-9219-f91a94e9601c",
//                    OrderId = order.Id
//                };

//                _context.OrderStatusHistories.Add(orderHistory);
//                await _context.SaveChangesAsync();

//                var settings = new JsonSerializerSettings
//                {
//                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
//                };

//                var userNamesForOrders = await _deliveryCompanyService.GetUserNameForOrderIdAsync(order.Id);
//                var deliveryCompanyPrice = await _deliveryCompanyService.GetDeliveryCompanyPriceForOrderIdAsync(order.Id);
//                var manufacturingCompanyName = await _dynamicCommon.GetManufacturingCompanyNameByOrderIdAsync(order.Id);
//                var deliveryCompanyName = await _dynamicCommon.GetDeliveryCompanyNameByOrderIdAsync(order.Id);
//                var manufacturingCompanyImage = await _dynamicCommon.GetManufacturingCompanyImageByOrderIdAsync(order.Id);
//                var deliveryCompanyImage = await _dynamicCommon.GetDeliveryCompanyImageByOrderIdAsync(order.Id);

//                var orders = new
//                {
//                    Order = order,
//                    Username = userNamesForOrders,
//                    DeliveryCompanyPrice = deliveryCompanyPrice,
//                    ManufacturingCompanyName = manufacturingCompanyName,
//                    DeliveryCompanyName = deliveryCompanyName,
//                    DeliveryCompanyImage = deliveryCompanyImage,
//                    ManufacturingCompanyImage = manufacturingCompanyImage
//                };

//                var orderJson = JsonConvert.SerializeObject(orders, settings);
//                var targetDeliveryCompanyId = order.DeliveryCompanyId;
//                var targetGroup = $"deliveryCompany_{targetDeliveryCompanyId}";

//                await _hubContext.Clients.Group("UsersExpectDelivery").SendAsync("NotifyOrderAdded", orderJson);
//                await _hubContext.Clients.Group(targetGroup).SendAsync("NotifyOrderAdded", orderJson);

//                return Ok(new { orderId = order.Id });
//            }
//            catch (Exception ex)
//            {
//                return new JsonResult(new { message = "حدث خطأ غير متوقع. الرجاء المحاولة لاحقا.", details = ex.Message }) { StatusCode = 500 };
//            }
//        }



//        public class OrderFromWebsiteViewModel
//        {
//            public Common.Countries Country { get; set; }
//            public string City { get; set; }
//            public string CustomerName { get; set; }
//            public string TelephoneNumber { get; set; }
//            public string Notes { get; set; }
//            public string Address { get; set; }
//            public decimal TotalPrice { get; set; }
//            public List<WarehouseFromWebsiteViewModel> SelectedWarehouses { get; set; }
//        }

//        public class WarehouseFromWebsiteViewModel
//        {
//            public int Amount { get; set; }
//            public string ProductCode { get; set; }
//        }
//    }
//}
