using CBS.Application.Common.Interfaces;
using CBS.Application.TradeFinance.DTOs;
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

    public TradeFinanceCommandHandler(ITradeFinanceRepository repository)
    {
        _repository = repository;
    }

    public async Task<DocumentaryCreditResponse> HandleDraftLcAsync(DraftDocumentaryCreditCommand command, CancellationToken ct = default)
    {
        var req = command.Request;

        // Perform TBML and Sanction Screening
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
            masterLcNumber: req.MasterLcNumber,
            masterLcAmount: masterAmount,
            masterLcShipmentDate: req.MasterLcShipmentDate,
            masterLcExpiryDate: req.MasterLcExpiryDate,
            edfRefinanced: req.EdfRefinanced);

        await _repository.AddLcAsync(lc, ct);

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

    public async Task<DocumentaryCreditResponse> HandleAuthorizeLcAsync(AuthorizeDocumentaryCreditCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var lc = await _repository.GetLcByNumberAsync(req.LcNumber, ct)
            ?? throw new KeyNotFoundException($"Documentary Credit '{req.LcNumber}' not found.");

        lc.AuthorizeAndIssue(req.CheckerUserId, req.IssuingBankBic);
        await _repository.UpdateLcAsync(lc, ct);

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
            req.MakerUserId);

        await _repository.AddSgAsync(sg, ct);

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

    public async Task<ShippingGuaranteeResponse> HandleAuthorizeSgAsync(AuthorizeShippingGuaranteeCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var sg = await _repository.GetSgByReferenceAsync(req.SgReference, ct)
            ?? throw new KeyNotFoundException($"Shipping Guarantee '{req.SgReference}' not found.");

        sg.Authorize(req.CheckerUserId);
        await _repository.UpdateSgAsync(sg, ct);

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

    public async Task<ShippingGuaranteeResponse> HandleReleaseSgAsync(ReleaseShippingGuaranteeCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var sg = await _repository.GetSgByReferenceAsync(req.SgReference, ct)
            ?? throw new KeyNotFoundException($"Shipping Guarantee '{req.SgReference}' not found.");

        sg.ReleaseUponOriginalBlSurrender(req.OriginalBlNumber, req.ReleasedByUserId);
        await _repository.UpdateSgAsync(sg, ct);

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
            req.MakerUserId);

        await _repository.AddExpAsync(exp, ct);

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

    public async Task<ExpRegulatoryResponse> HandleRealizeExpAsync(RealizeExpProceedsCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var exp = await _repository.GetExpByNumberAsync(req.ExpNumber, ct)
            ?? throw new KeyNotFoundException($"EXP Form '{req.ExpNumber}' not found.");

        var currency = Currency.Create(req.Currency, req.Currency, req.Currency);
        var amount = new Money(req.ReceivedAmount, currency);

        exp.RecordRealization(amount);
        await _repository.UpdateExpAsync(exp, ct);

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

    public async Task<ExpRegulatoryResponse> HandleEvaluateExpOverdueAsync(EvaluateExpOverdueCommand command, CancellationToken ct = default)
    {
        var req = command.Request;
        var exp = await _repository.GetExpByNumberAsync(req.ExpNumber, ct)
            ?? throw new KeyNotFoundException($"EXP Form '{req.ExpNumber}' not found.");

        exp.EvaluateOverdueStatus(req.EvaluationDate);
        await _repository.UpdateExpAsync(exp, ct);

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
}
