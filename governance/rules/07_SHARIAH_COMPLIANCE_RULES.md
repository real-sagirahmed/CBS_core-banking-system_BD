# Core Banking System (CBS) - Islamic Banking & Shariah Compliance Rules

> **Regulatory Grounding:** Bangladesh Bank BRPD Circular No. 12 (13 May 2024) — Shariah Directives  
> **Status:** MANDATORY FOR ISLAMIC BANKING OPERATIONS  
> **Target Subsystems:** Mudaraba Deposits, Al-Wadiah, Bai-Murabaha, Quard, Shariah Ledger

---

## 1. Absolute Segregation of Islamic & Conventional Funds
- **Rule 1.1 (Ledger Isolation):**
  - For full-fledged Islamic banks and Islamic banking branches/windows of conventional banks, the CBS must enforce complete accounting segregation of funds.
  - Islamic deposit pools must never co-mingle with interest-bearing conventional deposit liabilities or interbank call money borrowings (Introduction, 1.2.2-vii).

---

## 2. Deposit Products (Mudaraba & Al-Wadiah)
- **Rule 2.1 (Al-Wadiah Current Accounts):**
  - Operates on the contract of safe-custody (*Amanah*). The bank guarantees principal return with zero interest, profit, or dividend promise (3.3.1, 3.3.3).
- **Rule 2.2 (Mudaraba Deposits & Weightage-Based Profit Sharing):**
  - Mudaraba Savings, MSNTD, and MTDR profit distribution must strictly follow Bangladesh Bank Shariah guidelines:
    $$\text{Customer Profit Share} = \text{Mudaraba Pool Distributable Profit} \times \text{Product Weightage} \times \text{Average Balance}$$
  - Declaring a fixed, predetermined return rate at account inception is strictly prohibited. The system must accommodate monthly/quarterly provisional profit declaration followed by final year-end equalization (1.2.2-vii, 3.2.2-ii).

---

## 3. Investment & Asset Financing Invariants
- **Rule 3.1 (Murabaha Goods Purchase Verification):**
  - **Inviolable Shariah Invariant:** Transferring disbursed loan funds directly into a client's Current Account is a **severe Shariah violation**, as money itself cannot be traded for money at a profit (4.1.5-iv).
  - Disbursements under Bai-Murabaha must be paid directly to the verified third-party vendor/supplier of goods via **Pay Order** or direct settlement upon physical inspection or proof of delivery (4.1.5-iv).
- **Rule 3.2 (Quard Financing Rules):**
  - Under Quard contracts (QTDR, QHG, QPF, QBF, QACA), the bank may only recover documented actual operational fees (4.3.10).
  - Any interest, mark-up, or profit deduction is locked to **zero** (4.3.4-i).

---

## 4. Default, Arrears & Compensation Treatment
- **Rule 4.1 (Prohibition of Penal Interest into Bank Income):**
  - In Islamic banking, conventional penal interest is prohibited. Overdue dues accrue financial penalties known strictly as **Compensation** (*Ta'widh*) (4.1.9-iii).
  - **Income Exclusion Invariant:** Because compensation cannot be treated as bank earnings under Shariah law, the system is strictly blocked from booking compensation into P&L Income.
  - Accounting Entry:
    $$\text{Dr - Customer Due} \quad | \quad \text{Cr - Shariah Compensation / Suspense Account}$$
  - Compensation balances must be routed to approved social welfare / charitable purposes under Central Bank supervision (4.1.9-iii).
