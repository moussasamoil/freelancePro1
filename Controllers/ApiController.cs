using lotus_blue.API;
using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.Models.ViewModel;
using lotus_blue.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using lotus_blue.Models.ViewModel;
using lotus_blue.API;
using System.Text;
using System.Net.Http;
using System.Net;
using lotus_blue.ApiToken;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using lotus_blue.Hubs;
using Microsoft.AspNetCore.SignalR;
using lotus_blue.OrderStatus;

namespace lotus_blue.Controllers
{
    public class ApiController : Controller
    {
        private readonly FileUploadService _fileUploadService;
        private readonly RESTAPI _restApi;
        private readonly ApplicationDbContext _context;
        private readonly GetCurrentTimeInIstanbul _timeService;
        private readonly IHubContext<OrderHub> _hubContext;

        public ApiController(FileUploadService fileUploadService,
            RESTAPI restApi,
            ApplicationDbContext context,
            GetCurrentTimeInIstanbul timeService,
            IHubContext<OrderHub> hubContext)
        {
            _fileUploadService = fileUploadService;
            _restApi = restApi;
            _context = context;
            _timeService = timeService;
            _hubContext = hubContext;

        }

        [HttpGet]
        public IActionResult CreateStoreData()
        {
            return View(new StoreDataViewModel());
        }

        [HttpPost]
        public IActionResult GetStoreName()
        {
            return View(new StoreDataViewModel());
        }


        [HttpPost("Api/Order/UpdateStatus")]
        [TokenAuthorize]
        public async Task<IActionResult> UpdateOrderStatus([FromBody] UpdateStatusViewModel requestModel)
        {
            if (requestModel == null || requestModel.OrderIds == null || !requestModel.OrderIds.Any())
            {
                return Json("No order IDs provided.");
            }

            var token = HttpContext.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            if (token == null)
            {
                return Json("Invalid token or user not found.");
            }

            Console.WriteLine(requestModel.OrderIds);

            // Bulk update the orders in a single query using Entity Framework Plus
            await _context.Orders
                .Where(o => o.ExternalOrderId.HasValue && requestModel.OrderIds.Contains(o.ExternalOrderId.Value))
                .UpdateFromQueryAsync(o => new Order
                {
                    OrderStatus = requestModel.NewStatus,
                    LastEditedDate = _timeService.GetIstanbulTimeWithOffset()
                });

            // Retrieve the updated orders to create OrderStatusHistory records
            var ordersToUpdate = await _context.Orders
                .Where(o => o.ExternalOrderId.HasValue && requestModel.OrderIds.Contains(o.ExternalOrderId.Value))
                .ToListAsync();

            if (!ordersToUpdate.Any())
            {
                return Json("No matching orders found.");
            }

            // Create the OrderStatusHistory records
            var orderHistories = ordersToUpdate.Select(order => new OrderStatusHistory
            {
                OrderId = order.Id,
                Reason = requestModel.Reason,
                Status = requestModel.NewStatus,
                CreatedAt = _timeService.GetIstanbulTimeWithOffset(),
                ApplicationUserId = token == "94235bfb5b626499" ? "898a99f9-47ee-493c-88b8-c220272a746a" : null
            }).ToList();

            // Add and save the OrderStatusHistory records to the database
            _context.OrderStatusHistories.AddRange(orderHistories);
            await _context.SaveChangesAsync();

            // Fetch the user names based on the ApplicationUserId
            var userIds = orderHistories.Select(oh => oh.ApplicationUserId).Distinct().ToList();
            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name);  // Fetching the Name from the user table

