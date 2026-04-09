# =============================================================================
# setup-secrets.ps1
# Creates three secrets in Google Cloud Secret Manager from the service account
# JSON key file and grants the service account permission to read them.
#
# Usage (run once from the repo root):
#   .\scripts\setup-secrets.ps1
# =============================================================================

$ErrorActionPreference = "Stop"

# ── Configuration ─────────────────────────────────────────────────────────────
$KeyFile       = "in-300000000123933-mfg-f11b29410c75.json"
$ProjectId     = "in-300000000123933-mfg"
$ServiceAccount= "mfg-hackathon@in-300000000123933-mfg.iam.gserviceaccount.com"
$Location      = "us-central1"

# Secret names (must match appsettings.json → GoogleCloud.Secrets.*)
$SecretSaJson         = "automation-engine-sa-json"
$SecretPrivateKey     = "automation-engine-sa-private-key"
$SecretPrivateKeyId   = "automation-engine-sa-private-key-id"

# ── Helpers ───────────────────────────────────────────────────────────────────
function Write-Step([string]$msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-OK([string]$msg)   { Write-Host "    OK: $msg" -ForegroundColor Green }

# ── Validate prerequisites ────────────────────────────────────────────────────
Write-Step "Checking prerequisites"

if (-not (Get-Command gcloud -ErrorAction SilentlyContinue)) {
    throw "gcloud CLI not found. Install it from https://cloud.google.com/sdk/docs/install"
}
if (-not (Test-Path $KeyFile)) {
    throw "Key file '$KeyFile' not found. Run this script from the repo root."
}

Write-OK "gcloud found and key file present"

# ── Activate service account for gcloud ──────────────────────────────────────
Write-Step "Activating service account for gcloud"
gcloud auth activate-service-account --key-file=$KeyFile --project=$ProjectId
Write-OK "Service account activated"

# ── Read values from the key file ─────────────────────────────────────────────
Write-Step "Parsing key file"
$keyJson       = Get-Content $KeyFile -Raw
$keyObj        = $keyJson | ConvertFrom-Json
$privateKey    = $keyObj.private_key
$privateKeyId  = $keyObj.private_key_id
Write-OK "Key file parsed (private_key_id: $privateKeyId)"

# ── Helper: create or update a secret ─────────────────────────────────────────
function Set-GcpSecret {
    param(
        [string]$SecretId,
        [string]$SecretValue,
        [string]$Description
    )

    $exists = gcloud secrets describe $SecretId --project=$ProjectId 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "    Creating secret: $SecretId"
        gcloud secrets create $SecretId `
            --project=$ProjectId `
            --replication-policy="user-managed" `
            --locations=$Location `
            --labels="managed-by=automation-engine" | Out-Null
    } else {
        Write-Host "    Secret already exists, adding new version: $SecretId"
    }

    # Write secret value via stdin (avoids shell history / temp file exposure)
    $SecretValue | gcloud secrets versions add $SecretId `
        --project=$ProjectId `
        --data-file=- | Out-Null

    Write-OK "$SecretId stored successfully"
}

# ── Create / update secrets ───────────────────────────────────────────────────
Write-Step "Storing secrets in Secret Manager"

# 1. Full service account JSON (used by the app to create GoogleCredential)
Set-GcpSecret -SecretId $SecretSaJson `
              -SecretValue $keyJson `
              -Description "Full service account JSON for AutomationEngineService"

# 2. Private key PEM only (for auditing / rotation reference)
Set-GcpSecret -SecretId $SecretPrivateKey `
              -SecretValue $privateKey `
              -Description "Private key PEM for mfg-hackathon SA"

# 3. Private key ID (matches the key file's private_key_id field)
Set-GcpSecret -SecretId $SecretPrivateKeyId `
              -SecretValue $privateKeyId `
              -Description "Private key ID for mfg-hackathon SA"

# ── Grant the service account access to read its own secrets ──────────────────
Write-Step "Granting secretAccessor role to $ServiceAccount"

$secrets = @($SecretSaJson, $SecretPrivateKey, $SecretPrivateKeyId)
foreach ($secret in $secrets) {
    gcloud secrets add-iam-policy-binding $secret `
        --project=$ProjectId `
        --member="serviceAccount:$ServiceAccount" `
        --role="roles/secretmanager.secretAccessor" | Out-Null
    Write-OK "  $ServiceAccount → secretAccessor on $secret"
}

# ── Print full secret resource names ──────────────────────────────────────────
Write-Step "Secret resource names (use in appsettings.json)"
Write-Host ""
Write-Host "  ServiceAccountSecretName :" -NoNewline
Write-Host " projects/$ProjectId/secrets/$SecretSaJson" -ForegroundColor Yellow
Write-Host "  PrivateKeySecretName     :" -NoNewline
Write-Host " projects/$ProjectId/secrets/$SecretPrivateKey" -ForegroundColor Yellow
Write-Host "  PrivateKeyIdSecretName   :" -NoNewline
Write-Host " projects/$ProjectId/secrets/$SecretPrivateKeyId" -ForegroundColor Yellow
Write-Host ""
Write-Host "Setup complete." -ForegroundColor Green
