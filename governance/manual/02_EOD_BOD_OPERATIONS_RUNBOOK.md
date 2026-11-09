# Operational Runbook: End of Day (EOD) & Beginning of Day (BOD)

> **Regulatory Grounding:** Bangladesh Bank BRPD Circular No. 12 (13 May 2024) — Module 1 (1.2 & 1.3.15)  
> **Target Audience:** IT Operations, Central Processing Center (CPC), Database Administrators, System Operators

---

## 1. Overview & Architecture
The EOD/BOD batch lifecycle transitions the CBS from one financial business day to the next. In accordance with BRPD Circular 12/2024, the batch engine must operate in a **centralized, parallelized architecture with branch-wise isolation** while keeping 24x7 alternative delivery channels (ATM, POS, Internet Banking, MFS) live (1.1.2-xiv, 1.2.1-ii, vii).

---

## 2. Pre-EOD Pre-Flight Checklist
Before initiating the EOD sequence, the central system operator must verify:
1. **Branch Cut-off:** All physical branches, sub-branches, and collection booths have completed cash counter balancing and initiated local branch closure (1.1.2-iv).
2. **Unauthorized Voucher Check (Hard Regulatory Gate):**
   - **MANDATORY INVARIANT:** The system **shall not close the day with unauthorized vouchers** (1.3.15-i).
   - Run the Pre-EOD Verification Script:
     ```sql
     -- Verify zero pending vouchers across all branches
     SELECT BranchCode, BatchNumber, VoucherNumber, MakerUserId, TransactionDate 
     FROM FinancialVouchers 
     WHERE Status IN ('Draft', 'PendingAuthorization', 'Incomplete');
     ```
   - If any unauthorized vouchers exist, EOD execution is physically blocked. Central operations must notify the relevant branch authorizer or execute authorized cancellations with maker-checker audit logging (1.3.15-ii, iii).
3. **Clearing Settlement Ingestion:** Inward and outward clearing returns from BACH, BEFTN, and NPSB net settlements are fully reconciled and posted (1.5.4, 1.5.5-viii).

---

## 3. EOD Batch Pipeline Execution Steps

```text
[Step 1: Channel Freeze/Snapshot]
               │
               ▼
[Step 2: Pre-EOD Integrity & Unauthorized Voucher Scan]
               │
               ▼
[Step 3: Daily Interest Accrual Engine]
               │
               ▼
[Step 4: Scheduled Fees, Charges & Hovering Realization]
               │
               ▼
[Step 5: Asset Classification (CL) & DPD Aging Evaluation]
               │
               ▼
[Step 6: Account Inactivity Flagging (Dormant & 10-Yr Inoperative)]
               │
               ▼
[Step 7: Daily General Ledger Balance Consolidation]
               │
               ▼
[Step 8: Beginning of Day (BOD) Calendar Date Increment]
```

### Step 3: Daily Interest Accrual Engine
- Calculate daily product interest across all operative deposit accounts (SB, SND, MTDR) and lending facilities (CC, OD, Term Loans) (1.2.2-i).
- Post automated accrual vouchers:
  - Deposit Accrual: `Dr - P&L (Interest Expense) | Cr - Interest Accrued Payable`
  - Loan Accrual (Unclassified): `Dr - Interest Receivable | Cr - P&L (Interest Income)`
  - Loan Accrual (Classified - BL/NPA): `Dr - Interest Receivable | Cr - Interest Suspense Account` (3.9.2-i).
- For Islamic banking, run Shariah-compliant provisional profit allocation based on declared monthly weightages (1.2.2-vii).

### Step 4: Scheduled Fees, Charges & Hovering Realization
- Execute scheduled maintenance fees, SMS fees, and loan repayment sweeps.
- For insufficient funds, register unrealized amount holds and trigger hovering sweeping algorithms against incoming intraday balances (3.1.19-viii, 4.2.11-v).

### Step 5: Loan Classification (CL) & DPD Aging
- Evaluate loan repayment arrears against contractual amortization schedules.
- Calculate Day Past Due (DPD) and update status (`STD`, `SMA`, `SS`, `DF`, `BL`) with associated provisioning calculations (4.1.11, 4.2.27).

### Step 6: Account Inactivity Flagging
- Flag accounts with no customer-induced transactions as **Dormant** (1.2.2-vi).
- Flag accounts inactive for 10 years as **Inoperative/Unclaimed Deposit** for regulatory notice generation and subsequent transfer to Bangladesh Bank (1.2.2-v, 3.1.21-iv).

### Step 7: General Ledger Balance Consolidation & Balancing Check
- Execute trial balance verification across every branch and consolidated head office:
  $$\sum \text{Ledger Debits} - \sum \text{Ledger Credits} = 0$$
- If balance mismatch is detected, abort pipeline immediately and generate exception dump (1.3.5, 1.3.13).

---

## 4. Beginning of Day (BOD) Operations
1. **Calendar Roll:** Advance CBS System Value Date ($T \rightarrow T+1$) referencing the central bank operational holiday calendar (1.1.2-xii, 10.3.2-c).
2. **Exchange Rate Card Import:** Load the day's initial Treasury Spot/Card FX exchange rates with maker-checker authorization (6.2.1-v, 10.3.2-a, s).
3. **Standing Instructions (SI) Trigger:** Automatically execute scheduled recurring sweeps, utility debits, and standing transfer instructions set for the new business date (1.8.1).
4. **Branch Channel Unlocking:** Release branch user queues for daily operations while maintaining audit log trails.

---

## 5. Month-End & Year-End Book Closure Extension
- **Half-Year / Month-End:** Execute interest capitalization and apply NBR Source Tax and Excise Duty deductions (3.1.19, 3.2.2-i).
- **Year-End Book Closure:**
  - Close temporary income and expenditure GL heads into Retained Earnings through system-generated closing vouchers (1.2.2-iii).
  - Produce comprehensive immutable Audit Trail reports and Annual Financial Statements (AFS) conforming to Bank Company Act 1991 and IFRS/BAS (1.2.2-iii, 1.3.1).
