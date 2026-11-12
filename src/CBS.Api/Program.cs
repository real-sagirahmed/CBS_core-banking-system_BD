using CBS.Application.Accounts.Commands;
using CBS.Application.Accounts.Commands.OpenDepositAccount;
using CBS.Application.Accounts.Commands.PostTransaction;
using CBS.Application.Accounts.DTOs;
using CBS.Application.Accounts.Services;
using CBS.Application.Clearing.DTOs;
using CBS.Application.Crm.DTOs;
using CBS.Application.Corporate.Commands;
using CBS.Application.Corporate.DTOs;
using CBS.Application.Adc.Commands;
using CBS.Application.Adc.DTOs;
using CBS.Application.AiIntelligence.Commands;
using CBS.Application.AiIntelligence.DTOs;
using CBS.Application.BranchCash.Commands;
using CBS.Application.BranchCash.Commands.CloseTellerDrawer;
using CBS.Application.BranchCash.Commands.IssueRemittanceInstrument;
using CBS.Application.BranchCash.Commands.LiquidateRemittanceInstrument;
using CBS.Application.BranchCash.Commands.ManageRemittanceInstrument;
using CBS.Application.BranchCash.Commands.OpenTellerDrawer;
using CBS.Application.BranchCash.Commands.ProcessCashTransaction;
using CBS.Application.BranchCash.DTOs;
using CBS.Application.Common.Interfaces;
using CBS.Application.Customers.Commands;
using CBS.Application.Customers.Commands.CreateCustomerDraft;
using CBS.Application.Customers.Commands.VerifyCustomer;
using CBS.Application.Customers.DTOs;
using CBS.Application.Governance.DTOs;
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
using CBS.Application.Reconciliation.Commands;
using CBS.Application.Reconciliation.DTOs;
using CBS.Application.Reconciliation.Services;
using CBS.Application.SecurityAudit.Commands;
using CBS.Application.SecurityAudit.DTOs;
using CBS.Application.StandingInstructions.Commands;
using CBS.Application.StandingInstructions.DTOs;
using CBS.Application.Cheques.Commands;
using CBS.Application.Cheques.DTOs;
using CBS.Application.Deposits.Commands;
using CBS.Application.Deposits.DTOs;
using CBS.Application.TermDeposits.Commands;
using CBS.Application.TermDeposits.DTOs;
using CBS.Application.TradeFinance.Commands;
using CBS.Application.TradeFinance.DTOs;
using CBS.Application.Treasury.Commands;
using CBS.Application.Treasury.DTOs;
using CBS.Application.Remittance;
using CBS.Application.Reporting.Commands;
using CBS.Application.Reporting.DTOs;
using CBS.Application.Security.Commands;
using CBS.Application.Security.DTOs;
using CBS.Application.Shariah.Commands;
using CBS.Application.Shariah.DTOs;
using CBS.Application.Specialized.Commands;
using CBS.Application.Specialized.DTOs;
using CBS.Application.Lending.Demand.Commands;
using CBS.Application.Lending.Demand.DTOs;
using CBS.Application.Training.Commands;
using CBS.Application.Training.DTOs;
using CBS.Application.Training.Services;
using CBS.Domain.Shariah;
using CBS.Domain.Training;
using CBS.Domain.Accounts;
using CBS.Domain.Reporting;
using CBS.Domain.Enums;
using CBS.Domain.Adc;
using CBS.Domain.BranchCash;
using CBS.Domain.Cheques;
using CBS.Domain.StandingInstructions;
using CBS.Domain.TermDeposits;
using CBS.Domain.Exceptions;
using CBS.Domain.GeneralLedger;
using CBS.Domain.Lending;
using CBS.Domain.AiIntelligence;
using CBS.Domain.PaymentRails;
using CBS.Domain.Reconciliation;
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
        builder.Services.AddScoped<IGeminiAiService, GeminiAiService>();
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
        builder.Services.AddTransient<AdcCommandHandler>();
        builder.Services.AddTransient<ReconciliationCommandHandler>();
        builder.Services.AddTransient<StandingInstructionCommandHandler>();
        builder.Services.AddTransient<ChequeCommandHandler>();
        builder.Services.AddTransient<TermDepositCommandHandler>();
        builder.Services.AddTransient<DpsCommandHandler>();
        builder.Services.AddTransient<ForeignRemittanceCommandHandler>();
        builder.Services.AddTransient<CrmCommandHandler>();
        builder.Services.AddTransient<SystemAdminCommandHandler>();
        builder.Services.AddTransient<MisReportingCommandHandler>();

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

        // --- STATUTORY COMPLIANCE & DOCUMENT EXPIRATION APIS (BRPD 8.1.8-d, 8.1.15) ---
        customerApi.MapPost("/compliance/documents/scan-expired", async (
            ScanExpiredDocumentsRequest request,
            ICustomerDocumentComplianceService service,
            CancellationToken ct) =>
        {
            try
            {
                var summary = await service.ScanAndApplyExpiredDocumentRestraintsAsync(request, ct);
                return Results.Ok(summary);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        customerApi.MapPost("/compliance/documents/{docId:guid}/renew", async (
            Guid docId,
            RenewDocumentRequest request,
            ICustomerDocumentComplianceService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.RenewDocumentAndReleaseRestraintsAsync(docId, request, ct);
                return Results.Ok(result);
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

        customerApi.MapPost("/compliance/fatca-assessment", async (
            FatcaComplianceEvaluationRequest request,
            ICustomerDocumentComplianceService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.EvaluateFatcaComplianceAsync(request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
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
            catch (Exception ex)
            {
                return Results.Conflict(new { error = "Transaction Execution Conflict: Resource contention or concurrency conflict.", details = ex.Message });
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
            catch (Exception ex)
            {
                return Results.Conflict(new { error = "Transaction Execution Conflict: Resource contention or concurrency conflict.", details = ex.Message });
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

        // --- ACCOUNT RESTRAINTS, LIENS & COURT FREEZES (BRPD 1.6.2-v, 1.6.6, 3.1.15) ---
        accountApi.MapPost("/{accountNumber}/restraints/place", async (
            string accountNumber,
            PlaceRestraintRequest request,
            AccountRestraintCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandlePlaceRestraintAsync(accountNumber, request, ct);
                return Results.Created($"/api/v1/accounts/{accountNumber}/restraints/{result.RestraintId}", result);
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

        accountApi.MapPost("/{accountNumber}/restraints/{restraintId}/release", async (
            string accountNumber,
            string restraintId,
            ReleaseRestraintRequest request,
            AccountRestraintCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleReleaseRestraintAsync(accountNumber, restraintId, request, ct);
                return Results.Ok(result);
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

        accountApi.MapPost("/{accountNumber}/restraints/{restraintId}/realize", async (
            string accountNumber,
            string restraintId,
            RealizeRestraintRequest request,
            AccountRestraintCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleRealizeRestraintAsync(accountNumber, restraintId, request, ct);
                return Results.Ok(result);
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

        accountApi.MapGet("/{accountNumber}/restraints/active", async (
            string accountNumber,
            AccountRestraintCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleGetActiveRestraintsAsync(accountNumber, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        accountApi.MapGet("/{accountNumber}/balance-summary", async (
            string accountNumber,
            AccountRestraintCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleGetBalanceSummaryAsync(accountNumber, ct);
                return Results.Ok(result);
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

        // --- STATUTORY ACCOUNT CLOSING LIFECYCLE (BRPD 1.6.4, 3.1.19) ---
        accountApi.MapGet("/{accountNumber}/close/quote", async (
            string accountNumber,
            AccountClosingCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var quote = await handler.HandleGetClosingQuoteAsync(accountNumber, ct);
                return Results.Ok(quote);
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

        accountApi.MapPost("/{accountNumber}/close", async (
            string accountNumber,
            CloseAccountRequest request,
            AccountClosingCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleCloseAccountAsync(accountNumber, request, ct);
                return Results.Ok(result);
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

        // --- ACCOUNT STATEMENTS, TAX CERTIFICATES & SWIFT MT940/MT942 APIS (BRPD 3.1.17, 3.1.19, 3.1.23) ---
        accountApi.MapPost("/{accountNumber}/statements/generate", async (
            string accountNumber,
            GenerateStatementRequest request,
            IAccountStatementService statementService,
            CancellationToken ct) =>
        {
            try
            {
                var reqWithAcc = request with { AccountNumber = accountNumber };
                var stmt = await statementService.GenerateStatementAsync(reqWithAcc, ct);
                return Results.Ok(stmt);
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

        accountApi.MapPost("/{accountNumber}/statements/balance-certificate", async (
            string accountNumber,
            GenerateBalanceCertificateRequest request,
            IAccountStatementService statementService,
            CancellationToken ct) =>
        {
            try
            {
                var reqWithAcc = request with { AccountNumber = accountNumber };
                var cert = await statementService.GenerateBalanceCertificateAsync(reqWithAcc, ct);
                return Results.Ok(cert);
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

        accountApi.MapPost("/{accountNumber}/statements/tax-certificate", async (
            string accountNumber,
            GenerateTaxCertificateRequest request,
            IAccountStatementService statementService,
            CancellationToken ct) =>
        {
            try
            {
                var reqWithAcc = request with { AccountNumber = accountNumber };
                var cert = await statementService.GenerateTaxCertificateAsync(reqWithAcc, ct);
                return Results.Ok(cert);
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

        accountApi.MapPost("/{accountNumber}/statements/swift-mt940", async (
            string accountNumber,
            GenerateSwiftStatementRequest request,
            ISwiftStatementGenerator swiftGenerator,
            CancellationToken ct) =>
        {
            try
            {
                var reqWithAcc = request with { AccountNumber = accountNumber, StatementType = CBS.Domain.Enums.SwiftStatementType.MT940_CustomerStatement };
                var result = await swiftGenerator.GenerateSwiftStatementAsync(reqWithAcc, ct);
                return Results.Ok(result);
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

        accountApi.MapPost("/{accountNumber}/statements/swift-mt942", async (
            string accountNumber,
            GenerateSwiftStatementRequest request,
            ISwiftStatementGenerator swiftGenerator,
            CancellationToken ct) =>
        {
            try
            {
                var reqWithAcc = request with { AccountNumber = accountNumber, StatementType = CBS.Domain.Enums.SwiftStatementType.MT942_InterimTransactionReport };
                var result = await swiftGenerator.GenerateSwiftStatementAsync(reqWithAcc, ct);
                return Results.Ok(result);
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

        // --- HIGH-THROUGHPUT BULK CORPORATE PAYROLL & DIVIDEND APIS (BRPD 1.6.16) ---
        var corporateApi = app.MapGroup("/api/v1/corporate/bulk-batches").AddEndpointFilter<FinancialAuthorizationFilter>();

        corporateApi.MapPost("/upload", async (
            UploadBulkBatchRequest request,
            BulkCorporateCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var summary = await handler.HandleUploadBatchAsync(request, ct);
                return Results.Created($"/api/v1/corporate/bulk-batches/{summary.BatchId}", summary);
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

        corporateApi.MapPost("/{batchId}/execute", async (
            string batchId,
            ExecuteBulkBatchRequest request,
            BulkCorporateCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleExecuteBatchAsync(batchId, request, ct);
                return Results.Ok(result);
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

        corporateApi.MapGet("/{batchId}", async (
            string batchId,
            BulkCorporateCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var summary = await handler.HandleGetBatchSummaryAsync(batchId, ct);
                return Results.Ok(summary);
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

        tellerApi.MapPost("/drawers/swap-denominations", async (
            InterTellerDenominationSwapRequest request,
            BranchVaultCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.SwapTellerDenominationsAsync(request, ct);
                return Results.Ok(result);
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

        // --- BRANCH VAULT & DUAL-CUSTODY CASH APIS (BRPD Circular 12/2024 Module 1.7.1, 1.7.12) ---
        var vaultApi = app.MapGroup("/api/v1/branch-vault").AddEndpointFilter<FinancialAuthorizationFilter>();

        vaultApi.MapPost("/initialize", async (
            InitializeBranchVaultRequest request,
            BranchVaultCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.InitializeVaultAsync(request, ct);
                return Results.Created($"/api/v1/branch-vault/{result.BranchCode}", result);
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

        vaultApi.MapGet("/{branchCode}", async (
            string branchCode,
            BranchVaultCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.GetVaultByBranchAsync(branchCode, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        vaultApi.MapPost("/chest-movement", async (
            ChestCashMovementRequest request,
            BranchVaultCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.ProcessChestMovementAsync(request, ct);
                return Results.Ok(result);
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

        vaultApi.MapPost("/replenish-teller", async (
            VaultTellerTransferRequest request,
            BranchVaultCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.ReplenishTellerAsync(request, ct);
                return Results.Ok(result);
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

        vaultApi.MapPost("/receive-from-teller", async (
            VaultTellerTransferRequest request,
            BranchVaultCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.ReceiveFromTellerAsync(request, ct);
                return Results.Ok(result);
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

        vaultApi.MapPost("/evening-banking/toggle", async (
            ToggleEveningBankingRequest request,
            BranchVaultCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.ToggleEveningBankingAsync(request, ct);
                return Results.Ok(result);
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

        // --- PHASE 27: LOAN ASSET CLASSIFICATION, QUALITATIVE JUDGMENT, RESCHEDULING & WRITE-OFF ENGINE (BRPD CIRCULAR 12/2024) ---
        lendingApi.MapPost("/classification/evaluate-eod", async (
            EvaluateClassificationEodRequest request,
            ILoanClassificationService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.EvaluatePortfolioClassificationAsync(
                    request.BranchCode,
                    request.EvaluationDate ?? DateTime.UtcNow,
                    ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        lendingApi.MapGet("/classification/portfolio-report", async (
            string? branchCode,
            ILoanClassificationService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.GetPortfolioClassificationReportAsync(branchCode, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        lendingApi.MapPost("/loans/{accountNumber}/qualitative-judgment", async (
            string accountNumber,
            ApplyQualitativeJudgmentApiRequest request,
            ILoanClassificationService service,
            CancellationToken ct) =>
        {
            try
            {
                var command = new ApplyQualitativeJudgmentCommand(
                    accountNumber,
                    request.Grade,
                    request.Reason,
                    request.JustificationNotes,
                    request.InspectionReportReference,
                    request.InspectorUserId,
                    request.CheckerUserId);

                var result = await service.ApplyQualitativeJudgmentAsync(command, ct);
                return Results.Ok(result);
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

        lendingApi.MapPost("/loans/{accountNumber}/qualitative-judgment/revoke", async (
            string accountNumber,
            RevokeQualitativeJudgmentApiRequest request,
            ILoanClassificationService service,
            CancellationToken ct) =>
        {
            try
            {
                var command = new RevokeQualitativeJudgmentCommand(
                    accountNumber,
                    request.InspectorUserId,
                    request.CheckerUserId,
                    request.RevocationReason);

                var result = await service.RevokeQualitativeJudgmentAsync(command, ct);
                return Results.Ok(result);
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

        lendingApi.MapPost("/loans/{accountNumber}/reschedule", async (
            string accountNumber,
            RescheduleLoanApiRequest request,
            ILoanClassificationService service,
            CancellationToken ct) =>
        {
            try
            {
                var command = new RescheduleLoanCommand(
                    accountNumber,
                    request.RescheduleIteration,
                    request.DownPaymentPaid,
                    request.RequiredDownPayment,
                    request.NewMaturityDate,
                    request.NewInterestRate,
                    request.ApprovalAuthority,
                    request.BoardResolutionReference,
                    request.BoardApprovalDate,
                    request.InterestWaiver,
                    request.RestructuringNotes,
                    request.MakerUserId,
                    request.CheckerUserId);

                var result = await service.RescheduleLoanAsync(command, ct);
                return Results.Ok(result);
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

        lendingApi.MapPost("/loans/{accountNumber}/write-off", async (
            string accountNumber,
            WriteOffLoanApiRequest request,
            ILoanClassificationService service,
            CancellationToken ct) =>
        {
            try
            {
                var command = new WriteOffLoanCommand(
                    accountNumber,
                    request.SpecificProvisionAvailable,
                    request.BoardResolutionNumber,
                    request.BoardApprovalDate,
                    request.LegalCaseType,
                    request.ArthaRinCaseNumber,
                    request.ArthaRinFilingDate,
                    request.MakerUserId,
                    request.CheckerUserId,
                    request.JustificationNotes);

                var result = await service.WriteOffLoanAsync(command, ct);
                return Results.Ok(result);
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

        lendingApi.MapPost("/loans/{accountNumber}/write-off-recovery", async (
            string accountNumber,
            RecordWriteOffRecoveryApiRequest request,
            ILoanClassificationService service,
            CancellationToken ct) =>
        {
            try
            {
                var command = new RecordWriteOffRecoveryCommand(
                    accountNumber,
                    request.Amount,
                    request.RecoverySource,
                    request.ReferenceNumber,
                    request.ReceivedByUserId);

                var result = await service.RecordWriteOffRecoveryAsync(command, ct);
                return Results.Ok(result);
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

        lendingApi.MapGet("/written-off-register", async (
            ILoanClassificationService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.GetWrittenOffRegisterAsync(ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
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

        tradeApi.MapPost("/exp/eod-batch", async (
            ExpEodBatchRequest request,
            TradeFinanceCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleExpEodBatchAsync(request.BranchCode, request.EvaluationDate, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        tradeApi.MapGet("/reports/overdue-exp", async (
            string? branchCode,
            TradeFinanceCommandHandler handler,
            CancellationToken ct) =>
        {
            var report = await handler.GetOverdueExpReportAsync(branchCode, ct);
            return Results.Ok(report);
        });

        tradeApi.MapGet("/reports/tbml-audit", async (
            string? branchCode,
            TradeFinanceCommandHandler handler,
            CancellationToken ct) =>
        {
            var report = await handler.GetTbmlAuditReportAsync(branchCode, ct);
            return Results.Ok(report);
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
                deal.SettledAtUtc,
                deal.BranchCode,
                deal.SettlementVoucherNumber,
                deal.SecurityIsin);

            return Results.Ok(response);
        });

        // --- Government Securities & MTM Revaluation Endpoints (DOS Circular 05/2014) ---
        treasuryApi.MapPost("/securities/register", async (
            RegisterTreasurySecurityRequest request,
            TreasuryCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new RegisterTreasurySecurityCommand(request);
                var result = await handler.HandleRegisterSecurityAsync(command, ct);
                return Results.Created($"/api/v1/treasury/securities/{result.SecurityId}", result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        treasuryApi.MapGet("/securities/{securityId}", async (
            string securityId,
            ITreasuryRepository repository,
            CancellationToken ct) =>
        {
            var sec = await repository.GetSecurityByIdAsync(securityId, ct);
            if (sec is null)
                return Results.NotFound(new { error = $"Treasury Security '{securityId}' not found." });

            var response = new TreasurySecurityResponseDto(
                sec.SecurityId,
                sec.SecurityName,
                sec.SecurityType.ToString(),
                sec.Classification.ToString(),
                sec.FaceValue.Amount,
                sec.BookValue.Amount,
                sec.MarketValue.Amount,
                sec.FaceValue.Currency.Code,
                sec.CouponRate,
                sec.PurchaseYield,
                sec.CurrentMarketYield,
                sec.CleanPrice,
                sec.IssueDate,
                sec.MaturityDate,
                sec.LastMtmValuationDate,
                sec.UnrealizedMtmGainLoss.Amount,
                sec.BranchCode);

            return Results.Ok(response);
        });

        treasuryApi.MapGet("/securities", async (
            ITreasuryRepository repository,
            CancellationToken ct) =>
        {
            var list = await repository.GetAllSecuritiesAsync(ct);
            var responses = list.Select(sec => new TreasurySecurityResponseDto(
                sec.SecurityId,
                sec.SecurityName,
                sec.SecurityType.ToString(),
                sec.Classification.ToString(),
                sec.FaceValue.Amount,
                sec.BookValue.Amount,
                sec.MarketValue.Amount,
                sec.FaceValue.Currency.Code,
                sec.CouponRate,
                sec.PurchaseYield,
                sec.CurrentMarketYield,
                sec.CleanPrice,
                sec.IssueDate,
                sec.MaturityDate,
                sec.LastMtmValuationDate,
                sec.UnrealizedMtmGainLoss.Amount,
                sec.BranchCode)).ToList();

            return Results.Ok(responses);
        });

        treasuryApi.MapPost("/securities/mtm-batch", async (
            MtmRevaluationBatchRequest request,
            TreasuryCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new MtmRevaluationBatchCommand(request);
                var result = await handler.HandleMtmRevaluationBatchAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // --- Market Risk Limits Endpoints (VaR & FX NOP - BRPD 6.5.2) ---
        treasuryApi.MapPost("/risk/var/evaluate", async (
            EvaluateParametricVaRRequest request,
            TreasuryCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new EvaluateParametricVaRCommand(request);
                var result = await handler.HandleEvaluateVarAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        treasuryApi.MapPost("/risk/nop/evaluate", async (
            FxNetOpenPositionRequest request,
            TreasuryCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new EvaluateFxNetOpenPositionCommand(request);
                var result = await handler.HandleEvaluateNopAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
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

        // --- PHASE 28: TREASURY MANAGEMENT, FX BLOTTER, NOP, MTM & NOSTRO RECONCILIATION (BRPD CIRCULAR 12/2024 MODULE 6) ---
        treasuryApi.MapGet("/blotter", async (
            DateOnly? date,
            ITreasuryManagementService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.GetFxBlotterAsync(date, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        treasuryApi.MapPost("/limits/profile", async (
            ConfigureTreasuryLimitProfileCommand command,
            ITreasuryManagementService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.ConfigureLimitProfileAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        treasuryApi.MapPost("/limits/check", async (
            CheckTreasuryLimitCommand command,
            ITreasuryManagementService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.CheckAndEnforceLimitAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        treasuryApi.MapPost("/revaluation/mtm-eod", async (
            ExecuteMtmRevaluationCommand command,
            ITreasuryManagementService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.ExecuteMtmRevaluationAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        treasuryApi.MapPost("/reserves/evaluate-crr-slr", async (
            EvaluateStatutoryReservesCommand command,
            ITreasuryManagementService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.EvaluateStatutoryReservesAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        treasuryApi.MapGet("/reserves/compliance-statement", async (
            ITreasuryManagementService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.GetLatestStatutoryReserveAssessmentAsync(ct);
                return result is null ? Results.NotFound(new { message = "No statutory reserve assessment found." }) : Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        treasuryApi.MapPost("/nostro/local-entry", async (
            IngestLocalNostroEntryCommand command,
            ITreasuryManagementService service,
            CancellationToken ct) =>
        {
            try
            {
                await service.IngestLocalNostroEntryAsync(command, ct);
                return Results.Ok(new { message = "Local Nostro entry recorded successfully." });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        treasuryApi.MapPost("/nostro/reconcile-auto", async (
            IngestNostroStatementCommand command,
            HttpContext httpContext,
            ITreasuryManagementService service,
            CancellationToken ct) =>
        {
            try
            {
                var userId = httpContext.Request.Headers["X-User-Id"].FirstOrDefault() ?? "SYSTEM_TREASURY";
                var result = await service.ReconcileNostroStatementsAsync(command, userId, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        treasuryApi.MapGet("/nostro/unreconciled-aging", async (
            DateOnly? asOfDate,
            ITreasuryManagementService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.GetNostroUnreconciledAgingReportAsync(asOfDate, ct);
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

        auditApi.MapGet("/caats/verify-integrity", async (
            SecurityAuditCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new CaatsVerifyIntegrityCommand();
                var result = await handler.HandleCaatsVerifyIntegrityAsync(command, ct);
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

        complianceApi.MapPost("/bfiu/ctr/auto-aggregate", async (
            AutoAggregateCtrRequest request,
            SecurityAuditCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new AutoAggregateCtrCommand(request);
                var result = await handler.HandleAutoAggregateCtrAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        complianceApi.MapGet("/bfiu/reports/{reportReference}/goaml-export", async (
            string reportReference,
            string? format,
            SecurityAuditCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new ExportGoAmlReportCommand(reportReference, format ?? "xml");
                var result = await handler.HandleGoAmlExportAsync(command, ct);
                return Results.Ok(result);
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

        complianceApi.MapGet("/bfiu/reports", async (
            SecurityAuditCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleGetAllBfiuReportsAsync(ct);
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

        aiApi.MapGet("/advisory", async (
            AiIntelligenceCommandHandler handler,
            CancellationToken ct) =>
        {
            var results = await handler.HandleGetAllAdvisoryQueriesAsync(ct);
            return Results.Ok(results);
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

        aiApi.MapGet("/tbml/{assessmentId}", async (
            string assessmentId,
            AiIntelligenceCommandHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleGetTbmlAssessmentAsync(assessmentId, ct);
            if (result is null)
                return Results.NotFound(new { error = $"TBML Semantic Assessment '{assessmentId}' not found." });

            return Results.Ok(result);
        });

        aiApi.MapGet("/knowledge/documents", async (
            AiIntelligenceCommandHandler handler,
            CancellationToken ct) =>
        {
            var docs = await handler.HandleGetAllKnowledgeDocumentsAsync(ct);
            return Results.Ok(docs);
        });

        aiApi.MapGet("/knowledge/search", async (
            string? query,
            int? topK,
            AiIntelligenceCommandHandler handler,
            CancellationToken ct) =>
        {
            var docs = await handler.HandleSearchKnowledgeAsync(query ?? string.Empty, topK ?? 3, ct);
            return Results.Ok(docs);
        });

        aiApi.MapPost("/tools/pii-sanitize", (
            PiiSanitizeRequest request,
            AiIntelligenceCommandHandler handler) =>
        {
            var command = new PiiSanitizeCommand(request);
            var result = handler.HandlePiiSanitizeAsync(command);
            return Results.Ok(result.Result);
        });

        // --- ALTERNATIVE DELIVERY CHANNELS (ADC) SWITCH APIS (BRPD Circular 12/2024 Module 7) ---
        var adcApi = app.MapGroup("/api/v1/adc");

        adcApi.MapPost("/switch/iso8583/process", async (
            ProcessIso8583Request request,
            AdcCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var command = new ProcessIso8583Command(request);
                var result = await handler.HandleProcessIso8583Async(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        adcApi.MapPost("/switch/iso8583/reversal", async (
            ProcessIso8583ReversalCommand command,
            AdcCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleProcessIso8583ReversalAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        adcApi.MapGet("/terminals", async (
            IAdcRepository adcRepo,
            CancellationToken ct) =>
        {
            var terminals = await adcRepo.GetAllTerminalsAsync(ct);
            var dtos = terminals.Select(t => new AtmTerminalDto(t.TerminalId, t.TerminalName, t.BranchCode, t.Location, t.CurrentCashBalance, t.MaxCapacity, t.IsActive, t.LastReplenishedAtUtc)).ToList();
            return Results.Ok(dtos);
        });

        adcApi.MapPost("/terminals/seed", async (
            SeedAtmTerminalRequest request,
            AdcCommandHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleSeedAtmTerminalAsync(new SeedAtmTerminalCommand(request), ct);
            return Results.Ok(result);
        });

        adcApi.MapGet("/callcenter/customer/{cifNumber}", async (
            string cifNumber,
            AdcCommandHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleGetCallCenterCustomerViewAsync(cifNumber, ct);
            if (result is null)
                return Results.NotFound(new { error = $"Customer with CIF '{cifNumber}' not found." });

            return Results.Ok(result);
        });

        // --- PHASE 31: ADC MULTI-CHANNEL CENTRAL RECONCILIATION & RECTIFICATION (BRPD 12/2024 Module 7.2) ---
        adcApi.MapPost("/reconciliation/batches", async (
            CreateReconciliationBatchRequest request,
            ICrmAndAdcReconciliationService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.CreateReconciliationBatchAsync(request, ct);
                return Results.Created($"/api/v1/adc/reconciliation/batches/{result.BatchId}", result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        adcApi.MapPost("/reconciliation/batches/{batchId}/ingest", async (
            string batchId,
            IngestSwitchRecordRequest request,
            ICrmAndAdcReconciliationService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.IngestSwitchRecordAsync(batchId, request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        adcApi.MapPost("/reconciliation/batches/{batchId}/rectify", async (
            string batchId,
            GenerateRectificationVoucherRequest request,
            ICrmAndAdcReconciliationService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.GenerateRectificationVoucherAsync(batchId, request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        adcApi.MapGet("/reconciliation/batches/{batchId}", async (
            string batchId,
            ICrmAndAdcReconciliationService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.GetReconciliationBatchAsync(batchId, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        adcApi.MapPost("/reconciliation/batches/{batchId}/close", async (
            string batchId,
            CloseReconciliationBatchRequest request,
            ICrmAndAdcReconciliationService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.CloseReconciliationBatchAsync(batchId, request.ClosedByUserId, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // --- AUTOMATED RECONCILIATION & DISPUTE SETTLEMENT APIS (BRPD Circular 12/2024 Module 1.4 & 7.2) ---
        var reconApi = app.MapGroup("/api/v1/reconciliation");

        reconApi.MapPost("/settlement/3-way-match", async (
            RunReconciliationRequest request,
            ReconciliationCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleRunReconciliationAsync(new RunReconciliationCommand(request), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        reconApi.MapGet("/batches", async (
            IReconciliationRepository reconRepo,
            CancellationToken ct) =>
        {
            var batches = await reconRepo.GetAllBatchesAsync(ct);
            var dtos = batches.Select(b => new SettlementReconciliationResponse(
                b.BatchId,
                b.ReconciliationDate,
                b.Channel.ToString(),
                b.TotalCoreTransactions,
                b.TotalSwitchTransactions,
                b.TotalNetworkTransactions,
                b.MatchedCount,
                b.MismatchedCount,
                b.Status.ToString(),
                b.UnmatchedItems.Select(u => new UnmatchedReconItemDto(u.ItemId, u.Stan, u.Rrn, u.AccountNumber, u.Amount, u.DiscrepancyType, u.AgingBucket.ToString(), u.DaysUnreconciled, u.IsRectified)).ToList()
            )).ToList();
            return Results.Ok(dtos);
        });

        reconApi.MapPost("/disputes/file", async (
            FileDisputeClaimRequest request,
            ReconciliationCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleFileDisputeAsync(new FileDisputeCommand(request), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        reconApi.MapPost("/disputes/{claimId}/resolve", async (
            string claimId,
            ResolveDisputeRequest request,
            ReconciliationCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleResolveDisputeRefundAsync(new ResolveDisputeCommand(claimId, request), ct);
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

        reconApi.MapGet("/disputes", async (
            IReconciliationRepository reconRepo,
            CancellationToken ct) =>
        {
            var disputes = await reconRepo.GetAllDisputesAsync(ct);
            var dtos = disputes.Select(d => new DisputeClaimResponse(
                d.ClaimId,
                d.OriginalTransactionId,
                d.OriginalStan,
                d.OriginalRrn,
                d.AccountNumber.Value,
                d.DisputedAmount.Amount,
                d.Channel.ToString(),
                d.DisputeReason,
                d.Status.ToString(),
                d.RectificationVoucherNumber,
                d.ResolvedByOfficerId,
                d.InvestigationNotes,
                d.CreatedAtUtc,
                d.ResolvedAtUtc)).ToList();
            return Results.Ok(dtos);
        });

        // --- STANDING INSTRUCTIONS & SWEEP ACCOUNTS APIS (BRPD Circular 12/2024 Module 1.8 & 1.6) ---
        var siApi = app.MapGroup("/api/v1/standing-instructions");

        siApi.MapPost("/", async (
            CreateStandingInstructionRequest request,
            StandingInstructionCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleCreateAsync(new CreateStandingInstructionCommand(request), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        siApi.MapPost("/sweep/rule", async (
            ConfigureSweepRuleRequest request,
            StandingInstructionCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleConfigureSweepRuleAsync(new ConfigureSweepRuleCommand(request), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        siApi.MapPost("/sweep/execute/{ruleId}", async (
            string ruleId,
            StandingInstructionCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleExecuteSweepAsync(new ExecuteSweepRuleCommand(ruleId), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        siApi.MapPost("/batch/execute-due", async (
            DateOnly asOfDate,
            StandingInstructionCommandHandler handler,
            CancellationToken ct) =>
        {
            var count = await handler.HandleExecuteDueStandingInstructionsAsync(new ExecuteDueStandingInstructionsCommand(asOfDate), ct);
            return Results.Ok(new { processedCount = count, asOfDate });
        });

        siApi.MapPost("/{id}/pause", async (
            string id,
            string reason,
            StandingInstructionCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandlePauseAsync(new PauseStandingInstructionCommand(id, reason), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        siApi.MapPost("/{id}/resume", async (
            string id,
            StandingInstructionCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleResumeAsync(new ResumeStandingInstructionCommand(id), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        siApi.MapGet("/account/{accountNumber}", async (
            string accountNumber,
            IStandingInstructionRepository repo,
            CancellationToken ct) =>
        {
            var list = await repo.GetBySourceAccountAsync(accountNumber, ct);
            return Results.Ok(list.Select(s => new StandingInstructionResponseDto(
                s.Id, s.SourceAccountNumber.Value, s.TargetAccountNumber.Value, s.Type.ToString(), s.Frequency.ToString(),
                s.TransferAmount.Amount, s.ExecutionFee.Amount, s.StartDate, s.EndDate, s.NextExecutionDate, s.Status.ToString(),
                s.ConsecutiveFailureCount, s.PauseReason, s.Narrative)));
        });

        siApi.MapPost("/{id}/temporary-hold", async (
            string id,
            ApplyTemporaryHoldRequest request,
            StandingInstructionCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleApplyTemporaryHoldAsync(new ApplySiTemporaryHoldCommand(id, request), ct);
                return Results.Ok(result);
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

        siApi.MapPost("/{id}/release-hold", async (
            string id,
            ReleaseTemporaryHoldRequest request,
            StandingInstructionCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleReleaseTemporaryHoldAsync(new ReleaseSiTemporaryHoldCommand(id, request), ct);
                return Results.Ok(result);
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

        siApi.MapPut("/{id}/modify", async (
            string id,
            ModifyStandingInstructionRequest request,
            StandingInstructionCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleModifyInstructionAsync(new ModifyStandingInstructionCommand(id, request), ct);
                return Results.Ok(result);
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

        siApi.MapPost("/multi-sweeps/configure", async (
            ConfigureMultiSweepRequest request,
            MultiAccountSweepCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleConfigureMultiSweepAsync(request, ct);
                return Results.Ok(result);
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

        siApi.MapPost("/multi-sweeps/execute/{ruleId}", async (
            string ruleId,
            MultiAccountSweepCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleExecuteMultiSweepAsync(ruleId, ct);
                return Results.Ok(result);
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

        // --- CHEQUE BOOK & LEAF LIFECYCLE APIS (BRPD Circular 12/2024 Module 1.5 & 3.1.14) ---
        var chequeApi = app.MapGroup("/api/v1/cheques");

        chequeApi.MapPost("/requisition", async (
            RequisitionChequeBookRequest request,
            ChequeCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleRequisitionAsync(new RequisitionChequeBookCommand(request), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        chequeApi.MapPost("/books/{bookId}/deliver", async (
            string bookId,
            ChequeCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleDeliverBookAsync(new DeliverChequeBookCommand(bookId), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        chequeApi.MapPost("/stop-payment", async (
            ChequeStopPaymentRequest request,
            ChequeCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleStopPaymentAsync(new StopPaymentCommand(request), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        chequeApi.MapPost("/stop-payment/revoke", async (
            ChequeRevokeStopPaymentRequest request,
            ChequeCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleRevokeStopPaymentAsync(new RevokeStopPaymentCommand(request), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        chequeApi.MapGet("/leaf/status", async (
            string accountNumber,
            long leafNumber,
            ChequeCommandHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleGetLeafStatusAsync(accountNumber, leafNumber, ct);
            return result is null ? Results.NotFound(new { error = $"Cheque leaf '{leafNumber}' not found for account '{accountNumber}'." }) : Results.Ok(result);
        });

        chequeApi.MapGet("/undelivered/report", async (
            ChequeCommandHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleGetUndeliveredBooksReportAsync(DateOnly.FromDateTime(DateTime.UtcNow), ct);
            return Results.Ok(result);
        });

        // --- TERM DEPOSIT & FDR LIFECYCLE APIS (BRPD Circular 12/2024 Module 3.5 & 3.6) ---
        var tdApi = app.MapGroup("/api/v1/term-deposits");

        tdApi.MapPost("/open", async (
            OpenTermDepositRequest request,
            TermDepositCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleOpenAsync(new OpenTermDepositCommand(request), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        tdApi.MapPost("/{contractId}/accrue", async (
            string contractId,
            DateOnly calculationDate,
            TermDepositCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var accrued = await handler.HandleAccrueDailyInterestAsync(new AccrueDailyInterestCommand(contractId, calculationDate), ct);
                return Results.Ok(new { contractId, accruedAmount = accrued, calculationDate });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        tdApi.MapPost("/{contractId}/premature-encash", async (
            string contractId,
            PrematureEncashmentRequest request,
            TermDepositCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandlePrematureEncashmentAsync(new PrematureEncashmentCommand(contractId, request), ct);
                return Results.Ok(result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, code = ex.InvariantName });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        tdApi.MapPost("/{contractId}/rollover", async (
            string contractId,
            decimal prevailingInterestRate,
            bool hasTin,
            TermDepositCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleMaturityRolloverAsync(new ExecuteMaturityRolloverCommand(contractId, prevailingInterestRate, hasTin), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        tdApi.MapPost("/{contractId}/lien", async (
            string contractId,
            MarkLienRequest request,
            TermDepositCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleMarkLienAsync(new MarkLienCommand(contractId, request), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        tdApi.MapPost("/{contractId}/lien/release", async (
            string contractId,
            string releaseReference,
            TermDepositCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleReleaseLienAsync(new ReleaseLienCommand(contractId, releaseReference), ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        tdApi.MapGet("/{contractId}", async (
            string contractId,
            TermDepositCommandHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleGetByIdAsync(contractId, ct);
            return result is null ? Results.NotFound(new { error = $"FDR contract '{contractId}' not found." }) : Results.Ok(result);
        });

        // --- DEPOSIT PENSION SCHEME (DPS) & RECURRING DEPOSITS (BRPD Circular 12/2024 Module 3.7 & 3.8) ---
        var dpsApi = app.MapGroup("/api/v1/dps").AddEndpointFilter<FinancialAuthorizationFilter>();

        dpsApi.MapPost("/open", async (
            OpenDpsRequest request,
            DpsCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleOpenDpsAccountAsync(request, ct);
                return Results.Ok(result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.ToString(), statusCode: 500);
            }
        });

        dpsApi.MapGet("/{dpsAccountId}", async (
            string dpsAccountId,
            DpsCommandHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleGetDpsAccountAsync(dpsAccountId, ct);
            return result is null ? Results.NotFound(new { error = $"DPS account '{dpsAccountId}' not found." }) : Results.Ok(result);
        });

        dpsApi.MapPost("/{dpsAccountId}/collect-installment", async (
            string dpsAccountId,
            CollectInstallmentRequest request,
            DpsCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleCollectInstallmentAsync(dpsAccountId, request, ct);
                return Results.Ok(result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
        });

        dpsApi.MapGet("/{dpsAccountId}/premature-quote", async (
            string dpsAccountId,
            DateOnly encashmentDate,
            bool hasTaxIdNumber,
            DpsCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleGetPrematureQuoteAsync(dpsAccountId, encashmentDate, hasTaxIdNumber, ct);
                return Results.Ok(result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
        });

        dpsApi.MapPost("/{dpsAccountId}/premature-encash", async (
            string dpsAccountId,
            PrematureEncashDpsRequest request,
            DpsCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandlePrematureEncashAsync(dpsAccountId, request, ct);
                return Results.Ok(result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
        });

        dpsApi.MapPost("/eod-batch/process-sweeps", async (
            ProcessDpsSweepsRequest request,
            DpsCommandHandler handler,
            CancellationToken ct) =>
        {
            var count = await handler.HandleExecuteDueDpsSweepsAsync(request.EvaluationDate, ct);
            return Results.Ok(new { processedCount = count, evaluationDate = request.EvaluationDate });
        });

        // --- DEPOSIT LIEN & DLAD FACILITY APIS (BRPD Circular 12/2024 Module 3.8.10 & 3.6.1) ---
        var lienApi = app.MapGroup("/api/v1/deposits/liens").AddEndpointFilter<FinancialAuthorizationFilter>();

        lienApi.MapPost("/place", async (
            PlaceDepositLienRequest request,
            DpsCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandlePlaceDepositLienAsync(request, ct);
                return Results.Ok(result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
        });

        lienApi.MapPost("/{lienId}/release", async (
            string lienId,
            ReleaseDepositLienRequest request,
            DpsCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleReleaseDepositLienAsync(lienId, request, ct);
                return Results.Ok(result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
        });

        lienApi.MapGet("/{depositAccountId}", async (
            string depositAccountId,
            DpsCommandHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleGetActiveLienAsync(depositAccountId, ct);
            return result is null ? Results.NotFound(new { error = $"No active lien found for deposit account '{depositAccountId}'." }) : Results.Ok(result);
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

        // --- FOREIGN REMITTANCE & 2.5% CASH INCENTIVE ENGINE (BRPD Circular 12/2024 Module 2.2) ---
        var foreignRemittanceApi = app.MapGroup("/api/v1/remittance/foreign").AddEndpointFilter<FinancialAuthorizationFilter>();

        foreignRemittanceApi.MapPost("/inward/process", async (
            ProcessInwardRemittanceRequest request,
            ForeignRemittanceCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.ProcessInwardAsync(request, ct);
                return Results.Created($"/api/v1/remittance/foreign/{result.Id}", result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        foreignRemittanceApi.MapPost("/inward/{id:guid}/authorize", async (
            Guid id,
            AuthorizeInwardRemittanceRequest request,
            ForeignRemittanceCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.AuthorizeInwardAsync(id, request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        foreignRemittanceApi.MapPost("/outward/initiate", async (
            InitiateOutwardRemittanceRequest request,
            ForeignRemittanceCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.InitiateOutwardAsync(request, ct);
                return Results.Created($"/api/v1/remittance/foreign/{result.Id}", result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        foreignRemittanceApi.MapPost("/outward/{id:guid}/authorize", async (
            Guid id,
            AuthorizeOutwardRemittanceRequest request,
            ForeignRemittanceCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.AuthorizeOutwardAsync(id, request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        foreignRemittanceApi.MapPost("/{id:guid}/cancel", async (
            Guid id,
            CancelRemittanceRequest request,
            ForeignRemittanceCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.CancelAsync(id, request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        foreignRemittanceApi.MapPost("/incentives/batch-claims", async (
            BatchIncentiveClaimRequest request,
            ForeignRemittanceCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.BatchIncentivesAsync(request, ct);
                return Results.Created($"/api/v1/remittance/foreign/incentives/batches/{result.Id}", result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        foreignRemittanceApi.MapPost("/incentives/batches/{batchId:guid}/submit", async (
            Guid batchId,
            SubmitIncentiveClaimRequest request,
            ForeignRemittanceCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.SubmitClaimBatchAsync(batchId, request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        foreignRemittanceApi.MapGet("/{id:guid}", async (
            Guid id,
            ForeignRemittanceCommandHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.GetByIdAsync(id, ct);
            return result is null ? Results.NotFound(new { error = $"Remittance transaction '{id}' not found." }) : Results.Ok(result);
        });

        foreignRemittanceApi.MapGet("/inward/register", async (
            DateTime? fromUtc,
            DateTime? toUtc,
            ForeignRemittanceCommandHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.GetInwardRegisterAsync(fromUtc, toUtc, ct);
            return Results.Ok(result);
        });

        foreignRemittanceApi.MapGet("/incentives/batches", async (
            ForeignRemittanceCommandHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.GetAllClaimBatchesAsync(ct);
            return Results.Ok(result);
        });

        // --- CUSTOMER RELATIONSHIP MANAGEMENT (CRM), SPECIMEN SIGNATURES & MANDATES (BRPD Circular 12/2024 Module 8) ---
        var crmApi = app.MapGroup("/api/v1/crm").AddEndpointFilter<FinancialAuthorizationFilter>();

        crmApi.MapPost("/signatures/register", async (
            RegisterSpecimenSignatureRequest request,
            CrmCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.RegisterSignatureAsync(request, ct);
                return Results.Created($"/api/v1/crm/signatures/{result.Id}", result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapGet("/signatures/account/{accountNumber}", async (
            string accountNumber,
            CrmCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.GetSignaturesForAccountAsync(accountNumber, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapPost("/mandates/define", async (
            DefineAccountMandateRequest request,
            CrmCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.DefineMandateAsync(request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapPost("/mandates/resolve", async (
            ResolveMandateRequest request,
            CrmCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.ResolveMandateAsync(request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapPost("/corporate/create", async (
            CreateCorporateCustomerRequest request,
            CrmCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.CreateCorporateCustomerAsync(request, ct);
                return Results.Created($"/api/v1/crm/corporate/{result.CifNumber}", result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapPost("/corporate/{cif}/stakeholders", async (
            string cif,
            AddCorporateStakeholderRequest request,
            CrmCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.AddStakeholderAsync(cif, request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapPost("/corporate/{cif}/activate", async (
            string cif,
            ActivateCorporateCustomerRequest request,
            CrmCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.ActivateCorporateCustomerAsync(cif, request.CheckerUserId, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapPost("/groups/create", async (
            CreateCorporateGroupRequest request,
            CrmCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.CreateCorporateGroupAsync(request, ct);
                return Results.Created($"/api/v1/crm/groups/{result.Id}", result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapPost("/groups/{groupId}/link-cif", async (
            string groupId,
            LinkGroupMemberRequest request,
            CrmCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.LinkCorporateGroupMemberAsync(groupId, request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapGet("/groups/{groupId}/exposure", async (
            string groupId,
            CrmCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.GetCorporateGroupExposureAsync(groupId, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapPost("/customers/{cif}/risk-grade", async (
            string cif,
            EvaluateEkycRiskRequest request,
            CrmCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.EvaluateEkycRiskAsync(request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapPost("/documents/register", async (
            RegisterCustomerDocumentRequest request,
            CrmCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.RegisterDocumentAsync(request, ct);
                return Results.Created($"/api/v1/crm/documents/{result.Id}", result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapGet("/documents/expiring", async (
            int? daysThreshold,
            CrmCommandHandler handler,
            CancellationToken ct) =>
        {
            int days = daysThreshold ?? 30;
            var result = await handler.GetExpiringDocumentsAsync(days, ct);
            return Results.Ok(result);
        });

        // --- PHASE 29: CRM REGULATORY COMPLIANCE SUITE (CRG, SANCTIONS, FATCA & BORROWER ICRR) ---
        crmApi.MapPost("/compliance/risk/evaluate", async (
            EvaluateCustomerRiskCommand command,
            ICrmComplianceService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.EvaluateCustomerRiskAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapPost("/compliance/risk/approve-senior-mgmt", async (
            ApproveSeniorManagementCommand command,
            ICrmComplianceService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.ApproveSeniorManagementAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapGet("/compliance/risk/{cif}", async (
            string cif,
            ICrmComplianceService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.GetCustomerRiskAssessmentAsync(cif, ct);
                return result is null ? Results.NotFound(new { message = $"No risk assessment found for CIF '{cif}'." }) : Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapPost("/compliance/sanctions/screen", async (
            ScreenSanctionCommand command,
            ICrmComplianceService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.ScreenSanctionAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapPost("/compliance/sanctions/investigate", async (
            InvestigateSanctionCommand command,
            ICrmComplianceService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.InvestigateSanctionAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapPost("/compliance/sanctions/resolve", async (
            ResolveSanctionCheckerCommand command,
            ICrmComplianceService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.ResolveSanctionCheckerAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapGet("/compliance/sanctions/pending", async (
            ICrmComplianceService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.GetPendingSanctionsAsync(ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapPost("/compliance/fatca/evaluate", async (
            EvaluateFatcaIndiciaCommand command,
            ICrmComplianceService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.EvaluateFatcaIndiciaAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapPost("/compliance/fatca/w9", async (
            SubmitFatcaW9Command command,
            ICrmComplianceService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.SubmitFatcaW9Async(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapPost("/compliance/fatca/w8ben", async (
            SubmitFatcaW8BenCommand command,
            ICrmComplianceService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.SubmitFatcaW8BenAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapGet("/compliance/fatca/{cif}", async (
            string cif,
            ICrmComplianceService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.GetFatcaProfileAsync(cif, ct);
                return result is null ? Results.NotFound(new { message = $"No FATCA profile found for CIF '{cif}'." }) : Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapPost("/compliance/icrr/evaluate", async (
            EvaluateBorrowerIcrrCommand command,
            ICrmComplianceService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.EvaluateBorrowerIcrrAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapPost("/compliance/icrr/approve-board", async (
            ApproveIcrrByBoardCommand command,
            ICrmComplianceService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.ApproveIcrrByBoardAsync(command, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapGet("/compliance/icrr/{cif}", async (
            string cif,
            ICrmComplianceService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.GetLatestBorrowerIcrrAsync(cif, ct);
                return result is null ? Results.NotFound(new { message = $"No ICRR assessment found for CIF '{cif}'." }) : Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapGet("/compliance/icrr/{cif}/verify-sanction", async (
            string cif,
            ICrmComplianceService service,
            CancellationToken ct) =>
        {
            try
            {
                await service.VerifyBorrowerSanctionEligibilityAsync(cif, ct);
                return Results.Ok(new { message = $"Borrower '{cif}' is eligible for credit sanction under BRPD 03/2019." });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // --- PHASE 31: CORPORATE GROUP UBO HIERARCHY & TRANSACTION PROFILE (TP) VARIANCE (BRPD 12/2024 Modules 7 & 8) ---
        crmApi.MapPost("/groups/{groupId}/member-details", async (
            string groupId,
            AddGroupMemberDetailRequest request,
            ICrmAndAdcReconciliationService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.AddGroupMemberDetailAsync(groupId, request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapPost("/groups/{groupId}/ubos", async (
            string groupId,
            RegisterBeneficialOwnerRequest request,
            ICrmAndAdcReconciliationService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.RegisterBeneficialOwnerAsync(groupId, request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapGet("/groups/{groupId}/hierarchy", async (
            string groupId,
            ICrmAndAdcReconciliationService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.GetGroupHierarchyAsync(groupId, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapPost("/groups/{groupId}/validate-exposure", async (
            string groupId,
            ValidateGroupExposureRequest request,
            ICrmAndAdcReconciliationService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.ValidateGroupExposureLimitAsync(groupId, request, ct);
                return Results.Ok(new { isValid = result, message = "Proposed credit facility is within statutory group exposure limit." });
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapPost("/tp", async (
            CreateTransactionProfileRequest request,
            ICrmAndAdcReconciliationService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.CreateOrUpdateTpAsync(request, ct);
                return Results.Created($"/api/v1/crm/tp/{result.CifNumber}", result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapPost("/tp/{cif}/record", async (
            string cif,
            RecordTpTransactionRequest request,
            ICrmAndAdcReconciliationService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.RecordTpTransactionAsync(cif, request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapGet("/tp/{cif}", async (
            string cif,
            ICrmAndAdcReconciliationService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.GetTpByCifAsync(cif, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        crmApi.MapPost("/tp/{cif}/clear-breach", async (
            string cif,
            ClearTpBreachRequest request,
            ICrmAndAdcReconciliationService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.ClearTpBreachAsync(cif, request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // --- BACPS CHEQUE CLEARING DISHONOUR, RETURN MEMOS, BILLS FOR COLLECTION & IBP (BRPD Circular 12 Modules 1, 2 & 4) ---
        var clearingApi = app.MapGroup("/api/v1/clearing");

        clearingApi.MapPost("/cheques/dishonour-memo", async (
            IssueChequeDishonourMemoCommand command,
            IClearingAndBillsService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.IssueDishonourMemoAsync(command, ct);
                return Results.Created($"/api/v1/clearing/cheques/{result.Id}", result);
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

        clearingApi.MapPost("/cheques/{id:guid}/legal-notice", async (
            Guid id,
            ServeSection138NoticeCommand command,
            IClearingAndBillsService service,
            CancellationToken ct) =>
        {
            try
            {
                var cmdWithId = command with { DishonourRecordId = id };
                var result = await service.ServeSection138NoticeAsync(cmdWithId, ct);
                return Results.Ok(result);
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

        clearingApi.MapPost("/cheques/{id:guid}/cure", async (
            Guid id,
            RecordCurePaymentCommand command,
            IClearingAndBillsService service,
            CancellationToken ct) =>
        {
            try
            {
                var cmdWithId = command with { DishonourRecordId = id };
                var result = await service.RecordCurePaymentAsync(cmdWithId, ct);
                return Results.Ok(result);
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

        clearingApi.MapPost("/cheques/{id:guid}/lawsuit", async (
            Guid id,
            FileSection138SuitCommand command,
            IClearingAndBillsService service,
            CancellationToken ct) =>
        {
            try
            {
                var cmdWithId = command with { DishonourRecordId = id };
                var result = await service.FileSection138SuitAsync(cmdWithId, ct);
                return Results.Ok(result);
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

        clearingApi.MapGet("/cheques/{id:guid}", async (
            Guid id,
            IClearingAndBillsService service,
            CancellationToken ct) =>
        {
            var result = await service.GetDishonourRecordByIdAsync(id, ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        });

        clearingApi.MapGet("/cheques/{id:guid}/return-memo", async (
            Guid id,
            IClearingAndBillsService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.GetDishonourAdvicePrintAsync(id, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        clearingApi.MapPost("/bills/obc/lodge", async (
            LodgeBillForCollectionCommand command,
            IClearingAndBillsService service,
            CancellationToken ct) =>
        {
            try
            {
                var cmd = command with { CollectionType = CBS.Domain.Enums.BillCollectionType.OutwardCleanCollection_OBC };
                var result = await service.LodgeBillForCollectionAsync(cmd, ct);
                return Results.Created($"/api/v1/clearing/bills/{result.Id}", result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        clearingApi.MapPost("/bills/ibc/lodge", async (
            LodgeBillForCollectionCommand command,
            IClearingAndBillsService service,
            CancellationToken ct) =>
        {
            try
            {
                var cmd = command with { CollectionType = CBS.Domain.Enums.BillCollectionType.InwardCleanCollection_IBC };
                var result = await service.LodgeBillForCollectionAsync(cmd, ct);
                return Results.Created($"/api/v1/clearing/bills/{result.Id}", result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        clearingApi.MapPost("/bills/{id:guid}/dispatch", async (
            Guid id,
            DispatchBillScheduleCommand command,
            IClearingAndBillsService service,
            CancellationToken ct) =>
        {
            try
            {
                var cmdWithId = command with { BillId = id };
                var result = await service.DispatchBillScheduleAsync(cmdWithId, ct);
                return Results.Ok(result);
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

        clearingApi.MapPost("/bills/{id:guid}/realize", async (
            Guid id,
            RealizeBillForCollectionCommand command,
            IClearingAndBillsService service,
            CancellationToken ct) =>
        {
            try
            {
                var cmdWithId = command with { BillId = id };
                var result = await service.RealizeBillForCollectionAsync(cmdWithId, ct);
                return Results.Ok(result);
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

        clearingApi.MapPost("/bills/{id:guid}/dishonour", async (
            Guid id,
            DishonourBillForCollectionCommand command,
            IClearingAndBillsService service,
            CancellationToken ct) =>
        {
            try
            {
                var cmdWithId = command with { BillId = id };
                var result = await service.DishonourBillForCollectionAsync(cmdWithId, ct);
                return Results.Ok(result);
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

        clearingApi.MapGet("/bills/{id:guid}", async (
            Guid id,
            IClearingAndBillsService service,
            CancellationToken ct) =>
        {
            var result = await service.GetBillByIdAsync(id, ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        });

        clearingApi.MapPost("/ibp/purchase", async (
            PurchaseInlandBillCommand command,
            IClearingAndBillsService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.PurchaseInlandBillAsync(command, ct);
                return Results.Created($"/api/v1/clearing/ibp/{result.Id}", result);
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

        clearingApi.MapPost("/ibp/{id:guid}/settle", async (
            Guid id,
            SettleInlandBillCommand command,
            IClearingAndBillsService service,
            CancellationToken ct) =>
        {
            try
            {
                var cmdWithId = command with { IbpId = id };
                var result = await service.SettleInlandBillAsync(cmdWithId, ct);
                return Results.Ok(result);
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

        clearingApi.MapPost("/ibp/{id:guid}/crystallize-forced-loan", async (
            Guid id,
            CrystallizeForcedLoanCommand command,
            IClearingAndBillsService service,
            CancellationToken ct) =>
        {
            try
            {
                var cmdWithId = command with { IbpId = id };
                var result = await service.CrystallizeForcedLoanAsync(cmdWithId, ct);
                return Results.Ok(result);
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

        clearingApi.MapGet("/ibp/{id:guid}", async (
            Guid id,
            IClearingAndBillsService service,
            CancellationToken ct) =>
        {
            var result = await service.GetIbpByIdAsync(id, ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        });

        // --- SYSTEM ADMINISTRATION, DOA MATRIX & GOVERNANCE (BRPD Circular 12/2024 Modules 9 & 10) ---
        var adminApi = app.MapGroup("/api/v1/admin");

        adminApi.MapPost("/users/propose", async (
            ProposeCreateUserRequest request,
            SystemAdminCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.ProposeCreateUserAsync(request, ct);
                return Results.Created($"/api/v1/admin/proposals/{result.Id}", result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        adminApi.MapPost("/proposals/{id:guid}/approve", async (
            Guid id,
            ApproveProposalRequest request,
            SystemAdminCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.ApproveProposalAsync(id, request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        adminApi.MapPost("/proposals/{id:guid}/reject", async (
            Guid id,
            RejectProposalRequest request,
            SystemAdminCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.RejectProposalAsync(id, request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        adminApi.MapPost("/auth/login", async (
            LoginRequest request,
            SystemAdminCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.LoginAsync(request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        adminApi.MapPost("/auth/change-password", async (
            ChangePasswordRequest request,
            SystemAdminCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.ChangePasswordAsync(request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        adminApi.MapPost("/doa/evaluate", async (
            EvaluateDoaRequest request,
            SystemAdminCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.EvaluateDoaAsync(request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        adminApi.MapPost("/doa/overrides/{id:guid}/authorize", async (
            Guid id,
            AuthorizeOverrideRequest request,
            SystemAdminCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.AuthorizeOverrideAsync(id, request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or KeyNotFoundException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        adminApi.MapGet("/users/{userId}", async (
            string userId,
            SystemAdminCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.GetUserAsync(userId, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"User '{userId}' was not found." });
            }
        });

        adminApi.MapGet("/audit/journals", async (
            JournalCategory? category,
            DateTime? fromUtc,
            DateTime? toUtc,
            SystemAdminCommandHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.GetAuditJournalsAsync(category, fromUtc, toUtc, ct);
            return Results.Ok(result);
        });

        // -------------------------------------------------------------
        // MODULE 11: MIS & STATUTORY REGULATORY REPORTING ENGINE
        // (BRPD Circular 12/2024 Module 11 & Banking Companies Act 1991 §35)
        // -------------------------------------------------------------
        var misApi = app.MapGroup("/api/v1/mis/reports");

        misApi.MapGet("/daily-clean-cash", async (
            string? branchCode,
            string? date,
            MisReportingCommandHandler handler,
            CancellationToken ct) =>
        {
            var parsedDate = !string.IsNullOrWhiteSpace(date) && DateOnly.TryParse(date, out var d) ? d : DateOnly.FromDateTime(DateTime.UtcNow);
            var report = await handler.GetDailyCleanCashReportAsync(branchCode, parsedDate, ct);
            return Results.Ok(report);
        });

        misApi.MapGet("/daily-affairs", async (
            string? branchCode,
            string? date,
            MisReportingCommandHandler handler,
            CancellationToken ct) =>
        {
            var parsedDate = !string.IsNullOrWhiteSpace(date) && DateOnly.TryParse(date, out var d) ? d : DateOnly.FromDateTime(DateTime.UtcNow);
            var report = await handler.GetDailyAffairsReportAsync(branchCode, parsedDate, ct);
            return Results.Ok(report);
        });

        misApi.MapGet("/deposit-analytics", async (
            string? asOfDate,
            MisReportingCommandHandler handler,
            CancellationToken ct) =>
        {
            var parsedDate = !string.IsNullOrWhiteSpace(asOfDate) && DateOnly.TryParse(asOfDate, out var d) ? d : DateOnly.FromDateTime(DateTime.UtcNow);
            var report = await handler.GetDepositAnalyticsReportAsync(parsedDate, ct);
            return Results.Ok(report);
        });

        misApi.MapPost("/unclaimed-deposits/scan", async (
            string? asOfDate,
            MisReportingCommandHandler handler,
            CancellationToken ct) =>
        {
            var parsedDate = !string.IsNullOrWhiteSpace(asOfDate) && DateOnly.TryParse(asOfDate, out var d) ? d : DateOnly.FromDateTime(DateTime.UtcNow);
            var result = await handler.ScanAndRegisterUnclaimedDepositsAsync(parsedDate, ct);
            return Results.Ok(result);
        });

        misApi.MapPost("/unclaimed-deposits/notice", async (
            IssueNoticeRequestDto request,
            MisReportingCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                await handler.IssueUnclaimedNoticeAsync(request, ct);
                return Results.Ok(new { message = $"Notice successfully issued for unclaimed account '{request.AccountNumber}'." });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        misApi.MapPost("/unclaimed-deposits/transfer-to-bb", async (
            TransferUnclaimedDepositsRequestDto request,
            MisReportingCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.TransferUnclaimedDepositsToCentralBankAsync(request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        misApi.MapGet("/sbs3-classification", async (
            string? asOfDate,
            MisReportingCommandHandler handler,
            CancellationToken ct) =>
        {
            var parsedDate = !string.IsNullOrWhiteSpace(asOfDate) && DateOnly.TryParse(asOfDate, out var d) ? d : DateOnly.FromDateTime(DateTime.UtcNow);
            var report = await handler.GetSbs3ClassificationReportAsync(parsedDate, ct);
            return Results.Ok(report);
        });

        misApi.MapGet("/foreign-trade-summary", async (
            string? asOfDate,
            MisReportingCommandHandler handler,
            CancellationToken ct) =>
        {
            var parsedDate = !string.IsNullOrWhiteSpace(asOfDate) && DateOnly.TryParse(asOfDate, out var d) ? d : DateOnly.FromDateTime(DateTime.UtcNow);
            var report = await handler.GetForeignTradeSummaryReportAsync(parsedDate, ct);
            return Results.Ok(report);
        });

        misApi.MapGet("/remittance-incentives", async (
            string? fromDate,
            string? toDate,
            MisReportingCommandHandler handler,
            CancellationToken ct) =>
        {
            var start = !string.IsNullOrWhiteSpace(fromDate) && DateOnly.TryParse(fromDate, out var s) ? s : DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1));
            var end = !string.IsNullOrWhiteSpace(toDate) && DateOnly.TryParse(toDate, out var e) ? e : DateOnly.FromDateTime(DateTime.UtcNow);
            var report = await handler.GetRemittanceIncentiveReportAsync(start, end, ct);
            return Results.Ok(report);
        });

        misApi.MapGet("/adc-reconciliation", async (
            string? date,
            MisReportingCommandHandler handler,
            CancellationToken ct) =>
        {
            var parsedDate = !string.IsNullOrWhiteSpace(date) && DateOnly.TryParse(date, out var d) ? d : DateOnly.FromDateTime(DateTime.UtcNow);
            var report = await handler.GetAdcReconciliationReportAsync(parsedDate, ct);
            return Results.Ok(report);
        });

        misApi.MapPost("/archives/seal", async (
            SealReportRequestDto request,
            MisReportingCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var summary = await handler.SealStatutoryReportAsync(request, ct);
                return Results.Created($"/api/v1/mis/reports/archives/{summary.Id}", summary);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        misApi.MapPost("/archives/{id:guid}/submit", async (
            Guid id,
            SubmitReportRequestDto request,
            MisReportingCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                await handler.SubmitStatutoryReportAsync(id, request, ct);
                return Results.Ok(new { message = $"Statutory report '{id}' submitted successfully under reference '{request.SubmissionReference}'." });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        misApi.MapGet("/archives", async (
            RegulatoryReportType? type,
            string? fromDate,
            string? toDate,
            MisReportingCommandHandler handler,
            CancellationToken ct) =>
        {
            DateOnly? s = !string.IsNullOrWhiteSpace(fromDate) && DateOnly.TryParse(fromDate, out var p1) ? p1 : null;
            DateOnly? e = !string.IsNullOrWhiteSpace(toDate) && DateOnly.TryParse(toDate, out var p2) ? p2 : null;
            var archives = await handler.GetArchivedReportsAsync(type, s, e, ct);
            return Results.Ok(archives);
        });

        misApi.MapGet("/archives/{id:guid}", async (
            Guid id,
            MisReportingCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var (summary, payload) = await handler.GetArchivedReportDetailAsync(id, ct);
                return Results.Ok(new { summary, payload });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        // -------------------------------------------------------------
        // Phase 17: User Training, Customer Security Awareness & Master BRPD 12/2024 Compliance Certification
        // -------------------------------------------------------------
        var trainingApi = app.MapGroup("/api/v1/training").WithTags("Training & BRPD 12/2024 Compliance");

        // Simulation Sandbox
        trainingApi.MapPost("/simulations/start", async (
            StartSimulationRequestDto request,
            TrainingAndAwarenessCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.StartSimulationAsync(request, ct);
                return Results.Created($"/api/v1/training/simulations/{result.Id}", result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        trainingApi.MapPost("/simulations/{id:guid}/steps", async (
            Guid id,
            RecordSimulationStepRequestDto request,
            TrainingAndAwarenessCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.RecordSimulationStepAsync(id, request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        trainingApi.MapPost("/simulations/{id:guid}/complete", async (
            Guid id,
            CompleteSimulationRequestDto request,
            TrainingAndAwarenessCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.CompleteSimulationAsync(id, request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        trainingApi.MapGet("/simulations/user/{userId}", async (
            string userId,
            TrainingAndAwarenessCommandHandler handler,
            CancellationToken ct) =>
        {
            var sessions = await handler.GetSimulationsByUserAsync(userId, ct);
            return Results.Ok(sessions);
        });

        // Employee Certifications
        trainingApi.MapPost("/certifications/grant", async (
            GrantCertificationRequestDto request,
            TrainingAndAwarenessCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.GrantCertificationAsync(request, ct);
                return Results.Created($"/api/v1/training/certifications/{result.Id}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        trainingApi.MapGet("/certifications/employee/{employeeId}", async (
            string employeeId,
            TrainingAndAwarenessCommandHandler handler,
            CancellationToken ct) =>
        {
            var certs = await handler.GetCertificationsByEmployeeAsync(employeeId, ct);
            return Results.Ok(certs);
        });

        trainingApi.MapGet("/certifications/lapsed", async (
            string? asOfDate,
            TrainingAndAwarenessCommandHandler handler,
            CancellationToken ct) =>
        {
            DateTime targetDate = !string.IsNullOrWhiteSpace(asOfDate) && DateTime.TryParse(asOfDate, out var dt)
                ? dt
                : DateTime.UtcNow;

            var lapsed = await handler.GetLapsedCertificationsAsync(targetDate, ct);
            return Results.Ok(lapsed);
        });

        // Customer Security Broadcasts
        trainingApi.MapPost("/broadcasts/publish", async (
            CreateBroadcastRequestDto request,
            TrainingAndAwarenessCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.PublishBroadcastAsync(request, ct);
                return Results.Created($"/api/v1/training/broadcasts/{result.Id}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        trainingApi.MapGet("/broadcasts/active", async (
            string? asOfDate,
            TrainingAndAwarenessCommandHandler handler,
            CancellationToken ct) =>
        {
            DateOnly targetDate = !string.IsNullOrWhiteSpace(asOfDate) && DateOnly.TryParse(asOfDate, out var d)
                ? d
                : DateOnly.FromDateTime(DateTime.UtcNow);

            var active = await handler.GetActiveBroadcastsAsync(targetDate, ct);
            return Results.Ok(active);
        });

        // In-App User Manuals & SOPs
        trainingApi.MapGet("/manuals/modules/{moduleId}", async (
            string moduleId,
            TrainingAndAwarenessCommandHandler handler,
            CancellationToken ct) =>
        {
            if (!Enum.TryParse<BbrpdModuleId>(moduleId, true, out var parsedModule))
            {
                return Results.BadRequest(new { error = $"Invalid BbrpdModuleId '{moduleId}'." });
            }

            var topics = await handler.GetUserManualTopicsByModuleAsync(parsedModule, ct);
            return Results.Ok(topics);
        });

        trainingApi.MapGet("/manuals/search", async (
            string q,
            TrainingAndAwarenessCommandHandler handler,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.BadRequest(new { error = "Search query parameter 'q' is required." });
            }

            var results = await handler.SearchUserManualTopicsAsync(q, ct);
            return Results.Ok(results);
        });

        // Master BRPD 12/2024 Compliance Certification & Attestation
        trainingApi.MapGet("/compliance/master-attestation", async (
            BbrpdMasterComplianceService complianceService,
            CancellationToken ct) =>
        {
            var report = await complianceService.EvaluateMasterComplianceAsync(ct);
            return Results.Ok(report);
        });

        // -------------------------------------------------------------
        // Phase 18: Islamic Banking & Shariah Compliance Engine
        // -------------------------------------------------------------
        var shariahApi = app.MapGroup("/api/v1/shariah").WithTags("Islamic Banking & Shariah Compliance");

        // 1. Mudaraba Investment Pools
        shariahApi.MapPost("/pools", async (
            CreateMudarabaPoolRequestDto request,
            ShariahCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.CreateMudarabaPoolAsync(request, ct);
                return Results.Created($"/api/v1/shariah/pools/{result.Id}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        shariahApi.MapPost("/pools/{id:guid}/weightages", async (
            Guid id,
            ConfigureWeightageRequestDto request,
            ShariahCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.ConfigureWeightageAsync(id, request, ct);
                return Results.Ok(result);
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

        shariahApi.MapPost("/pools/{id:guid}/distribute", async (
            Guid id,
            DistributePoolProfitRequestDto request,
            ShariahCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.DistributePoolProfitAsync(id, request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        shariahApi.MapGet("/pools", async (
            ShariahCommandHandler handler,
            CancellationToken ct) =>
        {
            var pools = await handler.GetPoolsAsync(ct);
            return Results.Ok(pools);
        });

        // 2. Al-Wadiah Current Accounts
        shariahApi.MapPost("/al-wadiah/open", async (
            OpenAlWadiahAccountRequestDto request,
            ShariahCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.OpenAlWadiahAccountAsync(request, ct);
                return Results.Created($"/api/v1/shariah/al-wadiah/{result.AccountNumber}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        shariahApi.MapGet("/al-wadiah/{accountNumber}", async (
            string accountNumber,
            ShariahCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.GetAlWadiahAccountAsync(accountNumber, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        // 3. Bai-Murabaha Financing Facilities
        shariahApi.MapPost("/murabaha/create", async (
            CreateMurabahaFacilityRequestDto request,
            ShariahCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.CreateMurabahaFacilityAsync(request, ct);
                return Results.Created($"/api/v1/shariah/murabaha/{result.Id}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        shariahApi.MapPost("/murabaha/{id:guid}/inspect-goods", async (
            Guid id,
            InspectGoodsRequestDto request,
            ShariahCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.InspectGoodsAsync(id, request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        shariahApi.MapPost("/murabaha/{id:guid}/disburse-vendor", async (
            Guid id,
            DisburseMurabahaToVendorRequestDto request,
            ShariahCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.DisburseMurabahaToVendorAsync(id, request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        shariahApi.MapGet("/murabaha/{id:guid}", async (
            Guid id,
            ShariahCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.GetMurabahaFacilityAsync(id, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        // 4. Quard Financing Facilities
        shariahApi.MapPost("/quard/create", async (
            CreateQuardFacilityRequestDto request,
            ShariahCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.CreateQuardFacilityAsync(request, ct);
                return Results.Created($"/api/v1/shariah/quard/{result.Id}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        shariahApi.MapGet("/quard/{id:guid}", async (
            Guid id,
            ShariahCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.GetQuardFacilityAsync(id, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        // 5. Ta'widh Charity Fund
        shariahApi.MapPost("/tawidh/accrue", async (
            AccrueTaWidhPenaltyRequestDto request,
            ShariahCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.AccrueTaWidhPenaltyAsync(request, ct);
                return Results.Created($"/api/v1/shariah/tawidh/{result.Id}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        shariahApi.MapPost("/tawidh/{id:guid}/disburse-charity", async (
            Guid id,
            DisburseCharityFundRequestDto request,
            ShariahCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.DisburseCharityFundAsync(id, request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        shariahApi.MapGet("/tawidh/summary", async (
            ShariahCommandHandler handler,
            CancellationToken ct) =>
        {
            var summary = await handler.GetTaWidhSummaryAsync(ct);
            return Results.Ok(summary);
        });

        // --- PHASE 20: SPECIALIZED CREDIT, AGRICULTURAL FINANCE & REMITTANCE TEST KEY ENDPOINTS ---
        // Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 4.4, 4.6, 3.7, 3.12, 3.13

        // 1. Agricultural Loan Endpoints (BRPD 4.4)
        var agriApi = app.MapGroup("/api/v1/specialized-lending/agri")
            .WithTags("Agricultural Credit (BRPD 4.4)");

        agriApi.MapPost("/initiate", async (
            InitiateAgriLoanRequest request,
            SpecializedBankingCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.InitiateAgriLoanAsync(request, ct);
                return Results.Created($"/api/v1/specialized-lending/agri/{result.AccountNumber}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        agriApi.MapPost("/disburse", async (
            DisburseAgriLoanRequest request,
            SpecializedBankingCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.DisburseAgriLoanAsync(request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        agriApi.MapPost("/repay", async (
            RepayAgriLoanRequest request,
            SpecializedBankingCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.RepayAgriLoanAsync(request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        agriApi.MapPost("/reschedule-disaster", async (
            RescheduleAgriLoanRequest request,
            SpecializedBankingCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.RescheduleAgriLoanAsync(request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        agriApi.MapGet("/{accountNumber}", async (
            string accountNumber,
            SpecializedBankingCommandHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.GetAgriLoanAsync(accountNumber, ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        });

        // 2. Bill Discounting & Inland Bill Purchase (IBP) Endpoints (BRPD 4.6)
        var billsApi = app.MapGroup("/api/v1/specialized-lending/bills")
            .WithTags("Bill Discounting (BRPD 4.6)");

        billsApi.MapPost("/discount", async (
            DiscountBillRequest request,
            SpecializedBankingCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.DiscountBillAsync(request, ct);
                return Results.Created($"/api/v1/specialized-lending/bills/{result.AccountNumber}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        billsApi.MapPost("/amortize", async (
            AmortizeBillDiscountRequest request,
            SpecializedBankingCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.AmortizeBillDiscountAsync(request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        billsApi.MapPost("/early-retire", async (
            EarlyRetireBillRequest request,
            SpecializedBankingCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.EarlyRetireBillAsync(request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        billsApi.MapPost("/liquidate", async (
            LiquidateBillRequest request,
            SpecializedBankingCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.LiquidateBillAsync(request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        billsApi.MapGet("/{accountNumber}", async (
            string accountNumber,
            SpecializedBankingCommandHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.GetDiscountedBillAsync(accountNumber, ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        });

        // 3. Deposit Multiplication Schemes (BRPD 3.7)
        var multiplicationApi = app.MapGroup("/api/v1/specialized-deposits/multiplication")
            .WithTags("Deposit Multiplication Schemes (BRPD 3.7)");

        multiplicationApi.MapPost("/open", async (
            OpenMultiplicationAccountRequest request,
            SpecializedBankingCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.OpenMultiplicationAccountAsync(request, ct);
                return Results.Created($"/api/v1/specialized-deposits/multiplication/{result.AccountNumber}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        multiplicationApi.MapPost("/encash", async (
            EncashMultiplicationAccountRequest request,
            SpecializedBankingCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.EncashMultiplicationAccountAsync(request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        multiplicationApi.MapGet("/{accountNumber}", async (
            string accountNumber,
            SpecializedBankingCommandHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.GetMultiplicationAccountAsync(accountNumber, ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        });

        // 4. Inter-Branch Remittance & Cryptographic Test Key Engine (BRPD 3.12, 3.13)
        var remittanceTestKeyApi = app.MapGroup("/api/v1/inter-branch-remittance/test-key")
            .WithTags("Inter-Branch Remittance Test Key (BRPD 3.12, 3.13)");

        remittanceTestKeyApi.MapPost("/issue", async (
            IssueInterBranchInstrumentRequest request,
            SpecializedBankingCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.IssueInstrumentAsync(request, ct);
                return Results.Created($"/api/v1/inter-branch-remittance/test-key/{result.InstrumentNumber}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        remittanceTestKeyApi.MapPost("/verify-pay", async (
            VerifyAndPayInterBranchInstrumentRequest request,
            SpecializedBankingCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.VerifyAndPayInstrumentAsync(request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        remittanceTestKeyApi.MapPost("/cancel", async (
            CancelInterBranchInstrumentRequest request,
            SpecializedBankingCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.CancelInstrumentAsync(request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        remittanceTestKeyApi.MapGet("/{instrumentNumber}", async (
            string instrumentNumber,
            SpecializedBankingCommandHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.GetPaymentInstrumentAsync(instrumentNumber, ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        });

        // --- PHASE 21: DEMAND LOANS (PAD, LTR, FBP/IBP, CLEAN DEMAND LOANS, FORCED IMPORT RETIREMENT) (BRPD Circular 12 Module 4.5 & 5.1.4) ---
        var demandLoanApi = app.MapGroup("/api/v1/demand-loans")
            .AddEndpointFilter<FinancialAuthorizationFilter>()
            .WithTags("Demand Loans & Trade Financing (BRPD 4.5, 5.1.4)");

        demandLoanApi.MapPost("/initiate", async (
            InitiateDemandLoanRequest request,
            DemandLoanCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleInitiateAsync(request, ct);
                return Results.Created($"/api/v1/demand-loans/{result.LoanAccountNumber}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        demandLoanApi.MapPost("/disburse", async (
            DisburseDemandLoanRequest request,
            DemandLoanCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleDisburseAsync(request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        demandLoanApi.MapPost("/accrue-daily", async (
            AccrueDailyInterestRequest request,
            DemandLoanCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleAccrueDailyAsync(request, ct);
                return Results.Ok(result);
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

        demandLoanApi.MapPost("/capitalize-interest", async (
            CapitalizeInterestRequest request,
            DemandLoanCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleCapitalizeInterestAsync(request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        demandLoanApi.MapPost("/repay", async (
            RepayDemandLoanRequest request,
            DemandLoanCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleRepayAsync(request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        demandLoanApi.MapPost("/forced-pad-from-import", async (
            ForcedPadRequest request,
            DemandLoanCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleForcedPadAsync(request, ct);
                return Results.Created($"/api/v1/demand-loans/{result.LoanAccountNumber}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        demandLoanApi.MapGet("/{accountNumber}", async (
            string accountNumber,
            DemandLoanCommandHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleGetAsync(accountNumber, ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        });

        // --- PHASE 21: SECURITY DEPOSIT RECEIPTS (SDR) & LOCAL REMITTANCE ENGINE (BRPD Circular 12 Module 2.1) ---
        var sdrApi = app.MapGroup("/api/v1/local-remittance/sdr")
            .AddEndpointFilter<FinancialAuthorizationFilter>()
            .WithTags("Security Deposit Receipts (BRPD 2.1.4)");

        sdrApi.MapPost("/issue", async (
            IssueSdrRequest request,
            SecurityDepositReceiptCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleIssueSdrAsync(request, ct);
                return Results.Created($"/api/v1/local-remittance/sdr/{result.SdrNumber}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        sdrApi.MapPost("/lien", async (
            LienSdrRequest request,
            SecurityDepositReceiptCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleLienSdrAsync(request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        sdrApi.MapPost("/release-refund", async (
            ReleaseSdrRequest request,
            SecurityDepositReceiptCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleReleaseSdrAsync(request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        sdrApi.MapPost("/forfeit-pay", async (
            ForfeitSdrRequest request,
            SecurityDepositReceiptCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleForfeitSdrAsync(request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        sdrApi.MapPost("/stop-payment", async (
            StopSdrRequest request,
            SecurityDepositReceiptCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleStopSdrAsync(request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        sdrApi.MapPost("/revoke-stop-payment", async (
            RevokeStopSdrRequest request,
            SecurityDepositReceiptCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleRevokeStopSdrAsync(request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        sdrApi.MapPost("/issue-duplicate", async (
            IssueDuplicateSdrRequest request,
            SecurityDepositReceiptCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleIssueDuplicateSdrAsync(request, ct);
                return Results.Created($"/api/v1/local-remittance/sdr/{result.SdrNumber}", result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        sdrApi.MapGet("/{sdrNumber}", async (
            string sdrNumber,
            SecurityDepositReceiptCommandHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleGetSdrAsync(sdrNumber, ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        });

        // PO/DD Stop Payment Revocation (BRPD 2.1.1-ix, 2.1.2-vi)
        var remittanceStopApi = app.MapGroup("/api/v1/local-remittance/stop-payment")
            .AddEndpointFilter<FinancialAuthorizationFilter>()
            .WithTags("Remittance Stop Payment Revocation (BRPD 2.1.1, 2.1.2)");

        remittanceStopApi.MapPost("/revoke", async (
            RevokeInstrumentStopPaymentRequest request,
            SecurityDepositReceiptCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var success = await handler.HandleRevokeInstrumentStopPaymentAsync(request, ct);
                return Results.Ok(new { success, message = $"Stop payment on instrument '{request.InstrumentNumber}' revoked successfully." });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // --- PHASE 22: BANK GUARANTEE (BG) LIFECYCLE ENGINE (BRPD Circular 12 Module 5.4) ---
        var bankGuaranteeApi = app.MapGroup("/api/v1/trade/guarantees")
            .AddEndpointFilter<FinancialAuthorizationFilter>()
            .WithTags("Bank Guarantees (BRPD 5.4)");

        bankGuaranteeApi.MapPost("/issue", async (
            IssueBankGuaranteeRequest request,
            BankGuaranteeCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleIssueAsync(request, ct);
                return Results.Created($"/api/v1/trade/guarantees/{result.BgNumber}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        bankGuaranteeApi.MapPost("/amend", async (
            AmendBankGuaranteeRequest request,
            BankGuaranteeCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleAmendAsync(request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        bankGuaranteeApi.MapPost("/invoke-claim", async (
            InvokeGuaranteeClaimRequest request,
            BankGuaranteeCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleInvokeClaimAsync(request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        bankGuaranteeApi.MapPost("/liquidate-claim", async (
            LiquidateGuaranteeClaimRequest request,
            BankGuaranteeCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleLiquidateClaimAsync(request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        bankGuaranteeApi.MapPost("/cancel-release", async (
            CancelBankGuaranteeRequest request,
            BankGuaranteeCommandHandler handler,
            CancellationToken ct) =>
        {
            try
            {
                var result = await handler.HandleCancelReleaseAsync(request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        bankGuaranteeApi.MapGet("/{bgNumber}", async (
            string bgNumber,
            BankGuaranteeCommandHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleGetAsync(bgNumber, ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        });

        // --- PHASE 22: MULTI-CHANNEL RECONCILIATION AGING & PROVISIONING ENGINE (BRPD Circular 12 Module 1.4) ---
        var reconAgingApi = app.MapGroup("/api/v1/reconciliation/aging")
            .WithTags("Reconciliation Aging & Statutory Provisions (BRPD 1.4)");

        reconAgingApi.MapPost("/record", async (
            RecordUnreconciledItemRequest request,
            ReconciliationAgingService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.RecordUnreconciledItemAsync(request, ct);
                return Results.Created($"/api/v1/reconciliation/aging/{result.Id}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        reconAgingApi.MapPost("/resolve", async (
            ResolveUnreconciledItemRequest request,
            ReconciliationAgingService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.ResolveItemAsync(request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        reconAgingApi.MapGet("/summary", async (
            DateOnly? assessmentDate,
            string? channelType,
            ReconciliationAgingService service,
            CancellationToken ct) =>
        {
            var targetDate = assessmentDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var result = await service.GetAgingSummaryAsync(targetDate, channelType, ct);
            return Results.Ok(result);
        });

        // --- PHASE 26: MASTER EXPORT LC & B2B IMPORT LC ENGINE (BRPD Circular 12 Modules 5.5 & 5.6) ---
        var masterExportLcApi = app.MapGroup("/api/v1/trade/master-lcs")
            .AddEndpointFilter<FinancialAuthorizationFilter>()
            .WithTags("Master Export LC & B2B Engine (BRPD 5.5, 5.6)");

        masterExportLcApi.MapPost("/register", async (
            RegisterMasterExportLcRequest request,
            IMasterExportLcService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.RegisterMasterExportLcAsync(request, ct);
                return Results.Created($"/api/v1/trade/master-lcs/{result.MasterLcNumber}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        masterExportLcApi.MapPost("/{lcNumber}/authorize", async (
            string lcNumber,
            AuthorizeMasterExportLcRequest request,
            IMasterExportLcService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.AuthorizeMasterExportLcAsync(lcNumber, request, ct);
                return Results.Ok(result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        masterExportLcApi.MapGet("/{lcNumber}", async (
            string lcNumber,
            IMasterExportLcService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.GetMasterExportLcAsync(lcNumber, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        masterExportLcApi.MapPost("/{lcNumber}/b2b-lcs", async (
            string lcNumber,
            OpenB2bLcUnderMasterRequest request,
            IMasterExportLcService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.IssueB2bLcUnderMasterAsync(lcNumber, request, ct);
                return Results.Created($"/api/v1/trade/master-lcs/{lcNumber}/b2b-lcs/{result.B2bLcNumber}", result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        masterExportLcApi.MapPost("/{lcNumber}/amend", async (
            string lcNumber,
            AmendMasterLcRequest request,
            IMasterExportLcService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.AmendMasterExportLcAsync(lcNumber, request, ct);
                return Results.Ok(result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        masterExportLcApi.MapPost("/{lcNumber}/close", async (
            string lcNumber,
            CloseMasterLcRequest request,
            IMasterExportLcService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.CloseMasterExportLcAsync(lcNumber, request, ct);
                return Results.Ok(result);
            }
            catch (InvariantViolationException ex)
            {
                return Results.BadRequest(new { error = ex.Message, invariant = ex.InvariantName, regulatoryRef = ex.RegulatoryReference });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        masterExportLcApi.MapGet("/{lcNumber}/history", async (
            string lcNumber,
            IMasterExportLcService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.GetMasterLcHistoryReportAsync(lcNumber, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        // --- PHASE 26: TRADE BASED MONEY LAUNDERING (TBML) PREVENTION ENGINE (BRPD Circular 12 Module 5.7 & 5.0) ---
        var tbmlApi = app.MapGroup("/api/v1/trade/tbml")
            .AddEndpointFilter<FinancialAuthorizationFilter>()
            .WithTags("TBML Prevention & Price Verification (BRPD 5.7, 5.0)");

        tbmlApi.MapPost("/ttp", async (
            CreateOrUpdateTtpRequest request,
            ITbmlPreventionService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.CreateOrUpdateTtpAsync(request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        tbmlApi.MapGet("/ttp/{customerCif}", async (
            string customerCif,
            ITbmlPreventionService service,
            CancellationToken ct) =>
        {
            var result = await service.GetTtpByCifAsync(customerCif, ct);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        });

        tbmlApi.MapPost("/verify-price", async (
            VerifyPriceRequest request,
            ITbmlPreventionService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.VerifyPriceAsync(request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        tbmlApi.MapPost("/screen-transaction", async (
            ScreenTbmlRequest request,
            ITbmlPreventionService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.ScreenTradeTransactionAsync(request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message, stack = ex.ToString() });
            }
        });

        tbmlApi.MapPost("/vessels/track", async (
            TrackVesselRequest request,
            ITbmlPreventionService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.TrackVesselAsync(request, ct);
                return Results.Created($"/api/v1/trade/tbml/vessels/{result.Id}", result);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        tbmlApi.MapPost("/alerts/{alertId:guid}/escalate-str", async (
            Guid alertId,
            EscalateTbmlAlertRequest request,
            ITbmlPreventionService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.EscalateAlertToCamlcoAsync(alertId, request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        tbmlApi.MapGet("/alerts/active", async (
            ITbmlPreventionService service,
            CancellationToken ct) =>
        {
            var result = await service.GetActiveAlertsAsync(ct);
            return Results.Ok(result);
        });

        // --- PHASE 32: CBS TRANSACTION GOVERNANCE, PAM BREAK-GLASS & DAILY EOD PROOFING (BRPD 12/2024 Modules 9, 10 & 11) ---
        var governanceApi = app.MapGroup("/api/v1/governance").WithTags("CBS Governance & Anomaly Detection");

        governanceApi.MapPost("/anomalies/detect", async (
            DetectAnomalyRequest request,
            IGovernanceAndProofingService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.ScreenAndDetectAnomalyAsync(request, ct);
                return Results.Ok(result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        governanceApi.MapPost("/anomalies/{anomalyId}/resolve", async (
            string anomalyId,
            ResolveAnomalyRequest request,
            IGovernanceAndProofingService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.ResolveAnomalyAsync(anomalyId, request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        governanceApi.MapGet("/anomalies/pending", async (
            IGovernanceAndProofingService service,
            CancellationToken ct) =>
        {
            var result = await service.GetPendingAnomaliesAsync(ct);
            return Results.Ok(result);
        });

        var securityBreakGlassApi = app.MapGroup("/api/v1/security").WithTags("CBS Security & Emergency Break-Glass");

        securityBreakGlassApi.MapPost("/break-glass/request", async (
            RequestBreakGlassSessionRequest request,
            IGovernanceAndProofingService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.RequestBreakGlassSessionAsync(request, ct);
                return Results.Created($"/api/v1/security/break-glass/{result.SessionId}", result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        securityBreakGlassApi.MapPost("/break-glass/{sessionId}/approve", async (
            string sessionId,
            ApproveBreakGlassSessionRequest request,
            IGovernanceAndProofingService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.ApproveBreakGlassSessionAsync(sessionId, request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        securityBreakGlassApi.MapPost("/break-glass/{sessionId}/revoke", async (
            string sessionId,
            RevokeBreakGlassSessionRequest request,
            IGovernanceAndProofingService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.RevokeBreakGlassSessionAsync(sessionId, request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        securityBreakGlassApi.MapGet("/break-glass/{sessionId}", async (
            string sessionId,
            IGovernanceAndProofingService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.GetBreakGlassSessionAsync(sessionId, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        securityBreakGlassApi.MapPost("/break-glass/{sessionId}/validate", async (
            string sessionId,
            IGovernanceAndProofingService service,
            CancellationToken ct) =>
        {
            try
            {
                var isValid = await service.ValidateActiveBreakGlassSessionAsync(sessionId, ct);
                return Results.Ok(new { sessionId, isValid });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        var eodBalancingApi = app.MapGroup("/api/v1/eod").WithTags("CBS EOD Balancing & Multi-Rail Proofing");

        eodBalancingApi.MapPost("/balancing/generate", async (
            GenerateDailyBalancingSheetRequest request,
            IGovernanceAndProofingService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.GenerateDailyBalancingSheetAsync(request, ct);
                return Results.Created($"/api/v1/eod/balancing/{result.SheetId}", result);
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        eodBalancingApi.MapPost("/balancing/{sheetId}/verify-and-proof", async (
            string sheetId,
            ProofDailyBooksRequest request,
            IGovernanceAndProofingService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.ProofDailyBooksAsync(sheetId, request, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex) when (ex is InvariantViolationException or ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        eodBalancingApi.MapGet("/balancing/{sheetId}", async (
            string sheetId,
            IGovernanceAndProofingService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.GetDailyBalancingSheetAsync(sheetId, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        // --- PRODUCTION HEALTH & PROBE ENDPOINTS ---
        app.MapGet("/health", () => Results.Ok(new
        {
            status = "Healthy",
            timestampUtc = DateTime.UtcNow,
            service = "CBS Bangladesh Core Banking System",
            version = "2.0.0",
            regulation = "Bangladesh Bank BRPD Circular No. 12 (13 May 2024)"
        })).AllowAnonymous();

        app.MapGet("/health/live", () => Results.Ok(new { status = "Live", timestampUtc = DateTime.UtcNow })).AllowAnonymous();

        app.MapGet("/health/ready", async (CbsDbContext dbContext, CancellationToken ct) =>
        {
            try
            {
                var canConnect = await dbContext.Database.CanConnectAsync(ct);
                if (canConnect)
                {
                    return Results.Ok(new { status = "Ready", database = "Connected", timestampUtc = DateTime.UtcNow });
                }
                return Results.Json(new { status = "Not Ready", database = "Cannot Connect", timestampUtc = DateTime.UtcNow }, statusCode: 503);
            }
            catch (Exception ex)
            {
                return Results.Json(new { status = "Degraded", error = ex.Message, timestampUtc = DateTime.UtcNow }, statusCode: 503);
            }
        }).AllowAnonymous();

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

public sealed record EvaluateClassificationEodRequest(string BranchCode, DateTime? EvaluationDate);
public sealed record ApplyQualitativeJudgmentApiRequest(
    CBS.Domain.Enums.LoanClassificationGrade Grade,
    CBS.Domain.Enums.QualitativeJudgmentReason Reason,
    string JustificationNotes,
    string? InspectionReportReference,
    string InspectorUserId,
    string CheckerUserId
);
public sealed record RevokeQualitativeJudgmentApiRequest(
    string InspectorUserId,
    string CheckerUserId,
    string RevocationReason
);
public sealed record RescheduleLoanApiRequest(
    int RescheduleIteration,
    decimal DownPaymentPaid,
    decimal RequiredDownPayment,
    DateOnly NewMaturityDate,
    decimal NewInterestRate,
    CBS.Domain.Enums.RescheduleApprovalAuthority ApprovalAuthority,
    string? BoardResolutionReference,
    DateOnly? BoardApprovalDate,
    decimal? InterestWaiver,
    string RestructuringNotes,
    string MakerUserId,
    string CheckerUserId
);
public sealed record WriteOffLoanApiRequest(
    decimal SpecificProvisionAvailable,
    string BoardResolutionNumber,
    DateOnly BoardApprovalDate,
    CBS.Domain.Enums.LegalCaseType LegalCaseType,
    string? ArthaRinCaseNumber,
    DateOnly? ArthaRinFilingDate,
    string MakerUserId,
    string CheckerUserId,
    string JustificationNotes
);
public sealed record RecordWriteOffRecoveryApiRequest(
    decimal Amount,
    string RecoverySource,
    string ReferenceNumber,
    string ReceivedByUserId
);
