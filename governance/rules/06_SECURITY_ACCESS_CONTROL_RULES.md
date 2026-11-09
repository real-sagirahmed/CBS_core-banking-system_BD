# Core Banking System (CBS) - Information Security & Access Control Rules

> **Regulatory Grounding:** Bangladesh Bank BRPD Circular No. 12 (13 May 2024) — Module 9 & Module 10  
> **Status:** MANDATORY SECURITY INVARIANTS  
> **Target Subsystems:** IAM, RBAC, Database Security, Cryptography, VAPT, Session Management

---

## 1. Role-Based Access Control (RBAC) & Segregation of Duties (SoD)
- **Rule 1.1 (Least Privilege Principle):**
  - Users are provisioned with the strict minimum menu options, transaction limits, and functional privileges required for their assigned organizational role (9.1.1-xix, 10.1.6).
- **Rule 1.2 (Super-Admin Financial Transaction Ban):**
  - **Inviolable Security Invariant:** System Administrators and Super-Admin accounts in the CBS are **strictly prohibited from creating, approving, or posting financial transactions** (10.3.5-vii).
- **Rule 1.3 (Call Center Operational Barrier):**
  - Bank call center personnel and customer support agents are **strictly barred from executing transactions**. Their interface is strictly constrained to read-only view access with masked sensitive data (9.1.1-xxv, 7.2.1-xix).
- **Rule 1.4 (Dual Control for Administrative Actions):**
  - Any administrative modification—including user creation, role assignment, privilege escalation, parameter tuning, or system configuration—mandates dual authorization (Maker-Checker) (10.3.5-x).

---

## 2. Password & Authentication Invariants
- **Rule 2.1 (Password Lifecycle Standards):**
  - **30-Day Expiry:** User passwords must expire and be reset at least once every **30 days** (10.1.3-vi).
  - **Password History:** The system must record and enforce non-reuse of at least the **last 4 previous passwords** (10.1.3-v).
  - **First-Login Enforcement:** Initial or reset passwords must be forced to change upon first login (10.1.3-iv).
  - **Out-of-Band Delivery:** User IDs and temporary credentials must be transmitted via separate, isolated channels (10.1.3-iii).
- **Rule 2.2 (Multi-Factor Authentication - MFA/2FA):**
  - Multi-Factor Authentication (OTP, hardware token, or biometric) is mandatory for all administrative access, branch tellers, and high-privilege operational roles (1.1.2-xv, 9.1.1-xxi).
- **Rule 2.3 (Session Control & Inactivity Termination):**
  - **Single Active Session:** The system must terminate/block concurrent logins by the same user credential across multiple workstations (10.3.5-viii).
  - **Inactivity Timeout:** Sessions must terminate automatically following a predefined period of inactivity in alignment with Bangladesh Bank ICT Security Guidelines (7.2.1-xiv).
  - **Login Feedback Alert:** Every successful login must present the user with the timestamp and IP address of their previous successful and unsuccessful login attempts (9.1.1-xviii).

---

## 3. Database Security & Perimeter Isolation
- **Rule 3.1 (Zero Direct Database Access from Frontend):**
  - Frontend applications, web clients, and API middleware must never establish direct database connections with DDL privileges (9.1.1-xiii).
  - Application code must never execute dynamic DDL (`CREATE`, `ALTER`, `DROP`, `TRUNCATE`) against database tables or stored procedures (9.1.1-xxiii).
- **Rule 3.2 (Data Masking & API Cryptography):**
  - Payment Card Industry (PCI DSS) and e-KYC standards mandate that National ID, card numbers, and PII data must be masked and encrypted at rest and in transit (7.2.1-iii, 9.1.1-xxix).
  - All communication over public networks and inter-service channels must enforce TLS 1.3 encryption (9.1.1-xxx).

---

## 4. Audit Trail, IS Audit & VAPT Requirements
- **Rule 4.1 (Immutable Journal Reports):**
  - The system must capture an append-only, tamper-proof audit trail for (10.3.6):
    1. User Profile Management and privilege changes.
    2. All financial transactions with workstation IP, maker ID, checker ID, and UTC timestamp.
    3. User parameter overrides and balance mismatch attempts.
    4. Enquiry usage on sensitive customer balances.
    5. Configuration and static parameter updates.
- **Rule 4.2 (Annual Information Systems Audit):**
  - Comprehensive internal IS audits must be conducted at least **once per year** utilizing Computer-Assisted Auditing Tools (CAATs) by certified professionals (CISA/CISSP) (9.1.2).
  - An independent external CBS audit must be executed at least once annually (9.1.2-v).
- **Rule 4.3 (Vulnerability Assessment & Penetration Testing - VAPT):**
  - Periodic internal and external VAPT must be performed against both the CBS core and connected delivery channels (ADC) with test environments mirroring live production topology (9.1.3).
