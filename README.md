# AI-Powered Automation Engine

An AI-powered automation engine built with **.NET 10** and **Onion Architecture** that intelligently bridges the gap between software design, documentation, and quality assurance — entirely on Google Cloud.

---

## What It Does

Upload a design document to Google Cloud Storage and the engine automatically:

1. **Reads** the design document
2. **Generates** a Functional Specification using Vertex AI (Gemini)
3. **Saves** the spec as a `.docx` file back to Cloud Storage
4. **Generates** a complete Playwright test suite (TypeScript) from the spec using Vertex AI (Gemini)
5. **Triggers** a Cloud Build job to execute the tests inside a Docker container
6. **Reports** pass/fail results to Cloud Logging

Zero manual steps. One file upload kicks off the entire pipeline.

---

## Architecture

### Onion Architecture — 4 Projects

```
GCA- doc AI - 2026.sln
├── AutomationEngineService              ← API layer (Cloud Run entry point)
└── src/
    ├── AutomationEngine.Domain          ← Entities, interfaces, enums (no dependencies)
    ├── AutomationEngine.Application     ← Use cases, DTOs, options
    └── AutomationEngine.Infrastructure  ← Google Cloud service implementations
```

### Layer Responsibilities

| Layer | Responsibility |
|---|---|
| **Domain** | Core business entities (`DesignDocument`, `FunctionalSpec`, `TestScript`, `BuildJob`) and port interfaces |
| **Application** | `ProcessDocumentUseCase` orchestrates the full 7-step pipeline |
| **Infrastructure** | Concrete adapters for every Google Cloud API (Storage, Vertex AI, Cloud Build, Secret Manager) |
| **API** | ASP.NET Core controller receives Eventarc CloudEvents and invokes the use case |

---

## Process Flow

```
Developer uploads design_v2.md
          │
          ▼
  Cloud Storage (GCS)
          │ Eventarc trigger (google.cloud.storage.object.v1.finalized)
          ▼
  Cloud Run (AutomationEngineService)
  POST /api/orchestrator/trigger
          │
          ├─► Read design doc from GCS
          │
          ├─► Vertex AI (Gemini) ──► Generate Functional Specification
          │
          ├─► Save functional_spec_v2.docx → GCS /output/
          │
          ├─► Vertex AI (Gemini) ──► Generate Playwright test script (.spec.ts)
          │
          ├─► Save playwright_tests_v2.spec.ts → GCS /output/
          │
          └─► Cloud Build ──► Execute Playwright tests in Docker
                    │
                    ▼
              Cloud Logging (pass/fail results)
```

---

## Google Cloud APIs Used

| Service | API Endpoint | Purpose |
|---|---|---|
| **Vertex AI Platform** | `aiplatform.googleapis.com` | Gemini model for generating specs and tests |
| **Cloud Run** | `run.googleapis.com` | Hosts and runs the orchestration service |
| **Cloud Build** | `cloudbuild.googleapis.com` | Executes the generated Playwright tests |
| **Cloud Storage** | `storage.googleapis.com` | Stores design docs, specs, and test scripts |
| **Secret Manager** | `secretmanager.googleapis.com` | Securely stores credentials and config secrets |
| **Eventarc** | `eventarc.googleapis.com` | Triggers the pipeline on file upload |

---

## Project Structure

