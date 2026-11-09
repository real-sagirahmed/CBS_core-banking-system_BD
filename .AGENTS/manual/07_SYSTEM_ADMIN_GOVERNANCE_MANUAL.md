# System Administration, IAM Governance & Security Audit Manual

> **Regulatory Grounding:** Bangladesh Bank BRPD Circular No. 12 (13 May 2024) — Module 9 & Module 10  
> **Target Audience:** CBS System Administrators, Database Administrators, CISO / IT Security Teams, IS Auditors

---

## 1. User Account Lifecycle & IAM Provisioning
1. **User Onboarding Protocol:**
   - User creation requires an approved **User Authorization Form** signed by the Head of Human Resources and Head of Branch/Department (10.1.2, 10.2.1-iii).
   - Mandatory captured user metadata (10.1.4):
     - Unique User ID and verified Employee ID.
     - Allocated Home Branch / Service Outlet (SOL).
     - User Classification (`HO User`, `Branch Manager`, `Supervisor`, `Officer`, `Teller`).
     - Allowed IP Terminal Whitelist and Authorized Operating Hours (10.1.4-j).
2. **Dual-Control for User Administration:**
   - **MANDATORY CONTROL:** Creation, modification, role assignment, or status changes (activate/deactivate) must execute under Maker-Checker dual control (10.3.5-x).
3. **User Transfer & Offboarding:**
   - When an employee is transferred, system immediately revokes branch-specific transaction limits and rebinds the user to the destination branch code (10.2.1-i, 10.2.3).
   - Upon resignation or termination, the user ID must be locked immediately with zero delay (10.1.2).

---

## 2. Password Governance & Session Security
1. **Password Enforcement Rules (10.1.3):**
   - **Mandatory 30-Day Reset:** CBS forces password change every 30 days.
   - **Password History Lock:** System rejects any password matching the user's **last 4 previous passwords**.
   - **First-Login Force Change:** Default temporary credentials must be changed immediately upon first authentication.
2. **Session Concurrency & Inactivity Controls:**
   - **Single Active Session:** CBS actively blocks concurrent logins initiated under the same user ID (10.3.5-viii).
   - **Inactivity Logoff:** Terminal sessions terminate automatically after a configured inactivity window as per Bangladesh Bank ICT Security Guidelines (7.2.1-xiv).
   - **Login Security Feedback:** Present previous login timestamps (both successful and failed attempts) on the user dashboard upon every successful authentication (9.1.1-xviii).

---

## 3. Delegation of Authority (DOA) & Fallback Rules
1. **Granular Transaction Limits:**
   - Configure role-wise and currency-wise limits for each user (10.2.3):
     - Local Cash Credit / Debit Limit
     - Inter-Branch Clearing & Transfer Limit
     - General Ledger (GL/P&L) Posting Limit.
2. **Emergency Delegation & Absence Fallback:**
   - When a branch authorizer is on leave, temporary delegation of approval authority can be scheduled with automated expiration dates (10.2.4, 10.2.5).
   - System records the delegator, delegatee, and justification in the administrative audit log.

---

## 4. Parameter Maintenance & Holiday Calendar Setup
1. **Bank Master Parameters:**
   - Maintain Base Currency (BDT), allowed foreign currencies, and daily Spot/Card exchange rate tables (10.3.2).
2. **Yearly Calendar & Operational Holidays:**
   - Configure weekly bank holidays and public holidays in advance.
   - Ensure calendar dates synchronize seamlessly with Bangladesh Bank clearing networks (BACH, BEFTN, RTGS, NPSB) (1.5.5-vii, 10.3.2-c).

---

## 5. Audit Trail Journals & VAPT Readiness
1. **Mandatory Journal Reports (10.3.6):**
   - The system operator must preserve and review the following daily audit reports:
     - User Profile Management Activity Journal
     - Financial Transaction Journal (all maker/checker timestamps and IP addresses)
     - Transaction Overrides & Exception Journal
     - Parameter & Configuration Change Journal
     - Sensitive Account Enquiry Usage Security Journal.
2. **Annual IS Audit & CAATs Verification (9.1.2):**
   - Execute internal Information Systems (IS) audits at least once annually using Computer-Assisted Auditing Tools (CAATs) led by certified auditors (CISA).
   - Facilitate mandatory external CBS audits and track remediation of audit findings within stipulated timelines (9.1.2-v, vii).
3. **Vulnerability Assessment & Penetration Testing (VAPT) (9.1.3):**
   - Conduct bi-annual internal and external VAPT on the core CBS and all digital delivery channels (Internet banking, mobile APIs).
   - Execute tests in an isolated staging environment whose architecture, operating system patch levels, and application code mirror live production (9.1.3-iii).
