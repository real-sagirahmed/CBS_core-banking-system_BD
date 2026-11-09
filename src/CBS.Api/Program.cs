using CBS.Application.Accounts.Commands.OpenDepositAccount;
using CBS.Application.Accounts.Commands.PostTransaction;
using CBS.Application.Accounts.DTOs;
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
using CBS.Application.Lending.Commands;
using CBS.Application.Lending.DTOs;
using CBS.Application.PaymentRails.Commands.InitiateBeftn;
using CBS.Application.PaymentRails.Commands.PresentCheque;
using CBS.Application.PaymentRails.Commands.ProcessInwardCheque;
using CBS.Application.PaymentRails.Commands.Rtgs;
using CBS.Application.PaymentRails.DTOs;
using CBS.Domain.Accounts;
using CBS.Domain.BranchCash;
using CBS.Domain.Exceptions;
using CBS.Domain.Lending;
using CBS.Domain.PaymentRails;
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
        builder.Services.AddSingleton<ITellerDrawerRepository, InMemoryTellerDrawerRepository>();
        builder.Services.AddSingleton<IRemittanceInstrumentRepository, InMemoryRemittanceInstrumentRepository>();
        builder.Services.AddSingleton<ISecurityStationeryRepository, InMemorySecurityStationeryRepository>();
        builder.Services.AddSingleton<IChequeClearingRepository, InMemoryChequeClearingRepository>();
        builder.Services.AddSingleton<IBeftnRepository, InMemoryBeftnRepository>();
        builder.Services.AddSingleton<IRtgsRepository, InMemoryRtgsRepository>();
        builder.Services.AddSingleton<ILoanAccountRepository, InMemoryLoanAccountRepository>();
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

        // --- BRANCH CASH & TELLER APIS (PHASE 3) ---
        var tellerApi = app.MapGroup("/api/v1/teller");

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
        var remittanceApi = app.MapGroup("/api/v1/remittance");

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
        var bachApi = app.MapGroup("/api/v1/clearing/bach");

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

        var beftnApi = app.MapGroup("/api/v1/clearing/beftn");

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
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        var rtgsApi = app.MapGroup("/api/v1/clearing/rtgs");

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
        var lendingApi = app.MapGroup("/api/v1/lending");

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
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

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
