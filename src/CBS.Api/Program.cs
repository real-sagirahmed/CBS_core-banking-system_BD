using CBS.Application.Accounts.Commands.OpenDepositAccount;
using CBS.Application.Accounts.Commands.PostTransaction;
using CBS.Application.Accounts.DTOs;
using CBS.Application.Accounts.Services;
using CBS.Application.AiIntelligence.Commands;
using CBS.Application.AiIntelligence.DTOs;
using CBS.Application.BranchCash.Commands.CloseTellerDrawer;
using CBS.Application.BranchCash.Commands.IssueRemittanceInstrument;
using CBS.Application.BranchCash.Commands.LiquidateRemittanceInstrument;
using CBS.Application.BranchCash.Commands.ManageRemittanceInstrument;
using CBS.Application.BranchCash.Commands.OpenTellerDrawer;
using CBS.Application.BranchCash.Commands.ProcessCashTransaction;
using CBS.Application.BranchCash.DTOs;
using CBS.Application.Common.Interfaces;
using CBS.Application.Customers.Commands.CreateCustomerDraft;
using CBS.Application.Customers.Commands.VerifyCustomer;
using CBS.Application.Customers.DTOs;
using CBS.Application.GeneralBanking.Commands.RunBranchEodBatch;
using CBS.Application.GeneralLedger.DTOs;
using CBS.Application.GeneralLedger.Services;
using CBS.Application.Lending.Commands;
using CBS.Application.Lending.DTOs;
using CBS.Application.PaymentRails.Commands.InitiateBeftn;
using CBS.Application.PaymentRails.Commands.PresentCheque;
using CBS.Application.PaymentRails.Commands.ProcessInwardCheque;
using CBS.Application.PaymentRails.Commands.Rtgs;
using CBS.Application.PaymentRails.DTOs;
using CBS.Application.SecurityAudit.Commands;
using CBS.Application.SecurityAudit.DTOs;
using CBS.Application.TradeFinance.Commands;
using CBS.Application.TradeFinance.DTOs;
using CBS.Application.Treasury.Commands;
using CBS.Application.Treasury.DTOs;
using CBS.Domain.Accounts;
using CBS.Domain.BranchCash;
using CBS.Domain.Exceptions;
using CBS.Domain.GeneralLedger;
using CBS.Domain.Lending;
using CBS.Domain.AiIntelligence;
using CBS.Domain.PaymentRails;
using CBS.Domain.SecurityAudit;
using CBS.Domain.TradeFinance;
using CBS.Domain.Treasury;
using CBS.Domain.ValueObjects;
using CBS.Infrastructure;
using CBS.Infrastructure.AiIntelligence;
using CBS.Infrastructure.Gateways;
using CBS.Infrastructure.Persistence;
using CBS.Infrastructure.Security;
using CBS.Application.Common.Security;
using CBS.Api.Filters;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var app = Program.CreateWebApplication(args);
app.Run();

