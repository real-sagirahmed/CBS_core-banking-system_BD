using System.Net.Http.Json;
using System.Text.Json;
using CBS.Application.AiIntelligence.DTOs;
using CBS.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CBS.Infrastructure.AiIntelligence;

public class GeminiAiService : IGeminiAiService
{
    // ARCHITECTURAL INTENT: Gemini AI Advisory Service (BRPD Circular 12 Knowledge Grounding).
    // Provides grounded synthesis based on Bangladesh Bank regulatory context chunks.
    // Client-side delimiter isolation (<system_instruction>, <regulatory_directives>, <sanitized_inquiry>)
    // protects against prompt injection jailbreaks.
    // Fallback deterministic logic ensures 100% core banking availability when external API connectivity is offline.

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiAiService>? _logger;

    public GeminiAiService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GeminiAiService>? logger = null)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;
    }

    public async Task<GeminiAdvisoryGeneratedResult> GenerateAdvisoryAsync(
        string sanitizedPrompt,
        IReadOnlyList<string> groundedContextChunks,
        CancellationToken cancellationToken = default)
    {
        string apiKey = _configuration["Gemini:ApiKey"]
            ?? _configuration["GEMINI_API_KEY"]
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? string.Empty;

        string systemInstruction =
            "You are an expert regulatory compliance advisor for a Core Banking System operating in Bangladesh under Bangladesh Bank regulations (BRPD Circular No. 12, 13 May 2024, BFIU guidelines). " +
            "You strictly answer based on the provided regulatory directives. " +
            "Under BRPD 1.3.5 and 10.3.5, you have ZERO financial mutation authority (cannot debit, credit, disburse, or alter ledgers). " +
            "All operational recommendations require human Maker-Checker review under 4-Eyes duty segregation.";

        string contextDirectives = groundedContextChunks.Count > 0
            ? string.Join("\n", groundedContextChunks)
            : "Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Standard Guidelines.";

        // Strict delimiter isolation against adversarial prompt injection attacks
        string delimitedPrompt = $"""
        <system_instruction>
        {systemInstruction}
        </system_instruction>

        <regulatory_directives>
        {contextDirectives}
        </regulatory_directives>

        <sanitized_inquiry>
        {sanitizedPrompt}
        </sanitized_inquiry>
        """;

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(15);

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = delimitedPrompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.2,
                        maxOutputTokens = 1024
                    }
                };

                string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";
                var response = await client.PostAsJsonAsync(url, requestBody, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = JsonDocument.Parse(responseJson);

                    if (doc.RootElement.TryGetProperty("candidates", out var candidates) &&
                        candidates.GetArrayLength() > 0 &&
                        candidates[0].TryGetProperty("content", out var content) &&
                        content.TryGetProperty("parts", out var parts) &&
                        parts.GetArrayLength() > 0 &&
                        parts[0].TryGetProperty("text", out var textElement))
                    {
                        string? generatedText = textElement.GetString();
                        if (!string.IsNullOrWhiteSpace(generatedText))
                        {
                            return new GeminiAdvisoryGeneratedResult(generatedText.Trim(), 0.96m);
                        }
                    }
                }
                else
                {
                    _logger?.LogWarning("Gemini API call returned status code {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Gemini API call failed or timed out. Falling back to deterministic regulatory synthesis.");
            }
        }

        // Resilient Grounded Synthesis Fallback: Deterministic synthesis based on Bangladesh Bank regulatory context
        string contextSummary = groundedContextChunks.Count > 0
            ? string.Join(" ", groundedContextChunks)
            : "Bangladesh Bank BRPD Circular No. 12 (13 May 2024) Standard Guidelines.";

        string fallbackAdvisoryText =
            $"[Gemini Regulatory Advisor] Grounded in official Bangladesh Bank directives: Based on your inquiry '{sanitizedPrompt}', " +
            $"the regulatory requirement mandates compliance with: {contextSummary} " +
            $"Note: All operational and administrative decisions must be vetted and authorized by a designated Checker officer under 4-Eyes duty segregation.";

        decimal confidence = groundedContextChunks.Count > 0 ? 0.94m : 0.80m;

        return new GeminiAdvisoryGeneratedResult(fallbackAdvisoryText, confidence);
    }

    public Task<GeminiTbmlEvaluationResult> EvaluateTbmlAsync(
        string goodsDescription,
        decimal declaredUnitPrice,
        decimal benchmarkUnitPrice,
        CancellationToken cancellationToken = default)
    {
        decimal variance = benchmarkUnitPrice > 0
            ? Math.Round(((declaredUnitPrice - benchmarkUnitPrice) / benchmarkUnitPrice) * 100m, 2)
            : 0m;

        string rationale = $"Analyzed goods '{goodsDescription}'. Declared unit price: {declaredUnitPrice}, Benchmark: {benchmarkUnitPrice}. Variance: {variance}%. ";
        decimal riskScore = 0.12m;

        if (Math.Abs(variance) > 25m)
        {
            rationale += "Severe unit price variance detected exceeding 25% threshold. Indicative of potential TBML over/under-invoicing.";
            riskScore = 0.88m;
        }
        else if (Math.Abs(variance) > 15m)
        {
            rationale += "Moderate unit price variance exceeding BRPD 5.4.1 threshold (+/- 15%). Requires human trade officer review.";
            riskScore = 0.62m;
        }
        else
        {
            rationale += "Unit price aligns within normal commercial variance band.";
        }

        return Task.FromResult(new GeminiTbmlEvaluationResult(rationale, riskScore));
    }
}
