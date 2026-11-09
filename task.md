# Core Banking System (CBS) - Engineering Task & Execution Tracker

> **System Standard:** Bangladesh Bank BRPD Circular No. 12 (13 May 2024) — Guidelines on Core Banking Solution Features and Controls (Version 2.0)  
> **Mandatory Deadline:** 30 June 2025  
> **Architectural Pattern:** Modular Monolith Clean Architecture (.NET 9, C# 13, EF Core, SQL Server / PostgreSQL)  
> **Testing Strategy:**  
> - **Domain & Invariants:** xUnit + FluentAssertions (Pure Invariants, Mathematical Precision)  
> - **Architecture & Boundaries:** NetArchTest (Zero Dependency Leakage)  
> - **End-to-End & Integration (E2E):** **Playwright** (API & Browser UI workflows for Teller, Maker-Checker queues, Clearing rails, and Reports)

---

## 1. Project Phase Status Overview

| Phase | Description | Reference Module | Status | Playwright Coverage |
| :--- | :--- | :--- | :--- | :--- |
| **Phase 0** | Architecture, Foundation & Core Invariants | Core Accounting & Architecture | **DONE** | Domain Invariants Locked |
| **Phase 1** | Party & Identity (CRM / e-KYC & Frontend) | Module 8 & 3.1 | **DONE & VERIFIED** | 12 Playwright (4 API + 8 UI) Tests Passing |
| **Phase 2** | General Banking & Core Accounting Engine | Module 1 & 3 | **DONE & VERIFIED** | 3 Playwright API + Unit Suites Passing |
| **Phase 3** | Branch Cash & Teller Operations | Module 1.7, 2, 3.10-3.13 | **DONE & VERIFIED** | 3 Playwright API Suites Passing |
| **Phase 4** | Payment Rails, Clearing & Channel Gateway | Module 1.4, 1.5, 7 | **DONE & VERIFIED** | 3 Playwright API Suites Passing |
| **Phase 5** | Lending, CAD & Islamic Finance | Module 4 & Shariah | **DONE & VERIFIED** | 3 Playwright API Suites Passing (10 Unit + 3 E2E Tests) |
| **Phase 6** | Trade Finance & TBML Detection | Module 5 | **DONE & VERIFIED** | 3 Playwright API Suites Passing (13 Unit + 3 E2E Tests) |
| **Phase 7** | Treasury & ALM Engine | Module 6 | **DONE & VERIFIED** | 3 Playwright API Suites Passing (10 Unit + 3 E2E Tests) |
| **Phase 8** | Security, Audit & Central Bank Reporting | Module 9, 10, 11 | **IN PROGRESS** | Planned (IAM, Audit Journal, CTR/STR) |
| **Phase 9** | AI Knowledge Engine, Gemini & TBML | Google Gemini / RAG | **TODO** | Planned (RAG Grounding & Prompt Evals) |

---

## 2. Phase 4 Deliverables: Payment Rails, Clearing & Channel Gateway (DONE)

- [x] **Milestone 4.1: Bangladesh Automated Clearing House (BACH) MICR Clearing Engine**
  - [x] `RoutingNumber` Value Object (9 numeric digits: 3 Bank + 3 District + 3 Branch) (`BRPD 1.5.2-a, 1.5.4`).
  - [x] `MicrLine` Value Object (6-digit Cheque Serial + 9-digit Routing + 13-digit Modulus 11 Account + 2-digit Tx Code) (`BRPD 1.5.2-a`).
  - [x] High-Value session auto-classification for cheques $\ge 500,000$ BDT vs. Regular-Value session for $< 500,000$ BDT (`BRPD 1.5.1`).
  - [x] Inward presentment decision engine with Bangladesh Bank standard return codes:
    - [x] Code 01: Insufficient funds in drawee account (`InsufficientFunds`).
    - [x] Code 02: Account closed or dormant under BRPD 3.1.20 (`AccountClosedOrDormant`).
    - [x] Code 05: Stale cheques ($> 180$ days) and post-dated cheques (`PostDatedOrStaleCheque`).
- [x] **Milestone 4.2: Bangladesh Electronic Funds Transfer Network (BEFTN) Engine**
  - [x] `BeftnBatchItem` Aggregate Root supporting direct credit and debit transfers (`BRPD 1.5.4`).
  - [x] State transitions: `Queued` $\rightarrow$ `SentToBb` $\rightarrow$ `Settled` or `Returned`.
  - [x] Originating customer account debiting on outward transfer initiation.
- [x] **Milestone 4.3: Real-Time Gross Settlement (RTGS) Pipeline**
  - [x] `RtgsTransaction` Aggregate Root implementing ISO 20022 messages (`pacs.008`, `pacs.009`, `pacs.004`) (`BRPD 1.5.5`).
  - [x] Mandatory customer credit transfer minimum threshold enforcement ($\ge 100,000$ BDT) (`RtgsMinimumThresholdInvariant`).
  - [x] Real-time debtor account debiting and central bank settlement reference binding (`SettledAtBangladeshBank`).
- [x] **Milestone 4.4: Application Services, CQRS & Persistence**
  - [x] Repositories: `IChequeClearingRepository`, `IBeftnRepository`, `IRtgsRepository` with in-memory concurrent persistence.
  - [x] Commands & Handlers: `PresentOutwardChequeCommand`, `ProcessInwardChequeClearingCommand`, `InitiateBeftnTransferCommand`, `RtgsCommandHandler`.
  - [x] Minimal API Endpoints: `/api/v1/clearing/bach/outward/present`, `/inward/process`, `/api/v1/clearing/beftn/transfer`, `/api/v1/clearing/rtgs/transfer`, `/settle`, `/{endToEndId}`.
- [x] **Milestone 4.5: Comprehensive Automated Verification**
  - [x] 14 new domain unit tests in `PaymentRailsTests.cs` (98 total domain unit tests).
  - [x] 3 automated Playwright E2E integration test suites in `PaymentRailsPlaywrightTests.cs` (26 total Playwright E2E tests).

---

## 3. Phase 5 Deliverables: Lending, Credit Administration & Islamic Finance (DONE)

- [x] **Milestone 5.1: Credit Administration & CAD Duty Segregation Engine**
  - [x] Three-tier segregation: CRM/Branch initiates proposal $\rightarrow$ Sanction/Credit Committee approves $\rightarrow$ Credit Administration Department (CAD) vets collateral, legal documentation, and loads limit into system $\rightarrow$ Branch disburses (`BRPD 4.1.3-i, 4.1.5-i`).
  - [x] Hard gate prohibiting loan disbursement without verified CAD documentation checklist sign-off (`CadChecklistNotVettedInvariant`).
  - [x] Collateral Asset Registry with Forced Sale Value (FSV), Market Value (MV), Haircut margin, and Drawing Power (DP) enforcement (`BRPD 4.1.3, 4.1.6`).
- [x] **Milestone 5.2: Loan Facilities, Accrual & Regulatory Recovery Waterfall**
  - [x] Facility Types: Continuous Loan (CC/OD), Term Loan, Demand Loan (`BRPD 4.1`).
  - [x] Regulatory recovery waterfall (`BRPD 4.2.11-iii`):
    $$\text{Charges} \rightarrow \text{Penal Interest} \rightarrow \text{Overdue Interest} \rightarrow \text{Regular Interest} \rightarrow \text{Principal}$$
  - [x] Daily interest accrual and compounding schedule (`BRPD 4.1.7-i`).
- [x] **Milestone 5.3: Islamic Banking Financing (Shariah Invariants)**
  - [x] Bai-Murabaha supplier payment lock: Direct Pay Order/RTGS issuance to verified goods suppliers; strictly zero cash disbursement or credit to customer account (`BRPD 4.1.5-iv`).
  - [x] Quard-e-Hasana contracts: Strict $0.00\%$ interest/profit invariant (`BRPD 4.3.4-i`).
  - [x] Shariah default compensation: Credited to charity/suspense ledger, strictly prohibited from recognition as bank P&L income (`BRPD 4.1.9-iii`).
- [x] **Milestone 5.4: Asset Classification (CL) & Provisioning Engine**
  - [x] Automated Days Past Due (DPD) evaluation: Standard ($<60$), SMA ($60-179$), SS ($180-269$), DF ($270-359$), Bad/Loss ($\ge 360$) (`BRPD 4.1.11, 4.2.27`).
  - [x] Automatic interest suspension: Cessation of interest accrual into income once an account is classified; transfer to Interest Suspense account (`BRPD 3.9.2-i, 4.1.11-iii`).
- [x] **Milestone 5.5: Application Services, CQRS & Playwright E2E Suite**
  - [x] Domain aggregates, commands, handlers, endpoints (`/api/v1/lending/accounts`, `/vet-cad`, `/disburse`, `/repayment`, `/evaluate-classification`).
  - [x] 10 unit tests in `LendingTests.cs` and 3 automated Playwright E2E integration tests in `LendingPlaywrightTests.cs`.

---

## 4. Phase 6 Deliverables: International Trade Finance & TBML Engine (DONE)

- [x] **Milestone 6.1: Import Documentary Credit (LC) Lifecycle Engine**
  - [x] Pre-issuance sanction screening against OFAC/UN/EU and Bangladesh Bank CIB defaulter checklist (`BRPD 5.0, 5.1.1-xlii`).
  - [x] Importer IRC (Import Registration Certificate) validity and HS Code authorization gate (`BRPD 5.1.1-v`).
  - [x] SWIFT MT700 (Issue of a Documentary Credit) generation engine adhering strictly to SWIFT Category 7 Standards (`BRPD 5.1.8-i`).
  - [x] Shipping Guarantee (SG) / Delivery Order issuance engine with mandatory 100% cash margin lock and automated guarantee cancellation upon original Bill of Lading (BL) surrender (`BRPD 5.1.2`).
- [x] **Milestone 6.2: Export Credit & Regulatory Tracking Engine**
  - [x] EXP Regulatory Form Tracking: Central Bank 120-day realization deadline monitor with automated statutory overdue notification (`BRPD 5.2.1-vi, 5.2.5-i`).
  - [x] Back-to-Back (B2B) Inland/Foreign LC opening: Strict ceiling validation against Master Export LC / Sales Contract value ($e.g. \le 75\%$) (`BRPD 5.2.2-i`).
  - [x] Export Development Fund (EDF) refinancing and loan tagging (`BRPD 5.2.4-iv`).
- [x] **Milestone 6.3: Trade-Based Money Laundering (TBML) Detection Engine**
  - [x] Unit price anomaly detection: Over-invoicing and under-invoicing variance checking against commodity global reference price bands ($\pm 15\%$ tolerance) (`BRPD 5.4.1`).
  - [x] Dual-use goods classification and high-risk / sanctioned transshipment port route screening (`BRPD 5.4.2`).
- [x] **Milestone 6.4: Application Services, CQRS & In-Memory Persistence**
  - [x] `ITradeFinanceRepository` and thread-safe in-memory concurrent persistence.
  - [x] Commands, queries, and DTOs for LC draft/authorize, SG issue/release, EXP tracking, and TBML screening.
  - [x] Minimal API endpoints: `/api/v1/trade/lc/draft`, `/lc/authorize`, `/lc/{lcNumber}`, `/shipping-guarantee/issue`, `/shipping-guarantee/authorize`, `/shipping-guarantee/release-bl`, `/exp/register`, `/exp/realize`, `/exp/evaluate-overdue`, `/tbml/screen`.
- [x] **Milestone 6.5: Automated Verification Suite (Domain Unit Tests & Playwright E2E)**
  - [x] 13 pure domain unit tests in `TradeFinanceTests.cs` (121 total domain unit tests).
  - [x] 3 automated Playwright E2E integration test suites in `TradeFinancePlaywrightTests.cs` (32 total Playwright E2E tests).

---

## 5. Phase 7 Deliverables: Treasury & ALM Engine (DONE)

- [x] **Milestone 7.1: Foreign Exchange & Money Market Deal Processing Engine**
  - [x] Front-Office deal capture: FX Spot, Forward, Swap contracts (`BRPD 6.1.1, 6.5.1`).
  - [x] Money Market Deal Capture: Call Money, Term Placements, Term Borrowings, Commercial Papers (`BRPD 6.1.2`).
  - [x] Three-tier segregation: Front-office deal capture $\rightarrow$ Mid-office limit/risk verification $\rightarrow$ Back-office settlement processing (`BRPD 6.5.1-ii`).
- [x] **Milestone 7.2: Statutory Liquidity & Reserve Management (CRR & SLR)**
  - [x] Net Demand and Time Liabilities (NDTL) daily position calculation (`BRPD 6.2`).
  - [x] Cash Reserve Ratio (CRR): Daily bi-weekly average monitoring ($4.00\%$ bi-weekly average, $3.50\%$ daily minimum with Bangladesh Bank) (`BRPD 6.2`).
  - [x] Statutory Liquidity Ratio (SLR): Daily unencumbered approved securities & excess cash monitoring ($13.00\%$ Conventional / $5.50\%$ Islamic) (`BRPD 6.2`).
- [x] **Milestone 7.3: Treasury Blotter & Asset-Liability Management (ALM)**
  - [x] Structural liquidity gap profiling across all 6 regulatory maturity buckets (1-14 days, 15-28 days, 29-90 days, 91-180 days, 181-365 days, over 1 year) (`BRPD 6.3`).
  - [x] Prudential Alert triggering when short-term net negative deficit exceeds $10\%$ of outflows in 1-14 days bucket (`BRPD 6.3`).
- [x] **Milestone 7.4: Application Services, CQRS & Persistence**
  - [x] `ITreasuryRepository` with thread-safe in-memory concurrent persistence.
  - [x] Commands, queries, and DTOs: `CaptureTreasuryDealCommand`, `VerifyMidOfficeDealCommand`, `SettleBackOfficeDealCommand`, `EvaluateCrrSlrCommand`, `EvaluateLiquidityGapCommand`.
  - [x] Minimal API endpoints: `/api/v1/treasury/deals/capture`, `/verify-mid-office`, `/settle-back-office`, `/{dealReference}`, `/alm/crr-slr/evaluate`, `/alm/liquidity-gap/evaluate`.
- [x] **Milestone 7.5: Automated Verification Suite (Domain Unit Tests & Playwright E2E)**
  - [x] 10 pure domain unit tests in `TreasuryTests.cs` (131 total domain unit tests).
  - [x] 3 automated Playwright E2E integration test suites in `TreasuryPlaywrightTests.cs` (35 total Playwright E2E tests).

---

## 6. Active Phase: Phase 8 — Security, Audit & Central Bank Reporting Engine

### Milestones & Deliverables Breakdown

- [ ] **Milestone 8.1: Role-Based Access Control (RBAC) & Dual-Control Engine**
  - [ ] Fine-grained permission model: Branch Teller, CAD Officer, Trade Services Maker, Trade Services Checker, Treasury Dealer, Risk Officer, Compliance Auditor, System Admin (`BRPD 9.1.1`).
  - [ ] Hard gate prohibiting self-authorization across all banking workflows (`MakerCheckerAudit`, `BRPD 10.3.5-x`).
  - [ ] Session security: Concurrent login prevention, session idle timeout, and password complexity enforcement (`BRPD 9.3`).
- [ ] **Milestone 8.2: Immutable Audit Journal & CAATs Engine**
  - [ ] Immutable Audit Log entry: Timestamp UTC, User CIF/Id, Action Type, Entity Name, Entity Id, Before State JSON, After State JSON, Workstation IP, Terminal ID (`BRPD 10.1, 10.3.5`).
  - [ ] Tamper-evident cryptographic hash chain linking consecutive audit journal entries (`BRPD 10.3.5-vii`).
  - [ ] CAATs (Computer Assisted Audit Techniques) regulatory data extraction interface for internal and central bank inspectors (`BRPD 10.3`).
- [ ] **Milestone 8.3: BFIU AML/CFT Regulatory Reporting (CTR & STR)**
  - [ ] Cash Transaction Report (CTR): Daily automated aggregation engine detecting any customer aggregate cash transactions $\ge 1,000,000$ BDT across all accounts in a single calendar day (`BRPD 11.1, BFIU Circular 26`).
  - [ ] Suspicious Transaction Report (STR/SAR): Rule-based detection engine for transaction structuring (smurfing just below 1M BDT), sudden velocity spikes exceeding customer Transaction Profile (TP) monthly limits (`BRPD 11.2`).
  - [ ] Regulatory XML/JSON export conforming to goAML / BFIU schema standards.
- [ ] **Milestone 8.4: Application Services, CQRS & Persistence**
  - [ ] `IAuditRepository`, `IComplianceReportingRepository` with thread-safe persistence.
  - [ ] Commands, queries, handlers, and endpoints for audit logging, CAATs extraction, CTR generation, and STR alerting.
- [ ] **Milestone 8.5: Automated Verification Suite (Domain Unit Tests & Playwright E2E)**
  - [ ] Pure invariant unit tests in `SecurityAuditComplianceTests.cs`.
  - [ ] Automated Playwright E2E integration test suite in `SecurityAuditCompliancePlaywrightTests.cs`.

---

## 4. Invariant Locks & Verification Matrix

| Invariant Code | Circular Reference | Domain Constraint | Verification Method |
| :--- | :--- | :--- | :--- |
| `SanctionScreeningHitInvariant` | `BRPD 8.1.14, 5.0` | Confirmed sanction hit blocks account creation immediately. | xUnit Unit Test (`CustomerTests.cs`) |
| `MinorLegalGuardianInvariant` | `BRPD 3.2.1-ii` | Under-18 customer strictly requires verified Legal Guardian CIF. | xUnit Unit Test (`CustomerTests.cs`) |
| `NidValidationInvariant` | `BRPD 8.1.3-a` | NID must be 10, 13, or 17 numeric digits. | xUnit Unit Test (`CustomerTests.cs`) |
| `TinValidationInvariant` | `BRPD 8.1.4-a` | e-TIN must be exactly 12 numeric digits. | xUnit Unit Test (`CustomerTests.cs`) |
| `MakerCheckerSegregation` | `BRPD 1.3.18, 10.3.5-x` | $\text{MakerUserId} \neq \text{CheckerUserId}$. | xUnit Unit Test (`FinancialVoucherTests.cs`, `TellerCashTests.cs`, `RemittanceInstrumentTests.cs`) |
| `DormantAccountDebitBlockedInvariant` | `BRPD 3.1.20` | Customer-induced debit strictly blocked on dormant accounts. | xUnit Unit Test (`DepositAccountTests.cs`) |
| `BranchEodPendingVouchersProhibitedInvariant` | `BRPD 1.3.15-i` | EOD day close prohibited if any unauthorized/pending voucher exists. | xUnit Unit Test (`GeneralBankingTests.cs`) |
| `CashierPostingLimitExceededInvariant` | `BRPD 1.7.1-ii` | Cash transaction exceeding cashier posting limit requires dual supervisor authorization. | Playwright E2E (`BranchCashPlaywrightTests.cs`) |
| `VaultCashDenominationMismatchInvariant` | `BRPD 1.7.1-iii` | Vault allocation amount must exactly equal denomination bundle sum. | xUnit Unit Test (`TellerCashTests.cs`) |
| `WalkInDueDiligenceRequiredInvariant` | `BRPD 1.5.6-vi, 3.10.5-i` | Non-customer walk-in cash $\ge 50,000$ BDT strictly requires verified NID, mobile, purpose. | Playwright E2E (`BranchCashPlaywrightTests.cs`) |
| `DuplicateLiquidationBlockedInvariant` | `BRPD 2.1.2-i` | Encashment of already liquidated remittance instrument strictly prohibited. | Playwright E2E (`BranchCashPlaywrightTests.cs`) |
| `StoppedInstrumentLiquidationBlockedInvariant` | `BRPD 2.1.1-iii` | Encashment of stopped instrument strictly prohibited. | Playwright E2E (`BranchCashPlaywrightTests.cs`) |
| `JointCustodySegregationInvariant` | `BRPD 3.11.1-iv, 10.3.5-x` | Security stationery book must be held under joint control (Manager $\neq$ Custodian). | xUnit Unit Test (`RemittanceInstrumentTests.cs`) |
| `InvalidRoutingNumberInvariant` | `BRPD 1.5.2-a, 1.5.4` | Routing number must be exactly 9 numeric digits. | xUnit Unit Test (`PaymentRailsTests.cs`) |
| `InvalidChequeNumberInvariant` | `BRPD 1.5.2-a` | Cheque number must be exactly 6 numeric digits. | xUnit Unit Test (`PaymentRailsTests.cs`) |
| `ChequeHighValueSessionInvariant` | `BRPD 1.5.1` | Cheque $\ge 500,000$ BDT must be in High-Value session; $< 500,000$ BDT in Regular-Value session. | Playwright E2E (`PaymentRailsPlaywrightTests.cs`) |
| `StaleChequeReturnInvariant` | `BRPD 1.5.3 Code 05` | Cheque presented $> 180$ days after issue date returned as Stale Cheque. | Playwright E2E (`PaymentRailsPlaywrightTests.cs`) |
| `PostDatedChequeReturnInvariant` | `BRPD 1.5.3 Code 05` | Cheque presented before issue date returned as Post-Dated Cheque. | Playwright E2E (`PaymentRailsPlaywrightTests.cs`) |
| `DraweeAccountInsufficientFundsReturnInvariant` | `BRPD 1.5.3 Code 01` | Inward clearing cheque returned if drawer account has insufficient balance. | Playwright E2E (`PaymentRailsPlaywrightTests.cs`) |
| `RtgsMinimumThresholdInvariant` | `BRPD 1.5.5` | Customer RTGS (pacs.008) transfer requires minimum 100,000 BDT. | Playwright E2E (`PaymentRailsPlaywrightTests.cs`) |
| `CadChecklistNotVettedInvariant` | `BRPD 4.1.3-i, 4.1.5-i` | Disbursement prohibited without verified CAD documentation vetting. | Playwright E2E (`LendingPlaywrightTests.cs`) |
| `MakerCheckerCadSeparationInvariant` | `BRPD 4.1.5-i, 10.3.5-x` | Disbursing branch officer strictly prohibited from vetting CAD checklist ($\text{Maker} \neq \text{CAD}$). | Playwright E2E (`LendingPlaywrightTests.cs`) |
| `DrawingPowerExceededInvariant` | `BRPD 4.1.6-iii` | Disbursement cannot exceed collateral drawing power ($MV \times (1 - \text{Margin})$). | xUnit Unit Test (`LendingTests.cs`) |
| `MurabahaDirectVendorDisbursementInvariant` | `BRPD 4.1.5-iv` | Islamic Bai-Murabaha disbursement must be direct to verified goods vendor via PO/RTGS; customer direct payout prohibited. | Playwright E2E (`LendingPlaywrightTests.cs`) |
| `QuardNonZeroInterestInvariant` | `BRPD 4.3.4-i` | Islamic Quard-e-Hasana contracts strictly prohibited from charging interest or profit ($0.00\%$). | xUnit Unit Test (`LendingTests.cs`) |
| `StatutoryRecoveryWaterfallInvariant` | `BRPD 4.2.11-iii` | Repayments strictly cascade: Charges $\rightarrow$ Penal $\rightarrow$ Overdue $\rightarrow$ Regular $\rightarrow$ Principal. | xUnit Unit Test (`LendingTests.cs`) |
| `AssetClassificationSuspenseInvariant` | `BRPD 3.9.2-i, 4.1.11` | SS/DF/BL classified loan interest accrual ceases to P&L income and diverts to Interest Suspense. | xUnit Unit Test (`LendingTests.cs`) |
| `LcCibDefaulterBlockedInvariant` | `BRPD 5.1.1-xlii` | Opening documentary credit strictly prohibited against CIB defaulter borrower. | xUnit Unit Test (`TradeFinanceTests.cs`) |
| `LcInvalidIrcBlockedInvariant` | `BRPD 5.1.1-xli` | Opening documentary credit prohibited without valid and unexpired Import Registration Certificate (IRC). | xUnit Unit Test (`TradeFinanceTests.cs`) |
| `LcSanctionScreeningHitInvariant` | `BRPD 5.0, 5.7.4` | Pre-issuance sanction screening hit against UN/OFAC/Local blacklist blocks DC opening. | xUnit Unit Test (`TradeFinanceTests.cs`) |
| `BackToBackCeilingExceededInvariant` | `BRPD 5.2.2-i, 5.6.1-ii` | Back-to-Back LC cannot exceed 75% statutory ceiling of Master Export LC value. | xUnit Unit Test (`TradeFinanceTests.cs`) |
| `BackToBackShipmentDateInvariant` | `BRPD 5.6.1-vi` | Shipment date of Back-to-Back LC must be strictly earlier than Master LC shipment date. | xUnit Unit Test (`TradeFinanceTests.cs`) |
| `ShippingGuaranteeCashMarginInvariant` | `BRPD 5.1.2-iii` | Shipping Guarantee strictly requires 100% cash margin locked as security collateral. | Playwright E2E (`TradeFinancePlaywrightTests.cs`) |
| `ShippingGuaranteeOriginalBlReleaseInvariant` | `BRPD 5.1.2-vii` | Release/Cancellation of Shipping Guarantee strictly requires surrender of original Bill of Lading. | Playwright E2E (`TradeFinancePlaywrightTests.cs`) |
| `ExpStatutory120DayOverdueInvariant` | `BRPD 5.2.10` | Unrealized export proceeds $> 120$ days automatically marked Overdue and reported to Bangladesh Bank. | Playwright E2E (`TradeFinancePlaywrightTests.cs`) |
| `TbmlPriceVarianceAlertInvariant` | `BRPD 5.4.1, 5.7.6` | Unit price variance $> \pm 15\%$ against global commodity benchmark triggers TBML over/under-invoicing alert. | Playwright E2E (`TradeFinancePlaywrightTests.cs`) |
| `TreasuryValueDateBeforeDealDateInvariant` | `BRPD 6.5.1` | Treasury trade settlement value date cannot be earlier than deal date. | xUnit Unit Test (`TreasuryTests.cs`) |
| `TreasuryMidOfficeDealerSegregationInvariant` | `BRPD 6.5.1-ii, 10.3.5-x` | Front-office dealer strictly prohibited from performing mid-office limit/risk verification. | Playwright E2E (`TreasuryPlaywrightTests.cs`) |
| `TreasuryBackOfficeSettlementWithoutMidOfficeInvariant` | `BRPD 6.5.1-ii, 6.5.4-ii` | Back-office settlement strictly prohibited before mid-office verification sign-off. | xUnit Unit Test (`TreasuryTests.cs`) |
| `TreasuryBackOfficeDealerSegregationInvariant` | `BRPD 6.5.4-i, 10.3.5-x` | Dealer and mid-office risk officers strictly prohibited from executing back-office settlement. | Playwright E2E (`TreasuryPlaywrightTests.cs`) |
| `CrrDailyMinimumShortfallInvariant` | `BRPD 6.2` | Cash Reserve Ratio falling below 3.50% daily minimum triggers regulatory CRR shortfall. | Playwright E2E (`TreasuryPlaywrightTests.cs`) |
| `SlrConventionalAndIslamicRatioInvariant` | `BRPD 6.2` | Statutory Liquidity Ratio (13.00% Conventional, 5.50% Islamic) unencumbered securities deficit triggers SLR shortfall. | Playwright E2E (`TreasuryPlaywrightTests.cs`) |
| `PrudentialLiquidityGapDeficitInvariant` | `BRPD 6.3` | Net negative liquidity gap $> 10\%$ of outflows in 1-14 days bucket triggers prudential ALM alert. | Playwright E2E (`TreasuryPlaywrightTests.cs`) |
| `CleanArchitectureBoundary` | Software Engineering Standard | Domain has 0 dependencies; Application does not reference Infrastructure. | NetArchTest (`CleanArchitectureTests.cs`) |

---

## 6. Execution Changelog & Historical Audit

| Timestamp (UTC) | Phase | Action / Deliverable | Commit | Result |
| :--- | :--- | :--- | :--- | :--- |
| 2026-11-09 04:00 | Phase 0 | Initialized .NET 9 Clean Architecture solution, `AccountNumber` (Modulus 11), `Money`, `FinancialVoucher`. | `903212e` | 15/15 tests passed |
| 2026-11-09 04:10 | Governance | Added Phase 9 AI Knowledge Engine (Google Gemini & RAG) with 5 BRPD Circular 12 Invariant locks. | `4fdab20` | Pushed to main |
| 2026-11-09 04:16 | Phase 1 | Implemented `Customer` Aggregate, Value Objects (NID, TIN, TP, Address), and 11 unit tests in `CustomerTests.cs`. | `3be95b9` | 31/31 tests passed |
| 2026-11-09 04:34 | Phase 1 | Built Application CQRS commands, Infrastructure Gateways/Mocks, API endpoints, and Playwright E2E suite (`CustomerOnboardingPlaywrightTests.cs`). | `3be95b9` | 36/36 tests passed |
| 2026-11-09 05:00 | Phase 1 / UI | Engineered CBS Frontend UI (Dark/Light S7 Brand, BN/EN i18n, Hamburger Drawer, Maker-Checker Dual-Control Workflow) with 5 Playwright UI browser tests. | `3be95b9` | 41/41 tests passed |
| 2026-11-09 05:35 | UI/UX Audit | Performed UI/UX audit, fixed desktop grid clipping, mobile navbar overflow, sidebar rail icons, real-time live search. Added 3 Playwright tests. | `3be95b9` | 44/44 tests passed |
| 2026-11-09 07:25 | Phase 2 | Implemented General Banking & Core Accounting: `DepositAccount` (Modulus 11, Dormancy engine, 10-Yr statutory inoperative transfer, NBR tax & excise duty, hovering recovery), `BranchOperatingDay` (EOD pending voucher hard gate, BOD), `ExchangeRate`, `OrganizationalUnit`, API endpoints, and Playwright E2E suite (`GeneralBankingPlaywrightTests.cs`). | `bf27870` | 76/76 tests passed (55 Unit, 1 Arch, 20 Playwright) |
| 2026-11-09 07:40 | Phase 3 | Implemented Branch Cash & Teller Operations: `TellerDrawer` (vault-to-teller balancing, cashier posting limits, EOD discrepancy suspense), `CashDenominationBundle` (11 legal tender denominations), `WalkInCustomerDetails` (CDD $\ge 50,000$ BDT), `RemittanceInstrument` (PO/DD tiered commission, 15% NBR statutory VAT, stop payment, liquidation), `SecurityStationeryInventory` (joint custody), API endpoints, and Playwright E2E suite (`BranchCashPlaywrightTests.cs`). | `ef22c23` | 108/108 tests passed (84 Unit, 1 Arch, 23 Playwright) |
| 2026-11-09 07:50 | Phase 4 | Implemented Payment Rails & Clearing Gateway: `RoutingNumber` (9 digits), `MicrLine` (E-13B standard), `ChequeClearingItem` (High Value $\ge 500k$ vs Regular Value, BB return codes 01/02/05), `BeftnBatchItem` (outward/inward credit/debit), `RtgsTransaction` (ISO 20022 pacs.008, 100k BDT threshold, BB settlement), API endpoints, and Playwright E2E suite (`PaymentRailsPlaywrightTests.cs`). | `f7d6330` | 125/125 tests passed (98 Unit, 1 Arch, 26 Playwright) |
| 2026-11-09 08:00 | Phase 5 | Implemented Lending, Credit Administration & Islamic Finance: `LoanAccount` Aggregate, `CollateralAsset` (FSV/MV/DP), 3-Tier CAD duty segregation hard gate, Bai-Murabaha direct vendor disbursement lock, Quard-e-Hasana $0.00\%$ interest, statutory repayment waterfall, asset classification (Standard/SMA/SS/DF/BL) with automatic interest suspense, API endpoints, and Playwright E2E suite (`LendingPlaywrightTests.cs`). | `efe7326` | 138/138 tests passed (108 Unit, 1 Arch, 29 Playwright) |
| 2026-11-09 08:15 | Phase 6 | Implemented International Trade Finance & TBML Engine: `DocumentaryCredit` Aggregate, `ShippingGuarantee` (100% cash margin & BL surrender release), `ExpRegulatoryTracking` (120-day realization monitor & BB overdue reporting), `TbmlScreeningResult` (over/under-invoicing $\pm 15\%$ & sanctioned port route detection), SWIFT Category 7 MT700 message generator, `ITradeFinanceRepository`, API endpoints, and Playwright E2E suite (`TradeFinancePlaywrightTests.cs`). | `b2c7adc` | 154/154 tests passed (121 Unit, 1 Arch, 32 Playwright) |
| 2026-11-09 08:25 | Phase 7 | Implemented Treasury & ALM Engine: `TreasuryDeal` Aggregate, 3-tier Front-Mid-Back duty segregation hard gates, `CrrSlrPosition` (CRR 3.5% daily min & 13%/5.5% Conventional/Islamic SLR shortfall detection), `LiquidityGapProfile` (6 regulatory maturity buckets & 1-14 days deficit alert), `ITreasuryRepository`, API endpoints, and Playwright E2E suite (`TreasuryPlaywrightTests.cs`). | Pending Commit | 167/167 tests passed (131 Unit, 1 Arch, 35 Playwright) |
