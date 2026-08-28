# AI Helpers V2 — Installation & Maintenance

Living document, built while deploying the first dev/test IIS instance (VM: virgin box, IIS
installed, nothing else configured). Update this as new environments are set up or new gotchas
are found — don't let it go stale.

## Prerequisites on the target server

This app is only ever deployed to **Windows Server** (the Data Protection setup below assumes
it — see [DPAPI note](#data-protection--credential-encryption)).

- [ ] **.NET 10 Hosting Bundle** installed — not just the SDK or runtime. Search "hosting bundle"
      on the .NET download page; it's a separate installer bundling the IIS integration
      (ASP.NET Core Module, ANCM).
- [ ] **IIS role service: ISAPI Extensions + ISAPI Filters** (Server Manager → Add Roles and
      Features → Web Server (IIS) → Web Server → Application Development). A default/bare IIS
      install often doesn't have these enabled, and ANCM won't register properly without them.
      Enabling "ASP.NET 4.8" also works (it pulls these in as dependencies) but isn't itself a
      real requirement — this app doesn't use classic ASP.NET/System.Web at all.
- [ ] **Full reboot** after installing the above, not just `iisreset`. The Hosting Bundle installer
      modifies `applicationHost.config` to register the native module, but a plain `iisreset`
      doesn't reliably pick that up — this was the actual fix the first time round.
- [ ] Verify: IIS Manager → server (root) node → **Modules** → `AspNetCoreModuleV2` should be
      listed as a Native module. If it's genuinely missing after a reboot, check
      `%windir%\System32\inetsrv\config\applicationHost.config` directly for an
      `AspNetCoreModuleV2` entry under `<globalModules>` — if it's not there, repair-install the
      Hosting Bundle rather than assuming the first install completed correctly.
- [ ] A trusted TLS certificate for the site's real hostname (off Conwy's internal CA) — Entra
      requires HTTPS for any non-localhost redirect URI.

## IIS site setup

- [x] New **Application Pool** — .NET CLR version = **No Managed Code** (ASP.NET Core doesn't use
      the pool's CLR).
- [x] New **Site** bound to the real hostname, HTTPS binding with the cert above.
- [ ] Firewall inbound rule for 443 if not already open.
- Physical path = wherever the published output lands (see below).

## Publishing

**Before publishing, confirm `Project Info/` and `Design brief/` are NOT in the output.**
*(Hit for real on the first deployment — the folder had already been copied to the VM before this
was caught; removed from the server, and the fix below now stops it happening again.)* These
are gitignored reference folders (V1 schema exports, integration definitions, corporate style
guide) — `.gitignore` keeps them out of source control but means nothing to `dotnet publish`,
which uses MSBuild's own content globbing. They shipped straight into a real publish output during
the first deployment before this was caught. `AiHelpers.csproj` now explicitly excludes both
(alongside `Tools/`), verified by publishing to a scratch folder and confirming neither appears —
this note stays here as a standing check, not just a one-time fix, given `Project Info/` has held a
live credential before (a WSO2 auth token in an example doc, from earlier in this project).

**Publish to a folder OUTSIDE the project directory** — this bit us once already. `-o Publish`
inside the repo gets picked up by MSBuild's own file globbing on a second publish (it sees the
*previous* publish output's `wwwroot` as if it were source), producing `BLAZOR106` errors like:

```
error BLAZOR106: The JS module file '...\Publish\wwwroot\Components\Layout\ReconnectModal.razor.js'
was defined but no associated razor component or view was found for it.
```

If you hit that, delete the stale in-tree output folder and republish outside the repo:

```powershell
dotnet publish -c Release -o ..\AiHelpers-Publish
```

Copy that output folder's *contents* to the IIS site's physical path.

## Entra ID App Registration

App Registration: **"AI Helpers V2"**, Tenant ID `e6cbbff6-ddee-4ead-a3c7-2431651131bf`, Client ID
`d417d784-0d94-46d8-b56e-f9070b20b3fb` (both public/non-secret, already in `appsettings.json`).

- [x] Add this server's real redirect URIs to the App Registration (Azure Portal → App
      registrations → "AI Helpers V2" → Authentication → Add a platform/URI):
      `https://<hostname>/signin-oidc` and `https://<hostname>/signout-callback-oidc`.
      Only `https://localhost:7016/...` is registered by default (local dev).
- Microsoft Graph `User.Read` consent is already admin-consented at the App Registration level —
  this isn't tied to a specific redirect URI/server, so it carries over automatically.

## Configuration on the server

**Not via `dotnet user-secrets`** — that's local-dev-machine-scoped only (tied to the Windows user
profile of whoever ran `dotnet user-secrets set`, not something that travels with the code).

