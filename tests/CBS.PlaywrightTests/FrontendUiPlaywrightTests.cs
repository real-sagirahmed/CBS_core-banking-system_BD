using CBS.PlaywrightTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace CBS.PlaywrightTests;

public class FrontendUiPlaywrightTests : IClassFixture<CbsApiServerFixture>
{
    private readonly CbsApiServerFixture _fixture;

    public FrontendUiPlaywrightTests(CbsApiServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PlaywrightUI_LoadsIndexPage_HasS7BrandingAndStatus()
    {
        await using var browser = await _fixture.PlaywrightInstance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        var response = await page.GotoAsync(_fixture.ServerAddress);
        response.Should().NotBeNull();
        response!.Status.Should().Be(200);

        // Verify Page Title & S7 Brand
        var title = await page.TitleAsync();
        title.Should().Contain("S7 CBS");

        var brandText = await page.Locator(".brand-title").InnerTextAsync();
        brandText.Should().Contain("S7 CBS Core Banking");
    }

    [Fact]
    public async Task PlaywrightUI_ThemeToggle_SwitchesDarkAndLightMode()
    {
        await using var browser = await _fixture.PlaywrightInstance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync(_fixture.ServerAddress);

        // Initial theme should be 'dark'
        var initialTheme = await page.Locator("html").GetAttributeAsync("data-theme");
        initialTheme.Should().Be("dark");

        // Click Theme Toggle Button
        await page.Locator("#theme-toggle-btn").ClickAsync();

        // Theme should now be 'light'
        var toggledTheme = await page.Locator("html").GetAttributeAsync("data-theme");
        toggledTheme.Should().Be("light");

        // Toggle back
        await page.Locator("#theme-toggle-btn").ClickAsync();
        var revertedTheme = await page.Locator("html").GetAttributeAsync("data-theme");
        revertedTheme.Should().Be("dark");
    }

    [Fact]
    public async Task PlaywrightUI_LanguageToggle_SwitchesBetweenEnglishAndBengali()
    {
        await using var browser = await _fixture.PlaywrightInstance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync(_fixture.ServerAddress);

        // Default language is English (EN)
        var initialHeader = await page.Locator("h1[data-i18n='nav.dashboard']").InnerTextAsync();
        initialHeader.Should().Be("Overview Dashboard");

        // Click Language Toggle Button (switches to Bengali - BN)
        await page.Locator("#lang-toggle-btn").ClickAsync();

        var bengaliHeader = await page.Locator("h1[data-i18n='nav.dashboard']").InnerTextAsync();
        bengaliHeader.Should().Be("ওভারভিউ ড্যাশবোর্ড");

        var brandNameBengali = await page.Locator(".brand-title").InnerTextAsync();
        brandNameBengali.Should().Contain("এস৭ সিবিএস কোর ব্যাংকিং");

        // Switch back to English
        await page.Locator("#lang-toggle-btn").ClickAsync();
        var englishAgain = await page.Locator("h1[data-i18n='nav.dashboard']").InnerTextAsync();
        englishAgain.Should().Be("Overview Dashboard");
    }

    [Fact]
    public async Task PlaywrightUI_MobileViewport_HamburgerMenuTogglesSidebarDrawer()
    {
        await using var browser = await _fixture.PlaywrightInstance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        
        // Emulate Mobile Device (e.g. iPhone 13 / 390x844)
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 390, Height = 844 }
        });
        var page = await context.NewPageAsync();
        await page.GotoAsync(_fixture.ServerAddress);

        // Hamburger button must be visible on mobile
        var hamburger = page.Locator("#hamburger-btn");
        var isVisible = await hamburger.IsVisibleAsync();
        isVisible.Should().BeTrue();

        // Sidebar should NOT have mobile-open initially
        var sidebar = page.Locator("#sidebar");
        var classesInitial = await sidebar.GetAttributeAsync("class");
        classesInitial.Should().NotContain("mobile-open");

        // Click Hamburger
        await hamburger.ClickAsync();

        // Sidebar must now have mobile-open
        var classesOpen = await sidebar.GetAttributeAsync("class");
        classesOpen.Should().Contain("mobile-open");

        // Drawer backdrop should be active
        var backdrop = page.Locator("#drawer-backdrop");
        var backdropClasses = await backdrop.GetAttributeAsync("class");
        backdropClasses.Should().Contain("active");

        // Press Escape key to close accessible mobile drawer
        await page.Keyboard.PressAsync("Escape");
        var classesClosed = await sidebar.GetAttributeAsync("class");
        classesClosed.Should().NotContain("mobile-open");
    }

    [Fact]
    public async Task PlaywrightUI_DualControl_EnforcesMakerCheckerSegregation()
    {
        await using var browser = await _fixture.PlaywrightInstance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync(_fixture.ServerAddress);

        // Role is initially MAKER
        var makerBtn = page.Locator("#role-maker-btn");
        var isMakerActive = (await makerBtn.GetAttributeAsync("class"))!.Contains("active");
        isMakerActive.Should().BeTrue();

        // In Maker mode, trying to approve should trigger toast violation error
        var approveBtn = page.Locator("#queue-table-body button.btn-primary").First;
        await approveBtn.ClickAsync();

        // Toast error should appear
        var toastError = page.Locator(".toast.error");
        var errorCount = await toastError.CountAsync();
        errorCount.Should().BeGreaterThan(0);
        var toastText = await toastError.First.InnerTextAsync();
        toastText.Should().Contain("Maker cannot approve");

        // Switch to CHECKER mode
        await page.Locator("#role-checker-btn").ClickAsync();

        // Now authorize the draft
        await approveBtn.ClickAsync();
        var toastSuccess = page.Locator(".toast.success");
        var successText = await toastSuccess.First.InnerTextAsync();
        successText.Should().Contain("ACTIVE");
    }

    [Fact]
    public async Task PlaywrightUI_SidebarCollapse_PreservesIcons()
    {
        await using var browser = await _fixture.PlaywrightInstance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync(_fixture.ServerAddress);

        // Click sidebar toggle
        var toggleBtn = page.Locator("#sidebar-toggle-btn");
        await toggleBtn.ClickAsync();

        var sidebar = page.Locator("#sidebar");
        var isCollapsed = (await sidebar.GetAttributeAsync("class"))!.Contains("collapsed");
        isCollapsed.Should().BeTrue();

        // Icons must still be visible
        var firstIcon = page.Locator(".sidebar .nav-item .nav-icon").First;
        var isIconVisible = await firstIcon.IsVisibleAsync();
        isIconVisible.Should().BeTrue();
    }

    [Fact]
    public async Task PlaywrightUI_GlobalSearch_FiltersTablesDynamically()
    {
        await using var browser = await _fixture.PlaywrightInstance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync(_fixture.ServerAddress);

        var searchInput = page.Locator("#global-search");
        await searchInput.FillAsync("Nazrul");

        var rows = page.Locator("#queue-table-body tr");
        var count = await rows.CountAsync();
        count.Should().Be(1);
        var text = await rows.First.InnerTextAsync();
        text.Should().Contain("Kazi Nazrul Islam");
    }

    [Fact]
    public async Task PlaywrightUI_DedicatedViews_RenderPopulatedTables()
    {
        await using var browser = await _fixture.PlaywrightInstance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync(_fixture.ServerAddress);

        // Check full queue table on dedicated view
        var fullRows = page.Locator("#queue-table-body-full tr");
        var count = await fullRows.CountAsync();
        count.Should().BeGreaterThan(0);

        // Check full audit log on dedicated view
        var auditRows = page.Locator("#audit-log-list-full tr");
        var auditCount = await auditRows.CountAsync();
        auditCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PlaywrightUI_Scrollbars_AreConfiguredWithCustomDesignTokens()
    {
        await using var browser = await _fixture.PlaywrightInstance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync(_fixture.ServerAddress);

        // Verify scrollbar-gutter is stable on html root
        var scrollbarGutter = await page.EvaluateAsync<string>(
            "() => window.getComputedStyle(document.documentElement).scrollbarGutter"
        );
        scrollbarGutter.Should().Be("stable");

        // Verify custom scrollbar tokens in CSS
        var trackColor = await page.EvaluateAsync<string>(
            "() => window.getComputedStyle(document.documentElement).getPropertyValue('--scrollbar-track').trim()"
        );
        trackColor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PlaywrightUI_Pagination_NavigatesPagesAndUpdatesRowCount()
    {
        await using var browser = await _fixture.PlaywrightInstance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync(_fixture.ServerAddress);

        // Queue pagination container should exist
        var pagContainer = page.Locator("#queue-pagination");
        (await pagContainer.IsVisibleAsync()).Should().BeTrue();

        // Page 1 should display exactly 5 rows (pageSize = 5)
        var rowsPage1 = page.Locator("#queue-table-body tr");
        (await rowsPage1.CountAsync()).Should().Be(5);

        // Summary text should state "Showing 1 to 5 of 12 records"
        var summaryText = await page.Locator("#queue-pagination .pagination-summary").InnerTextAsync();
        summaryText.Should().Contain("Showing");
        summaryText.Should().Contain("1");
        summaryText.Should().Contain("5");
        summaryText.Should().Contain("12");

        // Click Page 2 button
        var page2Btn = page.Locator("#queue-pagination .page-btn", new() { HasText = "2" });
        await page2Btn.ClickAsync();

        // Page 2 should display 5 rows (items 6 to 10)
        var rowsPage2 = page.Locator("#queue-table-body tr");
        (await rowsPage2.CountAsync()).Should().Be(5);

        var summaryPage2 = await page.Locator("#queue-pagination .pagination-summary").InnerTextAsync();
        summaryPage2.Should().Contain("6");
        summaryPage2.Should().Contain("10");

        // Click Page 3 button
        var page3Btn = page.Locator("#queue-pagination .page-btn", new() { HasText = "3" });
        await page3Btn.ClickAsync();

        // Page 3 should display remaining 2 rows (items 11 to 12)
        var rowsPage3 = page.Locator("#queue-table-body tr");
        (await rowsPage3.CountAsync()).Should().Be(2);

        var summaryPage3 = await page.Locator("#queue-pagination .pagination-summary").InnerTextAsync();
        summaryPage3.Should().Contain("11");
        summaryPage3.Should().Contain("12");
    }

    [Fact]
    public async Task PlaywrightUI_Pagination_ChangesPageSize()
    {
        await using var browser = await _fixture.PlaywrightInstance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync(_fixture.ServerAddress);

        // Select page size 10
        await page.Locator("#queue-pagination-pagesize").SelectOptionAsync("10");

        // Page 1 should now display 10 rows
        var rows = page.Locator("#queue-table-body tr");
        (await rows.CountAsync()).Should().Be(10);

        var summary = await page.Locator("#queue-pagination .pagination-summary").InnerTextAsync();
        summary.Should().Contain("1");
        summary.Should().Contain("10");
        summary.Should().Contain("12");
    }

    [Fact]
    public async Task PlaywrightUI_Pagination_SupportsBengaliNumeralsAndLabels()
    {
        await using var browser = await _fixture.PlaywrightInstance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync(_fixture.ServerAddress);

        // Switch to Bengali
        await page.Locator("#lang-toggle-btn").ClickAsync();

        // Summary should display Bengali text and numerals
        var summary = await page.Locator("#queue-pagination .pagination-summary").InnerTextAsync();
        summary.Should().Contain("প্রদর্শিত");
        summary.Should().Contain("১");
        summary.Should().Contain("৫");
        summary.Should().Contain("১২");

        // Page buttons should contain Bengali numbers: ১, ২, ৩
        var pageBtns = page.Locator("#queue-pagination .page-buttons button.page-btn");
        var btnTexts = await pageBtns.AllInnerTextsAsync();
        btnTexts.Should().Contain("১");
        btnTexts.Should().Contain("২");
        btnTexts.Should().Contain("৩");
    }

    [Fact]
    public async Task PlaywrightUI_AiAdvisor_NavigatesToView_ShowsLivePiiSanitization_AndSubmitsGroundedQuery()
    {
        await using var browser = await _fixture.PlaywrightInstance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync(_fixture.ServerAddress);

        // 1. Click on AI Advisor navigation item
        await page.Locator("a[data-view='view-ai-advisory']").ClickAsync();

        // 2. View 8 should be visible
        var isVisible = await page.Locator("#view-ai-advisory").IsVisibleAsync();
        isVisible.Should().BeTrue();

        // 3. Fill query input with raw account and NID
        var input = page.Locator("#ai-query-input");
        await input.FillAsync("Explain CAD vetting guidelines for customer account 0101101000015 holding NID 19852692511000123 under BRPD 12.");

        // 4. Verify client-side PII sanitization preview updates live
        var sanitizedPreview = await page.Locator("#ai-sanitized-text").InnerTextAsync();
        sanitizedPreview.Should().NotContain("0101101000015");
        sanitizedPreview.Should().Contain("ACCT-***-0015");
        sanitizedPreview.Should().NotContain("19852692511000123");
        sanitizedPreview.Should().Contain("NID-***-0123");

        // 5. Submit query to Gemini Advisor
        await page.Locator("#ai-ask-btn").ClickAsync();

        // 6. Response container should appear with grounded citations
        var resultBox = page.Locator("#ai-advisory-result");
        await resultBox.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });

        var responseText = await page.Locator("#ai-response-text").InnerTextAsync();
        responseText.Should().Contain("Gemini Regulatory Advisor");

        var citations = page.Locator("#ai-citations-list > div");
        var count = await citations.CountAsync();
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PlaywrightUI_TbmlScreener_CalculatesVariance_AndEnforcesThresholdPill()
    {
        await using var browser = await _fixture.PlaywrightInstance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync(_fixture.ServerAddress);

        // 1. Navigate to AI view
        await page.Locator("a[data-view='view-ai-advisory']").ClickAsync();

        // 2. Enter declared price and benchmark
        await page.Locator("#tbml-declared-price").FillAsync("4.50");
        await page.Locator("#tbml-benchmark-price").FillAsync("3.50");

        // Trigger input event
        await page.Locator("#tbml-declared-price").DispatchEventAsync("input");

        // 3. Check calculated variance pill
        var varianceText = await page.Locator("#tbml-variance-pct").InnerTextAsync();
        varianceText.Should().Contain("+28.57%");

        var pillText = await page.Locator("#tbml-recommendation-pill").InnerTextAsync();
        pillText.Should().Be("EscalateToBfiu");

        // 4. Run full assessment
        await page.Locator("#tbml-screen-btn").ClickAsync();
        var toast = page.Locator(".toast.success");
        await toast.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        var toastText = await toast.InnerTextAsync();
        toastText.Should().Contain("TBML Assessment Complete");
    }

    [Fact]
    public async Task PlaywrightUI_CaptureDashboardScreenshots()
    {
        var artifactDir = @"C:\Users\sagir\.gemini\antigravity\brain\689a18f3-20e0-465a-a3d2-055bbebe1c9b";
        await using var browser = await _fixture.PlaywrightInstance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });

        // 1. Desktop 1920x1080 Dark
        var ctxDesktop = await browser.NewContextAsync(new BrowserNewContextOptions { ViewportSize = new ViewportSize { Width = 1920, Height = 1080 } });
        var pageDesktop = await ctxDesktop.NewPageAsync();
        await pageDesktop.GotoAsync(_fixture.ServerAddress);
        await pageDesktop.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await pageDesktop.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactDir, "dashboard_desktop_dark.png"), FullPage = true });

        // 2. Desktop 1920x1080 Light
        await pageDesktop.Locator("#theme-toggle-btn").ClickAsync();
        await pageDesktop.WaitForTimeoutAsync(300);
        await pageDesktop.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactDir, "dashboard_desktop_light.png"), FullPage = true });

        // 3. Laptop 1366x768 Dark
        var ctxLaptop = await browser.NewContextAsync(new BrowserNewContextOptions { ViewportSize = new ViewportSize { Width = 1366, Height = 768 } });
        var pageLaptop = await ctxLaptop.NewPageAsync();
        await pageLaptop.GotoAsync(_fixture.ServerAddress);
        await pageLaptop.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await pageLaptop.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactDir, "dashboard_laptop_dark.png"), FullPage = true });

        // 4. Mobile 390x844 Dark
        var ctxMobile = await browser.NewContextAsync(new BrowserNewContextOptions { ViewportSize = new ViewportSize { Width = 390, Height = 844 } });
        var pageMobile = await ctxMobile.NewPageAsync();
        await pageMobile.GotoAsync(_fixture.ServerAddress);
        await pageMobile.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await pageMobile.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactDir, "dashboard_mobile_dark.png"), FullPage = true });
    }
}
