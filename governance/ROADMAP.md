# Core Banking System (CBS) - Enterprise Implementation Roadmap

> **Authoritative Baseline:** Bangladesh Bank BRPD Circular No. 12 (13 May 2024) — Guidelines on Core Banking Solution Features and Controls (Version 2.0)  
> **Mandatory Regulatory Compliance Deadline:** 30 June 2025  
> **Architectural Pattern:** Modular Monolith using Clean Architecture (.NET 9, C# 13, EF Core, SQL Server / PostgreSQL)

---

## Roadmap Overview & Phase Delivery Matrix

```text
[Phase 0: Foundation & Invariant Locks]
                  │
                  ▼
[Phase 1: Party & Identity (CRM / e-KYC)] ───► Module 8
                  │
                  ▼
[Phase 2: General Banking & Ledger Engine] ──► Module 1 & 3
                  │
                  ▼
[Phase 3: Branch Cash & Teller Operations] ──► Module 1.7, 2, 3.10-3.13
                  │
                  ▼
[Phase 4: Payment Rails & Clearing (BACH/RTGS)] ► Module 1.4, 1.5, 7
                  │
                  ▼
[Phase 5: Lending & Islamic Finance Engine] ──► Module 4
                  │
                  ▼
[Phase 6: International Trade Finance & TBML] ─► Module 5
                  │
                  ▼
[Phase 7: Treasury & ALM Engine] ────────────► Module 6
                  │
                  ▼
[Phase 8: Security, Reporting & Regulatory Audit] ► Module 9, 10, 11
```

---

## Phase 0: Architecture, Foundation & Core Invariants (Current Sprint)
**Objective:** Establish non-negotiable architectural boundaries, domain primitives, mathematical precision standards, and automated decision locks.

- **Milestone 0.1: Domain Primitives & Value Objects**
  - `AccountNumber`: Exactly 13 numeric digits with Modulus 11 check-digit validation (`BRPD 1.1.1-ii, 3.1.8-i`).
  - `Money`: Fixed-point `decimal` with explicit ISO 4217 `Currency` (`BDT`, `USD`, `EUR`, `GBP`). Floating-point types strictly prohibited.
  - `CifNumber`: Unique Customer Information File ID for Single Customer View.
  - `MakerCheckerAudit`: Real-time separation of duties enforcing $\text{MakerUserId} \neq \text{CheckerUserId}$ (`BRPD 1.3.18, 10.3.5-x`).
- **Milestone 0.2: General Ledger Core Invariants**
  - `FinancialVoucher`: Batch voucher aggregate strictly enforcing $\sum \text{Debits} = \sum \text{Credits}$ in base currency (`BRPD 1.3.5, 1.3.13`).
  - Immutability of authorized entries; zero in-place update/delete; automatic Reversal Entry generation (`BRPD 1.3.6, 1.3.12`).
- **Milestone 0.3: Automated Regression Locks (Unit & ArchTests)**
  - Automated unit tests asserting all Phase 0 invariants.
  - NetArchTest enforcing Clean Architecture boundaries (Domain has 0 external dependencies; Presentation has zero direct DB context).

---

## Phase 1: Party & Identity Management (CRM / e-KYC Context)
**Circular Reference:** Module 8 (Customer Relationship Management) & Module 3.1

- **Milestone 1.1: Deduplication & CIF Engine**
  - Deterministic dedupe matching using NID, Passport, DOB, and Father's Name before profile creation (`BRPD 3.1.15-iii, 8.1.1-d`).
  - Corporate Group ID linking parent conglomerates and subsidiaries (`BRPD 8.1.1-c`).
- **Milestone 1.2: Bangladesh Election Commission (EC) e-KYC Integration**
  - Secure integration gateway with Election Commission servers to verify Smart NID and biometrics (`BRPD 8.1.1-e, 8.1.13-a`).
- **Milestone 1.3: Regulatory Sanction & Compliance Screening**
  - Real-time pre-onboarding screening against United Nations Security Council Resolutions (UNSCR) and domestic counter-terrorism blacklists (`BRPD 8.1.14`).
  - PEP (Politically Exposed Persons) and IP (Influential Persons) risk scoring (`BRPD 8.1.13`).
  - FATCA identification (US Citizenship, Green Card, US address, beneficial ownership) (`BRPD 8.1.15`).
- **Milestone 1.4: Biometrics & Encrypted Specimen Storage**
  - Encrypted, compressed storage of specimen signatures and live photographs with multi-signatory mandate resolution (`BRPD 8.1.12`).

---

## Phase 2: General Banking & Core Accounting Engine
**Circular Reference:** Module 1 (General Banking) & Module 3 (Deposits)

- **Milestone 2.1: Multi-Tier Chart of Accounts (COA)**
  - Tree-format COA aligned with Bank Company Act 1991, IAS, IFRS, and BAS (`BRPD 1.3.1`).
  - Organizational hierarchy: $\text{HO} \rightarrow \text{Division} \rightarrow \text{Region} \rightarrow \text{Cluster} \rightarrow \text{Branch} \rightarrow \text{Sub-branch} \rightarrow \text{Booth}$ (`BRPD 1.3.8`).
- **Milestone 2.2: Multi-Currency Revaluation Engine**
  - Dual-balance maintenance ($\text{Amount}_{\text{CCY}}$ and $\text{Amount}_{\text{BDT}}$) with daily/scheduled MTM FX revaluation (`BRPD 1.3.2, 1.3.9`).
- **Milestone 2.3: End of Day (EOD) & Beginning of Day (BOD) Pipeline**
  - Centralized, parallelized EOD batch engine with branch-level isolation (`BRPD 1.2.1`).
  - **Hard Gate:** Prohibition of day close with any pending/unauthorized vouchers (`BRPD 1.3.15-i`).
  - Daily interest accrual and provisioning calculation (`BRPD 1.2.2-i`).
- **Milestone 2.4: Deposit Product Lifecycles & State Transitions**
  - CASA, SND (7-day notice rule), and Fixed Deposit (FDR 360-day product basis) (`BRPD 3.2, 3.4, 3.5`).
  - Automated Dormancy state engine and Branch Manager reactivation workflow (`BRPD 3.1.20`).
  - 10-Year Inoperative account statutory notice engine and transfer to Bangladesh Bank (`BRPD 3.1.21-iv`).
  - Automated NBR Source Tax deduction and annual Excise Duty assessment (`BRPD 3.1.19`).
  - Automated hovering charge recovery on future credit availability (`BRPD 3.1.19-viii`).

---

## Phase 3: Branch Cash & Teller Operations
**Circular Reference:** Module 1.7 & Module 3.10–3.13, Module 2.1

- **Milestone 3.1: Cash Drawer & Denomination Balancing**
  - Vault-to-teller cash transfers tracked cashier-wise and denomination-wise (`BRPD 1.7.1-iii, 1.7.2`).
  - Cashier posting limits and automatic supervisor queue escalation (`BRPD 1.7.1-ii`).
- **Milestone 3.2: Transaction Profile (TP) & BFIU Threshold Controls**
  - Real-time TP checking with automatic override alerts upon limit breach (`BRPD 1.7.1-x, 1.7.9`).
  - Mandatory walk-in customer due diligence capture (NID, Mobile, Purpose) for threshold transactions (`BRPD 1.5.6-vi, 3.10.5-i`).
- **Milestone 3.3: Remittance Instruments (PO / DD / TT)**
  - Joint-custody security stationery inventory management (`BRPD 3.11.1-iv`).
  - Tiered commission and 15% VAT auto-calculation (`BRPD 1.5.6-v, 3.11.1-i`).
  - Stop payment, duplicate issuance, and liquidation workflows (`BRPD 2.1.1, 2.1.2`).

---

## Phase 4: Payment Rails, Clearing & Channel Integration
**Circular Reference:** Module 1.4, Module 1.5 & Module 7 (ADC)

- **Milestone 4.1: National Payment Rails Integration**
  - BACH (Bangladesh Automated Clearing House) MICR Cheque Clearing (`BRPD 1.5.1, 1.5.2, 1.5.3`).
  - BEFTN (Electronic Fund Transfer) Credit and Debit instruction processing (`BRPD 1.5.4`).
  - RTGS (Real-Time Gross Settlement) local & foreign currency fund transfer with Intraday Liquidity Facility (ILF) Repo mechanism (`BRPD 1.5.5`).
- **Milestone 4.2: Alternative Delivery Channels (ADC) Gateway**
  - 24x7 ISO 8583 / REST API gateway for ATM, POS, CRM, Internet Banking, MFS, and Binimoy (IDTP) (`BRPD 1.1.2-vi, 7.2.1-i`).
  - Multi-Factor Authentication (MFA/2FA) and session timeout controls (`BRPD 7.2.1-xi, xiv`).
  - Real-time cash reflection on ATM/CRM terminals (`BRPD 7.2.1-vi`).
- **Milestone 4.3: Automated Reconciliation & Dispute Resolution**
  - Inter-branch, Nostro, and payment switch automated daily reconciliation (`BRPD 1.4.1-i`).
  - Trace-number dispute refund and adjustment engine (`BRPD 1.4.1-vii`).

---

## Phase 5: Lending, Credit Administration & Islamic Finance
**Circular Reference:** Module 4 (Loans/Credit/Investment) & Shariah Directives

- **Milestone 5.1: Credit Administration & CAD Separation**
  - Strict duty segregation: CRM approves; CAD loads limits and validates collateral; branch disburses (`BRPD 4.1.3-i, 4.1.5-i`).
  - Collateral management with Forced Sale Value (FSV), Market Value (MV), and Drawing Power (DP) enforcement (`BRPD 4.1.3, 4.1.6`).
- **Milestone 5.2: Facility Servicing & Waterfall Repayment**
  - Continuous CC/OD daily accrual and quarterly compounding (`BRPD 4.1.7-i`).
  - Term loan amortization schedule generator and regulatory recovery waterfall (`BRPD 4.2.4, 4.2.11-iii`):
    $$\text{Charges} \rightarrow \text{Penal Interest} \rightarrow \text{Overdue Interest} \rightarrow \text{Regular Interest} \rightarrow \text{Principal}$$
  - Automated CASA hold creation and sweep realization for installment shortfalls (`BRPD 4.2.11-v`).
- **Milestone 5.3: Islamic Banking Financing (Shariah Mandate)**
  - Bai-Murabaha supplier payment lock: Direct Pay Order/RTGS to goods vendors; zero direct cash transfers to customer accounts (`BRPD 4.1.5-iv`).
  - Quard contracts: Strict $0.00\%$ interest/profit invariant (`BRPD 4.3.4-i`).
  - Shariah Compensation accounting: Routing default compensation to suspense/charity, never to bank P&L income (`BRPD 4.1.9-iii`).
- **Milestone 5.4: Asset Classification (CL) & Provisioning Engine**
  - Automated DPD evaluation into Standard, SMA, SS, DF, and Bad/Loss (`BRPD 4.1.11, 4.2.27`).
  - Cease interest accrual into income for classified accounts (`BRPD 3.9.2-i, 4.1.11-iii`).
  - Qualitative Judgment (QJ) committee override with Maker-Checker justification (`BRPD 4.1.11-iii`).

---

## Phase 6: International Trade Finance & TBML Engine
**Circular Reference:** Module 5 (Trade Service)

- **Milestone 6.1: Import Documentary Credit Lifecycle**
  - Pre-issuance sanction screening, IRC, and CIB defaulter checks (`BRPD 5.0, 5.1.1-xlii`).
  - SWIFT MT700 generation and automated push to Bangladesh Bank Web Portal (`BRPD 5.1.8-i`).
  - Shipping Guarantee (SG) issuance with 100% margin and mandatory release upon original BL receipt (`BRPD 5.1.2`).
  - UCP 600 5-day discrepancy refusal engine and Bill of Entry/CRF customs matching (`BRPD 5.1.3-vi, 5.1.9-vi`).
- **Milestone 6.2: Back-to-Back (BTB) LC & Master Contract Invariants**
  - Hard system locks:
    $$\text{BTB Expiry Date} < \text{Master Export Expiry Date}$$
    $$\text{BTB Shipment Date} < \text{Master Export Shipment Date}$$
  - Master LC closure blocked while any BTB liability is open (`BRPD 5.6.1-v, vi, viii`).
- **Milestone 6.3: Export Operations & 3-Phase EXP Tracking**
  - 3-Phase EXP form lifecycle: 1. Issuance $\rightarrow$ 2. Document Submission $\rightarrow$ 3. Proceeds Realization (`BRPD 5.2.7-i`).
  - Automated tracking of 4-month overdue export proceeds reporting to Bangladesh Bank (`BRPD 5.2.10-i`).
  - Proceeds Realization Certificate (PRC) and 6-month cash subsidy claim tracking (`BRPD 5.2.10-i`).
- **Milestone 6.4: Trade Based Money Laundering (TBML) Detection**
  - Trade Transaction Profile (TTP) variance alerts (`BRPD 5.7.1`).
  - Commodity price verification templates against international benchmark bulletins (`BRPD 5.7.6`).
  - Vessel tracking integration and Level-3 BFIU escalation for high-risk jurisdictions (`BRPD 5.7.5, 5.7.8`).

---

## Phase 7: Treasury & Asset-Liability Management (ALM)
**Circular Reference:** Module 6 (Treasury)

- **Milestone 7.1: Front-Office Dealing & Limits**
  - Real-time blotters (FX Spot, Forward, Swap; Money Market Call, Repo, Term) with STP workflow (`BRPD 6.1, 6.2.1`).
  - System-enforced dealer limits, stop-loss triggers, and counterparty credit caps (`BRPD 6.2.2`).
- **Milestone 7.2: Middle-Office Risk & Statutory Reserves**
  - Continuous monitoring of Cash Reserve Ratio (CRR) and Statutory Liquidity Ratio (SLR) (`BRPD 6.3.1-l`).
  - Off-market rate detection against live feeds (Reuters/Bloomberg) (`BRPD 6.3.1-m`).
  - Daily Mark-to-Market (MTM) revaluations posted to Revaluation Reserve/Loss accounts (`BRPD 6.3.1-iii, 6.5.38`).
- **Milestone 7.3: Back-Office Nostro Mirror Reconciliation**
  - Daily matching of CBS Local Book vs. external SWIFT MT940 statements with aging analysis (`BRPD 6.4.2`).
- **Milestone 7.4: ALM Gaps & ALCO Pack Generator**
  - Bucket-wise structural liquidity gaps and interest rate sensitivity analysis (`BRPD 6.5.49-g, h`).
  - Value at Risk (VaR), duration gap, and rate shock simulation (+100bps, +200bps) (`BRPD 6.5.49-q, r, t`).
  - Monthly ALCO pack automation (`BRPD 6.5.50`).

---

## Phase 8: Information Security, Reporting & Regulatory Audit
**Circular Reference:** Module 9, 10, 11 & 12

- **Milestone 8.1: Identity & Access Management (IAM) Invariants**
  - Strict RBAC with Least Privilege (`BRPD 9.1.1-xix`).
  - **Inviolable Security Invariant:** System Admins strictly banned from financial transactions (`BRPD 10.3.5-vii`).
  - Call center view-only restriction (`BRPD 9.1.1-xxv`).
  - Password governance: 30-day reset, 4-history restriction, and single session concurrency blocking (`BRPD 10.1.3, 10.3.5-viii`).
- **Milestone 8.2: Immutable Audit Trails & CAATs Readiness**
  - Daily journal reports for user profile changes, transactions, overrides, and parameter updates (`BRPD 10.3.6`).
  - Annual IS Audit preparation with Computer-Assisted Auditing Tools (CAATs) and VAPT verification (`BRPD 9.1.2, 9.1.3`).
- **Milestone 8.3: Central Bank Regulatory Reporting Engine**
  - Daily Clean Cash, Ledger Balance, and Overdue Statements (`BRPD 11.1.1`).
  - AML/CFT reporting: Cash Transaction Reports (CTR) and Suspicious Transaction Reports (STR) to BFIU (`BRPD 11.1.2-u`).
  - Statutory data preservation engine (`BRPD 11.2`).

---

## Verification & Definition of Done (DoD)
Every milestone in this roadmap is considered complete ONLY when:
1. Pure domain entities encapsulate all business logic and decision locks.
2. An explicit in-code banner comment `// ARCHITECTURAL INTENT: ...` explains WHAT is locked and WHY.
3. Automated unit tests in `tests/CBS.Domain.UnitTests` pass with $100\%$ green assertions.
4. Clean Architecture dependency rules pass in `tests/CBS.ArchitectureTests`.
5. Code compiles with 0 errors and 0 warnings under .NET 9.
