using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using lotus_blue.Models; // Assuming your user and employee models are here
using System.Threading.Tasks;
using System.Linq;
using lotus_blue.Data;
using lotus_blue.Models.ViewModel;

namespace lotus_blue.Components
{
    public class SidebarViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SidebarViewComponent(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            if (user == null)
            {
                // Handle the case when the user is not found
                return Content("User not found");
            }

            var employee = _context.Employees.FirstOrDefault(e => e.ApplicationUserId == user.Id);
            var deliveryCompanyMember = _context.DeliveryCompanies.FirstOrDefault(d => d.UserId == user.Id);

            var viewModel = new SidebarViewModel();

            if (employee != null)
            {
                viewModel.UserName = employee.Name;
                viewModel.UserImage = employee.ImageUrl;
                viewModel.UserId = employee.ApplicationUserId;

            }
            else if (deliveryCompanyMember != null)
            {
                viewModel.UserName = deliveryCompanyMember.Name;
                viewModel.UserImage = deliveryCompanyMember.ImageUrl;
            }

            return View("~/Views/Shared/Components/Sidebar/Sidebar.cshtml", viewModel);
        }
    }
}
