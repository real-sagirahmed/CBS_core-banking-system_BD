# Operational Manual: Customer Onboarding, e-KYC & Profile Management

> **Regulatory Grounding:** Bangladesh Bank BRPD Circular No. 12 (13 May 2024) — Module 8 & Module 3  
> **Target Audience:** Branch Customer Service Officers, Account Opening Desks, Branch Managers, AML/CFT Compliance Officers

---

## 1. Single Customer View & Deduplication Protocol
1. **Deduplication Check (Mandatory Pre-requisite):**
   - Before capturing new customer data, the user must execute a **Dedupe Query** in the CBS using National ID (NID), Passport Number, Date of Birth, and Father's Name (3.1.15-iii, 8.1.1-d).
   - If an existing Customer Information File (Cust ID) is located, the system blocks the creation of a duplicate Cust ID. A customer may possess multiple deposit and loan accounts, but must be represented by **exactly one unique Customer ID** bank-wide (8.1.1-a, b).
2. **Corporate Group Linking:**
   - For enterprise accounts and business conglomerates, link related individual/corporate IDs under a parent **Group ID** to monitor group-level credit exposure and beneficial ownership (8.1.1-c, 8.1.18-d).

---

## 2. Onboarding Workflow & Election Commission (EC) Verification
1. **e-KYC Identification via Election Commission:**
   - In accordance with Bangladesh Bank e-KYC guidelines, input the applicant's 10-digit Smart NID or 17-digit NID with Date of Birth.
   - The CBS triggers an automated API call to the Bangladesh Election Commission (EC) server to retrieve verified demographic data and official photo (8.1.1-e, 8.1.13-a).
2. **Customer Information Capture:**
   - Verify and capture mandatory attributes (8.1.2, 8.1.3):
     - Full Name, Gender, DOB, Place of Birth (District), Country of Birth Code.
     - Father's, Mother's, and Spouse's Name.
     - Permanent & Present Address with District, Upazila/Thana, and Postal Code.
     - Mobile Number (for 2FA/OTP transaction alerts) and Email Address.
3. **Tax & Profession Verification:**
   - TIN Verification: Input 12-digit e-TIN, Tax Circle, Tax Zone, and attach last tax return submission acknowledgment (8.1.4).
   - Profession & Sector Code: Map customer activity to the official Bangladesh Bank Sector Code table (8.1.5-b).
4. **Customer Due Diligence (CDD) & Source of Funds:**
   - Record declared source of funds (Salary, Business, Remittance, Asset Sale) with monthly inflow/outflow expectations to establish the **Transaction Profile (TP)** (8.1.6).

---

## 3. Risk Assessment, PEP & Sanction Screening
1. **Automated Sanction Screening (UNSCR & Local):**
   - The CBS runs real-time screening against the United Nations Security Council Resolution (UNSCR) consolidated list and Bangladesh domestic sanction lists (8.1.14-a).
   - If a positive sanction hit occurs:
     - The profile creation is immediately locked.
     - An alert routes to the Principal Anti-Money Laundering Compliance Officer (CAMLCO) via the AML escalation matrix (8.1.14-c).
2. **PEP, IP & High-Risk Profiling:**
   - System screens for Politically Exposed Persons (PEPs), Heads of International Organizations, Influential Persons (IPs), and their family members / close associates (8.1.13-d, e, f).
   - PEP accounts require Senior Management approval prior to account operationalization.
3. **FATCA (Foreign Account Tax Compliance Act) Identification:**
   - Screen for US Indicia: US Citizenship, US Green Card status, US birthplace, US residential address or telephone number, and Power of Attorney granted to a US person (8.1.15).
   - If positive, collect and record IRS Form W-9 / W-8BEN.

---

## 4. Digital Signature & Specimen Capture
1. **Biometric & Signature Ingestion:**
   - Capture applicant's specimen signature and live photograph using flatbed scanner or biometric pad (8.1.12-a, b).
   - Signatures are stored in the central database in an **encrypted and compressed format** (8.1.12-c, f).
2. **Multi-Signatory & Operating Instructions:**
   - For joint, partnership, and corporate accounts, capture all authorized signatories with their individual photos, designation, and mandate rules (`Singular`, `Jointly by Any Two`, etc.) (8.1.12-d, e).
   - Signatures must render on teller screens account-wise during counter transactions (8.1.12-e).

---

## 5. Account Lifecycle Management
1. **Minor Account Transition (At Age 18):**
   - When an accountholder reaches 18 years of age, CBS generates a system prompt at EOD.
   - Branch officer contacts customer, collects adult NID and independent signature card, and removes guardian proxy status (3.2.1-ii).
2. **Dormant Account Reactivation:**
   - When a dormant accountholder approaches the branch, obtain a written reactivation request with fresh KYC/NID verification.
   - Branch Manager authorizes reactivation in CBS under Maker-Checker dual control before processing any transaction (3.1.20-iii, iv).
3. **Deceased Account Procedure:**
   - Upon receiving official death certificate, mark status as **Deceased**.
   - Debit transactions and fee deductions are halted; pending balances are held for settlement with verified legal nominees or probate court administrators (3.1.21-v).
