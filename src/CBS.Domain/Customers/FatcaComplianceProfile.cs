// ARCHITECTURAL INTENT: FATCA Compliance Profile Aggregate Root
// Enforcing automated 7 US Indicia detection, Form W-9 / W-8BEN document validation,
// and Recalcitrant Account Holder 30% withholding tax indicator per Bangladesh Bank
// BRPD Circular No. 12 (13 May 2024) Module 8.1.15 and US Internal Revenue Code Chapter 4 (FATCA).

using System.Text.RegularExpressions;
using CBS.Domain.Common;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Customers;

public sealed partial class FatcaComplianceProfile : AggregateRoot<Guid>
{
    public CifNumber Cif { get; private set; } = null!;
    public bool HasUsCitizenshipOrGreenCard { get; private set; }
    public bool HasUsPlaceOfBirth { get; private set; }
    public bool HasUsAddress { get; private set; }
    public bool HasUsPhoneNumber { get; private set; }
    public bool HasStandingInstructionsToUsAccount { get; private set; }
    public bool HasPowerOfAttorneyGrantedToUsAddress { get; private set; }
    public bool HasInCareOfOrHoldMailAddress { get; private set; }

    public bool HasAnyUsIndicia =>
        HasUsCitizenshipOrGreenCard ||
        HasUsPlaceOfBirth ||
        HasUsAddress ||
        HasUsPhoneNumber ||
        HasStandingInstructionsToUsAccount ||
        HasPowerOfAttorneyGrantedToUsAddress ||
        HasInCareOfOrHoldMailAddress;

    public FatcaClassification Classification { get; private set; }
    public string? UsTaxpayerIdentificationNumber { get; private set; }
    public bool IsFormW9Submitted { get; private set; }
    public DateOnly? FormW9SubmissionDate { get; private set; }
    public bool IsFormW8BenSubmitted { get; private set; }
    public DateOnly? FormW8BenSubmissionDate { get; private set; }
    public string? GlobalIntermediaryIdentificationNumber { get; private set; }
    public bool IsSubjectToWithholdingTax { get; private set; }
    public DateOnly? IndiciaDetectedDate { get; private set; }
    public DateOnly? CureDeadlineDate { get; private set; }
    public string EvaluatedByUserId { get; private set; } = null!;
    public DateTime LastEvaluatedAtUtc { get; private set; }

    [GeneratedRegex(@"^\d{3}-?\d{2}-?\d{4}$|^\d{2}-?\d{7}$")]
    private static partial Regex UsTinPattern();

    #pragma warning disable CS8618
    private FatcaComplianceProfile() : base(Guid.Empty) { }
    #pragma warning restore CS8618

    public FatcaComplianceProfile(
        Guid id,
        CifNumber cif,
        bool hasUsCitizenshipOrGreenCard,
        bool hasUsPlaceOfBirth,
        bool hasUsAddress,
        bool hasUsPhoneNumber,
        bool hasStandingInstructionsToUsAccount,
        bool hasPowerOfAttorneyGrantedToUsAddress,
        bool hasInCareOfOrHoldMailAddress,
        DateOnly evaluationDate,
        string evaluatedByUserId,
        string? giin = null)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(cif, nameof(cif));
        if (string.IsNullOrWhiteSpace(evaluatedByUserId))
            throw new ArgumentException("Evaluated by user ID is required.", nameof(evaluatedByUserId));

        Cif = cif;
        HasUsCitizenshipOrGreenCard = hasUsCitizenshipOrGreenCard;
        HasUsPlaceOfBirth = hasUsPlaceOfBirth;
        HasUsAddress = hasUsAddress;
        HasUsPhoneNumber = hasUsPhoneNumber;
        HasStandingInstructionsToUsAccount = hasStandingInstructionsToUsAccount;
        HasPowerOfAttorneyGrantedToUsAddress = hasPowerOfAttorneyGrantedToUsAddress;
        HasInCareOfOrHoldMailAddress = hasInCareOfOrHoldMailAddress;
        GlobalIntermediaryIdentificationNumber = giin?.Trim().ToUpperInvariant();
        EvaluatedByUserId = evaluatedByUserId.Trim();
        LastEvaluatedAtUtc = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(GlobalIntermediaryIdentificationNumber))
        {
            Classification = FatcaClassification.ExemptEntity;
            IsSubjectToWithholdingTax = false;
        }
        else if (HasAnyUsIndicia)
        {
            Classification = FatcaClassification.NonUsPerson; // provisional until cured or confirmed
            IndiciaDetectedDate = evaluationDate;
            CureDeadlineDate = evaluationDate.AddDays(90); // 90 days statutory cure window
            IsSubjectToWithholdingTax = false;
        }
        else
        {
            Classification = FatcaClassification.NonUsPerson;
            IsSubjectToWithholdingTax = false;
        }
    }

    public void SubmitFormW9(string usTin, DateOnly submissionDate, string verifiedByUserId)
    {
        if (string.IsNullOrWhiteSpace(usTin))
            throw new ArgumentException("US TIN/SSN is required for Form W-9.", nameof(usTin));
        if (string.IsNullOrWhiteSpace(verifiedByUserId))
            throw new ArgumentException("Verified by user ID is required.", nameof(verifiedByUserId));

        var cleanTin = usTin.Trim().Replace("-", "");
        if (cleanTin.Length != 9 || !cleanTin.All(char.IsDigit))
        {
            throw new InvariantViolationException(
                "InvalidUsTinInvariant",
                "US IRC §6109 / FATCA",
                $"US Taxpayer Identification Number must be a valid 9-digit number ('{usTin}').");
        }

        UsTaxpayerIdentificationNumber = usTin.Trim();
        IsFormW9Submitted = true;
        FormW9SubmissionDate = submissionDate;
        Classification = FatcaClassification.UsPersonCompliant;
        IsSubjectToWithholdingTax = false;
        EvaluatedByUserId = verifiedByUserId.Trim();
        LastEvaluatedAtUtc = DateTime.UtcNow;
    }

    public void SubmitFormW8Ben(DateOnly submissionDate, string verifiedByUserId)
    {
        if (string.IsNullOrWhiteSpace(verifiedByUserId))
            throw new ArgumentException("Verified by user ID is required.", nameof(verifiedByUserId));

        IsFormW8BenSubmitted = true;
        FormW8BenSubmissionDate = submissionDate;
        Classification = FatcaClassification.NonUsPerson;
        IsSubjectToWithholdingTax = false;
        EvaluatedByUserId = verifiedByUserId.Trim();
        LastEvaluatedAtUtc = DateTime.UtcNow;
    }

    public void CheckAndEnforceRecalcitrantStatus(DateOnly asOfDate)
    {
        if (Classification == FatcaClassification.UsPersonCompliant ||
            Classification == FatcaClassification.ExemptEntity)
        {
            return;
        }

        // If US Indicia detected and cure deadline passed without W-9 or W-8BEN, account holder is Recalcitrant
        if (HasAnyUsIndicia && !IsFormW9Submitted && !IsFormW8BenSubmitted)
        {
            if (CureDeadlineDate.HasValue && asOfDate > CureDeadlineDate.Value)
            {
                Classification = FatcaClassification.RecalcitrantAccountHolder;
                IsSubjectToWithholdingTax = true; // Subject to 30% IRS Withholding Tax Indicator
                LastEvaluatedAtUtc = DateTime.UtcNow;
            }
        }
    }
}
