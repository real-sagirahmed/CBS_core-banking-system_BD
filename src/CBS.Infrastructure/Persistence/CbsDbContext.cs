using CBS.Domain.Accounts;
using CBS.Domain.Adc;
using CBS.Domain.AiIntelligence;
using CBS.Domain.BackValue;
using CBS.Domain.BranchCash;
using CBS.Domain.Cheques;
using CBS.Domain.Clearing;
using CBS.Domain.Corporate;
using CBS.Domain.Customers;
using CBS.Domain.Deposits;
using CBS.Domain.GeneralLedger;
using CBS.Domain.Lending;
using CBS.Domain.PaymentInstruments;
using CBS.Domain.PaymentRails;
using CBS.Domain.Reconciliation;
using CBS.Domain.Remittance;
using CBS.Domain.Reporting;
using CBS.Domain.Security;
using CBS.Domain.SecurityAudit;
using CBS.Domain.Shariah;
using CBS.Domain.StandingInstructions;
using CBS.Domain.TermDeposits;
using CBS.Domain.TradeFinance;
using CBS.Domain.Training;
using CBS.Domain.Treasury;
using CBS.Domain.ValueObjects;
using CBS.Domain.Lending.Agricultural;
using CBS.Domain.Lending.Discounted;
using CBS.Domain.Deposits.Multiplication;
using CBS.Domain.Lending.Demand;
using CBS.Infrastructure.Persistence.Configurations;
using CBS.Infrastructure.Persistence.Configurations.Shariah;
using CBS.Infrastructure.Persistence.Configurations.Specialized;
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
    public DbSet<MudarabaInvestmentPool> MudarabaInvestmentPools => Set<MudarabaInvestmentPool>();
    public DbSet<AlWadiahAccount> AlWadiahAccounts => Set<AlWadiahAccount>();
    public DbSet<MurabahaFinancingFacility> MurabahaFinancingFacilities => Set<MurabahaFinancingFacility>();
    public DbSet<QuardFinancingFacility> QuardFinancingFacilities => Set<QuardFinancingFacility>();
    public DbSet<ShariahTaWidhCharityFund> ShariahTaWidhCharityFunds => Set<ShariahTaWidhCharityFund>();
    public DbSet<AgriculturalLoanFacility> AgriculturalLoans => Set<AgriculturalLoanFacility>();
    public DbSet<BillDiscountingFacility> DiscountedBills => Set<BillDiscountingFacility>();
    public DbSet<DepositMultiplicationAccount> DepositMultiplicationAccounts => Set<DepositMultiplicationAccount>();
    public DbSet<InterBranchPaymentInstrument> InterBranchPaymentInstruments => Set<InterBranchPaymentInstrument>();
    public DbSet<DemandLoanFacility> DemandLoanFacilities => Set<DemandLoanFacility>();
    public DbSet<SecurityDepositReceipt> SecurityDepositReceipts => Set<SecurityDepositReceipt>();
    public DbSet<RemittanceStopPaymentJournal> RemittanceStopPaymentJournals => Set<RemittanceStopPaymentJournal>();
    public DbSet<BankGuarantee> BankGuarantees => Set<BankGuarantee>();
    public DbSet<ReconciliationAgingItem> ReconciliationAgingItems => Set<ReconciliationAgingItem>();
    public DbSet<BranchVault> BranchVaults => Set<BranchVault>();
    public DbSet<MultiAccountSweepRule> MultiAccountSweepRules => Set<MultiAccountSweepRule>();
    public DbSet<AccountRestraint> AccountRestraints => Set<AccountRestraint>();
    public DbSet<BulkCorporateBatch> BulkCorporateBatches => Set<BulkCorporateBatch>();
    public DbSet<BulkCorporateBatchItem> BulkCorporateBatchItems => Set<BulkCorporateBatchItem>();
    public DbSet<AccountLedgerEntry> AccountLedgerEntries => Set<AccountLedgerEntry>();
    public DbSet<StatutoryCertificateRecord> StatutoryCertificateRecords => Set<StatutoryCertificateRecord>();
    public DbSet<MasterExportLc> MasterExportLcs => Set<MasterExportLc>();
    public DbSet<TradeTransactionProfile> TradeTransactionProfiles => Set<TradeTransactionProfile>();
    public DbSet<TbmlPriceBenchmark> TbmlPriceBenchmarks => Set<TbmlPriceBenchmark>();
    public DbSet<TbmlAlertRecord> TbmlAlertRecords => Set<TbmlAlertRecord>();
    public DbSet<VesselTrackingRecord> VesselTrackingRecords => Set<VesselTrackingRecord>();
    public DbSet<LoanRescheduleRecord> LoanReschedules => Set<LoanRescheduleRecord>();
    public DbSet<LoanWriteOffRecord> LoanWriteOffs => Set<LoanWriteOffRecord>();
    public DbSet<QualitativeJudgmentRecord> QualitativeJudgments => Set<QualitativeJudgmentRecord>();
    public DbSet<TreasuryLimitProfile> TreasuryLimits => Set<TreasuryLimitProfile>();
    public DbSet<MtmRevaluationBatch> MtmRevaluationBatches => Set<MtmRevaluationBatch>();
    public DbSet<NostroMirrorReconciliationItem> NostroReconciliations => Set<NostroMirrorReconciliationItem>();
    public DbSet<StatutoryReserveAssessment> StatutoryReserves => Set<StatutoryReserveAssessment>();
    public DbSet<CustomerRiskAssessment> CustomerRiskAssessments => Set<CustomerRiskAssessment>();
    public DbSet<SanctionScreeningRecord> SanctionScreeningRecords => Set<SanctionScreeningRecord>();
    public DbSet<FatcaComplianceProfile> FatcaComplianceProfiles => Set<FatcaComplianceProfile>();
    public DbSet<BorrowerIcrrAssessment> BorrowerIcrrAssessments => Set<BorrowerIcrrAssessment>();
    public DbSet<ChequeDishonourRecord> ChequeDishonourRecords => Set<ChequeDishonourRecord>();
    public DbSet<BillForCollection> BillsForCollection => Set<BillForCollection>();
    public DbSet<InlandBillPurchased> InlandBillsPurchased => Set<InlandBillPurchased>();
    public DbSet<CustomerTransactionProfile> CustomerTransactionProfiles => Set<CustomerTransactionProfile>();
    public DbSet<AdcReconciliationBatch> AdcReconciliationBatches => Set<AdcReconciliationBatch>();
    public DbSet<TransactionAnomalyRecord> TransactionAnomalies => Set<TransactionAnomalyRecord>();
    public DbSet<BreakGlassEmergencySession> BreakGlassSessions => Set<BreakGlassEmergencySession>();
    public DbSet<DailyEodBalancingSheet> DailyEodBalancingSheets => Set<DailyEodBalancingSheet>();
    public DbSet<RemittanceCashIncentiveRecord> RemittanceCashIncentives => Set<RemittanceCashIncentiveRecord>();
    public DbSet<DailyCleanCashSheet> DailyCleanCashSheets => Set<DailyCleanCashSheet>();
    public DbSet<UserDoaProfile> UserDoaProfiles => Set<UserDoaProfile>();
    public DbSet<TemporaryAuthorityDelegation> TemporaryAuthorityDelegations => Set<TemporaryAuthorityDelegation>();
    public DbSet<UserPasswordCredential> UserPasswordCredentials => Set<UserPasswordCredential>();
    public DbSet<UserActiveSession> UserActiveSessions => Set<UserActiveSession>();
    public DbSet<AdminChangeRequest> AdminChangeRequests => Set<AdminChangeRequest>();
    public DbSet<BankGlobalParameters> BankGlobalParameters => Set<BankGlobalParameters>();
    public DbSet<BackValueTransactionRequest> BackValueTransactionRequests => Set<BackValueTransactionRequest>();
    public DbSet<BackValueAuditJournalEntry> BackValueAuditJournalEntries => Set<BackValueAuditJournalEntry>();
    public DbSet<LinkedTransactionReversalRecord> LinkedTransactionReversalRecords => Set<LinkedTransactionReversalRecord>();
    public DbSet<PaymentInstrument> PaymentInstruments => Set<PaymentInstrument>();
    public DbSet<PaymentInstrumentStockBook> PaymentInstrumentStockBooks => Set<PaymentInstrumentStockBook>();

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
        modelBuilder.ApplyConfiguration(new MudarabaInvestmentPoolConfiguration());
        modelBuilder.ApplyConfiguration(new AlWadiahAccountConfiguration());
        modelBuilder.ApplyConfiguration(new MurabahaFinancingFacilityConfiguration());
        modelBuilder.ApplyConfiguration(new QuardFinancingFacilityConfiguration());
        modelBuilder.ApplyConfiguration(new ShariahTaWidhCharityFundConfiguration());
        modelBuilder.ApplyConfiguration(new AgriculturalLoanFacilityConfiguration());
        modelBuilder.ApplyConfiguration(new BillDiscountingFacilityConfiguration());
        modelBuilder.ApplyConfiguration(new DepositMultiplicationAccountConfiguration());
        modelBuilder.ApplyConfiguration(new InterBranchPaymentInstrumentConfiguration());
        modelBuilder.ApplyConfiguration(new DemandLoanFacilityConfiguration());
        modelBuilder.ApplyConfiguration(new SecurityDepositReceiptConfiguration());
        modelBuilder.ApplyConfiguration(new RemittanceStopPaymentJournalConfiguration());
        modelBuilder.ApplyConfiguration(new BankGuaranteeConfiguration());
        modelBuilder.ApplyConfiguration(new ReconciliationAgingItemConfiguration());
        modelBuilder.Ignore<MultiAccountSweepTarget>();
        modelBuilder.ApplyConfiguration(new BranchVaultConfiguration());
        modelBuilder.ApplyConfiguration(new MultiAccountSweepRuleConfiguration());
        modelBuilder.ApplyConfiguration(new AccountRestraintConfiguration());
        modelBuilder.ApplyConfiguration(new BulkCorporateBatchConfiguration());
        modelBuilder.ApplyConfiguration(new BulkCorporateBatchItemConfiguration());
        modelBuilder.ApplyConfiguration(new AccountLedgerEntryConfiguration());
        modelBuilder.ApplyConfiguration(new StatutoryCertificateRecordConfiguration());
        modelBuilder.ApplyConfiguration(new MasterExportLcConfiguration());
        modelBuilder.ApplyConfiguration(new TradeTransactionProfileConfiguration());
        modelBuilder.ApplyConfiguration(new TbmlPriceBenchmarkConfiguration());
        modelBuilder.ApplyConfiguration(new TbmlAlertRecordConfiguration());
        modelBuilder.ApplyConfiguration(new VesselTrackingRecordConfiguration());
        modelBuilder.ApplyConfiguration(new LoanRescheduleRecordConfiguration());
        modelBuilder.ApplyConfiguration(new LoanWriteOffRecordConfiguration());
        modelBuilder.ApplyConfiguration(new QualitativeJudgmentRecordConfiguration());
        modelBuilder.ApplyConfiguration(new TreasuryLimitProfileConfiguration());
        modelBuilder.ApplyConfiguration(new MtmRevaluationBatchConfiguration());
        modelBuilder.ApplyConfiguration(new NostroMirrorReconciliationItemConfiguration());
        modelBuilder.ApplyConfiguration(new StatutoryReserveAssessmentConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerRiskAssessmentConfiguration());
        modelBuilder.ApplyConfiguration(new SanctionScreeningRecordConfiguration());
        modelBuilder.ApplyConfiguration(new FatcaComplianceProfileConfiguration());
        modelBuilder.ApplyConfiguration(new BorrowerIcrrAssessmentConfiguration());
        modelBuilder.ApplyConfiguration(new ChequeDishonourRecordConfiguration());
        modelBuilder.ApplyConfiguration(new BillForCollectionConfiguration());
        modelBuilder.ApplyConfiguration(new InlandBillPurchasedConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerTransactionProfileConfiguration());
        modelBuilder.ApplyConfiguration(new AdcReconciliationBatchConfiguration());
        modelBuilder.ApplyConfiguration(new TransactionAnomalyRecordConfiguration());
        modelBuilder.ApplyConfiguration(new BreakGlassEmergencySessionConfiguration());
        modelBuilder.ApplyConfiguration(new DailyEodBalancingSheetConfiguration());
        modelBuilder.ApplyConfiguration(new RemittanceCashIncentiveRecordConfiguration());
        modelBuilder.ApplyConfiguration(new DailyCleanCashSheetConfiguration());
        modelBuilder.ApplyConfiguration(new UserDoaProfileConfiguration());
        modelBuilder.ApplyConfiguration(new TemporaryAuthorityDelegationConfiguration());
        modelBuilder.ApplyConfiguration(new UserPasswordCredentialConfiguration());
        modelBuilder.ApplyConfiguration(new UserActiveSessionConfiguration());
        modelBuilder.ApplyConfiguration(new AdminChangeRequestConfiguration());
        modelBuilder.ApplyConfiguration(new BankGlobalParametersConfiguration());
        modelBuilder.ApplyConfiguration(new BackValueTransactionRequestConfiguration());
        modelBuilder.ApplyConfiguration(new BackValueAuditJournalEntryConfiguration());
        modelBuilder.ApplyConfiguration(new LinkedTransactionReversalRecordConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentInstrumentConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentInstrumentStockBookConfiguration());

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
            modelBuilder.Entity<MudarabaInvestmentPool>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<AlWadiahAccount>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<MurabahaFinancingFacility>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<QuardFinancingFacility>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<ShariahTaWidhCharityFund>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<AgriculturalLoanFacility>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<BillDiscountingFacility>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<DepositMultiplicationAccount>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<InterBranchPaymentInstrument>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<DemandLoanFacility>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<SecurityDepositReceipt>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<BankGuarantee>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<ReconciliationAgingItem>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<BranchVault>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<MultiAccountSweepRule>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<AccountRestraint>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<BulkCorporateBatch>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<AccountLedgerEntry>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<StatutoryCertificateRecord>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<MasterExportLc>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<TradeTransactionProfile>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<TbmlPriceBenchmark>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<TbmlAlertRecord>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<VesselTrackingRecord>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<LoanRescheduleRecord>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<LoanWriteOffRecord>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<QualitativeJudgmentRecord>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<TreasuryLimitProfile>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<MtmRevaluationBatch>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<NostroMirrorReconciliationItem>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<StatutoryReserveAssessment>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<CustomerRiskAssessment>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<SanctionScreeningRecord>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<FatcaComplianceProfile>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<BorrowerIcrrAssessment>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<ChequeDishonourRecord>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<BillForCollection>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<InlandBillPurchased>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<CustomerTransactionProfile>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<AdcReconciliationBatch>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<TransactionAnomalyRecord>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<BreakGlassEmergencySession>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<DailyEodBalancingSheet>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<RemittanceCashIncentiveRecord>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<DailyCleanCashSheet>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<UserDoaProfile>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<TemporaryAuthorityDelegation>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<UserPasswordCredential>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<UserActiveSession>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<AdminChangeRequest>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<BankGlobalParameters>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<BackValueTransactionRequest>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<BackValueAuditJournalEntry>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<LinkedTransactionReversalRecord>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<PaymentInstrument>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<PaymentInstrumentStockBook>().Property<uint>("xmin").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();

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
            modelBuilder.Entity<MudarabaInvestmentPool>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<AlWadiahAccount>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<MurabahaFinancingFacility>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<QuardFinancingFacility>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<ShariahTaWidhCharityFund>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<AgriculturalLoanFacility>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<BillDiscountingFacility>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<DepositMultiplicationAccount>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<InterBranchPaymentInstrument>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<DemandLoanFacility>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<SecurityDepositReceipt>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<BankGuarantee>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<ReconciliationAgingItem>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<BranchVault>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<MultiAccountSweepRule>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<AccountRestraint>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<BulkCorporateBatch>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<AccountLedgerEntry>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<StatutoryCertificateRecord>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<MasterExportLc>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<TradeTransactionProfile>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<TbmlPriceBenchmark>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<TbmlAlertRecord>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<VesselTrackingRecord>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<LoanRescheduleRecord>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<LoanWriteOffRecord>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<QualitativeJudgmentRecord>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<TreasuryLimitProfile>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<MtmRevaluationBatch>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<NostroMirrorReconciliationItem>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<StatutoryReserveAssessment>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<CustomerRiskAssessment>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<SanctionScreeningRecord>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<FatcaComplianceProfile>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<BorrowerIcrrAssessment>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<ChequeDishonourRecord>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<BillForCollection>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<InlandBillPurchased>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<CustomerTransactionProfile>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<AdcReconciliationBatch>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<TransactionAnomalyRecord>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<BreakGlassEmergencySession>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<DailyEodBalancingSheet>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<RemittanceCashIncentiveRecord>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<DailyCleanCashSheet>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<UserDoaProfile>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<TemporaryAuthorityDelegation>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<UserPasswordCredential>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<UserActiveSession>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<AdminChangeRequest>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<BankGlobalParameters>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<BackValueTransactionRequest>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<BackValueAuditJournalEntry>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<LinkedTransactionReversalRecord>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<PaymentInstrument>().Property<byte[]>("RowVersion").IsConcurrencyToken();
            modelBuilder.Entity<PaymentInstrumentStockBook>().Property<byte[]>("RowVersion").IsConcurrencyToken();

            modelBuilder.Entity<UserSession>()
                .HasIndex(s => s.UserId)
                .IsUnique()
                .HasDatabaseName("ux_active_user_session")
                .HasFilter("is_active = 1");
        }
    }
}
