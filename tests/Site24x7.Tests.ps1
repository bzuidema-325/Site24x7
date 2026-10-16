#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0' }

# Keep this file ASCII: Windows PowerShell 5.1 reads BOM-less files in the system code page.

BeforeDiscovery {
    $root = Split-Path $PSScriptRoot -Parent
    $endpointsText = Get-Content (Join-Path $root 'src/Site24x7/Core/Endpoints.cs') -Raw
    $paths = @{}
    foreach ($m in [regex]::Matches($endpointsText, 'const string (\w+) = "([^"]+)"')) { $paths[$m.Groups[1].Value] = $m.Groups[2].Value }

    # One test case per generated API area, straight from the generator's table.
    $table = & (Join-Path $root 'tools/Build-ResourceCmdlets.ps1') -TableOnly
    $generated = foreach ($r in $table) {
        @{
            Noun       = $r.Noun
            Path       = $paths[$r.Path]
            Id         = $r.Id
            ListOnly   = [bool]$r.ListOnly
            Verbs      = @($r.Verbs)
            Mandatory  = @($r.Fields | Where-Object { $_.NewMandatory } | ForEach-Object { $_.Name })
            ApiVersion = $(if ($r.ApiVersion) { $r.ApiVersion } else { '2.0' })
        }
    }

    # One test case per hand-written cmdlet that the other tests do not already drive. Run is the
    # command; the request it sends must match Method and Path, carry the Body fields, and (for
    # form or query requests) match the Form and Query patterns.
    $handWritten = @(
        @{ Run = "New-Site24x7Monitor 'm' PING -Property @{ host_name = 'h' }"; Method = 'POST'; Path = '/api/monitors'; Body = @{ type = 'PING'; display_name = 'm'; host_name = 'h' } }
        @{ Run = "New-Site24x7RestApiMonitor 'api' 'https://e.example/x' -RequestBody '{}' -RequestContentType J -OAuthProviderId O1"; Method = 'POST'; Path = '/api/monitors'; Body = @{ type = 'RESTAPI'; website = 'https://e.example/x'; request_param = '{}'; request_content_type = 'J'; oauth2_provider = 'O1' } }
        @{ Run = "New-Site24x7SslMonitor 'c' example.com"; Method = 'POST'; Path = '/api/monitors'; Body = @{ type = 'SSL_CERT'; domain_name = 'example.com'; port = 443; expire_days = 30; timeout = 30 } }
        @{ Run = "New-Site24x7PortMonitor 'p' h.example 8443 -UseSsl -Command QUIT"; Method = 'POST'; Path = '/api/monitors'; Body = @{ type = 'PORT'; host_name = 'h.example'; port = 8443; use_ssl = $true; command = 'QUIT' } }
        @{ Run = "New-Site24x7DnsMonitor 'd' ns1.example example.com -LookupType MX"; Method = 'POST'; Path = '/api/monitors'; Body = @{ type = 'DNS'; dns_host = 'ns1.example'; domain_name = 'example.com'; lookup_type = 15; dns_port = '53' } }
        @{ Run = "New-Site24x7HeartbeatMonitor 'hb' nightly-job"; Method = 'POST'; Path = '/api/monitors'; Body = @{ type = 'HEARTBEAT'; name_in_ping_url = 'nightly-job' } }
        @{ Run = "New-Site24x7DomainExpiryMonitor 'de' example.com -WhoisServer whois.example"; Method = 'POST'; Path = '/api/monitors'; Body = @{ type = 'DOMAINEXPIRY'; host_name = 'example.com'; expire_days = 30; whois_server = 'whois.example' } }
        @{ Run = "New-Site24x7MonitorGroup 'g' -MonitorId 1, 2 -IntegrationId S1"; Method = 'POST'; Path = '/api/monitor_groups'; Body = @{ display_name = 'g'; monitors = @('1', '2'); third_party_services = @('S1') }; ApiVersion = '2.1' }
        @{ Run = "New-Site24x7Subgroup 's' -TopGroupId T -ParentGroupId P"; Method = 'POST'; Path = '/api/subgroups'; Body = @{ display_name = 's'; top_group_id = 'T'; parent_group_id = 'P' } }
        @{ Run = "Set-Site24x7Subgroup 4 -Description d"; Method = 'PUT'; Path = '/api/subgroups/4'; Body = @{ description = 'd' } }
        @{ Run = "New-Site24x7UserGroup 'ug' -UserId u1"; Method = 'POST'; Path = '/api/user_groups'; Body = @{ display_name = 'ug'; users = @('u1') } }
        @{ Run = "Set-Site24x7UserGroup 3 -ProductId 0"; Method = 'PUT'; Path = '/api/user_groups/3'; Body = @{ product_id = 0 } }
        @{ Run = "Set-Site24x7Maintenance 6 -DisplayName x"; Method = 'PUT'; Path = '/api/maintenance/6'; Body = @{ display_name = 'x' } }
        @{ Run = "New-Site24x7Integration -IntegrationType slack -Property @{ name = 'n' }"; Method = 'POST'; Path = '/api/integration/slack'; Body = @{ name = 'n' } }
        @{ Run = "Set-Site24x7Integration 8 slack -Property @{ name = 'n' }"; Method = 'PUT'; Path = '/api/integration/slack/8'; Body = @{ name = 'n' } }
        @{ Run = "Set-Site24x7Monitor 5 -IntegrationId S1"; Method = 'PUT'; Path = '/api/monitors/5'; Body = @{ third_party_services = @('S1') } }
        @{ Run = "Add-Site24x7MonitorGroupMember G 1, 2"; Method = 'PUT'; Path = '/api/monitor_groups/add_monitors'; Body = @{ group_id = 'G'; monitor_ids = @('1', '2') } }
        @{ Run = "Get-Site24x7LogReport 5 -Date 2026-09-01"; Method = 'GET'; Path = '/api/reports/log_reports/5'; Query = '^date=2026-09-01$' }
        @{ Run = "Get-Site24x7MonitorType"; Method = 'GET'; Path = '/api/monitor_type_constants' }
        @{ Run = "Get-Site24x7DeviceKey"; Method = 'GET'; Path = '/api/device_key' }
        @{ Run = "Request-Site24x7AccessToken -ClientId c -ClientSecret (Secure s) -RefreshToken (Secure r) -AccountsUri `$accounts"; Method = 'POST'; Path = '/accounts/oauth/v2/token'; Form = 'grant_type=refresh_token'; Token = $true }
        @{ Run = "Revoke-Site24x7RefreshToken -RefreshToken (Secure r1) -AccountsUri `$accounts -Confirm:`$false"; Method = 'POST'; Path = '/accounts/oauth/v2/token/revoke'; Query = '^token=r1$' }
    )
}

