using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SecureServer.Data;
using SecureServer.Models;

public class BalanceHandler
{
    private readonly ApplicationDbContext _context;
    private const float CommissionRate = 0.15f;

    public BalanceHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Обрабатывает покупку, распределяя деньги между мододелом и системой.
    /// </summary>
    public async Task ProcessTransactionAsync(int userId, string modderName, float amount, string modName)
    {
        if (amount < 0)
            throw new ArgumentException("Сумма покупки должна быть больше 0.");

        float modderEarnings = amount;

        var modder = await _context.moddevelopers.FirstOrDefaultAsync(md => md.nameOfMod == modderName);
        if (modder == null)
            return;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Login == modder.nameOfMod);
        if (user == null)
            return;

        var systemProfit = new profitdata
        {
            WhoBought = userId,
            WhatBought = modName,
            Amount = amount,
            WhoEarn = user.Id,
            Profit = 0,
            Date = DateTime.UtcNow
        };

        user.balance += modderEarnings;

        _context.profitdata.Add(systemProfit);

        await _context.SaveChangesAsync();
    }

    public async Task ProcessDepositAsync(int userId, float amount, bool mode)
    {
        if (amount < 0)
            throw new ArgumentException("Сумма пополнения должна быть больше 0.");

        var user = await _context.Users.SingleOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            throw new Exception("Пользователь не найден");

        float commision = amount * CommissionRate;
        float total = amount - commision;

        var systemProfit = new profitdata
        {
            WhoBought = user.Id,
            WhatBought = "Deposit",
            Amount = total,
            WhoEarn = user.Id,
            Profit = commision,
            Date = DateTime.UtcNow
        };

        switch (mode)
        {
            case true:
                systemProfit.WhatBought = "Deposit (SET)";
                user.balance = amount;
                break;

            case false:
                systemProfit.WhatBought = "Deposit (ADD)";
                user.balance += total;
                break;
        }

        _context.profitdata.Add(systemProfit);

        await _context.SaveChangesAsync();
    }
}
