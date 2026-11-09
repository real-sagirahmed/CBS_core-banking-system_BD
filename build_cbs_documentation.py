import fitz
import re
import os

pdf_path = r"D:\CBS_core-banking-system_bd\may132024brpd12e.pdf"
output_md_path = r"D:\CBS_core-banking-system_bd\may132024brpd12e.md"

doc = fitz.open(pdf_path)

# Regex helpers
roman_pattern = re.compile(
    r'^(xlviii|xlvii|xlvi|xlv|xliv|xliii|xlii|xli|xl|'
    r'xxxix|xxxviii|xxxvii|xxxvi|xxxv|xxxiv|xxxiii|xxxii|xxxi|xxx|'
    r'xxix|xxviii|xxvii|xxvi|xxv|xxiv|xxiii|xxii|xxi|xx|'
    r'xix|xviii|xvii|xvi|xv|xiv|xiii|xii|xi|x|'
    r'ix|viii|vii|vi|v|iv|iii|ii|i)\.\s*(.*)',
    re.IGNORECASE
)
alpha_pattern = re.compile(r'^([a-z]{1,2})[\.\)]\s*(.*)', re.IGNORECASE)
valid_doubles = {'aa','bb','cc','dd','ee','ff','gg','hh','ii','jj','kk','ll','mm','nn','oo','pp','qq','rr','ss','tt'}

def format_description(text):
    if not text:
        return ""
    lines = text.splitlines()
    formatted = []
    
    for raw_line in lines:
        line = raw_line.strip()
        if not line:
            continue
            
        lower = line.lower()
        if lower.startswith("e.g.") or lower.startswith("i.e."):
            if formatted:
                formatted[-1] += " " + line
            else:
                formatted.append(line)
            continue
            
        m_rom = roman_pattern.match(line)
        m_alp = alpha_pattern.match(line)
        
        if m_rom:
            formatted.append(f"- **{m_rom.group(1)}.** {m_rom.group(2)}")
        elif m_alp:
            tag = m_alp.group(1).lower()
            if len(tag) == 1 or tag in valid_doubles:
                formatted.append(f"  - **{tag})** {m_alp.group(2)}")
            else:
                if formatted:
                    formatted[-1] += " " + line
                else:
                    formatted.append(line)
        else:
            if formatted:
                formatted[-1] += " " + line
            else:
                formatted.append(line)
                
    return "\n".join(formatted)

# Extract abbreviations
abbrs = []
for pno in [10, 11]:
    lines = [l.strip() for l in doc[pno].get_text().splitlines() if l.strip()]
    for i in range(1, len(lines)):
        if lines[i].startswith(':'):
            k = lines[i-1]
            v = lines[i][1:].strip()
            abbrs.append((k, v))

