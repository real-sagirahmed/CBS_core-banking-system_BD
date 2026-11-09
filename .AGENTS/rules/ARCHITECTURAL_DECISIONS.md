# Core Banking System (CBS) - Architectural Decisions & Invariant Locks

> **Status:** APPROVED & LOCKED  
> **Applicable Standard:** Bangladesh Bank BRPD Circular No. 12 (May 13, 2024)  
> **System Scope:** Scheduled Commercial & Islamic Banking Operations in Bangladesh  

---

## 1. Architectural Style & Boundaries

### Decision 1: Modular Monolith over Microservices
- **Decision:** The core system is architected as a **Modular Monolith** using Clean Architecture principles within a single deployable unit / cluster, rather than distributed microservices for core ledger operations.
- **Why:** 
  - Banking accounting mandates strict ACID guarantees. Distributed transactions (2PC or Saga rollbacks) across network partitions risk split-brain ledger states and reconciliation overhead.
  - Inter-branch and internal accounts must be updated within atomic database transactions.
  - Bounded contexts are separated at the project/assembly boundary (`GeneralBanking`, `Deposits`, `Lending`, `TradeFinance`, `Treasury`, `CRM`, `Security`).
- **What is Locked:** No distributed messaging or eventual consistency between the Ledger and Account balances.

---

## 2. Core Invariants & Decision Locks (Anti-Regression)

### Decision Lock 2.1: 13-Digit Account Number Format
- **BRPD Reference:** Module 1 (1.1.1-ii) & Module 3 (3.1.8-i)
- **Rule:** Every customer account number must be exactly 13 digits (or mapped strictly to 13 digits) using a deterministic check-digit algorithm (Modulus 11 or Luhn).
- **In-Code Intent Lock:**
  ```csharp
  // ARCHITECTURAL INTENT: Account number length is strictly locked to 13 numeric digits with check-digit validation.
  // DO NOT alter length, allow non-digit characters, or bypass check-digit verification as per BRPD Circular 12/2024 (1.1.1-ii, 3.1.8-i).
  ```
- **Test Lock:** `AccountNumber_Must_Be_Exactly_13_Digits_And_Valid_CheckDigit`

---

### Decision Lock 2.2: Strict Double-Entry Self-Balancing Ledger
- **BRPD Reference:** Module 1 (1.3.5-i, 1.3.13-i, 1.3.13-ii)
- **Rule:** A financial voucher or batch CANNOT be saved, posted, or authorized if `Sum(Debits) != Sum(Credits)`. Tellers and API gateways are physically prohibited from forwarding unbalanced vouchers to authorizers.
- **In-Code Intent Lock:**
  ```csharp
  // ARCHITECTURAL INTENT: Zero tolerance for unbalanced vouchers. Sum(Debits) must equal Sum(Credits) in base currency.
  // DO NOT allow single-legged postings or override flags to bypass contra balance balance checks (BRPD 1.3.5, 1.3.13).
  ```
- **Test Lock:** `Voucher_Posting_Must_Throw_When_Debits_Do_Not_Equal_Credits`

---

### Decision Lock 2.3: Immutable Ledger & Reversal Only
- **BRPD Reference:** Module 1 (1.3.12-i, 1.3.6-i)
- **Rule:** No authorized voucher, transaction record, or ledger balance may be updated (SQL `UPDATE`) or deleted (SQL `DELETE`). Any correction MUST be executed through a new auto-generated Reversal Entry with linked reference and audit log.
- **In-Code Intent Lock:**
  ```csharp
  // ARCHITECTURAL INTENT: Authorized ledger entries are immutable. Database UPDATE and DELETE are prohibited.
  // Corrections MUST use explicit Reversal Entries referencing the original voucher ID (BRPD 1.3.12, 1.3.6).
  ```

---

### Decision Lock 2.4: Real-Time Maker-Checker (4-Eyes Principle)
- **BRPD Reference:** Module 1 (1.3.18-i), Module 9 (9.1.1-xii, xix), Module 10 (10.3.5-x)
- **Rule:** The user who initiates (Maker) a financial transaction, parameter alteration, back-value entry, or limit change CANNOT authorize (Checker) the same transaction (`MakerUserId != CheckerUserId`).
- **Super-Admin Isolation:** Admin/Super-Admin users are strictly prohibited from performing or authorizing financial transactions (BRPD 10.3.5-vii).
- **In-Code Intent Lock:**
  ```csharp
  // ARCHITECTURAL INTENT: Separation of duties (4-eyes principle) is strictly enforced.
  // Maker cannot approve own transaction. System Admins cannot execute financial transactions (BRPD 1.3.18, 10.3.5-vii).
  ```
- **Test Lock:** `Checker_Cannot_Be_Same_As_Maker_Throws_Exception`

---

### Decision Lock 2.5: Financial Precision Standard
- **Rule:** All monetary amounts MUST use C# `decimal` and SQL `DECIMAL(18, 4)` or `DECIMAL(19, 4)` with explicit ISO 4217 Currency Code.
- **Float/Double Ban:** Never use `float` or `double` for currency calculations to prevent binary floating-point rounding errors.

---

### Decision Lock 2.6: Zero Direct Database Access from Frontend
- **BRPD Reference:** Module 9 (9.1.1-xiii, xxiii)
- **Rule:** Application tiers and frontend users have zero direct database access. Database user accounts must have least privilege; no DDL or direct schema mutations from application runtime.
