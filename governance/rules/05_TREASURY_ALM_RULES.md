# Core Banking System (CBS) - Treasury & Asset Liability Management (ALM) Rules

> **Regulatory Grounding:** Bangladesh Bank BRPD Circular No. 12 (13 May 2024) — Module 6  
> **Status:** MANDATORY COMPLIANCE  
> **Target Subsystems:** Front Office Dealing, Middle Office Risk, Back Office Settlement, ALM, ALCO

---

## 1. Front-Office Dealing & Limits
- **Rule 1.1 (Real-Time Dealer Limits):**
  - Dealers must operate under strict, system-enforced limits:
    - Intra-day Open Position Limit (Currency-wise)
    - Stop-Loss & Trigger Limits
    - Single Deal & Counterparty Limits (6.2.1-iv, 6.2.2-iii, iv)
  - Breach of any dealer limit must block transaction execution until authorized by Middle Office Risk officers (6.2.1-i, 6.3.1-p).
- **Rule 1.2 (Straight Through Processing - STP):**
  - Treasury deals (Money Market, FX, Securities) captured in front-office blotters must flow automatically via STP through Middle Office validation to Back Office settlement without manual re-keying (6.2.1-i, 6.5.41).

---

## 2. Middle-Office Market Risk & Mandatory Regulatory Limits
- **Rule 2.1 (Statutory Reserves - CRR & SLR Enforcement):**
  - The CBS must continuously monitor Cash Reserve Ratio (CRR) and Statutory Liquidity Ratio (SLR) balances against Bangladesh Bank regulatory thresholds (6.3.1-l).
  - Alerts must trigger immediately if intraday balances threaten statutory reserve maintenance minimums.
- **Rule 2.2 (Off-Market Rate Detection):**
  - Deal execution rates differing from live market rates (Reuters/Bloomberg feed) beyond established tolerance bands must be flagged as **Off-Market Rates** requiring Middle Office risk authorization (6.3.1-m).
- **Rule 2.3 (Mark-to-Market - MTM Revaluation):**
  - All foreign exchange positions, forward contracts, and securities portfolios (HFT/AFS) must undergo automated Mark-to-Market (MTM) revaluation at user-defined daily intervals (6.3.1-iii, 6.5.38).
  - Unrealized gains or losses must post automatically to Revaluation Reserve or Revaluation Loss ledger heads (6.3.1-v, vi).

---

## 3. Back-Office Settlement & Nostro Reconciliation
- **Rule 3.1 (Maker-Checker 4-Eyes Principle):**
  - Settlement confirmation, MT202/MT103 SWIFT generation, and fund transfers require four-eyes verification independent of the dealing desk (6.4.1-vii, 6.5.4).
- **Rule 3.2 (Nostro/Vostro Mirror Reconciliation):**
  - System must execute daily reconciliation between the internal mirror ledger (Local Book) and external correspondent bank statements (SWIFT MT940/MT950) (6.4.2-i, v, vi).
  - Unmatched Nostro items must trigger automated aging analysis and provisioning calculations (6.4.2-ix, 6.5.44).

---

## 4. Asset Liability Management (ALM) & ALCO Invariants
- **Rule 4.1 (Structural Liquidity Gap & Sensitivity):**
  - System must compute bucket-wise time bands (e.g. 1–14 days, 15–28 days, 1–3 months, 3–6 months, 6–12 months, >1 year) to assess:
    - Structural Liquidity Gap (cumulative gap vs. Board-approved tolerance limits) (6.5.49-g).
    - Interest Rate Sensitivity (Repricing gap analysis) (6.5.49-h).
- **Rule 4.2 (Stress Testing & Capital Metrics):**
  - System must support Liquidity Stress Testing, Rate Shock Analysis (+100bps, +200bps), Duration Gap analysis, and Value at Risk (VaR / DEAR) (6.5.49-q, r, t, gg).
- **Rule 4.3 (ALCO Integration):**
  - The CBS must auto-generate monthly ALCO packs reflecting Net Interest Margin (NIM), Risk-adjusted Margin, and Advance-to-Deposit Ratio (ADR) (6.5.50).
