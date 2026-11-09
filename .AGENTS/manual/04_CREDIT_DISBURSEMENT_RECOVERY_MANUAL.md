# Standard Operating Procedure (SOP): Credit Administration, Disbursement & Recovery

> **Regulatory Grounding:** Bangladesh Bank BRPD Circular No. 12 (13 May 2024) — Module 4  
> **Target Audience:** Credit Analysts, CRM Officers, Credit Administration Division (CAD), Branch Credit In-Charges

---

## 1. Credit Proposal Initiation & Appraisal Checklist
Before entering any credit proposal into the CBS credit module, the branch officer must verify and archive the mandatory checklist items (4.1.1, 4.2.1):
1. **Trade License & Legal Status:** Valid commercial license and incorporation documents from RJSC.
2. **Audited Financial Statements:** Minimum 3 years balance sheet, income statement, and cash flow statement.
3. **Tax Compliance:** Updated e-TIN certificate and tax return submission receipt.
4. **CIB Clearance:** Current Credit Information Bureau (CIB) inquiry report confirming zero default status across all financial institutions.
5. **Credit Risk Grading & EnvRR:** Assigned score under Bangladesh Bank Credit Risk Grading (CRG/ICRR) and Environmental & Social Risk Rating (EnvRR) framework (4.1.4-ii).
6. **Borrower Sanction Screening:** Verified absence from UNSCR and national counter-terrorism sanction lists (4.1.4-v, 4.2.6-v).

---

## 2. Credit Approval & CAD Limit Setup Workflow

```text
[Branch: Credit Proposal Submission]
                 │
                 ▼
[CRM Division: Risk Appraisal & Sanction Approval (DOA)]
                 │
                 ▼
[CAD: Legal Vetting, Collateral Valuation & CBS Limit Loading]
                 │
                 ▼
[Branch: Sanction Letter Issuance & Customer Acceptance]
                 │
                 ▼
[CAD: Pre-Disbursement Audit & Final CBS Limit Authorization]
                 │
                 ▼
[Disbursement: Phased Drawdown or Vendor Pay Order]
```

1. **CRM Approval:** CRM Division approves proposal within approved Delegation of Authority (DOA) or routes to Board of Directors (4.1.2).
2. **CAD Independent Valuation & Limit Loading:**
   - **MANDATORY INVARIANT:** Operative credit limits and collateral valuations must be entered and authorized exclusively by the **Credit Administration Division (CAD)**, never by branch personnel (4.1.3-i, 4.1.5-i).
   - Record collateral Forced Sale Value (FSV), Market Value (MV), registered mortgage deed, and valid insurance policy details (4.1.3, 4.2.23).
   - Define customer Drawing Power (DP) based on verified godown stock reports (4.1.3-iv).
3. **Sanction Letter Reconciliation:**
   - Branch issues sanction letter to customer conforming strictly to the CAD sanction advice.
   - Customer submits signed duplicate copy of acceptance.
   - CAD audits documentation, reconciles against CBS parameters, and unlocks the disbursement queue (4.1.5-i).

---

## 3. Disbursement Execution Procedures
1. **Continuous Loan (CC / Overdraft - OD):**
   - Disbursed on demand via customer-induced cheques or RTGS transfers against the operative overdraft account within the authorized Drawing Power (4.1.5-iii).
2. **Fixed Term Loan (Term Financing):**
   - Book deal in CBS to auto-generate loan account and calculate amortization schedule (Equated Monthly Installment - EMI, Balloon, or Bullet) (4.2.4, 4.2.5).
   - Disburse in single tranche or phase-by-phase milestone releases into customer's designated project disbursement account (4.2.7-ii, iii).
3. **Islamic Banking Disbursement (Bai-Murabaha & Quard):**
   - **STRICT SHARIAH MANDATE:** Disbursing funds directly into the client's current account without physical goods purchase is prohibited (4.1.5-iv).
   - Issue Pay Order (PO) or direct RTGS payment directly to the verified supplier/seller of goods upon submission of invoice and delivery chalan (4.1.5-iv).
   - Under Quard facilities, verify that interest/profit rate is configured to $0.00\%$ (4.3.4-i).

---

## 4. Installment Recovery, Auto-Sweeps & CASA Holds
1. **Automated Standing Instruction (SI) Execution:**
   - CBS initiates an automated sweep against the borrower's linked CASA account on the installment due date (4.2.16-i).
2. **Insufficient Balance & Hold Placement:**
   - If available CASA funds are insufficient, CBS creates an automated **Hold** for the unrealized shortfall amount (4.2.11-v, 4.4.8-iv).
   - The system triggers a recurrent sweep algorithm during intraday credits and EOD batch runs to recover the hold amount immediately upon liquidity arrival (4.2.11-iv).
3. **Repayment Allocation Waterfall:**
   - Recovered funds must be posted in the strict regulatory sequence (4.2.11-iii):
     $$\text{Charges/Duties} \rightarrow \text{Penal Interest} \rightarrow \text{Overdue Interest} \rightarrow \text{Regular Interest} \rightarrow \text{Principal}$$

---

## 5. Overdue Management, Classification (CL) & Restructuring
1. **Penal Interest / Compensation Application:**
   - Daily penal interest accrues on overdue principal and interest balances based on Board policy (4.1.9, 4.2.10).
   - For Islamic banking, penal charges are classified as **Compensation** and credited directly to Suspense/Charity accounts, never to bank income (4.1.9-iii).
2. **Automated Classification Evaluation:**
   - EOD evaluates Days Past Due (DPD) to assign classification:
     - Standard / SMA $\rightarrow$ Sub-Standard (SS) $\rightarrow$ Doubtful (DF) $\rightarrow$ Bad/Loss (BL) (4.1.11, 4.2.27).
   - For Bad/Loss accounts, interest accrual ceases immediately; all uncollected interest moves to Interest Suspense (3.9.2-i, 4.1.11-iii).
3. **Rescheduling & Restructuring Execution:**
   - Collect mandatory down payment in compliance with Bangladesh Bank BRPD circulars.
   - Rescheduled loans generate a new contract referenced immutably to the historical loan ID, maintaining all overdue records and non-accrual audit logs (4.1.12, 4.2.28).
