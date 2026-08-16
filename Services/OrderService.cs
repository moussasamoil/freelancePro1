using lotus_blue.API;
using lotus_blue.Data;
using lotus_blue.Hubs;
using lotus_blue.Models;
using lotus_blue.Models.ViewModel;
using lotus_blue.OrderStatus;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using static lotus_blue.Models.Common;

namespace lotus_blue.Services
{
    public class OrderService
    {
        // Assuming you have a context or a way to query your orders
        private readonly ApplicationDbContext _context;
        private readonly GetCurrentTimeInIstanbul _timeService;
        private readonly CurrencyExchangeService _currencyExchangeService; // Added CurrencyExchangeService
        private readonly RESTAPI _restapi;
        private readonly IHubContext<OrderHub> _hubContext;

        public OrderService(ApplicationDbContext context, GetCurrentTimeInIstanbul timeService, CurrencyExchangeService currencyExchangeService, RESTAPI restapi, IHubContext<OrderHub> hubContext)
        {
            _context = context;
            _timeService = timeService;
            _currencyExchangeService = currencyExchangeService;
            _restapi = restapi;
            _hubContext = hubContext;
        }

        // bot auto updae 
        public async Task UpdateOrderStatusesBasedOnDateAsync()
        {
            var currentDate = DateTime.Now; // Use your _timeService.GetIstanbulTimeWithOffset() if available

            // Get orders that are more than 2 days from today but not yet marked as "طلب جديد"
            var ordersToUpdate = await _context.Orders
                .Where(o => o.CreatedDate <= currentDate.AddDays(2) && o.CreatedDate >= currentDate && o.OrderStatus == OrderStatusEnum.الطلبات_المؤجلة)
                .ToListAsync();

            foreach (var order in ordersToUpdate)
            {
                // Update the status to "طلب جديد"
                order.OrderStatus = OrderStatusEnum.طلب_جديد;

                // Create a new OrderHistory record for each order that changes status
                var newHistory = new OrderStatusHistory
                {
                    CreatedAt = _timeService.GetIstanbulTimeWithOffset(),
                    Status = OrderStatusEnum.طلب_جديد,
                    Reason = null,
                    OrderId = order.Id,
                };

                _context.OrderStatusHistories.Add(newHistory);


                // Prepare SignalR data
                var orderStatusData = new
                {
                    newHistory.OrderId,
                    newHistory.Id,
                    newHistory.Status,
                    newHistory.CreatedAt,
                    newHistory.ApplicationUserId,
                    UserName = newHistory.User?.UserName ?? "Unknown",
                    StatusPhrase = OrderStatusHelper.GetOrderStatusPhrase(OrderStatusEnum.طلب_جديد),
                    ColorStyle = OrderStatusHelper.StatusColorMapping.GetValueOrDefault(OrderStatusEnum.طلب_جديد, "")
                };

                // Notify via SignalR
                await _hubContext.Clients.Group("UsersExpectDelivery").SendAsync("OrderStatusUpdated", orderStatusData);
                await _hubContext.Clients.Group($"deliveryCompany_{order.DeliveryCompanyId}").SendAsync("OrderStatusUpdated", orderStatusData);


                // If order is from Iraq, send API call
              ////  if (order.Country == Common.Countries.العراق)
              //  {
              //      // Prepare the data for the API call
              //      var externalOrderId = order.ExternalOrderId; // Assuming your order model has this property

              //      if (externalOrderId.HasValue)
              //      {
              //          // Construct the request object
              //          var updateStatusRequest = new UpdateStatusRequest
              //          {
              //              NewStatus = OrderStatusEnum.طلب_جديد, // Or any other status you want to update to
              //          };

              //          // Send the request to the external API
              //          var response = await _restapi.UpdateOrderStatusAsync(externalOrderId.Value, updateStatusRequest);

              //          if (!response.IsSuccessStatusCode)
              //          {
              //              Console.WriteLine("API call failed with status code: " + response.StatusCode);
              //              // Handle the failure as needed, e.g., log the error or throw an exception
              //          }
              //      }
              //  }
            }

            await _context.SaveChangesAsync();
        }