public partial class Program
{
    public static WebApplication CreateWebApplication(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // 1. Register Core Infrastructure & Application Services
        builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtTokenService.DefaultSecretKey)),
                    ValidateIssuer = true,
                    ValidIssuer = JwtTokenService.Issuer,
                    ValidateAudience = true,
                    ValidAudience = JwtTokenService.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });
        builder.Services.AddAuthorization();
        builder.Services.AddCbsPersistence(builder.Configuration);
        builder.Services.AddSingleton<IBranchOperatingDayRepository, InMemoryBranchOperatingDayRepository>();
        builder.Services.AddSingleton<IRemittanceInstrumentRepository, InMemoryRemittanceInstrumentRepository>();
        builder.Services.AddSingleton<ISecurityStationeryRepository, InMemorySecurityStationeryRepository>();
        builder.Services.AddSingleton<IChequeClearingRepository, InMemoryChequeClearingRepository>();
        builder.Services.AddSingleton<IBeftnRepository, InMemoryBeftnRepository>();
        builder.Services.AddSingleton<IRtgsRepository, InMemoryRtgsRepository>();
        builder.Services.AddSingleton<ILoanAccountRepository, InMemoryLoanAccountRepository>();
        builder.Services.AddSingleton<ITradeFinanceRepository, InMemoryTradeFinanceRepository>();
        builder.Services.AddSingleton<ITreasuryRepository, InMemoryTreasuryRepository>();
        builder.Services.AddSingleton<ISecurityAuditRepository, InMemorySecurityAuditRepository>();
        builder.Services.AddSingleton<IAiKnowledgeRepository, InMemoryAiKnowledgeRepository>();
        builder.Services.AddSingleton<IGeminiAiService, GeminiAiService>();
        builder.Services.AddSingleton<IElectionCommissionGateway, MockElectionCommissionGateway>();
        builder.Services.AddSingleton<ISanctionScreeningService, MockSanctionScreeningService>();

        // 2. Register Application Command Handlers
        builder.Services.AddTransient<CreateCustomerDraftCommandHandler>();
        builder.Services.AddTransient<VerifyCustomerCommandHandler>();
        builder.Services.AddTransient<OpenDepositAccountCommandHandler>();
        builder.Services.AddTransient<PostTransactionCommandHandler>();
        builder.Services.AddTransient<RunBranchEodBatchCommandHandler>();
        builder.Services.AddTransient<OpenTellerDrawerCommandHandler>();
        builder.Services.AddTransient<ProcessTellerCashTransactionCommandHandler>();
        builder.Services.AddTransient<CloseTellerDrawerCommandHandler>();
        builder.Services.AddTransient<IssueRemittanceInstrumentCommandHandler>();
        builder.Services.AddTransient<LiquidateRemittanceInstrumentCommandHandler>();
        builder.Services.AddTransient<ManageRemittanceInstrumentLifecycleCommandHandler>();
        builder.Services.AddTransient<PresentOutwardChequeCommandHandler>();
        builder.Services.AddTransient<ProcessInwardChequeClearingCommandHandler>();
        builder.Services.AddTransient<InitiateBeftnTransferCommandHandler>();
        builder.Services.AddTransient<RtgsCommandHandler>();
        builder.Services.AddTransient<LendingCommandHandler>();
        builder.Services.AddTransient<TradeFinanceCommandHandler>();
        builder.Services.AddTransient<TreasuryCommandHandler>();
        builder.Services.AddTransient<SecurityAuditCommandHandler>();
        builder.Services.AddTransient<AiIntelligenceCommandHandler>();

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
        });

        var app = builder.Build();
        app.UseDeveloperExceptionPage();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.UseAuthentication();
        app.UseAuthorization();

        // --- CUSTOMER APIS ---
        var customerApi = app.MapGroup("/api/v1/customers");

        customerApi.MapPost("/draft", async (
            CreateCustomerDraftRequest request,
            HttpContext httpContext,
            CreateCustomerDraftCommandHandler handler,
            CancellationToken ct) =>
        {
            string makerUserId = httpContext.Request.Headers["X-User-Id"].FirstOrDefault() ?? "MAKER01";
            string branchCode = httpContext.Request.Headers["X-Branch-Code"].FirstOrDefault() ?? "001";

            try
            {
                var command = new CreateCustomerDraftCommand(request, makerUserId, branchCode);
                var result = await handler.HandleAsync(command, ct);
                return Results.Created($"/api/v1/customers/{result.CifNumber}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        customerApi.MapPost("/{cif}/verify", async (
            string cif,
            VerifyCustomerRequest request,
            HttpContext httpContext,
            VerifyCustomerCommandHandler handler,
            CancellationToken ct) =>
        {
            string checkerUserId = httpContext.Request.Headers["X-User-Id"].FirstOrDefault() ?? "CHECKER01";

            try
            {
                var command = new VerifyCustomerCommand(cif, checkerUserId, request.IsApproved, request.RejectionReason);
                var result = await handler.HandleAsync(command, ct);
                return Results.Ok(result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Balance modified by another transaction.", details = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Database Concurrency Conflict: Resource locked by another transaction.", details = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Conflict(new { error = "Database Transaction Contention: Resource busy or transaction conflict.", details = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        customerApi.MapGet("/{cif}", async (string cif, ICustomerRepository repo, CancellationToken ct) =>
        {
            var customer = await repo.GetByCifAsync(new CifNumber(cif), ct);
            if (customer is null) return Results.NotFound();

            return Results.Ok(new CustomerResponseDto(
                customer.Id.Value,
                customer.FullName,
                customer.Type.ToString(),
                customer.RiskLevel.ToString(),
                customer.IsActive,
                customer.IsElectionCommissionVerified,
                customer.ComputeDeduplicationFingerprint(),
                customer.NationalId?.Value,
                customer.LegalGuardianCif?.Value));
        });

        customerApi.MapPost("/ec-verify", async (
            EcVerificationRequest req,
            IElectionCommissionGateway gateway,
            CancellationToken ct) =>
        {
            var result = await gateway.VerifyAsync(req, ct);
            return Results.Ok(result);
        });

        // --- ACCOUNT APIS (PHASE 2) ---
        var accountApi = app.MapGroup("/api/v1/accounts").AddEndpointFilter<FinancialAuthorizationFilter>();

        accountApi.MapPost("", async (
            OpenDepositAccountRequest request,
            HttpContext httpContext,
            OpenDepositAccountCommandHandler handler,
            CancellationToken ct) =>
        {
            string makerUserId = httpContext.Request.Headers["X-User-Id"].FirstOrDefault() ?? "MAKER01";
            string branchCode = httpContext.Request.Headers["X-Branch-Code"].FirstOrDefault() ?? "001";

            try
            {
                var command = new OpenDepositAccountCommand(request, makerUserId, branchCode);
                var result = await handler.HandleAsync(command, ct);
                return Results.Created($"/api/v1/accounts/{result.AccountNumber}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Balance modified by another transaction.", details = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Database Concurrency Conflict: Resource locked by another transaction.", details = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Conflict(new { error = "Database Transaction Contention: Resource busy or transaction conflict.", details = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        accountApi.MapGet("/{accountNumber}", async (string accountNumber, IAccountRepository repo, CancellationToken ct) =>
        {
            var acc = await repo.GetByAccountNumberAsync(AccountNumber.From(accountNumber), ct);
            if (acc is null) return Results.NotFound();

            return Results.Ok(new AccountResponseDto(
                acc.Id.Value,
                acc.Cif.Value,
                acc.ProductType.ToString(),
                acc.Status.ToString(),
                acc.Balance.Amount,
                acc.Balance.Currency.Code,
                acc.HasTin,
                acc.BranchCode));
        });

        accountApi.MapPost("/{accountNumber}/transactions", async (
            string accountNumber,
            PostTransactionRequest request,
            HttpContext httpContext,
            PostTransactionCommandHandler handler,
            CancellationToken ct) =>
        {
            string userId = httpContext.Request.Headers["X-User-Id"].FirstOrDefault() ?? "TELLER01";
            string branchCode = httpContext.Request.Headers["X-Branch-Code"].FirstOrDefault() ?? "001";

            try
            {
                var adjustedRequest = request with { AccountNumber = accountNumber };
                var command = new PostTransactionCommand(adjustedRequest, userId, branchCode);
                var result = await handler.HandleAsync(command, ct);
                return Results.Ok(result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Balance modified by another transaction.", details = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Database Concurrency Conflict: Resource locked by another transaction.", details = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Conflict(new { error = "Database Transaction Contention: Resource busy or transaction conflict.", details = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        accountApi.MapPost("/{accountNumber}/reactivate-dormant", async (
            string accountNumber,
            ReactivateDormantRequest request,
            HttpContext httpContext,
            IAccountRepository repo,
            CancellationToken ct) =>
        {
            string branchManagerUserId = httpContext.Request.Headers["X-User-Id"].FirstOrDefault() ?? "BM01";

            var acc = await repo.GetByAccountNumberAsync(AccountNumber.From(accountNumber), ct);
            if (acc is null) return Results.NotFound();

            try
            {
                acc.ReactivateDormant(branchManagerUserId, request.Remarks);
                await repo.UpdateAsync(acc, ct);

                return Results.Ok(new AccountResponseDto(
                    acc.Id.Value,
                    acc.Cif.Value,
                    acc.ProductType.ToString(),
                    acc.Status.ToString(),
                    acc.Balance.Amount,
                    acc.Balance.Currency.Code,
                    acc.HasTin,
                    acc.BranchCode));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // Maker requests dormancy reactivation (BRPD 3.1.20-iv, 1.3.18)
        accountApi.MapPost("/{accountNumber}/request-reactivation", async (
            string accountNumber,
            RequestDormancyReactivationRequest request,
            HttpContext httpContext,
            IDepositLifecycleService lifecycleService,
            CancellationToken ct) =>
        {
            string makerUserId = httpContext.Request.Headers["X-User-Id"].FirstOrDefault() ?? "MAKER01";

            try
            {
                await lifecycleService.RequestDormancyReactivationAsync(
                    AccountNumber.From(accountNumber),
                    makerUserId,
                    request.KycDocumentReference,
                    request.Remarks,
                    ct);

                return Results.Ok(new { message = $"Dormancy reactivation requested for account '{accountNumber}'. Pending Branch Manager approval.", maker = makerUserId });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // Checker (Branch Manager) approves dormancy reactivation (BRPD 3.1.20-iv, 10.3.5-x)
        accountApi.MapPost("/{accountNumber}/approve-reactivation", async (
            string accountNumber,
            ApproveDormancyReactivationRequest request,
            HttpContext httpContext,
            IDepositLifecycleService lifecycleService,
            CancellationToken ct) =>
        {
            string branchManagerUserId = httpContext.Request.Headers["X-Approver-Id"].FirstOrDefault()
                                         ?? httpContext.Request.Headers["X-User-Id"].FirstOrDefault()
                                         ?? "BM01";

            try
            {
                var response = await lifecycleService.ApproveDormancyReactivationAsync(
                    AccountNumber.From(accountNumber),
                    branchManagerUserId,
                    request.Remarks,
                    ct);

                return Results.Ok(response);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // Deposit Lifecycle EOD Batch execution (Dormancy, Inoperative, 10-Yr Unclaimed Transfer, Leap-Year Interest Accrual)
        accountApi.MapPost("/lifecycle/eod-batch", async (
            DepositLifecycleEodRequest request,
            HttpContext httpContext,
            IDepositLifecycleService lifecycleService,
            CancellationToken ct) =>
        {
            string userId = httpContext.Request.Headers["X-User-Id"].FirstOrDefault() ?? "OPS_OFFICER_01";
            var businessDate = string.IsNullOrWhiteSpace(request.BusinessDate)
                ? DateOnly.FromDateTime(DateTime.UtcNow)
                : DateOnly.Parse(request.BusinessDate);

            try
            {
                var summary = await lifecycleService.ExecuteBranchDepositEodAsync(
                    request.BranchCode,
                    businessDate,
                    userId,
                    ct);

                return Results.Ok(summary);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                var entryNames = string.Join(", ", ex.Entries.Select(e => $"{e.Entity.GetType().Name} ({e.State}): {string.Join(";", e.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name))}"));
                return Results.BadRequest(new { error = ex.Message, entries = entryNames });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // Deposit Periodic Interest Capitalization & NBR Withholding Tax Deductions (BRPD 1.2.2, 3.1.19, 3.2.2)
        accountApi.MapPost("/lifecycle/capitalize-interest", async (
            DepositInterestCapitalizationRequest request,
            HttpContext httpContext,
            IDepositLifecycleService lifecycleService,
            CancellationToken ct) =>
        {
            string userId = httpContext.Request.Headers["X-User-Id"].FirstOrDefault() ?? "OPS_OFFICER_01";
            var businessDate = string.IsNullOrWhiteSpace(request.BusinessDate)
                ? DateOnly.FromDateTime(DateTime.UtcNow)
                : DateOnly.Parse(request.BusinessDate);

            try
            {
                var result = await lifecycleService.ExecuteInterestCapitalizationBatchAsync(
                    request.BranchCode,
                    businessDate,
                    userId,
                    ct);

                return Results.Ok(result);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                var entryNames = string.Join(", ", ex.Entries.Select(e => $"{e.Entity.GetType().Name} ({e.State}): {string.Join(";", e.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name))}"));
                return Results.BadRequest(new { error = ex.Message, entries = entryNames });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // Regulatory Unclaimed Accounts Ageing Report (BRPD 3.1.21-iv)
        accountApi.MapGet("/reports/unclaimed-ageing", async (
            string? branchCode,
            string? asOfDate,
            IDepositLifecycleService lifecycleService,
            CancellationToken ct) =>
        {
            var date = string.IsNullOrWhiteSpace(asOfDate)
                ? DateOnly.FromDateTime(DateTime.UtcNow)
                : DateOnly.Parse(asOfDate);

            var report = await lifecycleService.GetUnclaimedAccountsAgeingReportAsync(branchCode, date, ct);
            return Results.Ok(report);
        });

        // --- GENERAL BANKING & EOD APIS (PHASE 2) ---
        var eodApi = app.MapGroup("/api/v1/eod");

        eodApi.MapPost("/batch", async (
            EodBatchRequest request,
            HttpContext httpContext,
            RunBranchEodBatchCommandHandler handler,
            CancellationToken ct) =>
        {
            string userId = httpContext.Request.Headers["X-User-Id"].FirstOrDefault() ?? "EOD_ADMIN";

            try
            {
                var command = new RunBranchEodBatchCommand(request.BranchCode, userId);
                var result = await handler.HandleAsync(command, ct);
                return Results.Ok(result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // Test hook to configure pending vouchers for branch
        eodApi.MapPost("/set-pending-vouchers", async (
            SetPendingVouchersRequest request,
            IBranchOperatingDayRepository repo,
            CancellationToken ct) =>
        {
            await repo.SetPendingVouchersCountAsync(request.BranchCode, request.PendingCount, ct);
            return Results.Ok(new { message = $"Pending vouchers set to {request.PendingCount} for branch {request.BranchCode}" });
        });

        // --- BRANCH CASH & TELLER APIS (PHASE 3) ---
        var tellerApi = app.MapGroup("/api/v1/teller").AddEndpointFilter<FinancialAuthorizationFilter>();

        tellerApi.MapPost("/drawers/open", async (
            OpenTellerDrawerRequest request,
            HttpContext httpContext,
            OpenTellerDrawerCommandHandler handler,
            CancellationToken ct) =>
        {
            string supervisorUserId = httpContext.Request.Headers["X-Supervisor-Id"].FirstOrDefault() ?? "SUPERVISOR01";

            try
            {
                var command = new OpenTellerDrawerCommand(request, supervisorUserId);
                var result = await handler.HandleAsync(command, ct);
                return Results.Created($"/api/v1/teller/drawers/{result.DrawerId}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        tellerApi.MapGet("/drawers/{drawerId:guid}", async (
            Guid drawerId,
            ITellerDrawerRepository repo,
            CancellationToken ct) =>
        {
            var drawer = await repo.GetByIdAsync(drawerId, ct);
            if (drawer is null) return Results.NotFound();

            return Results.Ok(new TellerDrawerResponseDto(
                drawer.Id,
                drawer.BranchCode,
                drawer.CashierUserId,
                drawer.OperatingDate,
                drawer.CashierPostingLimit.Amount,
                drawer.CurrentBalance.Amount,
                drawer.Status,
                drawer.Denominations.Counts.ToDictionary(kv => kv.Key, kv => kv.Value),
                drawer.OpeningSupervisorUserId,
                drawer.ClosingManagerUserId,
                drawer.DiscrepancyAmount));
        });

        tellerApi.MapPost("/transactions", async (
            ProcessCashTransactionRequest request,
            HttpContext httpContext,
            ProcessTellerCashTransactionCommandHandler handler,
            CancellationToken ct) =>
        {
            string cashierUserId = httpContext.Request.Headers["X-Cashier-Id"].FirstOrDefault() ?? "CASHIER01";

            try
            {
                var command = new ProcessTellerCashTransactionCommand(request, cashierUserId);
                var result = await handler.HandleAsync(command, ct);
                return Results.Ok(result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Balance modified by another transaction.", details = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Database Concurrency Conflict: Resource locked by another transaction.", details = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Conflict(new { error = "Database Transaction Contention: Resource busy or transaction conflict.", details = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        tellerApi.MapPost("/drawers/close", async (
            CloseTellerDrawerRequest request,
            HttpContext httpContext,
            CloseTellerDrawerCommandHandler handler,
            CancellationToken ct) =>
        {
            string managerUserId = httpContext.Request.Headers["X-Branch-Manager-Id"].FirstOrDefault() ?? "BRANCH_MGR01";

            try
            {
                var command = new CloseTellerDrawerCommand(request, managerUserId);
                var result = await handler.HandleAsync(command, ct);
                return Results.Ok(result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Balance modified by another transaction.", details = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Database Concurrency Conflict: Resource locked by another transaction.", details = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Conflict(new { error = "Database Transaction Contention: Resource busy or transaction conflict.", details = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // --- REMITTANCE & INSTRUMENTS APIS (PHASE 3) ---
        var remittanceApi = app.MapGroup("/api/v1/remittance").AddEndpointFilter<FinancialAuthorizationFilter>();

        remittanceApi.MapPost("/issue", async (
            IssueRemittanceInstrumentRequest request,
            HttpContext httpContext,
            IssueRemittanceInstrumentCommandHandler handler,
            CancellationToken ct) =>
        {
            string makerUserId = httpContext.Request.Headers["X-Maker-Id"].FirstOrDefault() ?? "MAKER01";
            string checkerUserId = httpContext.Request.Headers["X-Checker-Id"].FirstOrDefault() ?? "CHECKER01";

            try
            {
                var command = new IssueRemittanceInstrumentCommand(request, makerUserId, checkerUserId);
                var result = await handler.HandleAsync(command, ct);
                return Results.Created($"/api/v1/remittance/{result.InstrumentNumber}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        remittanceApi.MapGet("/{instrumentNumber}", async (
            string instrumentNumber,
            IRemittanceInstrumentRepository repo,
            CancellationToken ct) =>
        {
            var inst = await repo.GetByInstrumentNumberAsync(instrumentNumber, ct);
            if (inst is null) return Results.NotFound();

            return Results.Ok(IssueRemittanceInstrumentCommandHandler.MapToDto(inst));
        });

        remittanceApi.MapPost("/liquidate", async (
            LiquidateInstrumentRequest request,
            HttpContext httpContext,
            LiquidateRemittanceInstrumentCommandHandler handler,
            CancellationToken ct) =>
        {
            string tellerUserId = httpContext.Request.Headers["X-Teller-Id"].FirstOrDefault() ?? "TELLER01";

            try
            {
                var command = new LiquidateRemittanceInstrumentCommand(request, tellerUserId);
                var result = await handler.HandleAsync(command, ct);
                return Results.Ok(result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Balance modified by another transaction.", details = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Database Concurrency Conflict: Resource locked by another transaction.", details = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Conflict(new { error = "Database Transaction Contention: Resource busy or transaction conflict.", details = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        remittanceApi.MapPost("/stop-payment", async (
            StopPaymentRequest request,
            HttpContext httpContext,
            ManageRemittanceInstrumentLifecycleCommandHandler handler,
            CancellationToken ct) =>
        {
            string supervisorUserId = httpContext.Request.Headers["X-Supervisor-Id"].FirstOrDefault() ?? "SUPERVISOR01";

            try
            {
                var command = new StopRemittanceInstrumentCommand(request, supervisorUserId);
                var result = await handler.HandleStopAsync(command, ct);
                return Results.Ok(result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Balance modified by another transaction.", details = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Database Concurrency Conflict: Resource locked by another transaction.", details = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Conflict(new { error = "Database Transaction Contention: Resource busy or transaction conflict.", details = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        remittanceApi.MapPost("/cancel", async (
            CancelInstrumentRequest request,
            HttpContext httpContext,
            ManageRemittanceInstrumentLifecycleCommandHandler handler,
            CancellationToken ct) =>
        {
            string supervisorUserId = httpContext.Request.Headers["X-Supervisor-Id"].FirstOrDefault() ?? "SUPERVISOR01";

            try
            {
                var command = new CancelRemittanceInstrumentCommand(request, supervisorUserId);
                var result = await handler.HandleCancelAsync(command, ct);
                return Results.Ok(result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Balance modified by another transaction.", details = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Database Concurrency Conflict: Resource locked by another transaction.", details = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Conflict(new { error = "Database Transaction Contention: Resource busy or transaction conflict.", details = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        remittanceApi.MapPost("/stationery/seed", async (
            SeedStationeryRequest request,
            ISecurityStationeryRepository repo,
            CancellationToken ct) =>
        {
            try
            {
                var inventory = new SecurityStationeryInventory(
                    Guid.NewGuid(),
                    request.BranchCode,
                    request.Type,
                    request.Prefix,
                    request.StartLeaf,
                    request.EndLeaf,
                    request.ManagerUserId,
                    request.CustodianUserId);

                await repo.AddAsync(inventory, ct);
                return Results.Ok(new { message = $"Stationery book {request.Prefix} ({request.StartLeaf}-{request.EndLeaf}) registered." });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
        });

        // --- PAYMENT RAILS & CLEARING APIS (PHASE 4) ---
        var bachApi = app.MapGroup("/api/v1/clearing/bach").AddEndpointFilter<FinancialAuthorizationFilter>();

        bachApi.MapPost("/outward/present", async (
            PresentChequeRequest request,
            PresentOutwardChequeCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleAsync(new PresentOutwardChequeCommand(request), ct);
                return Results.Created($"/api/v1/clearing/bach/items/{result.ItemId}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        bachApi.MapPost("/inward/process", async (
            ProcessInwardChequeClearingRequest request,
            ProcessInwardChequeClearingCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleAsync(new ProcessInwardChequeClearingCommand(request), ct);
                return Results.Ok(result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        var beftnApi = app.MapGroup("/api/v1/clearing/beftn").AddEndpointFilter<FinancialAuthorizationFilter>();

        beftnApi.MapPost("/transfer", async (
            InitiateBeftnTransferRequest request,
            InitiateBeftnTransferCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleAsync(new InitiateBeftnTransferCommand(request), ct);
                return Results.Created($"/api/v1/clearing/beftn/items/{result.ItemId}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Balance modified by another transaction.", details = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Database Concurrency Conflict: Resource locked by another transaction.", details = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Conflict(new { error = "Database Transaction Contention: Resource busy or transaction conflict.", details = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        var rtgsApi = app.MapGroup("/api/v1/clearing/rtgs").AddEndpointFilter<FinancialAuthorizationFilter>();

        rtgsApi.MapPost("/transfer", async (
            InitiateRtgsTransferRequest request,
            RtgsCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleInitiateAsync(new InitiateRtgsTransferCommand(request), ct);
                return Results.Created($"/api/v1/clearing/rtgs/{result.EndToEndId}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Balance modified by another transaction.", details = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Database Concurrency Conflict: Resource locked by another transaction.", details = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Conflict(new { error = "Database Transaction Contention: Resource busy or transaction conflict.", details = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        rtgsApi.MapPost("/settle", async (
            SettleRtgsRequest request,
            RtgsCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleSettleAsync(new SettleRtgsCommand(request), ct);
                return Results.Ok(result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Balance modified by another transaction.", details = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Database Concurrency Conflict: Resource locked by another transaction.", details = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Conflict(new { error = "Database Transaction Contention: Resource busy or transaction conflict.", details = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        rtgsApi.MapGet("/{endToEndId}", async (
            string endToEndId,
            IRtgsRepository repo,
            CancellationToken ct) =>
        {
            var tx = await repo.GetByEndToEndIdAsync(endToEndId, ct);
            if (tx is null) return Results.NotFound();

            return Results.Ok(RtgsCommandHandler.MapToDto(tx));
        });

        // --- LENDING & CREDIT FACILITIES APIS (PHASE 5) ---
        var lendingApi = app.MapGroup("/api/v1/lending").AddEndpointFilter<FinancialAuthorizationFilter>();

        lendingApi.MapPost("/accounts", async (
            CreateLoanAccountRequest request,
            HttpContext httpContext,
            LendingCommandHandler handler,
            CancellationToken ct) =>
        {
            string approverUserId = httpContext.Request.Headers["X-Approver-Id"].FirstOrDefault() ?? "CREDIT_COMMITTEE_01";

            try
            {
                var command = new CreateLoanAccountCommand(request, approverUserId);
                var result = await handler.HandleCreateAsync(command, ct);
                return Results.Created($"/api/v1/lending/accounts/{result.AccountNumber}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Balance modified by another transaction.", details = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Database Concurrency Conflict: Resource locked by another transaction.", details = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Conflict(new { error = "Database Transaction Contention: Resource busy or transaction conflict.", details = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        lendingApi.MapGet("/accounts/{accountNumber}", async (
            string accountNumber,
            ILoanAccountRepository repo,
            CancellationToken ct) =>
        {
            var loan = await repo.GetByAccountNumberAsync(AccountNumber.From(accountNumber), ct);
            if (loan is null) return Results.NotFound();

            return Results.Ok(LendingCommandHandler.MapToDto(loan));
        });

        lendingApi.MapPost("/accounts/vet-cad", async (
            VetCadChecklistRequest request,
            HttpContext httpContext,
            LendingCommandHandler handler,
            CancellationToken ct) =>
        {
            string cadOfficerUserId = httpContext.Request.Headers["X-Cad-Officer-Id"].FirstOrDefault() ?? "CAD_OFFICER_01";

            try
            {
                var command = new VetCadChecklistCommand(request, cadOfficerUserId);
                var result = await handler.HandleVetCadAsync(command, ct);
                return Results.Ok(result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Balance modified by another transaction.", details = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Database Concurrency Conflict: Resource locked by another transaction.", details = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Conflict(new { error = "Database Transaction Contention: Resource busy or transaction conflict.", details = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        lendingApi.MapPost("/accounts/disburse", async (
            DisburseLoanRequest request,
            HttpContext httpContext,
            LendingCommandHandler handler,
            CancellationToken ct) =>
        {
            string disbursingOfficerUserId = httpContext.Request.Headers["X-Disbursing-Officer-Id"].FirstOrDefault() ?? "BRANCH_DISBURSER_01";

            try
            {
                var command = new DisburseLoanCommand(request, disbursingOfficerUserId);
                var result = await handler.HandleDisburseAsync(command, ct);
                return Results.Ok(result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Balance modified by another transaction.", details = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Database Concurrency Conflict: Resource locked by another transaction.", details = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Conflict(new { error = "Database Transaction Contention: Resource busy or transaction conflict.", details = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        lendingApi.MapPost("/accounts/repayment", async (
            ProcessLoanRepaymentRequest request,
            LendingCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new ProcessLoanRepaymentCommand(request);
                var result = await handler.HandleRepaymentAsync(command, ct);
                return Results.Ok(result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Balance modified by another transaction.", details = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Database Concurrency Conflict: Resource locked by another transaction.", details = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Conflict(new { error = "Database Transaction Contention: Resource busy or transaction conflict.", details = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        lendingApi.MapPost("/accounts/evaluate-classification", async (
            EvaluateLoanClassificationRequest request,
            LendingCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new EvaluateLoanClassificationCommand(request);
                var result = await handler.HandleClassificationAsync(command, ct);
                return Results.Ok(result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Balance modified by another transaction.", details = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Database Concurrency Conflict: Resource locked by another transaction.", details = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Conflict(new { error = "Database Transaction Contention: Resource busy or transaction conflict.", details = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        lendingApi.MapPost("/accounts/eod-batch", async (
            LoanEodBatchRequest request,
            HttpContext httpContext,
            LendingCommandHandler handler,
            CancellationToken ct) =>
        {
            string userId = httpContext.Request.Headers["X-User-Id"].FirstOrDefault() ?? "OPS_OFFICER_01";

            try
            {
                var result = await handler.HandleLoanEodBatchAsync(request.BranchCode, userId, ct);
                return Results.Ok(result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Resource modified by another transaction.", details = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        lendingApi.MapGet("/reports/cl-statement", async (
            string? branchCode,
            LendingCommandHandler handler,
            CancellationToken ct) =>
        {
            var report = await handler.GetClReportAsync(branchCode, ct);
            return Results.Ok(report);
        });

        // --- TRADE FINANCE & TBML APIS (BRPD Circular 12 Module 5) ---
        var tradeApi = app.MapGroup("/api/v1/trade").AddEndpointFilter<FinancialAuthorizationFilter>();

        tradeApi.MapPost("/lc/draft", async (
            DraftDocumentaryCreditRequest request,
            TradeFinanceCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new DraftDocumentaryCreditCommand(request);
                var result = await handler.HandleDraftLcAsync(command, ct);
                return Results.Created($"/api/v1/trade/lc/{result.LcNumber}", result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        tradeApi.MapPost("/lc/authorize", async (
            AuthorizeDocumentaryCreditRequest request,
            TradeFinanceCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new AuthorizeDocumentaryCreditCommand(request);
                var result = await handler.HandleAuthorizeLcAsync(command, ct);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Balance modified by another transaction.", details = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Database Concurrency Conflict: Resource locked by another transaction.", details = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Conflict(new { error = "Database Transaction Contention: Resource busy or transaction conflict.", details = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        tradeApi.MapGet("/lc/{lcNumber}", async (
            string lcNumber,
            ITradeFinanceRepository repository,
            CancellationToken ct) =>
        {
            var lc = await repository.GetLcByNumberAsync(lcNumber, ct);
            if (lc is null)
                return Results.NotFound(new { error = $"Documentary Credit '{lcNumber}' not found." });

            var response = new DocumentaryCreditResponse(
                lc.LcNumber,
                lc.Type.ToString(),
                lc.ApplicantId,
                lc.BeneficiaryName,
                lc.BeneficiaryCountry,
                lc.Amount.Amount,
                lc.Amount.Currency.Code,
                lc.CashMarginAmount.Amount,
                lc.Status.ToString(),
                lc.CheckerUserId,
                lc.Mt700Message?.RawMessageText,
                lc.TbmlResult?.RiskRating.ToString(),
                lc.TbmlResult?.RiskSummary);

            return Results.Ok(response);
        });

        tradeApi.MapPost("/shipping-guarantee/issue", async (
            IssueShippingGuaranteeRequest request,
            TradeFinanceCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new IssueShippingGuaranteeCommand(request);
                var result = await handler.HandleIssueSgAsync(command, ct);
                return Results.Created($"/api/v1/trade/shipping-guarantee/{result.SgReference}", result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        tradeApi.MapPost("/shipping-guarantee/authorize", async (
            AuthorizeShippingGuaranteeRequest request,
            TradeFinanceCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new AuthorizeShippingGuaranteeCommand(request);
                var result = await handler.HandleAuthorizeSgAsync(command, ct);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Balance modified by another transaction.", details = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Database Concurrency Conflict: Resource locked by another transaction.", details = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Conflict(new { error = "Database Transaction Contention: Resource busy or transaction conflict.", details = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        tradeApi.MapPost("/shipping-guarantee/release-bl", async (
            ReleaseShippingGuaranteeRequest request,
            TradeFinanceCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new ReleaseShippingGuaranteeCommand(request);
                var result = await handler.HandleReleaseSgAsync(command, ct);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Balance modified by another transaction.", details = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Database Concurrency Conflict: Resource locked by another transaction.", details = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Conflict(new { error = "Database Transaction Contention: Resource busy or transaction conflict.", details = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        tradeApi.MapPost("/exp/register", async (
            RegisterExpRequest request,
            TradeFinanceCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new RegisterExpTrackingCommand(request);
                var result = await handler.HandleRegisterExpAsync(command, ct);
                return Results.Created($"/api/v1/trade/exp/{result.ExpNumber}", result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        tradeApi.MapPost("/exp/realize", async (
            RealizeExpRequest request,
            TradeFinanceCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new RealizeExpProceedsCommand(request);
                var result = await handler.HandleRealizeExpAsync(command, ct);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Balance modified by another transaction.", details = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Database Concurrency Conflict: Resource locked by another transaction.", details = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Conflict(new { error = "Database Transaction Contention: Resource busy or transaction conflict.", details = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        tradeApi.MapPost("/exp/evaluate-overdue", async (
            EvaluateExpOverdueRequest request,
            TradeFinanceCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new EvaluateExpOverdueCommand(request);
                var result = await handler.HandleEvaluateExpOverdueAsync(command, ct);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Balance modified by another transaction.", details = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Database Concurrency Conflict: Resource locked by another transaction.", details = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Conflict(new { error = "Database Transaction Contention: Resource busy or transaction conflict.", details = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        tradeApi.MapPost("/tbml/screen", async (
            TbmlScreeningRequest request,
            TradeFinanceCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new ScreenTbmlCommand(request);
                var result = await handler.HandleScreenTbmlAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // --- TREASURY & ALM APIS (BRPD Circular 12 Module 6) ---
        var treasuryApi = app.MapGroup("/api/v1/treasury").AddEndpointFilter<FinancialAuthorizationFilter>();

        treasuryApi.MapPost("/deals/capture", async (
            CaptureTreasuryDealRequest request,
            TreasuryCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new CaptureTreasuryDealCommand(request);
                var result = await handler.HandleCaptureAsync(command, ct);
                return Results.Created($"/api/v1/treasury/deals/{result.DealReference}", result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        treasuryApi.MapPost("/deals/verify-mid-office", async (
            VerifyMidOfficeDealRequest request,
            TreasuryCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new VerifyMidOfficeDealCommand(request);
                var result = await handler.HandleVerifyMidOfficeAsync(command, ct);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Balance modified by another transaction.", details = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Database Concurrency Conflict: Resource locked by another transaction.", details = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Conflict(new { error = "Database Transaction Contention: Resource busy or transaction conflict.", details = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        treasuryApi.MapPost("/deals/settle-back-office", async (
            SettleBackOfficeDealRequest request,
            TreasuryCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new SettleBackOfficeDealCommand(request);
                var result = await handler.HandleSettleBackOfficeAsync(command, ct);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Balance modified by another transaction.", details = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Database Concurrency Conflict: Resource locked by another transaction.", details = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Conflict(new { error = "Database Transaction Contention: Resource busy or transaction conflict.", details = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        treasuryApi.MapGet("/deals/{dealReference}", async (
            string dealReference,
            ITreasuryRepository repository,
            CancellationToken ct) =>
        {
            var deal = await repository.GetDealByReferenceAsync(dealReference, ct);
            if (deal is null)
                return Results.NotFound(new { error = $"Treasury Deal '{dealReference}' not found." });

            var response = new TreasuryDealResponse(
                deal.DealReference,
                deal.Type.ToString(),
                deal.Direction.ToString(),
                deal.CounterpartyBankBic,
                deal.CounterpartyName,
                deal.PrimaryAmount.Amount,
                deal.PrimaryAmount.Currency.Code,
                deal.SecondaryAmount.Amount,
                deal.SecondaryAmount.Currency.Code,
                deal.ExchangeRate,
                deal.InterestRate,
                deal.DealDate,
                deal.ValueDate,
                deal.Status.ToString(),
                deal.FrontOfficeDealerUserId,
                deal.MidOfficeRiskOfficerUserId,
                deal.BackOfficeSettlementUserId,
                deal.SettledAtUtc);

            return Results.Ok(response);
        });

        treasuryApi.MapPost("/alm/crr-slr/evaluate", async (
            EvaluateCrrSlrRequest request,
            TreasuryCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new EvaluateCrrSlrCommand(request);
                var result = await handler.HandleEvaluateCrrSlrAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        treasuryApi.MapPost("/alm/liquidity-gap/evaluate", async (
            EvaluateLiquidityGapRequest request,
            TreasuryCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new EvaluateLiquidityGapCommand(request);
                var result = await handler.HandleEvaluateLiquidityGapAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // --- SECURITY, AUDIT & COMPLIANCE APIS (BRPD 10.3.5, 10.3.6, 11.1, 11.2) ---
        var securityApi = app.MapGroup("/api/v1/security");
        var auditApi = app.MapGroup("/api/v1/audit");
        var complianceApi = app.MapGroup("/api/v1/compliance");

        securityApi.MapPost("/sessions/login", async (
            LoginSessionRequest request,
            SecurityAuditCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new LoginSessionCommand(request);
                var result = await handler.HandleLoginAsync(command, ct);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        securityApi.MapPost("/sessions/logout", async (
            LogoutSessionRequest request,
            SecurityAuditCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new LogoutSessionCommand(request);
                var result = await handler.HandleLogoutAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Balance modified by another transaction.", details = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Database Concurrency Conflict: Resource locked by another transaction.", details = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Conflict(new { error = "Database Transaction Contention: Resource busy or transaction conflict.", details = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        securityApi.MapPost("/permission/assert-financial", async (
            AssertFinancialPermissionRequest request,
            SecurityAuditCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new AssertFinancialPermissionCommand(request);
                await handler.HandleAssertFinancialPermissionAsync(command, ct);
                return Results.Ok(new { message = "Financial permission authorized." });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                return Results.Conflict(new { error = "Concurrency Conflict: Balance modified by another transaction.", details = ex.Message });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                return Results.Conflict(new { error = "Database Concurrency Conflict: Resource locked by another transaction.", details = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                return Results.Conflict(new { error = "Database Transaction Contention: Resource busy or transaction conflict.", details = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        auditApi.MapPost("/log", async (
            LogAuditEntryRequest request,
            SecurityAuditCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new LogAuditEntryCommand(request);
                var result = await handler.HandleLogAuditAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        auditApi.MapGet("/caats-extract", async (
            DateTime? fromUtc,
            DateTime? toUtc,
            SecurityAuditCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new GetCaatsAuditExtractCommand(fromUtc, toUtc);
                var result = await handler.HandleCaatsExtractAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        complianceApi.MapPost("/bfiu/ctr/generate", async (
            GenerateCtrReportRequest request,
            SecurityAuditCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new GenerateCtrReportCommand(request);
                var result = await handler.HandleGenerateCtrAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        complianceApi.MapPost("/bfiu/str/evaluate", async (
            EvaluateStrAlertsRequest request,
            SecurityAuditCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new EvaluateStrAlertsCommand(request);
                var result = await handler.HandleEvaluateStrAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // --- AI KNOWLEDGE ENGINE & GEMINI ADVISORY APIS (BRPD 1.3.5, 9.1.1, 10.1, 10.3) ---
        var aiApi = app.MapGroup("/api/v1/ai");

        aiApi.MapPost("/advisory/ask", async (
            AskAiAdvisoryRequest request,
            AiIntelligenceCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new AskAiAdvisoryCommand(request);
                var result = await handler.HandleAskAdvisoryAsync(command, ct);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        aiApi.MapPost("/tbml/analyze", async (
            AnalyzeTbmlRequest request,
            AiIntelligenceCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new AnalyzeTbmlCommand(request);
                var result = await handler.HandleAnalyzeTbmlAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        aiApi.MapGet("/advisory/{queryId}", async (
            string queryId,
            IAiKnowledgeRepository aiRepo,
            CancellationToken ct) =>
        {
            var query = await aiRepo.GetAdvisoryQueryByIdAsync(queryId, ct);
            if (query is null)
                return Results.NotFound(new { error = $"AI Advisory Query '{queryId}' not found." });

            var response = new AiAdvisoryResponse(
                query.QueryId,
                query.OfficerUserId,
                query.Category.ToString(),
                query.SanitizedQueryText,
                query.AdvisoryResponse,
                query.Citations.Select(c => new GroundedCitationDto(c.CircularReference, c.SectionTitle, c.RelevantExcerpt, c.RelevanceScore)).ToList(),
                query.ConfidenceScore,
                query.RequiresMakerCheckerReview,
                query.CreatedAtUtc);

            return Results.Ok(response);
        });

        aiApi.MapPost("/tools/pii-sanitize", (
            PiiSanitizeRequest request,
            AiIntelligenceCommandHandler handler) =>
        {
            var command = new PiiSanitizeCommand(request);
            var result = handler.HandlePiiSanitizeAsync(command);
            return Results.Ok(result.Result);
        });

        // --- GENERAL LEDGER & CHART OF ACCOUNTS APIS (BRPD Circular 12/2024 Module 1) ---
        var glApi = app.MapGroup("/api/v1/gl").AddEndpointFilter<FinancialAuthorizationFilter>();

        glApi.MapGet("/chart-of-accounts", async (
            IChartOfAccountsRepository coaRepo,
            CancellationToken ct) =>
        {
            var accounts = await coaRepo.GetAllAsync(ct);
            var dtos = accounts.Select(a => new ChartOfAccountDto(
                a.Id,
                a.Name,
                a.Category,
                a.ParentCode,
                a.Level,
                a.IsLeaf,
                a.IsActive,
                a.CurrencyCode)).ToList();
            return Results.Ok(dtos);
        });

        glApi.MapGet("/vouchers/{voucherNumber}", async (
            string voucherNumber,
            IFinancialVoucherRepository voucherRepo,
            CancellationToken ct) =>
        {
            var voucher = await voucherRepo.GetByVoucherNumberAsync(voucherNumber, ct);
            if (voucher is null)
            {
                return Results.NotFound(new { error = $"Voucher '{voucherNumber}' not found." });
            }

            var entries = voucher.Entries.Select(e => new VoucherEntryDto(
                e.Id,
                e.AccountCode,
                e.Type,
                e.Amount.Amount,
                e.Amount.Currency.Code,
                e.ExchangeRate,
                e.BaseAmount.Amount,
                e.Narration)).ToList();

            var dto = new FinancialVoucherResponseDto(
                voucher.Id,
                voucher.VoucherNumber,
                voucher.BranchSol,
                voucher.Type,
                voucher.ValueDate,
                voucher.CreatedAtUtc,
                voucher.Status,
                voucher.Audit.MakerUserId,
                voucher.Audit.CheckerUserId,
                voucher.ReversalOfVoucherId,
                entries);

            return Results.Ok(dto);
        });

        glApi.MapGet("/trial-balance", async (
            string? asOfDate,
            string? branchSol,
            IFinancialVoucherRepository voucherRepo,
            CancellationToken ct) =>
        {
            var date = string.IsNullOrWhiteSpace(asOfDate)
                ? DateOnly.FromDateTime(DateTime.UtcNow)
                : DateOnly.Parse(asOfDate);

            var trialBalance = await voucherRepo.GenerateTrialBalanceAsync(date, branchSol, ct);
            return Results.Ok(trialBalance);
        });

        glApi.MapPost("/vouchers/{voucherNumber}/reverse", async (
            string voucherNumber,
            ReverseVoucherRequest request,
            IGeneralLedgerService glService,
            IUnitOfWork unitOfWork,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return Results.BadRequest(new { error = "Reversal reason is required (BRPD 1.3.6)." });
            }
            if (string.IsNullOrWhiteSpace(request.ReversalMakerUserId) || string.IsNullOrWhiteSpace(request.ReversalCheckerUserId))
            {
                return Results.BadRequest(new { error = "Reversal Maker and Checker user IDs are required." });
            }
            if (request.ReversalMakerUserId == request.ReversalCheckerUserId)
            {
                return Results.BadRequest(new { error = "Maker cannot act as Checker for reversal voucher (BRPD 1.3.13)." });
            }

            try
            {
                var reversalVoucher = await glService.ReverseVoucherAsync(
                    voucherNumber,
                    request.ReversalMakerUserId,
                    request.ReversalCheckerUserId,
                    request.Reason,
                    ct);

                await unitOfWork.SaveChangesAsync(ct);

                var entries = reversalVoucher.Entries.Select(e => new VoucherEntryDto(
                    e.Id,
                    e.AccountCode,
                    e.Type,
                    e.Amount.Amount,
                    e.Amount.Currency.Code,
                    e.ExchangeRate,
                    e.BaseAmount.Amount,
                    e.Narration)).ToList();

                var dto = new FinancialVoucherResponseDto(
                    reversalVoucher.Id,
                    reversalVoucher.VoucherNumber,
                    reversalVoucher.BranchSol,
                    reversalVoucher.Type,
                    reversalVoucher.ValueDate,
                    reversalVoucher.CreatedAtUtc,
                    reversalVoucher.Status,
                    reversalVoucher.Audit.MakerUserId,
                    reversalVoucher.Audit.CheckerUserId,
                    reversalVoucher.ReversalOfVoucherId,
                    entries);

                return Results.Ok(dto);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });

        app.Services.InitializeCbsDatabase();

        return app;
    }
}

public record VerifyCustomerRequest(bool IsApproved, string? RejectionReason = null);
public record ReactivateDormantRequest(string Remarks);
public record SetPendingVouchersRequest(string BranchCode, int PendingCount);
public record SeedStationeryRequest(
    string BranchCode,
    CBS.Domain.Enums.RemittanceInstrumentType Type,
    string Prefix,
    long StartLeaf,
    long EndLeaf,
    string ManagerUserId,
    string CustodianUserId);
