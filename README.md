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

Build it from source (needs the [.NET SDK](https://dotnet.microsoft.com/download) 8.0 or later):

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
New-Site24x7Monitor 'example.com cert' SSL_CERT -Property @{ domain_name = 'example.com'; port = 443; expire_days = 21 }

# Change one setting
Set-Site24x7Monitor $monitorId -CheckFrequency 1

# Maintenance now, and scheduled
Start-Site24x7Maintenance -MonitorName web01, web02 -Duration 60 -PerformMonitoring
Get-Site24x7Monitor -Name 'web-*' | Start-Site24x7Maintenance -Duration 30
New-Site24x7Maintenance 'Patch night' Once -StartDate 2026-11-01 -EndDate 2026-11-01 `
    -StartTime 22:00 -EndTime 23:30 -MonitorGroupId $groupId

# Reports
Get-Site24x7Report AvailabilitySummary $monitorId -Period Last30Days
Get-Site24x7LogReport $monitorId -Date (Get-Date).AddDays(-1)

# Anything without a cmdlet (a path relative to the API root)
Invoke-Site24x7Api GET 'monitors/name/Home page'
```

The New-* and Set-* cmdlets for API resources take `-Property @{ api_field = value }` for fields
that have no parameter, so nothing in the API is out of reach. A `-Property` key replaces the
matching typed parameter whatever its case. Set-* cmdlets send only the settings you pass; string
parameters do not accept an empty value, so to clear a field pass it in `-Property`.
Objects returned by one cmdlet can be piped to the next: id parameters bind from the API's own
field names (`monitor_id`, `group_id` and so on).

`-Name` on Get-* cmdlets matches an exact name first (so `'web [prod]'` works), then a wildcard
pattern; a plain name that matches nothing is reported as a not-found error.

Add `-Verbose` to see each request, and `-Debug` to see request and response bodies. Secrets are
shown as `***`: password, secret, token, key and credential fields, sensitive header values such as
`Authorization` and `Cookie`, and anything in a `SecureString`. A body that is not JSON is not shown
at all. `Get-Help <cmdlet> -Full` documents every cmdlet and parameter.

## Cmdlets

| Area | Cmdlets |
| --- | --- |
| Sign-in | `Connect-Site24x7`, `Disconnect-Site24x7`, `Get-Site24x7Session`, `Set-Site24x7Context`, `Request-Site24x7RefreshToken`, `Request-Site24x7AccessToken`, `Revoke-Site24x7RefreshToken` |
| Monitors | `Get-`, `New-`, `Set-`, `Remove-`, `Enable-`, `Disable-Site24x7Monitor`; `New-Site24x7WebsiteMonitor`, `RestApiMonitor`, `SslMonitor`, `PingMonitor`, `PortMonitor`, `DnsMonitor`, `HeartbeatMonitor`, `DomainExpiryMonitor`; `Get-Site24x7MonitorType`, `Get-Site24x7DeviceKey` |
| Monitor groups | `Get-`, `New-`, `Set-`, `Remove-Site24x7MonitorGroup`, `Add-Site24x7MonitorGroupMember`; `Get-`, `New-`, `Set-`, `Remove-Site24x7Subgroup` |
| Status and reports | `Get-Site24x7CurrentStatus`, `Get-Site24x7Report` (summary, availability, performance, outage, busy hours, trend), `Get-Site24x7LogReport`, `Get-Site24x7Alarm`, `Get-Site24x7AlertLog` |
| Users | `Get-`, `New-`, `Set-`, `Remove-` for `Site24x7User`, `Site24x7UserGroup`, `Site24x7AttributeAlertGroup`, `Site24x7OnCallSchedule` |
| Profiles | `Get-`, `New-`, `Set-`, `Remove-` for `Site24x7ThresholdProfile`, `NotificationProfile`, `LocationProfile`, `ResourceCheckProfile`, `CredentialProfile`, `EmailTemplate`, `BusinessHours`; `Get-Site24x7LocationTemplate` |
| Maintenance | `Get-`, `New-`, `Set-`, `Remove-`, `Start-`, `Stop-Site24x7Maintenance` |
| Automation and settings | `Get-`, `New-`, `Set-`, `Remove-` for `Site24x7ITAutomation`, `Tag`, `ConfigurationRule`, `ScheduledReport`, `SlaSetting`, `StatusPage`, `OAuthProvider`, `WebToken`, `GlobalParameter`; `Get-Site24x7ITAutomationLog`, `Get-Site24x7AuditLog`, `Get-Site24x7OnPremisePoller` |
| Integrations | `Get-`, `New-`, `Set-`, `Remove-Site24x7Integration` (Slack, PagerDuty, Opsgenie, ServiceNow, Teams, webhooks and others); `New-`, `Set-Site24x7Webhook` as typed shortcuts for webhooks |
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
- **Throttling.** When Site24x7 answers 429, the cmdlet waits as asked (up to 60 seconds, whether
  `Retry-After` gives seconds or a date) and retries up to three times; a longer wait is reported
  as an error.
- **Token renewal.** The session renews its access token a minute before it expires. When the API
  rejects a token with 401, it is renewed once and the request retried, but only if the error
  is about the token and the token is more than a minute old: Zoho allows only about ten new
  tokens per ten minutes, and a 401 for a missing permission would fail the same way with a new
  token. After a failed renewal the module waits 30 seconds before asking Zoho again.
- **Unexpected answers.** A successful answer that is not JSON, such as a proxy's sign-in page, is
  an error. `Invoke-Site24x7Api -Raw` returns such text as it is, for endpoints like CSV exports.
  Responses over 256 MB are refused.
- **Safety.** Ids are checked before they go into a URL, customer ids before they go into a
  cookie. `Invoke-Site24x7Api` takes only paths relative to the API root and refuses full URLs,
  backslashes and anything that would resolve outside the root, so the token is never sent
  elsewhere. Credentials are only sent over https (http is allowed for localhost, for testing).

## Coming from version 1

Version 2 is a clean break; version 1 scripts need updating. The main changes:

- Cmdlets and parameters use PowerShell naming: `-MonitorId` instead of `-monitor_id`,
  `-UserGroupId` instead of `-user_groups`, `Set-*` instead of `Update-*`.
- Monitor groups are `Get-`, `New-`, `Set-`, `Remove-Site24x7MonitorGroup` with `-MonitorGroupId`,
  and `Update-Site24x7Group_AddMonitor` is `Add-Site24x7MonitorGroupMember`.
- Third-party integrations are `Get-` and `Remove-Site24x7Integration`, with `-IntegrationId`
  everywhere (including on monitors and monitor groups).
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
[`src/Site24x7/Core/Endpoints.cs`](src/Site24x7/Core/Endpoints.cs), and the help for the cmdlets
that use them says so: alarms, alert logs, attribute groups, resource check profiles, OAuth
providers, web tokens, global parameters, IT automation logs, audit logs, configuration rules,
scheduled reports, SLA settings, status pages, on-premise pollers, and the by-name start/end
maintenance paths (kept from version 1).

To check them, connect to your account and run the read-only check script. It sends one GET to
each path that can be read and reports which ones Site24x7 recognises:

```powershell
Connect-Site24x7 ...
./tools/Test-Site24x7Endpoints.ps1 | Format-Table
```

For each path that comes back OK, remove its `// verify` comment and run
`tools/Build-ResourceCmdlets.ps1`; a "Wrong path" result is a one-line fix in the same file.

## Development

```
src/Site24x7/Core/       HTTP client, OAuth, JSON, sessions, data centers, enums, endpoint paths
src/Site24x7/Cmdlets/    Cmdlets, one file per area, on shared base classes in ResourceCmdlets.cs
src/Site24x7/Cmdlets/Generated/   Simple CRUD areas, generated by tools/Build-ResourceCmdlets.ps1
module/                  Manifest and formatting
tests/                   Pester 5 tests against a mock Site24x7 server (no account needed)
tools/                   The CRUD generator, the help builder, and the endpoint check script
```

- `./build.ps1` builds into `out/Site24x7`, writes Get-Help from the doc comments, and refreshes the
  cmdlet list and version in the manifest. The version is set only in `Site24x7.csproj`.
- `./build.ps1 -Test` also runs the tests (needs Pester 5: `Install-Module Pester -RequiredVersion 5.7.1`).
  Every generated area is tested from the generator's table, so a new area is covered automatically;
  hand-written cmdlets are listed in the `$handWritten` table in the tests, and a test fails when
  any exported cmdlet is never called.
- To add a simple API area, or a field to one, edit the table in `tools/Build-ResourceCmdlets.ps1`
  and run it. Each resource's path, label and API version live there. Areas with special
  behaviour (monitors, users, maintenance) generate only Get and Remove there and have
  hand-written New and Set cmdlets.
- The build fails when a cmdlet or parameter has no help text.
- Help comes from `/// <summary>` comments: `<para type="synopsis">` and `<para type="description">`
  on the cmdlet class, `<example><code>` for examples, and a summary on each parameter.

## License

GPL-3.0. See [LICENSE](LICENSE).
