using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.Services;

namespace lotus_blue.Services
{
    public class BackgroundServiceScheduled : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public BackgroundServiceScheduled(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {

                using (var scope = _scopeFactory.CreateScope())
                {
                    var orderService = scope.ServiceProvider.GetRequiredService<OrderService>();
                    await orderService.UpdateOrderStatusesBasedOnDateAsync();
                    await orderService.UpdateOrderStatusesBasedOnStatus();
                    await orderService.UpdateOrderStatuseFromcanceledtofaildBasedOnStatus();

                    // Purge leads older than 7 days (الطلبات المحتملة auto-expire).
                    // CreatedDate is stored in Istanbul local time (see HomeController.CreateLead).
                    // Use the factory + `using` so the DbContext is deterministically disposed
                    // — ApplicationDbContext is registered transient via AddDbContextFactory and
                    // would otherwise outlive the scope's tracked disposables.
                    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
                    var timeService = scope.ServiceProvider.GetRequiredService<GetCurrentTimeInIstanbul>();
                    var cutoff = timeService.GetIstanbulTimeWithOffset().AddDays(-7);
                    using (var db = dbFactory.CreateDbContext())
                    {
                        var staleIds = await db.Leads.AsNoTracking()
                            .Where(l => l.CreatedDate < cutoff)
                            .Select(l => l.Id)
                            .ToListAsync(stoppingToken);
                        if (staleIds.Count > 0)
                        {
                            var stubs = staleIds.Select(id => new Lead { Id = id }).ToList();
                            db.Leads.AttachRange(stubs);
                            db.Leads.RemoveRange(stubs);
                            await db.SaveChangesAsync(stoppingToken);
                        }
                    }
                }

                // Delay for 1 day
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }
}
