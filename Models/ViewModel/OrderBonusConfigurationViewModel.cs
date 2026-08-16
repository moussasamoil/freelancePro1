using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace lotus_blue.Models.ViewModel
{
    public class OrderBonusConfigurationViewModel
    {
        public int Id { get; set; }

        [DisplayName("السعر الأجمالي للطلب")]
        [Required]
        public decimal OrderThreshold { get; set; }

        [DisplayName("العمولة لمبلغ الطلب")]
        [Required]
        public decimal FlatBonusAmount { get; set; }

        [DisplayName("عمولة لجميع الطلبات")]
        public decimal? PercentageBonus { get; set; }

        [Required(ErrorMessage = "هذا الحقل مطلوب ")]
        public Common.Countries Country { get; set; }

        public int? EmployeeId { get; set; }

        [DisplayName("الموظف")]
        public string? EmployeeName { get; set; }

    }
}