BeforeAll {
    $root = Split-Path $PSScriptRoot -Parent
    $manifest = Join-Path $root 'out/Site24x7/Site24x7.psd1'
    if (-not (Test-Path $manifest)) { throw "Build the module first: ./build.ps1" }
    Import-Module $manifest -Force
    Import-Module (Join-Path $PSScriptRoot 'MockSite24x7.psm1') -Force

    # Every exported cmdlet must be called by some test: by name in this file, or by a generated-area test.
    $testText = Get-Content $PSCommandPath -Raw
    $generatedNames = foreach ($r in (& (Join-Path $root 'tools/Build-ResourceCmdlets.ps1') -TableOnly)) {
        foreach ($v in $r.Verbs) { "$v-Site24x7$($r.Noun)" }
    }

    $mock = Start-MockSite24x7
    $api = "$($mock.BaseUri)api/"
    $accounts = "$($mock.BaseUri)accounts/"

    function Secure([string] $text) { ConvertTo-SecureString $text -AsPlainText -Force }
    function Plain([securestring] $secure) { [System.Net.NetworkCredential]::new('', $secure).Password }

    function Connect-Mock {
        Set-MockRoute POST /accounts/oauth/v2/token @{ access_token = 'at-1'; expires_in = 3600; api_domain = 'https://www.zohoapis.com'; token_type = 'Bearer' }
        Connect-Site24x7 -ClientId cid -ClientSecret (Secure 'sec&ret') -RefreshToken (Secure 'rt') -ApiBaseUri $api -AccountsUri $accounts
    }

    function Body($request) { $request.Body | ConvertFrom-Json }
    function Ok($data) { @{ code = 0; message = 'success'; data = $data } }

    # The current access token's age decides whether a 401 renews it; tests make it look old.
    function Set-TokenAge([timespan] $age) {
        $session = Get-Site24x7Session
        $tokens = $session.GetType().GetProperty('Tokens', [System.Reflection.BindingFlags]'NonPublic,Instance').GetValue($session)
        $tokens.GetType().GetProperty('ObtainedUtc').SetValue($tokens, [datetime]::UtcNow - $age)
    }
}

AfterAll {
    Disconnect-Site24x7
    Stop-MockSite24x7
}

Describe 'Module' {
    It 'exports cmdlets for every API area, and no aliases' {
        $m = Get-Module Site24x7
        $m.ExportedCmdlets.Count | Should -BeGreaterThan 120
        $m.ExportedAliases.Count | Should -Be 0
    }

    It 'has Get-Help text for every cmdlet' {
        $missing = Get-Command -Module Site24x7 | Where-Object { -not (Get-Help $_.Name).Synopsis -or (Get-Help $_.Name).Synopsis -like "*$($_.Name)*[<]*" }
        $missing | Should -BeNullOrEmpty
        (Get-Help Connect-Site24x7 -Parameter RefreshToken).Description.Text | Should -Not -BeNullOrEmpty
    }

    It 'uses approved verbs, ShouldProcess for changes, and declares output types' {
        $approved = (Get-Verb).Verb
        foreach ($c in Get-Command -Module Site24x7) {
            $c.Verb | Should -BeIn $approved -Because "$($c.Name) needs an approved verb"
            if ($c.Verb -in 'New', 'Set', 'Remove', 'Add', 'Start', 'Stop', 'Enable', 'Disable', 'Revoke', 'Invoke') {
                $c.Parameters.ContainsKey('WhatIf') | Should -BeTrue -Because "$($c.Name) changes something"
            }
            if ($c.Verb -notin 'Remove', 'Disconnect') {
                $c.OutputType | Should -Not -BeNullOrEmpty -Because "$($c.Name) writes output"
            }
        }
    }

    It 'documents every parameter' {
        foreach ($c in Get-Command -Module Site24x7) {
            foreach ($p in (Get-Help $c.Name -Full).parameters.parameter) {
                if ($p.name -in 'WhatIf', 'Confirm') { continue }
                ($p.description.Text -join '') | Should -Not -BeNullOrEmpty -Because "$($c.Name) -$($p.name) needs help text"
            }
        }
    }

    It 'has a test that calls every cmdlet' {
        $untested = (Get-Module Site24x7).ExportedCmdlets.Keys | Where-Object {
            $_ -notin $generatedNames -and $testText -notmatch "\b$([regex]::Escape($_))\b"
        }
        $untested | Should -BeNullOrEmpty -Because 'every cmdlet needs a test (add it to $handWritten if nothing else covers it)'
    }

    It 'takes secrets as SecureString or PSCredential only' {
        foreach ($c in Get-Command -Module Site24x7) {
            foreach ($p in $c.Parameters.Values) {
                if ($p.Name -match 'Secret|Password|Token$' -and $p.ParameterType -ne [switch]) {
                    $p.ParameterType | Should -Be ([securestring]) -Because "$($c.Name) -$($p.Name) is a secret"
                }
            }
        }
    }
}