# Canonical Submodule Map
SUBMODULES_DEF = [
    # Module 1
    (1, "1.1", "Bank and Branch Setup"),
    (1, "1.2", "End of day/Beginning of day/Month End/Year End Operation"),
    (1, "1.3", "General Ledger"),
    (1, "1.4", "Reconciliation and Settlement"),
    (1, "1.5", "Clearing"),
    (1, "1.6", "Account Transaction"),
    (1, "1.7", "Teller Transaction"),
    (1, "1.8", "Standing Instructions (SI)"),
    # Module 2
    (2, "2.1", "Local Remittance"),
    (2, "2.2", "Foreign Remittance"),
    # Module 3
    (3, "3.1", "General Characteristics"),
    (3, "3.2", "Savings Deposit/Mudaraba Savings Deposit for Shariah based Islamic Banks"),
    (3, "3.3", "Current Deposit/Al-Wadiah Deposit for Islamic Banks"),
    (3, "3.4", "Special Notice Deposit/Mudaraba Special Notice Term Deposit"),
    (3, "3.5", "Fixed Deposit Receipt"),
    (3, "3.6", "Monthly/Quarterly/Other term Profit Based Fixed Deposit"),
    (3, "3.7", "Deposit Multiplication Scheme (Double, Triple, etc.)"),
    (3, "3.8", "Deposit Pension Scheme (DPS)"),
    (3, "3.9", "Continuous Loan Overdraft (OD) on Current Account"),
    (3, "3.10", "Teller (Cash Counter) Transaction Services"),
    (3, "3.11", "Payment Order (PO) Transaction Services"),
    (3, "3.12", "Demand Draft (DD) Transaction Services"),
    (3, "3.13", "Telegraphic/Telephonic Transfer (TT) Transaction Services"),
    # Module 4
    (4, "4.1", "Continuous Loan (CC/OD)"),
    (4, "4.2", "Fixed Term Loan"),
    (4, "4.3", "Quard (Islamic Finance) QTDR, QHG, QPF, QBF, QACA"),
    (4, "4.4", "Short Term Agricultural and Micro-Credit Loan"),
    (4, "4.5", "Demand Loan"),
    (4, "4.6", "Discounted Loan"),
    # Module 5
    (5, "5.0", "UNSCR and Local Sanction Screening for International Trade"),
    (5, "5.1", "Import"),
    (5, "5.2", "Export"),
    (5, "5.3", "Remittance"),
    (5, "5.4", "Guarantee"),
    (5, "5.5", "Back-to-Back LC"),
    (5, "5.6", "Master LC/Contract"),
    (5, "5.7", "Trade Based Money Laundering (TBML)"),
    # Module 6
    (6, "6.1", "Products"),
    (6, "6.2", "Front Office"),
    (6, "6.3", "Middle Office"),
    (6, "6.4", "Back Office"),
    (6, "6.5", "Other Features"),
    # Module 7
    (7, "7.1", "Existing ADC products"),
    (7, "7.2", "Different channels integration with CBS"),
    # Module 8
    (8, "8.1", "Customer Information Management"),
    # Module 9
    (9, "9.1", "Governance, Risk Management and Compliance"),
    # Module 10
    (10, "10.1", "User Management"),
    (10, "10.2", "Role Management"),
    (10, "10.3", "Application Administration"),
    # Module 11
    (11, "11.1", "Reporting Category and Features"),
    (11, "11.2", "Report Preservation"),
    # Module 12
    (12, "12.1", "Training and Awareness"),
    (12, "12.2", "Customer Awareness"),
    (12, "12.3", "User Manual")
]

MODULE_TITLES = {
    1: "General Banking",
    2: "Bills and Remittance",
    3: "Deposits",
    4: "Loans/Credit/Investment",
    5: "Trade Service",
    6: "Treasury",
    7: "Alternative Delivery Channel (ADC)",
    8: "Customer Relationship Management (CRM)",
    9: "Information Security, Governance and Compliance",
    10: "System Administration",
    11: "Reporting",
    12: "User Training and Customer Awareness"
}

# Parse all items from pages 13 to 86
extracted_items = []
current_item = None

for pno in range(12, len(doc)):
    page = doc[pno]
    tabs = page.find_tables()
    for tab in tabs:
        rows = tab.extract()
        for r in rows:
            cells = [c.strip() if c else '' for c in r if c is not None]
            while cells and not cells[0]: cells.pop(0)
            while cells and not cells[-1]: cells.pop()
            if not cells: continue
            
            first = cells[0]
            if first in ['Sl. No.', 'Sl.No.', 'Sl. No']: continue
            if len(cells) == 1 and re.match(r'^\d+(\.\d+)*\s+[A-Za-z]', first):
                continue
            
            m = re.match(r'^(\d+\.\d+\.\d+)', first)
            if m:
                sl = m.group(1)
                item_name = cells[1] if len(cells) > 1 else ''
                desc = cells[2] if len(cells) > 2 else (cells[1] if len(cells) == 2 else '')
                if len(cells) > 3:
                    item_name = cells[1]
                    desc = ' '.join(cells[2:])
                current_item = {
                    'sl': sl,
                    'item': item_name.replace('\n', ' ').strip(),
                    'desc': desc.strip(),
                    'page': pno + 1
                }
                extracted_items.append(current_item)
            else:
                if current_item:
                    current_item['desc'] += '\n' + ' '.join(cells)

