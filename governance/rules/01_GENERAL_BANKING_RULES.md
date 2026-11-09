# Core Banking System (CBS) - General Banking Business Rules

> **Regulatory Grounding:** Bangladesh Bank BRPD Circular No. 12 (13 May 2024) — Module 1  
> **Status:** MANDATORY COMPLIANCE  
> **Target Subsystems:** Bank & Branch Setup, Ledger Engine, EOD/BOD, Clearing, Tellers, Account Ops

---

## 1. Bank and Branch Hierarchy Setup
- **Rule 1.1 (Hierarchy Layers):** The CBS must support an organizational business structure with multi-layer nesting:
  $$\text{Head Office} \rightarrow \text{Division} \rightarrow \text{Region} \rightarrow \text{Cluster} \rightarrow \text{Branch} \rightarrow \text{Sub-branch} \rightarrow \text{Collection Booth}$$
  Trial balance, P&L, and balance sheets must be generatable at every individual layer as well as consolidated bank-wide (1.3.8).
- **Rule 1.2 (Branch Availability):** Branches must support independent open/closure states without affecting 24x7 delivery channels (ATM, Internet Banking, MFS, POS) (1.1.2-xiv).
- **Rule 1.3 (Numbering Standards):**
  - Service Outlet (SOL) Number: Standardized format across all branches/sub-branches.
  - Customer ID (Cust ID / CIF): Unique bank-wide identifier; deduplicated before profile creation (1.1.1-i, 3.1.15-iii).
  - Customer Account Number: Exactly 13 numeric digits with check-digit validation (1.1.1-ii).

---

## 2. General Ledger (GL) & Accounting Engine
- **Rule 2.1 (Chart of Accounts Tree):** Chart of accounts must follow a tree format aligned with Bank Company Act 1991, IAS, IFRS, and BAS, categorizing: Assets, Liabilities, Income, Expenses, Contingent/Memo, Provisions, Profit/Loss, Sundry/Suspense, and Inter-branch Reconciliation (1.3.1).
- **Rule 2.2 (Multi-Currency Balances):** Every multi-currency ledger account must maintain:
  1. Transaction currency balance ($\text{Amount}_{\text{CCY}}$)
  2. Base currency equivalent ($\text{Amount}_{\text{BDT}}$) using the revaluation exchange rate (1.3.2).
- **Rule 2.3 (Back-Value Entries):**
  - Back-dated transactions require dual control (Maker-Checker) strictly bounded by the bank's Delegation of Authority (DOA) (1.3.4-i, iv).
  - Back-dated entries must automatically recalculate accrued interest, tax, and fees for all affected historical days (1.3.4-ii).
  - All back-value transactions must be logged in an independent high-supervision audit report verified at EOD (1.3.4-iii).
- **Rule 2.4 (Day Close Prohibition):**
  - **No day close with unauthorized vouchers:** The system must strictly block EOD closure if any unauthorized or pending voucher exists in the branch or central batch queue (1.3.15-i).
- **Rule 2.5 (Reversals):**
  - Authorized vouchers cannot be modified. Reversals must be executed via system-generated offsetting entries that simultaneously reverse associated fees, commission, and VAT (1.3.6, 1.3.12).

---

## 3. End of Day (EOD) & Beginning of Day (BOD) Pipeline
- **Rule 3.1 (Execution Pipeline):** EOD/BOD batch processing must support parallel job execution, load balancing, step-by-step transaction log tracking, and centralized control with branch-wise isolation (1.2.1).
- **Rule 3.2 (Daily Accruals):** Daily interest accrual and provisioning must run automatically across all interest-bearing asset and liability products (1.2.2-i).
- **Rule 3.3 (Dormancy & Inoperative State Transitions):**
  - EOD must evaluate customer activity daily. Accounts with zero customer-induced transactions within the threshold must be flagged as **Dormant** (1.2.2-vi, 3.1.20).
  - Accounts inactive for 10 years must be flagged as **Unclaimed/Inoperative** for statutory transfer to Bangladesh Bank (1.2.2-v, 3.1.21-iv).

---

## 4. Reconciliation, Clearing & Settlement
- **Rule 4.1 (Inter-Branch & Nostro Auto-Reconciliation):** The system must execute automated matching for inter-branch, Nostro, Vostro, ATM, CRM, POS, BEFTN, and RTGS transactions with automated aging analysis for unmatched entries (1.4.1).
- **Rule 4.2 (Electronic Fund Transfer - EFT):**
  - **Credit EFT:** Originator account debited; responding bank credits designated client. In case of account mismatch, automated return must execute (1.5.4-i).
  - **Debit EFT:** Originating bank issues debit; responding bank verifies customer authorization before debiting (1.5.4-ii).
- **Rule 4.3 (Real-Time Gross Settlement - RTGS):**
  - Must process both local (BDT) and foreign currencies (USD, EUR, GBP, etc.) (1.5.5-iii).
  - Must support Intraday Liquidity Facility (ILF) repo mechanisms against Bangladesh Government Treasury Bonds (BGTB) and BB Bills (1.5.5-iv).
  - Must support net settlement file ingestion for BACH, IDTP (Binimoy), and NPSB (1.5.5-viii).
- **Rule 4.4 (Cheque & Instrument Stock):** Cheque leaves and PO/DD books must be tracked from central inventory to branch vault under dual custody. Cheque status (`Issued`, `Presented`, `Paid`, `Stopped`, `Cancelled`) must be verified before payment (1.5.6, 1.5.7).

---

## 5. Teller (Cash Counter) Controls
- **Rule 5.1 (Denomination Balancing):** Physical cash holding must be tracked cashier-wise and vault-wise by currency denomination (1.7.1-iii, 1.7.2).
- **Rule 5.2 (Transaction Profile - TP Enforcement):**
  - Tellers must be warned via automatic alert/override if a transaction exceeds the customer's declared monthly/daily Transaction Profile (1.7.1-x, 1.7.9).
  - Walk-in cash transactions exceeding BFIU threshold limits mandate recording Name, NID/Photo ID, and mobile number (1.5.6-vi, 1.7.4-i, 1.7.8).
- **Rule 5.3 (Sanction Screening):** Real-time UNSCR and local sanction screening must be performed before completing counter transactions (1.5.6-vii, 1.7.8).