Describe 'Signing in' {
    BeforeEach { Clear-MockSite24x7 }

    It 'exchanges the refresh token at Zoho Accounts' {
        Connect-Mock
        $req = Get-MockRequest POST /accounts/oauth/v2/token | Select-Object -Last 1
        $req.Body | Should -Match 'grant_type=refresh_token'
        $req.Body | Should -Match 'client_secret=sec%26ret'
        (Get-Site24x7Session).CanRefresh | Should -BeTrue
    }

    It 'sends the access token and API version on every call' {
        Connect-Mock
        Set-MockRoute GET /api/monitors (Ok @())
        Get-Site24x7Monitor
        $req = Get-MockRequest GET /api/monitors | Select-Object -Last 1
        $req.Headers['Authorization'] | Should -Be 'Zoho-oauthtoken at-1'
        $req.Headers['Accept'] | Should -Be 'application/json; version=2.0'
    }

    It 'refreshes the token once and retries when the API answers 401' {
        Connect-Mock
        Set-TokenAge ([timespan]::FromMinutes(5))
        Set-MockRoute GET /api/monitors -Status 401 -Once -Body @{ code = 1100; message = 'Invalid token' }
        Set-MockRoute GET /api/monitors (Ok @(@{ monitor_id = '1'; display_name = 'a' }))
        Set-MockRoute POST /accounts/oauth/v2/token @{ access_token = 'at-2'; expires_in = 3600 }
        $result = Get-Site24x7Monitor
        $result.monitor_id | Should -Be '1'
        (Get-MockRequest GET /api/monitors | Select-Object -Last 1).Headers['Authorization'] | Should -Be 'Zoho-oauthtoken at-2'
    }

    It 'does not renew a token that was obtained moments ago' {
        Connect-Mock
        Set-MockRoute GET /api/monitors -Status 401 -Body @{ code = 1100; message = 'Invalid token' }
        1..5 | ForEach-Object { Get-Site24x7Monitor -ErrorAction SilentlyContinue }
        @(Get-MockRequest POST /accounts/oauth/v2/token).Count | Should -Be 1
        @(Get-MockRequest GET /api/monitors).Count | Should -Be 5
    }

    It 'does not renew the token for a 401 about something else' {
        Connect-Mock
        Set-TokenAge ([timespan]::FromMinutes(5))
        Set-MockRoute GET /api/monitors -Status 401 -Body @{ code = 2001; message = 'You do not have permission for this customer' }
        $err = $null
        Get-Site24x7Monitor -ErrorVariable err -ErrorAction SilentlyContinue
        $err[0].CategoryInfo.Category | Should -Be 'AuthenticationError'
        @(Get-MockRequest POST /accounts/oauth/v2/token).Count | Should -Be 1
    }

    It 'waits before asking Zoho again after a failed renewal' {
        Connect-Mock
        Set-TokenAge ([timespan]::FromMinutes(5))
        Set-MockRoute GET /api/monitors -Status 401 -Body @{ code = 1100; message = 'Invalid token' }
        Set-MockRoute POST /accounts/oauth/v2/token -Status 400 -Body @{ error = 'access_denied' }
        { Get-Site24x7Monitor -ErrorAction Stop } | Should -Throw '*access_denied*'
        { Get-Site24x7Monitor -ErrorAction Stop } | Should -Throw '*waits*'
        @(Get-MockRequest POST /accounts/oauth/v2/token).Count | Should -Be 2
    }

    It 'reads the token lifetime from expires_in_sec, or expires_in in milliseconds' {
        Set-MockRoute POST /accounts/oauth/v2/token @{ access_token = 'a'; expires_in = 3600000; expires_in_sec = 3600 }
        $s = Connect-Site24x7 -ClientId c -ClientSecret (Secure s) -RefreshToken (Secure r) -ApiBaseUri $api -AccountsUri $accounts -PassThru
        ($s.AccessTokenExpires - [datetime]::UtcNow).TotalMinutes | Should -BeLessOrEqual 61
        Set-MockRoute POST /accounts/oauth/v2/token @{ access_token = 'a'; expires_in = 3600000 }
        $s = Connect-Site24x7 -ClientId c -ClientSecret (Secure s) -RefreshToken (Secure r) -ApiBaseUri $api -AccountsUri $accounts -PassThru
        ($s.AccessTokenExpires - [datetime]::UtcNow).TotalMinutes | Should -BeLessOrEqual 61
        ($s.AccessTokenExpires - [datetime]::UtcNow).TotalMinutes | Should -BeGreaterThan 50
    }

    It 'disconnects even when revoking the refresh token fails' {
        Connect-Mock
        Set-MockRoute POST /accounts/oauth/v2/token/revoke -Status 500 -Body @{ error = 'server_error' }
        Disconnect-Site24x7 -RevokeRefreshToken -Confirm:$false -ErrorAction SilentlyContinue
        Get-Site24x7Session | Should -BeNullOrEmpty
    }

    It 'stops with an error when Zoho returns an error with HTTP 200' {
        Set-MockRoute POST /accounts/oauth/v2/token @{ error = 'invalid_code' }
        { Connect-Site24x7 -ClientId c -ClientSecret (Secure s) -RefreshToken (Secure bad) -ApiBaseUri $api -AccountsUri $accounts } |
            Should -Throw '*invalid_code*'
    }

    It 'returns tokens as SecureStrings from Request-Site24x7RefreshToken' {
        Set-MockRoute POST /accounts/oauth/v2/token @{ access_token = 'a'; refresh_token = 'r'; expires_in = 3600 }
        $t = Request-Site24x7RefreshToken -ClientId c -ClientSecret (Secure s) -GrantCode code -AccountsUri $accounts
        Plain $t.AccessToken | Should -Be 'a'
        Plain $t.RefreshToken | Should -Be 'r'
        $t.ExpiresUtc.Kind | Should -Be 'Utc'
        (Get-MockRequest POST /accounts/oauth/v2/token | Select-Object -Last 1).Body | Should -Match 'grant_type=authorization_code'
    }

    It 'uses the right URLs for each data center' {
        $s = Connect-Site24x7 -AccessToken (Secure abc) -DataCenter EU -PassThru
        $s.ApiBaseUri | Should -Be 'https://www.site24x7.eu/api/'
        $s.AccountsUri | Should -Be 'https://accounts.zoho.eu/'
        $s = Connect-Site24x7 -AccessToken (Secure abc) -DataCenter CA -PassThru
        $s.ApiBaseUri | Should -Be 'https://www.site24x7.ca/api/'
        $s.AccountsUri | Should -Be 'https://accounts.zohocloud.ca/'
    }

    It 'refuses to send credentials over plain http to another machine' {
        { Connect-Site24x7 -AccessToken (Secure abc) -ApiBaseUri 'http://example.com/api/' } | Should -Throw '*https*'
        { Connect-Site24x7 -ClientId c -ClientSecret (Secure s) -RefreshToken (Secure r) -AccountsUri 'http://example.com/' } | Should -Throw '*https*'
    }

    It 'fails clearly when not connected' {
        Disconnect-Site24x7
        { Get-Site24x7Monitor -ErrorAction Stop } | Should -Throw '*Connect-Site24x7*'
    }

    It 'leaves the session alone with Disconnect-Site24x7 -WhatIf' {
        Connect-Site24x7 -AccessToken (Secure abc) -ApiBaseUri $api
        Disconnect-Site24x7 -WhatIf
        Get-Site24x7Session | Should -Not -BeNullOrEmpty
        Disconnect-Site24x7
        Get-Site24x7Session | Should -BeNullOrEmpty
    }

    It 'keeps sessions separate per runspace' {
        Connect-Mock
        $ps = [powershell]::Create()
        try {
            [void]$ps.AddScript({ param($m) Import-Module $m; Get-Site24x7Session }).AddArgument($manifest)
            $ps.Invoke() | Should -BeNullOrEmpty
        } finally {
            $ps.Dispose()
        }
        Get-Site24x7Session | Should -Not -BeNullOrEmpty
    }

    It 'sends the zaaid cookie after Set-Site24x7Context, without changing saved sessions' {
        Connect-Mock
        $saved = Get-Site24x7Session
        Set-Site24x7Context -CustomerId 12345
        Get-Site24x7Tag | Out-Null
        (Get-MockRequest GET /api/tags).Headers['Cookie'] | Should -Be 'zaaid=12345'
        $saved.CustomerId | Should -BeNullOrEmpty
        (Get-Site24x7Session).CustomerId | Should -Be '12345'
        Set-Site24x7Context -Reset
        (Get-Site24x7Session).CustomerId | Should -BeNullOrEmpty
    }

    It 'refuses a customer id that could add headers' {
        Connect-Mock
        { Set-Site24x7Context -CustomerId "1`r`nX-Evil: 1" } | Should -Throw
        { Set-Site24x7Context -CustomerId '1; zaaid=2' } | Should -Throw
        (Get-Site24x7Session).CustomerId | Should -BeNullOrEmpty
    }
}

