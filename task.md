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
| **Phase 5** | Lending, CAD & Islamic Finance | Module 4 & Shariah | **IN PROGRESS** | Planned (CAD Segregation, Amortization, Bai-Murabaha) |
| **Phase 6** | Trade Finance & TBML Detection | Module 5 | **TODO** | Planned (LC Lifecycle, EXP Tracking) |
| **Phase 7** | Treasury & ALM Engine | Module 6 | **TODO** | Planned (Blotter, CRR/SLR Monitoring) |
| **Phase 8** | Security, Audit & Central Bank Reporting | Module 9, 10, 11 | **TODO** | Planned (IAM, Audit Journal, CTR/STR) |
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

## 3. Active Phase: Phase 5 — Lending, Credit Administration & Islamic Finance

### Milestones & Deliverables Breakdown

- [ ] **Milestone 5.1: Credit Administration & CAD Duty Segregation Engine**
  - [ ] Three-tier segregation: CRM/Branch initiates proposal $\rightarrow$ Sanction/Credit Committee approves $\rightarrow$ Credit Administration Department (CAD) vets collateral, legal documentation, and loads limit into system $\rightarrow$ Branch disburses (`BRPD 4.1.3-i, 4.1.5-i`).
  - [ ] Hard gate prohibiting loan disbursement without verified CAD documentation checklist sign-off (`CadChecklistNotVettedInvariant`).
  - [ ] Collateral Asset Registry with Forced Sale Value (FSV), Market Value (MV), and Drawing Power (DP) enforcement (`BRPD 4.1.3, 4.1.6`).
- [ ] **Milestone 5.2: Loan Facilities, Accrual & Regulatory Recovery Waterfall**
  - [ ] Facility Types: Continuous Loan (CC/OD), Term Loan, Demand Loan (`BRPD 4.1`).
  - [ ] Term loan amortization schedule generator (Reducing balance method, 360/365-day basis) (`BRPD 4.2.4`).
  - [ ] Regulatory recovery waterfall (`BRPD 4.2.11-iii`):
    $$\text{Charges} \rightarrow \text{Penal Interest} \rightarrow \text{Overdue Interest} \rightarrow \text{Regular Interest} \rightarrow \text{Principal}$$
  - [ ] Daily interest accrual and compounding schedule (`BRPD 4.1.7-i`).
- [ ] **Milestone 5.3: Islamic Banking Financing (Shariah Invariants)**
  - [ ] Bai-Murabaha supplier payment lock: Direct Pay Order/RTGS issuance to verified goods suppliers; strictly zero cash disbursement or credit to customer account (`BRPD 4.1.5-iv`).
  - [ ] Quard-e-Hasana contracts: Strict $0.00\%$ interest/profit invariant (`BRPD 4.3.4-i`).
  - [ ] Shariah default compensation: Credited to charity/suspense ledger, strictly prohibited from recognition as bank P&L income (`BRPD 4.1.9-iii`).
- [ ] **Milestone 5.4: Asset Classification (CL) & Provisioning Engine**
  - [ ] Automated Days Past Due (DPD) evaluation: Standard, SMA (Special Mention Account), SS (Sub-Standard), DF (Doubtful), Bad/Loss (`BRPD 4.1.11, 4.2.27`).
  - [ ] Automatic interest suspension: Cessation of interest accrual into income once an account is classified; transfer to Interest Suspense account (`BRPD 3.9.2-i, 4.1.11-iii`).
- [ ] **Milestone 5.5: Application Services, CQRS & Playwright E2E Suite**
  - [ ] Domain aggregates, commands, handlers, endpoints, and automated Playwright E2E lending tests.

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
| 2026-11-09 07:40 | Phase 3 | Implemented Branch Cash & Teller Operations: `TellerDrawer` (vault-to-teller balancing, cashier posting limits, EOD discrepancy suspense), `CashDenominationBundle` (11 legal tender denominations), `WalkInCustomerDetails` (CDD $\ge 50,000$ BDT), `RemittanceInstrument` (PO/DD tiered commission, 15% NBR statutory VAT, stop payment, liquidation), `SecurityStationeryInventory` (joint custody), API endpoints, and Playwright E2E suite (`BranchCashPlaywrightTests.cs`). | `ef22c23` | 108/108 tests passed (84 Unit, 1 Arch, 23 Playwright) |
| 2026-11-09 07:50 | Phase 4 | Implemented Payment Rails & Clearing Gateway: `RoutingNumber` (9 digits), `MicrLine` (E-13B standard), `ChequeClearingItem` (High Value $\ge 500k$ vs Regular Value, BB return codes 01/02/05), `BeftnBatchItem` (outward/inward credit/debit), `RtgsTransaction` (ISO 20022 pacs.008, 100k BDT threshold, BB settlement), API endpoints, and Playwright E2E suite (`PaymentRailsPlaywrightTests.cs`). | Pending | 125/125 tests passed (98 Unit, 1 Arch, 26 Playwright) |
