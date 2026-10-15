<#
.SYNOPSIS
    Generates src/Site24x7/Cmdlets/Generated/ResourceCmdlets.g.cs from the resource table below.

.DESCRIPTION
    Many Site24x7 API areas are plain collections: GET /path, GET /path/{id}, POST /path,
    PUT /path/{id} and DELETE /path/{id}. Rather than hand-writing four near-identical classes for
    each one, they are described here and generated. To add an area or a field, edit the table and
    run this script; the build does not run it for you, and the generated file is checked in.

    Fields:
      Noun        Cmdlet noun after "Site24x7", e.g. Tag -> Get-Site24x7Tag
      Path        Endpoints constant holding the API path
      Id          Id parameter name; IdAliases are the API field names that hold the id
      Label       Words used in confirmation prompts
      NameField   API field that -Name filters on
      Verbs       Which of Get, New, Set, Remove to generate
      Fields      Typed body parameters: Name, Json (API field), Type (string, string[], int, switch,
                  or an enum name), and NewMandatory to make it required on New-*
      Aliases     Extra cmdlet aliases, for compatibility with version 1 names
      Help        One-line synopsis noun phrase
#>
[CmdletBinding()]
param(
    [string] $OutFile = (Join-Path $PSScriptRoot '../src/Site24x7/Cmdlets/Generated/ResourceCmdlets.g.cs')
)

