using CBS.Application.Accounts.Commands;
using CBS.Application.Accounts.Services;
using CBS.Application.BranchCash;
using CBS.Application.BranchCash.Commands;
using CBS.Application.Common.Interfaces;
using CBS.Application.Corporate.Commands;
using CBS.Application.Customers;
using CBS.Application.GeneralLedger.Services;
using CBS.Application.Lending.Demand;
using CBS.Application.Lending.Demand.Commands;
using CBS.Application.Remittance;
using CBS.Application.Reporting;
using CBS.Application.Security;
using CBS.Application.Shariah;
using CBS.Application.Shariah.Commands;
using CBS.Application.Specialized;
using CBS.Application.Specialized.Commands;
using CBS.Application.TradeFinance;
using CBS.Application.TradeFinance.Commands;
using CBS.Application.Reconciliation;
using CBS.Application.Reconciliation.Services;
using CBS.Application.StandingInstructions.Commands;
using CBS.Application.Training;
using CBS.Application.Training.Commands;
using CBS.Application.Training.Services;
using CBS.Infrastructure.Accounts;
using CBS.Infrastructure.GeneralLedger;
using CBS.Infrastructure.Persistence;
using CBS.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CBS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCbsPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var pgConnectionString = configuration.GetConnectionString("PostgreSql")
                                 ?? configuration["PostgreSql:ConnectionString"];

        if (!string.IsNullOrWhiteSpace(pgConnectionString))
        {
            services.AddDbContext<CbsDbContext>(options =>
            {
                options.UseNpgsql(pgConnectionString);
            });
        }
        else
        {
            // Resilient In-Memory SQLite Relational Database:
            // An open SqliteConnection as Singleton ensures isolated in-memory DB per WebApp instance
            // without Windows file locking or dirty state between tests.
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            services.AddSingleton(connection);

            services.AddDbContext<CbsDbContext>((sp, options) =>
            {
                var conn = sp.GetRequiredService<SqliteConnection>();
                options.UseSqlite(conn);
            });
        }

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IAccountRepository, EfAccountRepository>();
        services.AddScoped<ITellerDrawerRepository, EfTellerDrawerRepository>();
        services.AddScoped<ICustomerRepository, EfCustomerRepository>();
        services.AddScoped<IFinancialVoucherRepository, EfFinancialVoucherRepository>();
        services.AddScoped<IChartOfAccountsRepository, EfChartOfAccountsRepository>();
        services.AddScoped<IGeneralLedgerService, GeneralLedgerService>();
        services.AddScoped<IDepositLifecycleService, DepositLifecycleService>();
        services.AddScoped<ILoanAccountRepository, EfLoanAccountRepository>();
        services.AddScoped<ITradeFinanceRepository, EfTradeFinanceRepository>();
        services.AddScoped<ITreasuryRepository, EfTreasuryRepository>();
        services.AddScoped<ISecurityAuditRepository, EfSecurityAuditRepository>();
        services.AddScoped<IAiKnowledgeRepository, EfAiKnowledgeRepository>();
        services.AddScoped<IAdcRepository, EfAdcRepository>();
        services.AddScoped<IReconciliationRepository, EfReconciliationRepository>();
        services.AddScoped<IChequeClearingRepository, EfChequeClearingRepository>();
        services.AddScoped<IBeftnRepository, EfBeftnRepository>();
        services.AddScoped<IRtgsRepository, EfRtgsRepository>();
        services.AddScoped<IRemittanceInstrumentRepository, EfRemittanceInstrumentRepository>();
        services.AddScoped<ISecurityStationeryRepository, EfSecurityStationeryRepository>();
        services.AddScoped<IBranchOperatingDayRepository, EfBranchOperatingDayRepository>();
        services.AddScoped<IStandingInstructionRepository, EfStandingInstructionRepository>();
        services.AddScoped<IChequeBookRepository, EfChequeBookRepository>();
        services.AddScoped<ITermDepositRepository, EfTermDepositRepository>();
        services.AddScoped<IDpsRepository, EfDpsRepository>();
        services.AddScoped<IDepositLienRepository, EfDepositLienRepository>();
        services.AddScoped<IForeignRemittanceRepository, EfForeignRemittanceRepository>();
        services.AddScoped<ICrmRepository, EfCrmRepository>();
        services.AddScoped<ISystemAdminRepository, EfSystemAdminRepository>();
        services.AddScoped<IMisReportingRepository, EfMisReportingRepository>();
        services.AddScoped<ITrainingAndAwarenessRepository, EfTrainingAndAwarenessRepository>();
        services.AddScoped<TrainingAndAwarenessCommandHandler>();
        services.AddScoped<BbrpdMasterComplianceService>();
        services.AddScoped<IShariahComplianceRepository, EfShariahComplianceRepository>();
        services.AddScoped<ShariahCommandHandler>();
        services.AddScoped<ISpecializedBankingRepository, EfSpecializedBankingRepository>();
        services.AddScoped<SpecializedBankingCommandHandler>();
        services.AddScoped<IDemandLoanRepository, EfDemandLoanRepository>();
        services.AddScoped<DemandLoanCommandHandler>();
        services.AddScoped<ISecurityDepositReceiptRepository, EfSecurityDepositReceiptRepository>();
        services.AddScoped<SecurityDepositReceiptCommandHandler>();
        services.AddScoped<IBankGuaranteeRepository, EfBankGuaranteeRepository>();
        services.AddScoped<BankGuaranteeCommandHandler>();
        services.AddScoped<IReconciliationAgingRepository, EfReconciliationAgingRepository>();
        services.AddScoped<ReconciliationAgingService>();
        services.AddScoped<IBranchVaultRepository, EfBranchVaultRepository>();
        services.AddScoped<BranchVaultCommandHandler>();
        services.AddScoped<MultiAccountSweepCommandHandler>();
        services.AddScoped<IAccountRestraintRepository, EfAccountRestraintRepository>();
        services.AddScoped<AccountRestraintCommandHandler>();
        services.AddScoped<AccountClosingCommandHandler>();
        services.AddScoped<IBulkCorporateBatchRepository, EfBulkCorporateBatchRepository>();
        services.AddScoped<BulkCorporateCommandHandler>();
        services.AddHttpClient();

        return services;
    }

    public static void InitializeCbsDatabase(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CbsDbContext>();
        dbContext.Database.EnsureCreated();

        var coaRepo = scope.ServiceProvider.GetRequiredService<IChartOfAccountsRepository>();
        coaRepo.SeedDefaultAccountsAsync().GetAwaiter().GetResult();

        var aiRepo = scope.ServiceProvider.GetRequiredService<IAiKnowledgeRepository>() as EfAiKnowledgeRepository;
        aiRepo?.EnsureSeededAsync().GetAwaiter().GetResult();

        // Seed default ATM terminal for ADC testing
        var adcRepo = scope.ServiceProvider.GetRequiredService<IAdcRepository>();
        var existingAtm = adcRepo.GetTerminalByIdAsync("ATM-001").GetAwaiter().GetResult();
        if (existingAtm is null)
        {
            var defaultAtm = new CBS.Domain.Adc.AtmTerminal("ATM-001", "Motijheel Main Branch ATM 01", "0101", "Dhaka", 1000000m, 5000000m);
            adcRepo.AddTerminalAsync(defaultAtm).GetAwaiter().GetResult();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            uow.SaveChangesAsync().GetAwaiter().GetResult();
        }
    }
}
