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
| **Phase 1** | Party & Identity (CRM / e-KYC Context) | Module 8 & 3.1 | **IN PROGRESS (Core Verified)** | 4/4 Playwright E2E Tests Passing |
| **Phase 2** | General Banking & Core Accounting Engine | Module 1 & 3 | **TODO** | Planned (EOD/BOD, Voucher Lifecycle) |
| **Phase 3** | Branch Cash & Teller Operations | Module 1.7, 2, 3.10-3.13 | **TODO** | Planned (Teller Cash Balancing, PO/DD) |
| **Phase 4** | Payment Rails, Clearing & Channel Gateway | Module 1.4, 1.5, 7 | **TODO** | Planned (BACH, BEFTN, RTGS E2E) |
| **Phase 5** | Lending, CAD & Islamic Finance | Module 4 & Shariah | **TODO** | Planned (Loan Amortization, Murabaha) |
| **Phase 6** | Trade Finance & TBML Detection | Module 5 | **TODO** | Planned (LC Lifecycle, EXP Tracking) |
| **Phase 7** | Treasury & ALM Engine | Module 6 | **TODO** | Planned (Blotter, CRR/SLR Monitoring) |
| **Phase 8** | Security, Audit & Central Bank Reporting | Module 9, 10, 11 | **TODO** | Planned (IAM, Audit Journal, CTR/STR) |
| **Phase 9** | AI Knowledge Engine, Gemini & TBML | Google Gemini / RAG | **TODO** | Planned (RAG Grounding & Prompt Evals) |

---

## 2. Active Phase: Phase 1 — Party & Identity Management (CRM / e-KYC)

### Milestones & Deliverables Breakdown

- [x] **Milestone 1.1: Customer Value Objects & Deduplication Fingerprint**
  - [x] `NationalIdentityNumber`: 10-digit Smart NID, 13/17-digit legacy NID validation (`BRPD 8.1.3-a`).
  - [x] `TaxIdentificationNumber`: Exactly 12 numeric digits (`BRPD 8.1.4-a`).
  - [x] `Address`: Structured postal model (District, Upazila/Thana, Postal Code).
  - [x] `TransactionProfile`: Monthly cash/transfer caps and single-transaction limit checks (`BRPD 1.7.1-x`).
  - [x] `Customer` Aggregate Root: Deterministic SHA-256 dedupe fingerprint (`BRPD 3.1.15-iii, 8.1.1-d`).
  - [x] Unit Tests: 11 tests verifying all Milestone 1.1 domain invariants.

- [x] **Milestone 1.2: Election Commission (EC) e-KYC Verification Pipeline**
  - [x] `IElectionCommissionGateway`: Secure abstraction for Porichoy / EC biometric & Smart NID verification (`BRPD 8.1.1-e`).
  - [x] Mock / Sandbox EC integration adapter in `CBS.Infrastructure` (`MockElectionCommissionGateway.cs`).
  - [x] API endpoint `POST /api/v1/customers/ec-verify` with citizen demographic and tokenized verification (`BRPD 8.1.13-a`).

- [x] **Milestone 1.3: Regulatory Sanctions, PEP & FATCA Screening Engine**
  - [x] UNSCR & Domestic Counter-Terrorism Sanction list loader & matching engine (`BRPD 8.1.14`).
  - [x] Hard gate: Prohibit onboarding if confirmed sanction hit (`SanctionScreeningHitInvariant`).
  - [x] PEP / IP risk scoring escalation workflow (`BRPD 8.1.13`).
  - [x] FATCA Classification (`NonUsPerson`, `UsCitizen`, `UsGreenCardHolder`) (`BRPD 8.1.15`).

- [ ] **Milestone 1.4: Multi-signatory Corporate Mandates & Specimen Storage**
  - [ ] Compressed, encrypted signature specimen & photo storage abstraction (`BRPD 8.1.12`).
  - [ ] Mandate resolution rules: Single, Either/Or, Jointly, Any Two (`BRPD 8.1.10`).

