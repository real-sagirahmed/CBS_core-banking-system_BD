using CBS.Domain.Accounts;
using CBS.Domain.Adc;
using CBS.Domain.AiIntelligence;
using CBS.Domain.BranchCash;
using CBS.Domain.Cheques;
using CBS.Domain.Customers;
using CBS.Domain.Deposits;
using CBS.Domain.GeneralLedger;
using CBS.Domain.Lending;
using CBS.Domain.PaymentRails;
using CBS.Domain.Reconciliation;
using CBS.Domain.Remittance;
using CBS.Domain.Reporting;
using CBS.Domain.Security;
using CBS.Domain.SecurityAudit;
using CBS.Domain.StandingInstructions;
using CBS.Domain.TermDeposits;
using CBS.Domain.TradeFinance;
using CBS.Domain.Training;
using CBS.Domain.Treasury;
using CBS.Domain.ValueObjects;
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
    public DbSet<TreasuryDeal> TreasuryDeals => Set<TreasuryDeal>();
    public DbSet<TreasurySecurity> TreasurySecurities => Set<TreasurySecurity>();
    public DbSet<AuditJournalEntry> AuditJournalEntries => Set<AuditJournalEntry>();
    public DbSet<BfiuRegulatoryReport> BfiuRegulatoryReports => Set<BfiuRegulatoryReport>();
    public DbSet<AiAdvisoryQuery> AiAdvisoryQueries => Set<AiAdvisoryQuery>();
    public DbSet<TbmlSemanticAssessment> TbmlSemanticAssessments => Set<TbmlSemanticAssessment>();
    public DbSet<RegulatoryKnowledgeDocument> RegulatoryKnowledgeDocuments => Set<RegulatoryKnowledgeDocument>();
    public DbSet<AdcTransaction> AdcTransactions => Set<AdcTransaction>();
    public DbSet<AtmTerminal> AtmTerminals => Set<AtmTerminal>();
    public DbSet<SettlementReconciliationBatch> SettlementReconciliationBatches => Set<SettlementReconciliationBatch>();
    public DbSet<DisputeClaim> DisputeClaims => Set<DisputeClaim>();
    public DbSet<ChequeClearingItem> ChequeClearingItems => Set<ChequeClearingItem>();
    public DbSet<BeftnBatchItem> BeftnBatchItems => Set<BeftnBatchItem>();
    public DbSet<RtgsTransaction> RtgsTransactions => Set<RtgsTransaction>();
    public DbSet<RemittanceInstrument> RemittanceInstruments => Set<RemittanceInstrument>();
    public DbSet<SecurityStationeryInventory> SecurityStationeryInventories => Set<SecurityStationeryInventory>();
    public DbSet<BranchOperatingDay> BranchOperatingDays => Set<BranchOperatingDay>();
    public DbSet<StandingInstruction> StandingInstructions => Set<StandingInstruction>();
    public DbSet<SweepAccountRule> SweepAccountRules => Set<SweepAccountRule>();
    public DbSet<ChequeBook> ChequeBooks => Set<ChequeBook>();
    public DbSet<ChequeLeaf> ChequeLeaves => Set<ChequeLeaf>();
    public DbSet<TermDepositContract> TermDepositContracts => Set<TermDepositContract>();
    public DbSet<DpsAccount> DpsAccounts => Set<DpsAccount>();
    public DbSet<DpsInstallmentSchedule> DpsInstallments => Set<DpsInstallmentSchedule>();
    public DbSet<DepositLienFacility> DepositLiens => Set<DepositLienFacility>();
    public DbSet<ForeignRemittanceTransaction> ForeignRemittanceTransactions => Set<ForeignRemittanceTransaction>();
    public DbSet<GovernmentIncentiveClaimBatch> GovernmentIncentiveClaimBatches => Set<GovernmentIncentiveClaimBatch>();
    public DbSet<SpecimenSignature> SpecimenSignatures => Set<SpecimenSignature>();
    public DbSet<AccountSignatoryMandate> AccountSignatoryMandates => Set<AccountSignatoryMandate>();
    public DbSet<CorporateCustomerEntity> CorporateCustomerEntities => Set<CorporateCustomerEntity>();
    public DbSet<CorporateGroup> CorporateGroups => Set<CorporateGroup>();
    public DbSet<CustomerDocumentRecord> CustomerDocumentRecords => Set<CustomerDocumentRecord>();
    public DbSet<SystemUserProfile> SystemUserProfiles => Set<SystemUserProfile>();
    public DbSet<AdminActionProposal> AdminActionProposals => Set<AdminActionProposal>();
    public DbSet<TransactionDoaOverride> TransactionDoaOverrides => Set<TransactionDoaOverride>();
    public DbSet<AdminAuditJournal> AdminAuditJournals => Set<AdminAuditJournal>();
    public DbSet<StatutoryReportArchive> StatutoryReportArchives => Set<StatutoryReportArchive>();
    public DbSet<UnclaimedDepositItem> UnclaimedDepositItems => Set<UnclaimedDepositItem>();
    public DbSet<TrainingSimulationSession> TrainingSimulationSessions => Set<TrainingSimulationSession>();
    public DbSet<EmployeeTrainingCertification> EmployeeTrainingCertifications => Set<EmployeeTrainingCertification>();
    public DbSet<CustomerSecurityBroadcast> CustomerSecurityBroadcasts => Set<CustomerSecurityBroadcast>();
    public DbSet<SystemUserManualTopic> SystemUserManualTopics => Set<SystemUserManualTopic>();

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
        modelBuilder.ApplyConfiguration(new TreasuryDealConfiguration());
        modelBuilder.ApplyConfiguration(new TreasurySecurityConfiguration());
        modelBuilder.ApplyConfiguration(new AuditJournalEntryConfiguration());
        modelBuilder.ApplyConfiguration(new BfiuRegulatoryReportConfiguration());
        modelBuilder.ApplyConfiguration(new AiAdvisoryQueryConfiguration());
        modelBuilder.ApplyConfiguration(new TbmlSemanticAssessmentConfiguration());
        modelBuilder.ApplyConfiguration(new RegulatoryKnowledgeDocumentConfiguration());
        modelBuilder.ApplyConfiguration(new AdcTransactionConfiguration());
        modelBuilder.ApplyConfiguration(new AtmTerminalConfiguration());
        modelBuilder.ApplyConfiguration(new SettlementReconciliationConfiguration());
        modelBuilder.ApplyConfiguration(new DisputeClaimConfiguration());
        modelBuilder.ApplyConfiguration(new ChequeClearingItemConfiguration());
        modelBuilder.ApplyConfiguration(new BeftnBatchItemConfiguration());
        modelBuilder.ApplyConfiguration(new RtgsTransactionConfiguration());
        modelBuilder.ApplyConfiguration(new RemittanceInstrumentConfiguration());
        modelBuilder.ApplyConfiguration(new SecurityStationeryConfiguration());
        modelBuilder.ApplyConfiguration(new BranchOperatingDayConfiguration());
        modelBuilder.ApplyConfiguration(new StandingInstructionConfiguration());
        modelBuilder.ApplyConfiguration(new SweepAccountRuleConfiguration());
        modelBuilder.ApplyConfiguration(new ChequeBookConfiguration());
        modelBuilder.ApplyConfiguration(new ChequeLeafConfiguration());
        modelBuilder.ApplyConfiguration(new TermDepositContractConfiguration());
        modelBuilder.ApplyConfiguration(new DpsAccountConfiguration());
        modelBuilder.ApplyConfiguration(new DpsInstallmentScheduleConfiguration());
        modelBuilder.ApplyConfiguration(new DepositLienFacilityConfiguration());
        modelBuilder.ApplyConfiguration(new ForeignRemittanceTransactionConfiguration());
        modelBuilder.ApplyConfiguration(new GovernmentIncentiveClaimBatchConfiguration());
        modelBuilder.ApplyConfiguration(new SpecimenSignatureConfiguration());
        modelBuilder.ApplyConfiguration(new AccountSignatoryMandateConfiguration());
        modelBuilder.ApplyConfiguration(new CorporateCustomerEntityConfiguration());
        modelBuilder.ApplyConfiguration(new CorporateGroupConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerDocumentRecordConfiguration());
        modelBuilder.ApplyConfiguration(new SystemUserProfileConfiguration());
        modelBuilder.ApplyConfiguration(new AdminActionProposalConfiguration());
        modelBuilder.ApplyConfiguration(new TransactionDoaOverrideConfiguration());
        modelBuilder.ApplyConfiguration(new AdminAuditJournalConfiguration());
        modelBuilder.ApplyConfiguration(new StatutoryReportArchiveConfiguration());
        modelBuilder.ApplyConfiguration(new UnclaimedDepositItemConfiguration());
        modelBuilder.ApplyConfiguration(new TrainingSimulationSessionConfiguration());
        modelBuilder.ApplyConfiguration(new EmployeeTrainingCertificationConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerSecurityBroadcastConfiguration());
        modelBuilder.ApplyConfiguration(new SystemUserManualTopicConfiguration());

        // Concurrency token and partial index enforcement
        if (Database.IsNpgsql())
        {
            modelBuilder.Entity<DepositAccount>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<TellerDrawer>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<LoanAccount>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<DocumentaryCredit>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<ShippingGuarantee>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<ExpRegulatoryTracking>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<TreasuryDeal>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<TreasurySecurity>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<BfiuRegulatoryReport>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<AiAdvisoryQuery>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<TbmlSemanticAssessment>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<StandingInstruction>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<SweepAccountRule>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<ChequeBook>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<ChequeLeaf>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<TermDepositContract>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<DpsAccount>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<DepositLienFacility>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<ForeignRemittanceTransaction>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<GovernmentIncentiveClaimBatch>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<SpecimenSignature>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<AccountSignatoryMandate>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<CorporateCustomerEntity>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<CorporateGroup>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<CustomerDocumentRecord>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<SystemUserProfile>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<AdminActionProposal>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<TransactionDoaOverride>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<StatutoryReportArchive>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<UnclaimedDepositItem>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<TrainingSimulationSession>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<EmployeeTrainingCertification>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<CustomerSecurityBroadcast>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();

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
            modelBuilder.Entity<TreasuryDeal>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<TreasurySecurity>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<BfiuRegulatoryReport>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<AiAdvisoryQuery>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<TbmlSemanticAssessment>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<StandingInstruction>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<SweepAccountRule>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<ChequeBook>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<ChequeLeaf>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<TermDepositContract>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<DpsAccount>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<DepositLienFacility>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<ForeignRemittanceTransaction>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<GovernmentIncentiveClaimBatch>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<SpecimenSignature>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<AccountSignatoryMandate>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<CorporateCustomerEntity>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<CorporateGroup>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<CustomerDocumentRecord>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<SystemUserProfile>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<AdminActionProposal>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<TransactionDoaOverride>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<StatutoryReportArchive>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<UnclaimedDepositItem>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<TrainingSimulationSession>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<EmployeeTrainingCertification>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<CustomerSecurityBroadcast>().Property<byte[]>("RowVersion").IsConcurrencyToken();

            modelBuilder.Entity<UserSession>()
                .HasIndex(s => s.UserId)
                .IsUnique()
                .HasDatabaseName("ux_active_user_session")
                .HasFilter("is_active = 1");
        }
    }
}
