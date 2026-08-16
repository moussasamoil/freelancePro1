using lotus_blue.Models;
using static lotus_blue.Models.Common;

namespace lotus_blue.Models.ViewModel
{
    public class LeadViewModel
    {
        public int Id { get; set; }
        public string SourceName { get; set; }
        public OrderSourceEnum OrderSource { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ChatUrl { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? EmployeeName { get; set; }
        public string? EmployeeImage { get; set; }
    }

    public class LeadListViewModel
    {
        public PaginationViewModel<LeadViewModel> PaginationViewModel { get; set; }
    }
}
