using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using lotus_blue.Data;
using lotus_blue.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace lotus_blue.Controllers
{
    [Authorize]
    [Route("[controller]")]
    public class OrderMetaActionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        private static readonly HashSet<string> AllowedReasons = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "معالجة طلب",
            "موافقة",
            "متابعة توصيل",
            "متابعة التوصيل",
            "حل مشكلة",
            "التأكد من الاستلام",
            "الطلبات الغير مكتمله",
            "تشيك المعلومات",
            "أخرى"
        };

        public OrderMetaActionsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public class SaveMetaActionRequest
        {
            public int OrderId { get; set; }

            public string Reason { get; set; } = "";

            public string OtherText { get; set; } = "";

            public string Url { get; set; } = "";

            public string ContactType { get; set; } = "";
        }

        private class MetaActionLogRow
        {
            public int Id { get; set; }

            public int OrderId { get; set; }

            public string EmployeeName { get; set; } = "";

            public string Reason { get; set; } = "";

            public string OtherText { get; set; } = "";

            public string MetaUrl { get; set; } = "";

            public DateTime ClickedAt { get; set; }
        }

        private class MetaActionSummary
        {
            public int OrderId { get; set; }

            public int Count { get; set; }

            public int MetaCount { get; set; }

            public int WhatsAppCount { get; set; }

            public List<object> Logs { get; set; } = new List<object>();
        }


        private class RatingMetaActionRow
        {
            public string EmployeeId { get; set; } = "";

            public string UserId { get; set; } = "";

            public string EmployeeName { get; set; } = "";

            public string Reason { get; set; } = "";

            public int Count { get; set; }
        }

        private class RatingMetaActionDetailsRow
        {
            public int OrderId { get; set; }

            public string EmployeeId { get; set; } = "";

            public string UserId { get; set; } = "";

            public string EmployeeName { get; set; } = "";

            public string Reason { get; set; } = "";

            public string OtherText { get; set; } = "";

            public string MetaUrl { get; set; } = "";

            public DateTime ClickedAt { get; set; }
        }

        [HttpPost("Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save([FromBody] SaveMetaActionRequest request)
        {
            if (request == null || request.OrderId <= 0)
            {
                return Json(new { success = false, message = "لم يتم التعرف على رقم الطلب." });
            }

            var reason = (request.Reason ?? string.Empty).Trim();
            var otherText = (request.OtherText ?? string.Empty).Trim();

            if (!AllowedReasons.Contains(reason))
            {
                return Json(new { success = false, message = "اختاري سبب صحيح لفتح رابط ميتا." });
            }

            if (reason == "أخرى" && string.IsNullOrWhiteSpace(otherText))
            {
                return Json(new { success = false, message = "اكتبي السبب في خانة أخرى." });
            }

            // توافق مع النسخ القديمة: كان السبب باسم "موافقة" أو "متابعة توصيل"، والآن يظهر ويحفظ باسم "متابعة التوصيل".
            if (reason == "موافقة" || reason == "متابعة توصيل")
            {
                reason = "متابعة التوصيل";
            }

            if (otherText.Length > 500)
            {
                otherText = otherText.Substring(0, 500);
            }

            await EnsureMetaActionTableAsync();

            var userId = _userManager.GetUserId(User) ?? "";
            var employeeName = await GetEmployeeDisplayNameAsync(userId);

            if (string.IsNullOrWhiteSpace(employeeName))
            {
                employeeName = User.Identity?.Name ?? "موظف";
            }

            var clickedAt = DateTime.Now;

            var connection = _context.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != ConnectionState.Open;

            if (shouldCloseConnection)
            {
                await connection.OpenAsync();
            }

            try
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
INSERT INTO dbo.OrderMetaActionClicks
(
    OrderId,
    UserId,
    EmployeeName,
    Reason,
    OtherText,
    MetaUrl,
    ClickedAt
)
VALUES
(
    @OrderId,
    @UserId,
    @EmployeeName,
    @Reason,
    @OtherText,
    @MetaUrl,
    @ClickedAt
);";

                    AddParameter(command, "@OrderId", request.OrderId);
                    AddParameter(command, "@UserId", string.IsNullOrWhiteSpace(userId) ? DBNull.Value : userId);
                    AddParameter(command, "@EmployeeName", employeeName);
                    AddParameter(command, "@Reason", reason);
                    AddParameter(command, "@OtherText", string.IsNullOrWhiteSpace(otherText) ? DBNull.Value : otherText);
                    AddParameter(command, "@MetaUrl", string.IsNullOrWhiteSpace(request.Url) ? DBNull.Value : request.Url.Trim());
                    AddParameter(command, "@ClickedAt", clickedAt);

                    await command.ExecuteNonQueryAsync();
                }
            }
            finally
            {
                if (shouldCloseConnection)
                {
                    await connection.CloseAsync();
                }
            }

            var summary = await BuildSummaryForOrdersAsync(new[] { request.OrderId });

            return Json(new
            {
                success = true,
                item = summary.FirstOrDefault()
            });
        }

        [HttpGet("Summary")]
        public async Task<IActionResult> Summary(string orderIds)
        {
            if (!CanViewMetaActionHistory())
            {
                return Json(new { success = true, items = Array.Empty<object>() });
            }

            var ids = ParseOrderIds(orderIds);

            if (ids.Count == 0)
            {
                return Json(new { success = true, items = Array.Empty<object>() });
            }

            await EnsureMetaActionTableAsync();

            var summary = await BuildSummaryForOrdersAsync(ids);

            return Json(new
            {
                success = true,
                items = summary
            });
        }


        [HttpGet("RatingSummary")]
        public async Task<IActionResult> RatingSummary(DateTime? startDate, DateTime? endDate)
        {
            if (!CanViewMetaActionHistory())
            {
                return Json(new { success = true, items = Array.Empty<object>() });
            }

            await EnsureMetaActionTableAsync();

            var items = new List<object>();
            var currentUserFilterSql = GetCurrentUserFilterSql("m");

            var connection = _context.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != ConnectionState.Open;

            if (shouldCloseConnection)
            {
                await connection.OpenAsync();
            }

            try
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $@"
SELECT
    COALESCE(CONVERT(NVARCHAR(50), e.Id), N'') AS EmployeeId,
    COALESCE(m.UserId, N'') AS UserId,
    COALESCE(NULLIF(e.DisplayName, N''), NULLIF(e.Name, N''), NULLIF(m.EmployeeName, N''), u.Email, N'موظف') AS EmployeeName,
    CASE WHEN m.Reason IN (N'متابعة توصيل', N'موافقة') THEN N'متابعة التوصيل' ELSE m.Reason END AS Reason,
    COUNT(1) AS TotalCount
FROM dbo.OrderMetaActionClicks m
LEFT JOIN dbo.Employees e
    ON e.ApplicationUserId = m.UserId
LEFT JOIN dbo.AspNetUsers u
    ON u.Id = m.UserId
WHERE (@StartDate IS NULL OR m.ClickedAt >= @StartDate)
  AND (@EndDate IS NULL OR m.ClickedAt < @EndDate)
  {currentUserFilterSql}
GROUP BY
    COALESCE(CONVERT(NVARCHAR(50), e.Id), N''),
    COALESCE(m.UserId, N''),
    COALESCE(NULLIF(e.DisplayName, N''), NULLIF(e.Name, N''), NULLIF(m.EmployeeName, N''), u.Email, N'موظف'),
    CASE WHEN m.Reason IN (N'متابعة توصيل', N'موافقة') THEN N'متابعة التوصيل' ELSE m.Reason END
ORDER BY EmployeeName, Reason;";

                    AddParameter(command, "@StartDate", startDate.HasValue ? (object)startDate.Value : DBNull.Value);
                    AddParameter(command, "@EndDate", endDate.HasValue ? (object)endDate.Value : DBNull.Value);
                    AddCurrentUserFilterParameter(command);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            items.Add(new
                            {
                                employeeId = reader.IsDBNull(0) ? "" : reader.GetString(0),
                                userId = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                employeeName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                reason = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                count = reader.GetInt32(4)
                            });
                        }
                    }
                }
            }
            finally
            {
                if (shouldCloseConnection)
                {
                    await connection.CloseAsync();
                }
            }

            return Json(new
            {
                success = true,
                items
            });
        }

        [HttpGet("RatingDetails")]
        public async Task<IActionResult> RatingDetails(
            string employeeId,
            string employeeName,
            string reason,
            DateTime? startDate,
            DateTime? endDate)
        {
            if (!CanViewMetaActionHistory())
            {
                return Json(new { success = true, items = Array.Empty<object>() });
            }

            reason = (reason ?? string.Empty).Trim();
            employeeId = (employeeId ?? string.Empty).Trim();
            employeeName = (employeeName ?? string.Empty).Trim();

            if (!AllowedReasons.Contains(reason))
            {
                return Json(new { success = false, message = "نوع الإجراء غير صحيح." });
            }

            await EnsureMetaActionTableAsync();

            var items = new List<object>();
            var currentUserFilterSql = GetCurrentUserFilterSql("m");

            var connection = _context.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != ConnectionState.Open;

            if (shouldCloseConnection)
            {
                await connection.OpenAsync();
            }

            try
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $@"
SELECT TOP 1000
    m.OrderId,
    COALESCE(CONVERT(NVARCHAR(50), e.Id), N'') AS EmployeeId,
    COALESCE(m.UserId, N'') AS UserId,
    COALESCE(NULLIF(e.DisplayName, N''), NULLIF(e.Name, N''), NULLIF(m.EmployeeName, N''), u.Email, N'موظف') AS EmployeeName,
    CASE WHEN m.Reason IN (N'متابعة توصيل', N'موافقة') THEN N'متابعة التوصيل' ELSE m.Reason END AS Reason,
    COALESCE(m.OtherText, N'') AS OtherText,
    COALESCE(m.MetaUrl, N'') AS MetaUrl,
    m.ClickedAt
FROM dbo.OrderMetaActionClicks m
LEFT JOIN dbo.Employees e
    ON e.ApplicationUserId = m.UserId
LEFT JOIN dbo.AspNetUsers u
    ON u.Id = m.UserId
WHERE (CASE WHEN m.Reason IN (N'متابعة توصيل', N'موافقة') THEN N'متابعة التوصيل' ELSE m.Reason END = @Reason)
  AND (@StartDate IS NULL OR m.ClickedAt >= @StartDate)
  AND (@EndDate IS NULL OR m.ClickedAt < @EndDate)
  AND (
        @EmployeeId = N''
        OR CONVERT(NVARCHAR(50), e.Id) = @EmployeeId
        OR m.UserId = @EmployeeId
        OR COALESCE(NULLIF(e.DisplayName, N''), NULLIF(e.Name, N''), NULLIF(m.EmployeeName, N''), u.Email, N'موظف') = @EmployeeName
      )
  {currentUserFilterSql}
ORDER BY m.ClickedAt DESC, m.Id DESC;";

                    AddParameter(command, "@Reason", reason);
                    AddParameter(command, "@EmployeeId", employeeId);
                    AddParameter(command, "@EmployeeName", employeeName);
                    AddParameter(command, "@StartDate", startDate.HasValue ? (object)startDate.Value : DBNull.Value);
                    AddParameter(command, "@EndDate", endDate.HasValue ? (object)endDate.Value : DBNull.Value);
                    AddCurrentUserFilterParameter(command);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var clickedAt = reader.GetDateTime(7);

                            items.Add(new
                            {
                                orderId = reader.GetInt32(0),
                                employeeId = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                userId = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                employeeName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                reason = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                otherText = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                metaUrl = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                contactType = GetContactTypeFromUrl(reader.IsDBNull(6) ? "" : reader.GetString(6)),
                                clickedAt = clickedAt.ToString("yyyy-MM-dd HH:mm")
                            });
                        }
                    }
                }
            }
            finally
            {
                if (shouldCloseConnection)
                {
                    await connection.CloseAsync();
                }
            }

            return Json(new
            {
                success = true,
                items
            });
        }


        [HttpGet("ReasonStats")]
        public async Task<IActionResult> ReasonStats(string contactType = null, int? orderId = null, string url = null)
        {
            if (!CanViewMetaActionHistory())
            {
                return Json(new { success = true, items = Array.Empty<object>() });
            }

            await EnsureMetaActionTableAsync();

            var normalizedContactType = NormalizeContactType(contactType);
            var contactFilterSql = BuildContactTypeWhereSql(normalizedContactType, "COALESCE(m.MetaUrl, N'')");
            var hasOrderFilter = orderId.HasValue && orderId.Value > 0;

            if (!hasOrderFilter)
            {
                return Json(new
                {
                    success = true,
                    contactType = string.IsNullOrWhiteSpace(normalizedContactType) ? "All" : normalizedContactType,
                    orderId = 0,
                    items = Array.Empty<object>()
                });
            }

            var orderFilterSql = " AND m.OrderId = @OrderId";
            var normalizedUrl = (url ?? string.Empty).Trim();
            var urlFilterSql = string.IsNullOrWhiteSpace(normalizedUrl)
                ? string.Empty
                : " AND COALESCE(m.MetaUrl, N'') = @MetaUrl";
            var currentUserFilterSql = GetCurrentUserFilterSql("m");

            var orderedReasons = new[]
            {
                "معالجة طلب",
                "متابعة التوصيل",
                "حل مشكلة",
                "التأكد من الاستلام",
                "الطلبات الغير مكتمله",
                "تشيك المعلومات",
                "أخرى"
            };

            var counts = orderedReasons.ToDictionary(reason => reason, reason => 0, StringComparer.OrdinalIgnoreCase);
            var logs = orderedReasons.ToDictionary(reason => reason, reason => new List<object>(), StringComparer.OrdinalIgnoreCase);

            var connection = _context.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != ConnectionState.Open;

            if (shouldCloseConnection)
            {
                await connection.OpenAsync();
            }

            try
            {
                using (var countCommand = connection.CreateCommand())
                {
                    countCommand.CommandText = $@"
SELECT
    CASE WHEN m.Reason IN (N'متابعة توصيل', N'موافقة') THEN N'متابعة التوصيل' ELSE m.Reason END AS ReasonName,
    COUNT(1) AS TotalCount
FROM dbo.OrderMetaActionClicks m
WHERE {contactFilterSql}{orderFilterSql}{urlFilterSql}{currentUserFilterSql}
GROUP BY CASE WHEN m.Reason IN (N'متابعة توصيل', N'موافقة') THEN N'متابعة التوصيل' ELSE m.Reason END;";

                    AddOrderIdParameter(countCommand, orderId);
                    AddMetaUrlParameter(countCommand, normalizedUrl);
                    AddCurrentUserFilterParameter(countCommand);

                    using (var reader = await countCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var reason = reader.IsDBNull(0) ? "" : reader.GetString(0);
                            var count = reader.GetInt32(1);

                            if (counts.ContainsKey(reason))
                            {
                                counts[reason] = count;
                            }
                        }
                    }
                }

                using (var logCommand = connection.CreateCommand())
                {
                    logCommand.CommandText = $@"
SELECT TOP 500
    m.OrderId,
    COALESCE(NULLIF(e.DisplayName, N''), NULLIF(e.Name, N''), NULLIF(m.EmployeeName, N''), u.Email, N'موظف') AS EmployeeName,
    CASE WHEN m.Reason IN (N'متابعة توصيل', N'موافقة') THEN N'متابعة التوصيل' ELSE m.Reason END AS ReasonName,
    COALESCE(m.OtherText, N'') AS OtherText,
    COALESCE(m.MetaUrl, N'') AS MetaUrl,
    m.ClickedAt
FROM dbo.OrderMetaActionClicks m
LEFT JOIN dbo.Employees e
    ON e.ApplicationUserId = m.UserId
LEFT JOIN dbo.AspNetUsers u
    ON u.Id = m.UserId
WHERE {contactFilterSql}{orderFilterSql}{urlFilterSql}{currentUserFilterSql}
ORDER BY m.ClickedAt DESC, m.Id DESC;";

                    AddOrderIdParameter(logCommand, orderId);
                    AddMetaUrlParameter(logCommand, normalizedUrl);
                    AddCurrentUserFilterParameter(logCommand);

                    using (var reader = await logCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var reason = reader.IsDBNull(2) ? "" : reader.GetString(2);

                            if (!logs.ContainsKey(reason) || logs[reason].Count >= 25)
                            {
                                continue;
                            }

                            var metaUrl = reader.IsDBNull(4) ? "" : reader.GetString(4);
                            var clickedAt = reader.GetDateTime(5);

                            logs[reason].Add(new
                            {
                                orderId = reader.GetInt32(0),
                                employeeName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                reason,
                                otherText = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                metaUrl,
                                contactType = GetContactTypeFromUrl(metaUrl),
                                clickedAt = clickedAt.ToString("yyyy-MM-dd HH:mm")
                            });
                        }
                    }
                }
            }
            finally
            {
                if (shouldCloseConnection)
                {
                    await connection.CloseAsync();
                }
            }

            var items = orderedReasons.Select(reason => new
            {
                reason,
                count = counts[reason],
                logs = logs[reason]
            }).Cast<object>().ToList();

            return Json(new
            {
                success = true,
                contactType = string.IsNullOrWhiteSpace(normalizedContactType) ? "All" : normalizedContactType,
                orderId = hasOrderFilter ? orderId.Value : 0,
                items
            });
        }

        [HttpGet("AllLogs")]
        public async Task<IActionResult> AllLogs(string contactType = null, int? orderId = null, string url = null)
        {
            if (!CanViewMetaActionHistory())
            {
                return Json(new { success = true, items = Array.Empty<object>() });
            }

            await EnsureMetaActionTableAsync();

            var normalizedContactType = NormalizeContactType(contactType);
            var contactFilterSql = BuildContactTypeWhereSql(normalizedContactType, "COALESCE(m.MetaUrl, N'')");
            var hasOrderFilter = orderId.HasValue && orderId.Value > 0;

            if (!hasOrderFilter)
            {
                return Json(new
                {
                    success = true,
                    contactType = string.IsNullOrWhiteSpace(normalizedContactType) ? "All" : normalizedContactType,
                    orderId = 0,
                    items = Array.Empty<object>()
                });
            }

            var orderFilterSql = " AND m.OrderId = @OrderId";
            var normalizedUrl = (url ?? string.Empty).Trim();
            var urlFilterSql = string.IsNullOrWhiteSpace(normalizedUrl)
                ? string.Empty
                : " AND COALESCE(m.MetaUrl, N'') = @MetaUrl";
            var currentUserFilterSql = GetCurrentUserFilterSql("m");

            var items = new List<object>();

            var connection = _context.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != ConnectionState.Open;

            if (shouldCloseConnection)
            {
                await connection.OpenAsync();
            }

            try
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $@"
SELECT TOP 1000
    m.OrderId,
    COALESCE(NULLIF(e.DisplayName, N''), NULLIF(e.Name, N''), NULLIF(m.EmployeeName, N''), u.Email, N'موظف') AS EmployeeName,
    CASE WHEN m.Reason IN (N'متابعة توصيل', N'موافقة') THEN N'متابعة التوصيل' ELSE m.Reason END AS ReasonName,
    COALESCE(m.OtherText, N'') AS OtherText,
    COALESCE(m.MetaUrl, N'') AS MetaUrl,
    m.ClickedAt
FROM dbo.OrderMetaActionClicks m
LEFT JOIN dbo.Employees e
    ON e.ApplicationUserId = m.UserId
LEFT JOIN dbo.AspNetUsers u
    ON u.Id = m.UserId
WHERE {contactFilterSql}{orderFilterSql}{urlFilterSql}{currentUserFilterSql}
ORDER BY m.ClickedAt DESC, m.Id DESC;";

                    AddOrderIdParameter(command, orderId);
                    AddMetaUrlParameter(command, normalizedUrl);
                    AddCurrentUserFilterParameter(command);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var clickedAt = reader.GetDateTime(5);

                            items.Add(new
                            {
                                orderId = reader.GetInt32(0),
                                employeeName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                reason = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                otherText = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                metaUrl = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                contactType = GetContactTypeFromUrl(reader.IsDBNull(4) ? "" : reader.GetString(4)),
                                clickedAt = clickedAt.ToString("yyyy-MM-dd HH:mm")
                            });
                        }
                    }
                }
            }
            finally
            {
                if (shouldCloseConnection)
                {
                    await connection.CloseAsync();
                }
            }

            return Json(new
            {
                success = true,
                contactType = string.IsNullOrWhiteSpace(normalizedContactType) ? "All" : normalizedContactType,
                orderId = hasOrderFilter ? orderId.Value : 0,
                items
            });
        }


        private static void AddMetaUrlParameter(IDbCommand command, string normalizedUrl)
        {
            if (command == null || string.IsNullOrWhiteSpace(normalizedUrl))
            {
                return;
            }

            AddParameter(command, "@MetaUrl", normalizedUrl.Trim());
        }

        private static void AddOrderIdParameter(IDbCommand command, int? orderId)
        {
            if (command == null || !orderId.HasValue || orderId.Value <= 0)
            {
                return;
            }

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@OrderId";
            parameter.Value = orderId.Value;
            command.Parameters.Add(parameter);
        }

        private static string NormalizeContactType(string contactType)
        {
            contactType = (contactType ?? string.Empty).Trim().ToLowerInvariant();

            if (contactType.Contains("whatsapp") || contactType.Contains("واتساب"))
            {
                return "WhatsApp";
            }

            if (contactType.Contains("meta") || contactType.Contains("ميتا"))
            {
                return "Meta";
            }

            return string.Empty;
        }

        private static string BuildContactTypeWhereSql(string normalizedContactType, string urlExpression)
        {
            var whatsAppCondition = $@"(
    LOWER({urlExpression}) LIKE N'%whatsapp%'
    OR LOWER({urlExpression}) LIKE N'%wa.me%'
)";

            if (string.Equals(normalizedContactType, "WhatsApp", StringComparison.OrdinalIgnoreCase))
            {
                return whatsAppCondition;
            }

            if (string.Equals(normalizedContactType, "Meta", StringComparison.OrdinalIgnoreCase))
            {
                return $"NOT {whatsAppCondition}";
            }

            return "1 = 1";
        }

        private static string GetContactTypeFromUrl(string url)
        {
            url = (url ?? string.Empty).Trim().ToLowerInvariant();

            if (url.Contains("whatsapp") || url.Contains("wa.me"))
            {
                return "WhatsApp";
            }

            return "Meta";
        }

        private bool CanViewMetaActionHistory()
        {
            return User?.Identity?.IsAuthenticated == true;
        }

        private bool CanViewAllMetaActionHistory()
        {
            return User.IsInRole("Admin")
                || User.IsInRole("ExecutiveDirector")
                || User.IsInRole("FollowUpDepartment");
        }

        private string GetCurrentUserFilterSql(string alias = "m")
        {
            if (CanViewAllMetaActionHistory())
            {
                return string.Empty;
            }

            var prefix = string.IsNullOrWhiteSpace(alias) ? string.Empty : alias.Trim() + ".";
            return $" AND {prefix}UserId = @CurrentUserId";
        }

        private void AddCurrentUserFilterParameter(IDbCommand command)
        {
            if (CanViewAllMetaActionHistory())
            {
                return;
            }

            AddParameter(command, "@CurrentUserId", _userManager.GetUserId(User) ?? string.Empty);
        }

        private static List<int> ParseOrderIds(string orderIds)
        {
            return (orderIds ?? string.Empty)
                .Split(new[] { ',', ';', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => int.TryParse(value, out var id) ? id : 0)
                .Where(id => id > 0)
                .Distinct()
                .Take(300)
                .ToList();
        }

        private async Task<List<object>> BuildSummaryForOrdersAsync(IEnumerable<int> orderIds)
        {
            var ids = orderIds
                .Where(id => id > 0)
                .Distinct()
                .Take(300)
                .ToList();

            var summaries = ids.ToDictionary(
                id => id,
                id => new MetaActionSummary
                {
                    OrderId = id,
                    Count = 0,
                    Logs = new List<object>()
                });

            if (ids.Count == 0)
            {
                return new List<object>();
            }

            var inClause = string.Join(",", ids);
            var currentUserFilterSql = GetCurrentUserFilterSql("");

            var connection = _context.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != ConnectionState.Open;

            if (shouldCloseConnection)
            {
                await connection.OpenAsync();
            }

            try
            {
                using (var countCommand = connection.CreateCommand())
                {
                    countCommand.CommandText = $@"
SELECT
    OrderId,
    CASE
        WHEN LOWER(COALESCE(MetaUrl, N'')) LIKE N'%whatsapp%'
          OR LOWER(COALESCE(MetaUrl, N'')) LIKE N'%wa.me%'
        THEN N'WhatsApp'
        ELSE N'Meta'
    END AS ContactType,
    COUNT(1) AS TotalCount
FROM dbo.OrderMetaActionClicks
WHERE OrderId IN ({inClause}){currentUserFilterSql}
GROUP BY
    OrderId,
    CASE
        WHEN LOWER(COALESCE(MetaUrl, N'')) LIKE N'%whatsapp%'
          OR LOWER(COALESCE(MetaUrl, N'')) LIKE N'%wa.me%'
        THEN N'WhatsApp'
        ELSE N'Meta'
    END;";

                    AddCurrentUserFilterParameter(countCommand);

                    using (var reader = await countCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var orderId = reader.GetInt32(0);
                            var contactType = reader.IsDBNull(1) ? "Meta" : reader.GetString(1);
                            var count = reader.GetInt32(2);

                            if (summaries.ContainsKey(orderId))
                            {
                                summaries[orderId].Count += count;

                                if (string.Equals(contactType, "WhatsApp", StringComparison.OrdinalIgnoreCase))
                                {
                                    summaries[orderId].WhatsAppCount = count;
                                }
                                else
                                {
                                    summaries[orderId].MetaCount = count;
                                }
                            }
                        }
                    }
                }
                // Initial badge refresh only needs counts.
                // Logs are loaded later per exact order button/contact type from ReasonStats/AllLogs.
            }
            finally
            {
                if (shouldCloseConnection)
                {
                    await connection.CloseAsync();
                }
            }

            return summaries.Values
                .OrderBy(item => item.OrderId)
                .Select(item => new
                {
                    orderId = item.OrderId,
                    count = item.Count,
                    metaCount = item.MetaCount,
                    whatsappCount = item.WhatsAppCount,
                    logs = item.Logs
                })
                .Cast<object>()
                .ToList();
        }

        private async Task<string> GetEmployeeDisplayNameAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return "";
            }

            var connection = _context.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != ConnectionState.Open;

            if (shouldCloseConnection)
            {
                await connection.OpenAsync();
            }

            try
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT TOP 1
    COALESCE(NULLIF(DisplayName, N''), NULLIF(Name, N''), N'')
FROM dbo.Employees
WHERE ApplicationUserId = @UserId;";

                    AddParameter(command, "@UserId", userId);

                    var value = await command.ExecuteScalarAsync();

                    return value == null || value == DBNull.Value
                        ? ""
                        : value.ToString() ?? "";
                }
            }
            finally
            {
                if (shouldCloseConnection)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private async Task EnsureMetaActionTableAsync()
        {
            await _context.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'dbo.OrderMetaActionClicks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderMetaActionClicks
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderMetaActionClicks PRIMARY KEY,
        OrderId INT NOT NULL,
        UserId NVARCHAR(450) NULL,
        EmployeeName NVARCHAR(300) NULL,
        Reason NVARCHAR(100) NOT NULL,
        OtherText NVARCHAR(500) NULL,
        MetaUrl NVARCHAR(1000) NULL,
        ClickedAt DATETIME2(0) NOT NULL CONSTRAINT DF_OrderMetaActionClicks_ClickedAt DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_OrderMetaActionClicks_OrderId_ClickedAt
        ON dbo.OrderMetaActionClicks (OrderId, ClickedAt DESC);
END
");
        }

        private static void AddParameter(IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }
}
