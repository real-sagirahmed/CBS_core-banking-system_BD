namespace CBS.Domain.ValueObjects;

// ARCHITECTURAL INTENT: Statutory Recovery Waterfall Breakdown (BRPD Circular 12/2024 Module 4.2.11-iii).
// Order: Charges -> Penal Interest -> Overdue Interest -> Regular Interest -> Principal.
public sealed record RepaymentBreakdown(
    Money AllocatedCharges,
    Money AllocatedPenalInterest,
    Money AllocatedOverdueInterest,
    Money AllocatedRegularInterest,
    Money AllocatedPrincipal,
    Money TotalPaid,
    Money UnallocatedSurplus);