        public async Task UpdateOrderStatusesBasedOnStatus()
        {
            // Get orders with status "تم المعالجة"
            var ordersToUpdate = await _context.Orders
                .Where(o => o.OrderStatus == OrderStatusEnum.تم_المعالجة)
                .ToListAsync();

            foreach (var order in ordersToUpdate)
            {
                // Update the status to "طلب جديد"
                order.OrderStatus = OrderStatusEnum.طلب_جديد;

                // Create a new OrderHistory record for each order that changes status
                var newHistory = new OrderStatusHistory
                {
                    CreatedAt = _timeService.GetIstanbulTimeWithOffset(),
                    Status = OrderStatusEnum.طلب_جديد,
                    Reason = null,
                    OrderId = order.Id,
                    // Set other properties as needed, e.g., ApplicationUserId
                };

                _context.OrderStatusHistories.Add(newHistory);

                // Prepare SignalR data
                var orderStatusData = new
                {
                    newHistory.OrderId,
                    newHistory.Id,
                    newHistory.Status,
                    newHistory.CreatedAt,
                    newHistory.ApplicationUserId,
                    UserName = newHistory.User?.UserName ?? "Unknown",
                    StatusPhrase = OrderStatusHelper.GetOrderStatusPhrase(OrderStatusEnum.طلب_جديد),
                    ColorStyle = OrderStatusHelper.StatusColorMapping.GetValueOrDefault(OrderStatusEnum.طلب_جديد, "")
                };

                // Notify via SignalR
                await _hubContext.Clients.Group("UsersExpectDelivery").SendAsync("OrderStatusUpdated", orderStatusData);
                await _hubContext.Clients.Group($"deliveryCompany_{order.DeliveryCompanyId}").SendAsync("OrderStatusUpdated", orderStatusData);

                // If order is from Iraq, send API call
             //   if (order.Country == Common.Countries.العراق)
                //{
                //    // Prepare the data for the API call
                //    var externalOrderId = order.ExternalOrderId; // Assuming your order model has this property

                //    if (externalOrderId.HasValue)
                //    {
                //        // Construct the request object
                //        var updateStatusRequest = new UpdateStatusRequest
                //        {
                //            NewStatus = OrderStatusEnum.طلب_جديد, // Or any other status you want to update to
                //        };

                //        // Send the request to the external API
                //        var response = await _restapi.UpdateOrderStatusAsync(externalOrderId.Value, updateStatusRequest);

                //        if (!response.IsSuccessStatusCode)
                //        {
                //            Console.WriteLine("API call failed with status code: " + response.StatusCode);
                //            // Handle the failure as needed, e.g., log the error or throw an exception
                //        }
                //    }
                //}
            }

            await _context.SaveChangesAsync();
        }

