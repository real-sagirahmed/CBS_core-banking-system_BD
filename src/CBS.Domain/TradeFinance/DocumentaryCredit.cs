using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.TradeFinance;

/// <summary>
/// Documentary Credit (Letter of Credit - LC) Aggregate Root conforming to Bangladesh Bank BRPD Circular 12 (Module 5: Trade Service)
/// and SWIFT Category 7 MT700 standards.
/// </summary>
public sealed class DocumentaryCredit : AggregateRoot<string>
{
    public string LcNumber => Id;
    public LcType Type { get; private set; }
    public Guid ApplicantId { get; private set; }
    public string BeneficiaryName { get; private set; } = null!;
    public string BeneficiaryAddress { get; private set; } = null!;
    public string BeneficiaryCountry { get; private set; } = null!;
    public string AdvisingBankBic { get; private set; } = null!;
    public string AdvisingBankName { get; private set; } = null!;
    public Money Amount { get; private set; } = null!;
    public Money CashMarginAmount { get; private set; } = null!;
    public decimal TolerancePercentage { get; private set; }
    public DateTime IssueDate { get; private set; }
    public DateTime ExpiryDate { get; private set; }
    public string ExpiryPlace { get; private set; } = null!;
    public DateTime LatestShipmentDate { get; private set; }
    public string PortOfLoading { get; private set; } = null!;
    public string PortOfDischarge { get; private set; } = null!;
    public string GoodsDescription { get; private set; } = null!;
    public HsCode HsCode { get; private set; } = null!;
    public Incoterm Incoterm { get; private set; }
    public string IrcNumber { get; private set; } = null!;
    public bool IsIrcValid { get; private set; }
    public bool IsCibDefaulter { get; private set; }
    public LcStatus Status { get; private set; }
    public SwiftMt700Message? Mt700Message { get; private set; }
    public TbmlScreeningResult? TbmlResult { get; private set; }
    public string? MasterLcNumber { get; private set; }
    public Money? MasterLcAmount { get; private set; }
    public DateTime? MasterLcShipmentDate { get; private set; }
    public DateTime? MasterLcExpiryDate { get; private set; }
    public bool EdfRefinanced { get; private set; }
    public string MakerUserId { get; private set; } = null!;
    public string? CheckerUserId { get; private set; }
    public DateTime? AuthorizedAtUtc { get; private set; }

    private DocumentaryCredit(string lcNumber) : base(lcNumber) { }

