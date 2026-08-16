using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace lotus_blue.Migrations
{
    public partial class RecalculateExistingAttendanceLogs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.EmployeeAttendanceLogs_BeforeRefresh_20260611', 'U') IS NULL
BEGIN
    SELECT *
    INTO dbo.EmployeeAttendanceLogs_BeforeRefresh_20260611
    FROM dbo.EmployeeAttendanceLogs;
END;

;WITH CandidateShifts AS
(
    SELECT
        l.Id AS LogId,
        l.EmployeeId,
        l.EmployeeName,
        l.CheckInAt,
        s.Id AS ShiftId,
        s.ShiftStartTime,
        s.ShiftEndTime,
        s.CreatedAt AS ShiftCreatedAt,
        e.Salary,

        ss.ShiftStartAt,
        se.ShiftEndAt,

        ROW_NUMBER() OVER
        (
            PARTITION BY l.Id
            ORDER BY
                CASE
                    WHEN l.CheckInAt >= DATEADD(MINUTE, -30, ss.ShiftStartAt)
                     AND l.CheckInAt <= DATEADD(MINUTE, 30, se.ShiftEndAt)
                    THEN 0 ELSE 1
                END,
                CASE
                    WHEN CAST(s.CreatedAt AS date) <= CAST(l.CheckInAt AS date)
                    THEN 0 ELSE 1
                END,
                s.CreatedAt DESC,
                s.Id DESC
        ) AS rn
    FROM dbo.EmployeeAttendanceLogs l
    INNER JOIN dbo.Employees e
        ON e.Id = l.EmployeeId
    INNER JOIN dbo.EmployeeWorkShifts s
        ON s.EmployeeId = l.EmployeeId
    CROSS APPLY
    (
        SELECT BaseDate =
            DATEADD(
                DAY,
                CASE
                    WHEN CAST(s.ShiftEndTime AS time) <= CAST(s.ShiftStartTime AS time)
                     AND CAST(l.CheckInAt AS time) <= CAST(s.ShiftEndTime AS time)
                    THEN -1 ELSE 0
                END,
                CAST(CAST(l.CheckInAt AS date) AS datetime2)
            )
    ) bd
    CROSS APPLY
    (
        SELECT ShiftStartAt =
            DATEADD(
                SECOND,
                DATEDIFF(SECOND, CAST('00:00:00' AS time), CAST(s.ShiftStartTime AS time)),
                bd.BaseDate
            )
    ) ss
    CROSS APPLY
    (
        SELECT ShiftEndAt =
            DATEADD(
                DAY,
                CASE
                    WHEN CAST(s.ShiftEndTime AS time) <= CAST(s.ShiftStartTime AS time)
                    THEN 1 ELSE 0
                END,
                DATEADD(
                    SECOND,
                    DATEDIFF(SECOND, CAST('00:00:00' AS time), CAST(s.ShiftEndTime AS time)),
                    bd.BaseDate
                )
            )
    ) se
    WHERE l.EmployeeId IS NOT NULL
      AND (l.Notes IS NULL OR l.Notes NOT LIKE '%[AttendanceDeleted]%')
),
ChosenShift AS
(
    SELECT *
    FROM CandidateShifts
    WHERE rn = 1
)
UPDATE l
SET
    l.ShiftId = c.ShiftId,
    l.ShiftStartAt = c.ShiftStartAt,
    l.ShiftEndAt = c.ShiftEndAt
FROM dbo.EmployeeAttendanceLogs l
INNER JOIN ChosenShift c
    ON c.LogId = l.Id;

;WITH BaseCalc AS
(
    SELECT
        l.Id,
        l.EmployeeId,
        l.EmployeeName,
        l.CheckInAt,
        l.CheckOutAt,
        l.FaceImagePath,
        l.CheckInIpAddress,
        l.CheckInLocation,
        l.CheckOutFaceImagePath,
        l.CheckOutIpAddress,
        l.CheckOutLocation,
        l.Notes,
        l.ShiftStartAt,
        l.ShiftEndAt,
        e.Salary,

        CheckInMinute =
            DATEADD(MINUTE, DATEDIFF(MINUTE, CAST('2000-01-01' AS datetime2), l.CheckInAt), CAST('2000-01-01' AS datetime2)),

        CheckOutMinute =
            CASE
                WHEN l.CheckOutAt IS NULL THEN NULL
                ELSE DATEADD(MINUTE, DATEDIFF(MINUTE, CAST('2000-01-01' AS datetime2), l.CheckOutAt), CAST('2000-01-01' AS datetime2))
            END,

        ShiftMinutes =
            NULLIF(DATEDIFF(MINUTE, l.ShiftStartAt, l.ShiftEndAt), 0),

        DaysInMonth =
            DAY(EOMONTH(l.CheckInAt)),

        IsAutoAbsent =
            CASE
                WHEN l.Notes LIKE '%AutoAbsent%' THEN 1
                ELSE 0
            END,

        HasCheckInEvidence =
            CASE
                WHEN ISNULL(l.FaceImagePath, '') <> ''
                  OR ISNULL(l.CheckInIpAddress, '') <> ''
                  OR ISNULL(l.CheckInLocation, '') <> ''
                THEN 1 ELSE 0
            END
    FROM dbo.EmployeeAttendanceLogs l
    INNER JOIN dbo.Employees e
        ON e.Id = l.EmployeeId
    WHERE l.ShiftStartAt IS NOT NULL
      AND l.ShiftEndAt IS NOT NULL
      AND (l.Notes IS NULL OR l.Notes NOT LIKE '%[AttendanceDeleted]%')
),
Calc AS
(
    SELECT
        *,
        MinuteRate =
            CASE
                WHEN Salary > 0 AND ShiftMinutes > 0 AND DaysInMonth > 0
                THEN Salary / (CAST(ShiftMinutes AS decimal(18, 4)) * DaysInMonth)
                ELSE 0
            END,

        DayDeduction =
            CASE
                WHEN Salary > 0 AND DaysInMonth > 0
                THEN ROUND(Salary / DaysInMonth, 2)
                ELSE 0
            END,

        IsPureAutoAbsent =
            CASE
                WHEN IsAutoAbsent = 1 AND HasCheckInEvidence = 0
                THEN 1 ELSE 0
            END,

        LateMinutes =
            CASE
                WHEN CheckInMinute <= ShiftStartAt THEN 0
                WHEN CheckInMinute > ShiftEndAt THEN 0
                ELSE DATEDIFF(MINUTE, ShiftStartAt, CheckInMinute)
            END,

        IsAbsentByLateLogin =
            CASE
                WHEN CheckInMinute > ShiftEndAt THEN 1
                ELSE 0
            END,

        EarlyMinutes =
            CASE
                WHEN CheckOutMinute IS NULL THEN 0
                WHEN CheckOutMinute >= ShiftEndAt THEN 0
                ELSE DATEDIFF(MINUTE, CheckOutMinute, ShiftEndAt)
            END
    FROM BaseCalc
),
FinalCalc AS
(
    SELECT
        *,
        LatePenaltyMinutes = LateMinutes * 2,
        EarlyPenaltyMinutes = EarlyMinutes * 2,

        LateDeduction =
            CASE
                WHEN IsPureAutoAbsent = 1 THEN DayDeduction
                WHEN IsAbsentByLateLogin = 1 THEN DayDeduction
                WHEN LateMinutes > 0 THEN ROUND((LateMinutes * 2) * MinuteRate, 2)
                ELSE 0
            END,

        EarlyDeduction =
            CASE
                WHEN IsPureAutoAbsent = 1 THEN 0
                WHEN IsAbsentByLateLogin = 1 THEN 0
                WHEN EarlyMinutes > 0 THEN ROUND((EarlyMinutes * 2) * MinuteRate, 2)
                ELSE 0
            END
    FROM Calc
)
UPDATE l
SET
    l.CheckOutAt =
        CASE
            WHEN f.IsPureAutoAbsent = 1 THEN NULL
            ELSE l.CheckOutAt
        END,

    l.CheckOutFaceImagePath =
        CASE
            WHEN f.IsPureAutoAbsent = 1 THEN ''
            ELSE ISNULL(l.CheckOutFaceImagePath, '')
        END,

    l.CheckOutIpAddress =
        CASE
            WHEN f.IsPureAutoAbsent = 1 THEN ''
            ELSE ISNULL(l.CheckOutIpAddress, '')
        END,

    l.CheckOutLocation =
        CASE
            WHEN f.IsPureAutoAbsent = 1 THEN ''
            ELSE ISNULL(l.CheckOutLocation, '')
        END,

    l.DeductionAmount =
        CASE
            WHEN f.IsPureAutoAbsent = 1 THEN f.DayDeduction
            WHEN f.IsAbsentByLateLogin = 1 THEN f.DayDeduction
            ELSE ROUND(f.LateDeduction + f.EarlyDeduction, 2)
        END,

    l.DeductionReason =
        CASE
            WHEN f.IsPureAutoAbsent = 1 THEN N'غياب'
            WHEN f.IsAbsentByLateLogin = 1 THEN N'غياب'

            WHEN f.LateDeduction > 0 AND f.EarlyDeduction > 0 THEN
                CONCAT(
                    N'تأخر ', f.LateMinutes, N' دقيقة × 2 = ', f.LatePenaltyMinutes, N' دقيقة خصم',
                    N' + ',
                    N'خروج مبكر ', f.EarlyMinutes, N' دقيقة × 2 = ', f.EarlyPenaltyMinutes, N' دقيقة خصم'
                )

            WHEN f.LateDeduction > 0 THEN
                CONCAT(N'تأخر ', f.LateMinutes, N' دقيقة × 2 = ', f.LatePenaltyMinutes, N' دقيقة خصم')

            WHEN f.EarlyDeduction > 0 THEN
                CONCAT(N'خروج مبكر ', f.EarlyMinutes, N' دقيقة × 2 = ', f.EarlyPenaltyMinutes, N' دقيقة خصم')

            ELSE N'لا يوجد خصم'
        END,

    l.UpdatedAt = GETDATE()
FROM dbo.EmployeeAttendanceLogs l
INNER JOIN FinalCalc f
    ON f.Id = l.Id;

-- تنظيف صفوف غياب AutoAbsent المكررة لنفس الموظف ونفس وقت الشيفت، مع ترك آخر صف فقط
;WITH DuplicatedAutoAbsent AS
(
    SELECT
        Id,
        ROW_NUMBER() OVER
        (
            PARTITION BY EmployeeId, ShiftStartAt
            ORDER BY Id DESC
        ) AS rn
    FROM dbo.EmployeeAttendanceLogs
    WHERE Notes LIKE '%AutoAbsent%'
      AND EmployeeId IS NOT NULL
      AND ShiftStartAt IS NOT NULL
      AND (FaceImagePath IS NULL OR FaceImagePath = '')
      AND (CheckInIpAddress IS NULL OR CheckInIpAddress = '')
      AND (CheckInLocation IS NULL OR CheckInLocation = '')
      AND (Notes IS NULL OR Notes NOT LIKE '%[AttendanceDeleted]%')
)
UPDATE l
SET
    l.Notes = CONCAT(ISNULL(l.Notes, ''), ' [AttendanceDeleted]'),
    l.UpdatedAt = GETDATE()
FROM dbo.EmployeeAttendanceLogs l
INNER JOIN DuplicatedAutoAbsent d
    ON d.Id = l.Id
WHERE d.rn > 1;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // لا نرجع الداتا تلقائيًا هنا حتى لا نمسح تعديلات حقيقية اتعملت بعد الميجريشن.
            // الباك أب موجود في جدول:
            // EmployeeAttendanceLogs_BeforeRefresh_20260611
        }
    }
}