using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace lotus_blue.Models
{
    public class Expense
    {
        public int Id { get; set; }

        [DisplayName(" الوصف")]
        [Required(ErrorMessage = "الوصف مطلوب.")]
        [StringLength(255, ErrorMessage = "يجب أن يكون الوصف على الأكثر 255 حرفًا.")]
        public string Description { get; set; }

        [DisplayName(" المبلغ")]
        [Required(ErrorMessage = "المبلغ مطلوب.")]
        public decimal Amount { get; set; }

        [DisplayName("تاريخ الأضافة")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;  // Setting default value to current date and time
    }
}