# Group items by submodule
# Note: For 12.1.5 under Customer Awareness, its page is 86
items_by_submodule = {}
for it in extracted_items:
    sl = it['sl']
    if sl == '12.1.5' and it['item'].lower().startswith('customer awareness'):
        sub_key = '12.2'
    elif sl == '12.2.1' and it['item'].lower().startswith('user manual'):
        sub_key = '12.3'
    else:
        parts = sl.split('.')
        sub_key = f"{parts[0]}.{parts[1]}"
    
    if sub_key not in items_by_submodule:
        items_by_submodule[sub_key] = []
    items_by_submodule[sub_key].append(it)

# Now construct the Markdown documentation
md = []

# Title & Metadata
md.append("# Guidelines on Core Banking Solution (CBS) Features and Controls (Version 2.0)")
md.append("")
md.append("> **Regulatory Standard of Bangladesh Bank**  ")
md.append("> **Department:** Banking Regulation and Policy Department (BRPD)  ")
md.append("> **Circular:** BRPD Circular No. 12  ")
md.append("> **Date:** 13 May 2024 / 30 Baishakh 1431  ")
md.append("> **Effective Date:** Immediate  ")
md.append("> **Mandatory Full Compliance Deadline:** 30 June 2025  ")
md.append("> **Repeals:** BRPD Circular No. 08, dated 01 December 2016  ")
md.append("")
md.append("---")
md.append("")

# Circular Letter
md.append("## Official Circular Directive")
md.append("")
md.append("**Banking Regulation and Policy Department**  ")
md.append("**Bangladesh Bank, Head Office, Dhaka**  ")
md.append("**Website:** [www.bb.org.bd](https://www.bb.org.bd)  ")
md.append("")
md.append("**Date:** 30 Baishakh 1431 / 13 May 2024  ")
md.append("**BRPD Circular No. 12**  ")
md.append("")
md.append("**To:** Managing Directors / Chief Executive Officers, All Scheduled Banks functioning in Bangladesh  ")
md.append("")
md.append("**Subject: Guidelines on Core Banking Solution (CBS) Features and Controls**")
md.append("")
md.append("Dear Sirs,")
md.append("")
md.append("Please refer to BRPD Circular No. 08, dated 01 December 2016 on the captioned subject.")
md.append("")
md.append("2. Guidelines on Core Banking Solution (CBS) Features and Controls (Version 1.0) was issued to formulate a uniform set of instruction as minimum requirements for CBS through the said circular. To adapt to constantly evolving technology-driven environment, the above mentioned guidelines has been updated recently.")
md.append("")
md.append("3. The updated Guidelines on Core Banking Solution (CBS) Features and Controls (Version 2.0) is available in Bangladesh Bank website ([www.bb.org.bd](https://www.bb.org.bd)). This guidelines shall come into force with immediate effect. However, its full compliance must be ensured by **30 June 2025**.")
md.append("")
md.append("4. In case of any query arising in the process of implementation, banks are advised to communicate with **ICT Audit, Inspection and Compliance Wing of Information & Communication Technology Department, Bangladesh Bank**.")
md.append("")
md.append("5. BRPD Circular No. 08, dated 01 December 2016 is hereby repealed.")
md.append("")
md.append("**Yours Sincerely,**  ")
md.append("*(Md. Harun-Ar-Rashid)*  ")
md.append("Director (BRPD)  ")
md.append("Phone: 9530095  ")
md.append("")
md.append("---")
md.append("")

