# Core Banking System (CBS) - Deposit & Customer Account Rules

> **Regulatory Grounding:** Bangladesh Bank BRPD Circular No. 12 (13 May 2024) — Module 3 & Module 8  
> **Status:** MANDATORY COMPLIANCE  
> **Target Subsystems:** CASA, Term Deposits, DPS, Tax/Excise Deduction, Account Lifecycle

---

## 1. Customer Eligibility & Special Restrictions
- **Rule 1.1 (Customer Classification):** System must enforce distinct parameter setups for: Individual, Joint, Minor, Non-Resident, Staff, Trustee, Proprietorship, Partnership, Pvt Ltd, Public Ltd, NGO/NPO, Autonomous, and Government bodies (3.1.1).
- **Rule 1.2 (Foreign Contractor Restriction):** Foreign contractors and entities restricted by national foreign exchange laws are strictly barred from opening interest-bearing deposit accounts (3.1.1-w).
- **Rule 1.3 (Deduplication Check):** Deduplication (Dedupe) checks on NID/Passport/Date of Birth must execute before a new Customer Information File (Cust ID) is created to guarantee a **Single Customer View** (3.1.15-iii, 8.1.1).
- **Rule 1.4 (Minor Account Lifecycle):**
  - Any customer under 18 years of age is classified as a Minor (configurable) with required legal guardian KYC (3.2.1-i, ii).
  - Upon reaching age 18, the system must trigger an alert to update KYC and transition the account to standard individual operation (3.2.1-ii).
  - Minor account legal guardians must pass UNSCR/local sanction screening (3.2.1-iii).

---

## 2. Account Inactivity: Dormant, Inoperative & Deceased Rules
- **Rule 2.1 (Dormant Account State Machine):**
  - **Condition:** No customer-initiated debit/credit transaction for the BB-defined period on CASA accounts (3.1.20-ii).
  - **Posting Restriction:** Only incoming credit postings are permitted. Customer-induced debits are blocked (3.1.20-ii).
  - **Reactivation / Normalization:** First deposit/withdrawal on a dormant account triggers a mandatory authorization prompt to the Branch Manager (Role-based) before unmarking dormancy (3.1.20-iii, iv).
  - **Accrual:** Interest must continue to accrue and capitalize normally while dormant (3.1.20-v).
- **Rule 2.2 (10-Year Inoperative / Unclaimed Deposit Rule):**
  - If an account remains inoperative for 10 years (configurable), the system must generate statutory notices (minimum 3 notices served) and automatically transfer the remaining balance to Bangladesh Bank as unclaimed deposit (3.1.21-iv).
- **Rule 2.3 (Deceased Customer Account Control):**
  - Upon receiving official notification/death certificate, account status changes to **Deceased**.
  - **Financial Lock:** The system must freeze all outgoing debits, stop fee/maintenance charge deductions, and permit credit postings only under dual-control audit logging (3.1.21-v).

---

## 3. Account Restraints, Lien & Hovering Deduction
- **Rule 3.1 (Multi-Tier Restraints):**
  - The system must support selective freezing:
    1. Freeze Customer (all accounts under Cust ID)
    2. Freeze Account (single 13-digit account)
    3. Restrain Debit only
    4. Restrain Credit only
    5. Restrain specific channels (e.g. ATM/POS/Web blocked, counter allowed) (3.1.15).
  - Amount blocking must function irrespective of whether the current balance is sufficient to cover the blocked amount (3.1.15-x).
- **Rule 3.2 (Automated Hovering Charge Recovery):**
  - If an account balance is insufficient during fee, tax, or excise duty deduction, the system must create an unrealized balance hold and activate **hovering logic** to automatically deduct the outstanding amount immediately upon future credit availability (3.1.19-viii, 3.2.3-vi).

---

## 4. Taxation & Regulatory Fees (NBR / BB Rules)
- **Rule 4.1 (Source Tax / Withholding Tax):**
  - Source tax on interest/profit must be automatically deducted at the exact timestamp of interest payment/capitalization as per current NBR tax brackets (3.1.19-ii).
  - Tax rates must dynamically distinguish between TIN-verified and non-TIN accountholders.
- **Rule 4.2 (Excise Duty):**
  - System must execute annual/periodic excise duty assessment on the highest credit balance slab during the calendar year in compliance with NBR schedules (3.1.19-iii).
- **Rule 4.3 (Account Maintenance Fees):**
  - Recovered half-yearly (June & December) based on average deposit balance slabs (3.2.3-ii, 3.3.4-i).
  - Term deposits and special regulatory schemes are exempt from closing charges and maintenance fees (3.1.19-iv).

---

## 5. Interest & Profit Calculation Formulas
- **Rule 5.1 (Savings Account Interest):**
  - Interest is calculated on daily product or monthly minimum balance basis (5th to last day of the month) and compounded half-yearly in June and December (3.2.2-i).
  - Accounting Entries:
    - **Month-end:** `Dr - P&L (Interest Expense) | Cr - Interest Accrual`
    - **Half-year end:** `Dr - Interest Accrual | Cr - Customer Savings Account`
- **Rule 5.2 (Special Notice Deposit - SND / MSNTD):**
  - Calculated on daily product basis using standard banking formula:
    $$\text{Interest} = \frac{\text{Daily Balance} \times \text{No. of Days} \times \text{Rate}}{360 \times 100}$$
  - A 7-day prior notice is mandatory for fund withdrawal. If notice is not registered, interest for the respective month must be automatically forfeited (3.4.1-ii, iii).
- **Rule 5.3 (Fixed Deposit Receipt - FDR):**
  - Term: 360-day product basis with simple interest.
  - Auto-renewal: In the absence of disposal instructions, principal or principal+interest renews automatically at prevailing market rates; auto-capitalized upon renewal (3.5.5, 3.5.10).
  - No renewal is permitted after verified notice of depositor's demise (3.5.4-iii).
  - FDR transfer across individuals is strictly prohibited (3.5.12).
- **Rule 5.4 (DPS Lien Lock):**
  - When an overdraft (OD) or loan is issued against a Deposit Pension Scheme (DPS) or FDR, the system must place an automated lien. On maturity or premature closure, proceeds cannot be released to linked accounts until the loan liability is fully adjusted (3.5.13, 3.8.10).
