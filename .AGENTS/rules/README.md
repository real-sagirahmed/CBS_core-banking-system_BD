# Core Banking System (CBS) - Regulatory Rules & System Invariants

This directory contains the authoritative, non-negotiable architectural intent, system invariants, and business rules derived directly from **Bangladesh Bank BRPD Circular No. 12 (13 May 2024) — Guidelines on Core Banking Solution (CBS) Features and Controls (Version 2.0)**.

Every developer, solutions architect, code reviewer, and autonomous agent working on this repository **MUST** adhere to these documents.

---

## Directory Index

| Rule File | Regulatory Scope | Key Controls Enforced |
| :--- | :--- | :--- |
| [**`00_SYSTEM_INVARIANTS.md`**](00_SYSTEM_INVARIANTS.md) | Global System Locks | 13-digit A/C format, double-entry self-balancing, immutability & reversals only, real-time maker-checker (4-eyes), zero direct DB mutation, financial precision (`decimal`). |
| [**`01_GENERAL_BANKING_RULES.md`**](01_GENERAL_BANKING_RULES.md) | Module 1: General Banking | Multi-tier branch hierarchy, tree COA, EOD/BOD state machine, back-value approval, no day close with unauthorized vouchers, clearing & RTGS. |
| [**`02_DEPOSIT_ACCOUNT_RULES.md`**](02_DEPOSIT_ACCOUNT_RULES.md) | Module 3 & 8: Deposits & CRM | Single Customer View (dedupe), minor lifecycle, dormancy transition, 10-year unclaimed deposit transfer to BB, NBR tax & excise duty, hovering fee deduction. |
| [**`03_LENDING_CREDIT_RULES.md`**](03_LENDING_CREDIT_RULES.md) | Module 4: Lending & Credit | CAD/CRM separation of duties, drawing power vs limit, term loan repayment priority waterfall, auto-CASA hold realization, DPD auto-classification & QJ rules. |
| [**`04_TRADE_FINANCE_TBML_RULES.md`**](04_TRADE_FINANCE_TBML_RULES.md) | Module 5: Trade Finance & TBML | Pre-transaction sanction screening, Back-to-Back LC expiry/shipment date locks, 3-phase EXP tracking, 4-month overdue export reporting, TBML red flags & vessel tracking. |
| [**`05_TREASURY_ALM_RULES.md`**](05_TREASURY_ALM_RULES.md) | Module 6: Treasury & ALM | Real-time dealer limits, CRR/SLR monitoring, off-market rate detection, MTM daily revaluations, ALM structural liquidity gaps & ALCO packs. |
| [**`06_SECURITY_ACCESS_CONTROL_RULES.md`**](06_SECURITY_ACCESS_CONTROL_RULES.md) | Module 9 & 10: Security & Admin | RBAC, super-admin financial transaction ban, call center view-only restriction, 30-day password reset with 4-history, single session enforcement, annual IS audit & VAPT. |
| [**`07_SHARIAH_COMPLIANCE_RULES.md`**](07_SHARIAH_COMPLIANCE_RULES.md) | Shariah Banking Directives | Islamic fund segregation, Mudaraba weightage-based profit sharing, Bai-Murabaha supplier payment lock, Quard zero-profit rule, compensation treatment. |

---

## Anti-Regression Policy
Under no circumstance should an established in-code architectural decision lock be reverted or weakened to accommodate temporary bug fixes or convenience. Automated unit and architecture tests in CI/CD are linked directly to these rule files.