Describe 'Reading' {
    BeforeAll { Connect-Mock }
    BeforeEach {
        Clear-MockSite24x7
        Set-MockRoute GET /api/monitors (Ok @(
            @{ monitor_id = '1'; display_name = 'Web Home'; type = 'URL'; state = 0 }
            @{ monitor_id = '2'; display_name = 'API Orders'; type = 'RESTAPI'; state = 0 }
            @{ monitor_id = '3'; display_name = 'Web Shop'; type = 'URL'; state = 5 }
        ))
    }

    It 'lists every monitor, one object per monitor' {
        $m = Get-Site24x7Monitor
        $m.Count | Should -Be 3
        $m[0].PSObject.TypeNames[0] | Should -Be 'Site24x7.Monitor'
    }

    It 'filters by name with wildcards' {
        (Get-Site24x7Monitor -Name 'web*').monitor_id | Should -Be @('1', '3')
    }

    It 'matches a name with wildcard characters exactly, and reports a plain name that matches nothing' {
        Set-MockRoute GET /api/monitors (Ok @(@{ monitor_id = '5'; display_name = 'web [prod]' }))
        (Get-Site24x7Monitor -Name 'web [prod]').monitor_id | Should -Be '5'
        $err = $null
        Get-Site24x7Monitor -Name 'nothing' -ErrorVariable err -ErrorAction SilentlyContinue | Should -BeNullOrEmpty
        $err[0].CategoryInfo.Category | Should -Be 'ObjectNotFound'
        Get-Site24x7Monitor -Name 'nothing*' -ErrorAction Stop | Should -BeNullOrEmpty
    }

    It 'filters by type, including when the API returns a single object' {
        (Get-Site24x7Monitor -MonitorType RESTAPI).monitor_id | Should -Be '2'
        Set-MockRoute GET /api/monitors (Ok @{ monitor_id = '9'; display_name = 'Only'; type = 'URL' })
        (Get-Site24x7Monitor -MonitorType URL).monitor_id | Should -Be '9'
    }

    It 'gets one monitor by id, including from API objects in the pipeline' {
        Set-MockRoute GET /api/monitors/2 (Ok @{ monitor_id = '2'; display_name = 'API Orders' })
        (Get-Site24x7Monitor 2).display_name | Should -Be 'API Orders'
        ([pscustomobject]@{ monitor_id = '2' } | Get-Site24x7Monitor).display_name | Should -Be 'API Orders'
    }

    It 'turns API errors into PowerShell errors with the API code' {
        Set-MockRoute GET /api/monitors/404 -Status 404 -Body @{ code = 1001; message = 'Resource not found'; error_info = @{ monitor_id = 'unknown' } }
        $err = $null
        Get-Site24x7Monitor 404 -ErrorVariable err -ErrorAction SilentlyContinue
        $err[0].FullyQualifiedErrorId | Should -BeLike 'Site24x7.Api.1001*'
        $err[0].CategoryInfo.Category | Should -Be 'ObjectNotFound'
        $err[0].Exception.Message | Should -Match 'Resource not found'
        { Get-Site24x7Monitor 404 -ErrorAction Stop } | Should -Throw
    }

    It 'keeps going through the pipeline after one failure, even an odd one' {
        Set-MockRoute GET /api/monitors/1 (Ok @{ monitor_id = '1' })
        Set-MockRoute GET /api/monitors/2 -Body '{"code":"INVALID_TOKEN","message":"odd"}'
        Set-MockRoute GET /api/monitors/3 -Body '{"code":99999999999,"message":"big"}'
        Set-MockRoute GET /api/monitors/4 (Ok @{ monitor_id = '4' })
        $err = $null
        $ok = '1', '2', '3', '4' | ForEach-Object { [pscustomobject]@{ monitor_id = $_ } } | Get-Site24x7Monitor -ErrorVariable err -ErrorAction SilentlyContinue
        $ok.monitor_id | Should -Be @('1', '4')
        $err.Count | Should -Be 2
        $err[0].Exception.Message | Should -Match 'INVALID_TOKEN'
    }

    It 'uses API version 2.1 for monitor groups, and their formatting' {
        Set-MockRoute GET /api/monitor_groups (Ok @(@{ group_id = '1'; display_name = 'g' }))
        (Get-Site24x7MonitorGroup).PSObject.TypeNames[0] | Should -Be 'Site24x7.MonitorGroup'
        (Get-FormatData Site24x7.MonitorGroup) | Should -Not -BeNullOrEmpty
        (Get-MockRequest GET /api/monitor_groups).Headers['Accept'] | Should -Be 'application/json; version=2.1'
    }

    It 'builds current status paths and flags' {
        Get-Site24x7CurrentStatus -MonitorGroupId 77 -IncludeSuspended | Out-Null
        $req = Get-MockRequest GET /api/current_status/group/77
        $req.Query | Should -Be 'suspended_required=true'
    }

    It 'uses a custom period when dates are given for a report' {
        Get-Site24x7Report -Report AvailabilitySummary -MonitorId 5 -StartDate 2026-09-01 -EndDate 2026-09-30 | Out-Null
        $req = Get-MockRequest GET /api/reports/availability_summary/5
        $req.Query | Should -Match 'period=50'
        $req.Query | Should -Match 'start_date=2026-09-01'
        $req.Query | Should -Match 'end_date=2026-09-30'
    }

    It 'needs both report dates, and not -Period with them' {
        { Get-Site24x7Report Summary 5 -StartDate 2026-09-01 -ErrorAction Stop } | Should -Throw '*both -StartDate and -EndDate*'
        { Get-Site24x7Report Summary 5 -Period Last7Days -StartDate 2026-09-01 -EndDate 2026-09-02 -ErrorAction Stop } | Should -Throw '*not both*'
        Get-MockRequest GET | Should -BeNullOrEmpty
    }

    It 'refuses a hashtable as a query value' {
        { Get-Site24x7Tag -Query @{ x = @{ a = 1 } } -ErrorAction Stop } | Should -Throw '*query value*'
    }

    It 'lets -Query replace a report option whatever its case' {
        Get-Site24x7Report Summary 5 -Query @{ Period = 3 } | Out-Null
        (Get-MockRequest GET /api/reports/summary/5).Query | Should -Be 'Period=3'
    }

    It 'escapes ids in the URL path' {
        # A space rather than a slash: .NET Framework un-escapes %2F in http paths.
        Get-Site24x7Tag -TagId 'a b' | Out-Null
        (Get-MockRequest GET).Path | Should -Match '^/api/tags/a(%20| )b$'
    }

    It 'refuses ids that would change which URL is called' {
        foreach ($id in '..', '.', 'a/b', 'a\b', '%2e%2e') {
            $err = $null
            Remove-Site24x7Monitor -MonitorId $id -Confirm:$false -ErrorVariable err -ErrorAction SilentlyContinue
            $err[0].CategoryInfo.Category | Should -Be 'InvalidArgument' -Because "'$id' is not an id"
        }
        Get-MockRequest DELETE | Should -BeNullOrEmpty
    }

    It 'waits out a short Retry-After and retries' {
        Set-MockRoute GET /api/tags -Status 429 -Once -Headers @{ 'Retry-After' = '1' } -Body @{ code = 429; message = 'slow down' }
        Set-MockRoute GET /api/tags (Ok @(@{ tag_id = '1'; tag_name = 't' }))
        (Get-Site24x7Tag).tag_id | Should -Be '1'
        @(Get-MockRequest GET /api/tags).Count | Should -Be 2
    }

    It 'understands Retry-After given as a date' {
        $soon = [datetime]::UtcNow.AddSeconds(2).ToString('r')
        Set-MockRoute GET /api/tags -Status 429 -Once -Headers @{ 'Retry-After' = $soon } -Body @{ code = 429; message = 'slow down' }
        Set-MockRoute GET /api/tags (Ok @(@{ tag_id = '1' }))
        (Get-Site24x7Tag).tag_id | Should -Be '1'
        Clear-MockSite24x7
        $late = [datetime]::UtcNow.AddHours(2).ToString('r')
        Set-MockRoute GET /api/tags -Status 429 -Headers @{ 'Retry-After' = $late } -Body @{ code = 429; message = 'slow down' }
        { Get-Site24x7Tag -ErrorAction Stop } | Should -Throw '*slow down*'
        @(Get-MockRequest GET /api/tags).Count | Should -Be 1
    }

    It 'reports a successful answer that is not JSON as an error' {
        Set-MockRoute GET /api/tags -Body '<html>Sign in</html>' -ContentType 'text/html'
        $err = $null
        Get-Site24x7Tag -ErrorVariable err -ErrorAction SilentlyContinue | Should -BeNullOrEmpty
        $err[0].Exception.Message | Should -Match 'text/html instead of JSON'
        Invoke-Site24x7Api GET tags -Raw | Should -Be '<html>Sign in</html>'
    }

    It 'checks the unverified API paths with tools/Test-Site24x7Endpoints.ps1, read-only' {
        $results = & (Join-Path $root 'tools/Test-Site24x7Endpoints.ps1')
        ($results | Where-Object Constant -eq 'Alarms').Result | Should -Be 'OK'
        ($results | Where-Object Constant -eq 'StartMaintenanceByName').Result | Should -Be 'Not checked'
        Get-MockRequest | Where-Object Method -ne 'GET' | Should -BeNullOrEmpty
    }

    It 'reports a long Retry-After instead of hanging' {
        Set-MockRoute GET /api/tags -Status 429 -Headers @{ 'Retry-After' = '3600' } -Body @{ code = 429; message = 'slow down' }
        $elapsed = Measure-Command { { Get-Site24x7Tag -ErrorAction Stop } | Should -Throw '*slow down*' }
        $elapsed.TotalSeconds | Should -BeLessThan 10
    }

    It 'stops a hung request when the pipeline is stopped' {
        # A listener that accepts connections and never answers.
        $hang = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
        $hang.Start()
        $ps = [powershell]::Create()
        try {
            $url = "http://127.0.0.1:$($hang.LocalEndpoint.Port)/api/"
            [void]$ps.AddScript({
                param($m, $url)
                Import-Module $m
                Connect-Site24x7 -AccessToken (ConvertTo-SecureString x -AsPlainText -Force) -ApiBaseUri $url
                Get-Site24x7Monitor
            }).AddArgument($manifest).AddArgument($url)
            $handle = $ps.BeginInvoke()
            Start-Sleep -Seconds 2
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            $ps.Stop()
            $sw.Elapsed.TotalSeconds | Should -BeLessThan 5
        } finally {
            $ps.Dispose()
            $hang.Stop()
        }
    }
}

