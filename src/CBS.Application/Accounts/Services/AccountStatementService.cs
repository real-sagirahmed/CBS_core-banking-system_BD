using System.Text.Json;
using CBS.Application.Accounts.DTOs;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Accounts;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Accounts.Services;

// ARCHITECTURAL INTENT: Comprehensive Customer Account Statement Generation,
// Balance Confirmation Certification, and NBR Section 102/53F Annual Withholding Tax Engine
// strictly adhering to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Modules 3.1.17, 3.1.19, and 3.1.23.
public class AccountStatementService : IAccountStatementService
{
    private readonly IAccountRepository _accountRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IAccountLedgerRepository _ledgerRepository;
    private readonly IStatutoryCertificateRepository _certificateRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AccountStatementService(
        IAccountRepository accountRepository,
        ICustomerRepository customerRepository,
        IAccountLedgerRepository ledgerRepository,
        IStatutoryCertificateRepository certificateRepository,
        IUnitOfWork unitOfWork)
    {
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _ledgerRepository = ledgerRepository ?? throw new ArgumentNullException(nameof(ledgerRepository));
        _certificateRepository = certificateRepository ?? throw new ArgumentNullException(nameof(certificateRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<AccountStatementDto> GenerateStatementAsync(
        GenerateStatementRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        if (request.ToDate < request.FromDate)
            throw new ArgumentException("ToDate cannot be earlier than FromDate.", nameof(request));

        var accNum = AccountNumber.From(request.AccountNumber);
        var account = await _accountRepository.GetByAccountNumberAsync(accNum, cancellationToken)
            ?? throw new KeyNotFoundException($"Deposit Account '{request.AccountNumber}' was not found.");

        var customer = await _customerRepository.GetByCifAsync(account.Cif, cancellationToken);
        string customerName = customer?.FullName ?? "Valued Customer";

        decimal feeDeducted = 0m;
        decimal vatDeducted = 0m;

        // INVARIANT 1: Fee Exemption for Scheduled Periodic Statements (BRPD 3.1.17-ii)
        // vs Mandatory Statutory Fee for Ad-Hoc / Duplicate Statements (BRPD 3.1.23-i)
        if (request.StatementType != AccountStatementType.PeriodicScheduled && !request.WaiveFee)
        {
            account.DeductServiceChargeWithVat(
                feeAmount: 100.00m,
                vatRate: 0.15m,
                narration: $"Statement Charge ({request.StatementType})",
                out var feeMoney,
                out var vatMoney);

            feeDeducted = feeMoney.Amount;
            vatDeducted = vatMoney.Amount;

            if (feeDeducted > 0m)
            {
                var feeLedgerEntry = new AccountLedgerEntry(
                    id: Guid.NewGuid(),
                    accountNumber: account.Id.Value,
                    valueDate: DateOnly.FromDateTime(DateTime.UtcNow),
                    bookingTimeUtc: DateTime.UtcNow,
                    voucherNumber: $"STMT-CHG-{DateTime.UtcNow:yyyyMMddHHmmss}",
                    type: DebitCredit.Debit,
                    amount: Money.Bdt(feeDeducted + vatDeducted),
                    balanceAfter: account.Balance,
                    narration: $"Statutory Statement Issuance Fee (BDT {feeDeducted:N2} + VAT BDT {vatDeducted:N2})",
                    referenceNumber: $"STMT-{request.AccountNumber}-{DateTime.UtcNow:yyyyMMdd}");

                await _ledgerRepository.AddAsync(feeLedgerEntry, cancellationToken);
            }

            await _accountRepository.UpdateAsync(account, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // 2. Ledger Extraction & Chronological Balance Calculation
        var rawEntries = await _ledgerRepository.GetEntriesAsync(request.AccountNumber, request.FromDate, request.ToDate, cancellationToken);
        decimal openingBalance = await _ledgerRepository.GetOpeningBalanceAsync(request.AccountNumber, request.FromDate, cancellationToken);

        // Fallback calculation if opening balance returned 0 but account has balance with no prior history
        if (openingBalance == 0m && rawEntries.Count == 0 && account.Balance.Amount > 0m)
        {
            openingBalance = account.Balance.Amount;
        }

        var lines = new List<StatementTransactionLineDto>();
        decimal running = openingBalance;
        decimal totalDebits = 0m;
        decimal totalCredits = 0m;

        foreach (var entry in rawEntries.OrderBy(e => e.BookingTimeUtc))
        {
            decimal debit = entry.Type == DebitCredit.Debit ? entry.Amount.Amount : 0m;
            decimal credit = entry.Type == DebitCredit.Credit ? entry.Amount.Amount : 0m;

            totalDebits += debit;
            totalCredits += credit;
            running += (credit - debit);

            lines.Add(new StatementTransactionLineDto(
                TransactionId: entry.Id,
                ValueDate: entry.ValueDate,
                BookingDateUtc: entry.BookingTimeUtc,
                Narration: entry.Narration,
                ReferenceNumber: entry.ReferenceNumber,
                DebitAmount: debit,
                CreditAmount: credit,
                RunningBalance: running));
        }

        decimal closingBalance = running;

        return new AccountStatementDto(
            AccountNumber: account.Id.Value,
            CustomerName: customerName,
            CifNumber: account.Cif.Value,
            ProductType: account.ProductType.ToString(),
            Currency: account.Balance.Currency.Code,
            FromDate: request.FromDate,
            ToDate: request.ToDate,
            StatementType: request.StatementType.ToString(),
            OpeningBalance: openingBalance,
            TotalDebits: totalDebits,
            TotalCredits: totalCredits,
            ClosingBalance: closingBalance,
            FeeDeducted: feeDeducted,
            VatDeducted: vatDeducted,
            Transactions: lines,
            GeneratedAtUtc: DateTime.UtcNow);
    }

    public async Task<BalanceConfirmationCertificateDto> GenerateBalanceCertificateAsync(
        GenerateBalanceCertificateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var accNum = AccountNumber.From(request.AccountNumber);
        var account = await _accountRepository.GetByAccountNumberAsync(accNum, cancellationToken)
            ?? throw new KeyNotFoundException($"Deposit Account '{request.AccountNumber}' was not found.");

        var customer = await _customerRepository.GetByCifAsync(account.Cif, cancellationToken);
        string customerName = customer?.FullName ?? "Valued Customer";

        decimal feeDeducted = 0m;
        decimal vatDeducted = 0m;

        if (!request.WaiveFee)
        {
            account.DeductServiceChargeWithVat(
                feeAmount: 100.00m,
                vatRate: 0.15m,
                narration: "Balance Confirmation Certificate Fee",
                out var feeMoney,
                out var vatMoney);

            feeDeducted = feeMoney.Amount;
            vatDeducted = vatMoney.Amount;

            await _accountRepository.UpdateAsync(account, cancellationToken);
        }

        string certNumber = $"CERT-BAL-{request.AsOfDate:yyyyMMdd}-{Guid.NewGuid():N}"[..26].ToUpperInvariant();
        string certText = $"This is to certify that M/S / Mr. / Ms. {customerName} maintains a {account.ProductType} " +
                          $"(Account No: {account.Id.Value}) with our bank. As of close of business on {request.AsOfDate:yyyy-MM-dd}, " +
                          $"the ledger balance is BDT {account.Balance.Amount:N2}, active legal/lien holds are BDT {account.TotalActiveHoldAmount.Amount:N2}, " +
                          $"leaving an unencumbered available balance of BDT {account.AvailableBalance.Amount:N2}.";

        var certRecord = new StatutoryCertificateRecord(
            id: Guid.NewGuid(),
            certificateNumber: certNumber,
            accountNumber: account.Id.Value,
            cifNumber: account.Cif.Value,
            type: CertificateType.BalanceConfirmation,
            issuedAtUtc: DateTime.UtcNow,
            issuedByUserId: request.RequestedByUserId,
            feeCharged: Money.Bdt(feeDeducted),
            vatCharged: Money.Bdt(vatDeducted),
            detailsJson: JsonSerializer.Serialize(new
            {
                account.Balance.Amount,
                account.TotalActiveHoldAmount,
                account.AvailableBalance,
                account.AccruedInterestProvision
            }));

        await _certificateRepository.AddAsync(certRecord, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new BalanceConfirmationCertificateDto(
            CertificateNumber: certNumber,
            AccountNumber: account.Id.Value,
            CustomerName: customerName,
            CifNumber: account.Cif.Value,
            ProductType: account.ProductType.ToString(),
            Currency: account.Balance.Currency.Code,
            AsOfDate: request.AsOfDate,
            LedgerBalance: account.Balance.Amount,
            TotalActiveHoldAmount: account.TotalActiveHoldAmount.Amount,
            AvailableBalance: account.AvailableBalance.Amount,
            AccruedInterestProvision: account.AccruedInterestProvision.Amount,
            FeeDeducted: feeDeducted,
            VatDeducted: vatDeducted,
            CertificationText: certText,
            IssuedAtUtc: DateTime.UtcNow);
    }

    public async Task<TaxDeductionCertificateDto> GenerateTaxCertificateAsync(
        GenerateTaxCertificateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var accNum = AccountNumber.From(request.AccountNumber);
        var account = await _accountRepository.GetByAccountNumberAsync(accNum, cancellationToken)
            ?? throw new KeyNotFoundException($"Deposit Account '{request.AccountNumber}' was not found.");

        var customer = await _customerRepository.GetByCifAsync(account.Cif, cancellationToken);
        string customerName = customer?.FullName ?? "Valued Customer";
        string tinNumber = customer?.TaxId?.TinNumber ?? (account.HasTin ? "TIN-REGISTERED-VALID" : "NOT-SUBMITTED");

        // INVARIANT 3: NBR Section 102/53F Tax Withholding Rate Invariant (BRPD 3.1.19-i, ii)
        // 10% rate applies with verified e-TIN; 15% rate applies in the absence of valid TIN
        bool hasValidTin = account.HasTin || customer?.TaxId != null;
        decimal taxRate = hasValidTin ? 10.00m : 15.00m;

        // Interest calculation or retrieval
        decimal grossInterest = account.AccruedInterestProvision.Amount > 0m
            ? account.AccruedInterestProvision.Amount
            : Math.Max(1_000.00m, Math.Round(account.Balance.Amount * 0.05m, 2));

        decimal totalTaxWithheld = Math.Round(grossInterest * (taxRate / 100.00m), 2, MidpointRounding.AwayFromZero);
        decimal netInterest = grossInterest - totalTaxWithheld;

        string certNumber = $"CERT-TAX-{request.FiscalYearStart}-{Guid.NewGuid():N}"[..26].ToUpperInvariant();
        string challanRef = $"NBR-TR-{request.FiscalYearStart}-BB-{account.BranchCode}-CHALLAN-{Random.Shared.Next(100000, 999999)}";

        var certRecord = new StatutoryCertificateRecord(
            id: Guid.NewGuid(),
            certificateNumber: certNumber,
            accountNumber: account.Id.Value,
            cifNumber: account.Cif.Value,
            type: CertificateType.TaxDeductionAit,
            issuedAtUtc: DateTime.UtcNow,
            issuedByUserId: request.RequestedByUserId,
            feeCharged: Money.Zero(account.Balance.Currency),
            vatCharged: Money.Zero(account.Balance.Currency),
            detailsJson: JsonSerializer.Serialize(new
            {
                GrossInterest = grossInterest,
                TaxRate = taxRate,
                TotalTaxWithheld = totalTaxWithheld,
                NetInterest = netInterest,
                ChallanRef = challanRef
            }));

        await _certificateRepository.AddAsync(certRecord, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new TaxDeductionCertificateDto(
            CertificateNumber: certNumber,
            AccountNumber: account.Id.Value,
            CustomerName: customerName,
            CifNumber: account.Cif.Value,
            TaxIdentificationNumber: tinNumber,
            FiscalYearStart: request.FiscalYearStart,
            FiscalYearEnd: request.FiscalYearStart + 1,
            GrossInterestCredited: grossInterest,
            TaxRatePercentage: taxRate,
            TotalTaxWithheld: totalTaxWithheld,
            NetInterestCredited: netInterest,
            GovernmentTreasuryChallanRef: challanRef,
            IssuedAtUtc: DateTime.UtcNow);
    }
}
