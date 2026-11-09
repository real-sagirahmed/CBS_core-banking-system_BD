# Standard Operating Procedure (SOP): Branch & Teller Operations

> **Regulatory Grounding:** Bangladesh Bank BRPD Circular No. 12 (13 May 2024) — Module 1 (1.5, 1.6, 1.7) & Module 3 (3.10–3.13)  
> **Target Audience:** Branch Managers, Operation Managers, Head Tellers, Cashiers, Customer Service Officers

---

## 1. Daily Branch Opening & Cash Counter Initialization
1. **System Branch Open:** The Operation Manager / Branch Supervisor executes the Beginning of Day (BOD) branch open procedure after verifying calendar status and holiday schedules (1.1.2-iv, xii).
2. **Vault to Teller Cash Transfer:**
   - Head Teller retrieves currency bundles from the dual-custody physical vault.
   - The transfer from Vault General Ledger (`GL - Vault Cash`) to individual Cashier Cash Drawer (`GL - Teller Cash`) is entered in CBS with exact **denomination breakdown** (1.7.1-iii, 1.7.2).
   - Cashier accepts the custody in the CBS queue to establish personal cash holding responsibility.

---

## 2. Cash Deposit Workflow
1. **Slip Receipt & Verification:** Cashier receives cash deposit slip and validates the **13-digit customer account number**, account title, and currency (1.1.1-ii, 3.1.8-i).
2. **Account Status Inspection:** CBS screen displays real-time status indicators (3.10.2-ii):
   - Active, Dormant, Frozen, Lien, Court Order, No-Credit flag.
   - If account is **Dormant**, system blocks deposit until Branch Manager authorizes status normalization (3.1.20-iii, iv).
3. **Transaction Profile (TP) Check:**
   - CBS checks the customer's monthly declared Transaction Profile (TP).
   - If the deposit exceeds the monthly limit, CBS prompts an alert and transfers the transaction to the Supervisor queue for Maker-Checker authorization (1.7.1-x, 1.7.9).
4. **BFIU Walk-In Customer Due Diligence:**
   - For walk-in deposits above BFIU threshold limits, cashier must record: Full Name, NID/Photo ID Number, Mobile Number, and Source of Fund (1.7.4-i, 3.10.5-i).
   - Instant UNSCR & local sanction screening executes automatically before transaction commitment (1.7.8, 3.10.5-ii).
5. **Denomination Entry & Receipt Generation:** Cashier inputs denomination counts, saves voucher, and prints system-generated receipt with unique transaction reference (1.7.1-i, 1.7.5).

---

## 3. Cash Withdrawal Workflow
1. **Instrument Verification:** Cashier receives customer cheque, withdrawal slip, or debit voucher (1.7.1-vi).
2. **Signature & Photo Verification:**
   - Cashier toggles the CBS signature card to inspect digital specimen signatures and customer photograph (1.7.6, 8.1.12).
   - For joint/corporate accounts, verify that operating instructions (`Joint`, `Either or Survivor`, `Any`) match signatories (3.1.5, 8.1.12-d).
3. **Cheque Stock & Status Validation:**
   - System verifies that the presented leaf belongs to the issued cheque series for that specific account and has not been marked as `Paid`, `Stopped`, or `Cancelled` (1.5.7-iii, x, xii).
4. **Balance & Limit Check:**
   - System checks clear available balance (excluding holds/liens).
   - If withdrawal exceeds cashier's authorized posting limit, transaction routes automatically to supervisor queue for authorization (1.7.1-ii).
5. **Cash Disbursement:** Upon approval, cashier hands over cash, marks instrument as paid in CBS, and archives physical leaf.

---

## 4. Payment Order (PO) & Demand Draft (DD) Issuance
1. **Requisition Entry:**
   - Enter Payee Name, Amount, Remitter Name, and Purpose (3.11.1-ii, 3.12.1-ii).
   - Run automated UNSCR / local sanction screening on both remitter and beneficiary (3.11.1-viii, 3.12.1-vi).
2. **Fee & VAT Calculation:** System auto-computes tiered commission and 15% VAT, deducting them separately from the source account (1.5.6-v, 3.11.1-i).
3. **Instrument Printing:** Print PO/DD leaf using pre-numbered security stationery tracked under joint custodian inventory (3.11.1-iii, iv).
4. **Liquidation & Cancellation:**
   - Liquidation against beneficiary account verifies instrument number and payee authentication (2.1.1-xiii, 2.1.2-xiii).
   - Cancellation requires original instrument return or verified duplicate issuance procedures with maker-checker approval (2.1.1-ii, iii, 2.1.2-iv, v).

---

## 5. End of Day Teller Balancing & Drawer Close
1. **Physical Cash Balancing:** Cashier counts physical cash by denomination and matches against the CBS Cashier Summary Report.
2. **Discrepancy Resolution:** Any cash shortage or excess must immediately be reported to the Operation Manager and booked into Cash Shortage/Excess Suspense Accounts under dual control.
3. **Cash Handover to Vault:** Cashier transfers balance back to Head Teller vault custody with denomination verification.
4. **Drawer Close:** Cashier closes workstation queue, ensuring zero unauthorized or pending transactions remain (1.3.15-i).
