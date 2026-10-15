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
3. Within the code's lifetime, exchange it for a refresh token:

```powershell
$token = Initialize-ZohoToken -ClientId $clientId -ClientSecret $clientSecret -GrantCode $code -DataCenter US
$token.refresh_token   # store this securely, for example with Microsoft.PowerShell.SecretManagement
```

From then on, connect with the refresh token. The session renews its access token automatically
before it expires, so long-running scripts keep working:

```powershell
Connect-Site24x7 -ClientId $clientId -ClientSecret $clientSecret -RefreshToken $refreshToken -DataCenter US
```

You can also connect with just an access token (`Connect-Site24x7 -AccessToken $token`), which lasts
an hour, or pass `-AccessToken` to any cmdlet as version 1 did. To work with several accounts at
once, use `Connect-Site24x7 -PassThru` and pass the session with `-Session`.

MSP and Business Unit accounts can switch to a customer with `Set-Site24x7Context -CustomerId <zaaid>`
(see `Get-Site24x7MspCustomer` and `Get-Site24x7BusinessUnit`) and back with `Set-Site24x7Context -Reset`.

## Examples

```powershell
# Everything that is down right now
(Get-Site24x7CurrentStatus).monitors | Where-Object status -eq 0

# Find monitors by name and suspend them
Get-Site24x7Monitor -Name 'web-*' | Disable-Site24x7Monitor

# Create a website monitor
New-Site24x7WebsiteMonitor 'Home page' 'https://example.com' `
    -LocationProfileId $loc -NotificationProfileId $notify -ThresholdProfileId $threshold -UserGroupIds $admins

# Any monitor type, with the API's own field names
New-Site24x7Monitor -Type SSL_CERT -DisplayName 'example.com cert' -Property @{ domain_name = 'example.com'; port = 443; expire_days = 21 }

# Change one setting
Set-Site24x7Monitor $monitorId -CheckFrequency 1

# Maintenance now, and scheduled
Start-Site24x7Maintenance -MonitorNames web01, web02 -Duration 60 -PerformMonitoring
New-Site24x7Maintenance 'Patch night' -MaintenanceType Once -StartDate 2026-11-01 -EndDate 2026-11-01 `
    -StartTime 22:00 -EndTime 23:30 -MonitorGroups $groupId

# Reports
Get-Site24x7Report -Report AvailabilitySummary -MonitorId $monitorId -Period Last30Days
Get-Site24x7LogReport $monitorId -Date (Get-Date).AddDays(-1)

# Anything without a cmdlet
Invoke-Site24x7Api GET 'monitors/name/Home page'
```

Every New-* and Set-* cmdlet takes `-Property @{ api_field = value }` for fields that have no
parameter, so nothing in the API is out of reach. Set-* cmdlets send only the settings you pass.
Add `-Verbose` to see each request, and `-Debug` to see request and response bodies.

## Cmdlets

| Area | Cmdlets |
| --- | --- |
| Sign-in | `Connect-Site24x7`, `Disconnect-Site24x7`, `Get-Site24x7Session`, `Set-Site24x7Context`, `Initialize-ZohoToken`, `Request-ZohoAccessToken`, `Revoke-ZohoRefreshToken` |
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

`Get-Command -Module Site24x7` lists them all, and `Get-Help <cmdlet> -Parameter *` describes each parameter.

## Coming from version 1

Scripts written for version 1 should mostly keep working: every version 1 command name still
exists (the `Update-*` commands are aliases of the new `Set-*` cmdlets), and every version 1
parameter name (`-access_token`, `-monitor_id`, `-display_name` and so on) is an alias of the new
PowerShell-style name (`-AccessToken`, `-MonitorId`, `-DisplayName`). What changed:

- **Errors.** Version 1 returned the HTTP status code (or the raw response) when a call failed.
  Version 2 writes a PowerShell error instead. Use `-ErrorAction Stop` with `try`/`catch`, or
  `-ErrorVariable`. The exception carries the API's `ErrorCode`, `ErrorInfo` and `ResponseBody`.
- **Output.** Lists come back as one object per item, so they work in the pipeline.
- **Sign-in.** `-access_token` on every call still works, but `Connect-Site24x7` is simpler and
  renews the token itself.
- **Enums.** Roles, alert channels, days and similar values accept their names (tab-completed) or
  numbers as before. `JobTitle` `CEO/CTO` is now `CEO_CTO`, and `WeekOfMonth` `Forth` is now `Fourth`.
- **Maintenance.** The per-type settings are ordinary parameters rather than dynamic ones, and
  `New-Site24x7Maintenance` tells you which ones the chosen type still needs. Dates are `[datetime]`.
- **Renamed.** `Update-Site24x7Group_AddMonitor` is `Add-Site24x7GroupMonitor`, and
  `New-Site24x7ThresholdProfile_Incomplete` is `New-Site24x7ThresholdProfile` (both old names remain as aliases).

### Bugs from version 1 that are fixed

The review of version 1 turned up these, all fixed in the rewrite:

- `Initialize-ZohoToken` called `Get-Date.AddSeconds(...)` (missing parentheses), so it always failed after the token exchange.
- `Update-Site24x7User` had no user id parameter and sent the update to `/users`, so it could not update anyone; it also declared `job_title` as `[sting]`.
- `New-`/`Update-Site24x7User` matched roles with regular expressions, so `SuperAdministrator` also matched `Administrator` and the second `Add` threw a duplicate-key error. The `cloudspend_role` branches checked StatusIQ role names, and `StatusIQSpokesPerson23` was missing its `|`.
- `Stop-Site24x7Maintenance` checked an undefined `$data` variable and returned `1` without ever calling the API.
- `New-Site24x7Maintenance` sent `maintenance_end_on` under the `maintenance_end_type` key.
- `Start-Site24x7Maintenance` compared parameter set names like `Monitor Names` that did not exist, so names were never sent to the `/names` endpoint and `selection_type` was often missing; `$perform_monitoring` and `$subgroup_monitors` were used but never declared.
- Values of `$false` or `0` were silently dropped from request bodies (for example `-suppress_alert $false`), because each field was added only when it was "truthy".
- The API root was hard-coded to the US data center, and Zoho Accounts to accounts.zoho.com.
- `Update-Site24x7Usergroup` declared `-users` as a single string instead of an array.

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
```

- `./build.ps1` builds into `out/Site24x7` and refreshes the cmdlet and alias lists in the manifest.
- `./build.ps1 -Test` also runs the tests (needs Pester 5: `Install-Module Pester -MinimumVersion 5.5`).
- To add a simple API area, or a field to one, edit the table in `tools/Build-ResourceCmdlets.ps1`
  and run it. Areas with special behavior (monitors, users, maintenance, reports) are hand-written.

## License

GPL-3.0. See [LICENSE](LICENSE).
