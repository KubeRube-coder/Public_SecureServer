using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SecureServer.Data;
using SecureServer.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class DailyTaskRefresher : BackgroundService
{
    private readonly ILogger<DailyTaskRefresher> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public DailyTaskRefresher(ILogger<DailyTaskRefresher> logger, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Daily Task works! Executing refresh database");

                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var BLHandler = scope.ServiceProvider.GetRequiredService<BalanceHandler>();

                    _logger.LogInformation("Starting refreshing Subscriptions");
                    await HandleSubscription(context, BLHandler);

                    _logger.LogInformation("Starting refreshing Mods");
                    await HandleMods(context, BLHandler);
                }

                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error when processing Task");
            }
        }
    }

    private async Task HandleSubscription(ApplicationDbContext context, BalanceHandler _balanceHandler)
    {
        var subscriptions = await context.subscription.Where(s => s.subActive == true).ToListAsync();
        if (subscriptions.Count > 0)
        {

            foreach (var subscription in subscriptions)
            {
                if (subscription.BuyWhenExpires == true && subscription.expireData.Date < DateTime.UtcNow.Date)
                {
                    var user = await context.Users.FirstOrDefaultAsync(u => u.Login == subscription.login);
                    if (user != null)
                    {
                        var price = await context.premmods.FirstOrDefaultAsync(u => u.mods == subscription.subscriptionMods);
                        if (price != null)
                        {
                            var modcr = await context.moddevelopers.FirstOrDefaultAsync(md => md.modsby == price.modsby);
                            if (modcr != null)
                            {
                                await _balanceHandler.ProcessTransactionAsync(user.Id, modcr.nameOfMod, price.premPrice, price.mods);
                            }
                            user.balance -= price.premPrice;
                            subscription.expireData = DateTime.UtcNow.AddDays(30);
                        }
                    }
                }
                else if (subscription.expireData.Date < DateTime.UtcNow.Date)
                {
                    subscription.subActive = false;
                }
            }
        }

        await context.SaveChangesAsync();
        _logger.LogInformation("Sucesfully refresh DB Subscriptions");
    }

    private async Task HandleMods(ApplicationDbContext context, BalanceHandler _balanceHandler)
    {
        var purchases = await context.purchasesInfo.ToListAsync();

        if (purchases.Count > 0)
        {

            foreach (var purchase in purchases)
            {
                if (purchase.expires_date.Date < DateTime.UtcNow.Date)
                {
                    if (purchase.serverId != -1)
                    {
                        var server = await context.Servers.FirstOrDefaultAsync(s => s.id == purchase.serverId);
                        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == purchase.whoBuyed);

                        if (purchase.BuyWhenExpires && user != null)
                        {
                            float modPrice = await GetModPrice(context, purchase.modId);
                            if (user.balance >= modPrice)
                            {
                                await _balanceHandler.ProcessTransactionAsync(user.Id, await GetNameOfMod(context, purchase.modId), modPrice, purchase.modId.ToString());
                                user.balance -= modPrice;
                                purchase.expires_date = DateTime.UtcNow.AddDays(30);
                            }
                            else
                            {
                                _logger.LogWarning($"User {user.Id} doesn't have enough balance for renewal.");
                            }
                        }
                        else
                        {
                            if (server != null)
                            {
                                var serverModsIds = server.mods.Split(',')
                                    .Select(id => int.Parse(id.Trim()))
                                    .ToList();

                                if (serverModsIds.Remove(purchase.modId))
                                {
                                    server.mods = string.Join(',', serverModsIds);
                                }
                            }
                        }
                    }
                    else
                    {
                        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == purchase.whoBuyed);
                        if (user != null)
                        {
                            if (purchase.BuyWhenExpires)
                            {
                                float modPrice = await GetModPrice(context, purchase.modId);
                                if (user.balance >= modPrice)
                                {
                                    await _balanceHandler.ProcessTransactionAsync(user.Id, await GetNameOfMod(context, purchase.modId), modPrice, purchase.modId.ToString());
                                    user.balance -= modPrice;
                                    purchase.expires_date = DateTime.UtcNow.AddDays(30);
                                }
                                else
                                {
                                    _logger.LogWarning($"User {user.Id} doesn't have enough balance for renewal.");
                                }
                            }
                            else
                            {
                                var claimedModsDict = new Dictionary<int, int>();

                                if (!string.IsNullOrWhiteSpace(user.ClaimedMods))
                                {
                                    foreach (var entry in user.ClaimedMods.Split(','))
                                    {
                                        var parts = entry.Trim().Split('[');
                                        if (parts.Length == 2 &&
                                            int.TryParse(parts[0], out int modId) &&
                                            int.TryParse(parts[1].TrimEnd(']'), out int count))
                                        {
                                            claimedModsDict[modId] = count;
                                        }
                                    }
                                }

                                if (claimedModsDict.ContainsKey(purchase.modId))
                                {
                                    if (claimedModsDict[purchase.modId] > 1)
                                    {
                                        claimedModsDict[purchase.modId]--;
                                    }
                                    else
                                    {
                                        claimedModsDict.Remove(purchase.modId);
                                    }

                                    user.ClaimedMods = string.Join(",", claimedModsDict.Select(kvp => $"{kvp.Key}[{kvp.Value}]"));
                                }
                            }
                        }
                    }
                }
            }
        }

        await context.SaveChangesAsync();
        _logger.LogInformation("Successfully refreshed Mods");
    }
    private async Task<float> GetModPrice(ApplicationDbContext context, int modId)
    {
        var mod = await context.Mods.FirstOrDefaultAsync(m => m.Id == modId);
        return mod?.price ?? 0;
    }

    private async Task<string> GetNameOfMod(ApplicationDbContext context, int modId)
    {
        var mod = await context.Mods.FirstOrDefaultAsync(m => m.Id == modId);
        if (mod != null)
        {
            var modcr = await context.moddevelopers.FirstOrDefaultAsync(md => md.modsby == mod.modsby);

            return modcr?.nameOfMod ?? string.Empty;
        };

        return string.Empty;
    }
}