$resources = @(
    @{
        Noun = 'ThresholdProfile'; Path = 'ThresholdProfiles'; Id = 'ProfileId'; IdAliases = @('profile_id')
        Label = 'threshold profile'; NameField = 'profile_name'; Help = 'threshold profiles'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Aliases = @{ New = @('New-Site24x7ThresholdProfile_Incomplete'); Set = @('Update-Site24x7ThresholdProfile') }
        Fields = @(
            @{ Name = 'ProfileName'; Json = 'profile_name'; Type = 'string'; NewMandatory = $true }
            @{ Name = 'Type'; Json = 'type'; Type = 'string'; NewMandatory = $true; Doc = 'The monitor type the profile applies to, for example URL or SERVER.' }
            @{ Name = 'ProfileType'; Json = 'profile_type'; Type = 'int'; Doc = '1 = static thresholds, 2 = AI-based thresholds.' }
            @{ Name = 'DownLocationThreshold'; Json = 'down_location_threshold'; Type = 'int'; Doc = 'How many locations must report down before the monitor is down.' }
            @{ Name = 'WebsiteContentModified'; Json = 'website_content_modified'; Type = 'switch' }
        )
    }
    @{
        Noun = 'NotificationProfile'; Path = 'NotificationProfiles'; Id = 'ProfileId'; IdAliases = @('profile_id')
        Label = 'notification profile'; NameField = 'profile_name'; Help = 'notification profiles'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @(
            @{ Name = 'ProfileName'; Json = 'profile_name'; Type = 'string'; NewMandatory = $true }
            @{ Name = 'RcaNeeded'; Json = 'rca_needed'; Type = 'switch'; Doc = 'Attach a root cause analysis to down alerts.' }
            @{ Name = 'NotifyAfterExecutingActions'; Json = 'notify_after_executing_actions'; Type = 'switch' }
            @{ Name = 'SuppressAutomation'; Json = 'suppress_automation'; Type = 'switch' }
            @{ Name = 'DowntimeNotificationDelay'; Json = 'downtime_notification_delay'; Type = 'int'; Doc = 'Number of polls to wait before alerting on a down state.' }
            @{ Name = 'PersistentNotification'; Json = 'persistent_notification'; Type = 'int'; Doc = 'Repeat the alert every N polls while down.' }
            @{ Name = 'EscalationUserGroupId'; Json = 'escalation_user_group_id'; Type = 'string' }
            @{ Name = 'EscalationWaitTime'; Json = 'escalation_wait_time'; Type = 'int'; Doc = 'Minutes to wait before escalating.' }
            @{ Name = 'TemplateId'; Json = 'template_id'; Type = 'string'; Doc = 'Email template id (see Get-Site24x7EmailTemplate).' }
        )
    }
    @{
        Noun = 'LocationProfile'; Path = 'LocationProfiles'; Id = 'ProfileId'; IdAliases = @('profile_id')
        Label = 'location profile'; NameField = 'profile_name'; Help = 'location profiles'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @(
            @{ Name = 'ProfileName'; Json = 'profile_name'; Type = 'string'; NewMandatory = $true }
            @{ Name = 'PrimaryLocation'; Json = 'primary_location'; Type = 'string'; NewMandatory = $true; Doc = 'Location id, from Get-Site24x7LocationTemplate.' }
            @{ Name = 'SecondaryLocations'; Json = 'secondary_locations'; Type = 'string[]' }
            @{ Name = 'RestrictAlternateLocation'; Json = 'restrict_alt_loc'; Type = 'switch' }
        )
    }
    @{
        Noun = 'ResourceCheckProfile'; Path = 'ResourceCheckProfiles'; Id = 'ProfileId'; IdAliases = @('profile_id')
        Label = 'resource check profile'; NameField = 'profile_name'; Help = 'resource check profiles'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @( @{ Name = 'ProfileName'; Json = 'profile_name'; Type = 'string'; NewMandatory = $true } )
    }
    @{
        Noun = 'CredentialProfile'; Path = 'CredentialProfiles'; Id = 'CredentialProfileId'; IdAliases = @('credential_profile_id', 'profile_id')
        Label = 'credential profile'; NameField = 'credential_name'; Help = 'credential profiles'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @(
            @{ Name = 'CredentialName'; Json = 'credential_name'; Type = 'string'; NewMandatory = $true }
            @{ Name = 'CredentialType'; Json = 'credential_type'; Type = 'int' }
            @{ Name = 'UserName'; Json = 'username'; Type = 'string' }
            @{ Name = 'Password'; Json = 'password'; Type = 'string' }
        )
    }
    @{
        Noun = 'Tag'; Path = 'Tags'; Id = 'TagId'; IdAliases = @('tag_id')
        Label = 'tag'; NameField = 'tag_name'; Help = 'tags'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @(
            @{ Name = 'TagName'; Json = 'tag_name'; Type = 'string'; NewMandatory = $true }
            @{ Name = 'TagValue'; Json = 'tag_value'; Type = 'string' }
            @{ Name = 'TagColor'; Json = 'tag_color'; Type = 'string'; Doc = 'A hex color such as #B7DA9E.' }
        )
    }
    @{
        Noun = 'OnCallSchedule'; Path = 'OnCallSchedules'; Id = 'OnCallScheduleId'; IdAliases = @('on_call_schedule_id', 'schedule_id')
        Label = 'on-call schedule'; NameField = 'schedule_name'; Help = 'on-call schedules'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @( @{ Name = 'ScheduleName'; Json = 'schedule_name'; Type = 'string' } )
    }
    @{
        Noun = 'AttributeAlertGroup'; Path = 'AttributeGroups'; Id = 'AttributeGroupId'; IdAliases = @('attribute_group_id')
        Label = 'attribute alert group'; NameField = 'display_name'; Help = 'attribute alert groups'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @( @{ Name = 'DisplayName'; Json = 'display_name'; Type = 'string' } )
    }
    @{
        Noun = 'EmailTemplate'; Path = 'EmailTemplates'; Id = 'TemplateId'; IdAliases = @('template_id')
        Label = 'email template'; NameField = 'display_name'; Help = 'email templates'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @( @{ Name = 'DisplayName'; Json = 'display_name'; Type = 'string' } )
    }
    @{
        Noun = 'BusinessHours'; Path = 'BusinessHours'; Id = 'BusinessHoursId'; IdAliases = @('business_hours_id', 'business_hour_id')
        Label = 'business hours'; NameField = 'display_name'; Help = 'business hours definitions'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @( @{ Name = 'DisplayName'; Json = 'display_name'; Type = 'string' } )
    }
    @{
        Noun = 'OAuthProvider'; Path = 'OAuthProviders'; Id = 'ProviderId'; IdAliases = @('provider_id')
        Label = 'OAuth provider'; NameField = 'name'; Help = 'OAuth providers used by REST API monitors'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @( @{ Name = 'ProviderName'; Json = 'name'; Type = 'string' } )
    }
    @{
        Noun = 'WebToken'; Path = 'WebTokens'; Id = 'WebTokenId'; IdAliases = @('web_token_id', 'token_id')
        Label = 'web token'; NameField = 'name'; Help = 'web tokens'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @()
    }
    @{
        Noun = 'GlobalParameter'; Path = 'GlobalParameters'; Id = 'ParameterId'; IdAliases = @('param_id', 'parameter_id')
        Label = 'global parameter'; NameField = 'name'; Help = 'global parameters'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @(
            @{ Name = 'ParameterName'; Json = 'name'; Type = 'string' }
            @{ Name = 'Value'; Json = 'value'; Type = 'string' }
        )
    }
    @{
        Noun = 'ITAutomation'; Path = 'ITAutomation'; Id = 'ActionId'; IdAliases = @('action_id')
        Label = 'IT automation'; NameField = 'action_name'; Help = 'IT automation actions'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @(
            @{ Name = 'ActionName'; Json = 'action_name'; Type = 'string'; NewMandatory = $true }
            @{ Name = 'ActionType'; Json = 'action_type'; Type = 'int'; Doc = 'The automation type constant, for example 1 for URL action.' }
            @{ Name = 'ActionUrl'; Json = 'action_url'; Type = 'string' }
            @{ Name = 'ActionMethod'; Json = 'action_method'; Type = 'string'; Doc = 'P = POST, G = GET.' }
            @{ Name = 'ActionTimeout'; Json = 'action_timeout'; Type = 'int' }
        )
    }
    @{
        Noun = 'ConfigurationRule'; Path = 'ConfigurationRules'; Id = 'RuleId'; IdAliases = @('rule_id')
        Label = 'configuration rule'; NameField = 'rule_name'; Help = 'configuration rules'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @()
    }
    @{
        Noun = 'ScheduledReport'; Path = 'ScheduledReports'; Id = 'ReportId'; IdAliases = @('report_id')
        Label = 'scheduled report'; NameField = 'display_name'; Help = 'scheduled reports'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @( @{ Name = 'DisplayName'; Json = 'display_name'; Type = 'string' } )
    }
    @{
        Noun = 'SlaSetting'; Path = 'SlaSettings'; Id = 'SlaId'; IdAliases = @('sla_id')
        Label = 'SLA setting'; NameField = 'display_name'; Help = 'SLA settings'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @( @{ Name = 'DisplayName'; Json = 'display_name'; Type = 'string' } )
    }
    @{
        Noun = 'StatusPage'; Path = 'StatusPages'; Id = 'StatusPageId'; IdAliases = @('statuspage_id', 'status_page_id')
        Label = 'status page'; NameField = 'display_name'; Help = 'status pages'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @( @{ Name = 'DisplayName'; Json = 'display_name'; Type = 'string' } )
    }
    @{
        Noun = 'ThirdPartyIntegration'; Path = 'ThirdPartyServices'; Id = 'ServiceId'; IdAliases = @('service_id', 'third_party_service_id')
        Label = 'third-party integration'; NameField = 'name'; Help = 'third-party integrations (Slack, PagerDuty, webhooks and others)'
        Verbs = 'Get', 'Remove'
        Fields = @()
    }
    @{
        Noun = 'OnPremisePoller'; Path = 'OnPremisePollers'; Id = 'PollerId'; IdAliases = @('poller_id')
        Label = 'on-premise poller'; NameField = 'display_name'; Help = 'on-premise pollers'
        Verbs = 'Get'
        Fields = @()
    }
    @{
        Noun = 'AuditLog'; Path = 'AuditLogs'; Label = 'audit log'; NameField = 'display_name'
        Help = 'audit log entries (pass date filters with -Query)'
        Verbs = 'Get'; ListOnly = $true; Fields = @()
    }
    @{
        Noun = 'ITAutomationLog'; Path = 'ITAutomationLogs'; Label = 'IT automation log'; NameField = 'action_name'
        Help = 'IT automation run logs (pass date filters with -Query)'
        Verbs = 'Get'; ListOnly = $true; Fields = @()
    }
    @{
        Noun = 'AlertLog'; Path = 'AlertLogs'; Label = 'alert log'; NameField = 'display_name'
        Help = 'alert logs (pass date filters with -Query)'
        Verbs = 'Get'; ListOnly = $true; Fields = @()
    }
    @{
        Noun = 'Alarm'; Path = 'Alarms'; Label = 'alarm'; NameField = 'display_name'
        Help = 'open alarms (monitors that are down, in trouble or critical)'
        Verbs = 'Get'; ListOnly = $true; Fields = @()
    }
    @{
        Noun = 'LocationTemplate'; Path = 'LocationTemplate'; Label = 'location template'; NameField = 'display_name'
        Help = 'the monitoring locations you can use in location profiles'
        Verbs = 'Get'; ListOnly = $true; Fields = @()
    }
    @{
        Noun = 'MspCustomer'; Path = 'MspCustomers'; Label = 'MSP customer'; NameField = 'name'
        Help = 'MSP customers; use a customer''s zaaid with Set-Site24x7Context'
        Verbs = 'Get'; ListOnly = $true; Fields = @()
    }
    @{
        Noun = 'BusinessUnit'; Path = 'BusinessUnits'; Label = 'business unit'; NameField = 'name'
        Help = 'Business Units; use a unit''s zaaid with Set-Site24x7Context'
        Verbs = 'Get'; ListOnly = $true; Fields = @()
    }
)