**Method — Application Pool-level environment variables, NOT site-level.** First attempt used
IIS Manager → site → Configuration Editor → `system.webServer/aspNetCore` →
`environmentVariables` — this edits the SITE's own `web.config`, which every `dotnet publish` /
file copy regenerates from scratch, silently wiping these on every redeploy (hit for real:
running the app locally afterward looked broken - it wasn't, but the *deployed* copy's env vars
were gone after a routine re-copy). Use PowerShell to set them on the **Application Pool**
instead - that config lives in `applicationHost.config` (server-wide), untouched by any site
file copy:

```powershell
$poolName = "<app pool name>"
$filter = "system.applicationHost/applicationPools/add[@name='$poolName']/environmentVariables"
Add-WebConfigurationProperty -PSPath "MACHINE/WEBROOT/APPHOST" -Filter $filter -Name "." -Value @{name="ASPNETCORE_ENVIRONMENT"; value="Production"}
Add-WebConfigurationProperty -PSPath "MACHINE/WEBROOT/APPHOST" -Filter $filter -Name "." -Value @{name="ConnectionStrings__AiHelpers"; value="..."}
Add-WebConfigurationProperty -PSPath "MACHINE/WEBROOT/APPHOST" -Filter $filter -Name "." -Value @{name="AzureAd__ClientSecret"; value="..."}
```

(Double-underscore `__` is .NET's standard way of expressing a nested config key, e.g.
`AzureAd:ClientSecret`, as an env var name.) Recycle the app pool afterward to pick it up.

- [ ] `ASPNETCORE_ENVIRONMENT` = `Production` — set explicitly rather than relying on the
      implicit default; Development mode shows detailed stack traces on error pages, not
      something to leave possible on a network-reachable server.
- [ ] `ConnectionStrings__AiHelpers` = `Server=cm-itsqltest01.corp.conwy.gov.uk;Database=AI_Helpers;Trusted_Connection=True;TrustServerCertificate=True;`
- [ ] `AzureAd__ClientSecret` = the real secret value, typed directly into IIS on the server -
      never pasted into chat with Claude.
