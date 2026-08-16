using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class FixRecoveredAutoAbsentWrongCheckout : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.EmployeeAttendanceLogs_BeforeFix_WrongCheckout_20260611', 'U') IS NULL
BEGIN
    SELECT *
    INTO dbo.EmployeeAttendanceLogs_BeforeFix_WrongCheckout_20260611
    FROM dbo.EmployeeAttendanceLogs;
END;

IF OBJECT_ID('tempdb..#BadRows') IS NOT NULL
    DROP TABLE #BadRows;

SELECT
    l.*,
    e.Salary,
    s.ShiftStartTime,
    s.ShiftEndTime,
    DayDeduction =
        CASE
            WHEN e.Salary > 0
            THEN ROUND(e.Salary / DAY(EOMONTH(l.CheckInAt)), 2)
            ELSE 0
        END
INTO #BadRows
FROM dbo.EmployeeAttendanceLogs l
INNER JOIN dbo.Employees e
    ON e.Id = l.EmployeeId
LEFT JOIN dbo.EmployeeWorkShifts s
    ON s.Id = l.ShiftId
WHERE l.EmployeeId IS NOT NULL
  AND l.CheckOutAt IS NOT NULL
  AND l.ShiftStartAt IS NOT NULL
  AND l.ShiftEndAt IS NOT NULL
  AND l.CheckOutAt > DATEADD(MINUTE, 30, l.ShiftEndAt)
  AND (
        ISNULL(l.Notes, '') LIKE '%AutoAbsent%'
        OR ISNULL(l.DeductionReason, '') LIKE N'%غياب%'
        OR (
            DATEDIFF(MINUTE, l.ShiftStartAt, l.CheckInAt) = 0
            AND ISNULL(l.DeductionReason, '') = N'لا يوجد خصم'
        )
      );

;WITH NewShiftForWrongCheckout AS
(
    SELECT
        b.Id AS OldLogId,
        b.UserId,
        b.EmployeeId,
        b.EmployeeEmail,
        b.EmployeeName,
        b.CheckOutAt AS NewCheckInAt,
        b.CheckOutFaceImagePath,
        b.FaceImagePath,
        b.CheckOutIpAddress,
        b.CheckInIpAddress,
        b.CheckOutLocation,
        b.CheckInLocation,
        b.ShiftId,
        b.ShiftStartTime,
        b.ShiftEndTime,
        b.Salary,
        b.DayDeduction,

        NewShiftStartAt =
            DATEADD(
                SECOND,
                DATEDIFF(SECOND, CAST('00:00:00' AS time), CAST(b.ShiftStartTime AS time)),
                DATEADD(
                    DAY,
                    CASE
                        WHEN CAST(b.ShiftEndTime AS time) <= CAST(b.ShiftStartTime AS time)
                         AND CAST(b.CheckOutAt AS time) <= CAST(b.ShiftEndTime AS time)
                        THEN -1 ELSE 0
                    END,
                    CAST(CAST(b.CheckOutAt AS date) AS datetime2)
                )
            )
    FROM #BadRows b
    WHERE b.ShiftStartTime IS NOT NULL
      AND b.ShiftEndTime IS NOT NULL
),
PreparedNewRows AS
(
    SELECT
        n.*,
        NewShiftEndAt =
            DATEADD(
                DAY,
                CASE
                    WHEN CAST(n.ShiftEndTime AS time) <= CAST(n.ShiftStartTime AS time)
                    THEN 1 ELSE 0
                END,
                DATEADD(
                    SECOND,
                    DATEDIFF(SECOND, CAST('00:00:00' AS time), CAST(n.ShiftEndTime AS time)),
                    CAST(CAST(n.NewShiftStartAt AS date) AS datetime2)
                )
            )
    FROM NewShiftForWrongCheckout n
)
INSERT INTO dbo.EmployeeAttendanceLogs
(
    UserId,
    EmployeeId,
    EmployeeEmail,
    EmployeeName,
    CheckInAt,
    FaceImagePath,
    CheckInIpAddress,
    CheckInLocation,
    DeductionAmount,
    DeductionReason,
    Notes,
    CreatedAt,
    UpdatedAt,
    ShiftId,
    ShiftStartAt,
    ShiftEndAt
)
SELECT
    p.UserId,
    p.EmployeeId,
    p.EmployeeEmail,
    p.EmployeeName,
    p.NewCheckInAt,
    CASE
        WHEN ISNULL(p.CheckOutFaceImagePath, '') <> '' THEN p.CheckOutFaceImagePath
        ELSE ISNULL(p.FaceImagePath, '')
    END,
    CASE
        WHEN ISNULL(p.CheckOutIpAddress, '') <> '' THEN p.CheckOutIpAddress
        ELSE ISNULL(p.CheckInIpAddress, '')
    END,
    CASE
        WHEN ISNULL(p.CheckOutLocation, '') <> '' THEN p.CheckOutLocation
        ELSE ISNULL(p.CheckInLocation, '')
    END,
    0,
    N'لا يوجد خصم',
    N'[RecoveredFromWrongPreviousCheckout]',
    p.NewCheckInAt,
    GETDATE(),
    p.ShiftId,
    p.NewShiftStartAt,
    p.NewShiftEndAt
FROM PreparedNewRows p
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.EmployeeAttendanceLogs existingLog
    WHERE existingLog.EmployeeId = p.EmployeeId
      AND existingLog.ShiftStartAt = p.NewShiftStartAt
      AND existingLog.Id <> p.OldLogId
      AND (existingLog.Notes IS NULL OR existingLog.Notes NOT LIKE '%[AttendanceDeleted]%')
);

UPDATE l
SET
    l.CheckOutAt = NULL,
    l.CheckOutFaceImagePath = '',
    l.CheckOutIpAddress = '',
    l.CheckOutLocation = '',

    l.FaceImagePath = '',
    l.CheckInIpAddress = '',
    l.CheckInLocation = '',

    l.DeductionAmount = b.DayDeduction,
    l.DeductionReason = N'غياب',
    l.Notes =
        CASE
            WHEN ISNULL(l.Notes, '') LIKE '%AutoAbsent%'
            THEN CONCAT(ISNULL(l.Notes, ''), ' [WrongCheckoutFixed]')
            ELSE CONCAT(ISNULL(l.Notes, ''), ' AutoAbsent [WrongCheckoutFixed]')
        END,
    l.UpdatedAt = GETDATE()
FROM dbo.EmployeeAttendanceLogs l
INNER JOIN #BadRows b
    ON b.Id = l.Id;

DROP TABLE #BadRows;
");
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
