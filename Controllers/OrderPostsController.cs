using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using lotus_blue.Data;
using lotus_blue.Hubs;
using lotus_blue.Models;
using lotus_blue.Services;

namespace lotus_blue.Controllers
{
    [Authorize]
    [Route("[controller]/[action]")]
    public class OrderPostsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly FileUploadService _fileUploadService;
        private readonly IHubContext<OrderHub> _hubContext;
        private readonly GetCurrentTimeInIstanbul _timeService;

        public OrderPostsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            FileUploadService fileUploadService,
            IHubContext<OrderHub> hubContext,
            GetCurrentTimeInIstanbul timeService)
        {
            _context = context;
            _userManager = userManager;
            _fileUploadService = fileUploadService;
            _hubContext = hubContext;
            _timeService = timeService;
        }

        // POST /OrderPosts/Create  (multipart/form-data)
        // Fields: OrderId (int), Type (int 0|1|2), Body (string?), Images (IFormFile[]?)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int orderId, OrderPostType type, string? body, List<IFormFile>? images)
        {
            if (!await _context.Orders.AnyAsync(o => o.Id == orderId))
                return NotFound(new { message = "الطلب غير موجود" });

            if (string.IsNullOrWhiteSpace(body) && (images == null || images.Count == 0))
                return BadRequest(new { message = "لا يمكن إرسال إبلاغ فارغ" });

            var userId = _userManager.GetUserId(User);

            var post = new OrderPost
            {
                OrderId = orderId,
                Type = type,
                AuthorUserId = userId,
                Body = string.IsNullOrWhiteSpace(body) ? null : body.Trim(),
                CreatedAt = _timeService.GetIstanbulTimeWithOffset(),
            };

            if (images != null && images.Count > 0)
            {
                int sort = 0;
                foreach (var file in images)
                {
                    if (file == null || file.Length == 0) continue;
                    if (!file.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? true) continue;

                    var rel = await _fileUploadService.UploadFileAsync(file, "images/orderposts");
                    if (string.IsNullOrEmpty(rel)) continue;

                    post.Images.Add(new OrderPostImage
                    {
                        Url = "/" + rel.TrimStart('/'),
                        SortOrder = sort++,
                    });
                }
            }

            _context.OrderPosts.Add(post);
            await _context.SaveChangesAsync();

            // Notify panel-role clients so the home-page header panels refresh.
            // OrderNote has no home-page panel, so it doesn't get a live push — its sound is
            // played client-side on ajax success (so CallCenter hears it on their own actions).
            if (type != OrderPostType.OrderNote)
            {
                await _hubContext.Clients.Group("OrderPostListeners")
                    .SendAsync("newOrderPost", orderId, (int)type);
            }

            return Ok(new
            {
                id = post.Id,
                orderId = post.OrderId,
                type = (int)post.Type,
                body = post.Body,
                createdAt = post.CreatedAt,
                images = post.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).ToList(),
            });
        }

        // GET /OrderPosts/List?orderId=X&type=N
        // Returns bubbles for one (order, type), newest-first.
        // Visibility: Problem & EditNote → caller's own posts (Admin sees all); OrderNote → all.
        [HttpGet]
        public async Task<IActionResult> List(int orderId, OrderPostType type)
        {
            var userId = _userManager.GetUserId(User);
            var canSeeAll = User.IsInRole("Admin") || User.IsInRole("FollowUpDepartment") || User.IsInRole("ExecutiveDirector");
            var canDelete = User.IsInRole("Admin") || User.IsInRole("ExecutiveDirector");

            var q = _context.OrderPosts
                .AsNoTracking()
                .Where(p => p.OrderId == orderId && p.Type == type);

            if (type != OrderPostType.OrderNote && !canSeeAll)
                q = q.Where(p => p.AuthorUserId == userId);

            // Pull raw rows; resolve author display name client-side to avoid EF translation pitfalls
            // around chained subqueries + null-coalesce.
            var rows = await q
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new
                {
                    p.Id,
                    p.Body,
                    p.CreatedAt,
                    p.AuthorUserId,
                    Images = p.Images.OrderBy(i => i.SortOrder).Select(i => new { i.Id, i.Url }).ToList(),
                })
                .ToListAsync();

            // Author display name: Employee.DisplayName (preferred) > ApplicationUser.Name > "غير معروف".
            // Mirrors the project pattern used in HomeController for order/employee names.
            var authorIds = rows.Select(r => r.AuthorUserId).Distinct().ToList();
            var employeeDisplayNames = await _context.Employees
                .AsNoTracking()
                .Where(e => authorIds.Contains(e.ApplicationUserId))
                .Select(e => new { e.ApplicationUserId, e.DisplayName })
                .ToDictionaryAsync(x => x.ApplicationUserId, x => x.DisplayName);
            var userNames = await _context.Users
                .AsNoTracking()
                .Where(u => authorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Name })
                .ToDictionaryAsync(x => x.Id, x => x.Name);

            string ResolveName(string uid)
            {
                if (employeeDisplayNames.TryGetValue(uid, out var dn) && !string.IsNullOrWhiteSpace(dn)) return dn;
                if (userNames.TryGetValue(uid, out var n) && !string.IsNullOrWhiteSpace(n)) return n;
                return "غير معروف";
            }

            var posts = rows.Select(r => new
            {
                id = r.Id,
                body = r.Body,
                createdAt = r.CreatedAt,
                authorName = ResolveName(r.AuthorUserId),
                isCurrentUser = r.AuthorUserId == userId,
                images = r.Images,
            }).ToList();

            // Order context — drives the chat shell header for OrderNote (store image + name + source page).
            var orderInfo = await _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == orderId)
                .Select(o => new
                {
                    StoreName = o.ManufacturingCompany != null ? o.ManufacturingCompany.Name : null,
                    StoreImage = o.ManufacturingCompany != null ? o.ManufacturingCompany.ImageUrl : null,
                    SourceEnum = o.OrderSource,
                    o.SourceName,
                })
                .FirstOrDefaultAsync();

            object orderCtx = orderInfo == null ? null : new
            {
                storeName = orderInfo.StoreName,
                storeImage = orderInfo.StoreImage,
                sourceLabel = orderInfo.SourceEnum == OrderSourceEnum.واتساب ? "واتساب" : orderInfo.SourceName,
            };

            return Ok(new { posts, canDelete, order = orderCtx });
        }

        // POST /OrderPosts/Edit  { id, body }
        // Author-only — lets the original poster update the text body of their post.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string? body)
        {
            var userId = _userManager.GetUserId(User);
            var post = await _context.OrderPosts.FirstOrDefaultAsync(p => p.Id == id);
            if (post == null) return NotFound(new { message = "المنشور غير موجود" });
            if (post.AuthorUserId != userId) return Forbid();

            post.Body = string.IsNullOrWhiteSpace(body) ? null : body.Trim();
            await _context.SaveChangesAsync();

            return Ok(new { id = post.Id, body = post.Body });
        }

        // POST /OrderPosts/DeleteImage  { imageId }
        // Author-only — removes one image from a post (file on disk + DB row).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteImage(int imageId)
        {
            var userId = _userManager.GetUserId(User);
            var image = await _context.OrderPostImages
                .Include(i => i.OrderPost)
                .FirstOrDefaultAsync(i => i.Id == imageId);
            if (image == null) return NotFound();
            if (image.OrderPost.AuthorUserId != userId) return Forbid();

            _fileUploadService.DeleteFile(image.Url);
            _context.OrderPostImages.Remove(image);
            await _context.SaveChangesAsync();

            return Ok(new { imageId });
        }

        // POST /OrderPosts/Delete  { id }
        // Problem / EditNote → Admin or ExecutiveDirector only.
        // OrderNote → any logged-in role (the ملاحظة الطلب thread is fully open: anyone sees, posts, deletes).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var post = await _context.OrderPosts
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (post == null) return NotFound();

            if (post.Type != OrderPostType.OrderNote
                && !User.IsInRole("Admin")
                && !User.IsInRole("ExecutiveDirector"))
            {
                return Forbid();
            }

            foreach (var image in post.Images)
                _fileUploadService.DeleteFile(image.Url);

            _context.OrderPosts.Remove(post);
            await _context.SaveChangesAsync();

            if (post.Type != OrderPostType.OrderNote)
            {
                await _hubContext.Clients.Group("OrderPostListeners")
                    .SendAsync("newOrderPost", post.OrderId, (int)post.Type);
            }

            return Ok(new { id });
        }

        // GET /OrderPosts/Panels
        // Drives the two header panels (الإبلاغات / التعديلات المطلوبة).
        // Each panel shows ONE card per order that has any posts of that type (deletion is how admin clears them).
        // Admin-only — non-admins get empty.
        [HttpGet]
        public async Task<IActionResult> Panels()
        {
            var canSeePanels = User.IsInRole("Admin") || User.IsInRole("FollowUpDepartment") || User.IsInRole("ExecutiveDirector");
            if (!canSeePanels)
                return Ok(new { problem = EmptyPanel(), editNote = EmptyPanel() });

            var problem = await BuildPanelAsync(OrderPostType.Problem);
            var editNote = await BuildPanelAsync(OrderPostType.EditNote);

            return Ok(new { problem, editNote });
        }

        private async Task<object> BuildPanelAsync(OrderPostType type)
        {
            // Pull all posts of this type ordered newest-first, then group client-side.
            // Client-side grouping is more reliable than EF Core 6's GroupBy.First() pattern combined
            // with subquery projections. Admin deletes posts to clear them from the panel.
            var rows = await _context.OrderPosts
                .AsNoTracking()
                .Where(p => p.Type == type)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new
                {
                    p.Id,
                    p.OrderId,
                    p.CreatedAt,
                    p.AuthorUserId,
                    p.Body,
                    HasImages = p.Images.Any(),
                })
                .ToListAsync();

            // One row per order — the latest, since `rows` is already DESC by CreatedAt.
            var latestPerOrder = rows
                .GroupBy(r => r.OrderId)
                .Select(g => g.First())
                .ToList();

            // Resolve author display names: Employee.DisplayName > ApplicationUser.Name > "غير معروف".
            var authorIds = latestPerOrder.Select(r => r.AuthorUserId).Distinct().ToList();
            var employeeDisplayNames = await _context.Employees
                .AsNoTracking()
                .Where(e => authorIds.Contains(e.ApplicationUserId))
                .Select(e => new { e.ApplicationUserId, e.DisplayName })
                .ToDictionaryAsync(x => x.ApplicationUserId, x => x.DisplayName);
            var userNames = await _context.Users
                .AsNoTracking()
                .Where(u => authorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Name })
                .ToDictionaryAsync(x => x.Id, x => x.Name);

            // Resolve order country (string) — separate dictionary for the same reason name resolution
            // is split: avoids EF translation hazards on chained nav properties.
            var orderIds = latestPerOrder.Select(r => r.OrderId).Distinct().ToList();
            var orderCountries = await _context.Orders
                .AsNoTracking()
                .Where(o => orderIds.Contains(o.Id))
                .Select(o => new { o.Id, o.Country })
                .ToDictionaryAsync(x => x.Id, x => x.Country.ToString());

            string ResolveName(string userId)
            {
                if (employeeDisplayNames.TryGetValue(userId, out var dn) && !string.IsNullOrWhiteSpace(dn)) return dn;
                if (userNames.TryGetValue(userId, out var n) && !string.IsNullOrWhiteSpace(n)) return n;
                return "غير معروف";
            }

            // Group by date (yyyy-MM-dd) for the panel's date headers.
            var groups = latestPerOrder
                .GroupBy(x => x.CreatedAt.ToString("yyyy-MM-dd"))
                .Select(g => new
                {
                    date = g.Key,
                    cards = g.Select(x => new
                    {
                        orderId = x.OrderId,
                        country = orderCountries.TryGetValue(x.OrderId, out var c) ? c : "",
                        createdAt = x.CreatedAt,
                        authorName = ResolveName(x.AuthorUserId),
                        snippet = BuildSnippet(x.Body, x.HasImages),
                    }).ToList(),
                })
                .ToList();

            return new { count = latestPerOrder.Count, groups };
        }

        // GET /OrderPosts/OrderCounts?orderId=X
        // Per-order unread/active counts driving the badges next to the 3 modal triggers in
        // the order-details header. Visibility mirrors List(): Problem/EditNote → caller's own
        // posts unless Admin; OrderNote → all (no ack concept, so we report total).
        [HttpGet]
        public async Task<IActionResult> OrderCounts(int orderId)
        {
            var userId = _userManager.GetUserId(User);
            var canSeeAll = User.IsInRole("Admin") || User.IsInRole("FollowUpDepartment") || User.IsInRole("ExecutiveDirector");

            var problemQ = _context.OrderPosts
                .AsNoTracking()
                .Where(p => p.OrderId == orderId && p.Type == OrderPostType.Problem);
            if (!canSeeAll) problemQ = problemQ.Where(p => p.AuthorUserId == userId);
            var problem = await problemQ.CountAsync();

            var editNoteQ = _context.OrderPosts
                .AsNoTracking()
                .Where(p => p.OrderId == orderId && p.Type == OrderPostType.EditNote);
            if (!canSeeAll) editNoteQ = editNoteQ.Where(p => p.AuthorUserId == userId);
            var editNote = await editNoteQ.CountAsync();

            var orderNote = await _context.OrderPosts
                .AsNoTracking()
                .CountAsync(p => p.OrderId == orderId && p.Type == OrderPostType.OrderNote);

            return Ok(new { problem, editNote, orderNote });
        }

        private static object EmptyPanel() => new { count = 0, groups = new object[0] };

        private static string BuildSnippet(string? body, bool hasImages)
        {
            if (!string.IsNullOrWhiteSpace(body))
            {
                var trimmed = body.Trim();
                return trimmed.Length > 120 ? trimmed.Substring(0, 120) + "…" : trimmed;
            }
            return hasImages ? "[صور مرفقة]" : string.Empty;
        }
    }
}
