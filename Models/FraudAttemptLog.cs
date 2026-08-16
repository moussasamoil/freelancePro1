using System;
using System.ComponentModel.DataAnnotations;

namespace lotus_blue.Models
{
    public class FraudAttemptLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string OrderTelephoneNumber { get; set; }

        public string? OrderSecondTelephoneNumber { get; set; }

        [Required]
        public string MatchedField { get; set; }

        [Required]
        public string MatchedDigits { get; set; }

        public int? ExistingOrderId { get; set; }

        public int? ManufacturingCompanyId { get; set; }

        public string? AttemptedByUserId { get; set; }

        public DateTime AttemptedAt { get; set; }

        public string? SubmittedCustomerName { get; set; }
        public string? SubmittedAddress { get; set; }
        public string? SubmittedNotes { get; set; }
        public string? SubmittedSourceName { get; set; }
        public string? SubmittedChatUrl { get; set; }
    }
}
