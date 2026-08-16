using System;
using System.Collections.Generic;

namespace lotus_blue.Models.ViewModel
{
    public class OrderEditChangesViewModel
    {
        public int OrderId { get; set; }
        public List<OrderEditChangeGroup> EditGroups { get; set; } = new();
    }

    public class OrderEditChangeGroup
    {
        public int EditNumber { get; set; }
        public DateTime EditedAt { get; set; }
        public string EditedBy { get; set; }
        public string? EditedById { get; set; }
        public List<OrderEditChange> Changes { get; set; } = new();
    }

    public class OrderEditChange
    {
        public string FieldKey { get; set; }
        public string FieldLabel { get; set; }
        public string? Before { get; set; }
        public string? After { get; set; }
    }
}
