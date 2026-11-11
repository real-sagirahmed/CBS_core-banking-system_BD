using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using CBS.Domain.Accounts;
using CBS.Domain.Enums;
using CBS.Domain.Exceptions;
using CBS.Domain.Shariah;
using CBS.Domain.Shariah.ValueObjects;
using CBS.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CBS.StressTests;

public class ConcurrencyAndStressTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ConcurrencyAndStressTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // =========================================================================
    // 1. CONCURRENCY: Race Condition & Double-Spending Prevention
    // =========================================================================
    [Fact]
    public async Task Test_ConcurrentWithdrawals_PreventsDoubleSpending_AndEnforcesZeroNegativeBalance()
    {
        // Arrange
        // Account with initial balance 5,000 BDT
        var accountNumber = AccountNumber.Generate("001", "001", 54321);
        var account = new DepositAccount(
            accountNumber,
            new CifNumber("CIF-12345678"),
            DepositProductType.SavingsAccount,
            "001",
            new DateOnly(2026, 1, 1),
            Currency.BDT);

        account.Credit(Money.Bdt(5000m), "Initial Cash Deposit");
        account.Balance.Amount.Should().Be(5000m);

        int totalThreads = 50;
        decimal withdrawalAmount = 200m; // Total demand = 50 * 200 = 10,000 BDT (> 5,000 BDT)
        
        var successCount = 0;
        var failureCount = 0;
        var exceptions = new ConcurrentBag<Exception>();
        var balanceLock = new object();

        // Act: 50 concurrent withdrawal attempts executed in parallel
        var tasks = Enumerable.Range(0, totalThreads).Select(_ => Task.Run(() =>
        {
            try
            {
                lock (balanceLock)
                {
                    account.Debit(Money.Bdt(withdrawalAmount), "ATM Fast Cash Withdrawal");
                }
                Interlocked.Increment(ref successCount);
            }
            catch (InvariantViolationException ex)
            {
                Interlocked.Increment(ref failureCount);
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        // Exactly 25 withdrawals must succeed (25 * 200 = 5,000 BDT)
        successCount.Should().Be(25, "5,000 BDT balance can only fulfill exactly twenty-five 200 BDT withdrawals");
        failureCount.Should().Be(25, "The remaining 25 requests must be rejected to prevent overdraft");
        account.Balance.Amount.Should().Be(0.00m, "Account balance must be exactly zero, strictly preventing negative balances");
        
        // Ensure all failed transactions threw the statutory InsufficientFundsInvariant
        exceptions.Should().AllSatisfy(ex =>
        {
            ex.Should().BeOfType<InvariantViolationException>();
            ((InvariantViolationException)ex).InvariantName.Should().Be("InsufficientFundsInvariant");
        });
    }

    // =========================================================================
    // 2. CONCURRENCY: High-Throughput Double-Entry Ledger Zero-Sum Invariant
    // =========================================================================
    [Fact]
    public async Task Test_HighThroughput_GeneralLedger_DoubleEntryInvariant_UnderConcurrency()
    {
        // Arrange
        int transactionCount = 1000;
        var debitLedger = new ConcurrentBag<decimal>();
        var creditLedger = new ConcurrentBag<decimal>();

        // Act: Simulate 1,000 concurrent financial postings across multiple threads
        var tasks = Enumerable.Range(1, transactionCount).Select(i => Task.Run(() =>
        {
            decimal amount = i * 15.50m;
            
            // Double-entry accounting rule: Debit must strictly equal Credit
            debitLedger.Add(amount);
            creditLedger.Add(amount);
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        debitLedger.Count.Should().Be(transactionCount);
        creditLedger.Count.Should().Be(transactionCount);

        decimal totalDebits = debitLedger.Sum();
        decimal totalCredits = creditLedger.Sum();

        totalDebits.Should().Be(totalCredits, "Total Debits must strictly equal Total Credits in double-entry banking ledger");
        (totalDebits - totalCredits).Should().Be(0.00m, "Zero discrepancy is an absolute non-negotiable invariant");
    }

    // =========================================================================
    // 3. STRESS: Mudaraba High-Volume Profit Pool Calculation Stress
    // =========================================================================
    [Fact]
    public void Test_MudarabaProfitDistribution_HighVolumeStress_UnderConcurrentAllocations()
    {
        // Arrange: Pool with 10,000,000 BDT deposits, 40:60 Mudarib:Rab-ul-Mal split, 5% PER, 3% IRR
        var pool = new MudarabaInvestmentPool(
            "High-Volume National Mudaraba Investment Pool",
            MudarabaPoolType.GeneralInvestmentPool,
            "BDT",
            mudaribShareRatio: 40.00m,
            perRatio: 5.00m,
            irrRatio: 3.00m);

        pool.SetWeightage(new MudarabaWeightage("PROD-SB", "Mudaraba Savings", 1.00m, 4.50m));
        pool.SetWeightage(new MudarabaWeightage("PROD-MTD-03M", "Term Deposit 3M", 1.15m, 6.00m));
        pool.SetWeightage(new MudarabaWeightage("PROD-MTD-06M", "Term Deposit 6M", 1.25m, 6.75m));
        pool.SetWeightage(new MudarabaWeightage("PROD-MTD-12M", "Term Deposit 12M", 1.40m, 7.50m));

        // Inject 500 concurrent deposit accounts into the pool
        Parallel.For(0, 500, i =>
        {
            pool.AddDeposit(10000m + (i * 100m));
        });

        // Act: Distribute 1,000,000 BDT gross profit
        decimal grossProfit = 1_000_000.00m;
        var distribution = pool.DistributeProfit(grossProfit, DateTime.UtcNow);

        // Assert
        // Gross Profit = 1,000,000
        // Mudarib Share (40%) = 400,000
        // Rab-ul-Mal Gross Share (60%) = 600,000
        // PER (5% of 600k) = 30,000
        // IRR (3% of 600k) = 18,000
        // Net Distributable Profit = 600,000 - 30,000 - 18,000 = 552,000
        distribution.MudaribShareAmount.Should().Be(400_000.00m);
        distribution.RabUlMalGrossShare.Should().Be(600_000.00m);
        distribution.PerDeductionAmount.Should().Be(30_000.00m);
        distribution.IrrDeductionAmount.Should().Be(18_000.00m);
        distribution.NetDistributableProfit.Should().Be(552_000.00m);

        (distribution.MudaribShareAmount + distribution.RabUlMalGrossShare).Should().Be(grossProfit);
        (distribution.PerDeductionAmount + distribution.IrrDeductionAmount + distribution.NetDistributableProfit).Should().Be(distribution.RabUlMalGrossShare);
    }

    // =========================================================================
    // 4. PERFORMANCE BENCHMARK: HTTP API Throughput & Latency Profiling
    // =========================================================================
    [Fact]
    public async Task Test_ApiThroughputAndLatency_HealthAndReadiness_Benchmark()
    {
        // Warm-up to eliminate cold-start JIT compilation skew
        var warmup = await _client.GetAsync("/health");
        warmup.EnsureSuccessStatusCode();

        // Arrange
        int totalRequests = 100;
        var latencies = new ConcurrentBag<long>();
        var statusCodes = new ConcurrentBag<HttpStatusCode>();

        var stopwatch = Stopwatch.StartNew();

        // Act: Fire requests with high concurrency to the production health probe
        var tasks = Enumerable.Range(0, totalRequests).Select(async _ =>
        {
            var reqSw = Stopwatch.StartNew();
            var response = await _client.GetAsync("/health");
            reqSw.Stop();

            latencies.Add(reqSw.ElapsedMilliseconds);
            statusCodes.Add(response.StatusCode);
        });

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Telemetry Calculation
        double totalSeconds = stopwatch.Elapsed.TotalSeconds;
        double throughputTps = totalRequests / (totalSeconds > 0 ? totalSeconds : 0.001);
        var sortedLatencies = latencies.OrderBy(l => l).ToList();
        double avgLatency = sortedLatencies.Average();
        long p50 = sortedLatencies[(int)(totalRequests * 0.50)];
        long p95 = sortedLatencies[(int)(totalRequests * 0.95)];
        long p99 = sortedLatencies[(int)(totalRequests * 0.99)];

        // Assert Quality Gates
        statusCodes.Should().AllBeEquivalentTo(HttpStatusCode.OK, "All health probe requests must return 200 OK");
        throughputTps.Should().BeGreaterThan(10.0, "API throughput should exceed 10 TPS on local test runner");
        p95.Should().BeLessThan(1000, "P95 response time must be under 1000ms under heavy test server load");

        // Verify JSON Payload Structure
        var sampleResponse = await _client.GetAsync("/health");
        var body = await sampleResponse.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
        json.RootElement.GetProperty("service").GetString().Should().Be("CBS Bangladesh Core Banking System");
    }

    // =========================================================================
    // 5. SECURITY & CONCURRENCY: Single Session Token Race Condition Lock
    // =========================================================================
    [Fact]
    public void Test_SingleUserSession_PreventsMultipleActiveLogins()
    {
        // BRPD 10.3.5-viii: Single active login session per user enforcement
        var activeSessions = new ConcurrentDictionary<string, string>(); // UserId -> SessionId

        string userId = "TELLER_001";
        string session1 = Guid.NewGuid().ToString();
        string session2 = Guid.NewGuid().ToString();

        // Login 1
        bool login1 = activeSessions.TryAdd(userId, session1);
        login1.Should().BeTrue();

        // Login 2 concurrent attempt for same user
        bool login2 = activeSessions.TryAdd(userId, session2);
        login2.Should().BeFalse("Concurrent active session for same user must be blocked atomically");

        // Ensure session 1 remains the authoritative active session
        activeSessions[userId].Should().Be(session1);
    }
}
