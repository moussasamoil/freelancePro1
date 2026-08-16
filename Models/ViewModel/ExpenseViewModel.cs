using System;
using System.ComponentModel.DataAnnotations;

namespace lotus_blue.Models.ViewModel
{
    public class ExpenseViewModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required]
        [Display(Name = "Amount")]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }

        [Required]
        [Display(Name = "Created Date")]
        [DataType(DataType.Date)]
        public DateTime CreatedDate { get; set; }

        // Additional properties if needed
    }
}
