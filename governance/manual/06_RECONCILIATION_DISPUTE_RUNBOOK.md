# Operational Runbook: Inter-Branch, Nostro & ADC Reconciliation and Dispute Settlement

> **Regulatory Grounding:** Bangladesh Bank BRPD Circular No. 12 (13 May 2024) — Module 1 (1.4), Module 6 (6.4.2), Module 7 (7.2.1)  
> **Target Audience:** Central Reconciliation Department, Nostro Desk, Settlement Officers, ADC Dispute Teams

---

## 1. Automated Inter-Branch & Settlement Reconciliation
1. **Scope:** CBS executes daily automated matching across all inter-branch, inter-office, and central clearing settlement heads (1.4.1-i):
   - Bangladesh Automated Clearing House (BACH - Cheque & BEFTN)
   - Real-Time Gross Settlement (RTGS)
   - National Payment Switch Bangladesh (NPSB)
   - Alternative Delivery Channels (ATM, CDM, CRM, POS, Internet Banking, MFS, Binimoy).
2. **Automated Matching Rules:**
   - One-to-One, One-to-Many, and Many-to-One transaction matching algorithms (6.4.2-ii).
   - Match criteria: Transaction Reference Number, Session Date, Currency, and Net Amount.
3. **Unmatched Items & Aging Reports:**
   - Daily extraction of unmatched ledger items categorized by branch, cost center, and aging bucket (1–7 days, 8–15 days, 16–30 days, >30 days) (1.4.1-ii).
   - Outstanding unreconciled debit balances must carry mandatory accounting provisions in accordance with central bank prudential regulations (1.4.1-iv, 6.4.2-ix-c).

---

## 2. Nostro Mirror Account Daily Reconciliation
1. **Statement File Ingestion:**
   - Import incoming electronic bank statements from foreign correspondent banks via SWIFT MT940 / MT950 (6.4.2-vii).
2. **Local Book vs. Correspondent Book Matching:**
   - Compare CBS Local Nostro Mirror Ledger against external correspondent entries:
     $$\text{CBS Local Book Balance} \longleftrightarrow \text{Correspondent Bank Statement Balance}$$
3. **Variance Rectification:**
   - Investigate uncredited export remittances, unpresented bank drafts, and foreign bank charges.
   - Rectification entries require higher-level supervisor authorization under four-eyes control (6.4.2-iii).

---

## 3. Alternative Delivery Channel (ADC) Dispute Resolution
1. **Trace Number Dispute Tracking:**
   - For failed ATM cash dispensations, CRM deposit timeouts, or NPSB POS debit drops:
   - **MANDATORY CONTROL:** The dispute resolution workflow must utilize the **exact same original system trace audit number (STAN) / RRN** to process adjustments or customer refunds (1.4.1-vii).
2. **Investigation & Rectification Steps:**
   - **Step 1:** Ingest switch transaction log, terminal journal (EJ log), and network settlement file.
   - **Step 2:** If terminal counter confirms cash un-dispensed or switch confirms transaction drop:
     - Generate system rectification voucher crediting the customer's account and debiting the ADC Settlement Bridge GL head (1.4.1-vi, 7.2.1-xx).
   - **Step 3:** Notify customer via automated SMS/email alert confirming dispute settlement (7.2.1-xv).
3. **Audit Trail & Governance:**
   - Maintain channel-wise daily dispute registers, resolution aging metrics, and rectification vouchers for internal audit and Bangladesh Bank inspection (7.2.1-xx).
