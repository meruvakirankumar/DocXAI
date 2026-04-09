using AutomationEngine.Domain.Interfaces;
using AutomationEngine.Infrastructure.Options;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.AIPlatform.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutomationEngine.Infrastructure.GoogleCloud.AI;

public sealed class GcpVertexAIService : IAIGenerationService
{
    private readonly PredictionServiceClient _client;
    private readonly GoogleCloudOptions _options;
    private readonly ILogger<GcpVertexAIService> _logger;

    public GcpVertexAIService(
        GoogleCredential credential,
        IOptions<GoogleCloudOptions> options,
        ILogger<GcpVertexAIService> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Credential loaded from Secret Manager (or ADC in local dev)
        _client = new PredictionServiceClientBuilder
        {
            Endpoint = $"{_options.Location}-aiplatform.googleapis.com",
            Credential = credential
        }.Build();
    }

    // Full Vertex AI publisher model resource name
    private string ModelName =>
        $"projects/{_options.ProjectId}/locations/{_options.Location}" +
        $"/publishers/google/models/{_options.GeminiModelId}";

    public async Task<string> GenerateFunctionalSpecAsync(string designDocumentContent, CancellationToken ct = default)
    {
        _logger.LogInformation("Generating Functional Specification via Vertex AI model {Model}", ModelName);
        return await GenerateAsync(BuildFunctionalSpecPrompt(designDocumentContent), ct);
    }

    public async Task<string> GeneratePlaywrightTestsAsync(string functionalSpecContent, CancellationToken ct = default)
    {
        _logger.LogInformation("Generating Playwright test script via Vertex AI model {Model}", ModelName);
        return await GenerateAsync(BuildPlaywrightTestsPrompt(functionalSpecContent), ct);
    }

    private async Task<string> GenerateAsync(string prompt, CancellationToken ct)
    {
        var request = new GenerateContentRequest
        {
            Model = ModelName,
            GenerationConfig = new GenerationConfig
            {
                Temperature = 0.7f,
                MaxOutputTokens = 8192
            },
            Contents =
            {
                new Content
                {
                    Role = "user",
                    Parts = { new Part { Text = prompt } }
                }
            }
        };

        var response = await _client.GenerateContentAsync(request, cancellationToken: ct);

        var text = response.Candidates
                       .FirstOrDefault()
                       ?.Content
                       ?.Parts
                       .FirstOrDefault()
                       ?.Text
                   ?? throw new InvalidOperationException(
                       $"Vertex AI returned no content from model {ModelName}.");

        _logger.LogInformation("Vertex AI response received. OutputLength={Length}", text.Length);
        return text;
    }

    // ── Prompt builders ──────────────────────────────────────────────────────

    private static string BuildFunctionalSpecPrompt(string designDocContent) => $"""
        You are a senior technical writer and solutions architect.
        Given the following design document, produce a comprehensive Functional Specification.

        The document must include:
        1. Executive Summary
        2. System Overview and Architecture
        3. Functional Requirements (numbered, detailed)
        4. Non-Functional Requirements (performance, security, scalability)
        5. API Specifications (endpoints, request/response schemas)
        6. Data Models and Schemas
        7. Error Handling and Logging Strategy
        8. Security Considerations (OWASP threat mitigations)
        9. Integration Points
        10. Acceptance Criteria for each requirement

        Design Document:
        ---
        {designDocContent}
        ---

        Output the Functional Specification in structured Markdown format.
        Be thorough, precise, and production-ready.
        """;

    private static string BuildPlaywrightTestsPrompt(string functionalSpecContent) => $"""
        You are a senior QA automation engineer specialising in Playwright and TypeScript.
        Given the Functional Specification below, generate a complete Playwright test suite.

        Requirements:
        1. Use TypeScript with the @playwright/test framework
        2. Implement the Page Object Model (POM) pattern
        3. Cover ALL functional requirements with individual test cases
        4. Include positive (happy path) and negative (error-path) scenarios
        5. Use descriptive names and nest tests with describe() blocks
        6. Include beforeAll / afterAll / beforeEach hooks for setup and teardown
        7. Use meaningful expect() assertions
        8. Include API-level tests for every specified endpoint
        9. Test authentication and authorisation scenarios
        10. Add retry logic for potentially flaky operations

        Functional Specification:
        ---
        {functionalSpecContent}
        ---

        Output ONLY the TypeScript code — a single .spec.ts file executable via `npx playwright test`.
        No explanatory prose; pure code only.
        """;
}