    /// <summary>
    /// Creates a draft Documentary Credit (LC) with mandatory regulatory checks.
    /// Invariants:
    /// 1. CIB defaulters are strictly prohibited from opening an LC (BRPD 5.1.1-xlii).
    /// 2. Importers with expired/invalid IRC are blocked (BRPD 5.1.1-xli).
    /// 3. Pre-issuance sanction screening hit blocks drafting (BRPD 5.0, 5.7.4).
    /// 4. Back-to-Back LC ceiling validation (<= 75% of Master LC) and date restrictions (BRPD 5.2.2-i, 5.6.1-v, 5.6.1-vi).
    /// </summary>
    public static DocumentaryCredit CreateDraft(
        string lcNumber,
        LcType type,
        Guid applicantId,
        string beneficiaryName,
        string beneficiaryAddress,
        string beneficiaryCountry,
        string advisingBankBic,
        string advisingBankName,
        Money amount,
        Money cashMarginAmount,
        decimal tolerancePercentage,
        DateTime issueDate,
        DateTime expiryDate,
        string expiryPlace,
        DateTime latestShipmentDate,
        string portOfLoading,
        string portOfDischarge,
        string goodsDescription,
        HsCode hsCode,
        Incoterm incoterm,
        string ircNumber,
        bool isIrcValid,
        bool isCibDefaulter,
        TbmlScreeningResult tbmlResult,
        string makerUserId,
        string? masterLcNumber = null,
        Money? masterLcAmount = null,
        DateTime? masterLcShipmentDate = null,
        DateTime? masterLcExpiryDate = null,
        bool edfRefinanced = false)
    {
        if (string.IsNullOrWhiteSpace(lcNumber))
            throw new ArgumentException("LC Number is required.", nameof(lcNumber));

        if (isCibDefaulter)
            throw new InvalidOperationException("Regulatory Invariant Violation [BRPD 5.1.1-xlii]: System shall not permit opening DC against defaulter borrower.");

        if (!isIrcValid)
            throw new InvalidOperationException("Regulatory Invariant Violation [BRPD 5.1.1-xli]: System shall not permit opening DC without valid and unexpired Import Registration Certificate (IRC).");

        if (tbmlResult.HasSanctionHit)
            throw new InvalidOperationException("Regulatory Invariant Violation [BRPD 5.0, 5.7.4]: Sanction screening hit detected against UN/OFAC/Local blacklist. DC issuance is strictly prohibited.");

        if (cashMarginAmount.Currency != amount.Currency)
            throw new InvalidOperationException("Cash margin currency must match LC currency.");

        if (cashMarginAmount.Amount > amount.Amount)
            throw new InvalidOperationException("Cash margin cannot exceed total LC amount.");

        if (latestShipmentDate > expiryDate)
            throw new InvalidOperationException("Latest shipment date cannot be later than LC expiry date.");

        // Back-to-Back LC Invariants (BRPD 5.2.2-i, 5.6.1-ii, 5.6.1-v, 5.6.1-vi)
        if (type == LcType.BackToBack)
        {
            if (string.IsNullOrWhiteSpace(masterLcNumber) || masterLcAmount is null)
                throw new InvalidOperationException("Regulatory Invariant Violation [BRPD 5.5.1-xi]: Back-to-Back LC must be tagged against a valid Master Export LC.");

            // Standard Bangladesh Bank maximum ceiling is 75% of Master LC value
            var maxAllowedB2BAmount = masterLcAmount.Amount * 0.75m;
            if (amount.Amount > maxAllowedB2BAmount)
                throw new InvalidOperationException($"Regulatory Invariant Violation [BRPD 5.2.2-i, 5.6.1-ii]: Back-to-Back LC value ({amount.Amount} {amount.Currency}) exceeds statutory ceiling of 75% of Master LC value ({maxAllowedB2BAmount} {masterLcAmount.Currency}).");

            if (masterLcShipmentDate.HasValue && latestShipmentDate >= masterLcShipmentDate.Value)
                throw new InvalidOperationException("Regulatory Invariant Violation [BRPD 5.6.1-vi]: Shipment date of Back-to-Back LC must be strictly earlier than shipment date of Master LC.");

            if (masterLcExpiryDate.HasValue && expiryDate >= masterLcExpiryDate.Value)
                throw new InvalidOperationException("Regulatory Invariant Violation [BRPD 5.6.1-v]: Expiry date of Back-to-Back LC must be strictly earlier than expiry date of Master LC.");
        }

        if (string.IsNullOrWhiteSpace(makerUserId))
            throw new ArgumentException("Maker User ID is required.", nameof(makerUserId));

        var lc = new DocumentaryCredit(lcNumber.Trim())
        {
            Type = type,
            ApplicantId = applicantId,
            BeneficiaryName = beneficiaryName.Trim(),
            BeneficiaryAddress = beneficiaryAddress.Trim(),
            BeneficiaryCountry = beneficiaryCountry.Trim(),
            AdvisingBankBic = advisingBankBic.Trim().ToUpperInvariant(),
            AdvisingBankName = advisingBankName.Trim(),
            Amount = amount,
            CashMarginAmount = cashMarginAmount,
            TolerancePercentage = tolerancePercentage,
            IssueDate = issueDate,
            ExpiryDate = expiryDate,
            ExpiryPlace = expiryPlace.Trim(),
            LatestShipmentDate = latestShipmentDate,
            PortOfLoading = portOfLoading.Trim(),
            PortOfDischarge = portOfDischarge.Trim(),
            GoodsDescription = goodsDescription.Trim(),
            HsCode = hsCode,
            Incoterm = incoterm,
            IrcNumber = ircNumber.Trim(),
            IsIrcValid = isIrcValid,
            IsCibDefaulter = isCibDefaulter,
            Status = LcStatus.Draft,
            TbmlResult = tbmlResult,
            MasterLcNumber = masterLcNumber?.Trim(),
            MasterLcAmount = masterLcAmount,
            MasterLcShipmentDate = masterLcShipmentDate,
            MasterLcExpiryDate = masterLcExpiryDate,
            EdfRefinanced = edfRefinanced,
            MakerUserId = makerUserId.Trim()
        };

        return lc;
    }

