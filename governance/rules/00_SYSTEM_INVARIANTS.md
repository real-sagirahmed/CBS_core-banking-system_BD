# Core Banking System (CBS) - Global System Invariants & Architectural Decision Locks

> **Regulatory Grounding:** Bangladesh Bank BRPD Circular No. 12 (13 May 2024)  
> **Status:** LOCKED & MANDATORY (Anti-Regression & Anti-Hallucination)  
> **Scope:** Entire Core Banking Solution (All Modules)

---

## 1. 13-Digit Account Number Format
- **Circular Reference:** Module 1 (1.1.1-i, 1.1.1-ii) & Module 3 (3.1.8-i)
- **Mandate:**
  1. Every customer account number in the CBS must be exactly **13 numeric digits** or deterministically mapped to 13 digits.
  2. The account number structure must incorporate a **check digit** (e.g., Modulus 11 or Luhn) to guarantee numerical integrity.
  3. Branch Code (SOL) and Product Code must be modularly tagged and transferable without altering the immutable customer identity.
- **In-Code Intent Lock:**
  ```csharp
  // ARCHITECTURAL INTENT: Account number length is strictly locked to 13 numeric digits with check-digit validation.
  // DO NOT alter length, allow alpha characters, or bypass check-digit verification as per BRPD Circular 12/2024 (1.1.1-ii, 3.1.8-i).
  ```

---

## 2. Strict Double-Entry Self-Balancing General Ledger
- **Circular Reference:** Module 1 (1.3.1-i, 1.3.5-i, 1.3.13-i, 1.3.13-ii, 1.6.5-i)
- **Mandate:**
  1. System must enforce double-entry accounting standards for all inter- and intra-bank transactions.
  2. For every financial voucher and transaction batch:
     $$\sum \text{Debits (Base CCY)} = \sum \text{Credits (Base CCY)}$$
  3. The system shall raise an override flag at entry generation if a balance mismatch occurs and **prohibit batch closure**.
  4. Tellers and systems are strictly blocked from submitting unbalanced transactions to authorizers.
- **In-Code Intent Lock:**
  ```csharp
  // ARCHITECTURAL INTENT: Zero tolerance for unbalanced vouchers. Sum(Debits) must equal Sum(Credits) in base currency.
  // DO NOT allow single-legged postings or override flags to bypass contra balance checks (BRPD 1.3.5, 1.3.13).
  ```

---

## 3. Immutability of Authorized Ledger & Reversals Only
- **Circular Reference:** Module 1 (1.3.6-i, 1.3.6-ii, 1.3.12-i, 1.3.12-ii)
- **Mandate:**
  1. Once authorized, a voucher or ledger transaction **can never be edited, modified, or deleted** (no SQL `UPDATE` or `DELETE` on financial entries).
  2. All corrections must be performed exclusively via system auto-generated **Reversal Entries** that explicitly reference the original transaction identifier with full audit logging.
  3. Reversals must systematically roll back all related entries (charges, commission, VAT, withholding tax) with back-value date adjustment capabilities.
- **In-Code Intent Lock:**
  ```csharp
  // ARCHITECTURAL INTENT: Authorized ledger entries are immutable. Database UPDATE and DELETE are prohibited.
  // Corrections MUST use explicit Reversal Entries referencing the original voucher ID (BRPD 1.3.12, 1.3.6).
  ```

---

## 4. Real-Time Maker-Checker (4-Eyes Principle)
- **Circular Reference:** Module 1 (1.3.18-i, 1.3.19-i, 1.7.1-ii), Module 9 (9.1.1-xii, xix), Module 10 (10.3.5-x)
- **Mandate:**
  1. Maker and Checker separation of duties is strictly enforced on a real-time basis:
     $$\text{MakerUserId} \neq \text{CheckerUserId}$$
  2. Single transactions or daily cumulative amounts exceeding a user's Posting Limit must be automatically pushed to the supervisor queue for authorization.
  3. **Super-Admin Isolation:** System Administrators and Super-Admin users in CBS are strictly **forbidden from performing financial transactions** (BRPD 10.3.5-vii).
- **In-Code Intent Lock:**
  ```csharp
  // ARCHITECTURAL INTENT: Separation of duties (4-eyes principle) is strictly enforced.
  // Maker cannot approve own transaction. System Admins cannot execute financial transactions (BRPD 1.3.18, 10.3.5-vii).
  ```

---

## 5. Security & Zero Direct Database Access
- **Circular Reference:** Module 9 (9.1.1-xiii, 9.1.1-xiv, 9.1.1-xxiii)
- **Mandate:**
  1. Frontend application users must never possess direct database access credentials.
  2. CBS application frontends are strictly prohibited from executing DDL operations (creating, dropping, modifying database objects).
  3. All database interactions must occur through authenticated, role-constrained application services with immutable audit logging.
  4. Passwords must be reset at least every 30 days and maintain at least 4 past password histories (BRPD 10.1.3).
  5. Multi-Factor Authentication (MFA/2FA) is mandatory for system login and channel administration (BRPD 1.1.2-xv, 9.1.1-xxi).

---

## 6. Financial Precision Standard
- **Mandate:**
  1. Monetary values must always use fixed-point `decimal` data types (C# `decimal`, SQL `DECIMAL(18, 4)` or `DECIMAL(19, 4)`).
  2. Floating-point types (`float`, `double`) are banned in financial calculations to eliminate IEEE 754 precision loss.
  3. Every transaction must carry both original transaction currency (`CCY`) and base currency equivalent (`LCY`).

---

## 7. Shariah Compliance & Segregation
- **Circular Reference:** Module I (Introduction), Module 1 (1.2.2-vii), Module 3 (3.2), Module 4 (4.3)
- **Mandate:**
  1. Complete segregation of funds, accounts, and ledgers between Conventional and Islamic Banking operations.
  2. In Islamic Banking, Quard contracts allow zero interest or profit. Bai-Murabaha disbursement must be backed by actual purchase-and-sale vouchers (no cash credit transfers to customer accounts without goods delivery proof).
