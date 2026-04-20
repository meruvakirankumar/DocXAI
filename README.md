# DocXAI — AI-Powered Document & Test Automation Engine

**DocXAI** is a web-based automation platform built with **.NET 10** and **Clean (Onion) Architecture** that transforms raw design documents into structured functional specifications and executable Playwright test suites — entirely powered by Google Cloud and Gemini AI.

---

## What It Does

Upload a design document (`.docx`, `.md`, or any text file) through the browser UI and DocXAI:

1. **Uploads** the file to Google Cloud Storage under `{SolutionName}/{filename}`
2. **Reads** and extracts text from the design document (including full `.docx` parsing)
3. **Generates** a Functional Specification using Vertex AI (Gemini 2.0 Flash)
4. **Saves** the spec as a versioned `.docx` file → `{SolutionName}/functional/{SolutionName}_functional_v1.docx`
5. **Displays** the spec in a rich Markdown viewer in the browser — with copy and Word download options
6. **On demand** (user clicks "Generate Test Cases"): generates a full Playwright TypeScript test suite from the spec
7. **Saves** the test file with versioning → `{SolutionName}/test cases/{SolutionName}_testcases_v1.spec.ts`
8. **Displays** the test script in a syntax-highlighted TypeScript viewer with download and copy options
9. **Triggers** a Cloud Build job to execute the generated Playwright tests in a Docker container

The user retains full control: functional spec generation is automatic; test case generation is on-demand via a dedicated button.

---

## Architecture

### Clean (Onion) Architecture — 4 Projects

```
GCA- doc AI - 2026.sln
├── AutomationEngineService              ← API + Web host (Cloud Run entry point)
│   ├── Controllers/OrchestratorController.cs
│   ├── wwwroot/                         ← Single-page browser UI
│   │   ├── index.html
│   │   ├── css/app.css
│   │   └── js/app.js
│   └── Program.cs
└── src/
    ├── AutomationEngine.Domain          ← Entities, interfaces (no dependencies)
    ├── AutomationEngine.Application     ← Use cases, DTOs, pipeline options
    └── AutomationEngine.Infrastructure  ← Google Cloud service adapters
```

### Layer Responsibilities

| Layer | Responsibility |
|---|---|
| **Domain** | Core entities (`DesignDocument`, `FunctionalSpec`, `TestScript`, `BuildJob`) and port interfaces |
| **Application** | `ProcessDocumentUseCase` — two entry points: `ExecuteAsync` (spec generation) and `GenerateTestsAsync` (test generation) |
| **Infrastructure** | Adapters for Vertex AI, Cloud Storage, Cloud Build, Secret Manager, and OpenXML serialisation |
| **API / Web** | ASP.NET Core controller + static-file SPA frontend |

---

## Process Flow

```
User uploads design document via Browser UI
          │
          ▼
  POST /api/orchestrator/upload
  (file + solutionName form fields)
          │
          ├─► Save to GCS: {SolutionName}/{filename}
          │
          ├─► Extract text (plain text or .docx via OpenXML)
          │
          ├─► Vertex AI (Gemini 2.0 Flash) ──► Functional Specification (Markdown)
          │
          ├─► Serialize to .docx (OpenXML)
          │
          ├─► Save to GCS: {SolutionName}/functional/{SolutionName}_functional_v1.docx
          │                                             (auto-increments: v2, v3 …)
          │
          └─► Return spec content to browser → Markdown viewer
                    │
                    │  [User clicks "Generate Test Cases"]
                    ▼
          POST /api/orchestrator/generate-tests
                    │
                    ├─► Vertex AI (Gemini 2.0 Flash) ──► Playwright test script (TypeScript)
                    │
                    ├─► Save to GCS: {SolutionName}/test cases/{SolutionName}_testcases_v1.spec.ts
                    │                                                (auto-increments: v2, v3 …)
                    │
                    ├─► Cloud Build ──► Execute Playwright tests in Docker
                    │         └─► Cloud Logging (pass/fail results)
                    │
                    └─► Return test content to browser → Syntax-highlighted TypeScript viewer
```

---

## File Naming & Versioning

| Artifact | GCS Path | Example |
|---|---|---|
| Uploaded design doc | `{SolutionName}/{filename}` | `MyApp/design.docx` |
| Functional spec | `{SolutionName}/functional/{SolutionName}_functional_v{N}.docx` | `MyApp/functional/MyApp_functional_v1.docx` |
| Test script | `{SolutionName}/test cases/{SolutionName}_testcases_v{N}.spec.ts` | `MyApp/test cases/MyApp_testcases_v1.spec.ts` |

Versioning is automatic: the system lists existing files in the target GCS folder and picks the next free `v{N}` slot, so re-running never overwrites previous outputs.

---

## Google Cloud APIs Used

| Service | Purpose |
|---|---|
| **Vertex AI (Gemini 2.0 Flash)** | Generates functional specifications and Playwright test scripts |
| **Cloud Run** | Hosts the ASP.NET Core service and browser UI |
| **Cloud Build** | Executes the generated Playwright tests inside a Docker container |
| **Cloud Storage** | Stores uploaded design docs, generated specs, and test scripts |
| **Secret Manager** | Securely stores service account credentials |
| **Eventarc** | (Optional) Triggers the pipeline automatically on GCS file upload |

---

## Project Structure