```
├── Controllers/
│   └── OrchestratorController.cs       # Eventarc endpoint + health check
├── Models/
│   └── CloudStorageEvent.cs            # CloudEvent envelope model
├── src/
│   ├── AutomationEngine.Domain/
│   │   ├── Entities/
│   │   │   ├── DesignDocument.cs
│   │   │   ├── FunctionalSpec.cs
│   │   │   ├── TestScript.cs
│   │   │   ├── BuildJob.cs
│   │   │   └── ProcessingContext.cs    # Tracks pipeline state
│   │   ├── Enums/
│   │   │   ├── ProcessingStatus.cs
│   │   │   └── DocumentType.cs
│   │   └── Interfaces/
│   │       ├── IAIGenerationService.cs
│   │       ├── IBuildService.cs
│   │       ├── IDocumentSerializer.cs
│   │       ├── ISecretService.cs
│   │       └── IStorageRepository.cs
│   ├── AutomationEngine.Application/
│   │   ├── DTOs/
│   │   │   ├── StorageEventDto.cs
│   │   │   └── GenerationResultDto.cs
│   │   ├── Options/
│   │   │   └── ProcessDocumentOptions.cs
│   │   ├── UseCases/
│   │   │   ├── IProcessDocumentUseCase.cs
│   │   │   └── ProcessDocumentUseCase.cs   # Core pipeline logic
│   │   └── Extensions/
│   │       └── ApplicationServiceExtensions.cs
│   └── AutomationEngine.Infrastructure/
│       ├── GoogleCloud/
│       │   ├── AI/
│       │   │   └── GcpVertexAIService.cs       # Vertex AI (Gemini) calls
│       │   ├── Build/
│       │   │   └── GcpCloudBuildService.cs     # Cloud Build job submission
│       │   ├── Documents/
│       │   │   └── OpenXmlDocumentSerializer.cs # .docx generation
│       │   ├── Secrets/
│       │   │   └── GcpSecretManagerService.cs  # Secret Manager access
│       │   └── Storage/
│       │       └── GcpCloudStorageService.cs   # GCS read/write
│       ├── Options/
│       │   └── GoogleCloudOptions.cs
│       └── Extensions/
│           └── InfrastructureServiceExtensions.cs
├── Program.cs                          # App bootstrap + DI registration
├── appsettings.json                    # Configuration (set your project ID here)
├── Dockerfile                          # Multi-stage build for Cloud Run
├── cloudbuild.yaml                     # CI/CD: build → push → deploy to Cloud Run
└── .gcloudignore
```

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Google Cloud SDK (`gcloud`)](https://cloud.google.com/sdk/docs/install)
- A GCP project with billing enabled

---

## Setup

### 1. Enable Required APIs

```bash
gcloud services enable \
  aiplatform.googleapis.com \
  run.googleapis.com \
  cloudbuild.googleapis.com \
  storage.googleapis.com \
  secretmanager.googleapis.com \
  eventarc.googleapis.com
```

### 2. Configure the Application

Edit `appsettings.json` and set your project ID:

```json
{
  "GoogleCloud": {
    "ProjectId": "YOUR_GCP_PROJECT_ID",
    "Location": "us-central1",
    "GeminiModelId": "gemini-2.0-flash-001",
    "OutputFolder": "output",
    "CloudBuildPlaywrightImage": "node:22-bullseye-slim"
  }
}
```

### 3. Create a Cloud Storage Bucket

```bash
gsutil mb -l us-central1 gs://YOUR_BUCKET_NAME
```

### 4. Create a Service Account for Cloud Run

```bash
gcloud iam service-accounts create automation-engine-sa \
  --display-name="Automation Engine Service Account"

# Grant required roles
gcloud projects add-iam-policy-binding YOUR_GCP_PROJECT_ID \
  --member="serviceAccount:automation-engine-sa@YOUR_GCP_PROJECT_ID.iam.gserviceaccount.com" \
  --role="roles/aiplatform.user"

gcloud projects add-iam-policy-binding YOUR_GCP_PROJECT_ID \
  --member="serviceAccount:automation-engine-sa@YOUR_GCP_PROJECT_ID.iam.gserviceaccount.com" \
  --role="roles/storage.objectAdmin"

gcloud projects add-iam-policy-binding YOUR_GCP_PROJECT_ID \
  --member="serviceAccount:automation-engine-sa@YOUR_GCP_PROJECT_ID.iam.gserviceaccount.com" \
  --role="roles/cloudbuild.builds.editor"

gcloud projects add-iam-policy-binding YOUR_GCP_PROJECT_ID \
  --member="serviceAccount:automation-engine-sa@YOUR_GCP_PROJECT_ID.iam.gserviceaccount.com" \
  --role="roles/secretmanager.secretAccessor"
```

### 5. Set Up Eventarc Trigger

```bash
gcloud eventarc triggers create design-doc-trigger \
  --location=us-central1 \
  --destination-run-service=automation-engine \
  --destination-run-region=us-central1 \
  --destination-run-path=/api/orchestrator/trigger \
  --event-filters="type=google.cloud.storage.object.v1.finalized" \
  --event-filters="bucket=YOUR_BUCKET_NAME" \
  --service-account=automation-engine-sa@YOUR_GCP_PROJECT_ID.iam.gserviceaccount.com
```

### 6. Deploy to Cloud Run

Update the substitution variables in `cloudbuild.yaml`, then run:

```bash
gcloud builds submit --config cloudbuild.yaml \
  --substitutions \
    _REGION=us-central1,\
    _ARTIFACT_REGISTRY=us-central1-docker.pkg.dev/YOUR_GCP_PROJECT_ID/automation-engine,\
    _SERVICE_NAME=automation-engine,\
    _SERVICE_ACCOUNT=automation-engine-sa@YOUR_GCP_PROJECT_ID.iam.gserviceaccount.com
```

---

## Running Locally

```bash
# Authenticate with your GCP account (provides Application Default Credentials)
gcloud auth application-default login

# Run the service
dotnet run --project AutomationEngineService.csproj
```

The service will start on `http://localhost:8080`. You can test the health check at:

```
GET http://localhost:8080/api/orchestrator/health
```

---

## Triggering the Pipeline Manually

To simulate an Eventarc trigger locally, send a POST request:

```bash
curl -X POST http://localhost:8080/api/orchestrator/trigger \
  -H "Content-Type: application/cloudevents+json" \
  -d '{
    "specversion": "1.0",
    "type": "google.cloud.storage.object.v1.finalized",
    "source": "//storage.googleapis.com/projects/_/buckets/YOUR_BUCKET_NAME",
    "id": "test-event-001",
    "time": "2026-04-10T00:00:00Z",
    "data": {
      "bucket": "YOUR_BUCKET_NAME",
      "name": "design_v2.md",
      "contentType": "text/markdown",
      "size": "1024",
      "timeCreated": "2026-04-10T00:00:00Z"
    }
  }'
```

---

## API Endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/orchestrator/trigger` | Eventarc entry point — starts the pipeline |
| `GET` | `/api/orchestrator/health` | Health check for Cloud Run probes |
| `GET` | `/openapi` | OpenAPI spec (Development only) |

### Pipeline Response

```json
{
  "success": true,
  "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "functionalSpecPath": "output/functional_spec_v2.docx",
  "testScriptPath": "output/playwright_tests_v2.spec.ts",
  "buildJobId": "abc123",
  "buildLogUrl": "https://console.cloud.google.com/cloud-build/builds/abc123?project=YOUR_PROJECT",
  "errorMessage": null
}
```

---

## Viewing Results

- **Generated files** — browse `gs://YOUR_BUCKET_NAME/output/` in the [GCS Console](https://console.cloud.google.com/storage)
- **Test execution logs** — view in [Cloud Build Console](https://console.cloud.google.com/cloud-build/builds)
- **Service logs** — view in [Cloud Logging](https://console.cloud.google.com/logs) filtered by `resource.type="cloud_run_revision"`

---

## Technology Stack

| Component | Technology |
|---|---|
| Runtime | .NET 10, ASP.NET Core |
| Architecture | Onion Architecture (Clean Architecture) |
| AI Model | Vertex AI — Gemini 2.0 Flash |
| Hosting | Google Cloud Run |
| CI/CD | Google Cloud Build |
| Storage | Google Cloud Storage |
| Event Trigger | Google Eventarc |
| Secrets | Google Secret Manager |
| Test Framework | Playwright (TypeScript) |
| Document Format | OpenXML (.docx) |
| Logging | Google Cloud Logging (structured JSON) |
| Auth | Application Default Credentials (ADC) |
