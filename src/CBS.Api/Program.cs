using CBS.Application.Accounts.Commands.OpenDepositAccount;
using CBS.Application.Accounts.Commands.PostTransaction;
using CBS.Application.Accounts.DTOs;
using CBS.Application.Common.Interfaces;
using CBS.Application.Customers.Commands.CreateCustomerDraft;
using CBS.Application.Customers.Commands.VerifyCustomer;
using CBS.Application.Customers.DTOs;
using CBS.Application.GeneralBanking.Commands.RunBranchEodBatch;
using CBS.Domain.Accounts;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;
using CBS.Infrastructure.Gateways;
using CBS.Infrastructure.Persistence;
using CBS.Infrastructure.Security;

var app = Program.CreateWebApplication(args);
app.Run();

public partial class Program
{
    public static WebApplication CreateWebApplication(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // 1. Register Core Infrastructure & Application Services
        builder.Services.AddSingleton<ICustomerRepository, InMemoryCustomerRepository>();
        builder.Services.AddSingleton<IAccountRepository, InMemoryAccountRepository>();
        builder.Services.AddSingleton<IBranchOperatingDayRepository, InMemoryBranchOperatingDayRepository>();
        builder.Services.AddSingleton<IElectionCommissionGateway, MockElectionCommissionGateway>();
        builder.Services.AddSingleton<ISanctionScreeningService, MockSanctionScreeningService>();

        // 2. Register Application Command Handlers
        builder.Services.AddTransient<CreateCustomerDraftCommandHandler>();
        builder.Services.AddTransient<VerifyCustomerCommandHandler>();
        builder.Services.AddTransient<OpenDepositAccountCommandHandler>();
        builder.Services.AddTransient<PostTransactionCommandHandler>();
        builder.Services.AddTransient<RunBranchEodBatchCommandHandler>();

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
        });

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        app.UseDefaultFiles();
        app.UseStaticFiles();

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
        var accountApi = app.MapGroup("/api/v1/accounts");

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

        return app;
    }
}

public record VerifyCustomerRequest(bool IsApproved, string? RejectionReason = null);
public record ReactivateDormantRequest(string Remarks);
public record SetPendingVouchersRequest(string BranchCode, int PendingCount);
