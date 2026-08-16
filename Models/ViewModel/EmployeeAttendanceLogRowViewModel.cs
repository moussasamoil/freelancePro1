using System;

namespace lotus_blue.Models.ViewModel
{
    public class EmployeeAttendanceLogRowViewModel
    {
        public int Id { get; set; }

        public string UserId { get; set; } = "";

        public int? EmployeeId { get; set; }

        public string EmployeeName { get; set; } = "";

        public string EmployeeEmail { get; set; } = "";

        public DateTime CheckInAt { get; set; }

        public DateTime? CheckOutAt { get; set; }

        public string FaceImagePath { get; set; } = "";

        public string CheckOutFaceImagePath { get; set; } = "";

        public string CheckInIpAddress { get; set; } = "";

        public string CheckInLocation { get; set; } = "";

        public string CheckOutIpAddress { get; set; } = "";

        public string CheckOutLocation { get; set; } = "";

        public decimal? DeductionAmount { get; set; }

        public string DeductionReason { get; set; } = "";

        public string Notes { get; set; } = "";

        public string ShiftStartTimeText { get; set; } = "-";

        public string ShiftEndTimeText { get; set; } = "-";

        public int? LateMinutes { get; set; }

        public string LateReason { get; set; } = "";

        public decimal CalculatedDeductionAmount { get; set; }

        public decimal AdvanceAmount { get; set; }

        public decimal BonusAmount { get; set; }
    }
}