            // Prepare SignalR notifications in parallel
            var tasks = orderHistories.Select(async orderHistory =>
            {
                var order = ordersToUpdate.FirstOrDefault(o => o.Id == orderHistory.OrderId);
                if (order == null) return;

                // Get the UserName from the users dictionary
                var userName = orderHistory.ApplicationUserId != null && users.ContainsKey(orderHistory.ApplicationUserId)
                    ? users[orderHistory.ApplicationUserId]
                    : "Unknown";

                var orderStatusData = new
                {
                    OrderId = orderHistory.OrderId,
                    StatusHistoryId = orderHistory.Id,
                    Status = orderHistory.Status,
                    CreatedAt = orderHistory.CreatedAt,
                    ApplicationUserId = orderHistory.ApplicationUserId,
                    Reason = orderHistory.Reason,
                    UserName = userName,  // Using the fetched UserName
                    StatusPhrase = OrderStatusHelper.GetOrderStatusPhrase(requestModel.NewStatus),
                    ColorStyle = OrderStatusHelper.StatusColorMapping.GetValueOrDefault(requestModel.NewStatus, "")
                };

                // Broadcast update to relevant groups
                await _hubContext.Clients.Group("UsersExpectDelivery").SendAsync("OrderStatusUpdated", orderStatusData);
                await _hubContext.Clients.Group($"deliveryCompany_{order.DeliveryCompanyId}").SendAsync("OrderStatusUpdated", orderStatusData);
            });

            // Execute all SignalR broadcasts in parallel
            await Task.WhenAll(tasks);

            return Ok(new { success = true, message = "Order status updated successfully." });
        }



        // track shipment in lotus blue website  store
        [HttpGet("/Api/Order/ShipmentTracking/{orderId}")]
        [TokenAuthorize]
        public async Task<ActionResult<OrderShippingDetailsApiViewModel>> GetOrderStatusHistoryLotusblue(int orderId)
        {
            var order = _context.Orders.FirstOrDefault(o => o.Id == orderId);
            if (order == null)
            {
                return NotFound("Order not found.");
            }

            if (order.ManufacturingCompanyId != 1)
            {
                return BadRequest("Wrong order.");
            }

            var city = order.State; // Ensure this matches the actual property name
            var country = order.Country.ToString();
            List<WarehouseDetailViewModel> warehouseDetails = new List<WarehouseDetailViewModel>(); // Initialize as a list


            // Add fetched details from the local context to the list
            warehouseDetails.AddRange(_context.OrderWarehouses
                .Include(ow => ow.Warehouse)
                .Where(ow => ow.OrderId == orderId)
                .Select(ow => new WarehouseDetailViewModel
                {
                    WarehouseName = ow.Warehouse.Name,
                    ImageUrl = ow.Warehouse.MainWarehouse.ImageUrl,
                    Amount = ow.Amount
                }));


            var orderStatusHistory = _context.OrderStatusHistories
                .Where(o => o.OrderId == orderId)
                .Where(o => o.Status == OrderStatusEnum.طلب_جديد ||
                    o.Status == OrderStatusEnum.تم_التجهيز ||
                    o.Status == OrderStatusEnum.قيد_التوصيل ||
                    o.Status == OrderStatusEnum.تم_التسليم ||
                    o.Status == OrderStatusEnum.فشل_التسليم)
                .ToList()
                .GroupBy(o => o.Status)
                .Select(g => g.OrderByDescending(o => o.CreatedAt).FirstOrDefault())
                .OrderBy(o => o.CreatedAt)
                .Select(o => new OrderStatusHistoryViewModel
                {
                    CreatedAt = o.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    Status = o.Status.ToString(),
                    Reason = o.Reason
                })
                .ToList();

            if (!orderStatusHistory.Any())
            {
                return NotFound();
            }

            var result = new OrderShippingDetailsApiViewModel
            {
                WarehouseDetails = warehouseDetails, // This now correctly matches the expected list type
                OrderStatusHistory = orderStatusHistory,
                Country = country,
                City = city,
                OrderId = orderId,
            };

            return Ok(result);
        }

        // for sbs details page 
        [HttpGet("/WarehouseDetails/{externalOrderId}")]
        public async Task<IActionResult> GetWarehouseDetailsByExternalOrderId(int externalOrderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderWarehouses)
                    .ThenInclude(ow => ow.Warehouse)
                .FirstOrDefaultAsync(o => o.ExternalOrderId == externalOrderId);

            if (order == null)
            {
                return NotFound("Order not found.");
            }

            var warehouseDetails = order.OrderWarehouses.Select(ow => new
            {
                WarehouseName = ow.Warehouse.Name,
                Amount = ow.Amount
            }).ToList();

            return Ok(warehouseDetails);
        }




    }


}
