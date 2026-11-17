# Tests

## Unit and integration tests (`tests/RadiantHomes.Tests`)

xUnit. Run from the repository root:

```
dotnet test tests/RadiantHomes.Tests/RadiantHomes.Tests.csproj
```

- Every run creates a throw-away SQL Server database (`MichuHomes_Tests_xxxx`), applies **all migrations** to it, and drops it at the end. A broken migration chain fails the tests.
- Server: the environment variable `MICHU_TEST_SQL`, a connection string *without* a database. Without it, the tests use the developer connection string in `RadiantHomes/appsettings.json`, with a different database name. They never touch `HomePortalV2` or production.
- What is covered:
  - `PermissionCatalogTests`: the catalogue, company lifecycle transitions and the policy provider (no database).
  - `AccessControlTests`:
    - effective permissions and the company approval gate
    - delegation: you can only grant permissions you hold
    - cross-company and cross-domain assignment refused
    - you can't change your own roles, and the last super admin is protected
    - role deletion rules
    - the company status guard, and approval decisions with history, notifications and audit
  - `AuthorizationHttpTests`: the real app (`WebApplicationFactory`), signed in through a test scheme as accounts seeded in the test database. It checks direct URL access by anonymous visitors, permission holders, company staff, staff of pending companies and customers.

CI (`.github/workflows/ci.yml`) runs these on every push and pull request, against a SQL Server container.

## Browser journey (`tests/e2e`)

`verify-rbac.js` drives a real browser through Foundation 1, using Puppeteer and the installed Edge:
- platform roles and users
- revocation and deactivation
- company roles and agents
- forged requests
- a new company waiting for approval
- public visibility
- password recovery through the emailed link

It needs:
- the app running on a **test** database with the seeded test accounts (`admin@michuhomes.local`, `r3.realestate@michuhomes.local`, `r1.owner@michuhomes.local`), in Development so emails go to `RadiantHomes/App_Data/mail`
- `E2E_BASE_URL` (default `http://localhost:5310`), `E2E_PASSWORD` (the test accounts' password), and optionally `BROWSER_PATH` and `E2E_MAIL_DIR`

```
cd tests/e2e
npm install
node verify-rbac.js
```

It creates its own roles, users and a company each run, all with unique names. Never point it at production.
