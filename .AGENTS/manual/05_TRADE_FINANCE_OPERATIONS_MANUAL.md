# Standard Operating Procedure (SOP): International Trade Finance & TBML Monitoring

> **Regulatory Grounding:** Bangladesh Bank BRPD Circular No. 12 (13 May 2024) — Module 5  
> **Target Audience:** Trade Services Department (TDS), Authorized Dealer (AD) Branches, International Division, Trade Compliance Officers

---

## 1. Import Letter of Credit (DC) Issuance Workflow

```text
[AD Branch: LC Application & Document Vetting]
                     │
                     ▼
[TDS: Sanction Screening, IRC & Credit Limit Validation]
                     │
                     ▼
[CBS: Cash Margin Holding & SWIFT MT700 Generation]
                     │
                     ▼
[SWIFT Gateway: Transmission & ACK / NACK Verification]
                     │
                     ▼
[Bangladesh Bank Web Portal: Online LC Data Reporting]
```

### Step 1: Branch Pre-Vetting & Checklist
- The AD branch receives LC application along with Proforma Invoice (PI) or sales contract, valid Importer Registration Certificate (IRC), Trade License, and Insurance Cover Note (5.1.1-ii, viii, ix, xli).
- Branch Operations Manager completes and signs the Standard Operating Procedure (SOP) checklist with two authorized signatories (5.1.6-i).

### Step 2: TDS Centralized Verification & Limit Check
- Trade Services Department (TDS) verifies:
  1. **Sanction Screening:** Instant UNSCR and local sanction search on applicant, beneficiary, and advising bank (5.0, 5.1.1-xxvii).
  2. **CIB & Defaulter Check:** Automatic verification that the importer is not a loan defaulter (5.1.1-xlii, xlviii).
  3. **Credit Limit Line:** Check that operative LC limit line is active, non-expired, and sufficient. The CBS **strictly blocks drafting or authorizing an LC without an approved limit** (5.1.7-i).
  4. **Commodity & Regulatory Compliance:** Verify that goods are permissible under the prevailing Import Policy Order (IPO) and confirm if Pre-Shipment Inspection (PSI) is mandatory for the specified HS Code (e.g. HS Code 7207) (5.1.1-xliii, xliv).

### Step 3: Margin Holding & SWIFT MT700 Transmission
- CBS automatically debits customer account and deposits collateral into `GL - Marginal Deposit` (5.1.1-xiii).
- Maker drafts the LC data including all mandatory clauses and Incoterms (5.1.1-xxxvi).
- Upon Checker authorization, CBS formats and routes the SWIFT MT700 message to the SWIFT communication server (5.1.8-i).
- System tracks delivery status (`ACK`, `NACK`, `REPAIR`). Data is pushed synchronously to the Bangladesh Bank Online Import Monitoring Portal (5.1.8-i).

---

## 2. Shipping Guarantee (SG) & Bill Settlement
1. **Shipping Guarantee Issuance:**
   - When cargo arrives before shipping documents, issue an SG reference tagged to the underlying Import DC (5.1.2-i, ii).
   - Transfer 100% margin in local currency and auto-generate the official No Objection Certificate (NOC) for Port and Customs authorities (5.1.2-iii, v).
2. **Document Arrival & SG Retirement:**
   - Upon receipt of original transport documents (Bill of Lading / Airway Bill), cancel the active SG in CBS and release excess collateral (5.1.2-vii).
3. **Bill Lodgment & UCP 600 Compliance:**
   - Register import bill and Airway Bill number in CBS (5.1.3-i, ii).
   - If documents contain discrepancies, issue an MT734 Refusal Advice within **5 banking days** in strict compliance with UCP 600 rules (5.1.3-vi).
4. **Customs Matching & Retirement:**
   - Match Bill of Entry data (HS Code, Item Description, Bill Value) with Clean Report of Findings (CRF) and commercial invoice (5.1.9-vi).
   - Settle bill via customer deposit, Export Retention Quota (ERQ), or conversion to Post Import Financing (PAD/LTR/EDF) within authorized facility tenor (5.1.4).

---

## 3. Export Operations & 3-Phase EXP Tracking
1. **Export LC Advising & Master Contract Tagging:**
   - Check authenticity of incoming SWIFT MT710/MT700 Export LC and assign a unique **Internal Job Number** to trace all linked export performance (5.2.5-i).
   - For new foreign buyers, obtain an international credit rating agency report before opening Back-to-Back facilities (5.2.5-i).
2. **Back-to-Back (BTB) LC Controls:**
   - **MANDATORY INVARIANTS (5.6.1):**
     $$\text{BTB LC Expiry Date} < \text{Master Export LC Expiry Date}$$
     $$\text{BTB LC Shipment Date} < \text{Master Export LC Shipment Date}$$
   - CBS strictly blocks opening a BTB LC if the Master LC is expired, and prevents closing the Master LC while any BTB liability is open (5.6.1-vii, viii).
3. **3-Phase Electronic EXP Form Lifecycle (5.2.7-i):**
   - **Phase 1 (Issuance):** Register EXP electronically in CBS before cargo customs clearance.
   - **Phase 2 (Document Submission):** Log exporter's submitted shipping documents and cross-check against EXP certified values.
   - **Phase 3 (Proceeds Realization):** Reconcile proceeds upon foreign remittance credit (MT103/MT202/MT910).
4. **Overdue Export Proceeds Monitoring:**
   - Export proceeds must be realized within **4 months** of shipment. Any unrealized bill beyond 4 months must be flagged as **Overdue** and reported to Bangladesh Bank (5.2.10-i).
   - Issue Proceeds Realization Certificate (PRC) immediately upon realization. Notify client that export cash subsidy claims must be submitted within **6 months** of PRC issuance (5.2.10-i).

---

## 4. Trade Based Money Laundering (TBML) Monitoring
1. **Trade Transaction Profile (TTP) Check:** Compare trade volume, frequency, and invoice value against customer's approved TTP (5.7.1).
2. **Price Verification Template:** Verify unit prices of imported/exported commodities against international price bulletin benchmarks to prevent over-invoicing or under-invoicing capital flight (5.7.6).
3. **Vessel Tracking:** Verify transport logistics via integrated vessel tracking feeds to detect illicit ship-to-ship transfers or unauthorized calls at sanctioned ports (5.7.5).
4. **Level-3 Escalation:** Route unverified transactions involving high-risk tax havens, landlocked destinations, or round-sum pricing to the Head of Trade Compliance for BFIU SAR/STR reporting (5.7.7, 5.7.8).
