using CBS.Application.Common.Interfaces;
using CBS.Application.GeneralLedger.Services;
using CBS.Application.TradeFinance.DTOs;
using CBS.Domain.Accounts;
using CBS.Domain.Enums;
using CBS.Domain.TradeFinance;
using CBS.Domain.ValueObjects;

namespace CBS.Application.TradeFinance.Commands;

public sealed record DraftDocumentaryCreditCommand(DraftDocumentaryCreditRequest Request);
public sealed record AuthorizeDocumentaryCreditCommand(AuthorizeDocumentaryCreditRequest Request);
public sealed record IssueShippingGuaranteeCommand(IssueShippingGuaranteeRequest Request);
public sealed record AuthorizeShippingGuaranteeCommand(AuthorizeShippingGuaranteeRequest Request);
public sealed record ReleaseShippingGuaranteeCommand(ReleaseShippingGuaranteeRequest Request);
public sealed record RegisterExpTrackingCommand(RegisterExpRequest Request);
public sealed record RealizeExpProceedsCommand(RealizeExpRequest Request);
public sealed record EvaluateExpOverdueCommand(EvaluateExpOverdueRequest Request);
public sealed record ScreenTbmlCommand(TbmlScreeningRequest Request);

public class TradeFinanceCommandHandler
{
    private readonly ITradeFinanceRepository _repository;
    private readonly IGeneralLedgerService _glService;
    private readonly IAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TradeFinanceCommandHandler(
        ITradeFinanceRepository repository,
        IGeneralLedgerService glService,
        IAccountRepository accountRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _glService = glService ?? throw new ArgumentNullException(nameof(glService));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<DocumentaryCreditResponse> HandleDraftLcAsync(DraftDocumentaryCreditCommand command, CancellationToken ct = default)
    {
        var req = command.Request;

        // 1. Perform TBML and Sanction Screening (BRPD 5.0, 5.7.4, 5.7.6, 5.7.7)
        var tbmlResult = TbmlScreeningResult.Evaluate(
            entitySanctionHit: req.IsSanctionHit,
            declaredUnitPrice: req.DeclaredUnitPrice,
            benchmarkMinPrice: req.BenchmarkMinPrice,
            benchmarkMaxPrice: req.BenchmarkMaxPrice,
            portOfLoading: req.PortOfLoading,
            portOfDischarge: req.PortOfDischarge,
            isDualUseHsCode: req.IsDualUseHsCode);

        var currency = Currency.Create(req.Currency, req.Currency, req.Currency);
        var hsCode = HsCode.Create(req.HsCode);
        var amount = new Money(req.Amount, currency);
        var cashMargin = new Money(req.CashMarginAmount, currency);

        Money? masterAmount = req.MasterLcAmount.HasValue
            ? new Money(req.MasterLcAmount.Value, currency)
            : null;

        var lc = DocumentaryCredit.CreateDraft(
            lcNumber: req.LcNumber,
            type: req.Type,
            applicantId: req.ApplicantId,
            beneficiaryName: req.BeneficiaryName,
            beneficiaryAddress: req.BeneficiaryAddress,
            beneficiaryCountry: req.BeneficiaryCountry,
            advisingBankBic: req.AdvisingBankBic,
            advisingBankName: req.AdvisingBankName,
            amount: amount,
            cashMarginAmount: cashMargin,
            tolerancePercentage: req.TolerancePercentage,
            issueDate: req.IssueDate,
            expiryDate: req.ExpiryDate,
            expiryPlace: req.ExpiryPlace,
            latestShipmentDate: req.LatestShipmentDate,
            portOfLoading: req.PortOfLoading,
            portOfDischarge: req.PortOfDischarge,
            goodsDescription: req.GoodsDescription,
            hsCode: hsCode,
            incoterm: req.Incoterm,
            ircNumber: req.IrcNumber,
            isIrcValid: req.IsIrcValid,
            isCibDefaulter: req.IsCibDefaulter,
            tbmlResult: tbmlResult,
            makerUserId: req.MakerUserId,
            branchCode: req.BranchCode,
            customerDepositAccountNumber: req.CustomerDepositAccountNumber,
            masterLcNumber: req.MasterLcNumber,
            masterLcAmount: masterAmount,
            masterLcShipmentDate: req.MasterLcShipmentDate,
            masterLcExpiryDate: req.MasterLcExpiryDate,
            edfRefinanced: req.EdfRefinanced);

        await _repository.AddLcAsync(lc, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(lc);
    }

    public async Task<DocumentaryCreditResponse> HandleAuthorizeLcAsync(AuthorizeDocumentaryCreditCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var lc = await _repository.GetLcByNumberForUpdateAsync(req.LcNumber, ct)
            ?? throw new KeyNotFoundException($"Documentary Credit '{req.LcNumber}' not found.");

        lc.AuthorizeAndIssue(req.CheckerUserId, req.IssuingBankBic);

        // Deduct Cash Margin from customer CASA deposit account if specified (BRPD 5.1.1-xiii)
        if (lc.CashMarginAmount.Amount > 0 && !string.IsNullOrWhiteSpace(lc.CustomerDepositAccountNumber))
        {
            var accNo = AccountNumber.From(lc.CustomerDepositAccountNumber);
            var account = await _accountRepository.GetByAccountNumberForUpdateAsync(accNo, ct);
            if (account != null)
            {
                var marginDebit = new Money(lc.CashMarginAmount.Amount, account.Balance.Currency);
                account.Debit(marginDebit, $"Cash Margin Hold for LC {lc.LcNumber}");
                await _accountRepository.UpdateAsync(account, ct);
            }
        }

        // Post Off-Balance Sheet Contingent Liability & Margin Hold Voucher to General Ledger (BRPD 5.1.1-xi, 5.1.8-i)
        await _glService.PostDocumentaryCreditIssuanceAsync(
            branchSol: lc.BranchCode,
            lcNumber: lc.LcNumber,
            lcAmount: lc.Amount.Amount,
            cashMarginAmount: lc.CashMarginAmount.Amount,
            customerAccountNumber: lc.CustomerDepositAccountNumber,
            makerUserId: req.CheckerUserId,
            checkerUserId: "SYSTEM_GL_AUTOPOST",
            ct: ct);

        await _repository.UpdateLcAsync(lc, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(lc);
    }

    public async Task<ShippingGuaranteeResponse> HandleIssueSgAsync(IssueShippingGuaranteeCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var currency = Currency.Create(req.Currency, req.Currency, req.Currency);
        var guaranteeAmount = new Money(req.GuaranteeAmount, currency);
        var marginAmount = new Money(req.CashMarginAmount, currency);

        var sg = ShippingGuarantee.Issue(
            req.SgReference,
            req.LcNumber,
            req.ApplicantId,
            req.CarrierName,
            req.VesselName,
            req.PortOfDischarge,
            guaranteeAmount,
            marginAmount,
            req.MakerUserId,
            req.BranchCode,
            req.CustomerDepositAccountNumber);

        await _repository.AddSgAsync(sg, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToSgDto(sg);
    }

    public async Task<ShippingGuaranteeResponse> HandleAuthorizeSgAsync(AuthorizeShippingGuaranteeCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var sg = await _repository.GetSgByReferenceForUpdateAsync(req.SgReference, ct)
            ?? throw new KeyNotFoundException($"Shipping Guarantee '{req.SgReference}' not found.");

        sg.Authorize(req.CheckerUserId);

        // Deduct 100% Cash Margin from customer account (BRPD 5.1.2-iii)
        if (!string.IsNullOrWhiteSpace(sg.CustomerDepositAccountNumber))
        {
            var accNo = AccountNumber.From(sg.CustomerDepositAccountNumber);
            var account = await _accountRepository.GetByAccountNumberForUpdateAsync(accNo, ct);
            if (account != null)
            {
                var marginDebit = new Money(sg.CashMarginAmount.Amount, account.Balance.Currency);
                account.Debit(marginDebit, $"100% Cash Margin Lock for SG {sg.SgReference}");
                await _accountRepository.UpdateAsync(account, ct);
            }
        }

        // Post 100% Cash Margin Hold to General Ledger
        await _glService.PostShippingGuaranteeIssuanceAsync(
            branchSol: sg.BranchCode,
            sgReference: sg.SgReference,
            guaranteeAmount: sg.GuaranteeAmount.Amount,
            cashMarginAmount: sg.CashMarginAmount.Amount,
            customerAccountNumber: sg.CustomerDepositAccountNumber ?? "201010001",
            makerUserId: req.CheckerUserId,
            checkerUserId: "SYSTEM_GL_AUTOPOST",
            ct: ct);

        await _repository.UpdateSgAsync(sg, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToSgDto(sg);
    }

    public async Task<ShippingGuaranteeResponse> HandleReleaseSgAsync(ReleaseShippingGuaranteeCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var sg = await _repository.GetSgByReferenceForUpdateAsync(req.SgReference, ct)
            ?? throw new KeyNotFoundException($"Shipping Guarantee '{req.SgReference}' not found.");

        sg.ReleaseUponOriginalBlSurrender(req.OriginalBlNumber, req.ReleasedByUserId);
        await _repository.UpdateSgAsync(sg, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToSgDto(sg);
    }

    public async Task<ExpRegulatoryResponse> HandleRegisterExpAsync(RegisterExpTrackingCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var currency = Currency.Create(req.Currency, req.Currency, req.Currency);
        var amount = new Money(req.InvoiceAmount, currency);

        var exp = ExpRegulatoryTracking.Register(
            req.ExpNumber,
            req.ExportLcOrContractNumber,
            req.ExporterCustomerId,
            req.DestinationCountry,
            amount,
            req.ShipmentDate,
            req.MakerUserId,
            req.BranchCode);

        await _repository.AddExpAsync(exp, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToExpDto(exp);
    }

    public async Task<ExpRegulatoryResponse> HandleRealizeExpAsync(RealizeExpProceedsCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var exp = await _repository.GetExpByNumberForUpdateAsync(req.ExpNumber, ct)
            ?? throw new KeyNotFoundException($"EXP Form '{req.ExpNumber}' not found.");

        var currency = Currency.Create(req.Currency, req.Currency, req.Currency);
        var amount = new Money(req.ReceivedAmount, currency);

        exp.RecordRealization(amount);
        await _repository.UpdateExpAsync(exp, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToExpDto(exp);
    }

    public async Task<ExpRegulatoryResponse> HandleEvaluateExpOverdueAsync(EvaluateExpOverdueCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var exp = await _repository.GetExpByNumberForUpdateAsync(req.ExpNumber, ct)
            ?? throw new KeyNotFoundException($"EXP Form '{req.ExpNumber}' not found.");

        exp.EvaluateOverdueStatus(req.EvaluationDate);
        await _repository.UpdateExpAsync(exp, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToExpDto(exp);
    }

    public Task<TbmlScreeningResponse> HandleScreenTbmlAsync(ScreenTbmlCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var result = TbmlScreeningResult.Evaluate(
            entitySanctionHit: req.EntitySanctionHit,
            declaredUnitPrice: req.DeclaredUnitPrice,
            benchmarkMinPrice: req.BenchmarkMinPrice,
            benchmarkMaxPrice: req.BenchmarkMaxPrice,
            portOfLoading: req.PortOfLoading,
            portOfDischarge: req.PortOfDischarge,
            isDualUseHsCode: req.IsDualUseHsCode);

        var response = new TbmlScreeningResponse(
            result.HasSanctionHit,
            result.HasPriceVarianceAnomaly,
            result.HasHighRiskPortRoute,
            result.HasDualUseGoodsRisk,
            result.UnitPriceVariancePercentage,
            result.RiskRating.ToString(),
            result.RiskSummary);

        return Task.FromResult(response);
    }

    public async Task<ExpEodBatchResponseDto> HandleExpEodBatchAsync(string branchCode, string? evaluationDateStr = null, CancellationToken ct = default)
    {
        var allExps = await _repository.GetAllExpsAsync(ct);
        var targetExps = allExps
            .Where(e => string.IsNullOrWhiteSpace(branchCode) || e.BranchCode.Equals(branchCode, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var evalDate = string.IsNullOrWhiteSpace(evaluationDateStr)
            ? DateTime.UtcNow
            : DateTime.Parse(evaluationDateStr);

        int totalProcessed = targetExps.Count;
        int overdueCount = 0;
        decimal totalOverdueAmount = 0m;

        foreach (var exp in targetExps)
        {
            if (exp.Status != ExpTrackingStatus.Realized)
            {
                bool isOverdue = exp.EvaluateOverdueStatus(evalDate);
                if (isOverdue || exp.Status == ExpTrackingStatus.OverdueReportedToBangladeshBank)
                {
                    overdueCount++;
                    decimal unadjusted = exp.InvoiceAmount.Amount - exp.RealizedAmount.Amount;
                    totalOverdueAmount += unadjusted;
                    await _repository.UpdateExpAsync(exp, ct);
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return new ExpEodBatchResponseDto(
            branchCode,
            totalProcessed,
            overdueCount,
            totalOverdueAmount,
            $"EXP EOD Batch completed successfully for Branch '{branchCode}'. Total: {totalProcessed}, Overdue: {overdueCount}, Overdue Realization: {totalOverdueAmount:F2} BDT.");
    }

    public async Task<IReadOnlyList<OverdueExpReportDto>> GetOverdueExpReportAsync(string? branchCode = null, CancellationToken ct = default)
    {
        var allExps = await _repository.GetAllExpsAsync(ct);
        var query = allExps.Where(e => e.Status == ExpTrackingStatus.OverdueReportedToBangladeshBank);

        if (!string.IsNullOrWhiteSpace(branchCode))
        {
            query = query.Where(e => e.BranchCode.Equals(branchCode, StringComparison.OrdinalIgnoreCase));
        }

        var today = DateTime.UtcNow.Date;

        return query.Select(e => new OverdueExpReportDto(
            e.ExpNumber,
            e.ExportLcOrContractNumber,
            e.ExporterCustomerId,
            e.DestinationCountry,
            e.InvoiceAmount.Amount,
            e.RealizedAmount.Amount,
            e.InvoiceAmount.Currency.Code,
            e.ShipmentDate,
            e.RealizationDeadlineDate,
            Math.Max(0, (int)(today - e.RealizationDeadlineDate.Date).TotalDays),
            e.BranchCode)).ToList();
    }

    public async Task<IReadOnlyList<TbmlAuditRecordDto>> GetTbmlAuditReportAsync(string? branchCode = null, CancellationToken ct = default)
    {
        var allLcs = await _repository.GetAllLcsAsync(ct);
        var query = allLcs.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(branchCode))
        {
            query = query.Where(l => l.BranchCode.Equals(branchCode, StringComparison.OrdinalIgnoreCase));
        }

        return query.Where(l => l.TbmlResult != null).Select(l => new TbmlAuditRecordDto(
            l.LcNumber,
            $"APPLICANT CIF {l.ApplicantId}",
            $"{l.BeneficiaryName}, {l.BeneficiaryCountry}",
            0m,
            0m,
            0m,
            l.TbmlResult!.UnitPriceVariancePercentage,
            l.TbmlResult.RiskRating.ToString(),
            l.TbmlResult.RiskSummary,
            l.IssueDate)).ToList();
    }

    public static DocumentaryCreditResponse MapToDto(DocumentaryCredit lc)
    {
        return new DocumentaryCreditResponse(
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
    }

    public static ShippingGuaranteeResponse MapToSgDto(ShippingGuarantee sg)
    {
        return new ShippingGuaranteeResponse(
            sg.SgReference,
            sg.LcNumber,
            sg.ApplicantId,
            sg.CarrierName,
            sg.VesselName,
            sg.GuaranteeAmount.Amount,
            sg.GuaranteeAmount.Currency.Code,
            sg.CashMarginAmount.Amount,
            sg.Status.ToString(),
            sg.CheckerUserId,
            sg.SurrenderedOriginalBlNumber);
    }

    public static ExpRegulatoryResponse MapToExpDto(ExpRegulatoryTracking exp)
    {
        return new ExpRegulatoryResponse(
            exp.ExpNumber,
            exp.ExportLcOrContractNumber,
            exp.ExporterCustomerId,
            exp.DestinationCountry,
            exp.InvoiceAmount.Amount,
            exp.RealizedAmount.Amount,
            exp.InvoiceAmount.Currency.Code,
            exp.ShipmentDate,
            exp.RealizationDeadlineDate,
            exp.Status.ToString(),
            exp.ReportedToBangladeshBank);
    }
}
