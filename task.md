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
| **Phase 3** | Branch Cash & Teller Operations | Module 1.7, 2, 3.10-3.13 | **IN PROGRESS** | Planned (Vault-to-Teller Balancing, PO/DD) |
| **Phase 4** | Payment Rails, Clearing & Channel Gateway | Module 1.4, 1.5, 7 | **TODO** | Planned (BACH, BEFTN, RTGS E2E) |
| **Phase 5** | Lending, CAD & Islamic Finance | Module 4 & Shariah | **TODO** | Planned (Loan Amortization, Murabaha) |
| **Phase 6** | Trade Finance & TBML Detection | Module 5 | **TODO** | Planned (LC Lifecycle, EXP Tracking) |
| **Phase 7** | Treasury & ALM Engine | Module 6 | **TODO** | Planned (Blotter, CRR/SLR Monitoring) |
| **Phase 8** | Security, Audit & Central Bank Reporting | Module 9, 10, 11 | **TODO** | Planned (IAM, Audit Journal, CTR/STR) |
| **Phase 9** | AI Knowledge Engine, Gemini & TBML | Google Gemini / RAG | **TODO** | Planned (RAG Grounding & Prompt Evals) |

---

## 2. Phase 2 Deliverables: General Banking & Core Accounting Engine (DONE)

- [x] **Milestone 2.1: Multi-Tier Chart of Accounts (COA) & Organizational Units**
  - [x] Organizational unit model: HO -> Division -> Region -> Cluster -> Branch (`BRPD 1.3.8`).
  - [x] Branch operating constraints and hierarchy validation.
- [x] **Milestone 2.2: Multi-Currency Revaluation Engine**
  - [x] `ExchangeRate` Value Object and revaluation calculation (`BRPD 1.3.2, 1.3.9`).
  - [x] Mark-to-market (MTM) conversion and revaluation gain/loss tracking.
- [x] **Milestone 2.3: End of Day (EOD) & Beginning of Day (BOD) Pipeline**
  - [x] `BranchOperatingDay` Aggregate Root managing branch operational day lifecycle (`BRPD 1.2.1`).
  - [x] **Hard Gate:** Prohibition of day close with any pending or unauthorized vouchers (`BRPD 1.3.15-i`).
  - [x] Beginning of Day (BOD) transition incrementing operational date.
- [x] **Milestone 2.4: Deposit Product Lifecycles & State Transitions**
  - [x] `DepositAccount` Aggregate Root with 13-digit Modulus 11 validation (`BRPD 1.1.1-ii, 3.1.8-i`).
  - [x] Automated Dormancy state engine (`BRPD 3.1.20`): 6 months for Current accounts, 12 months for Savings/SND.
  - [x] Hard gate blocking customer-induced debits on dormant accounts (`DormantAccountDebitBlockedInvariant`).
  - [x] Branch Manager dormancy reactivation workflow (`BRPD 3.1.20-iv`).
  - [x] 10-Year Inoperative account statutory transfer to Bangladesh Bank (`BRPD 3.1.21-iv`).
  - [x] Automated NBR Source Tax deduction (10% with e-TIN, 15% without e-TIN) (`BRPD 3.1.19-i`).
  - [x] Statutory annual Excise Duty assessment slab calculation (`BRPD 3.1.19-vii`).
  - [x] Hovering charge recovery engine on future credit availability (`BRPD 3.1.19-viii`).
- [x] **Milestone 2.5: Application Services, CQRS & Persistence**
  - [x] Commands: `OpenDepositAccountCommand`, `PostTransactionCommand`, `RunBranchEodBatchCommand`, `ReactivateDormantAccountCommand`.
  - [x] Persistence: `InMemoryAccountRepository`, `InMemoryBranchOperatingDayRepository`.
  - [x] Minimal API Endpoints: `/api/v1/accounts`, `/transactions`, `/reactivate-dormant`, `/api/v1/eod/batch`, `/set-pending-vouchers`.
- [x] **Milestone 2.6: Comprehensive Verification**
  - [x] 24 new Unit Tests in `DepositAccountTests.cs` and `GeneralBankingTests.cs` (55 total domain unit tests).
  - [x] Playwright E2E suite in `GeneralBankingPlaywrightTests.cs` (Deposit account lifecycle, transaction posting, EOD hard gate verification).

---

## 3. Active Phase: Phase 3 — Branch Cash & Teller Operations

### Milestones & Deliverables Breakdown