# Technical Committee & Contributors
md.append("## Technical Committee & Contributors")
md.append("")
md.append("### Technical Committee")
md.append("")
md.append("**Chairman:**")
md.append("- **Mohammed Ishaque Miah**, Chief Information Security Officer (CISO), Cyber Security Unit (CSU), Bangladesh Bank")
md.append("")
md.append("**Members:**")
md.append("- **Md. Wahidul Islam Sarker**, Additional Director (ICT), Information and Communication Technology Department, Bangladesh Bank")
md.append("- **Md. Khairul Anam**, Additional Director, Payment Systems Department, Bangladesh Bank")
md.append("- **Shadril Ahmed**, Additional Director, Banking Regulation and Policy Department, Bangladesh Bank")
md.append("- **S. M. Tofayel Ahmad**, Joint Director (ICT) and Member Secretary, Information and Communication Technology Department, Bangladesh Bank")
md.append("- **Abu Sadat Mohammad Sayem**, Joint Director, Department of Banking Inspection-1, Bangladesh Bank")
md.append("- **Md. Towfiqul Islam**, Joint Director, Department of Banking Inspection-3, Bangladesh Bank")
md.append("- **Md. Rafiqul Islam**, General Manager, Sonali Bank PLC, Khulna GM Office")
md.append("- **Hossain Ahmad**, Senior Executive Vice President (SEVP) and Chief Information Officer (CIO), ICT Division, Bank Asia PLC")
md.append("- **Endra Mohan Sutradhar**, General Manager, Software Development Department, Pubali Bank PLC")
md.append("- **Md. Mashfiqur Rahman**, Head of IT, The Hongkong and Shanghai Banking Corporation Limited (HSBC) Bangladesh")
md.append("- **Mohammad Rezaul Karim**, Executive Vice President (EVP), Software Division, Islami Bank Bangladesh PLC")
md.append("- **Shajeed Md Shafique**, Executive Vice President, Project Head, CBS Upgradation Project, Dutch-Bangla Bank PLC")
md.append("- **Md. Mushfiqur Rahman**, SVP and Chief Information Technology Officer (CITO), First Security Islami Bank PLC")
md.append("- **Md. Soeb Ul Islam**, Vice President (VP), ICT Division, Social Islami Bank PLC")
md.append("- **Uttam Adhikari**, Senior Vice President (SVP) and Head of Core Banking Applications, Technology Division, BRAC Bank PLC")
md.append("")
md.append("### Additional Contributors")
md.append("- **Md. Munirul Islam Masud**, Assistant General Manager, Information Technology Division (Business IT), Sonali Bank PLC")
md.append("- **Kashef Rahman**, Executive Vice President and Head of IT, Information Technology Division, United Commercial Bank PLC")
md.append("- **Mohammad Anamul Huq**, Senior Vice President and Head of Export, Brac Bank PLC")
md.append("- **Sharif Ahmed**, Head of Core Banking Systems and Technology Projects, Vice President, Digital Banking Division, Mutual Trust Bank PLC")
md.append("- **Kazi Mohammad Sadik**, Vice President (Head of ICT Operation), ICT Wing, Al-Arafah Islami Bank PLC")
md.append("- **Mohammad Nazmul Alam**, Principal Officer, Information Technology Division (Business IT), Sonali Bank PLC")
md.append("")
md.append("---")
md.append("")

# Table of Contents
md.append("## Table of Contents")
md.append("")
md.append("- [Official Circular Directive](#official-circular-directive)")
md.append("- [Technical Committee & Contributors](#technical-committee--contributors)")
md.append("- [I. Regulatory Framework & Foundations](#i-regulatory-framework--foundations)")
md.append("  - [I. Introduction](#i-introduction)")
md.append("  - [I (A). Objectives](#i-a-objectives)")
md.append("  - [I (B). Scope and Applicability](#i-b-scope-and-applicability)")
md.append("  - [I (C). Compliance and Dispensation](#i-c-compliance-and-dispensation)")
md.append("  - [I (D). List of Abbreviations](#i-d-list-of-abbreviations)")

for mod_num in range(1, 13):
    mod_name = MODULE_TITLES[mod_num]
    anchor = f"module-{mod_num}-{mod_name.lower().replace(' ', '-').replace('/', '-').replace('(', '').replace(')', '')}"
    md.append(f"- [Module {mod_num}: {mod_name}](#{anchor})")
    
    submods = [s for s in SUBMODULES_DEF if s[0] == mod_num]
    for m_num, s_key, s_title in submods:
        sub_anchor = f"{s_key.replace('.', '')}-{s_title.lower().replace(' ', '-').replace('/', '-').replace('(', '').replace(')', '').replace(',', '')}"
        md.append(f"  - [{s_key} {s_title}](#{sub_anchor})")