Describe 'Writing' {
    BeforeAll { Connect-Mock }
    BeforeEach { Clear-MockSite24x7 }

    It 'creates a website monitor with sensible defaults' {
        $cred = [pscredential]::new('bob', (Secure 'pw'))
        New-Site24x7WebsiteMonitor 'Home' 'https://example.com' -LocationProfileId L -UserGroupId U1, U2 -Header @{ 'X-Test' = '1' } -Keyword 'Welcome' -Credential $cred | Out-Null
        $b = Body (Get-MockRequest POST /api/monitors)
        $b.type | Should -Be 'URL'
        $b.display_name | Should -Be 'Home'
        $b.website | Should -Be 'https://example.com'
        $b.http_method | Should -Be 'G'
        $b.timeout | Should -Be 30
        $b.check_frequency | Should -Be '5'
        $b.user_group_ids | Should -Be @('U1', 'U2')
        $b.custom_headers[0].name | Should -Be 'X-Test'
        $b.matching_keyword.value | Should -Be 'Welcome'
        $b.auth_user | Should -Be 'bob'
        $b.auth_pass | Should -Be 'pw'
        $b.PSObject.Properties.Name | Should -Not -Contain 'notification_profile_id'
    }

    It 'keeps passwords out of the debug stream' {
        $DebugPreference = 'Continue'
        $cred = [pscredential]::new('bob', (Secure 'hunter2'))
        $debug = New-Site24x7WebsiteMonitor 'Home' 'https://example.com' -Credential $cred 5>&1 | Where-Object { $_ -is [System.Management.Automation.DebugRecord] }
        ($debug.Message -join "`n") | Should -Match 'Request body'
        ($debug.Message -join "`n") | Should -Not -Match 'hunter2'
    }

    It 'keeps secrets in headers, keys, string bodies and responses out of the debug stream' {
        $DebugPreference = 'Continue'
        Set-MockRoute POST /api/monitors (Ok @{ monitor_id = '1'; auth_pass = 'echoed-secret'; custom_headers = @(@{ name = 'Authorization'; value = 'Bearer resp-token' }) })
        Set-MockRoute POST /api/custom (Ok @{})
        $debug = & {
            New-Site24x7WebsiteMonitor 'Home' 'https://example.com' -Header @{ Authorization = 'Bearer SUPERSECRET'; 'X-Plain' = 'visible' } 5>&1
            Invoke-Site24x7Api POST custom -Body '{"password":"PW2","note":"ok"}' -Confirm:$false 5>&1
            Invoke-Site24x7Api POST custom -Body @{ api_key = 'KEY3' } -Confirm:$false 5>&1
            Invoke-Site24x7Api POST custom -Body 'not json PW4' -Confirm:$false 5>&1
        } | Where-Object { $_ -is [System.Management.Automation.DebugRecord] }
        $text = $debug.Message -join "`n"
        foreach ($secret in 'SUPERSECRET', 'PW2', 'KEY3', 'PW4', 'echoed-secret', 'resp-token') {
            $text | Should -Not -Match $secret -Because "$secret must not be logged"
        }
        $text | Should -Match 'visible'
        $text | Should -Match '"note":"ok"'
    }

    It 'sends a SecureString in -Property as its value' {
        New-Site24x7Monitor 'm' URL -Property @{ auth_pass = (Secure 'pw-from-vault') } | Out-Null
        (Body (Get-MockRequest POST /api/monitors)).auth_pass | Should -Be 'pw-from-vault'
    }

    It 'lets -Property replace a typed setting whatever its case' {
        New-Site24x7WebsiteMonitor 'Home' 'https://example.com' -Timeout 10 -Property @{ Timeout = 5 } | Out-Null
        $req = Get-MockRequest POST /api/monitors
        $b = Body $req
        $b.timeout | Should -Be 5
        ([regex]::Matches($req.Body, '"timeout"', 'IgnoreCase')).Count | Should -Be 1
    }

    It 'lets -Property override and extend the typed parameters' {
        New-Site24x7WebsiteMonitor 'Home' 'https://example.com' -Timeout 10 -Property @{ timeout = 45; follow_redirects = $true } | Out-Null
        $b = Body (Get-MockRequest POST /api/monitors)
        $b.timeout | Should -Be 45
        $b.follow_redirects | Should -BeTrue
    }

    It 'does nothing with -WhatIf' {
        New-Site24x7PingMonitor 'p' 'host.example.com' -WhatIf
        Get-MockRequest POST /api/monitors | Should -BeNullOrEmpty
    }

    It 'updates only the settings given, including false and zero' {
        Set-Site24x7MonitorGroup -MonitorGroupId 9 -Description '' -HealthThresholdCount 0 -SuppressAlert:$false -Confirm:$false | Out-Null
        $req = Get-MockRequest PUT /api/monitor_groups/9
        $b = Body $req
        @($b.PSObject.Properties.Name | Sort-Object) | Should -Be @('description', 'health_threshold_count', 'suppress_alert')
        $b.health_threshold_count | Should -Be 0
        $b.suppress_alert | Should -BeFalse
        $req.Headers['Accept'] | Should -Be 'application/json; version=2.1'
    }

    It 'refuses an update with nothing to change once, before reading the pipeline' {
        $err = $null
        { '1', '2', '3' | ForEach-Object { [pscustomobject]@{ tag_id = $_ } } | Set-Site24x7Tag -ErrorAction Stop } |
            Should -Throw '*Nothing to update*'
        Get-MockRequest PUT | Should -BeNullOrEmpty
    }

    It 'refuses to create something from nothing, and empty strings in generated parameters' {
        { New-Site24x7WebToken -Property @{} -ErrorAction Stop } | Should -Throw '*Nothing to create*'
        { Set-Site24x7Tag 5 -TagName '' -ErrorAction Stop } | Should -Throw
        Get-MockRequest | Should -BeNullOrEmpty
    }

    It 'merges with the current monitor when asked' {
        Set-MockRoute GET /api/monitors/5 (Ok @{ monitor_id = '5'; display_name = 'Old'; type = 'URL'; website = 'https://a' })
        Set-Site24x7Monitor 5 -DisplayName New -MergeWithCurrent -Confirm:$false | Out-Null
        $b = Body (Get-MockRequest PUT /api/monitors/5)
        $b.display_name | Should -Be 'New'
        $b.website | Should -Be 'https://a'
        $b.PSObject.Properties.Name | Should -Not -Contain 'monitor_id'
    }

    It 'sends user roles and alert settings as the API numbers' {
        New-Site24x7User -DisplayName Jo -EmailAddress jo@example.com -UserRole SuperAdministrator -NotifyMedium Email, SMS -AlertDown Email -AlertDayExclusion Sunday, Saturday -AlertStartTime 08:00 -AlertEndTime 18:00 -JobTitle DevOpsEngineer -UserGroupId G1 | Out-Null
        $b = Body (Get-MockRequest POST /api/users)
        $b.user_role | Should -Be 1
        $b.notify_medium | Should -Be @(1, 2)
        $b.job_title | Should -Be 3
        $b.user_groups | Should -Be @('G1')
        $b.alert_settings.down | Should -Be @(1)
        $b.alert_settings.dont_alert_on_days | Should -Be @(0, 6)
        $b.alert_settings.alerting_period.start_time | Should -Be '08:00'
        $b.PSObject.Properties.Name | Should -Not -Contain 'mobile_settings'
    }

    It 'updates the user given by id' {
        Set-Site24x7User -UserId 42 -UserRole Operator -Confirm:$false | Out-Null
        (Body (Get-MockRequest PUT /api/users/42)).user_role | Should -Be 3
    }

    It 'checks maintenance settings for the maintenance type' {
        { New-Site24x7Maintenance 'm' MonthlyByDay -StartTime 01:00 -EndTime 02:00 -MonitorId 1 -ErrorAction Stop } |
            Should -Throw '*-StartWeek, -StartDay, -Duration*'
        { New-Site24x7Maintenance 'm' Daily -StartTime 01:00 -EndTime 02:00 -ErrorAction Stop } |
            Should -Throw '*-MonitorId, -MonitorGroupId or -TagId*'
        Get-MockRequest POST | Should -BeNullOrEmpty
    }

    It 'refuses a one-time maintenance that ends before it starts' {
        { New-Site24x7Maintenance 'm' Once -StartDate 2026-11-01 -EndDate 2026-11-01 -StartTime 23:00 -EndTime 22:00 -MonitorId 1 -ErrorAction Stop } |
            Should -Throw '*ends before it starts*'
    }

    It 'starts maintenance for monitors piped in' {
        [pscustomobject]@{ monitor_id = '1'; display_name = 'a' } | Start-Site24x7Maintenance -Duration 15 | Out-Null
        $b = Body (Get-MockRequest POST /api/start_maintenance)
        $b.monitors | Should -Be @('1')
        $b.selection_type | Should -Be 2
    }

    It 'creates a one-time maintenance' {
        New-Site24x7Maintenance 'Patch' Once -StartDate 2026-11-01 -EndDate 2026-11-02 -StartTime 22:00 -EndTime 23:30 -MonitorGroupId G1 -SubgroupMonitors | Out-Null
        $b = Body (Get-MockRequest POST /api/maintenance)
        $b.maintenance_type | Should -Be 3
        $b.start_date | Should -Be '2026-11-01'
        $b.selection_type | Should -Be 1
        $b.monitor_groups | Should -Be @('G1')
        $b.subgroup_monitors | Should -BeTrue
    }

    It 'starts maintenance by name on the names endpoint' {
        Start-Site24x7Maintenance -MonitorName web01, web02 -Duration 30 | Out-Null
        $b = Body (Get-MockRequest POST /api/start_maintenance/names)
        $b.selection_type | Should -Be 2
        $b.monitor_names | Should -Be @('web01', 'web02')
        $b.duration | Should -Be 30
    }

    It 'ends a maintenance window by id' {
        Stop-Site24x7Maintenance -MaintenanceId 77 | Out-Null
        (Body (Get-MockRequest POST /api/end_maintenance)).maintenance_id | Should -Be '77'
    }

    It 'deletes API objects from the pipeline and writes nothing' {
        $out = [pscustomobject]@{ monitor_id = '8' }, [pscustomobject]@{ monitor_id = '9' } | Remove-Site24x7Monitor -Confirm:$false
        $out | Should -BeNullOrEmpty
        (Get-MockRequest DELETE).Path | Should -Be @('/api/monitors/8', '/api/monitors/9')
    }

    It 'does not bind unrelated pipeline objects to an id' {
        $err = $null
        [pscustomobject]@{ monitor_id = '8' } | Remove-Site24x7Tag -Confirm:$false -ErrorVariable err -ErrorAction SilentlyContinue
        $err | Should -Not -BeNullOrEmpty
        Get-MockRequest DELETE | Should -BeNullOrEmpty
    }

    It 'suspends and activates monitors' {
        Disable-Site24x7Monitor 3 | Out-Null
        Enable-Site24x7Monitor 3 | Out-Null
        (Get-MockRequest PUT).Path | Should -Be @('/api/monitors/suspend/3', '/api/monitors/activate/3')
    }

    It 'adds monitors to a group by name' {
        Add-Site24x7MonitorGroupMember -MonitorGroupName Web -MonitorName a, b | Out-Null
        $b = Body (Get-MockRequest PUT /api/monitor_groups/add_monitors)
        $b.group_name | Should -Be 'Web'
        $b.monitor_names | Should -Be @('a', 'b')
    }

    It 'creates a webhook with defaults and sends only changes on update' {
        New-Site24x7Webhook 'Ops' 'https://hooks.example.com/x' -TagId T1 | Out-Null
        $b = Body (Get-MockRequest POST /api/integration/webhooks)
        $b.method | Should -Be 'P'
        $b.timeout | Should -Be 30
        $b.selection_type | Should -Be 3
        $b.tags | Should -Be @('T1')
        Set-Site24x7Webhook 5 -Timeout 10 -Confirm:$false | Out-Null
        @((Body (Get-MockRequest PUT /api/integration/webhooks/5)).PSObject.Properties.Name) | Should -Be @('timeout')
    }

    It 'sends a credential profile''s password from a PSCredential' {
        New-Site24x7CredentialProfile 'db' -Credential ([pscredential]::new('sa', (Secure 'pw'))) | Out-Null
        $b = Body (Get-MockRequest POST /api/credential_profiles)
        $b.username | Should -Be 'sa'
        $b.password | Should -Be 'pw'
    }

    It 'calls any endpoint through Invoke-Site24x7Api' {
        Set-MockRoute POST /api/custom/thing (Ok @{ id = 1 })
        $r = Invoke-Site24x7Api POST custom/thing -Body @{ x = 'y' } -Confirm:$false
        $r.id | Should -Be 1
        (Body (Get-MockRequest POST /api/custom/thing)).x | Should -Be 'y'
        Invoke-Site24x7Api GET '/api/monitors' | Out-Null
        Get-MockRequest GET /api/monitors | Should -Not -BeNullOrEmpty
    }

    It 'never sends the token outside the API root' {
        $bad = @(
            'https://example.com/collect', "$($api)monitors", ' https://evil.example/x', "`thttps://evil.example/x",
            '\/evil.example/x', '\\evil.example/x', '//evil.example/x', '..\accounts\oauth', '../oauth/x', 'api/../../x',
            'ftp://evil.example/', 'file:///etc/passwd', 'mailto:x@evil.example', "monitors`r`nX: 1"
        )
        foreach ($endpoint in $bad) {
            $err = $null
            Invoke-Site24x7Api GET $endpoint -ErrorVariable err -ErrorAction SilentlyContinue
            $err[0].CategoryInfo.Category | Should -Be 'InvalidArgument' -Because "'$endpoint' leaves the API root"
        }
        @(Get-MockRequest).Count | Should -Be 0
        { Invoke-Site24x7Api GET monitors -Body @{ a = 1 } -ErrorAction Stop } | Should -Throw '*GET request cannot have a body*'
    }

    It 'keeps going through the pipeline after an unusable endpoint' {
        $err = $null
        $out = '\\evil.example/x', 'tags' | ForEach-Object { Invoke-Site24x7Api GET $_ -ErrorVariable +err -ErrorAction SilentlyContinue }
        @($err).Count | Should -Be 1
        Get-MockRequest GET /api/tags | Should -Not -BeNullOrEmpty
    }

    It 'round-trips awkward text through JSON' {
        # Non-ASCII characters are built from code points so the file stays ASCII.
        $text = "quote `" backslash \ tab `t newline `n unicode " + [char]0x00E9 + [char]0x2713
        New-Site24x7Tag -TagName $text | Out-Null
        (Body (Get-MockRequest POST /api/tags)).tag_name | Should -Be $text
    }

    It 'refuses numbers JSON cannot carry' {
        { New-Site24x7Tag -TagName t -Property @{ x = [double]::NaN } -ErrorAction Stop } | Should -Throw '*JSON*'
    }
}