- [ ] **Milestone 3.1: Cash Drawer & Denomination Balancing Engine**
  - [ ] Vault-to-teller and teller-to-vault cash movement aggregate (`BRPD 1.7.1-iii, 1.7.2`).
  - [ ] Bangladeshi currency denomination-wise tracking (Notes: 1000, 500, 200, 100, 50, 20, 10, 5, 2; Coins: 5, 2, 1).
  - [ ] Cashier transaction posting limit and mandatory supervisor authorization escalation (`BRPD 1.7.1-ii`).
  - [ ] End-of-day teller physical cash count vs. system ledger cash balancing verification (`BRPD 1.7.2-ii`).
- [ ] **Milestone 3.2: Transaction Profile (TP) Real-Time Validation & BFIU CTR/STR Thresholds**
  - [ ] Real-time comparison of deposit/withdrawal against customer TP (`BRPD 1.7.1-x`).
  - [ ] Automatic supervisor override queue and alert generation on TP limit breach (`BRPD 1.7.9`).
  - [ ] Walk-in / non-customer cash transaction due diligence capture ($\ge 50,000$ BDT threshold requiring NID, mobile, purpose) (`BRPD 1.5.6-vi, 3.10.5-i`).
  - [ ] BFIU Cash Transaction Report (CTR) threshold detection ($\ge 1,000,000$ BDT in a single day) (`BRPD 10.1.2-i`).
- [ ] **Milestone 3.3: Remittance & Security Instruments (PO / DD / TT)**
  - [ ] Security stationery inventory tracking under dual custody (`BRPD 3.11.1-iv`).
  - [ ] Pay Order (PO) & Demand Draft (DD) issuance with automated tiered commission and 15% NBR VAT deduction (`BRPD 1.5.6-v, 3.11.1-i`).
  - [ ] Instrument lifecycle: `Issued`, `Cleared/Liquidated`, `Stopped`, `Cancelled`, `DuplicateIssued` (`BRPD 2.1.1, 2.1.2`).
- [ ] **Milestone 3.4: Application Services, CQRS & Teller Endpoints**
  - [ ] Commands & Queries for vault allocation, teller cash deposit/withdrawal, PO issuance, and instrument liquidation.
  - [ ] Minimal API endpoints for Teller and Branch Cash Operations.
- [ ] **Milestone 3.5: Playwright Automated Verification**
  - [ ] Playwright E2E tests for Teller cash deposit with denomination breakdown and TP limit alert.
  - [ ] Playwright E2E tests for Pay Order issuance with commission and VAT computation and subsequent liquidation.

---

## 4. Invariant Locks & Verification Matrix

| Invariant Code | Circular Reference | Domain Constraint | Verification Method |
| :--- | :--- | :--- | :--- |
| `SanctionScreeningHitInvariant` | `BRPD 8.1.14, 5.0` | Confirmed sanction hit blocks account creation immediately. | xUnit Unit Test (`CustomerTests.cs`) |
| `MinorLegalGuardianInvariant` | `BRPD 3.2.1-ii` | Under-18 customer strictly requires verified Legal Guardian CIF. | xUnit Unit Test (`CustomerTests.cs`) |
| `NidValidationInvariant` | `BRPD 8.1.3-a` | NID must be 10, 13, or 17 numeric digits. | xUnit Unit Test (`CustomerTests.cs`) |
| `TinValidationInvariant` | `BRPD 8.1.4-a` | e-TIN must be exactly 12 numeric digits. | xUnit Unit Test (`CustomerTests.cs`) |
| `MakerCheckerSegregation` | `BRPD 1.3.18, 10.3.5-x` | $\text{MakerUserId} \neq \text{CheckerUserId}$. | xUnit Unit Test (`FinancialVoucherTests.cs`) |
| `DormantAccountDebitBlockedInvariant` | `BRPD 3.1.20` | Customer-induced debit strictly blocked on dormant accounts. | xUnit Unit Test (`DepositAccountTests.cs`) |
| `BranchEodPendingVouchersProhibitedInvariant` | `BRPD 1.3.15-i` | EOD day close prohibited if any unauthorized/pending voucher exists. | xUnit Unit Test (`GeneralBankingTests.cs`) |
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
| 2026-11-09 07:25 | Phase 2 | Implemented General Banking & Core Accounting: `DepositAccount` (Modulus 11, Dormancy engine, 10-Yr statutory inoperative transfer, NBR tax & excise duty, hovering recovery), `BranchOperatingDay` (EOD pending voucher hard gate, BOD), `ExchangeRate`, `OrganizationalUnit`, API endpoints, and Playwright E2E suite (`GeneralBankingPlaywrightTests.cs`). | Pending | 76/76 tests passed (55 Unit, 1 Arch, 20 Playwright) |