    /// <summary>
    /// Dual-control authorization by an independent checker officer. Generates SWIFT MT700 message and issues the LC.
    /// Invariant: MakerUserId != CheckerUserId (BRPD 1.3.18, 5.1.8-i, 10.3.5-x).
    /// </summary>
    public void AuthorizeAndIssue(string checkerUserId, string issuingBankBic = "S7BDBD2X")
    {
        if (Status != LcStatus.Draft && Status != LcStatus.SanctionChecked)
            throw new InvalidOperationException($"Cannot authorize LC in status '{Status}'. Only Draft or SanctionChecked LCs can be authorized.");

        if (string.IsNullOrWhiteSpace(checkerUserId))
            throw new ArgumentException("Checker User ID is required.", nameof(checkerUserId));

        if (string.Equals(MakerUserId, checkerUserId.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Regulatory Invariant Violation [BRPD 1.3.18, 10.3.5-x]: Dual-control separation of duty enforced. The maker cannot authorize their own LC.");

        CheckerUserId = checkerUserId.Trim();
        AuthorizedAtUtc = DateTime.UtcNow;

        // Generate SWIFT MT700 Message (BRPD 5.1.8-i)
        Mt700Message = SwiftMt700Message.Generate(
            senderBic: issuingBankBic,
            receiverBic: AdvisingBankBic,
            lcNumber: LcNumber,
            issueDate: IssueDate,
            expiryDate: ExpiryDate,
            expiryPlace: ExpiryPlace,
            applicantInfo: $"APPLICANT CIF {ApplicantId}",
            beneficiaryInfo: $"{BeneficiaryName}, {BeneficiaryAddress}, {BeneficiaryCountry}",
            currency: Amount.Currency.Code,
            amount: Amount.Amount,
            availableWith: AdvisingBankBic,
            partialShipmentAllowed: true,
            transshipmentAllowed: false,
            portOfLoading: PortOfLoading,
            portOfDischarge: PortOfDischarge,
            latestShipmentDate: LatestShipmentDate,
            goodsDescription: $"{GoodsDescription} (HS CODE: {HsCode.Code})",
            documentsRequired: "SIGNED COMMERCIAL INVOICE IN 3 COPIES, FULL SET CLEAN ON BOARD BILLS OF LADING, PACKING LIST",
            additionalConditions: HsCode.RequiresPreShipmentInspection ? "PRE-SHIPMENT INSPECTION (PSI) CERTIFICATE REQUIRED AS PER IPO" : "STANDARD COMMERCIAL CONDITIONS",
            confirmationInstruction: "WITHOUT");

        Status = LcStatus.Issued;
    }

    /// <summary>
    /// Adjusts status when trade documents arrive.
    /// </summary>
    public void LodgeBill()
    {
        if (Status != LcStatus.Issued)
            throw new InvalidOperationException($"Cannot lodge bill against LC in status '{Status}'. Must be Issued.");

        Status = LcStatus.BillLodged;
    }

    /// <summary>
    /// Retires the LC upon bill payment / settlement.
    /// </summary>
    public void Retire()
    {
        if (Status != LcStatus.BillLodged)
            throw new InvalidOperationException($"Cannot retire LC in status '{Status}'. Must be BillLodged.");

        Status = LcStatus.Retired;
    }
}
