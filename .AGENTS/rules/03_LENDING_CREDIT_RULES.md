# Core Banking System (CBS) - Lending, Credit & Investment Rules

> **Regulatory Grounding:** Bangladesh Bank BRPD Circular No. 12 (13 May 2024) — Module 4  
> **Status:** MANDATORY COMPLIANCE  
> **Target Subsystems:** Loans & Advances, Islamic Finance (Quard/Murabaha), Credit Risk, Asset Classification (CL)

---

## 1. Credit Administration & Duty Separation (CRM vs CAD vs Branch)
- **Rule 1.1 (Three-Tier Credit Segregation):**
  - **Credit Risk Management (CRM):** Assesses proposals, assigns Credit Risk Grading (CRG/ICRR), and approves limit lines within Delegation of Authority (DOA) (4.1.2, 4.1.6).
  - **Credit Administration Division (CAD):** Centrally validates security documents, verifies collateral valuation, and loads/authorizes the operative limit in the CBS. Branch users are **strictly barred** from loading limits or authenticating collateral valuations (4.1.3-i, 4.1.5-i).
  - **Branch / Service Outlet:** Executes customer documentation, disburses funds only against active CAD limits, and monitors day-to-day drawing power (4.1.5-i, ii).
- **Rule 1.2 (Collateral & Drawing Power Mapping):**
  - Collateral items must be mapped against single or multiple loan limits/clients with recorded forced sale value (FSV) and market value (MV) (4.1.3-ii, iii).
  - Disbursed balance must never exceed the lower of: **Approved Credit Limit** or **Drawing Power (DP)** backed by verified stock/insurance coverage (4.1.3-iv, 4.1.6).

---

## 2. Islamic Finance Disbursement Restrictions
- **Rule 2.1 (Goods Purchase Invariant - Shariah Banking):**
  - Under Bai-Murabaha and Shariah-compliant financing, transferring funds directly into the customer's current account without proof of goods purchase is a severe **violation of Shariah** (4.1.5-iv).
  - Disbursement must be executed via direct Pay Order (PO) or settlement transfer to the verified supplier/seller of goods upon presentation of invoices, unless a formal, documented Agency Arrangement is executed (4.1.5-iv).
- **Rule 2.2 (Quard Rules):**
  - Under Quard facilities (QTDR, QHG, QPF, QBF, QACA), charging or collecting any form of interest or mark-up profit is strictly prohibited ($0.00\%$ interest/profit rate locked) (4.3.4-i). Only actual administrative service fees may be charged (4.3.10).

---

## 3. Repayment Waterfall & Automatic Realization
- **Rule 3.1 (Fixed Term Loan Repayment Sequence):**
  - Incomings funds towards overdue term loan obligations must be allocated strictly following the regulatory priority hierarchy (4.2.11-iii):
    $$\text{1. Charges/Duties} \rightarrow \text{2. Penal Interest} \rightarrow \text{3. Overdue Interest} \rightarrow \text{4. Regular Interest} \rightarrow \text{5. Principal Outstanding}$$
- **Rule 3.2 (Short-Term Agricultural Loan Waterfall):**
  - Short-term agricultural and 10-Taka micro-credit loans follow the simplified statutory sequence (4.4.8-ii):
    $$\text{1. Charges/Duties} \rightarrow \text{2. Simple Interest} \rightarrow \text{3. Principal}$$
- **Rule 3.3 (Track Receivable & CASA Hold):**
  - If a scheduled installment cannot be recovered in full due to insufficient funds in the linked CASA account:
    1. The system must create an automated **Hold/Lien** on the CASA account for the exact unrealized shortfall (4.2.11-v, 4.4.8-iv).
    2. During every subsequent EOD or intraday credit deposit, the system must recurrently sweep available funds into the loan until the installment is cleared (4.2.11-iv).

---

## 4. Interest Accounting, Arrears & Compensation
- **Rule 4.1 (Continuous Loan / CC / OD Interest):**
  - Accrual: Simple daily accrual against the negative ending balance.
  - Capitalization: Quarterly compounding for regular unclassified facilities (4.1.7-i, 3.9.1-i).
- **Rule 4.2 (Non-Performing Accounts - NPA Capitalization Lock):**
  - Interest capitalization on overdrafts and term facilities is **strictly barred** for classified (NPA/Bad-Loss) accounts and deceased borrowers (3.9.2-ii, 4.1.11-iii).
  - Accounting Entry for Classified Accruals:
    $$\text{Dr - Interest Receivable} \quad | \quad \text{Cr - Interest Suspense Account}$$
    Unearned interest on classified loans must never enter the P&L Income Account (3.9.2-i).
- **Rule 4.3 (Islamic Compensation Treatment):**
  - In Islamic banking, past due profit is termed **Compensation**. Because income is uncertain, banks cannot book compensation into income. It must be credited to a dedicated **Compensation / Suspense Account** and routed to charity/CSR as directed by Bangladesh Bank (4.1.9-iii).

---

## 5. Asset Classification (CL) & Provisioning
- **Rule 5.1 (Automated CL State Engine):**
  - The CBS must automatically evaluate Days Past Due (DPD) and overdue installment counts at every EOD to assign regulatory status (4.1.11, 4.2.27, 4.4.18):
    - **Standard (STD)** / **Special Mention Account (SMA)**
    - **Sub-Standard (SS)**
    - **Doubtful (DF)**
    - **Bad/Loss (BL)**
- **Rule 5.2 (Qualitative Judgment - QJ Override):**
  - If an authorized credit committee issues a Qualitative Judgment (QJ) downgrade due to non-financial deterioration or fraud, the system must allow manual override with mandatory maker-checker justification (4.1.11-iii, 4.2.27-iii).
  - An account downgraded via QJ can never be auto-upgraded by the system solely based on DPD without explicit committee approval.
- **Rule 5.3 (Rescheduling & Restructuring Control):**
  - Rescheduled facilities must link the new contract ID to historical account references.
  - Historical EMI-wise overdue segregation, interest waiver records, and down-payment verifications must be preserved in the audit log for Bangladesh Bank inspection (4.1.12, 4.2.28).