        public async Task UpdateOrderStatuseFromcanceledtofaildBasedOnStatus()
        {
            // Get orders with status "تم الإلغاء"
            var ordersToUpdate = await _context.Orders
                .Where(o => o.OrderStatus == OrderStatusEnum.تم_الإلغاء)
                .ToListAsync();

            foreach (var order in ordersToUpdate)
            {
                // Update the status to "فشل التسليم"
                order.OrderStatus = OrderStatusEnum.فشل_التسليم;

                // Create a new OrderHistory record for each order that changes status
                var newHistory = new OrderStatusHistory
                {
                    CreatedAt = _timeService.GetIstanbulTimeWithOffset(),
                    Status = OrderStatusEnum.فشل_التسليم,
                    Reason = null,
                    OrderId = order.Id,
                    // Set other properties as needed, e.g., ApplicationUserId
                };

                _context.OrderStatusHistories.Add(newHistory);

                // Prepare SignalR data
                var orderStatusData = new
                {
                    newHistory.OrderId,
                    newHistory.Id,
                    newHistory.Status,
                    newHistory.CreatedAt,
                    newHistory.ApplicationUserId,
                    UserName = newHistory.User?.UserName ?? "Unknown",
                    StatusPhrase = OrderStatusHelper.GetOrderStatusPhrase(OrderStatusEnum.طلب_جديد),
                    ColorStyle = OrderStatusHelper.StatusColorMapping.GetValueOrDefault(OrderStatusEnum.طلب_جديد, "")
                };

                // Notify via SignalR
                await _hubContext.Clients.Group("UsersExpectDelivery").SendAsync("OrderStatusUpdated", orderStatusData);
                await _hubContext.Clients.Group($"deliveryCompany_{order.DeliveryCompanyId}").SendAsync("OrderStatusUpdated", orderStatusData);

                //// If order is from Iraq, send API call
                //if (order.Country == Common.Countries.العراق)
                //{
                //    // Prepare the data for the API call
                //    var externalOrderId = order.ExternalOrderId; // Assuming your order model has this property

                //    if (externalOrderId.HasValue)
                //    {
                //        // Construct the request object
                //        var updateStatusRequest = new UpdateStatusRequest
                //        {
                //            NewStatus = OrderStatusEnum.فشل_التسليم, // Or any other status you want to update to
                //        };

                //        // Send the request to the external API
                //        var response = await _restapi.UpdateOrderStatusAsync(externalOrderId.Value, updateStatusRequest);

                //        if (!response.IsSuccessStatusCode)
                //        {
                //            Console.WriteLine("API call failed with status code: " + response.StatusCode);
                //            // Handle the failure as needed, e.g., log the error or throw an exception
                //        }
                //    }
                //}
            }

            await _context.SaveChangesAsync();
        }
        // end of bot auto update

          
        // calcualte order prices with out delviery comapny
        public async Task<decimal> CalculateTotalPriceInUSDForOrdersAsync(List<int> orderIds)
        {
            // Fetch the totals by country
            var countryTotals = await _context.Orders
                .Where(order => orderIds.Contains(order.Id))
                .GroupBy(order => order.Country)
                .Select(group => new
                {
                    CountryName = group.Key.ToString(),
                    TotalPrice = group.Sum(order => order.TotalPrice)
                })
                .ToListAsync();

            // Convert the totals to USD in memory
            var grandTotalInUSD = countryTotals
                .AsParallel() // AsParallel for potential parallel processing
                .Select(countryTotal =>
                {
                    try
                    {
                        return _currencyExchangeService.ConvertToUSD(countryTotal.TotalPrice, countryTotal.CountryName);
                    }
                    catch (Exception ex)
                    {
                        // Log or handle the exception as necessary
                        // Consider how to handle conversion failures, possibly by skipping or setting a default value
                        return 0m; // Assuming you want to continue with other conversions even if one fails
                    }
                })
                .Sum(); // Sum up the converted amounts

            return grandTotalInUSD;
        }

