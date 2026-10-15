#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0' }

BeforeAll {
    $root = Split-Path $PSScriptRoot -Parent
    $manifest = Join-Path $root 'out/Site24x7/Site24x7.psd1'
    if (-not (Test-Path $manifest)) { throw "Build the module first: ./build.ps1" }
    Import-Module $manifest -Force
    Import-Module (Join-Path $PSScriptRoot 'MockSite24x7.psm1') -Force

    $mock = Start-MockSite24x7
    $api = "$($mock.BaseUri)api/"
    $accounts = "$($mock.BaseUri)accounts/"

    function Connect-Mock {
        Set-MockRoute POST /accounts/oauth/v2/token @{ access_token = 'at-1'; expires_in = 3600; api_domain = 'https://www.zohoapis.com'; token_type = 'Bearer' }
        Connect-Site24x7 -ClientId cid -ClientSecret 'sec&ret' -RefreshToken rt -ApiBaseUri $api -AccountsUri $accounts
    }

    function Body($request) { $request.Body | ConvertFrom-Json }
    function Ok($data) { @{ code = 0; message = 'success'; data = $data } }
}

AfterAll {
    Disconnect-Site24x7
    Stop-MockSite24x7
}

Describe 'Module' {
    It 'exports cmdlets for every API area' {
        (Get-Command -Module Site24x7 -CommandType Cmdlet).Count | Should -BeGreaterThan 120
    }

    It 'keeps the version 1 command names working' {
        foreach ($name in 'Initialize-ZohoToken', 'Request-ZohoAccessToken', 'Revoke-ZohoRefreshToken',
            'Get-Site24x7Monitor', 'Remove-Site24x7Monitor', 'Enable-Site24x7Monitor', 'Disable-Site24x7Monitor',
            'New-Site24x7Group', 'Get-Site24x7Group', 'Update-Site24x7Group', 'Remove-Site24x7Group', 'Update-Site24x7Group_AddMonitor',
            'New-Site24x7Subgroup', 'Get-Site24x7Subgroup', 'Update-Site24x7Subgroup', 'Remove-Site24x7Subgroup',
            'New-Site24x7User', 'Get-Site24x7User', 'Update-Site24x7User', 'Remove-Site24x7User',
            'New-Site24x7Usergroup', 'Get-Site24x7Usergroup', 'Update-Site24x7Usergroup', 'Remove-Site24x7Usergroup',
            'New-Site24x7ThresholdProfile_Incomplete', 'Get-Site24x7ThresholdProfile',
            'New-Site24x7Maintenance', 'Get-Site24x7Maintenance', 'Update-Site24x7Maintenance', 'Remove-Site24x7Maintenance',
            'Start-Site24x7Maintenance', 'Stop-Site24x7Maintenance') {
            Get-Command $name -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty -Because "$name existed in version 1"
        }
    }

    It 'keeps the version 1 parameter names as aliases' {
        (Get-Command Get-Site24x7Monitor).Parameters['MonitorId'].Aliases | Should -Contain 'monitor_id'
        (Get-Command Get-Site24x7Monitor).Parameters['AccessToken'].Aliases | Should -Contain 'access_token'
        (Get-Command Initialize-ZohoToken).Parameters['GrantCode'].Aliases | Should -Contain 'oneTimeCode'
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
        Set-MockRoute GET /api/monitors -Status 401 -Once -Body @{ code = 1100; message = 'Invalid token' }
        Set-MockRoute GET /api/monitors (Ok @(@{ monitor_id = '1'; display_name = 'a' }))
        Set-MockRoute POST /accounts/oauth/v2/token @{ access_token = 'at-2'; expires_in = 3600 }
        $result = Get-Site24x7Monitor
        $result.monitor_id | Should -Be '1'
        (Get-MockRequest GET /api/monitors | Select-Object -Last 1).Headers['Authorization'] | Should -Be 'Zoho-oauthtoken at-2'
    }

    It 'reports a Zoho error returned with HTTP 200' {
        Set-MockRoute POST /accounts/oauth/v2/token @{ error = 'invalid_code' }
        { Connect-Site24x7 -ClientId c -ClientSecret s -RefreshToken bad -ApiBaseUri $api -AccountsUri $accounts } |
            Should -Throw '*invalid_code*'
    }

    It 'returns the version 1 token shape from Initialize-ZohoToken' {
        Set-MockRoute POST /accounts/oauth/v2/token @{ access_token = 'a'; refresh_token = 'r'; expires_in = 3600 }
        $t = Initialize-ZohoToken -client_id c -client_secret s -oneTimeCode code -AccountsUri $accounts
        $t.access_token | Should -Be 'a'
        $t.refresh_token | Should -Be 'r'
        $t.access_token_expires | Should -BeOfType [datetime]
        (Get-MockRequest POST /accounts/oauth/v2/token | Select-Object -Last 1).Body | Should -Match 'grant_type=authorization_code'
    }

    It 'uses the right URLs for each data center' {
        Set-MockRoute POST /accounts/oauth/v2/token @{ access_token = 'x'; expires_in = 3600 }
        $s = Connect-Site24x7 -AccessToken abc -DataCenter EU -PassThru
        $s.ApiBaseUri | Should -Be 'https://www.site24x7.eu/api/'
        $s.AccountsUri | Should -Be 'https://accounts.zoho.eu/'
        $s = Connect-Site24x7 -AccessToken abc -DataCenter CA -PassThru
        $s.ApiBaseUri | Should -Be 'https://www.site24x7.ca/api/'
        $s.AccountsUri | Should -Be 'https://accounts.zohocloud.ca/'
    }

    It 'fails clearly when not connected' {
        Disconnect-Site24x7
        { Get-Site24x7Monitor -ErrorAction Stop } | Should -Throw '*Connect-Site24x7*'
    }

    It 'accepts -access_token per call, as version 1 did' {
        Disconnect-Site24x7
        Connect-Site24x7 -AccessToken unused -ApiBaseUri $api
        Set-MockRoute GET /api/monitors/9 (Ok @{ monitor_id = '9' })
        Get-Site24x7Monitor -monitor_id 9 -access_token tok | Out-Null
        (Get-MockRequest GET /api/monitors/9).Headers['Authorization'] | Should -Be 'Zoho-oauthtoken tok'
    }

    It 'sends the zaaid cookie after Set-Site24x7Context' {
        Connect-Mock
        Set-Site24x7Context -CustomerId 12345
        Get-Site24x7Tag | Out-Null
        (Get-MockRequest GET /api/tags).Headers['Cookie'] | Should -Be 'zaaid=12345'
        Set-Site24x7Context -Reset
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

    It 'filters by type' {
        (Get-Site24x7Monitor -Type RESTAPI).monitor_id | Should -Be '2'
    }

    It 'gets one monitor by id, including from the pipeline' {
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

    It 'keeps going through the pipeline after one failure' {
        Set-MockRoute GET /api/monitors/1 (Ok @{ monitor_id = '1' })
        Set-MockRoute GET /api/monitors/2 -Status 404 -Body @{ code = 1001; message = 'nope' }
        Set-MockRoute GET /api/monitors/3 (Ok @{ monitor_id = '3' })
        $ok = '1', '2', '3' | Get-Site24x7Monitor -ErrorAction SilentlyContinue
        $ok.monitor_id | Should -Be @('1', '3')
    }

    It 'uses API version 2.1 for monitor groups' {
        Set-MockRoute GET /api/monitor_groups (Ok @())
        Get-Site24x7Group | Out-Null
        (Get-MockRequest GET /api/monitor_groups).Headers['Accept'] | Should -Be 'application/json; version=2.1'
    }

    It 'builds current status paths and flags' {
        Get-Site24x7CurrentStatus -GroupId 77 -IncludeSuspended | Out-Null
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

    It 'escapes ids in the URL path' {
        # A space rather than a slash: .NET Framework un-escapes %2F in http paths, so a slash
        # in an id cannot survive on Windows PowerShell 5.1. Site24x7 ids are numeric anyway.
        Get-Site24x7Tag -TagId 'a b' | Out-Null
        (Get-MockRequest GET).Path | Should -Match '^/api/tags/a(%20| )b$'
    }
}

Describe 'Writing' {
    BeforeAll { Connect-Mock }
    BeforeEach { Clear-MockSite24x7 }

    It 'creates a website monitor with sensible defaults' {
        New-Site24x7WebsiteMonitor 'Home' 'https://example.com' -LocationProfileId L -UserGroupIds U1, U2 -CustomHeaders @{ 'X-Test' = '1' } -Keyword 'Welcome' | Out-Null
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
        $b.PSObject.Properties.Name | Should -Not -Contain 'notification_profile_id'
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
        Set-Site24x7Group -GroupId 9 -Description '' -HealthThresholdCount 0 -SuppressAlert:$false -Confirm:$false | Out-Null
        $req = Get-MockRequest PUT /api/monitor_groups/9
        $b = Body $req
        @($b.PSObject.Properties.Name | Sort-Object) | Should -Be @('description', 'health_threshold_count', 'suppress_alert')
        $b.health_threshold_count | Should -Be 0
        $b.suppress_alert | Should -BeFalse
    }

    It 'refuses an update with nothing to change' {
        { Set-Site24x7Tag -TagId 1 -ErrorAction Stop } | Should -Throw '*Nothing to update*'
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
        New-Site24x7User -DisplayName Jo -EmailAddress jo@example.com -UserRole SuperAdministrator -NotifyMedium Email, SMS -AlertDown Email -AlertDayExclusion Sunday, Saturday -AlertStartTime 08:00 -AlertEndTime 18:00 -JobTitle DevOpsEngineer | Out-Null
        $b = Body (Get-MockRequest POST /api/users)
        $b.user_role | Should -Be 1
        $b.notify_medium | Should -Be @(1, 2)
        $b.job_title | Should -Be 3
        $b.alert_settings.down | Should -Be @(1)
        $b.alert_settings.dont_alert_on_days | Should -Be @(0, 6)
        $b.alert_settings.alerting_period.start_time | Should -Be '08:00'
        $b.PSObject.Properties.Name | Should -Not -Contain 'mobile_settings'
    }

    It 'updates the user given by id (version 1 had no id parameter)' {
        Set-Site24x7User -UserId 42 -UserRole Operator -Confirm:$false | Out-Null
        (Body (Get-MockRequest PUT /api/users/42)).user_role | Should -Be 3
    }

    It 'checks maintenance settings for the maintenance type' {
        { New-Site24x7Maintenance 'm' MonthlyByDay -StartTime 01:00 -EndTime 02:00 -Monitors 1 -ErrorAction Stop } |
            Should -Throw '*-StartWeek, -StartDay, -Duration*'
    }

    It 'creates a one-time maintenance' {
        New-Site24x7Maintenance -display_name 'Patch' -maintenance_type Once -start_date 2026-11-01 -end_date 2026-11-02 -start_time 22:00 -end_time 23:30 -monitor_groups G1 -SubgroupMonitors | Out-Null
        $b = Body (Get-MockRequest POST /api/maintenance)
        $b.maintenance_type | Should -Be 3
        $b.start_date | Should -Be '2026-11-01'
        $b.selection_type | Should -Be 1
        $b.monitor_groups | Should -Be @('G1')
        $b.subgroup_monitors | Should -BeTrue
    }

    It 'starts maintenance by name on the names endpoint' {
        Start-Site24x7Maintenance -MonitorNames web01, web02 -Duration 30 | Out-Null
        $b = Body (Get-MockRequest POST /api/start_maintenance/names)
        $b.selection_type | Should -Be 2
        $b.monitor_names | Should -Be @('web01', 'web02')
        $b.duration | Should -Be 30
    }

    It 'ends a maintenance window by id' {
        Stop-Site24x7Maintenance -MaintenanceId 77 | Out-Null
        (Body (Get-MockRequest POST /api/end_maintenance)).maintenance_id | Should -Be '77'
    }

    It 'deletes from the pipeline after confirmation is waived' {
        [pscustomobject]@{ monitor_id = '8' }, [pscustomobject]@{ monitor_id = '9' } | Remove-Site24x7Monitor -Confirm:$false
        (Get-MockRequest DELETE).Path | Should -Be @('/api/monitors/8', '/api/monitors/9')
    }

    It 'suspends and activates monitors' {
        Disable-Site24x7Monitor 3 | Out-Null
        Enable-Site24x7Monitor 3 | Out-Null
        (Get-MockRequest PUT).Path | Should -Be @('/api/monitors/suspend/3', '/api/monitors/activate/3')
    }

    It 'adds monitors to a group by name with the version 1 command' {
        Update-Site24x7Group_AddMonitor -group_name Web -monitor_names a, b | Out-Null
        $b = Body (Get-MockRequest PUT /api/monitor_groups/add_monitors)
        $b.group_name | Should -Be 'Web'
        $b.monitor_names | Should -Be @('a', 'b')
    }

    It 'calls any endpoint through Invoke-Site24x7Api' {
        Set-MockRoute POST /api/custom/thing (Ok @{ id = 1 })
        $r = Invoke-Site24x7Api POST /api/custom/thing -Body @{ x = 'y' } -Confirm:$false
        $r.id | Should -Be 1
        (Body (Get-MockRequest POST /api/custom/thing)).x | Should -Be 'y'
    }

    It 'round-trips awkward text through JSON' {
        # Non-ASCII characters are built from code points so the file stays ASCII for Windows PowerShell 5.1.
        $text = "quote `" backslash \ tab `t newline `n unicode " + [char]0x00E9 + [char]0x2713
        New-Site24x7Tag -TagName $text | Out-Null
        (Body (Get-MockRequest POST /api/tags)).tag_name | Should -Be $text
    }
}
