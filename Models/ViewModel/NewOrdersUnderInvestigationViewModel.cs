using System.Collections.Generic;

namespace lotus_blue.Models.ViewModel
{
    public class NewOrdersUnderInvestigationViewModel
    {
        public List<OrderViewModel> Orders { get; set; } = new List<OrderViewModel>();

        public int TotalCount { get; set; }
    }
}