md.append("")
md.append("---")
md.append("")

# Section I: Regulatory Framework & Foundations
md.append("## I. Regulatory Framework & Foundations")
md.append("")
md.append("### I. Introduction")
md.append("")
md.append("The banking industry in Bangladesh is the core driver in economic development of the country. The focus on inclusion and the aim for sustainability have advanced the industry to grow with a faster pace and in a well-structured manner. The industry has undergone many changes on providing the services to their customers. Most of the banks have adopted automated banking environment for faster services, better control in operations and established alternative delivery channels to facilitate services at door-steps. Core Banking Solution (CBS) is playing a vital role for this technology driven banking services.")
md.append("")
md.append("Today, the role of CBS is not just as a transaction processing engine; it is the key repository of information to further analyze and detect risks. A standard and flexible CBS is a significant contributor to a bank's ability to respond quickly to the diversified business including compliances with changing regulations.")
md.append("")
md.append("Apart from the CBS, banks are using different important solutions for different business portfolios like Trade Finance, Remittance, Asset Liability Management, Anti-Money Laundering, Card Management, etc. Though these business operations may be done using separate solutions, the reporting to stakeholders and regulators is done in a consolidated manner. Most of the commercial banks have already implemented CBS and the rests are on the way of implementation. Different commercial banks of Bangladesh are using different outsourced or self-developed CBS. Due to the variations in development architecture, support service, modality and flexibility in these systems, stakeholders are facing challenges in collecting the necessary and intended information at the right time. This necessitates a uniform set of instructions that should be accommodated as a minimum requirement in any CBS.")
md.append("")
md.append("This document is developed in a collaborative approach to guide the commercial banks in Bangladesh in maintaining the minimum required features and controls in their CBS and other related banking solutions so that the banks can comply with most of the stakeholders’ requirements. The document covers the necessary features and controls of banking services like General Banking, Bills and Remittance, Deposits, Loans/Credit/Investment, Trade Service, Treasury, System Administration and Reporting. Each bank has its own way of management reporting and regulators also have their different query and reporting requirements which are changing over time, as such fixed reporting formats and demands could not be stated in the reporting module. It is expected that the reporting tools used in the banking system shall be capable and flexible enough to generate new format of reports as required.")
md.append("")
md.append("> [!IMPORTANT]")
md.append("> **Islamic Banking Principles:** In case of Islamic Banking, for all kinds of operations including profit and loss calculation will be based on Shariah principles and relevant guidelines of Bangladesh Bank.")
md.append("")
md.append("### I (A). Objectives")
md.append("")
md.append("Core banking operations involve managing transactions, processing payments, handling loans, and many other activities. It facilitates online banking, automated teller machines (ATMs), telebanking, and immediate or remote fund transfers through mechanisms like immediate payment service (IMPS), electronic fund transfer (EFT), and real-time gross settlement (RTGS).")
md.append("")
md.append("Application Programming Interfaces (APIs) are becoming more common in core banking systems to facilitate easy integration with third-party services. It facilitates a broader range of services and enhances the customer experience. For instance, through APIs, banks can integrate with FinTech services, payment gateways, or even other banks for seamless interbank and inter platform transactions.")
md.append("")
md.append("As the banking industry continues to evolve, these systems are increasingly adopting innovative technologies like AI, cloud computing, and blockchain, paving the way for more advanced, secure, and customer-centric banking services. However, choosing and implementing a CBS is a significant undertaking. It requires a thorough understanding of the organization’s needs, a comprehensive evaluation of potential solutions, and careful planning and management of the implementation process. The rise of digital banking will shape the future of CBS, making this an exciting space to watch in the years to come. Some basic objectives of this guideline are:")
md.append("")
objectives = [
    ("a", "Ensuring proper due diligence while using Core Banking Solutions."),
    ("b", "Ensuring quicker services at the bank counters for routine transactions like cash deposits, withdrawal, passbooks, statements of accounts, demand drafts, etc."),
    ("c", "Ensuring anywhere banking by eliminating dependency with branch banking."),
    ("d", "Ensuring provision of banking services 24 X 7."),
    ("e", "Ensuring fast payment processing through Internet banking, mobile banking."),
    ("f", "Ensuring anytime anywhere banking through ATMs, CRMs."),
    ("g", "Ensuring that all branches access applications from central servers/datacenter, so deposits made in any branch reflect immediately and the customer can withdraw money from any other branch throughout the world."),
    ("h", "Ensuring easy transfer of funds from the cities to the villages and vice versa."),
    ("i", "Ensuring process standardization within bank & branches."),
    ("j", "Ensuring retention of customers through better customer service."),
    ("k", "Ensuring accuracy in transactions & minimization of errors."),
    ("l", "Ensuring improved management of documentation & records – having centralized databases results in quick gathering of data & MIS reports."),
    ("m", "Ensuring ease in the submission of various reports to the Government & Regulatory body."),
    ("n", "Ensuring convenience in opening accounts, processing cash, servicing loans, calculating interest, implementing changes in policies (i.e. change in interest rates)."),
    ("o", "Ensuring security and data privacy requirements."),
    ("p", "Ensuring interoperability and portability of data and services between intra system environments."),
    ("q", "Ensuring roles and responsibilities of the relevant parties."),
    ("r", "Ensuring organizational security, privacy and its computing requirements."),
    ("s", "Ensuring a secure environment for data processing."),
    ("t", "Ensuring awareness of stakeholder’s roles and responsibilities for protection of information in CBS environment."),
    ("u", "Ensuring business continuity, resilience and recovery capabilities."),
    ("v", "Ensuring effective Customer Service Provider (CSP) management."),
    ("w", "Ensuring best practices (industry standard) in the use of technology etc.")
]
for tag, text in objectives:
    md.append(f"- **{tag})** {text}")

