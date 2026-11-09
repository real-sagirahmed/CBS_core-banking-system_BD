/**
 * S7 CBS — Internationalization (i18n) Engine
 * Supported Languages: English ('en' - Default) & Bengali ('bn')
 * Bangladesh Bank BRPD Circular 12 v2.0 Compliance
 */

const I18N_RESOURCES = {
  en: {
    // Brand & Header
    "brand.name": "S7 CBS Core Banking",
    "brand.tagline": "Bangladesh Bank BRPD Circular 12",
    "brand.status": "LIVE · Core Active",
    "branch.name": "Principal Branch (001)",
    "search.placeholder": "Search CIF, Account No or Voucher (Ctrl + K)...",
    "role.maker": "Maker Officer",
    "role.checker": "Checker Manager",
    "role.switch_to_maker": "Switch to Maker",
    "role.switch_to_checker": "Switch to Checker",
    "theme.toggle": "Toggle Theme",
    "lang.toggle": "BN",

    // Navigation Menu
    "nav.dashboard": "Overview Dashboard",
    "nav.customer_onboarding": "Customer e-KYC & Party",
    "nav.maker_checker": "Maker-Checker Queue",
    "nav.general_banking": "Accounts & Deposits",
    "nav.vouchers": "Vouchers & Cash Ledger",
    "nav.regulatory_audit": "Central Bank Audit Trail",
    "nav.system_security": "Security & 4-Eyes Access",
    "nav.ai_advisory": "Gemini Advisor & TBML",
    "nav.group_operations": "Banking Operations",
    "nav.group_compliance": "Audit & Compliance",
    "nav.group_ai": "AI & Intelligence",
    "nav.group_system": "System & Security",
    "nav.collapse": "Collapse Sidebar",
    "nav.expand": "Expand Sidebar",

    // Dashboard Statistics & KPIs
    "kpi.total_deposits": "Total Branch Deposits",
    "kpi.active_cif": "Active Customers (CIF)",
    "kpi.clearing_volume": "Daily Clearing (BACH/BEFTN)",
    "kpi.pending_approvals": "Pending Dual-Control",
    "kpi.vault_cash": "Branch Vault Balance",
    "kpi.slr_crr_ratio": "SLR / CRR Regulatory Health",
    "kpi.trend_daily": "+3.4% today",
    "kpi.trend_healthy": "Compliant 100%",

    // Sections & Headers
    "section.recent_activity": "Real-Time Transaction Feed",
    "section.pending_approval_table": "Dual-Control Maker-Checker Approval Queue",
    "section.customer_form_title": "Customer Onboarding & e-KYC Draft",
    "section.customer_form_sub": "Dual-control registration compliant with BRPD Circular 12 (Section 8.1)",
    "section.audit_log_title": "Immutable BRPD Audit Log Journal",
    "section.voucher_title": "Double-Entry Financial Voucher Engine",

    // Form Fields & Labels
    "form.customer_type": "Customer Entity Type",
    "form.customer_type_individual": "Individual / Retail",
    "form.customer_type_corporate": "Corporate / Enterprise",
    "form.full_name": "Full Legal Name",
    "form.full_name_placeholder": "e.g., Kazi Nazrul Islam",
    "form.dob": "Date of Birth",
    "form.gender": "Gender",
    "form.gender_male": "Male",
    "form.gender_female": "Female",
    "form.gender_other": "Other",
    "form.nid": "National Identity Number (NID)",
    "form.nid_hint": "10-digit Smart NID or 13/17-digit legacy (BRPD 8.1.3-a)",
    "form.tin": "Tax Identification Number (e-TIN)",
    "form.tin_hint": "Must be exactly 12 numeric digits (BRPD 8.1.4-a)",
    "form.mobile": "Mobile Number",
    "form.address": "Street Address",
    "form.district": "District",
    "form.upazila": "Thana / Upazila",
    "form.postal_code": "Postal Code",
    "form.guardian_cif": "Legal Guardian CIF (Mandatory for Minors < 18)",
    "form.guardian_cif_hint": "Minor under-18 requires verified Guardian CIF (BRPD 3.2.1-ii)",
    "form.monthly_deposit_cap": "Monthly Cash Deposit Limit (BDT)",
    "form.monthly_withdraw_cap": "Monthly Cash Withdrawal Limit (BDT)",

    "btn.view_all": "View All",
    "btn.full_log": "Full Log",
    "role.maker_btn": "Maker",
    "role.checker_btn": "Checker",
    "search.no_results": "No matching records found",
    "th.user": "Operator",
    "pagination.showing": "Showing",
    "pagination.to": "to",
    "pagination.of": "of",
    "pagination.records": "records",
    "pagination.rows_per_page": "Rows per page:",
    "pagination.prev": "Previous",
    "pagination.next": "Next",
    // Buttons & Actions
    "btn.verify_ec": "Verify via Election Commission (EC)",
    "btn.submit_draft": "Submit Draft to Checker",
    "btn.approve": "Authorize (4-Eyes Check)",
    "btn.reject": "Reject Draft",
    "btn.cancel": "Cancel",
    "btn.view_details": "Inspect e-KYC",
    "btn.reset_form": "Reset Form",
    "btn.new_customer": "New Customer (Maker)",
    "btn.export_csv": "Export Audit CSV",

    // Table Column Headers
    "th.cif": "CIF Number",
    "th.name": "Customer Name",
    "th.nid": "NID / Smart ID",
    "th.type": "Type",
    "th.maker": "Maker Officer",
    "th.status": "Workflow Status",
    "th.actions": "Actions",
    "th.time": "Timestamp",
    "th.event": "Audit Event",
    "th.regulatory_ref": "Regulatory Ref",

    // Workflow Statuses
    "status.draft": "Draft",
    "status.pending": "Pending Checker",
    "status.active": "Authorized / Active",
    "status.rejected": "Rejected",
    "status.blocked": "Sanction Hit (Blocked)",

    // Invariant & Regulatory Notices
    "banner.title": "Dual-Control Segregation of Duties Notice",
    "banner.desc": "Bangladesh Bank BRPD Circular 12 mandate: The Maker who creates a customer draft or financial voucher cannot authorize it. Authorization requires an independent Checker with 4-eyes review.",
    "msg.draft_created": "Customer draft created successfully and queued for Checker authorization.",
    "msg.approved": "Customer record verified and authorized by Checker. Account is now Active.",
    "msg.rejected": "Customer draft has been rejected.",
    "msg.same_user_violation": "Dual-Control Violation: Maker cannot approve their own draft! Switch to Checker mode (BRPD 1.3.18).",
    "msg.sanction_hit_blocked": "FATAL: Sanction Screening Hit! Account onboarding prohibited under UNSCR and domestic AML/CFT regulations.",
    "msg.under18_guardian_required": "Minor Customer Invariant: Customers under 18 strictly require a verified Legal Guardian CIF (BRPD 3.2.1-ii)."
  },

  bn: {
    // Brand & Header
    "brand.name": "এস৭ সিবিএস কোর ব্যাংকিং",
    "brand.tagline": "বাংলাদেশ ব্যাংক বিআরপিডি সার্কুলার ১২",
    "brand.status": "সক্রিয় · কোর লাইভ",
    "branch.name": "প্রধান শাখা (০০১)",
    "search.placeholder": "সিআইএফ, হিসাব নং বা ভাউচার খুঁজুন (Ctrl + K)...",
    "role.maker": "মেকার কর্মকর্তা",
    "role.checker": "চেকার ব্যবস্থাপক",
    "role.switch_to_maker": "মেকার মোডে যান",
    "role.switch_to_checker": "চেকার মোডে যান",
    "theme.toggle": "থিম পরিবর্তন",
    "lang.toggle": "EN",

    // Navigation Menu
    "nav.dashboard": "ওভারভিউ ড্যাশবোর্ড",
    "nav.customer_onboarding": "গ্রাহক নিবন্ধন ও ই-কেওয়াইসি",
    "nav.maker_checker": "মেকার-চেকার অনুমোদন কিউ",
    "nav.general_banking": "হিসাব ও আমানত",
    "nav.vouchers": "ভাউচার ও ক্যাশ খতিয়ান",
    "nav.regulatory_audit": "কেন্দ্রীয় ব্যাংক অডিট ট্রেইল",
    "nav.system_security": "নিরাপত্তা ও ৪-আই অ্যাক্সেস",
    "nav.ai_advisory": "জেমিআই রেগুলেটরি এআই ও টিবিএমএল",
    "nav.group_operations": "ব্যাংকিং কার্যক্রম",
    "nav.group_compliance": "অডিট ও কমপ্লায়েন্স",
    "nav.group_ai": "এআই ও ইন্টেলিজেন্স",
    "nav.group_system": "সিস্টেম ও সিকিউরিটি",
    "nav.collapse": "সাইডবার সংকুচিত করুন",
    "nav.expand": "সাইডবার প্রসারিত করুন",

    // Dashboard Statistics & KPIs
    "kpi.total_deposits": "শাখার মোট আমানত",
    "kpi.active_cif": "সক্রিয় গ্রাহক (সিআইএফ)",
    "kpi.clearing_volume": "দৈনিক ক্লিয়ারিং (বাচ/বিইএফটিএন)",
    "kpi.pending_approvals": "অপেক্ষমাণ ডুয়াল-কন্ট্রোল",
    "kpi.vault_cash": "শাখা ভল্ট ব্যালেন্স",
    "kpi.slr_crr_ratio": "এসএলআর / সিআরআর স্বাস্থ্য",
    "kpi.trend_daily": "+৩.৪% আজ",
    "kpi.trend_healthy": "১০০% বিধিবদ্ধ",

    // Sections & Headers
    "section.recent_activity": "লাইভ লেনদেন ও ভাউচার ফিড",
    "section.pending_approval_table": "ডুয়াল-কন্ট্রোল মেকার-চেকার অনুমোদন তালিকা",
    "section.customer_form_title": "নতুন গ্রাহক নিবন্ধন ও ই-কেওয়াইসি ড্রাফট",
    "section.customer_form_sub": "বাংলাদেশ ব্যাংক বিআরপিডি সার্কুলার ১২ (ধারা ৮.১) অনুযায়ী বাধ্যতামূলক মেকার-চেকার এন্ট্রি",
    "section.audit_log_title": "অপরিবর্তনীয় বিআরপিডি অডিট লগ জার্নাল",
    "section.voucher_title": "দ্বি-তরফা দাখিলা আর্থিক ভাউচার ইঞ্জিন",

    // Form Fields & Labels
    "form.customer_type": "গ্রাহকের সত্তার ধরন",
    "form.customer_type_individual": "ব্যক্তিগত / রিটেইল",
    "form.customer_type_corporate": "প্রাতিষ্ঠানিক / কর্পোরেট",
    "form.full_name": "গ্রাহকের পূর্ণ আইনি নাম",
    "form.full_name_placeholder": "যেমন: কাজী নজরুল ইসলাম",
    "form.dob": "জন্ম তারিখ",
    "form.gender": "লিঙ্গ",
    "form.gender_male": "পুরুষ",
    "form.gender_female": "মহিলা",
    "form.gender_other": "অন্যান্য",
    "form.nid": "জাতীয় পরিচয়পত্র নম্বর (এনআইডি)",
    "form.nid_hint": "১০-ডিজিটের স্মার্ট এনআইডি অথবা ১৩/১৭-ডিজিটের সনাতন (বিআরপিডি ৮.১.৩-ক)",
    "form.tin": "কর শনাক্তকরণ নম্বর (ই-টিআইএন)",
    "form.tin_hint": "অবশ্যই ঠিক ১২টি সংখ্যার হতে হবে (বিআরপিডি ৮.১.৪-ক)",
    "form.mobile": "মোবাইল নম্বর",
    "form.address": "ঠিকানা ও সড়ক",
    "form.district": "জেলা",
    "form.upazila": "থানা / উপজেলা",
    "form.postal_code": "পোস্টাল কোড",
    "form.guardian_cif": "আইনি অভিভাবকের সিআইএফ (১৮ বছরের নিচে বাধ্যতামূলক)",
    "form.guardian_cif_hint": "১৮ বছরের কম বয়সীদের ক্ষেত্রে যাচাইকৃত অভিভাবকের সিআইএফ বাধ্যতামূলক (বিআরপিডি ৩.২.১-খ)",
    "form.monthly_deposit_cap": "মাসিক নগদ জমা সীমা (টাকা)",
    "form.monthly_withdraw_cap": "মাসিক নগদ উত্তোলন সীমা (টাকা)",

    "btn.view_all": "সকল দেখুন",
    "btn.full_log": "সম্পূর্ণ লগ",
    "role.maker_btn": "মেকার",
    "role.checker_btn": "চেকার",
    "search.no_results": "কোনো রেকর্ড পাওয়া যায়নি",
    "th.user": "অপারেটর",
    "pagination.showing": "প্রদর্শিত",
    "pagination.to": "থেকে",
    "pagination.of": "মোট",
    "pagination.records": "টি রেকর্ড",
    "pagination.rows_per_page": "প্রতি পৃষ্ঠায় সারি:",
    "pagination.prev": "পূর্ববর্তী",
    "pagination.next": "পরবর্তী",
    // Buttons & Actions
    "btn.verify_ec": "নির্বাচন কমিশন (ইসি) হতে যাচাই",
    "btn.submit_draft": "চেকার অনুমোদনের জন্য পাঠান",
    "btn.approve": "অনুমোদন করুন (৪-আই নীতি)",
    "btn.reject": "ড্রাফট প্রত্যাখ্যান করুন",
    "btn.cancel": "বাতিল",
    "btn.view_details": "ই-কেওয়াইসি পর্যালোচনা",
    "btn.reset_form": "ফর্ম রিসেট",
    "btn.new_customer": "নতুন গ্রাহক (মেকার)",
    "btn.export_csv": "অডিট সিএসভি ডাউনলোড",

    // Table Column Headers
    "th.cif": "সিআইএফ নম্বর",
    "th.name": "গ্রাহকের নাম",
    "th.nid": "এনআইডি / স্মার্ট কার্ড",
    "th.type": "ধরন",
    "th.maker": "মেকার কর্মকর্তা",
    "th.status": "ওয়ার্কফ্লো অবস্থা",
    "th.actions": "কার্যক্রম",
    "th.time": "সময়",
    "th.event": "অডিট ইভেন্ট",
    "th.regulatory_ref": "বিধিবদ্ধ রেফারেন্স",

    // Workflow Statuses
    "status.draft": "খসড়া",
    "status.pending": "চেকার অনুমোদনের অপেক্ষায়",
    "status.active": "অনুমোদিত ও সক্রিয়",
    "status.rejected": "প্রত্যাখ্যাত",
    "status.blocked": "স্যাংশন হিট (স্থগিত)",

    // Invariant & Regulatory Notices
    "banner.title": "ডুয়াল-কন্ট্রোল দায়িত্ব পৃথকীকরণ বিজ্ঞপ্তি",
    "banner.desc": "বাংলাদেশ ব্যাংক বিআরপিডি সার্কুলার ১২ এর বাধ্যবাধকতা: যে মেকার গ্রাহক নিবন্ধন বা আর্থিক ভাউচার তৈরি করবেন, তিনি নিজে তা অনুমোদন করতে পারবেন না। অনুমোদনের জন্য স্বাধীন চেকারের ৪-আই পর্যালোচনার প্রয়োজন।",
    "msg.draft_created": "গ্রাহকের খসড়া সফলভাবে তৈরি হয়েছে এবং চেকারের অনুমোদনের অপেক্ষায় রয়েছে।",
    "msg.approved": "চেকার কর্তৃক গ্রাহকের তথ্য অনুমোদিত ও যাচাই সম্পন্ন হয়েছে। অ্যাকাউন্ট এখন সক্রিয়।",
    "msg.rejected": "গ্রাহকের খসড়া আবেদনটি প্রত্যাখ্যান করা হয়েছে।",
    "msg.same_user_violation": "ডুয়াল-কন্ট্রোল লঙ্ঘন: মেকার নিজের তৈরি খসড়া অনুমোদন করতে পারবেন না! অনুগ্রহ করে চেকার মোডে যান (বিআরপিডি ১.৩.১৮)।",
    "msg.sanction_hit_blocked": "সতর্কতা: জাতিসংঘ ও জাতীয় সন্ত্রাসী তালিকাভুক্ত স্যাংশন হিট! মানি লন্ডারিং প্রতিরোধ আইনে অ্যাকাউন্ট অনবোর্ডিং নিষিদ্ধ।",
    "msg.under18_guardian_required": "নাবালক গ্রাহক বিধি: ১৮ বছরের নিচে গ্রাহকের ক্ষেত্রে বৈধ অভিভাবকের সিআইএফ আবশ্যক (বিআরপিডি ৩.২.১-খ)।"
  }
};