function Get-CSharpType([string] $type) {
    switch ($type) {
        'switch' { 'SwitchParameter' }
        default { $type }
    }
}

function Write-Fields($sb, $fields, [string] $indent) {
    foreach ($f in $fields) {
        if ($f.Doc) { [void]$sb.AppendLine("$indent/// <summary>$($f.Doc)</summary>") }
        [void]$sb.AppendLine("$indent[Parameter]")
        if ($f.Json -ne $f.Name) { [void]$sb.AppendLine("$indent[Alias(`"$($f.Json)`")]") }
        $virtual = if ($f.NewMandatory) { 'virtual ' } else { '' }
        [void]$sb.AppendLine("${indent}public $virtual$(Get-CSharpType $f.Type) $($f.Name) { get; set; }")
        [void]$sb.AppendLine()
    }
}

function Write-AliasAttribute($sb, $r, [string] $verb) {
    if ($r.Aliases -and $r.Aliases[$verb]) {
        $list = ($r.Aliases[$verb] | ForEach-Object { "`"$_`"" }) -join ', '
        [void]$sb.AppendLine("    [Alias($list)]")
    }
}

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('// <auto-generated>')
[void]$sb.AppendLine('// Generated by tools/Build-ResourceCmdlets.ps1. Edit the table in that script, not this file.')
[void]$sb.AppendLine('// </auto-generated>')
[void]$sb.AppendLine('using System.Collections;')
[void]$sb.AppendLine('using System.Management.Automation;')
[void]$sb.AppendLine()
[void]$sb.AppendLine('namespace Site24x7.Cmdlets')
[void]$sb.AppendLine('{')

foreach ($r in $resources) {
    $noun = $r.Noun
    $idAliases = (@($r.IdAliases) + 'Id' | ForEach-Object { "`"$_`"" }) -join ', '
    $idParam = @"
        [Parameter(Mandatory = true, Position = 0, __PIPE__ValueFromPipelineByPropertyName = true)]
        [Alias($idAliases)]
        [ValidateNotNullOrEmpty]
        public string $($r.Id) { get; set; }
"@
    $common = @"
        protected override string ResourcePath => Endpoints.$($r.Path);
        protected override string ResourceLabel => "$($r.Label)";
"@

    [void]$sb.AppendLine("    #region $noun")
    [void]$sb.AppendLine()

    if ($r.Verbs -contains 'Get') {
        [void]$sb.AppendLine('    /// <summary>')
        [void]$sb.AppendLine("    /// <para type=`"synopsis`">Gets $($r.Help).</para>")
        [void]$sb.AppendLine('    /// </summary>')
        if ($r.ListOnly) {
            [void]$sb.AppendLine("    [Cmdlet(VerbsCommon.Get, `"Site24x7$noun`", DefaultParameterSetName = ParameterSets.List)]")
            [void]$sb.AppendLine('    [OutputType(typeof(PSObject))]')
            [void]$sb.AppendLine("    public sealed class GetSite24x7${noun}Command : GetResourceCmdlet")
            [void]$sb.AppendLine('    {')
            [void]$sb.AppendLine($common)
            [void]$sb.AppendLine("        protected override string NameProperty => `"$($r.NameField)`";")
        } else {
            [void]$sb.AppendLine("    [Cmdlet(VerbsCommon.Get, `"Site24x7$noun`", DefaultParameterSetName = ParameterSets.List)]")
            Write-AliasAttribute $sb $r 'Get'
            [void]$sb.AppendLine('    [OutputType(typeof(PSObject))]')
            [void]$sb.AppendLine("    public sealed class GetSite24x7${noun}Command : GetResourceCmdlet")
            [void]$sb.AppendLine('    {')
            [void]$sb.AppendLine((($idParam -replace 'Mandatory = true, Position = 0, ', 'Mandatory = true, Position = 0, ParameterSetName = ParameterSets.ById, ') -replace '__PIPE__', 'ValueFromPipeline = true, '))
            [void]$sb.AppendLine()
            [void]$sb.AppendLine($common)
            [void]$sb.AppendLine("        protected override string ResourceId => $($r.Id);")
            [void]$sb.AppendLine("        protected override string NameProperty => `"$($r.NameField)`";")
        }
        [void]$sb.AppendLine('    }')
        [void]$sb.AppendLine()
    }

    $hasWrite = ($r.Verbs -contains 'New') -or ($r.Verbs -contains 'Set')
    if ($hasWrite) {
        [void]$sb.AppendLine("    /// <summary>Settings shared by New-Site24x7$noun and Set-Site24x7$noun.</summary>")
        [void]$sb.AppendLine("    public abstract class Site24x7${noun}WriteCmdlet : WriteResourceCmdlet")
        [void]$sb.AppendLine('    {')
        Write-Fields $sb $r.Fields '        '
        [void]$sb.AppendLine($common)
        [void]$sb.AppendLine()
        [void]$sb.AppendLine('        protected override void BuildBody(IDictionary body)')
        [void]$sb.AppendLine('        {')
        foreach ($f in $r.Fields) {
            [void]$sb.AppendLine("            AddIfBound(body, `"$($f.Json)`", nameof($($f.Name)), $($f.Name));")
        }
        [void]$sb.AppendLine('        }')
        [void]$sb.AppendLine('    }')
        [void]$sb.AppendLine()
    }

    if ($r.Verbs -contains 'New') {
        $mandatory = @($r.Fields | Where-Object { $_.NewMandatory })
        [void]$sb.AppendLine('    /// <summary>')
        [void]$sb.AppendLine("    /// <para type=`"synopsis`">Creates a $($r.Label). Fields without a parameter can be passed with -Property.</para>")
        [void]$sb.AppendLine('    /// </summary>')
        [void]$sb.AppendLine("    [Cmdlet(VerbsCommon.New, `"Site24x7$noun`", SupportsShouldProcess = true)]")
        Write-AliasAttribute $sb $r 'New'
        [void]$sb.AppendLine('    [OutputType(typeof(PSObject))]')
        [void]$sb.AppendLine("    public sealed class NewSite24x7${noun}Command : Site24x7${noun}WriteCmdlet")
        [void]$sb.AppendLine('    {')
        $pos = 0
        foreach ($f in $mandatory) {
            [void]$sb.AppendLine("        [Parameter(Mandatory = true, Position = $pos)]")
            if ($f.Json -ne $f.Name) { [void]$sb.AppendLine("        [Alias(`"$($f.Json)`")]") }
            [void]$sb.AppendLine('        [ValidateNotNullOrEmpty]')
            [void]$sb.AppendLine("        public override $(Get-CSharpType $f.Type) $($f.Name) { get; set; }")
            [void]$sb.AppendLine()
            $pos++
        }
        [void]$sb.AppendLine('        protected override void ProcessRecordCore()')
        [void]$sb.AppendLine('        {')
        [void]$sb.AppendLine('            var body = CreateBody();')
        [void]$sb.AppendLine("            if (ShouldProcess(Describe(body, ResourceLabel), `"Create $($r.Label)`"))")
        [void]$sb.AppendLine('            {')
        [void]$sb.AppendLine('                WriteData(Call("POST", ResourcePath, body, apiVersion: ApiVersion));')
        [void]$sb.AppendLine('            }')
        [void]$sb.AppendLine('        }')
        [void]$sb.AppendLine('    }')
        [void]$sb.AppendLine()
    }

    if ($r.Verbs -contains 'Set') {
        [void]$sb.AppendLine('    /// <summary>')
        [void]$sb.AppendLine("    /// <para type=`"synopsis`">Updates a $($r.Label). Only the settings you pass are sent.</para>")
        [void]$sb.AppendLine('    /// </summary>')
        [void]$sb.AppendLine("    [Cmdlet(VerbsCommon.Set, `"Site24x7$noun`", SupportsShouldProcess = true)]")
        Write-AliasAttribute $sb $r 'Set'
        [void]$sb.AppendLine('    [OutputType(typeof(PSObject))]')
        [void]$sb.AppendLine("    public sealed class SetSite24x7${noun}Command : Site24x7${noun}WriteCmdlet")
        [void]$sb.AppendLine('    {')
        [void]$sb.AppendLine(($idParam -replace '__PIPE__', ''))
        [void]$sb.AppendLine()
        [void]$sb.AppendLine("        protected override string ResourceId => $($r.Id);")
        [void]$sb.AppendLine()
        [void]$sb.AppendLine('        protected override void ProcessRecordCore()')
        [void]$sb.AppendLine('        {')
        [void]$sb.AppendLine('            var body = CreateBody();')
        [void]$sb.AppendLine("            if (body.Count == 0) throw new Site24x7Exception(`$`"Nothing to update on $($r.Label) {$($r.Id)}. Pass at least one setting or -Property.`");")
        [void]$sb.AppendLine("            if (ShouldProcess($($r.Id), `"Update $($r.Label)`"))")
        [void]$sb.AppendLine('            {')
        [void]$sb.AppendLine('                WriteData(Call("PUT", ItemPath, body, apiVersion: ApiVersion));')
        [void]$sb.AppendLine('            }')
        [void]$sb.AppendLine('        }')
        [void]$sb.AppendLine('    }')
        [void]$sb.AppendLine()
    }

    if ($r.Verbs -contains 'Remove') {
        [void]$sb.AppendLine('    /// <summary>')
        [void]$sb.AppendLine("    /// <para type=`"synopsis`">Deletes a $($r.Label).</para>")
        [void]$sb.AppendLine('    /// </summary>')
        [void]$sb.AppendLine("    [Cmdlet(VerbsCommon.Remove, `"Site24x7$noun`", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]")
        Write-AliasAttribute $sb $r 'Remove'
        [void]$sb.AppendLine("    public sealed class RemoveSite24x7${noun}Command : RemoveResourceCmdlet")
        [void]$sb.AppendLine('    {')
        [void]$sb.AppendLine(($idParam -replace '__PIPE__', 'ValueFromPipeline = true, '))
        [void]$sb.AppendLine()
        [void]$sb.AppendLine($common)
        [void]$sb.AppendLine("        protected override string ResourceId => $($r.Id);")
        [void]$sb.AppendLine('    }')
        [void]$sb.AppendLine()
    }

    [void]$sb.AppendLine("    #endregion")
    [void]$sb.AppendLine()
}

[void]$sb.AppendLine('}')

$dir = Split-Path $OutFile -Parent
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
$text = $sb.ToString() -replace "`r`n", "`n"
[System.IO.File]::WriteAllText($OutFile, $text, [System.Text.UTF8Encoding]::new($false))
Write-Verbose "Wrote $OutFile"
