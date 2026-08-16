using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace lotus_blue.Models
{
    public class EmployeeBonusRate
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string EmployeeId { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public virtual ApplicationUser Employee { get; set; }

        public decimal BonusPercentage { get; set; } = 0m;

        public decimal BonusProcessingPercentage { get; set; } = 0m;

        public decimal ProBonusPercentage { get; set; } = 0m;

        public decimal ProBonusProcessingPercentage { get; set; } = 0m;

        // Stored in the employee's local currency (Employee.Country, bridged to
        // Common.Countries via Common.EmployeeCountryToEnum). At runtime the cycle's
        // USD profit is converted to the same currency for comparison.
        public decimal ProThreshold { get; set; } = 0m;

        public bool IsBonusPanelHidden { get; set; } = false;

        public bool IsBonusAmountsRevealed { get; set; } = false;
    }
}
