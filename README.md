# Cabin Bingo

Small full-stack app for a cabin trip: **Cognito Hosted UI** sign-in, **exclusive guest roster** claims, **DynamoDB** preference catalog + per-user answers, and **two 5×5 bingo cards** (fixed center square + 24 generated challenges) from a .NET 10 **Lambda** HTTP API. Static SPA is built with **Vite + React** and served from **S3 + CloudFront**. Infrastructure is **AWS SAM** (CloudFormation) in **us-east-2**.

## Repository layout

| Path | Purpose |
|------|---------|
| [src/CabinBingo.Api](src/CabinBingo.Api) | ASP.NET Core minimal API (`Handler: CabinBingo.Api`, runtime `dotnet10`) |
| [CabinBingo.slnx](CabinBingo.slnx) | .NET solution (Visual Studio / `dotnet build CabinBingo.slnx`) |
| [infrastructure/template.yaml](infrastructure/template.yaml) | SAM template: Cognito, DynamoDB (3 tables), HTTP API → Lambda, S3, CloudFront |
| [web](web) | React SPA (Hosted UI + PKCE via `oidc-client-ts`) |
| [tools/Seed.ps1](tools/Seed.ps1) | Seeds sample `CabinGuests` + `PreferenceCatalog` rows via AWS CLI |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [AWS SAM CLI](https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/install-sam-cli.html)
- AWS CLI configured with credentials that can deploy the stack (`us-east-2`)
- Node.js 20+ for the SPA

## Deploy backend and hosting (first time)

1. Copy SAM config and adjust if needed:

   ```powershell
   copy infrastructure\samconfig.toml.example infrastructure\samconfig.toml
   ```

   Pick a **globally unique** `CognitoDomainPrefix` in `parameter_overrides` (Cognito hosted domains are shared across accounts).

2. From the **infrastructure** directory:

   ```powershell
   cd infrastructure
   sam build
   sam deploy
   ```

3. Note stack **Outputs**: `HttpApiUrl`, `UserPoolId`, `UserPoolClientId`, `CognitoIssuer`, `CognitoHostedUiBaseUrl`, `CloudFrontDomainName`, `WebsiteBucketName`.

4. **Seed DynamoDB** (replace table names with your stack’s physical names from the console or `aws cloudformation describe-stack-resources`):

   ```powershell
   ./tools/Seed.ps1 -GuestsTable YOUR_GUESTS_TABLE -PreferencesTable YOUR_PREFS_TABLE -Region us-east-2
   ```

5. **Cognito app client callbacks**: ensure `SpaCallbackUrl` / `SpaLogoutUrl` match what you use in the SPA. After you know the CloudFront URL, update the user pool client (redeploy with `CorsOrigin1=https://YOUR.cloudfront.net` and Hosted UI callback `https://YOUR.cloudfront.net/callback`, or edit the client in the console).

6. **Configure the SPA** — copy [web/.env.example](web/.env.example) to `web/.env` and set:

   - `VITE_COGNITO_AUTHORITY` = `CognitoIssuer` output  
   - `VITE_COGNITO_CLIENT_ID` = `UserPoolClientId`  
   - `VITE_REDIRECT_URI` / `VITE_POST_LOGOUT_REDIRECT_URI` = your SPA origin (localhost or CloudFront)  
   - `VITE_API_BASE_URL` = `HttpApiUrl` output (no trailing slash)

7. **Build and upload the SPA**:

   ```powershell
   cd web
   npm ci
   npm run build
   aws s3 sync ./dist s3://YOUR_WEBSITE_BUCKET_NAME/ --delete
   ```

8. Open `https://YOUR_CLOUDFRONT_DOMAIN` (or `npm run dev` for local UI against deployed API).

## Local API run (optional)

Set `CabinBingo__*` environment variables or edit `appsettings.Development.json` with real table names and Cognito settings, then:

```powershell
dotnet run --project src/CabinBingo.Api
```

The project is configured for Lambda (`AWSProjectType`); local Kestrel still runs for smoke tests.

## API routes (JWT required except `/health`)

- `GET /health`
- `GET /guests` — selectable guests (others’ claims hidden)
- `GET /profile`, `PUT /profile` `{ "guestId": "..." }`
- `GET /preferences/catalog`, `GET /preferences/me`, `PUT /preferences/me` `{ "answers": { "drink": ["Yes"], ... } }`
- `POST /bingo/cards` `{ "seed": "optional" }`

## License

Private / use for your cabin trip.
