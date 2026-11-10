using CBS.Domain.Accounts;
using CBS.Domain.BranchCash;
using CBS.Domain.Customers;
using CBS.Domain.GeneralLedger;
using CBS.Domain.Lending;
using CBS.Domain.SecurityAudit;
using CBS.Domain.TradeFinance;
using CBS.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence;

public class CbsDbContext : DbContext
{
    public DbSet<DepositAccount> DepositAccounts => Set<DepositAccount>();
    public DbSet<TellerDrawer> TellerDrawers => Set<TellerDrawer>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<FinancialVoucher> FinancialVouchers => Set<FinancialVoucher>();
    public DbSet<VoucherEntry> VoucherEntries => Set<VoucherEntry>();
    public DbSet<ChartOfAccount> ChartOfAccounts => Set<ChartOfAccount>();
    public DbSet<LoanAccount> LoanAccounts => Set<LoanAccount>();
    public DbSet<DocumentaryCredit> DocumentaryCredits => Set<DocumentaryCredit>();
    public DbSet<ShippingGuarantee> ShippingGuarantees => Set<ShippingGuarantee>();
    public DbSet<ExpRegulatoryTracking> ExpRegulatoryTrackings => Set<ExpRegulatoryTracking>();

    public CbsDbContext(DbContextOptions<CbsDbContext> options) : base(options)
    {
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // ARCHITECTURAL INTENT: Optimistic Concurrency Token Synchronization
        // PostgreSQL natively maintains the 'xmin' system column.
        // For SQLite and test providers, manually advance the shadow 'RowVersion' to guarantee
        // that concurrent updates trigger DbUpdateConcurrencyException.
        if (!Database.IsNpgsql())
        {
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State is EntityState.Added or EntityState.Modified)
                {
                    if (entry.Metadata.FindProperty("RowVersion") != null)
                    {
                        entry.Property("RowVersion").CurrentValue = Guid.NewGuid().ToByteArray();
                    }
                }
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new DepositAccountConfiguration());
        modelBuilder.ApplyConfiguration(new TellerDrawerConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerConfiguration());
        modelBuilder.ApplyConfiguration(new UserSessionConfiguration());
        modelBuilder.ApplyConfiguration(new FinancialVoucherConfiguration());
        modelBuilder.ApplyConfiguration(new VoucherEntryConfiguration());
        modelBuilder.ApplyConfiguration(new ChartOfAccountConfiguration());
        modelBuilder.ApplyConfiguration(new LoanAccountConfiguration());
        modelBuilder.ApplyConfiguration(new DocumentaryCreditConfiguration());
        modelBuilder.ApplyConfiguration(new ShippingGuaranteeConfiguration());
        modelBuilder.ApplyConfiguration(new ExpRegulatoryTrackingConfiguration());

        // Concurrency token and partial index enforcement
        if (Database.IsNpgsql())
        {
            modelBuilder.Entity<DepositAccount>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<TellerDrawer>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<LoanAccount>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<DocumentaryCredit>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<ShippingGuarantee>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<ExpRegulatoryTracking>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();

            modelBuilder.Entity<UserSession>()
                .HasIndex(s => s.UserId)
                .IsUnique()
                .HasDatabaseName("ux_active_user_session")
                .HasFilter("is_active = true");
        }
        else
        {
            modelBuilder.Entity<DepositAccount>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<TellerDrawer>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<LoanAccount>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<DocumentaryCredit>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<ShippingGuarantee>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<ExpRegulatoryTracking>().Property<byte[]>("RowVersion").IsConcurrencyToken();

            modelBuilder.Entity<UserSession>()
                .HasIndex(s => s.UserId)
                .IsUnique()
                .HasDatabaseName("ux_active_user_session")
                .HasFilter("is_active = 1");
        }
    }
}