- [x] **Milestone 1.5: Application Services, CQRS & Maker-Checker Onboarding Pipeline**
  - [x] `CreateCustomerDraftCommand` & Handler (Maker drafts profile $\rightarrow$ `PendingCheckerApproval`).
  - [x] `VerifyCustomerCommand` & Handler (Checker approves with 4-eyes segregation $\rightarrow$ `Active`).
  - [x] Customer DTOs and lifecycle status state machine (`Draft`, `PendingCheckerApproval`, `Active`, `Rejected`).

- [x] **Milestone 1.6: Playwright E2E Integration Suite for Customer Onboarding**
  - [x] Setup `tests/CBS.PlaywrightTests` test project with Chromium & Kestrel loopback fixture (`CbsApiServerFixture.cs`).
  - [x] Automated Playwright test: Full Maker draft $\rightarrow$ Checker authorization workflow passing (HTTP 201 $\rightarrow$ HTTP 200).
  - [x] Automated Playwright test: Maker approving own draft blocked with `MakerCheckerSeparationInvariant` (Dual-Control 4-eyes principle).
  - [x] Automated Playwright test: Prohibited Sanction Screening Hit blocked immediately (HTTP 400).
  - [x] Automated Playwright test: Minor Customer without Legal Guardian rejected (HTTP 400).

---

## 3. Invariant Locks & Verification Matrix

| Invariant Code | Circular Reference | Domain Constraint | Verification Method |
| :--- | :--- | :--- | :--- |
| `SanctionScreeningHitInvariant` | `BRPD 8.1.14, 5.0` | Confirmed sanction hit blocks account creation immediately. | xUnit Unit Test (`CustomerTests.cs`) |
| `MinorLegalGuardianInvariant` | `BRPD 3.2.1-ii` | Under-18 customer strictly requires verified Legal Guardian CIF. | xUnit Unit Test (`CustomerTests.cs`) |
| `NidValidationInvariant` | `BRPD 8.1.3-a` | NID must be 10, 13, or 17 numeric digits. | xUnit Unit Test (`CustomerTests.cs`) |
| `TinValidationInvariant` | `BRPD 8.1.4-a` | e-TIN must be exactly 12 numeric digits. | xUnit Unit Test (`CustomerTests.cs`) |
| `MakerCheckerSegregation` | `BRPD 1.3.18, 10.3.5-x` | $\text{MakerUserId} \neq \text{CheckerUserId}$. | xUnit Unit Test (`FinancialVoucherTests.cs`) |
| `CleanArchitectureBoundary` | Software Engineering Standard | Domain has 0 dependencies; Application does not reference Infrastructure. | NetArchTest (`CleanArchitectureTests.cs`) |

---

## 4. Execution Changelog & Historical Audit

| Timestamp (UTC) | Phase | Action / Deliverable | Commit | Result |
| :--- | :--- | :--- | :--- | :--- |
| 2026-11-09 04:00 | Phase 0 | Initialized .NET 9 Clean Architecture solution, `AccountNumber` (Modulus 11), `Money`, `FinancialVoucher`. | `903212e` | 15/15 tests passed |
| 2026-11-09 04:10 | Governance | Added Phase 9 AI Knowledge Engine (Google Gemini & RAG) with 5 BRPD Circular 12 Invariant locks. | `4fdab20` | Pushed to main |
| 2026-11-09 04:16 | Phase 1 | Implemented `Customer` Aggregate, Value Objects (NID, TIN, TP, Address), and 11 unit tests in `CustomerTests.cs`. | Pending | 31/31 tests passed |
| 2026-11-09 04:34 | Phase 1 | Built Application CQRS commands, Infrastructure Gateways/Mocks, API endpoints, and Playwright E2E suite (`CustomerOnboardingPlaywrightTests.cs`). | Pending | 36/36 tests passed (31 Unit, 1 Arch, 4 Playwright E2E) |
