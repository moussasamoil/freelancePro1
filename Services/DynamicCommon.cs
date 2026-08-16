using lotus_blue.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
namespace lotus_blue.Services
{
    public class DynamicCommon
    {
        private readonly ApplicationDbContext _context;

        public DynamicCommon(ApplicationDbContext context)
        {
            _context = context;
        }

        // This method retrieves the User's Name based on the ApplicationUserId
        public async Task<string> GetUserNameByIdAsync(string applicationUserId)
        {
            if (string.IsNullOrEmpty(applicationUserId))
            {
                return "Unknown";
            }

            var user = await _context.Users
                .Where(u => u.Id == applicationUserId)
                .Select(u => u.Name)  // Directly select the Name to avoid loading unnecessary data
                .FirstOrDefaultAsync();

            return user ?? "Unknown";
        }

        public async Task<string> GetEmployeeImageByNameAsync(string UserId)
        {
            var EmployeeImage = await _context.Employees
                .Where(o => o.ApplicationUserId == UserId)
                .Select(o => o.ImageUrl)
                .FirstOrDefaultAsync();

            return "/" + EmployeeImage;
        }





        public async Task<string> GetImageForStore(int Id)
        {
            var image = await _context.ManufacturingCompanies
                .Where(o => o.Id == Id)
                .Select(o => o.ImageUrl)
                .FirstOrDefaultAsync();

            return "/" + image;
        }

        public async Task<string> GetSecondImageForStore(int  Id)
        {
            var image = await _context.ManufacturingCompanies
                .Where(o => o.Id == Id)
                .Select(o => o.ImageUrl2)
                .FirstOrDefaultAsync();

            return  image;
        }
     


    }
}
