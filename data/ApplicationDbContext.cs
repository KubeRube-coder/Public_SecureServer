using Microsoft.EntityFrameworkCore;
using SecureServer.Models;

namespace SecureServer.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Mod> Mods { get; set; }
        public DbSet<Blacklist> Blacklist { get; set; }
        public DbSet<ActiveToken> ActiveTokens { get; set; }
        public DbSet<subscription> subscription { get; set; }
        public DbSet<Servers> Servers { get; set; }
        public DbSet<moddevelopers> moddevelopers { get; set; }
        public DbSet<premmods> premmods { get; set; }
        public DbSet<PendingMod> pendingMods { get; set; }
        public DbSet<purchasesInfo> purchasesInfo { get; set; }
        public DbSet<webhooksError> webhooks { get; set; }
        public DbSet<profitdata> profitdata { get; set; }
        public DbSet<FilesModel> filesdifine { get; set; }
        public DbSet<newsModel> newsmodels { get; set; }
        public DbSet<PaymentModel> paymentmodel { get; set; }
    }
}
