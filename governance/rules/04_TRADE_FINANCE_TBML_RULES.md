# Core Banking System (CBS) - Trade Finance & TBML Compliance Rules

> **Regulatory Grounding:** Bangladesh Bank BRPD Circular No. 12 (13 May 2024) — Module 5  
> **Status:** MANDATORY COMPLIANCE  
> **Target Subsystems:** Import DC, Export DC, Back-to-Back LC, Master Contracts, TBML, SWIFT

---

## 1. Sanction Screening & High-Risk Blacklists
- **Rule 1.1 (Pre-Transaction Sanction Mandate):** Real-time UNSCR, OFAC, and domestic Bangladesh sanction screening must execute automatically **prior to opening, amending, or settling any Letter of Credit (LC)**, and before executing any inward/outward remittance (5.0, 5.7.4).
- **Rule 1.2 (Defaulter & CIB Enforcement):**
  - Issuance of an Import Documentary Credit (DC) to any borrower classified as a loan defaulter or carrying an adverse CIB report is strictly prohibited (5.1.1-xlii, xlviii).
  - The system must display a hard blocking warning if any previous import bill is overdue or carries irregular liability (5.1.1-xl).

---

## 2. Master LC and Back-to-Back (BTB) LC Invariants
- **Rule 2.1 (BTB Expiry Date Lock):**
  - The expiry date of a Back-to-Back (BTB) LC must strictly precede the expiry date of the underlying Master Export LC / Sales Contract (5.6.1-v):
    $$\text{BTB LC Expiry Date} < \text{Master Export LC Expiry Date}$$
- **Rule 2.2 (BTB Shipment Date Lock):**
  - The shipment date of a Back-to-Back LC must strictly precede the shipment date of the Master Export LC / Sales Contract (5.6.1-vi):
    $$\text{BTB LC Shipment Date} < \text{Master Export LC Shipment Date}$$
- **Rule 2.3 (Master LC Expiration & Closure Lock):**
  - No new BTB LC may be opened against an expired Master LC / Contract (5.6.1-vii).
  - The system must block the closure of an Export Master LC/Contract if any tagged BTB LC remains active or un-liquidated (5.6.1-viii).
- **Rule 2.4 (Value Addition & Financing Ceiling):**
  - Amendments decreasing the Master LC value must automatically trigger validation to ensure the remaining export margin satisfies the national value addition threshold and total financing ceiling (5.6.1-iii, iv).

---

## 3. Import Lifecycle Controls
- **Rule 3.1 (Documentation & License Validity):**
  - Mandatory import credentials—including Importer Registration Certificate (IRC), Trade License, and Insurance Cover Note—must be valid at LC drafting. If expired, transaction authorization is blocked (5.1.1-viii, ix, xli).
  - HS Code checks must verify if pre-shipment inspection (PSI) is legally mandated (e.g., HS Code 7207) or if goods are restricted under the prevailing national Import Policy Order (IPO) (5.1.1-xliii, xliv).
- **Rule 3.2 (Shipping Guarantee - SG Replacement):**
  - Margin held against an SG must equal 100% of the SG value in local currency (5.1.2-iii).
  - Upon arrival of original import documents, the system must force the cancellation/removal of the active SG and reconcile the bill (5.1.2-vii).
- **Rule 3.3 (UCP 600 Overdue & Bill of Entry Matching):**
  - Import bills pending acceptance or payment beyond 5 banking days must be automatically flagged as overdue under UCP 600 rules (5.1.3-vi).
  - **Customs Matching:** Bill of Entry data (Item, HS Code, Bill Value) must match Clean Report of Findings (CRF) and commercial invoice value. Discrepancies exceeding regulatory tolerance thresholds must be reported to Bangladesh Bank (5.1.9-vi).

---

## 4. Export Lifecycle & 3-Phase EXP Tracking
- **Rule 4.1 (EXP Form 3-Phase Lifecycle):**
  - The system must enforce mandatory state transitions across three distinct milestones (5.2.7-i):
    1. **Phase 1 (Issuance):** Registered upon initial EXP generation.
    2. **Phase 2 (Document Submission):** Logged when exporter presents original shipping documents with customs endorsement.
    3. **Phase 3 (Proceeds Realization):** Finalized upon receipt of foreign currency proceeds via SWIFT (MT 103/202/910).
- **Rule 4.2 (Overdue Export Proceeds & PRC Rules):**
  - Export proceeds must be fully realized within **4 months** of shipment; uncollected bills beyond 4 months must be reported as Overdue Export Bills to Bangladesh Bank (5.2.10-i).
  - Proceeds Realization Certificate (PRC) must be issued upon realization. Cash subsidy claims must be lodged within **6 months** from PRC issuance (5.2.10-i).
- **Rule 4.3 (Force Loan Conversion):**
  - If pre-shipment advances (Packing Credit - PC) or Foreign Documentary Bills Purchased (FDBP) are not liquidated by realized proceeds on maturity, the CBS must automatically generate a **Force Loan** against the client (5.2.3-v).

---

## 5. Trade Based Money Laundering (TBML) Detection Rules
- **Rule 5.1 (Trade Transaction Profile - TTP Validation):**
  - Every trade client must have a verified Trade Transaction Profile (TTP). Transactions exceeding the volume, commodity category, or pricing tolerances of the TTP must trigger an override alert (5.7.1).
- **Rule 5.2 (Automated TBML Red Flags):**
  - System must automatically flag transactions matching suspicious trade indicators (5.7.7-i):
    - Transshipment or invoicing involving landlocked countries or non-maritime hubs.
    - Financial institutions located in FATF high-risk jurisdictions or known tax havens.
    - Anomalous round-figure transaction sums.
    - Over-invoicing or under-invoicing against standard commodity international price reference templates (5.7.6-i).
- **Rule 5.3 (Vessel Tracking & Escalation):**
  - Integration with 3rd-party vessel tracking APIs must verify port of loading and route authenticity with immutable audit logging (5.7.5).
  - Confirmed sanction hits or unresolved TBML alerts must escalate to Level-3 Compliance for filing Suspicious Activity Reports (SAR/STR) to the Bangladesh Financial Intelligence Unit (BFIU) (5.7.8).
