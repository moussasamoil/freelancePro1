using lotus_blue.API;
using lotus_blue.Data;
using lotus_blue.Hubs;
using lotus_blue.Models;
using lotus_blue.Models.ViewModel;
using lotus_blue.Roles;
using lotus_blue.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
namespace lotus_blue.Controllers
{
    public class EditHistoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly RoleAuthorizationService _roleAuthService;
        private readonly RESTAPI _restApi;
        private readonly GetCurrentTimeInIstanbul _timeService;
        private readonly PdfReportGenerator _reportGenerator;
        private readonly DeliveryCompanyService _deliveryCompanyService;
        private readonly FinancialService _financialService;
        private readonly DynamicCommon _dynamicCommon;
        private readonly RESTAPI _restapi;
        private readonly PdfReportGenertorOrderDetails _pdfReportGenertorOrderDetails;
        private readonly IHubContext<OrderHub> _hubContext;

        public EditHistoryController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, RESTAPI restApi, GetCurrentTimeInIstanbul timeService, PdfReportGenerator reportGenerator, DeliveryCompanyService deliveryCompanyService, FinancialService financialService, DynamicCommon dynamicCommon, RESTAPI restapi, PdfReportGenertorOrderDetails pdfReportGenertorOrderDetails, IHubContext<OrderHub> hubContext)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _roleAuthService = new RoleAuthorizationService();
            _restApi = restApi;
            _timeService = timeService;
            _reportGenerator = reportGenerator;
            _deliveryCompanyService = deliveryCompanyService;
            _financialService = financialService;
            _dynamicCommon = dynamicCommon;
            _restapi = restapi;
            _pdfReportGenertorOrderDetails = pdfReportGenertorOrderDetails;
            _hubContext = hubContext; 

        }
        // GET: /EditHistory/Mockup — renders the static mockup inside the site
        [Authorize]
        public IActionResult Mockup() => View();

        // GET: /EditHistory/OrderChanges?id=123
        // Returns a partial of (before, after) field-level diffs across consecutive snapshots
        // plus the current Order as the final "after".
        [Authorize]
        public async Task<IActionResult> OrderChanges(int id)
        {
            var snapshots = await _context.OrderEditHistories
                .Where(h => h.OrderId == id)
                .Include(h => h.OrderWarehouseEditHistories)
                    .ThenInclude(ow => ow.Warehouse)
                .OrderBy(h => h.EditNumber)
                .ToListAsync();

            var currentOrder = await _context.Orders
                .Include(o => o.OrderWarehouses).ThenInclude(ow => ow.Warehouse)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (currentOrder == null)
                return NotFound();

            // Resolve FK ids -> display names in batch
            var manuIds = snapshots.Select(s => s.ManufacturingCompanyId).Where(x => x.HasValue).Select(x => x.Value)
                .Append(currentOrder.ManufacturingCompanyId ?? 0).Distinct().ToList();
            var deliveryIds = snapshots.Select(s => s.DeliveryCompanyId)
                .Append(currentOrder.DeliveryCompanyId).Distinct().ToList();
            var campaignIds = snapshots.Select(s => s.CampaignId).Where(x => x.HasValue).Select(x => x.Value)
                .Append(currentOrder.CampaignId ?? 0).Distinct().ToList();
            var editorIds = snapshots.Select(s => s.Editedby).Where(x => !string.IsNullOrEmpty(x))
                .Concat(snapshots.Select(s => s.ApplicationUserId).Where(x => !string.IsNullOrEmpty(x)))
                .Concat(new[] { currentOrder.ApplicationUserId, currentOrder.Editedby }.Where(x => !string.IsNullOrEmpty(x)))
                .Distinct()
                .ToList();

            var manuMap = await _context.ManufacturingCompanies.Where(c => manuIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name);
            var deliveryMap = await _context.DeliveryCompanies.Where(c => deliveryIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name);
            var campaignMap = await _context.Campaigns.Where(c => campaignIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name);
            var editorRows = await _context.Employees
                .Where(e => editorIds.Contains(e.ApplicationUserId))
                .Select(e => new { e.ApplicationUserId, e.Name, e.DisplayName, e.ImageUrl })
                .ToListAsync();
            var editorMap = editorRows.ToDictionary(
                e => e.ApplicationUserId,
                e => (Name: !string.IsNullOrWhiteSpace(e.DisplayName) ? e.DisplayName : e.Name, ImageUrl: e.ImageUrl));

            string ManuName(int? idn) => idn.HasValue && manuMap.TryGetValue(idn.Value, out var n) ? n : (idn?.ToString() ?? "");
            string DeliveryName(int idn) => deliveryMap.TryGetValue(idn, out var n) ? n : idn.ToString();
            string CampaignName(int? idn) => idn.HasValue && campaignMap.TryGetValue(idn.Value, out var n) ? n : (idn?.ToString() ?? "");

            // Project each snapshot + current order to a uniform "state" record
            var states = new List<EditState>();
            foreach (var s in snapshots)
            {
                states.Add(new EditState
                {
                    EditNumber = s.EditNumber,
                    EditedAt = s.LastEditedDate ?? s.CreatedDate,
                    EditorId = s.Editedby,
                    Country = s.Country.ToString(),
                    State = s.State,
                    OrderSource = s.OrderSource.ToString(),
                    SourceName = s.SourceName,
                    ManufacturingCompany = ManuName(s.ManufacturingCompanyId),
                    DeliveryCompany = DeliveryName(s.DeliveryCompanyId),
                    Campaign = CampaignName(s.CampaignId),
                    TelephoneNumber = s.TelephoneNumber,
                    SecondTelephoneNumber = s.SecondTelephoneNumber,
                    CustomerName = s.CustomerName,
                    Notes = s.Notes,
                    Address = s.Address,
                    Chaturl = s.Chaturl,
                    OrderStatus = s.OrderStatus.ToString().Replace("_", " "),
                    TotalPrice = s.TotalPrice,
                    DeliveryPrice = s.DeliveryPrice,
                    FromComments = s.FromComments,
                    Gender = s.Gender,
                    IsPaid = s.IsPaid,
                    Warehouses = s.OrderWarehouseEditHistories?
                        .ToDictionary(ow => ow.Warehouse?.Name ?? ow.WarehouseId.ToString(), ow => ow.Amount)
                        ?? new Dictionary<string, int>()
                });
            }

            // Append the current Order as the "after" of the latest snapshot.
            // The actor on the LAST edit is whoever did the most recent EditOrder — currentOrder.Editedby.
            states.Add(new EditState
            {
                EditNumber = (snapshots.LastOrDefault()?.EditNumber ?? 0) + 1,
                EditedAt = currentOrder.LastEditedDate ?? currentOrder.CreatedDate,
                EditorId = currentOrder.Editedby,
                Country = currentOrder.Country.ToString(),
                State = currentOrder.State,
                OrderSource = currentOrder.OrderSource.ToString(),
                SourceName = currentOrder.SourceName,
                ManufacturingCompany = ManuName(currentOrder.ManufacturingCompanyId),
                DeliveryCompany = DeliveryName(currentOrder.DeliveryCompanyId),
                Campaign = CampaignName(currentOrder.CampaignId),
                TelephoneNumber = currentOrder.TelephoneNumber,
                SecondTelephoneNumber = currentOrder.SecondTelephoneNumber,
                CustomerName = currentOrder.CustomerName,
                Notes = currentOrder.Notes,
                Address = currentOrder.Address,
                Chaturl = currentOrder.Chaturl,
                OrderStatus = currentOrder.OrderStatus.ToString().Replace("_", " "),
                TotalPrice = currentOrder.TotalPrice,
                DeliveryPrice = currentOrder.DeliveryPrice,
                FromComments = currentOrder.FromComments,
                Gender = currentOrder.Gender,
                IsPaid = currentOrder.IsPaid,
                Warehouses = currentOrder.OrderWarehouses?
                    .ToDictionary(ow => ow.Warehouse?.Name ?? ow.WarehouseId.ToString(), ow => ow.Amount)
                    ?? new Dictionary<string, int>()
            });

            // Resolve editor names now that we also need currentOrder.Editedby
            if (!string.IsNullOrEmpty(currentOrder.Editedby) && !editorMap.ContainsKey(currentOrder.Editedby))
            {
                var emp = await _context.Employees.FirstOrDefaultAsync(e => e.ApplicationUserId == currentOrder.Editedby);
                if (emp != null)
                {
                    var resolvedName = !string.IsNullOrWhiteSpace(emp.DisplayName) ? emp.DisplayName : emp.Name;
                    editorMap[currentOrder.Editedby] = (resolvedName, emp.ImageUrl);
                }
            }
            string EditorName(string? uid) => !string.IsNullOrEmpty(uid) && editorMap.TryGetValue(uid, out var v) ? v.Name : "غير معروف";
            string? EditorImage(string? uid) => !string.IsNullOrEmpty(uid) && editorMap.TryGetValue(uid, out var v) ? v.ImageUrl : null;

            // Field comparators: (key, label, extractor)
            var fields = new (string Key, string Label, Func<EditState, string?> Get)[]
            {
                ("Country", "البلد", s => s.Country),
                ("State", "المدينة", s => s.State),
                ("OrderSource", "نوع الصفحة", s => s.OrderSource),
                ("SourceName", "اسم الصفحة", s => s.SourceName),
                ("ManufacturingCompany", "الشركة المصنعة", s => s.ManufacturingCompany),
                ("DeliveryCompany", "شركة التوصيل", s => s.DeliveryCompany),
                ("Campaign", "الحملة", s => s.Campaign),
                ("TelephoneNumber", "رقم الهاتف", s => s.TelephoneNumber),
                ("SecondTelephoneNumber", "رقم الهاتف الثاني", s => s.SecondTelephoneNumber),
                ("CustomerName", "اسم العميل", s => s.CustomerName),
                ("Notes", "الملاحظات", s => s.Notes),
                ("Address", "العنوان", s => s.Address),
                ("Chaturl", "رابط المحادثة", s => s.Chaturl),
                // OrderStatus intentionally excluded — status changes are tracked separately
                ("TotalPrice", "السعر الإجمالي", s => s.TotalPrice.ToString("0.##")),
                ("DeliveryPrice", "تكلفة التوصيل", s => s.DeliveryPrice.ToString("0.##")),
                ("FromComments", "من التعليقات", s => s.FromComments ? "نعم" : "لا"),
                ("Gender", "الجنس", s => s.Gender ? "ذكر" : "أنثى"),
                ("IsPaid", "طريقة الدفع", s => s.IsPaid ? "مدفوع" : "غير مدفوع"),
            };

            static string Norm(string? v) => v ?? "";

            // Stored timestamps are already Istanbul-local — just format them.
            string FmtTime(DateTime dt) => dt.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            string FmtDate(DateTime dt) => dt.ToString("yyyy/MM/dd", System.Globalization.CultureInfo.InvariantCulture);

            // Build one entry per edit session — each entry carries the full list of
            // field changes made in that single edit, plus a combined products diff
            // when any warehouse quantity changed.
            var groupedEntries = new List<EntryEnvelope>();
            for (int i = 1; i < states.Count; i++)
            {
                var prev = states[i - 1];
                var next = states[i];
                var changeList = new List<Dictionary<string, object?>>();

                foreach (var f in fields)
                {
                    var b = f.Get(prev);
                    var a = f.Get(next);
                    if (Norm(b) != Norm(a))
                    {
                        var isNew = string.IsNullOrEmpty(b) && !string.IsNullOrEmpty(a);
                        var isDel = !string.IsNullOrEmpty(b) && string.IsNullOrEmpty(a);
                        changeList.Add(new Dictionary<string, object?>
                        {
                            ["kind"] = "field",
                            ["fieldKey"] = f.Key,
                            ["label"] = f.Label,
                            ["op"] = "تعديل " + f.Label,
                            ["before"] = b,
                            ["after"] = a,
                            ["isNew"] = isNew,
                            ["isDel"] = isDel,
                        });
                    }
                }

                // Warehouse diff: union of products touched on either side, with
                // before/after quantities (0 when product wasn't present on that side).
                var productDiffs = new List<Dictionary<string, object?>>();
                var allWh = prev.Warehouses.Keys.Union(next.Warehouses.Keys).Distinct();
                foreach (var wh in allWh)
                {
                    prev.Warehouses.TryGetValue(wh, out var bAmt);
                    next.Warehouses.TryGetValue(wh, out var aAmt);
                    var hadBefore = prev.Warehouses.ContainsKey(wh);
                    var hasAfter = next.Warehouses.ContainsKey(wh);
                    if (bAmt != aAmt || hadBefore != hasAfter)
                    {
                        productDiffs.Add(new Dictionary<string, object?>
                        {
                            ["name"] = wh,
                            ["before"] = bAmt,
                            ["after"] = aAmt,
                        });
                    }
                }
                if (productDiffs.Count > 0)
                {
                    changeList.Add(new Dictionary<string, object?>
                    {
                        ["kind"] = "products",
                        ["label"] = "المنتجات",
                        ["op"] = "تعديل المنتجات",
                        ["products"] = productDiffs,
                    });
                }

                if (changeList.Count == 0) continue; // skip no-op edits

                groupedEntries.Add(new EntryEnvelope
                {
                    EditNumber = next.EditNumber,
                    Payload = new Dictionary<string, object?>
                    {
                        ["user"] = EditorName(next.EditorId),
                        ["userImage"] = EditorImage(next.EditorId),
                        ["time"] = FmtTime(next.EditedAt),
                        ["date"] = FmtDate(next.EditedAt),
                        ["info"] = null,
                        ["changes"] = changeList,
                    }
                });
            }

            var entries = groupedEntries
                .OrderByDescending(g => g.EditNumber)
                .Select(g => g.Payload)
                .ToList();

            // Append a creation entry. The creator is whoever owns ApplicationUserId — the
            // first snapshot's Editedby is null at create time (Editedby is only set on edit),
            // so we use ApplicationUserId from the first snapshot (or the order itself, as
            // a fallback when no snapshots exist).
            var firstSnap = snapshots.FirstOrDefault();
            var creatorId = firstSnap?.ApplicationUserId ?? currentOrder.ApplicationUserId;
            var creationDate = firstSnap?.CreatedDate ?? currentOrder.CreatedDate;
            entries.Add(new Dictionary<string, object?>
            {
                ["user"] = EditorName(creatorId),
                ["userImage"] = EditorImage(creatorId),
                ["time"] = FmtTime(creationDate),
                ["date"] = FmtDate(creationDate),
                ["info"] = "تم إنشاء الطلب بنجاح",
                ["changes"] = null,
            });

            return Json(new { orderId = id, entries });
        }

        private class EntryEnvelope
        {
            public int EditNumber { get; set; }
            public Dictionary<string, object?> Payload { get; set; } = new();
        }

        private class EditState
        {
            public int EditNumber { get; set; }
            public DateTime EditedAt { get; set; }
            public string? EditorId { get; set; }
            public string? Country { get; set; }
            public string? State { get; set; }
            public string? OrderSource { get; set; }
            public string? SourceName { get; set; }
            public string? ManufacturingCompany { get; set; }
            public string? DeliveryCompany { get; set; }
            public string? Campaign { get; set; }
            public string? TelephoneNumber { get; set; }
            public string? SecondTelephoneNumber { get; set; }
            public string? CustomerName { get; set; }
            public string? Notes { get; set; }
            public string? Address { get; set; }
            public string? Chaturl { get; set; }
            public string? OrderStatus { get; set; }
            public decimal TotalPrice { get; set; }
            public decimal DeliveryPrice { get; set; }
            public bool FromComments { get; set; }
            public bool Gender { get; set; }
            public bool IsPaid { get; set; }
            public Dictionary<string, int> Warehouses { get; set; } = new();
        }
    }
}