Describe 'Generated area <Noun>' -ForEach $generated {
    BeforeAll { Connect-Mock }
    BeforeEach { Clear-MockSite24x7 }

    It 'lists with GET /api/<Path>' {
        & "Get-Site24x7$Noun" | Out-Null
        $req = Get-MockRequest GET "/api/$Path"
        $req | Should -Not -BeNullOrEmpty
        $req.Headers['Accept'] | Should -Be "application/json; version=$ApiVersion"
    }

    It 'gets, updates and deletes by id' -Skip:$ListOnly {
        $id = @{ $Id = '7' }
        & "Get-Site24x7$Noun" @id | Out-Null
        Get-MockRequest GET "/api/$Path/7" | Should -Not -BeNullOrEmpty
        if ($Verbs -contains 'Set') {
            & "Set-Site24x7$Noun" @id -Property @{ a = 1 } -Confirm:$false | Out-Null
            (Body (Get-MockRequest PUT "/api/$Path/7")).a | Should -Be 1
        }
        if ($Verbs -contains 'Remove') {
            & "Remove-Site24x7$Noun" @id -Confirm:$false
            Get-MockRequest DELETE "/api/$Path/7" | Should -Not -BeNullOrEmpty
        }
    }

    It 'creates with POST /api/<Path>' -Skip:($Verbs -notcontains 'New') {
        $params = @{ Property = @{ a = 1 } }
        foreach ($name in $Mandatory) { $params[$name] = 'x' }
        & "New-Site24x7$Noun" @params | Out-Null
        (Body (Get-MockRequest POST "/api/$Path")).a | Should -Be 1
    }
}

Describe 'Hand-written cmdlet: <Run>' -ForEach $handWritten {
    BeforeAll { Connect-Mock }
    BeforeEach { Clear-MockSite24x7 }

    It 'sends <Method> <Path>' {
        if ($Token) { Set-MockRoute POST /accounts/oauth/v2/token @{ access_token = 'a'; expires_in = 3600 } }
        & ([scriptblock]::Create($Run)) | Out-Null
        $req = @(Get-MockRequest $Method $Path)
        $req.Count | Should -Be 1 -Because "$Run should send one $Method $Path"
        if ($ApiVersion) { $req[0].Headers['Accept'] | Should -Be "application/json; version=$ApiVersion" }
        if ($Query) { $req[0].Query | Should -Match $Query }
        if ($Form) { $req[0].Body | Should -Match $Form }
        if ($Body) {
            $sent = Body $req[0]
            foreach ($key in $Body.Keys) {
                $sent.PSObject.Properties.Name | Should -Contain $key
                @($sent.$key) | Should -Be @($Body[$key]) -Because "field $key"
            }
        }
    }
}
