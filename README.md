# CBS - Core Banking Solution (Bangladesh)

> **Enterprise Core Banking System complying with Bangladesh Bank Regulatory Guidelines**  
> Aligned with **BRPD Circular No. 12, Dated: 13 May 2024** (*Guidelines on Core Banking Solution Features and Controls Version 2.0*).  
> **Mandatory Regulatory Compliance Deadline:** 30 June 2025.

---

## Overview

This repository hosts the architecture, domain models, specifications, and implementation of a modern, secure, and compliant **Core Banking Solution (CBS)** tailored for scheduled commercial banks and Islamic banks operating under the regulatory supervision of **Bangladesh Bank**.

## Regulatory Documentation & Standards

The complete, unabridged regulatory standard issued by Bangladesh Bank has been converted and cataloged into our documentation structure:

- **Full Regulatory Specification (416 Controls & Features):** [`governance/documentation/may132024brpd12e.md`](governance/documentation/may132024brpd12e.md)
- **Enterprise Implementation Roadmap:** [`governance/ROADMAP.md`](governance/ROADMAP.md)

### Key Modules Covered

| Module No. | Domain / Module Name | Key Regulatory Capabilities |
| :---: | :--- | :--- |
| **01** | **General Banking** | 13-digit A/C format, Tree Chart of Accounts (COA), EOD/BOD batch lifecycle, Self-balancing ledger, Dual-control clearing (MICR, BACH, BEFTN, RTGS). |
| **02** | **Bills & Remittance** | Local remittance (PO/DD/TT), SWIFT-integrated foreign remittances, FCY collections, BFIU threshold reporting. |
| **03** | **Deposits** | CASA, Mudaraba, Al-Wadiah, SND/MSNTD, Fixed Deposits (FDR), DPS with automated lien, Dormant/Inoperative workflows, NBR tax & excise duty. |
| **04** | **Loans / Credit / Investment** | Continuous loans (CC/OD), Fixed Term loans, Islamic Quard & Murabaha, Micro-credit, Auto-classification (CL), and Day-Past-Due (DPD) provisioning. |
| **05** | **Trade Service** | Import/Export Letters of Credit (DC), Back-to-Back LC, Master Contracts, EXP/IMP matching, Shipping Guarantees, TBML detection & vessel tracking. |
| **06** | **Treasury** | Money market, Forex blotters, Securities (BGTB, Sukuk), Derivatives, VaR limits, ALM structural liquidity gaps, and ALCO packs. |
| **07** | **Alternative Delivery Channels (ADC)** | 24x7 ISO 8583 integration, ATM/CRM/POS/QR/Internet/Mobile/Binimoy, 2FA/MFA, transaction profile (TP) checks, real-time vault/cash reflection. |
| **08** | **Customer Relationship Management (CRM)** | Single Customer View (Unique CIF), Election Commission (EC) e-KYC integration, deduplication, FATCA compliance, PEP/IP sanction screening. |
| **09** | **Information Security & Governance** | Role-Based Access Control (RBAC), Least Privilege, zero direct database access from frontend, VAPT protocols, annual IS audit with CAATs. |
| **10** | **System Administration** | User/Role lifecycle, delegation of authority (DOA), parameter management, and immutable multi-tier audit trails. |
| **11** | **Reporting** | Automated daily clean cash, ledger balance, loan defaulter statements, AML/CFT (CTR/STR) returns, and central bank data retention. |
| **12** | **Training & User Enablement** | Role-tailored cybersecurity education, simulation environments, customer awareness, and interactive process documentation. |

---

## Architectural Principles & Invariants

1. **Deterministic Double-Entry Ledger:** All accounting entries are strictly double-entry, self-balancing, and immutable upon authorization. Reversals create explicit offset entries with time-stamped audit logs.
2. **Maker-Checker (4-Eyes Principle):** Real-time dual control enforced across all financial transactions, high-value operations, parameter changes, and back-value entries.
3. **Least Privilege & Zero Direct DB Mutation:** Application users interact strictly through audited domain services. Direct modification of database schemas or tables from frontend tiers is programmatically prohibited.
4. **Shariah Banking Compliance:** Complete segregation of funds, mark-up profit calculations, and Mudaraba profit-sharing ratios adhering to Bangladesh Bank Shariah guidelines.

---

## Repository Structure

```text
CBS_core-banking-system_BD/
├── governance/
│   ├── documentation/             # Regulatory standards & compliance specs
│   │   └── may132024brpd12e.md    # Bangladesh Bank BRPD Circular 12/2024
│   ├── manual/                    # Standard Operating Procedures & runbooks
│   │   ├── 01_BRANCH_TELLER_OPERATIONS_SOP.md
│   │   ├── 02_EOD_BOD_OPERATIONS_RUNBOOK.md
│   │   ├── 03_CUSTOMER_ONBOARDING_KYC_MANUAL.md
│   │   ├── 04_CREDIT_DISBURSEMENT_RECOVERY_MANUAL.md
│   │   ├── 05_TRADE_FINANCE_OPERATIONS_MANUAL.md
│   │   ├── 06_RECONCILIATION_DISPUTE_RUNBOOK.md
│   │   └── 07_SYSTEM_ADMIN_GOVERNANCE_MANUAL.md
│   └── rules/                     # Non-negotiable system invariants & decision locks
│       ├── 00_SYSTEM_INVARIANTS.md
│       ├── 01_GENERAL_BANKING_RULES.md
│       ├── 02_DEPOSIT_ACCOUNT_RULES.md
│       ├── 03_LENDING_CREDIT_RULES.md
│       ├── 04_TRADE_FINANCE_TBML_RULES.md
│       ├── 05_TREASURY_ALM_RULES.md
│       ├── 06_SECURITY_ACCESS_CONTROL_RULES.md
│       └── 07_SHARIAH_COMPLIANCE_RULES.md
├── src/
│   ├── CBS.Domain/                # Pure domain entities, value objects & invariants
│   ├── CBS.Application/           # Use cases, CQRS & Maker-Checker interceptors
│   ├── CBS.Infrastructure/        # EF Core, persistence, security & integrations
│   └── CBS.Api/                   # ASP.NET Core APIs & channel gateways
├── tests/
│   ├── CBS.Domain.UnitTests/      # Domain & invariant unit test locks
│   └── CBS.ArchitectureTests/    # NetArchTest architectural boundary locks
├── CBS.sln                        # .NET 9 solution
├── .gitignore                     # Production .NET gitignore
├── LICENSE                        # MIT License
├── README.md                      # Repository overview
└── build_cbs_documentation.py     # PDF to MD documentation pipeline
```

---

## License

This repository is licensed under the [MIT License](LICENSE).
