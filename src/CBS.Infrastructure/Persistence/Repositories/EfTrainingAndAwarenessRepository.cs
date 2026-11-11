using CBS.Application.Training;
using CBS.Domain.Enums;
using CBS.Domain.Training;
using Microsoft.EntityFrameworkCore;

namespace CBS.Infrastructure.Persistence.Repositories;

public class EfTrainingAndAwarenessRepository : ITrainingAndAwarenessRepository
{
    private readonly CbsDbContext _context;

    public EfTrainingAndAwarenessRepository(CbsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<TrainingSimulationSession?> GetSimulationSessionByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.TrainingSimulationSessions
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<IReadOnlyList<TrainingSimulationSession>> GetSimulationSessionsByUserIdAsync(string userId, CancellationToken ct = default)
    {
        return await _context.TrainingSimulationSessions
            .Where(s => s.TraineeUserId == userId)
            .OrderByDescending(s => s.StartTimeUtc)
            .ToListAsync(ct);
    }

    public async Task SaveSimulationSessionAsync(TrainingSimulationSession session, CancellationToken ct = default)
    {
        if (_context.Entry(session).State == EntityState.Detached)
        {
            var exists = await _context.TrainingSimulationSessions.AnyAsync(s => s.Id == session.Id, ct);
            if (exists)
            {
                _context.TrainingSimulationSessions.Update(session);
            }
            else
            {
                await _context.TrainingSimulationSessions.AddAsync(session, ct);
            }
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task<EmployeeTrainingCertification?> GetCertificationByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.EmployeeTrainingCertifications
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<IReadOnlyList<EmployeeTrainingCertification>> GetCertificationsByEmployeeIdAsync(string employeeId, CancellationToken ct = default)
    {
        return await _context.EmployeeTrainingCertifications
            .Where(c => c.EmployeeId == employeeId)
            .OrderByDescending(c => c.TrainingDate)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<EmployeeTrainingCertification>> GetLapsedCertificationsAsync(DateTime asOfUtc, CancellationToken ct = default)
    {
        return await _context.EmployeeTrainingCertifications
            .Where(c => c.ExpiresAtUtc < asOfUtc)
            .OrderBy(c => c.ExpiresAtUtc)
            .ToListAsync(ct);
    }

    public async Task SaveCertificationAsync(EmployeeTrainingCertification certification, CancellationToken ct = default)
    {
        if (_context.Entry(certification).State == EntityState.Detached)
        {
            var exists = await _context.EmployeeTrainingCertifications.AnyAsync(c => c.Id == certification.Id, ct);
            if (exists)
            {
                _context.EmployeeTrainingCertifications.Update(certification);
            }
            else
            {
                await _context.EmployeeTrainingCertifications.AddAsync(certification, ct);
            }
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task<CustomerSecurityBroadcast?> GetBroadcastByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.CustomerSecurityBroadcasts
            .FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    public async Task<IReadOnlyList<CustomerSecurityBroadcast>> GetActiveBroadcastsAsync(DateOnly asOfDate, CancellationToken ct = default)
    {
        return await _context.CustomerSecurityBroadcasts
            .Where(b => b.IsActive && b.EffectiveStartDate <= asOfDate && b.EffectiveEndDate >= asOfDate)
            .OrderByDescending(b => b.EffectiveStartDate)
            .ToListAsync(ct);
    }

    public async Task SaveBroadcastAsync(CustomerSecurityBroadcast broadcast, CancellationToken ct = default)
    {
        if (_context.Entry(broadcast).State == EntityState.Detached)
        {
            var exists = await _context.CustomerSecurityBroadcasts.AnyAsync(b => b.Id == broadcast.Id, ct);
            if (exists)
            {
                _context.CustomerSecurityBroadcasts.Update(broadcast);
            }
            else
            {
                await _context.CustomerSecurityBroadcasts.AddAsync(broadcast, ct);
            }
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SystemUserManualTopic>> GetUserManualTopicsByModuleAsync(BbrpdModuleId moduleId, CancellationToken ct = default)
    {
        return await _context.SystemUserManualTopics
            .Where(t => t.ModuleId == moduleId)
            .OrderBy(t => t.TopicTitle)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SystemUserManualTopic>> SearchUserManualTopicsAsync(string query, CancellationToken ct = default)
    {
        string normalized = query.Trim().ToLower();
        return await _context.SystemUserManualTopics
            .Where(t => t.TopicTitle.ToLower().Contains(normalized)
                     || t.Summary.ToLower().Contains(normalized)
                     || t.InvariantsEnforced.ToLower().Contains(normalized)
                     || t.RegulatoryClauses.ToLower().Contains(normalized))
            .OrderBy(t => t.ModuleId)
            .ToListAsync(ct);
    }

    public async Task SaveUserManualTopicAsync(SystemUserManualTopic topic, CancellationToken ct = default)
    {
        if (_context.Entry(topic).State == EntityState.Detached)
        {
            var exists = await _context.SystemUserManualTopics.AnyAsync(t => t.Id == topic.Id, ct);
            if (exists)
            {
                _context.SystemUserManualTopics.Update(topic);
            }
            else
            {
                await _context.SystemUserManualTopics.AddAsync(topic, ct);
            }
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task SeedUserManualTopicsIfEmptyAsync(CancellationToken ct = default)
    {
        if (await _context.SystemUserManualTopics.AnyAsync(ct))
            return;

        var defaultTopics = new List<SystemUserManualTopic>
        {
            new(
                "SOP-MOD01-01",
                BbrpdModuleId.Module01_GeneralBanking,
                "Module 01: General Banking",
                "Cash Counter Balancing & Vault Dual Custody",
                "Operating procedure for daily cash drawer opening, balancing, and dual vault transfer.",
                "1. Verify opening balance with Head Cashier. 2. Post counter transactions. 3. Reconcile drawer before EOD. 4. Transfer excess cash to main vault under dual key custody.",
                "BRPD 12/2024 (1.1, 1.3)",
                "VaultDualCustodyInvariant, CashDrawerBalanceInvariant"),
            new(
                "SOP-MOD02-01",
                BbrpdModuleId.Module02_BillsAndRemittance,
                "Module 02: Bills & Remittance",
                "Foreign Inward Remittance 2.5% Cash Incentive Disbursal",
                "Validation and automated calculation of 2.5% Government Cash Incentive on eligible remittance.",
                "1. Receive remittance MT103/API payload. 2. Verify beneficiary NID and account. 3. Automatically compute 2.5% government subsidy. 4. Credit beneficiary account with remittance and incentive.",
                "BRPD 12/2024 (2.1), FE Circular 01/2022",
                "RemittanceIncentive2Point5PctInvariant, BeneficiaryVerificationInvariant"),
            new(
                "SOP-MOD03-01",
                BbrpdModuleId.Module03_Deposits,
                "Module 03: Deposits & Term Accounts",
                "10-Year Unclaimed Deposit Identification & Bangladesh Bank Transfer",
                "Procedure for scanning 10-year dormant accounts, 30-day registered notice, and BB remittance.",
                "1. Execute statutory scan on accounts dormant for 10 years. 2. Issue 30-day registered postal notice. 3. If unclaimed, transfer balance to Bangladesh Bank Unclaimed Pool via BEFTN/RTGS voucher.",
                "BRPD 12/2024 (3.2), Bank Company Act 1991 (Sec 35)",
                "UnclaimedDeposit10YearNoticeInvariant, CentralBankTransferImmutableVoucher"),
            new(
                "SOP-MOD04-01",
                BbrpdModuleId.Module04_LoansAndAdvances,
                "Module 04: Loans & Advances (CAD)",
                "Lending Against Term/Recurring Deposits (Lien Facility 90% LTV)",
                "Sanctioning and disbursing consumer or commercial loan secured against FDR/DPS lien.",
                "1. Identify active FDR/DPS instrument. 2. Calculate maximum sanctionable limit (<=90% principal). 3. Apply immutable lien hold on deposit. 4. Disburse loan funds.",
                "BRPD 12/2024 (4.1), BRPD Circular 05/2020",
                "LienMaxLtv90PctInvariant, AutoLienHoldOnUnderlyingDeposit"),
            new(
                "SOP-MOD05-01",
                BbrpdModuleId.Module05_TradeService,
                "Module 05: Trade Services (LC & Guarantees)",
                "Import Documentary Credit Issuance & Cash Margin Custody",
                "Processing import LC issuance with mandatory cash margin debit and TBML sanction screening.",
                "1. Verify importer LCA and proforma invoice. 2. Screen commodity unit price against BB index. 3. Debit cash margin to dedicated GL. 4. Transmit MT700 message.",
                "BRPD 12/2024 (5.1), Guidelines for Foreign Exchange (GFET)",
                "CashMarginSegregatedGLInvariant, TbmlPriceVarianceCheckInvariant"),
            new(
                "SOP-MOD06-01",
                BbrpdModuleId.Module06_Treasury,
                "Module 06: Treasury & ALM",
                "Bi-Weekly CRR & SLR Maintenance and Gap Monitoring",
                "Monitoring daily cash reserves and statutory liquidity ratio against BB regulatory benchmarks.",
                "1. Compute net demand and time liabilities (NDTL). 2. Reconcile CRR account with Bangladesh Bank. 3. Verify unencumbered government securities for SLR. 4. Flag breaches immediately.",
                "BRPD 12/2024 (6.1), DOS Circular 01/2014",
                "CrrDailyMinimumInvariant, SlrEligibleSecuritiesRatioInvariant"),
            new(
                "SOP-MOD07-01",
                BbrpdModuleId.Module07_Adc,
                "Module 07: Alternative Delivery Channels (ADC)",
                "ISO 8583 ADC Switch Settlement & T+1 Reconciliation",
                "Daily clearing, dispute resolution, and automated settlement of ATM, POS, and digital payment rails.",
                "1. Ingest NPSB/Visa/Mastercard settlement raw files. 2. Match against CBS ADC transaction logs. 3. Post net settlement voucher to Settlement Clearing GL. 4. Auto-lodge chargebacks for unmatched debits.",
                "BRPD 12/2024 (7.2), PSD Circular 02/2020",
                "AdcSwitchSettlementReconInvariant, T1DisputeFilingWindow"),
            new(
                "SOP-MOD08-01",
                BbrpdModuleId.Module08_Crm,
                "Module 08: Customer Relationship Management (CRM)",
                "Specimen Signature Digitization & Ultimate Beneficial Ownership (UBO)",
                "Onboarding corporate customers, resolving 25% ultimate beneficial owners, and capturing mandates.",
                "1. Collect corporate constitution documents and board resolution. 2. Trace ownership tree to identify natural persons holding >=25% shares. 3. Upload biometric/specimen signature. 4. Configure dual signatory mandate.",
                "BRPD 12/2024 (8.1), BFIU Circular 19/2019",
                "UboNaturalPersonThresholdInvariant, AccountSignatoryMandateRule"),
            new(
                "SOP-MOD09-01",
                BbrpdModuleId.Module09_SecurityAndCompliance,
                "Module 09: Security & Compliance",
                "BFIU Cash Transaction Reporting (CTR) & Maker-Checker Dual Control",
                "Mandatory daily CTR extraction for transactions exceeding BDT 1,000,000 and STR filing.",
                "1. Automated batch extracts all single-day cash transactions >= BDT 1,000,000. 2. Compliance officer reviews aggregated CTR payload. 3. Cryptographically seal and transmit to BFIU goAML. 4. Enforce strict dual approval.",
                "BRPD 12/2024 (9.1), Money Laundering Prevention Act 2012",
                "MakerCheckerDualControlInvariant, BfiuCtrThreshold10LacInvariant"),
            new(
                "SOP-MOD10-01",
                BbrpdModuleId.Module10_SystemAdmin,
                "Module 10: System Administration & Governance",
                "Financial Delegation of Authority (DoA) Matrix & Emergency Overrides",
                "Enforcing tiered officer financial limits and dual-officer emergency override protocol.",
                "1. Transaction financial impact is evaluated against officer grade limit. 2. If above limit, systematically route to authorized higher grade. 3. In operational emergency, require two independent managers to sign override voucher.",
                "BRPD 12/2024 (10.1, 10.3)",
                "DoaFinancialLimitInvariant, EmergencyDualOverrideSignatureRequirement"),
            new(
                "SOP-MOD11-01",
                BbrpdModuleId.Module11_Reporting,
                "Module 11: MIS & Regulatory Reporting",
                "Daily Clean Cash & SBS-3 Classified Loans Return Generation",
                "Generating immutable, SHA-256 tamper-proof statutory reports for Bangladesh Bank inspection.",
                "1. Trigger EOD batch report generation. 2. Reconcile vault, counter cash, and clearing GLs for Clean Cash. 3. Classify advances into SMA, SS, DF, BL for SBS-3 return. 4. Compute SHA-256 hash and persist statutory archive.",
                "BRPD 12/2024 (11.1), SBS Guidelines 2023",
                "DailyCleanCashZeroDifferenceInvariant, Sbs3RegulatoryClassificationAccuracy"),
            new(
                "SOP-MOD12-01",
                BbrpdModuleId.Module12_UserTrainingAndAwareness,
                "Module 12: User Training & Customer Awareness",
                "Employee Role-Based Certification & Customer Fraud Broadcasts",
                "Administering mandatory training simulation sessions, 80% passing certifications, and security broadcasts.",
                "1. Assign role-based curriculum (General Banking, Lending, Trade, AML/CFT, Security). 2. Conduct sandboxed simulation. 3. Grant 365-day certification if score >= 80%. 4. Disseminate anti-phishing customer alerts via ADC/SMS.",
                "BRPD 12/2024 (12.1, 12.2, 12.3)",
                "CertificationPassingScore80PctInvariant, SimulationSandboxZeroLiveContamination")
        };

        await _context.SystemUserManualTopics.AddRangeAsync(defaultTopics, ct);
        await _context.SaveChangesAsync(ct);
    }
}
