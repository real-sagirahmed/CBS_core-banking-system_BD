// ARCHITECTURAL INTENT: Continuous Loan Godown Inspection Entity
// Records physical verification of hypothecated/pledged godown stock and enforces mandatory insurance coverage
// adequacy checks per Bangladesh Bank BRPD Circular No. 12 (4.1.3-iv).

using System;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Domain.Lending.Continuous;

public class ContinuousLoanGodownInspection
{
    public string InspectionId { get; private set; }
    public string LoanAccountNumber { get; private set; }
    public DateOnly InspectionDate { get; private set; }
    public string InspectorUserId { get; private set; }

    public string GodownLocation { get; private set; }
    public Money InspectedStockValue { get; private set; }
    public string StockConditionDescription { get; private set; }

    public string InsurancePolicyNumber { get; private set; }
    public string InsuranceCompany { get; private set; }
    public DateOnly InsuranceExpiryDate { get; private set; }
    public Money InsuranceSumInsured { get; private set; }

    public bool IsInsuranceValid { get; private set; }
    public bool IsInsuranceAdequate { get; private set; }
    public Money InsurableCappedStockValue { get; private set; }

    public StockInspectionResult InspectionResult { get; private set; }
    public string Remarks { get; private set; }

#pragma warning disable CS8618
    private ContinuousLoanGodownInspection() { }
#pragma warning restore CS8618

    public ContinuousLoanGodownInspection(
        string inspectionId,
        string loanAccountNumber,
        DateOnly inspectionDate,
        string inspectorUserId,
        string godownLocation,
        Money inspectedStockValue,
        string stockConditionDescription,
        string insurancePolicyNumber,
        string insuranceCompany,
        DateOnly insuranceExpiryDate,
        Money insuranceSumInsured,
        StockInspectionResult inspectionResult,
        string remarks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inspectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(loanAccountNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(inspectorUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(godownLocation);
        ArgumentNullException.ThrowIfNull(inspectedStockValue);
        ArgumentException.ThrowIfNullOrWhiteSpace(insurancePolicyNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(insuranceCompany);
        ArgumentNullException.ThrowIfNull(insuranceSumInsured);

        InspectionId = inspectionId.Trim().ToUpperInvariant();
        LoanAccountNumber = loanAccountNumber.Trim();
        InspectionDate = inspectionDate;
        InspectorUserId = inspectorUserId.Trim().ToUpperInvariant();
        GodownLocation = godownLocation.Trim();
        InspectedStockValue = inspectedStockValue;
        StockConditionDescription = stockConditionDescription?.Trim() ?? "Satisfactory";
        InsurancePolicyNumber = insurancePolicyNumber.Trim().ToUpperInvariant();
        InsuranceCompany = insuranceCompany.Trim();
        InsuranceExpiryDate = insuranceExpiryDate;
        InsuranceSumInsured = insuranceSumInsured;

        // BRPD 4.1.3-iv: Valid insurance must not be expired on inspection date
        IsInsuranceValid = insuranceExpiryDate >= inspectionDate && insuranceSumInsured.Amount > 0m;
        IsInsuranceAdequate = IsInsuranceValid && insuranceSumInsured >= inspectedStockValue;

        // If insurance is invalid/expired, insurable stock is 0; otherwise capped at sum insured
        if (!IsInsuranceValid)
        {
            InsurableCappedStockValue = Money.Zero(Currency.BDT);
        }
        else
        {
            InsurableCappedStockValue = Money.Bdt(Math.Min(inspectedStockValue.Amount, insuranceSumInsured.Amount));
        }

        InspectionResult = inspectionResult;
        Remarks = remarks?.Trim() ?? string.Empty;
    }
}
