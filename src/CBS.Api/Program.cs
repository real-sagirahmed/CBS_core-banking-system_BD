using CBS.Application.Common.Interfaces;
using CBS.Application.Customers.Commands.CreateCustomerDraft;
using CBS.Application.Customers.Commands.VerifyCustomer;
using CBS.Application.Customers.DTOs;
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

        // Register Core Infrastructure & Application Services
        builder.Services.AddSingleton<ICustomerRepository, InMemoryCustomerRepository>();
        builder.Services.AddSingleton<IElectionCommissionGateway, MockElectionCommissionGateway>();
        builder.Services.AddSingleton<ISanctionScreeningService, MockSanctionScreeningService>();

        builder.Services.AddTransient<CreateCustomerDraftCommandHandler>();
        builder.Services.AddTransient<VerifyCustomerCommandHandler>();

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

        // API Group for Customer Onboarding & Identity
        var customerApi = app.MapGroup("/api/v1/customers");

        // 1. Maker: Create Customer Draft
        customerApi.MapPost("/draft", async (
            CreateCustomerDraftRequest request,
            HttpContext httpContext,
            CreateCustomerDraftCommandHandler handler,
            CancellationToken ct) =>
        {
            string makerUserId = httpContext.Request.Headers["X-User-Id"].FirstOrDefault() ?? "MAKER01";
            string branchCode = httpContext.Request.Headers["X-Branch-Code"].FirstOrDefault() ?? "BR001";

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

        // 2. Checker: Verify & Authorize Customer
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

        // 3. Query: Get Customer by CIF
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

        // 4. EC Gateway Verification
        customerApi.MapPost("/ec-verify", async (
            EcVerificationRequest req,
            IElectionCommissionGateway gateway,
            CancellationToken ct) =>
        {
            var result = await gateway.VerifyAsync(req, ct);
            return Results.Ok(result);
        });

        return app;
    }
}

public record VerifyCustomerRequest(bool IsApproved, string? RejectionReason = null);
