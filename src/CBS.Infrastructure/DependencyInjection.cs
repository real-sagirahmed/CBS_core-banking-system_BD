using CBS.Application.Accounts.Services;
using CBS.Application.Common.Interfaces;
using CBS.Application.GeneralLedger.Services;
using CBS.Infrastructure.Accounts;
using CBS.Infrastructure.GeneralLedger;
using CBS.Infrastructure.Persistence;
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

        return services;
    }

    public static void InitializeCbsDatabase(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CbsDbContext>();
        dbContext.Database.EnsureCreated();

        var coaRepo = scope.ServiceProvider.GetRequiredService<IChartOfAccountsRepository>();
        coaRepo.SeedDefaultAccountsAsync().GetAwaiter().GetResult();
    }
}