class I18nManager {
  constructor() {
    this.currentLanguage = localStorage.getItem('cbs_lang') || 'en';
  }

  get currentLang() {
    return this.currentLanguage;
  }

  init() {
    this.applyLanguage(this.currentLanguage);
  }

  setLanguage(lang) {
    if (lang !== 'en' && lang !== 'bn') return;
    this.currentLanguage = lang;
    localStorage.setItem('cbs_lang', lang);
    this.applyLanguage(lang);
  }

  toggleLanguage() {
    const nextLang = this.currentLanguage === 'en' ? 'bn' : 'en';
    this.setLanguage(nextLang);
    return nextLang;
  }

  t(key, fallback = '') {
    const resources = I18N_RESOURCES[this.currentLanguage] || I18N_RESOURCES['en'];
    return resources[key] || fallback || key;
  }

  applyLanguage(lang) {
    document.documentElement.lang = lang;
    const elements = document.querySelectorAll('[data-i18n]');
    elements.forEach(el => {
      const key = el.getAttribute('data-i18n');
      if (key && I18N_RESOURCES[lang] && I18N_RESOURCES[lang][key]) {
        el.textContent = I18N_RESOURCES[lang][key];
      }
    });

    const placeholders = document.querySelectorAll('[data-i18n-placeholder]');
    placeholders.forEach(el => {
      const key = el.getAttribute('data-i18n-placeholder');
      if (key && I18N_RESOURCES[lang] && I18N_RESOURCES[lang][key]) {
        el.setAttribute('placeholder', I18N_RESOURCES[lang][key]);
      }
    });

    // Update Language toggle button label
    const langBtn = document.getElementById('lang-toggle-btn');
    if (langBtn) {
      langBtn.innerHTML = `<svg class="svg-icon" viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="2" y1="12" x2="22" y2="12"></line><path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"></path></svg> <span>${lang === 'en' ? 'BN' : 'EN'}</span>`;
    }
  }
}

window.i18n = new I18nManager();
