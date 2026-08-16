using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Data.Common;
using System.Security.Claims;
using System.Threading.Tasks;
using lotus_blue.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace lotus_blue.Controllers
{
    [Authorize]
    [Route("OrderPosts")]
    public class OrderPostsFollowUpToolsController : Controller
    {
        private const int ProblemType = 0;
        private const int EditNoteType = 1;
        private readonly ApplicationDbContext _context;

        public OrderPostsFollowUpToolsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool CanManageEditNotes()
        {
            return User.IsInRole("Admin")
                || User.IsInRole("ExecutiveDirector")
                || User.IsInRole("FollowUpDepartment");
        }

        private bool CanSendProblemDeduction()
        {
            return User.IsInRole("Admin")
                || User.IsInRole("ExecutiveDirector");
        }

        private bool CanReadProblemDeductionHistory()
        {
            return User.IsInRole("Admin")
                || User.IsInRole("ExecutiveDirector")
                || User.IsInRole("FollowUpDepartment");
        }

        private bool CanUseHistoryForType(int type)
        {
            return type == EditNoteType
                ? CanManageEditNotes()
                : type == ProblemType && CanSendProblemDeduction();
        }

        private bool CanReadDeletedHistoryForType(int type)
        {
            return type == EditNoteType
                ? CanManageEditNotes()
                : type == ProblemType && CanReadProblemDeductionHistory();
        }

        private static void AddParameter(DbCommand command, string name, object? value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        private static string ReadString(DbDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
        }

        private static DateTime? ReadDateTime(DbDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            return Convert.ToDateTime(reader.GetValue(ordinal));
        }

        private static int ReadInt(DbDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
        }

        private static int? ReadNullableInt(DbDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
        }

        private static decimal ReadDecimal(DbDataReader reader, string name)
        {
            var ordinal = reader.GetOrdinal(name);
            return reader.IsDBNull(ordinal) ? 0m : Convert.ToDecimal(reader.GetValue(ordinal));
        }

        private async Task<string> GetCurrentUserDisplayNameAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                var employeeName = await _context.Employees
                    .AsNoTracking()
                    .Where(e => e.ApplicationUserId == userId)
                    .Select(e => e.DisplayName)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrWhiteSpace(employeeName))
                {
                    return employeeName;
                }
            }

            return User.Identity?.Name ?? userId ?? string.Empty;
        }

        [HttpGet("ListFollowUpEditNotes")]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
        public async Task<IActionResult> ListFollowUpEditNotes(int orderId, int type)
        {
            if (!CanManageEditNotes() || orderId <= 0 || type != EditNoteType)
            {
                return Forbid();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var posts = new List<object>();
            var connection = _context.Database.GetDbConnection();

            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    p.Id,
    p.OrderId,
    p.CreatedAt,
    p.AuthorUserId,
    p.Body,
    COALESCE(e.DisplayName, u.Name, p.AuthorUserId, N'غير معروف') AS AuthorName
FROM dbo.OrderPosts AS p
LEFT JOIN dbo.Employees AS e ON e.ApplicationUserId = p.AuthorUserId
LEFT JOIN dbo.AspNetUsers AS u ON u.Id = p.AuthorUserId
WHERE p.OrderId = @orderId AND p.Type = @type
ORDER BY p.CreatedAt DESC;";
                AddParameter(command, "@orderId", orderId);
                AddParameter(command, "@type", type);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var authorUserId = ReadString(reader, "AuthorUserId");

                        posts.Add(new
                        {
                            id = ReadInt(reader, "Id"),
                            orderId = ReadInt(reader, "OrderId"),
                            createdAt = ReadDateTime(reader, "CreatedAt"),
                            authorUserId,
                            authorName = ReadString(reader, "AuthorName"),
                            body = ReadString(reader, "Body"),
                            images = Array.Empty<object>(),
                            isCurrentUser = string.Equals(authorUserId, currentUserId, StringComparison.OrdinalIgnoreCase)
                        });
                    }
                }
            }

            return Json(new
            {
                posts,
                canDelete = true
            });
        }

        [HttpPost("DeleteEditNoteWithHistory")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
        public async Task<IActionResult> DeleteEditNoteWithHistory(int id)
        {
            if (!CanManageEditNotes() || id <= 0)
            {
                return Forbid();
            }

            var deletedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var deletedByName = await GetCurrentUserDisplayNameAsync();
            var deletedAt = DateTime.Now;
            var connection = _context.Database.GetDbConnection();

            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            using (var transaction = await connection.BeginTransactionAsync())
            {
                try
                {
                    using (var insertCommand = connection.CreateCommand())
                    {
                        insertCommand.Transaction = transaction;
                        insertCommand.CommandText = @"
INSERT INTO dbo.OrderPostDeletedHistories
(
    OrderPostId,
    OrderId,
    Type,
    Body,
    AuthorUserId,
    AuthorName,
    CreatedAt,
    DeletedAt,
    DeletedByUserId,
    DeletedByName
)
SELECT
    p.Id,
    p.OrderId,
    p.Type,
    p.Body,
    p.AuthorUserId,
    COALESCE(e.DisplayName, u.Name, p.AuthorUserId, N'غير معروف') AS AuthorName,
    p.CreatedAt,
    @deletedAt,
    @deletedByUserId,
    @deletedByName
FROM dbo.OrderPosts AS p
LEFT JOIN dbo.Employees AS e ON e.ApplicationUserId = p.AuthorUserId
LEFT JOIN dbo.AspNetUsers AS u ON u.Id = p.AuthorUserId
WHERE p.Id = @id
  AND p.Type = @type
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.OrderPostDeletedHistories AS h
      WHERE h.OrderPostId = p.Id
  );";
                        AddParameter(insertCommand, "@id", id);
                        AddParameter(insertCommand, "@type", EditNoteType);
                        AddParameter(insertCommand, "@deletedAt", deletedAt);
                        AddParameter(insertCommand, "@deletedByUserId", deletedByUserId);
                        AddParameter(insertCommand, "@deletedByName", deletedByName);

                        await insertCommand.ExecuteNonQueryAsync();
                    }

                    int deletedRows;
                    using (var deleteCommand = connection.CreateCommand())
                    {
                        deleteCommand.Transaction = transaction;
                        deleteCommand.CommandText = @"
DELETE FROM dbo.OrderPosts
WHERE Id = @id AND Type = @type;";
                        AddParameter(deleteCommand, "@id", id);
                        AddParameter(deleteCommand, "@type", EditNoteType);

                        deletedRows = await deleteCommand.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();

                    if (deletedRows <= 0)
                    {
                        return NotFound(new { success = false, message = "لم يتم العثور على التعديل المطلوب حذفه" });
                    }

                    return Json(new { success = true });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }

        [HttpGet("DeletedHistory")]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
        public async Task<IActionResult> DeletedHistory(int orderId, int type)
        {
            if (!CanReadDeletedHistoryForType(type) || orderId <= 0)
            {
                return Forbid();
            }

            var items = new List<object>();
            var connection = _context.Database.GetDbConnection();

            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    Id,
    OrderPostId,
    OrderId,
    Type,
    Body,
    AuthorUserId,
    AuthorName,
    CreatedAt,
    DeletedAt,
    DeletedByUserId,
    DeletedByName
FROM dbo.OrderPostDeletedHistories
WHERE OrderId = @orderId AND Type = @type
ORDER BY DeletedAt DESC, Id DESC;";
                AddParameter(command, "@orderId", orderId);
                AddParameter(command, "@type", EditNoteType);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        items.Add(new
                        {
                            id = ReadInt(reader, "Id"),
                            orderPostId = ReadInt(reader, "OrderPostId"),
                            orderId = ReadInt(reader, "OrderId"),
                            type = ReadInt(reader, "Type"),
                            body = ReadString(reader, "Body"),
                            authorUserId = ReadString(reader, "AuthorUserId"),
                            authorName = ReadString(reader, "AuthorName"),
                            createdAt = ReadDateTime(reader, "CreatedAt"),
                            deletedAt = ReadDateTime(reader, "DeletedAt"),
                            deletedByUserId = ReadString(reader, "DeletedByUserId"),
                            deletedByName = ReadString(reader, "DeletedByName")
                        });
                    }
                }
            }

            return Json(new { items });
        }

        [HttpPost("DeletePostWithHistory")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
        public async Task<IActionResult> DeletePostWithHistory(int id, int type)
        {
            if (!CanUseHistoryForType(type) || id <= 0)
            {
                return Forbid();
            }

            var deletedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var deletedByName = await GetCurrentUserDisplayNameAsync();
            var deletedAt = DateTime.Now;
            var connection = _context.Database.GetDbConnection();

            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            using (var transaction = await connection.BeginTransactionAsync())
            {
                try
                {
                    using (var insertCommand = connection.CreateCommand())
                    {
                        insertCommand.Transaction = transaction;
                        insertCommand.CommandText = @"
INSERT INTO dbo.OrderPostDeletedHistories
(
    OrderPostId,
    OrderId,
    Type,
    Body,
    AuthorUserId,
    AuthorName,
    CreatedAt,
    DeletedAt,
    DeletedByUserId,
    DeletedByName
)
SELECT
    p.Id,
    p.OrderId,
    p.Type,
    p.Body,
    p.AuthorUserId,
    COALESCE(e.DisplayName, u.Name, p.AuthorUserId, N'غير معروف') AS AuthorName,
    p.CreatedAt,
    @deletedAt,
    @deletedByUserId,
    @deletedByName
FROM dbo.OrderPosts AS p
LEFT JOIN dbo.Employees AS e ON e.ApplicationUserId = p.AuthorUserId
LEFT JOIN dbo.AspNetUsers AS u ON u.Id = p.AuthorUserId
WHERE p.Id = @id
  AND p.Type = @type
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.OrderPostDeletedHistories AS h
      WHERE h.OrderPostId = p.Id
  );";
                        AddParameter(insertCommand, "@id", id);
                        AddParameter(insertCommand, "@type", type);
                        AddParameter(insertCommand, "@deletedAt", deletedAt);
                        AddParameter(insertCommand, "@deletedByUserId", deletedByUserId);
                        AddParameter(insertCommand, "@deletedByName", deletedByName);

                        await insertCommand.ExecuteNonQueryAsync();
                    }

                    int deletedRows;
                    using (var deleteCommand = connection.CreateCommand())
                    {
                        deleteCommand.Transaction = transaction;
                        deleteCommand.CommandText = @"
DELETE FROM dbo.OrderPosts
WHERE Id = @id AND Type = @type;";
                        AddParameter(deleteCommand, "@id", id);
                        AddParameter(deleteCommand, "@type", type);

                        deletedRows = await deleteCommand.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();

                    if (deletedRows <= 0)
                    {
                        return NotFound(new { success = false, message = "لم يتم العثور على الإبلاغ المطلوب حذفه" });
                    }

                    return Json(new { success = true });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }

        [HttpGet("ProblemDeductionInfo")]
        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public async Task<IActionResult> ProblemDeductionInfo(int orderId)
        {
            if (!CanSendProblemDeduction() || orderId <= 0)
            {
                return Forbid();
            }

            decimal orderTotal = 0m;
            var employees = new List<object>();
            var connection = _context.Database.GetDbConnection();

            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            using (var orderCommand = connection.CreateCommand())
            {
                orderCommand.CommandText = @"
SELECT TOP (1) TotalPrice
FROM dbo.Orders
WHERE Id = @orderId;";
                AddParameter(orderCommand, "@orderId", orderId);

                var value = await orderCommand.ExecuteScalarAsync();
                if (value == null || value == DBNull.Value)
                {
                    return NotFound(new { success = false, message = "لم يتم العثور على الطلب" });
                }

                orderTotal = Convert.ToDecimal(value);
            }

            using (var employeeCommand = connection.CreateCommand())
            {
                employeeCommand.CommandText = @"
;WITH RelatedUsers AS
(
    SELECT ApplicationUserId AS UserId
    FROM dbo.Orders
    WHERE Id = @orderId
      AND ApplicationUserId IS NOT NULL
      AND ApplicationUserId <> N''

    UNION

    SELECT AuthorUserId AS UserId
    FROM dbo.OrderPosts
    WHERE OrderId = @orderId
      AND AuthorUserId IS NOT NULL
      AND AuthorUserId <> N''
)
SELECT DISTINCT
    e.Id,
    COALESCE(NULLIF(e.DisplayName, N''), u.Name, e.ApplicationUserId, CONVERT(NVARCHAR(30), e.Id)) AS EmployeeName
FROM dbo.Employees AS e
LEFT JOIN dbo.AspNetUsers AS u ON u.Id = e.ApplicationUserId
INNER JOIN RelatedUsers AS ru ON ru.UserId = e.ApplicationUserId
ORDER BY EmployeeName;";
                AddParameter(employeeCommand, "@orderId", orderId);

                using (var reader = await employeeCommand.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        employees.Add(new
                        {
                            id = ReadInt(reader, "Id"),
                            name = ReadString(reader, "EmployeeName")
                        });
                    }
                }
            }

            return Json(new { success = true, orderTotal, employees });
        }

        [HttpPost("CreateProblemDeduction")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,ExecutiveDirector")]
        public async Task<IActionResult> CreateProblemDeduction(int orderId, int employeeId, decimal amount, string? reason, string? problemText)
        {
            if (!CanSendProblemDeduction() || orderId <= 0 || employeeId <= 0)
            {
                return Forbid();
            }

            amount = Math.Round(amount, 2);
            reason = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason.Trim();
            problemText = string.IsNullOrWhiteSpace(problemText) ? string.Empty : problemText.Trim();

            if (amount <= 0)
            {
                return BadRequest(new { success = false, message = "قيمة الخصم غير صحيحة" });
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                return BadRequest(new { success = false, message = "تفاصيل الخصم مطلوبة" });
            }

            var createdByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var createdByName = await GetCurrentUserDisplayNameAsync();
            var now = DateTime.Now;
            decimal orderTotal = 0m;
            string employeeName = string.Empty;
            var connection = _context.Database.GetDbConnection();

            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            using (var validateOrderCommand = connection.CreateCommand())
            {
                validateOrderCommand.CommandText = @"
SELECT TOP (1) TotalPrice
FROM dbo.Orders
WHERE Id = @orderId;";
                AddParameter(validateOrderCommand, "@orderId", orderId);
                var value = await validateOrderCommand.ExecuteScalarAsync();

                if (value == null || value == DBNull.Value)
                {
                    return NotFound(new { success = false, message = "لم يتم العثور على الطلب" });
                }

                orderTotal = Convert.ToDecimal(value);
            }

            using (var employeeCommand = connection.CreateCommand())
            {
                employeeCommand.CommandText = @"
SELECT TOP (1)
    COALESCE(NULLIF(e.DisplayName, N''), u.Name, e.ApplicationUserId, CONVERT(NVARCHAR(30), e.Id)) AS EmployeeName
FROM dbo.Employees AS e
LEFT JOIN dbo.AspNetUsers AS u ON u.Id = e.ApplicationUserId
WHERE e.Id = @employeeId;";
                AddParameter(employeeCommand, "@employeeId", employeeId);
                var value = await employeeCommand.ExecuteScalarAsync();

                if (value == null || value == DBNull.Value)
                {
                    return NotFound(new { success = false, message = "لم يتم العثور على الموظف" });
                }

                employeeName = Convert.ToString(value) ?? string.Empty;
            }

            var transactionReason = $"خصم بسبب التبليغ عن مشكلة على الطلب #{orderId} - {reason}";

            using (var transaction = await connection.BeginTransactionAsync())
            {
                try
                {
                    int? employeeTransactionId = null;

                    using (var insertTransactionCommand = connection.CreateCommand())
                    {
                        insertTransactionCommand.Transaction = transaction;
                        insertTransactionCommand.CommandText = @"
INSERT INTO dbo.EmployeeTransactions
(
    Amount,
    TransactionType,
    Reason,
    Date,
    EmployeeId
)
OUTPUT INSERTED.Id
VALUES
(
    @amount,
    0,
    @reason,
    @date,
    @employeeId
);";
                        AddParameter(insertTransactionCommand, "@amount", amount);
                        AddParameter(insertTransactionCommand, "@reason", transactionReason);
                        AddParameter(insertTransactionCommand, "@date", now);
                        AddParameter(insertTransactionCommand, "@employeeId", employeeId);

                        var insertedId = await insertTransactionCommand.ExecuteScalarAsync();
                        employeeTransactionId = insertedId == null || insertedId == DBNull.Value
                            ? null
                            : Convert.ToInt32(insertedId);
                    }

                    using (var insertHistoryCommand = connection.CreateCommand())
                    {
                        insertHistoryCommand.Transaction = transaction;
                        insertHistoryCommand.CommandText = @"
INSERT INTO dbo.OrderPostEmployeeDeductions
(
    OrderId,
    EmployeeId,
    EmployeeName,
    Amount,
    OrderTotal,
    Reason,
    ProblemText,
    CreatedAt,
    CreatedByUserId,
    CreatedByName,
    EmployeeTransactionId
)
VALUES
(
    @orderId,
    @employeeId,
    @employeeName,
    @amount,
    @orderTotal,
    @reason,
    @problemText,
    @createdAt,
    @createdByUserId,
    @createdByName,
    @employeeTransactionId
);";
                        AddParameter(insertHistoryCommand, "@orderId", orderId);
                        AddParameter(insertHistoryCommand, "@employeeId", employeeId);
                        AddParameter(insertHistoryCommand, "@employeeName", employeeName);
                        AddParameter(insertHistoryCommand, "@amount", amount);
                        AddParameter(insertHistoryCommand, "@orderTotal", orderTotal);
                        AddParameter(insertHistoryCommand, "@reason", reason);
                        AddParameter(insertHistoryCommand, "@problemText", problemText);
                        AddParameter(insertHistoryCommand, "@createdAt", now);
                        AddParameter(insertHistoryCommand, "@createdByUserId", createdByUserId);
                        AddParameter(insertHistoryCommand, "@createdByName", createdByName);
                        AddParameter(insertHistoryCommand, "@employeeTransactionId", employeeTransactionId);

                        await insertHistoryCommand.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();
                    return Json(new { success = true });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }

        [HttpGet("ProblemDeductionHistory")]
        [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
        public async Task<IActionResult> ProblemDeductionHistory(int orderId)
        {
            if (!CanReadProblemDeductionHistory() || orderId <= 0)
            {
                return Forbid();
            }

            var items = new List<object>();
            var connection = _context.Database.GetDbConnection();

            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            using (var deductionCommand = connection.CreateCommand())
            {
                deductionCommand.CommandText = @"
SELECT
    Id,
    OrderId,
    EmployeeId,
    EmployeeName,
    Amount,
    OrderTotal,
    Reason,
    ProblemText,
    CreatedAt,
    CreatedByUserId,
    CreatedByName,
    EmployeeTransactionId
FROM dbo.OrderPostEmployeeDeductions
WHERE OrderId = @orderId
ORDER BY CreatedAt DESC, Id DESC;";
                AddParameter(deductionCommand, "@orderId", orderId);

                using (var reader = await deductionCommand.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        items.Add(new
                        {
                            kind = "deduction",
                            id = ReadInt(reader, "Id"),
                            orderId = ReadInt(reader, "OrderId"),
                            employeeId = ReadInt(reader, "EmployeeId"),
                            employeeName = ReadString(reader, "EmployeeName"),
                            amount = ReadDecimal(reader, "Amount"),
                            orderTotal = ReadDecimal(reader, "OrderTotal"),
                            reason = ReadString(reader, "Reason"),
                            problemText = ReadString(reader, "ProblemText"),
                            createdAt = ReadDateTime(reader, "CreatedAt"),
                            createdByUserId = ReadString(reader, "CreatedByUserId"),
                            createdByName = ReadString(reader, "CreatedByName"),
                            employeeTransactionId = ReadNullableInt(reader, "EmployeeTransactionId")
                        });
                    }
                }
            }

            using (var deletedCommand = connection.CreateCommand())
            {
                deletedCommand.CommandText = @"
SELECT
    Id,
    OrderPostId,
    OrderId,
    Type,
    Body,
    AuthorUserId,
    AuthorName,
    CreatedAt,
    DeletedAt,
    DeletedByUserId,
    DeletedByName
FROM dbo.OrderPostDeletedHistories
WHERE OrderId = @orderId AND Type = @type
ORDER BY DeletedAt DESC, Id DESC;";
                AddParameter(deletedCommand, "@orderId", orderId);
                AddParameter(deletedCommand, "@type", ProblemType);

                using (var reader = await deletedCommand.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        items.Add(new
                        {
                            kind = "deleted",
                            id = ReadInt(reader, "Id"),
                            orderPostId = ReadInt(reader, "OrderPostId"),
                            orderId = ReadInt(reader, "OrderId"),
                            type = ReadInt(reader, "Type"),
                            body = ReadString(reader, "Body"),
                            authorUserId = ReadString(reader, "AuthorUserId"),
                            authorName = ReadString(reader, "AuthorName"),
                            createdAt = ReadDateTime(reader, "CreatedAt"),
                            deletedAt = ReadDateTime(reader, "DeletedAt"),
                            deletedByUserId = ReadString(reader, "DeletedByUserId"),
                            deletedByName = ReadString(reader, "DeletedByName")
                        });
                    }
                }
            }

            return Json(new { items });
        }

    }
}
