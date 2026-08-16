using System.Collections.Generic;
using lotus_blue.Services.Bonus;

namespace lotus_blue.Models.ViewModel
{
    public class EmployeeBonusIndexViewModel
    {
        // Filter state echoed back to the view.
        public string SelectedEmployeeId { get; set; }
        public System.DateTime? FilterStart { get; set; }
        public System.DateTime? FilterEnd { get; set; }
        public bool IsDateFiltered { get; set; }

        // Window the cards are summing across. Used to build the card → /Home/Index
        // navigation URL so the home page filters to the same CreatedDate range.
        public System.DateTime WindowStart { get; set; }
        public System.DateTime WindowEnd { get; set; }

        // Stat cards aggregated across all employees (or the single selected employee).
        public List<BonusStatCard> Stats { get; set; } = new List<BonusStatCard>();

        // Pro-progress card (only set when filtering by employee).
        public ProProgressCard ProProgress { get; set; }

        // Employee table rows (rate config + pay button).
        public List<EmployeeBonusIndexRow> Rows { get; set; } = new List<EmployeeBonusIndexRow>();

        // Dropdown source for the employee filter.
        public List<EmployeeOption> EmployeeOptions { get; set; } = new List<EmployeeOption>();
    }

    public class BonusStatCard
    {
        public string Label { get; set; }
        public string ColorHex { get; set; }
        public int OrderCount { get; set; }
        // Count for today's shift (no filter) or the filtered date range. Shown as sub-label.
        public int? ContextCount { get; set; }
        // Only populated for classes that generate bonus dollars (Success + ProcessedForOthers).
        public decimal? AmountUsd { get; set; }
        // Raw profit (USD) underlying the bonus amount. Only set for the same classes.
        public decimal? ProfitUsd { get; set; }
        // Exact order IDs underlying OrderCount. The view emits them as data-order-ids
        // so a click on the card opens /Home/Index?orderIds=… showing those exact rows.
        public System.Collections.Generic.List<int> OrderIds { get; set; } = new();
        // Navigation IDs: union of OrderIds + orders delegated to this employee (DelegateEmployeeId).
        // Null when same as OrderIds (no extras). View prefers this for click-through navigation.
        public System.Collections.Generic.List<int> NavOrderIds { get; set; }
        // Net profit (USD) for cards that show profit but carry no bonus amount (failure/incomplete/delayed).
        public decimal? NetProfitUsd { get; set; }
    }

    public class ProProgressCard
    {
        public decimal CycleProfitUsd { get; set; }
        public decimal ThresholdUsd { get; set; }
        public decimal RemainingUsd { get; set; }
        public bool ThresholdReached { get; set; }
    }

    public class EmployeeBonusIndexRow
    {
        public string EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public decimal BonusPercentage { get; set; }
        public decimal BonusProcessingPercentage { get; set; }
        public decimal ProBonusPercentage { get; set; }
        public decimal ProBonusProcessingPercentage { get; set; }
        public decimal ProThreshold { get; set; }
        // Total payable amount for this employee. Normally from closed cycles only.
        // When IsEarlyPay is true, this is the CURRENT cycle's amount — populated this way
        // ONLY when the employee has nothing owed from any closed cycle (so early pay is safe).
        public decimal PayableUsd { get; set; }
        public int PayableOrderCount { get; set; }
        public bool IsEarlyPay { get; set; }
        public bool IsBonusPanelHidden { get; set; }
        public Common.Countries? Country { get; set; }
        // Union of all bonus-classifiable order IDs for THIS employee in the active window.
        // Used by the row click handler to open /Home/Index?orderIds=… aggregated across
        // every stat card for that employee.
        public System.Collections.Generic.List<int> AllBonusOrderIds { get; set; } = new();
    }

    public class EmployeeOption
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
}
