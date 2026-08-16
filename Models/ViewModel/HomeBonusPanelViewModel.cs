using System;
using static lotus_blue.Models.Common;

namespace lotus_blue.Models.ViewModel
{
    public class HomeBonusPanelViewModel
    {
        // The employee these stats belong to. Used to build the card → /Home/Index
        // navigation URL with creator-or-fixer scoping.
        public string EmployeeId { get; set; }

        // Alternate currency shown alongside USD on the bonus cards.
        // Null country falls back to TL (the original behavior). When set, the
        // user's country is used and the 3-letter code (e.g. IQD) is shown.
        public Countries? AltCurrencyCountry { get; set; }
        public string AltCurrencyCode { get; set; } = "TL";
        public string AltCurrencySubtitle { get; set; } = "(ما يقابله بالليرة التركية)";

        // True when an unpaid pre-payment success order blocks the counter from
        // advancing into the current cycle (see BonusHomePanelService for rules).
        public bool Frozen { get; set; }

        public DateTime DisplayStart { get; set; }
        public DateTime DisplayEnd { get; set; }

        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public int CollectionCount { get; set; }
        public int IncompleteCount { get; set; }
        public int DelayedCount { get; set; }
        public int ProcessedForOthersCount { get; set; }

        // Today's shift counts (10:30–10:30 Istanbul). Shown as the "اليوم" sub-counter
        // on each card alongside the cumulative count above.
        public int SuccessTodayCount { get; set; }
        public int FailureTodayCount { get; set; }
        public int CollectionTodayCount { get; set; }
        public int IncompleteTodayCount { get; set; }
        public int DelayedTodayCount { get; set; }
        public int ProcessedForOthersTodayCount { get; set; }

        // Order IDs that compose each card's count. Emitted as data-order-ids on the
        // card markup so clicking it can navigate to /Home/Index?orderIds=...
        // (the same orders the user is staring at).
        public System.Collections.Generic.List<int> SuccessOrderIds { get; set; } = new();
        public System.Collections.Generic.List<int> FailureOrderIds { get; set; } = new();
        public System.Collections.Generic.List<int> CollectionOrderIds { get; set; } = new();
        public System.Collections.Generic.List<int> IncompleteOrderIds { get; set; } = new();
        public System.Collections.Generic.List<int> DelayedOrderIds { get; set; } = new();
        public System.Collections.Generic.List<int> ProcessedForOthersOrderIds { get; set; } = new();

        // Failure/Incomplete orders delegated TO this employee (DelegateEmployeeId = this user).
        // Unioned into the nav IDs for those cards so clicking navigates to all relevant orders.
        // Never included in counts.
        public System.Collections.Generic.List<int> DelegatedFailureOrderIds { get; set; } = new();
        public System.Collections.Generic.List<int> DelegatedIncompleteOrderIds { get; set; } = new();

        public decimal SuccessAmountUsd { get; set; }
        public decimal ProcessedForOthersAmountUsd { get; set; }

        // Raw profit (USD) underlying the bonus amounts above.
        public decimal SuccessProfitUsd { get; set; }
        public decimal ProcessedForOthersProfitUsd { get; set; }

        // Collection-class orders: raw profit (USD) currently in the pipeline, plus
        // the projected bonus payout if those orders eventually deliver.
        public decimal CollectionProfitUsd { get; set; }
        public decimal CollectionProjectedBonusUsd { get; set; }

        // Net profit (TotalPrice - DeliveryPrice, USD) for orders in each non-bonus class.
        public decimal FailureProfitUsd { get; set; }
        public decimal IncompleteProfitUsd { get; set; }
        public decimal DelayedProfitUsd { get; set; }

        public bool HasProConfigured { get; set; }
        public decimal ProRemainingUsd { get; set; }
        public decimal ProThresholdUsd { get; set; }
        public decimal ProCurrentProfitUsd { get; set; }

        // Per-user persisted preference: true = digits visible, false = digits shown as stars.
        public bool AmountsRevealed { get; set; }
    }
}
