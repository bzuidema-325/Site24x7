# Site24x7 PowerShell Module

PowerShell cmdlets for the [Site24x7 REST API](https://www.site24x7.com/help/api/), written in C#.
Version 2 is a full rewrite of the original script module as a compiled (binary) module, and adds
cmdlets for most of the API areas version 1 did not cover.

- Works in **Windows PowerShell 5.1** and **PowerShell 7+** on Windows, Linux and macOS (one `netstandard2.0` build).
- Signs in with **Zoho OAuth** and renews the access token for you.
- Supports every Site24x7 **data center**: US, EU, CN, IN, AU, JP, CA, UK, AE and SA.
- No third-party dependencies, so nothing conflicts with other modules.
- Failures are real PowerShell errors you can catch, with the API's error code attached.
- Every cmdlet that changes something supports `-WhatIf` and `-Confirm`.

## Install

Build it from source (needs the [.NET SDK](https://dotnet.microsoft.com/download) 6.0 or later):

```powershell
git clone https://github.com/bzuidema-325/Site24x7.git
cd Site24x7
./build.ps1
Import-Module ./out/Site24x7/Site24x7.psd1
```

To install it for your user, copy `out/Site24x7` into a folder on `$env:PSModulePath`
(for example `~/Documents/PowerShell/Modules/Site24x7`). Every CI run also uploads the built
module as a downloadable artifact.

## Sign in

Site24x7 uses Zoho OAuth. You do the first step once:

1. Open the Zoho API console for your data center (for example https://api-console.zoho.com, or
   https://api-console.zoho.eu for EU) and create a **Self Client**.
2. Generate a grant code with the scopes you need, for example
   `Site24x7.Admin.All,Site24x7.Reports.All,Site24x7.Operations.All,Site24x7.Account.All`.
3. Within the code's lifetime, exchange it for a refresh token and store it. Secrets are
   `SecureString`s throughout the module, which is what
   [SecretManagement](https://learn.microsoft.com/powershell/utility-modules/secretmanagement/overview)
   stores and returns:

```powershell
$secret = Read-Host 'Client secret' -AsSecureString
$token = Request-Site24x7RefreshToken -ClientId $clientId -ClientSecret $secret -GrantCode $code -DataCenter US
Set-Secret Site24x7RefreshToken $token.RefreshToken
Set-Secret ZohoClientSecret $secret
```

From then on, connect with the refresh token. The session renews its access token automatically
before it expires, so long-running scripts keep working:

```powershell
Connect-Site24x7 -ClientId $clientId -ClientSecret (Get-Secret ZohoClientSecret) -RefreshToken (Get-Secret Site24x7RefreshToken) -DataCenter US
```

You can also connect with just an access token (`Connect-Site24x7 -AccessToken $secureToken`),
which lasts an hour.

The session belongs to the PowerShell runspace you connected in. To work with several accounts,
or inside `ForEach-Object -Parallel` and thread jobs, use `Connect-Site24x7 -PassThru` and pass
the session to each cmdlet with `-Session` (for example `-Session $using:session`).

MSP and Business Unit accounts can switch to a customer with `Set-Site24x7Context -CustomerId <zaaid>`,
or `Get-Site24x7MspCustomer -Name 'Contoso*' | Set-Site24x7Context`, and back with `Set-Site24x7Context -Reset`.

## Examples

```powershell
# Everything that is down right now
(Get-Site24x7CurrentStatus).monitors | Where-Object status -eq 0

# Find monitors by name and suspend them
Get-Site24x7Monitor -Name 'web-*' | Disable-Site24x7Monitor

# Create a website monitor
New-Site24x7WebsiteMonitor 'Home page' 'https://example.com' `
    -LocationProfileId $loc -NotificationProfileId $notify -ThresholdProfileId $threshold -UserGroupId $admins

# Any monitor type, with the API's own field names
New-Site24x7Monitor -Type SSL_CERT -DisplayName 'example.com cert' -Property @{ domain_name = 'example.com'; port = 443; expire_days = 21 }

# Change one setting
Set-Site24x7Monitor $monitorId -CheckFrequency 1

# Maintenance now, and scheduled
Start-Site24x7Maintenance -MonitorName web01, web02 -Duration 60 -PerformMonitoring
New-Site24x7Maintenance 'Patch night' Once -StartDate 2026-11-01 -EndDate 2026-11-01 `
    -StartTime 22:00 -EndTime 23:30 -MonitorGroupId $groupId

# Reports
Get-Site24x7Report AvailabilitySummary $monitorId -Period Last30Days
Get-Site24x7LogReport $monitorId -Date (Get-Date).AddDays(-1)

# Anything without a cmdlet
Invoke-Site24x7Api GET 'monitors/name/Home page'
```

Every New-* and Set-* cmdlet takes `-Property @{ api_field = value }` for fields that have no
parameter, so nothing in the API is out of reach. Set-* cmdlets send only the settings you pass.
Objects returned by one cmdlet can be piped to the next: id parameters bind from the API's own
field names (`monitor_id`, `group_id` and so on).

Add `-Verbose` to see each request, and `-Debug` to see request and response bodies (passwords,
secrets and tokens in request bodies are shown as `***`). `Get-Help <cmdlet> -Full` documents every
cmdlet and parameter.

## Cmdlets

| Area | Cmdlets |
| --- | --- |
| Sign-in | `Connect-Site24x7`, `Disconnect-Site24x7`, `Get-Site24x7Session`, `Set-Site24x7Context`, `Request-Site24x7RefreshToken`, `Request-Site24x7AccessToken`, `Revoke-Site24x7RefreshToken` |
| Monitors | `Get-`, `New-`, `Set-`, `Remove-`, `Enable-`, `Disable-Site24x7Monitor`; `New-Site24x7WebsiteMonitor`, `RestApiMonitor`, `SslMonitor`, `PingMonitor`, `PortMonitor`, `DnsMonitor`, `HeartbeatMonitor`, `DomainExpiryMonitor`; `Get-Site24x7MonitorType`, `Get-Site24x7DeviceKey` |
| Monitor groups | `Get-`, `New-`, `Set-`, `Remove-Site24x7Group`, `Add-Site24x7GroupMonitor`; `Get-`, `New-`, `Set-`, `Remove-Site24x7Subgroup` |
| Status and reports | `Get-Site24x7CurrentStatus`, `Get-Site24x7Report` (summary, availability, performance, outage, busy hours, trend), `Get-Site24x7LogReport`, `Get-Site24x7Alarm`, `Get-Site24x7AlertLog` |
| Users | `Get-`, `New-`, `Set-`, `Remove-` for `Site24x7User`, `Site24x7UserGroup`, `Site24x7AttributeAlertGroup`, `Site24x7OnCallSchedule` |
| Profiles | `Get-`, `New-`, `Set-`, `Remove-` for `Site24x7ThresholdProfile`, `NotificationProfile`, `LocationProfile`, `ResourceCheckProfile`, `CredentialProfile`, `EmailTemplate`, `BusinessHours`; `Get-Site24x7LocationTemplate` |
| Maintenance | `Get-`, `New-`, `Set-`, `Remove-`, `Start-`, `Stop-Site24x7Maintenance` |
| Automation and settings | `Get-`, `New-`, `Set-`, `Remove-` for `Site24x7ITAutomation`, `Tag`, `ConfigurationRule`, `ScheduledReport`, `SlaSetting`, `StatusPage`, `OAuthProvider`, `WebToken`, `GlobalParameter`; `Get-Site24x7ITAutomationLog`, `Get-Site24x7AuditLog`, `Get-Site24x7OnPremisePoller` |
| Integrations | `Get-`, `Remove-Site24x7ThirdPartyIntegration`, `New-`, `Set-Site24x7Webhook`, `New-`, `Set-Site24x7Integration` (Slack, PagerDuty, Opsgenie, ServiceNow, Teams and others) |
| MSP | `Get-Site24x7MspCustomer`, `Get-Site24x7BusinessUnit`, `Set-Site24x7Context` |
| Anything else | `Invoke-Site24x7Api` |

## Behaviour worth knowing

- **Errors** are PowerShell errors. A failure for one pipeline item is a non-terminating error and
  the next item still runs; use `-ErrorAction Stop` with `try`/`catch`, or `-ErrorVariable`. The
  exception carries the API's `ErrorCode`, `ErrorInfo` and `ResponseBody`. Problems with the
  parameters themselves (for example a Set-* call with nothing to change) stop the command before
  anything is sent.
- **Confirmation.** Remove-* cmdlets and `Revoke-Site24x7RefreshToken` ask for confirmation; pass
  `-Confirm:$false` in unattended scripts. They write nothing on success.
- **Ctrl+C** stops a request immediately. Requests time out after 120 seconds.
- **Throttling.** When Site24x7 answers 429, the cmdlet waits as asked (up to 60 seconds) and
  retries up to three times; a longer `Retry-After` is reported as an error.
- **Safety.** Ids are checked before they go into a URL, customer ids before they go into a
  cookie, and `Invoke-Site24x7Api` only sends the token to the session's own API root. Credentials
  are only sent over https (http is allowed for localhost, for testing).

## Coming from version 1

Version 2 is a clean break; version 1 scripts need updating. The main changes:

- Cmdlets and parameters use PowerShell naming: `-MonitorId` instead of `-monitor_id`,
  `-UserGroupId` instead of `-user_groups`, `Set-*` instead of `Update-*`.
  `Update-Site24x7Group_AddMonitor` is `Add-Site24x7GroupMonitor`.
- `Initialize-ZohoToken`, `Request-ZohoAccessToken` and `Revoke-ZohoRefreshToken` are
  `Request-Site24x7RefreshToken`, `Request-Site24x7AccessToken` and `Revoke-Site24x7RefreshToken`,
  and take and return `SecureString`s.
- Sign in once with `Connect-Site24x7` instead of passing `-access_token` to every call.
- Settings that were `[bool]` are switches: `-PerformMonitoring`, `-SuppressAlert:$false`.
- Errors are PowerShell errors instead of returned status codes, and lists come back as one object
  per item.

These version 1 bugs are gone with it:

- `Initialize-ZohoToken` called `Get-Date.AddSeconds(...)` (missing parentheses), so it always failed after the token exchange.
- `Update-Site24x7User` had no user id parameter and sent the update to `/users`.
- Roles were matched with regular expressions, so `SuperAdministrator` also matched `Administrator`.
- `Stop-Site24x7Maintenance` checked an undefined `$data` variable and never called the API.
- `New-Site24x7Maintenance` sent `maintenance_end_on` under the `maintenance_end_type` key.
- Values of `$false` or `0` were silently dropped from request bodies.
- The API root was hard-coded to the US data center.

## Endpoints to confirm

The Site24x7 documentation site could not be reached in full while this was written, so a few
API paths come from memory of the API rather than from the page. They are marked `// verify` in
[`src/Site24x7/Core/Endpoints.cs`](src/Site24x7/Core/Endpoints.cs), where each is one line to fix:
alarms, alert logs, attribute groups, resource check profiles, OAuth providers, web tokens, global
parameters, IT automation logs, audit logs, configuration rules, scheduled reports, SLA settings,
status pages, on-premise pollers, and the by-name start/end maintenance paths (kept from version 1).
The report paths for monitor groups and monitor types (`reports/<report>/group/<id>` and
`reports/<report>/monitor_type/<type>`) are also worth a check. Everything else follows the
documentation or version 1.

## Development

```
src/Site24x7/Core/       HTTP client, OAuth, JSON, sessions, data centers, enums, endpoint paths
src/Site24x7/Cmdlets/    Cmdlets, one file per area, on shared base classes in ResourceCmdlets.cs
src/Site24x7/Cmdlets/Generated/   Simple CRUD areas, generated by tools/Build-ResourceCmdlets.ps1
module/                  Manifest and formatting
tests/                   Pester 5 tests against a mock Site24x7 server (no account needed)
tools/                   The CRUD generator and the help builder
```

- `./build.ps1` builds into `out/Site24x7`, writes Get-Help from the doc comments, and refreshes the
  cmdlet list and version in the manifest. The version is set only in `Site24x7.csproj`.
- `./build.ps1 -Test` also runs the tests (needs Pester 5: `Install-Module Pester -RequiredVersion 5.7.1`).
  Every generated area is tested from the generator's table, so a new area is covered automatically.
- To add a simple API area, or a field to one, edit the table in `tools/Build-ResourceCmdlets.ps1`
  and run it. Areas with special behaviour (monitors, users, maintenance, reports) are hand-written.
- Help comes from `/// <summary>` comments: `<para type="synopsis">` and `<para type="description">`
  on the cmdlet class, `<example><code>` for examples, and a summary on each parameter.

## License

GPL-3.0. See [LICENSE](LICENSE).
