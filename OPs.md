# CabinBingo — deployment and testing (operations)

This file is the **canonical runbook** for deploying and smoke-testing the stack. Region is **us-east-2** unless you change the SAM config.

## Readiness (are we ready?)

**Yes — the repo is ready to deploy and test**, assuming:

| Requirement | Notes |
|-------------|--------|
| AWS account + credentials | Default profile (or `AWS_PROFILE`) with rights to deploy IAM, Lambda, API Gateway, Cognito, DynamoDB, S3, CloudFormation, CloudFront |
| Tools installed | [.NET 10 SDK](https://dotnet.microsoft.com/download), [AWS SAM CLI](https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/install-sam-cli.html), [AWS CLI v2](https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html), Node.js **20+** |
| Cognito hosted domain prefix | Must be **globally unique** across AWS (`CognitoDomainPrefix` in SAM parameters) |
| Git / code | `main` branch contains the API, SAM template, SPA, and seed data |

Nothing else must be merged before a first deploy; seed data is applied **after** the stack exists.

---

## One-time local setup

1. **AWS CLI** — verify identity and region:

   ```powershell
   aws sts get-caller-identity
   aws configure get region
   ```

   If region is not `us-east-2`, either run `aws configure set region us-east-2` or pass `--region us-east-2` on every command below.

2. **SAM config** — from repo root:

   ```powershell
   copy infrastructure\samconfig.toml.example infrastructure\samconfig.toml
   ```

   Edit `infrastructure\samconfig.toml`:

   - Set **`parameter_overrides`** so `CognitoDomainPrefix` is unique (e.g. `cabinbingo-yourfamily-2026`).
   - For **first** deploy while you only test from localhost, defaults for `SpaCallbackUrl` / `SpaLogoutUrl` / `CorsOrigin1` pointing at `http://localhost:5173` are fine.

---

## Deploy infrastructure (SAM)

All commands from **`infrastructure`** unless noted.

```powershell
cd infrastructure
sam build
sam deploy
```

- First run: SAM may prompt for stack name, confirm IAM capabilities, and S3 bucket for artifacts — answer **yes** to create roles as needed.
- Wait until **CloudFormation** reports `CREATE_COMPLETE` (or `UPDATE_COMPLETE`).

### Capture stack outputs

Either in the SAM/CloudFormation console or:

```powershell
aws cloudformation describe-stacks --stack-name cabinbingo --region us-east-2 --query "Stacks[0].Outputs" --output table
```

Record at least:

| Output (logical) | Use for |
|------------------|---------|
| `HttpApiUrl` | `VITE_API_BASE_URL` (no trailing slash) |
| `CognitoIssuer` | `VITE_COGNITO_AUTHORITY` |
| `UserPoolClientId` | `VITE_COGNITO_CLIENT_ID` |
| `CognitoHostedUiBaseUrl` | Human login host (authorize UI); OIDC metadata still comes from issuer |
| `WebsiteBucketName` | `aws s3 sync` target |
| `CloudFrontDomainName` | SPA URL after deploy |
| `CloudFrontDistributionId` | Optional CloudFront invalidation |

---

## Seed DynamoDB (after first deploy)

Physical table names include a random suffix. List resources:

```powershell
aws cloudformation describe-stack-resources --stack-name cabinbingo --region us-east-2 --query "StackResources[?ResourceType=='AWS::DynamoDB::Table'].{Logical:LogicalResourceId,Physical:PhysicalResourceId}" --output table
```

From repo root (PowerShell):

```powershell
.\tools\Seed.ps1 `
  -GuestsTable PHYSICAL_NAME_OF_CabinGuestsTable `
  -PreferencesTable PHYSICAL_NAME_OF_PreferenceCatalogTable `
  -Region us-east-2
```

Re-running put-items **overwrites** the same `guestId` / `preferenceId` keys. Rows with **old** IDs that no longer exist under [`tools/seed/`](tools/seed/) are **not** deleted for you—clean those up in the DynamoDB console if needed. If someone had claimed a removed guest, clear `claimedBySub` and fix their `UserData` profile.

The cabin roster and stable `guestId` values are documented in [`tools/seed/README.md`](tools/seed/README.md).

---

## Point Cognito + CORS at the real SPA URL (production path)

After the first deploy you know **`CloudFrontDomainName`** (e.g. `d111111abcdef8.cloudfront.net`).

1. **HTTPS SPA origin** (no path): `https://d111111abcdef8.cloudfront.net`  
2. **OAuth redirect** (must match app + Cognito client exactly):  
   `https://d111111abcdef8.cloudfront.net/callback`  
3. **Post-logout redirect**:  
   `https://d111111abcdef8.cloudfront.net/` (or without trailing slash — be consistent with Cognito client settings)

**Option A — Redeploy (recommended for repeatability)**  
Update `infrastructure\samconfig.toml` `parameter_overrides` to include (space-separated string):

- `SpaCallbackUrl=https://YOUR_CF_DOMAIN/callback`
- `SpaLogoutUrl=https://YOUR_CF_DOMAIN/`
- `CorsOrigin1=https://YOUR_CF_DOMAIN`

Then:

```powershell
cd infrastructure
sam build
sam deploy
```

**Option B — Console**  
Edit the Cognito app client callback/logout URLs and (for CORS) either redeploy with new `CorsOrigin1` or you will still need a stack update to change API CORS.

---

## Build and publish the SPA

From repo root:

```powershell
cd web
copy .env.example .env
# Edit .env: set VITE_* from stack outputs (see table above).
npm ci
npm run build
cd ..
aws s3 sync web/dist s3://WEBSITE_BUCKET_NAME/ --delete --region us-east-2
```

**Optional — invalidate CloudFront** so browsers do not keep old `index.html` / assets:

```powershell
aws cloudfront create-invalidation --distribution-id CLOUDFRONT_DISTRIBUTION_ID --paths "/*" --region us-east-2
```

(`us-east-2` is accepted for create-invalidation even though CloudFront is global.)

---

## Smoke tests (after SPA + seed + Cognito URLs align)

| Step | Action | Expected |
|------|--------|------------|
| 1 | `curl -sS "HTTPS_API_URL/health"` | JSON with `"status":"ok"` |
| 2 | Open SPA at CloudFront (or `npm run dev` with `.env` pointing at deployed API) | Page loads |
| 3 | **Sign in** via Cognito Hosted UI | Redirect to `/callback`, then home; no console OIDC errors |
| 4 | **GET** guests (implicit after sign-in in UI) | Guest list appears (seeded names) |
| 5 | **Claim** a guest | Success; name disappears for a second user |
| 6 | **Save preferences** | All five preferences saved |
| 7 | **Generate bingo** | Two 5×5 grids; center cell is the fixed hug square |

**Local UI against deployed API:** `CorsOrigin1` must include `http://localhost:5173` and Cognito app client must list `http://localhost:5173/callback` and logout URL (add in console or second parameter set).

---

## Update application code only (no infra change)

```powershell
cd infrastructure
sam build
sam deploy
```

Then rebuild and re-sync the SPA if `web/` changed.

---

## Troubleshooting (short)

| Symptom | Likely cause |
|---------|----------------|
| `403` / CORS errors from browser | `CorsOrigin1` does not match exact SPA origin (scheme + host + port). |
| Cognito `redirect_mismatch` | Callback URL in app client does not match `VITE_REDIRECT_URI` character-for-character. |
| API `401` / invalid audience | `VITE_COGNITO_CLIENT_ID` must match app client; use **access** or **ID** token consistently (API accepts either `aud` or `client_id`). |
| Empty guest list | Seed script not run, or wrong table name. |
| `409` on profile | Guest already claimed (expected). |
| Bingo “not enough slots” | User answers gated out too many slots; broaden answers or add slots in `BingoService.cs`. |

---

## Teardown (optional)

```powershell
cd infrastructure
sam delete --stack-name cabinbingo --region us-east-2
```

Confirm S3 bucket empty if delete fails on bucket retention. DynamoDB tables in this template use default deletion policy with stack removal (verify in console if you added `DeletionPolicy: Retain` later).

---

## Change log (ops)

| Date | Who | Change |
|------|-----|--------|
| 2026-04-18 | — | Initial `OPs.md` runbook created. |
| 2026-04-18 | — | Cabin guest seed roster updated to trip list (`tools/seed/`). |

_Add a row here whenever you change parameters, domains, or deployment procedure._
