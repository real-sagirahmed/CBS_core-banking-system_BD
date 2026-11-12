using System.Globalization;
using System.Text;
using CBS.Application.Accounts.DTOs;
using CBS.Application.Common.Interfaces;
using CBS.Domain.Enums;
using CBS.Domain.ValueObjects;

namespace CBS.Application.Accounts.Services;

// ARCHITECTURAL INTENT: Standards-Compliant ISO 15022 SWIFT MT940 (Customer Statement Message)
// and MT942 (Interim Transaction Report) Generation Engine for Corporate Cash Management
// adhering to Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Module 3.1.17-iv.
public class SwiftStatementGenerator : ISwiftStatementGenerator
{
    private readonly IAccountRepository _accountRepository;
    private readonly IAccountLedgerRepository _ledgerRepository;

    public SwiftStatementGenerator(
        IAccountRepository accountRepository,
        IAccountLedgerRepository ledgerRepository)
    {
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _ledgerRepository = ledgerRepository ?? throw new ArgumentNullException(nameof(ledgerRepository));
    }

    public async Task<SwiftStatementResponseDto> GenerateSwiftStatementAsync(
        GenerateSwiftStatementRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        if (request.ToDate < request.FromDate)
            throw new ArgumentException("ToDate cannot be earlier than FromDate.", nameof(request));

        var accNum = AccountNumber.From(request.AccountNumber);
        var account = await _accountRepository.GetByAccountNumberAsync(accNum, cancellationToken)
            ?? throw new KeyNotFoundException($"Deposit Account '{request.AccountNumber}' was not found.");

        var entries = await _ledgerRepository.GetEntriesAsync(request.AccountNumber, request.FromDate, request.ToDate, cancellationToken);
        decimal openingBal = await _ledgerRepository.GetOpeningBalanceAsync(request.AccountNumber, request.FromDate, cancellationToken);

        if (openingBal == 0m && entries.Count == 0 && account.Balance.Amount > 0m)
        {
            openingBal = account.Balance.Amount;
        }

        var sb = new StringBuilder();

        if (request.StatementType == SwiftStatementType.MT940_CustomerStatement)
        {
            BuildMt940Message(sb, account.Id.Value, request.FromDate, request.ToDate, openingBal, entries, request.StatementSequenceNumber);
        }
        else
        {
            BuildMt942Message(sb, account.Id.Value, request.FromDate, request.ToDate, entries, request.StatementSequenceNumber);
        }

        return new SwiftStatementResponseDto(
            AccountNumber: account.Id.Value,
            StatementType: request.StatementType.ToString(),
            StatementSequenceNumber: request.StatementSequenceNumber,
            SwiftMessageBlock: sb.ToString(),
            GeneratedAtUtc: DateTime.UtcNow);
    }

    private static void BuildMt940Message(
        StringBuilder sb,
        string accountNumber,
        DateOnly fromDate,
        DateOnly toDate,
        decimal openingBalance,
        IReadOnlyList<CBS.Domain.Accounts.AccountLedgerEntry> entries,
        int seqNo)
    {
        string trn = $"STMT{DateTime.UtcNow:yyyyMMdd}{seqNo:D3}";
        string openDOrC = openingBalance >= 0m ? "C" : "D";
        string openDate = fromDate.ToString("yyMMdd");
        string openAmt = Math.Abs(openingBalance).ToString("F2", CultureInfo.InvariantCulture).Replace('.', ',');

        sb.AppendLine("{1:F01BANKBDDHAXXX0000000000}{2:I940BANKBDDHXXXXN}{4:");
        sb.AppendLine($":20:{trn}");
        sb.AppendLine($":25:{accountNumber}");
        sb.AppendLine($":28C:{seqNo:D5}/1");
        sb.AppendLine($":60F:{openDOrC}{openDate}BDT{openAmt}");

        decimal running = openingBalance;
        foreach (var e in entries.OrderBy(x => x.BookingTimeUtc))
        {
            string valDate = e.ValueDate.ToString("yyMMdd");
            string entryDate = e.BookingTimeUtc.ToString("MMdd");
            string dc = e.Type == DebitCredit.Credit ? "C" : "D";
            string amtStr = e.Amount.Amount.ToString("F2", CultureInfo.InvariantCulture).Replace('.', ',');
            string refStr = e.ReferenceNumber.Length > 16 ? e.ReferenceNumber[..16] : e.ReferenceNumber;

            running += (e.Type == DebitCredit.Credit ? e.Amount.Amount : -e.Amount.Amount);

            sb.AppendLine($":61:{valDate}{entryDate}{dc}{amtStr}NMSC{refStr}//{e.VoucherNumber}");
            sb.AppendLine($":86:{e.Narration}");
        }

        string closeDOrC = running >= 0m ? "C" : "D";
        string closeDate = toDate.ToString("yyMMdd");
        string closeAmt = Math.Abs(running).ToString("F2", CultureInfo.InvariantCulture).Replace('.', ',');

        sb.AppendLine($":62F:{closeDOrC}{closeDate}BDT{closeAmt}");
        sb.Append("-}");
    }

    private static void BuildMt942Message(
        StringBuilder sb,
        string accountNumber,
        DateOnly fromDate,
        DateOnly toDate,
        IReadOnlyList<CBS.Domain.Accounts.AccountLedgerEntry> entries,
        int seqNo)
    {
        string trn = $"ITR{DateTime.UtcNow:yyyyMMdd}{seqNo:D3}";
        string nowStamp = DateTime.UtcNow.ToString("yyMMddHHmm") + "+0600";

        sb.AppendLine("{1:F01BANKBDDHAXXX0000000000}{2:I942BANKBDDHXXXXN}{4:");
        sb.AppendLine($":20:{trn}");
        sb.AppendLine($":25:{accountNumber}");
        sb.AppendLine($":28C:{seqNo:D5}/1");
        sb.AppendLine($":34F:BDT0,00");
        sb.AppendLine($":13D:{nowStamp}");

        int debitCount = 0;
        decimal debitSum = 0m;
        int creditCount = 0;
        decimal creditSum = 0m;

        foreach (var e in entries.OrderBy(x => x.BookingTimeUtc))
        {
            string valDate = e.ValueDate.ToString("yyMMdd");
            string entryDate = e.BookingTimeUtc.ToString("MMdd");
            string dc = e.Type == DebitCredit.Credit ? "C" : "D";
            string amtStr = e.Amount.Amount.ToString("F2", CultureInfo.InvariantCulture).Replace('.', ',');
            string refStr = e.ReferenceNumber.Length > 16 ? e.ReferenceNumber[..16] : e.ReferenceNumber;

            if (e.Type == DebitCredit.Debit)
            {
                debitCount++;
                debitSum += e.Amount.Amount;
            }
            else
            {
                creditCount++;
                creditSum += e.Amount.Amount;
            }

            sb.AppendLine($":61:{valDate}{entryDate}{dc}{amtStr}NMSC{refStr}//{e.VoucherNumber}");
            sb.AppendLine($":86:{e.Narration}");
        }

        string debitSumStr = debitSum.ToString("F2", CultureInfo.InvariantCulture).Replace('.', ',');
        string creditSumStr = creditSum.ToString("F2", CultureInfo.InvariantCulture).Replace('.', ',');

        sb.AppendLine($":90D:{debitCount}BDT{debitSumStr}");
        sb.AppendLine($":90C:{creditCount}BDT{creditSumStr}");
        sb.Append("-}");
    }
}