        // 
        public decimal GetEmployeeBonusTotal(string userId)
        {
            // Retrieve the user; return 0 if not found
            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return 0;

            // Get all orders for the user that qualify for bonuses and are not yet paid
            var qualifyingOrders = _context.Orders
              .AsNoTracking()
              .Where(o => o.IsBonus && !o.IsBonusPaidForEmployee &&
                          (o.OrderStatus == OrderStatusEnum.تم_التسليم ||
                           o.OrderStatus == OrderStatusEnum.تم_الدفع ||
                           o.OrderStatus == OrderStatusEnum.تم_تحديث_الرصيد) &&
                          o.ApplicationUserId == userId)
              .Select(o => new { o.TotalPrice, o.Country })
              .ToList();

            // Load all bonus configurations into memory
            var bonusConfigurations = _context.OrderBonusConfigurations.ToList();

            // Calculate the total bonus amount in-memory
            var totalBonusAmount = qualifyingOrders.Sum(order =>
            {
                var applicableBonuses = bonusConfigurations
                    .Where(bc => bc.Country == order.Country)
                    .ToList();

                return applicableBonuses.Sum(bonusConfig =>
                {
                    // Calculate how many times the threshold is met
                    int multiplier = (int)(order.TotalPrice / bonusConfig.OrderThreshold);

                    if (bonusConfig.FlatBonusAmount > 0)
                    {
                        // Apply the bonus for each threshold met
                        return multiplier * bonusConfig.FlatBonusAmount;
                    }
                    else if (bonusConfig.PercentageBonus.HasValue)
                    {
                        // Apply percentage-based bonus for each threshold
                        return multiplier * (order.TotalPrice * bonusConfig.PercentageBonus.Value);
                    }
                    return 0;
                });
            });

            return totalBonusAmount;
        }

        // calcualte order prices with out delviery comapny

        public class TotalPriceResult
        {
            public decimal TotalPriceUSD { get; set; }
            public decimal TotalPriceLocalCurrency { get; set; }
        }
        // get number of orders 
        public async Task<int> GetNumberOfFixedOrdersAsync(
         IQueryable<Order> filteredOrdersQuery,
         DateTime? startDay,
         DateTime? endDay)
        {
            var now = _timeService.GetIstanbulTimeWithOffset();

            if (startDay == null && endDay == null)
            {
                if (now.TimeOfDay < TimeSpan.FromHours(10))
                {
                    startDay = now.Date.AddDays(-1).AddHours(10); // Yesterday at 10 AM
                    endDay = now.Date.AddHours(10); // Today at 10 AM
                }
                else
                {
                    startDay = now.Date.AddHours(10); // Today at 10 AM
                    endDay = now.Date.AddDays(1).AddHours(10); // Tomorrow at 10 AM
                }
            }
            else
            {
                // Provided startDay and endDay are used, adjusted to 10 AM
                startDay = startDay?.Date.AddHours(10);
                endDay = endDay?.Date.AddHours(10);
            }


            return await filteredOrdersQuery
                .Where(o => o.FixedOrderDate >= startDay && o.FixedOrderDate <= endDay)
                .CountAsync();
        }


        public async Task<Dictionary<object, TotalPriceResult>> CalculateTotalPricesForOrdersWithOutDeliveryCompanyAsync(List<Order> allOrders)
        {
            var deliveryCompanyIds = allOrders.Select(o => o.DeliveryCompanyId).Distinct().ToList();
            var deliveryCompanyPrices = await _context.DeliveryCompanyPrices
                .Where(d => deliveryCompanyIds.Contains(d.DeliveryCompanyId))
                .ToListAsync();

            return allOrders.GroupBy(o => new { o.ManufacturingCompanyId, o.Country, o.OrderSource })
                .ToDictionary(
                    g => (object)g.Key,
                    g => new TotalPriceResult
                    {
                        TotalPriceUSD = g.Sum(o =>
                        {
                            var deliveryPrice = deliveryCompanyPrices.FirstOrDefault(d =>
                                d.DeliveryCompanyId == o.DeliveryCompanyId &&
                                d.Country == o.Country &&
                                d.City == o.State)?.Price ?? 0M;
                            var orderPriceMinusDelivery = o.TotalPrice - deliveryPrice;
                            return _currencyExchangeService.ConvertToUSD(orderPriceMinusDelivery, o.Country.ToString());
                        }),
                        TotalPriceLocalCurrency = g.Sum(o =>
                        {
                            var deliveryPrice = deliveryCompanyPrices.FirstOrDefault(d =>
                                d.DeliveryCompanyId == o.DeliveryCompanyId &&
                                d.Country == o.Country &&
                                d.City == o.State)?.Price ?? 0M;
                            return o.TotalPrice - deliveryPrice;
                        })
                    }
                );
        }

    }
}
