using lotus_blue.Models;
using static lotus_blue.Models.Common;

namespace lotus_blue.Models.ViewModel
{
    public class PotentialOrderViewModel
    {
        public int Id { get; set; }
        public string CustomerName { get; set; }
        public Countries Country { get; set; }
        public string? ChatUrl { get; set; }
        public string StoreName { get; set; }
        public string? StoreLogoUrl { get; set; }
        public string? PhoneNumber { get; set; }
        public OrderSourceEnum OrderSource { get; set; }
        public PotentialOrderStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? LastEditedDate { get; set; }
        public string? EmployeeName { get; set; }
        public string? EmployeeImage { get; set; }
    }

    public class PotentialOrderListViewModel
    {
        public PaginationViewModel<PotentialOrderViewModel> PaginationViewModel { get; set; }
        public List<Countries> Countries { get; set; }
        public List<PotentialOrderStatus> Statuses { get; set; }
        public Dictionary<string, string> CountryImageUrls { get; set; }
    }
}
