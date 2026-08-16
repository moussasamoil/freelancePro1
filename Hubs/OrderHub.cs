using lotus_blue.Data;
using lotus_blue.Models.ViewModel;
using lotus_blue.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;

using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Authorization;
using System.Runtime.CompilerServices;
namespace lotus_blue.Hubs
{
    public class OrderHub : Hub
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        public OrderHub(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public override async Task OnConnectedAsync()
        {
            // List of roles that should be added to the general group "UsersExpectDelivery"
            var includedUserRoles = new string[] { "Admin", "Accountant", "ExecutiveDirector" };
            bool isIncludedInGeneralGroup = includedUserRoles.Any(role => Context.User.IsInRole(role));

            if (isIncludedInGeneralGroup)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "UsersExpectDelivery");
            }

            // Roles that get live OrderPost notifications (home-page panels: الإبلاغات / التعديلات المطلوبة).
            var orderPostListenerRoles = new string[] { "Admin", "FollowUpDepartment", "ExecutiveDirector" };
            if (orderPostListenerRoles.Any(role => Context.User.IsInRole(role)))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "OrderPostListeners");
            }

            // Handle CallCenter and FollowUpDepartment based on ManufacturingCompany logic
            var manufacturingRelatedRoles = new string[] { "CallCenter", "FollowUpDepartment" };
            bool isManufacturingRelated = manufacturingRelatedRoles.Any(role => Context.User.IsInRole(role));

            if (isManufacturingRelated)
            {
                var userId = _userManager.GetUserId(Context.User);

                // Find all manufacturing companies the user can see
                var employeeManufacturingCompanies = await _context.EmployeeManufacturingCompany
                                            .Where(emc => emc.ApplicationUserId == userId && emc.CanSeeManufacturingCompany)
                                            .Select(emc => emc.ManufacturingCompanyId)
                                            .ToListAsync();

                // Add the user to a specific group for each manufacturing company they can see
                foreach (var manufacturingCompanyId in employeeManufacturingCompanies)
                {
                    var groupName = $"manufacturingCompany_{manufacturingCompanyId}";
                    await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
                }
            }

            // Handle delivery company-related roles
            var deliveryRelatedRoles = new string[] { "DeliveryCompany", "DeliveryRepresentative" };
            bool isDeliveryRelated = deliveryRelatedRoles.Any(role => Context.User.IsInRole(role));

            if (isDeliveryRelated)
            {
                var userId = _userManager.GetUserId(Context.User);
                var userDeliveryCompany = await _context.DeliveryCompanies
                                        .Where(dc => dc.UserId == userId)
                                        .Select(dc => dc.Id)
                                        .FirstOrDefaultAsync();

                // Check if a delivery company was found
                if (userDeliveryCompany != null)
                {
                    var deliveryCompanyGroup = $"deliveryCompany_{userDeliveryCompany}";
                    await Groups.AddToGroupAsync(Context.ConnectionId, deliveryCompanyGroup);
                }
            }

         

            await base.OnConnectedAsync();
        }






        // Notifies clients with sound when an order status is marked as failed
        public async Task NotifyClientsWithFailedOrderStatusSound()
        {
            await Clients.Group("UsersExpectDelivery").SendAsync("failedorderstatussound");

        }



        // Notifies admin clients with sound when an order status is marked as success
        public async Task NotifyWithDeliverdOrderStatusNotification(string orderId , string ordercountry)
        {
            await Clients.Group("UsersExpectDelivery").SendAsync("successorderstatusnotification", orderId , ordercountry);
        }

        // Notifies admin clients with sound when an order status is marked as failed
        public async Task NotifyWithFailedOrderStatusNotification(string orderId, string ordercountry)
        {
            await Clients.Group("UsersExpectDelivery").SendAsync("failedorderstatusnotification", orderId, ordercountry);
        }

        // Notifies admin clients with sound when an order status is marked as fixed
        public async Task NotifyWithFixedOrderStatusNotification(string orderId, string ordercountry)
        {
            await Clients.Group("UsersExpectDelivery").SendAsync("fixedorderstatusnotification", orderId, ordercountry);
        }

        // Broadcasts to admin-tier users that a new OrderPost was created for an order.
        // Drives the home-page header dummy panels (الإبلاغات / التعديلات المطلوبة) so they refresh
        // counts + cards in real time. Type is OrderPostType (0=Problem, 1=EditNote, 2=OrderNote).
        public async Task NotifyNewOrderPost(int orderId, int type)
        {
            await Clients.Group("UsersExpectDelivery").SendAsync("newOrderPost", orderId, type);
        }






    }
}