- [ ] **SQL access grant**: whichever identity the app pool runs as needs an explicit Windows-auth
      login granted on `cm-itsqltest01` (`Trusted_Connection=True` — every DB connection this
      project has needed has required this to be requested from IT first; "Login failed for user"
      is the symptom if it's missing).
      **Method chosen**: the default `ApplicationPoolIdentity` (no IIS identity change needed) —
      outbound Windows-authenticated network connections present as the VM's own computer account
      automatically. Get the exact name via `$env:COMPUTERNAME` on the VM, then request
      `CORPDOM\<name>$` be added as a SQL login on `cm-itsqltest01` with `db_datareader` +
      `db_datawriter` on `AI_Helpers` (not `db_owner`/`db_ddladmin` — the running app never applies
      migrations itself). **Note for later**: this ties DB access to this specific VM's computer
      account - a VM rebuild needs the grant redone even if the hostname is reused.

## Database

- Currently pointed at the **shared test DB** (`cm-itsqltest01.corp.conwy.gov.uk` / `AI_Helpers`) —
  same one used throughout dev. No migration step needed for a new server instance using this DB;
  it's already current.
- If a server ever needs its own separate DB: run `dotnet ef database update` from the published
  source (or a machine with the SDK + this repo checked out) before first use.

## Data Protection / credential encryption

Data Protection keys (which encrypt `ProviderCredential` rows — the Bedrock credential) are
persisted to the shared DB but DPAPI-protected **machine-scoped**
(`ProtectKeysWithDpapi(protectToLocalMachine: true)` in `Program.cs` — deliberate choice, see the
comment there: *"if this ever runs across multiple servers, switch to ProtectKeysWithCertificate
instead, since DPAPI machine keys don't roam"*).

**Practical consequence**: a credential encrypted on one machine will NOT decrypt on another. On
every new server:

- [ ] Sign in as admin → `/admin/credentials` → re-enter the AWS Bedrock bearer token. Don't
      expect the existing encrypted value to just work — it won't, and the failure mode is a
      `CryptographicException`, not a helpful error.

**Noisy but harmless**: on startup you may see a wall of `fail:`/`warn:` log lines from
`Microsoft.AspNetCore.DataProtection.*` about keys being "ineligible" and failing to decrypt,
followed by an `INSERT INTO [DataProtectionKeys]`. This is expected, not a crash - the
`DataProtectionKeys` table is shared across every machine that's ever pointed at this DB (dev
boxes, scratch test boots, every VM), each with its own machine-scoped key. A machine correctly
can't decrypt another machine's key, logs that loudly, then mints and inserts a fresh one for
itself. Only worth investigating further if the app actually fails to start/respond afterward -
check that specifically before assuming this log block itself is the problem.
      **Confirmed hit exactly as predicted on the first deployment** — running a Helper failed
      with "Error occurred during a cryptographic operation" shown in the run page's Options pane
      (the app itself worked fine up to that point - sign-in and the Graph org-info pre-fill both
      succeeded).
      **Real bug found trying to apply the fix**: `/admin/credentials` itself threw the same
      `CryptographicException` on load (it decrypts the existing credential just to show a masked
      "currently set: ****1234" preview) — meaning the page you need to fix this couldn't be
      reached at all. Fixed in `Components/Pages/Admin/Credentials.razor` — the decrypt-for-preview
      call is now wrapped in a try/catch, falling back to a warning banner instead of crashing the
      page, so the save form always renders and a fresh credential can always be entered regardless
      of whether the existing one is readable on this machine. **Needs a redeploy to take effect**
      (chose to wait for the proper fix over a DB-side stopgap - see git history if a future
      deployment wants the immediate-unblock option instead: delete the stale `ProviderCredentials`
      row and the *unfixed* code already handles a missing row fine, since `GetDefaultAsync` only
      throws when a row exists but can't be decrypted, not when there's no row at all).

## First run / smoke test

- [ ] Browse to the site — confirm it redirects into a real Entra sign-in (not an error).
- [ ] Sign in as admin, re-enter the Bedrock credential (above).
- [ ] Run a Helper end-to-end — confirms outbound reachability to Bedrock, Microsoft Graph, and
      `login.microsoftonline.com`. Check outbound firewall/proxy rules on a fresh VM if this
      fails; don't assume connectivity "just works" on a new box.

## Troubleshooting log

Real issues hit during the first deployment, kept here so the next one goes faster.

| Symptom | Cause | Fix |
|---|---|---|
| `BLAZOR106` build error referencing a `.razor.js` file under a `Publish\` folder | Publishing into a folder inside the project tree; MSBuild globs it back in as source on the next build | Delete the stale in-tree output, publish outside the repo (see [Publishing](#publishing)) |
| IIS Manager: "The Data is invalid" when touching site config (e.g. Configuration Editor) | `AspNetCoreModuleV2` not registered — Hosting Bundle missing, or installed but not fully picked up | Install Hosting Bundle if missing; otherwise full **reboot** (not just `iisreset`); verify via IIS Manager → server node → Modules |
| `AspNetCoreModuleV2` still missing after Hosting Bundle install + `iisreset` | `iisreset` isn't always enough for the MSI's IIS registration step to take effect | Full reboot; if still missing, check `applicationHost.config` directly and repair-install the Hosting Bundle if the entry's genuinely absent |
| `AspNetCoreModuleV2` still missing after a full reboot AND installing ISAPI Extensions/Filters | Confirmed: neither of those alone is sufficient if the Hosting Bundle's own IIS-registration step just didn't complete on first install | **Resolved**: verify with `Get-WebGlobalModule -Name "AspNetCoreModuleV2"` (empty/no output = confirmed missing). Re-run the Hosting Bundle installer and choose **Repair** when it detects the existing install, then reboot fully. This alone fixed it on this VM — full uninstall/reinstall wasn't needed, Repair is the first thing to try |

## Redeploying / updating

- Publish to an out-of-tree folder (see above), copy over the site's physical path, recycle the
  app pool (or `iisreset`) to pick up the new binaries.
- New NuGet packages (e.g. a merge that adds a dependency) need a real app pool recycle/restart —
  don't rely on any kind of hot-reload in a deployed IIS instance.
- New EF Core migrations need `dotnet ef database update` run against whichever DB that
  environment points at before/after deploying the code that depends on them.