```
├── Controllers/
│   └── OrchestratorController.cs       # All API endpoints
├── Models/
│   └── CloudStorageEvent.cs
├── wwwroot/
│   ├── index.html                      # Single-page browser UI
│   ├── css/app.css
│   └── js/app.js                       # All frontend logic (vanilla JS IIFE)
├── src/
│   ├── AutomationEngine.Domain/
│   │   ├── Entities/
│   │   │   ├── DesignDocument.cs
│   │   │   ├── FunctionalSpec.cs       # Derives versioned output path
│   │   │   ├── TestScript.cs           # Derives versioned output path
│   │   │   ├── BuildJob.cs
│   │   │   └── ProcessingContext.cs
│   │   └── Interfaces/
│   │       ├── IAIGenerationService.cs
│   │       ├── IBuildService.cs
│   │       ├── IDocumentSerializer.cs
│   │       ├── ISecretService.cs
│   │       └── IStorageRepository.cs
│   ├── AutomationEngine.Application/
│   │   ├── DTOs/
│   │   │   ├── StorageEventDto.cs
│   │   │   └── GenerationResultDto.cs  # Carries TestScriptContent back to UI
│   │   ├── Options/
│   │   │   └── ProcessDocumentOptions.cs
│   │   └── UseCases/
│   │       ├── IProcessDocumentUseCase.cs
│   │       └── ProcessDocumentUseCase.cs  # ExecuteAsync + GenerateTestsAsync
│   └── AutomationEngine.Infrastructure/
│       └── GoogleCloud/
│           ├── AI/GcpVertexAIService.cs
│           ├── Build/GcpCloudBuildService.cs
│           ├── Documents/OpenXmlDocumentSerializer.cs
│           ├── Secrets/GcpSecretManagerService.cs
│           └── Storage/GcpCloudStorageService.cs
├── Program.cs
├── appsettings.json
├── Dockerfile
└── cloudbuild.yaml
```

---

## API Endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/orchestrator/upload` | Upload design doc + solution name → run spec generation pipeline |
| `POST` | `/api/orchestrator/generate-tests` | Generate Playwright tests from an existing functional spec |
| `GET` | `/api/orchestrator/test-content?path=…` | Fetch raw test-script content from GCS by path |
| `POST` | `/api/orchestrator/save-docx` | Convert Markdown spec content to a `.docx` file download |
| `POST` | `/api/orchestrator/trigger` | Eventarc entry point (triggered automatically on GCS file upload) |
| `GET` | `/api/orchestrator/health` | Health check for Cloud Run probes |

### Upload Response

```json
{
  "success": true,
  "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "functionalSpecPath": "MyApp/functional/MyApp_functional_v1.docx",
  "testScriptPath": null,
  "buildJobId": null,
  "functionalSpecContent": "## Functional Specification\n…",
  "errorMessage": null
}
```

### Generate Tests Response

```json
{
  "success": true,
  "correlationId": "7ab12c34-…",
  "functionalSpecPath": "MyApp/functional/MyApp_functional_v1.docx",
  "testScriptPath": "MyApp/test cases/MyApp_testcases_v1.spec.ts",
  "testScriptContent": "import { test, expect } from '@playwright/test';\n…",
  "buildJobId": "abc123",
  "buildLogUrl": "https://console.cloud.google.com/cloud-build/builds/abc123",
  "errorMessage": null
}
```

---

## Browser UI Features

| Feature | Description |
|---|---|
| Drag-and-drop upload | Drop any `.docx`, `.md`, or text file onto the upload zone |
| Solution Name | Groups all artefacts under a named folder in GCS |
| Functional Spec viewer | Renders the generated spec as formatted Markdown |
| Save as Word | Downloads the spec as a `.docx` file |
| Copy | Copies the spec Markdown to clipboard |
| Generate Test Cases | On-demand button — calls Gemini to produce Playwright tests |
| View Test Cases | Reappears after generation so the user can navigate back at any time |
| Syntax-highlighted viewer | Displays the `.spec.ts` file with Atom One Dark theme (highlight.js) |
| Download `.spec.ts` | Client-side download of the test file |
| Back to Spec / New Upload | Navigation between panels; New Upload resets all state |

---

## Running Locally

```bash
# Authenticate with GCP (Application Default Credentials)
gcloud auth application-default login

# Start the service
dotnet run --project AutomationEngineService.csproj
```

Open `http://localhost:8081` in your browser. The health check is at:

```
GET http://localhost:8081/api/orchestrator/health
```

---

## Deploying to Cloud Run

```bash
gcloud builds submit --config cloudbuild.yaml \
  --substitutions \
    _REGION=us-central1,\
    _ARTIFACT_REGISTRY=us-central1-docker.pkg.dev/YOUR_PROJECT/automation-engine,\
    _SERVICE_NAME=automation-engine,\
    _SERVICE_ACCOUNT=automation-engine-sa@YOUR_PROJECT.iam.gserviceaccount.com
```

---

## Technology Stack

| Component | Technology |
|---|---|
| Runtime | .NET 10, ASP.NET Core |
| Architecture | Clean / Onion Architecture |
| AI Model | Vertex AI — Gemini 2.0 Flash (`gemini-2.0-flash-001`) |
| Hosting | Google Cloud Run |
| CI/CD | Google Cloud Build + `cloudbuild.yaml` |
| Storage | Google Cloud Storage |
| Secrets | Google Secret Manager |
| Test Framework | Playwright (TypeScript, `.spec.ts`) |
| Document Format | OpenXML `.docx` (DocumentFormat.OpenXml) |
| Frontend | Vanilla JS, marked.js (Markdown), highlight.js (syntax) |
| Logging | Structured JSON → Google Cloud Logging |
| Auth | Application Default Credentials (ADC) / Service Account JSON |
