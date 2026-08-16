using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace lotus_blue.Models.AppViewModel
{
    public class AppExpensesViewModel
    {
        public int Id { get; set; }

        [DisplayName(" الوصف")]
        [Required(ErrorMessage = "الوصف مطلوب.")]
        [StringLength(255, ErrorMessage = "يجب أن يكون الوصف على الأكثر 255 حرفًا.")]
        public string Description { get; set; }

        [DisplayName(" المبلغ")]
        [Required(ErrorMessage = "المبلغ مطلوب.")]
        public string Amount { get; set; }

        [DisplayName("تاريخ الأضافة")]
        public string DateAdded { get; set; }  // Setting default value to current date and time
    }


    public class AppCreateExpenseViewModel
    {
        [DisplayName(" الوصف")]
        [Required(ErrorMessage = "الوصف مطلوب.")]
        [StringLength(255, ErrorMessage = "يجب أن يكون الوصف على الأكثر 255 حرفًا.")]
        public string Description { get; set; }

        [DisplayName(" المبلغ")]
        [Required(ErrorMessage = "المبلغ مطلوب.")]
        public decimal Amount { get; set; }

     
    }
}