md.append("")
md.append("### I (B). Scope and Applicability")
md.append("")
md.append("This guideline is applicable to all Banks of Bangladesh. It addresses the approach and principles necessary for adoption of Core Banking Solution by the Bank. However, this guideline does not prescribe or recommend any specific Core Banking Solution, service arrangement, service agreement, service provider or deployment models. The Bank must perform their own analysis to determine if CBS meets their strategic aims whilst managing any associated risks and compliance with regulatory requirements.")
md.append("")
md.append("### I (C). Compliance and Dispensation")
md.append("")
dispensation = [
    ("a", "The bank shall assign a Team/Committee/Entity to monitor compliance with this guideline. A Team/Committee/Entity shall provide the initial observation of any non-compliance issue to the competent authority."),
    ("b", "The bank shall take initiatives to rectify the non-compliance activities as per guidelines within a specific time frame."),
    ("c", "For non-compliance issues, compliance plans shall be submitted to Bangladesh Bank. If any non-compliance issue persists due to exceptional cases, then dispensation can be taken from Bangladesh Bank for a specific period."),
    ("d", "If the bank fails to rectify the non-compliance activities within the time frame, the bank shall seek permission for dispensation from Bangladesh Bank. After the compliance failure multiple times, a penalty may be imposed on the bank depending on the impact of business or any adverse impact on customers’ interest.")
]
for tag, text in dispensation:
    md.append(f"- **{tag})** {text}")

md.append("")
md.append("### I (D). List of Abbreviations")
md.append("")
md.append("| Abbreviation | Meaning / Definition |")
md.append("| :--- | :--- |")
for k, v in abbrs:
    md.append(f"| **{k}** | {v} |")

md.append("")
md.append("---")
md.append("")

