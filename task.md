# Core Banking System (CBS) - Engineering Task & Execution Tracker

> **System Standard:** Bangladesh Bank BRPD Circular No. 12 (13 May 2024) — Guidelines on Core Banking Solution Features and Controls (Version 2.0)  
> **Mandatory Deadline:** 30 June 2025  
> **Architectural Pattern:** Modular Monolith Clean Architecture (.NET 9, C# 13, EF Core, SQL Server / PostgreSQL)  
> **Testing Strategy:**  
> - **Domain & Invariants:** xUnit + FluentAssertions (Pure Invariants, Mathematical Precision)  
> - **Architecture & Boundaries:** NetArchTest (Zero Dependency Leakage)  
> - **End-to-End & Integration (E2E):** **Playwright** (API & Browser UI workflows for Teller, Maker-Checker queues, and Reports)

---

## 1. Project Phase Status Overview

| Phase | Description | Reference Module | Status | Playwright Coverage |
| :--- | :--- | :--- | :--- | :--- |
| **Phase 0** | Architecture, Foundation & Core Invariants | Core Accounting & Architecture | **DONE** | Domain Invariants Locked |
| **Phase 1** | Party & Identity (CRM / e-KYC & Frontend) | Module 8 & 3.1 | **DONE & VERIFIED** | 12 Playwright (4 API + 8 UI) Tests Passing |
| **Phase 2** | General Banking & Core Accounting Engine | Module 1 & 3 | **DONE & VERIFIED** | 3 Playwright API + Unit Suites Passing |
| **Phase 3** | Branch Cash & Teller Operations | Module 1.7, 2, 3.10-3.13 | **DONE & VERIFIED** | 3 Playwright API Suites Passing |
| **Phase 4** | Payment Rails, Clearing & Channel Gateway | Module 1.4, 1.5, 7 | **IN PROGRESS** | Planned (BACH, BEFTN, RTGS, NPSB E2E) |
| **Phase 5** | Lending, CAD & Islamic Finance | Module 4 & Shariah | **TODO** | Planned (Loan Amortization, Murabaha) |
| **Phase 6** | Trade Finance & TBML Detection | Module 5 | **TODO** | Planned (LC Lifecycle, EXP Tracking) |
| **Phase 7** | Treasury & ALM Engine | Module 6 | **TODO** | Planned (Blotter, CRR/SLR Monitoring) |
| **Phase 8** | Security, Audit & Central Bank Reporting | Module 9, 10, 11 | **TODO** | Planned (IAM, Audit Journal, CTR/STR) |
| **Phase 9** | AI Knowledge Engine, Gemini & TBML | Google Gemini / RAG | **TODO** | Planned (RAG Grounding & Prompt Evals) |

---

## 2. Phase 3 Deliverables: Branch Cash & Teller Operations (DONE)

- [x] **Milestone 3.1: Cash Drawer & Denomination Balancing Engine**
  - [x] Bangladeshi legal tender denomination bundles (`CashDenominationBundle`: 1000, 500, 200, 100, 50, 20, 10, 5, 2 notes; 5, 2, 1 coins) (`BRPD 1.7.1-iii`).
  - [x] Vault-to-teller dual-control handoff requiring distinct supervisor and cashier (`MakerCheckerSeparationInvariant`) (`BRPD 1.7.1-iii, 10.3.5-x`).
  - [x] Cashier posting limits with mandatory supervisor dual-control authorization on limit breach (`CashierPostingLimitExceededInvariant`) (`BRPD 1.7.1-ii`).
  - [x] EOD physical cash count balancing and automated suspense parking for cash shortages or excesses (`BRPD 1.7.2-ii`).
- [x] **Milestone 3.2: Transaction Profile (TP) Real-Time Validation & BFIU CTR Thresholds**
  - [x] Real-time customer `TransactionProfile` verification on cash transactions (single transaction cap & monthly cash cap checks) (`BRPD 1.7.1-x, 1.7.9`).
  - [x] Mandatory Walk-in Customer Due Diligence (CDD: Full Name, NID, Mobile, Purpose) for transactions $\ge 50,000$ BDT (`WalkInDueDiligenceRequiredInvariant`) (`BRPD 1.5.6-vi, 3.10.5-i`).
  - [x] Automatic BFIU Cash Transaction Report (CTR) threshold detection for transactions $\ge 1,000,000$ BDT (`BRPD 10.1.2-i`).
- [x] **Milestone 3.3: Remittance & Security Instruments (PO / DD / TT)**
  - [x] Security stationery inventory managed under joint custody (`SecurityStationeryInventory`: Manager != Custodian) (`BRPD 3.11.1-iv`).
  - [x] Sequential leaf allocation and exhaustion locks (`StationeryExhaustedInvariant`).
  - [x] Automated statutory tiered commission calculation and 15% NBR VAT computation (`BRPD 1.5.6-v, 3.11.1-i`).
  - [x] Instrument lifecycle state machine: `Issued`, `Liquidated`, `Stopped`, `Cancelled`, `DuplicateIssued` (`BRPD 2.1.1, 2.1.2`).
  - [x] Anti-double encashment lock (`DuplicateLiquidationBlockedInvariant`) and liquidation block on stopped/cancelled instruments (`StoppedInstrumentLiquidationBlockedInvariant`).
- [x] **Milestone 3.4: Application Services, CQRS & Persistence**
  - [x] Repositories: `ITellerDrawerRepository`, `IRemittanceInstrumentRepository`, `ISecurityStationeryRepository` with in-memory implementations.
  - [x] Commands & Handlers: `OpenTellerDrawerCommand`, `ProcessTellerCashTransactionCommand`, `CloseTellerDrawerCommand`, `IssueRemittanceInstrumentCommand`, `LiquidateRemittanceInstrumentCommand`, `ManageRemittanceInstrumentLifecycleCommand`.
  - [x] Minimal API Endpoints: `/api/v1/teller/drawers/open`, `/drawers/{id}`, `/transactions`, `/drawers/close`, `/api/v1/remittance/issue`, `/{number}`, `/liquidate`, `/stop-payment`, `/cancel`, `/stationery/seed`.
- [x] **Milestone 3.5: Playwright Automated Verification**
  - [x] 29 new domain unit tests in `TellerCashTests.cs` and `RemittanceInstrumentTests.cs` (84 total domain unit tests).
  - [x] 3 automated Playwright E2E integration test suites in `BranchCashPlaywrightTests.cs` (100% green).

---

## 3. Active Phase: Phase 4 — Payment Rails, Clearing & Channel Gateway

### Milestones & Deliverables Breakdown

- [ ] **Milestone 4.1: Bangladesh Automated Clearing House (BACH) MICR Clearing Engine**
  - [ ] Outward and Inward cheque clearing batch cycle processing (High Value $\ge 500,000$ BDT vs. Regular Value) (`BRPD 1.5.1, 1.5.2`).
  - [ ] MICR line parsing: Cheque Number (6 digits), Routing Number (9 digits), Account Number, Transaction Code (`BRPD 1.5.2-a`).
  - [ ] Return code engine (Insufficient Funds, Stop Payment, Signature Mismatch, Alteration) (`BRPD 1.5.3`).
- [ ] **Milestone 4.2: Bangladesh Electronic Funds Transfer Network (BEFTN) Engine**
  - [ ] Inward and Outward credit/debit batch creation according to BB BEFTN operating rules (`BRPD 1.5.4`).
  - [ ] Automated validation of 9-digit Bangladesh Bank routing numbers and 13-digit accounts.
  - [ ] Batch settlement accounting: Nostro / Bangladesh Bank settlement account integration.
- [ ] **Milestone 4.3: Real-Time Gross Settlement (RTGS) Pipeline**
  - [ ] Real-time high-value and urgent transactions ($\ge 100,000$ BDT standard, custom threshold) (`BRPD 1.5.5`).
  - [ ] ISO 20022 message schema adherence (`pacs.008` customer transfer, `pacs.009` financial institution transfer, `pacs.004` payment return).
- [ ] **Milestone 4.4: National Payment Switch Bangladesh (NPSB) & Channel API Gateway**
  - [ ] Interbank ATM, POS, and Internet Banking / Mobile Banking fund transfers (`BRPD 1.4, 7.1`).
  - [ ] Real-time debit authorization, duplicate transaction idempotency, and reversal window handling (`BRPD 7.1.3`).
- [ ] **Milestone 4.5: Application Services, CQRS & Playwright E2E Suite**
  - [ ] Domain aggregates, commands, handlers, endpoints, and automated Playwright E2E clearing tests.

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
| `CleanArchitectureBoundary` | Software Engineering Standard | Domain has 0 dependencies; Application does not reference Infrastructure. | NetArchTest (`CleanArchitectureTests.cs`) |

---

## 5. Execution Changelog & Historical Audit

| Timestamp (UTC) | Phase | Action / Deliverable | Commit | Result |
| :--- | :--- | :--- | :--- | :--- |
| 2026-11-09 04:00 | Phase 0 | Initialized .NET 9 Clean Architecture solution, `AccountNumber` (Modulus 11), `Money`, `FinancialVoucher`. | `903212e` | 15/15 tests passed |
| 2026-11-09 04:10 | Governance | Added Phase 9 AI Knowledge Engine (Google Gemini & RAG) with 5 BRPD Circular 12 Invariant locks. | `4fdab20` | Pushed to main |
| 2026-11-09 04:16 | Phase 1 | Implemented `Customer` Aggregate, Value Objects (NID, TIN, TP, Address), and 11 unit tests in `CustomerTests.cs`. | `3be95b9` | 31/31 tests passed |
| 2026-11-09 04:34 | Phase 1 | Built Application CQRS commands, Infrastructure Gateways/Mocks, API endpoints, and Playwright E2E suite (`CustomerOnboardingPlaywrightTests.cs`). | `3be95b9` | 36/36 tests passed |
| 2026-11-09 05:00 | Phase 1 / UI | Engineered CBS Frontend UI (Dark/Light S7 Brand, BN/EN i18n, Hamburger Drawer, Maker-Checker Dual-Control Workflow) with 5 Playwright UI browser tests. | `3be95b9` | 41/41 tests passed |
| 2026-11-09 05:35 | UI/UX Audit | Performed UI/UX audit, fixed desktop grid clipping, mobile navbar overflow, sidebar rail icons, real-time live search. Added 3 Playwright tests. | `3be95b9` | 44/44 tests passed |
| 2026-11-09 07:25 | Phase 2 | Implemented General Banking & Core Accounting: `DepositAccount` (Modulus 11, Dormancy engine, 10-Yr statutory inoperative transfer, NBR tax & excise duty, hovering recovery), `BranchOperatingDay` (EOD pending voucher hard gate, BOD), `ExchangeRate`, `OrganizationalUnit`, API endpoints, and Playwright E2E suite (`GeneralBankingPlaywrightTests.cs`). | `bf27870` | 76/76 tests passed (55 Unit, 1 Arch, 20 Playwright) |
| 2026-11-09 07:40 | Phase 3 | Implemented Branch Cash & Teller Operations: `TellerDrawer` (vault-to-teller balancing, cashier posting limits, EOD discrepancy suspense), `CashDenominationBundle` (11 legal tender denominations), `WalkInCustomerDetails` (CDD $\ge 50,000$ BDT), `RemittanceInstrument` (PO/DD tiered commission, 15% NBR statutory VAT, stop payment, liquidation), `SecurityStationeryInventory` (joint custody), API endpoints, and Playwright E2E suite (`BranchCashPlaywrightTests.cs`). | Pending | 108/108 tests passed (84 Unit, 1 Arch, 23 Playwright) |