# Modules 1 to 12
for mod_num in range(1, 13):
    mod_name = MODULE_TITLES[mod_num]
    md.append(f"## Module {mod_num}: {mod_name}")
    md.append("")
    
    # Check if module has special intro
    if mod_num == 5:
        md.append("### 5.0 UNSCR and Local Sanction Screening for International Trade")
        md.append("")
        md.append("Before opening an L/C or making payment against an L/C, UNSCR and local sanction screening shall be conducted. Similar measures shall be made in case of inward as well as outward remittances.")
        md.append("")
    elif mod_num == 7:
        md.append("Alternative delivery channels (ADC) are the channels that expand the reach of financial services beyond the traditional bank branch channel. In providing financial services directly to customers outside the branch, ADC has proven its ability to meet customers’ expectation ensuring accuracy, convenience, and timely/on-time/real-time 24/7 services.")
        md.append("")
        md.append("### 7.1 Existing ADC products")
        md.append("")
        adc_products = [
            "Internet-Banking(IB)",
            "Banking with Mobile Apps",
            "Mobile Financial Services (MFS)",
            "Electronic Fund Transfer (EFT)",
            "Real Time Gross Settlement (RTGS)",
            "National Payment Switch Bangladesh (NPSB)",
            "BACH (Bangladesh Automated Clearing House)",
            "E-Wallet",
            "Call Center",
            "ATM Switching System",
            "Card Management System",
            "Agent Banking",
            "Binimoy",
            "Other Channels"
        ]
        for idx, prod in enumerate(adc_products):
            tag = chr(ord('a') + idx)
            md.append(f"- **{tag})** {prod}")
        md.append("")
    elif mod_num == 8:
        md.append("It is necessary to manage the required customer information of an individual, company, nominee, introducer, chairman, director, guardian, grantor, bearer etc. and relationship among them. Ensuring KYC with proper due diligence complying AML & CFT guidelines are required for customers that includes asset liabilities info, occupation info, business nature (if applicable), transaction profile, source of funds, beneficiary owner info (if applicable), etc. It is required to generate reports related to customer profile, account, transaction, CIB, CL, Group Customer, AML, RIT and any other reports including regulatory reports.")
        md.append("")
    elif mod_num == 9:
        md.append("The Bank shall ensure that CBS must have Access Controls and Governance as following:")
        md.append("")
    elif mod_num == 11:
        md.append("Core Banking System shall be capable of generating the specified management and regulatory reports.")
        md.append("")
        
    # Get all submodules for this module
    submods = [s for s in SUBMODULES_DEF if s[0] == mod_num and s[1] not in ["5.0", "7.1"]]
    
    for _, s_key, s_title in submods:
        md.append(f"### {s_key} {s_title}")
        md.append("")
        
        # Submodule items
        sub_items = items_by_submodule.get(s_key, [])
        if not sub_items:
            md.append("*(No individual tabular items under this section)*")
            md.append("")
            continue
            
        # Summary Table for the submodule
        md.append("| Sl. No. | Control / Feature Item | Key Regulatory Scope |")
        md.append("| :--- | :--- | :--- |")
        for it in sub_items:
            first_line = it['desc'].splitlines()[0] if it['desc'] else ''
            first_line = re.sub(r'^(x{0,3}(?:ix|iv|v?i{1,3}))\.\s*', '', first_line, flags=re.IGNORECASE)
            first_line = first_line.replace('|', '/').strip()
            if len(first_line) > 90:
                first_line = first_line[:87] + '...'
            md.append(f"| [`{it['sl']}`](#{it['sl'].replace('.', '')}-{it['item'].lower().replace(' ', '-').replace('/', '-').replace('(', '').replace(')', '').replace(',', '')}) | **{it['item']}** | {first_line} |")
        md.append("")
        
        # Detailed items
        for it in sub_items:
            md.append(f"#### {it['sl']} {it['item']}")
            md.append("")
            formatted_desc = format_description(it['desc'])
            md.append(formatted_desc)
            md.append("")
            
    md.append("---")
    md.append("")

# Write to file
with open(output_md_path, 'w', encoding='utf-8') as f:
    f.write('\n'.join(md))

print(f"Documentation successfully updated at: {output_md_path}")
print(f"Total lines: {len(md)}")
print(f"File size: {os.path.getsize(output_md_path)} bytes")
