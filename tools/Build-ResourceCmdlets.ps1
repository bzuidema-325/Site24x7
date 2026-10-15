<#
.SYNOPSIS
    Generates src/Site24x7/Cmdlets/Generated/ResourceCmdlets.g.cs from the resource table below.

.DESCRIPTION
    Many Site24x7 API areas are plain collections: GET /path, GET /path/{id}, POST /path,
    PUT /path/{id} and DELETE /path/{id}. Rather than hand-writing four near-identical classes for
    each one, they are described here and generated. The behaviour lives in the base classes in
    src/Site24x7/Cmdlets/ResourceCmdlets.cs; generated classes only declare parameters. To add an
    area or a field, edit the table and run this script; the build does not run it for you, and
    the generated file is checked in (CI fails if it is out of date).

    Fields:
      Noun        Cmdlet noun after "Site24x7", e.g. Tag -> Get-Site24x7Tag
      Path        Endpoints constant holding the API path
      Id          Id parameter name; IdField is the API field holding the id, used as an alias so
                  API objects can be piped in by property name
      Label       Words used in confirmation prompts and help
      NameField   API field that -Name filters on
      ApiVersion  Accept header version when the area needs one other than 2.0
      Verbs       Which of Get, New, Set, Remove to generate
      ListOnly    Get has no id parameter (the API only lists)
      Fields      Typed body parameters: Name, Json (API field), Type (string, string[], int, switch,
                  credential, or an enum name), Doc, and NewMandatory to make it required on New-*.
                  A credential field sends the user name to Json and the password to PasswordJson.
      Help        Noun phrase for the Get-* synopsis

    -TableOnly returns the table instead of writing the file; the tests use it.
#>
[CmdletBinding()]
param(
    [string] $OutFile = (Join-Path $PSScriptRoot '../src/Site24x7/Cmdlets/Generated/ResourceCmdlets.g.cs'),

    # Returns the resource table instead of writing the file. The tests use it to check every generated area.
    [switch] $TableOnly
)

$resources = @(
    @{
        Noun = 'ThresholdProfile'; Path = 'ThresholdProfiles'; Id = 'ProfileId'; IdField = 'profile_id'
        Label = 'threshold profile'; NameField = 'profile_name'; Help = 'threshold profiles'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        ApiVersion = '2.1'
        Fields = @(
            @{ Name = 'ProfileName'; Json = 'profile_name'; Type = 'string'; NewMandatory = $true }
            @{ Name = 'Type'; Json = 'type'; Type = 'string'; NewMandatory = $true; Doc = 'The monitor type the profile applies to, for example URL or SERVER.' }
            @{ Name = 'ProfileType'; Json = 'profile_type'; Type = 'int'; Doc = '1 = static thresholds, 2 = AI-based thresholds.' }
            @{ Name = 'DownLocationThreshold'; Json = 'down_location_threshold'; Type = 'int'; Doc = 'How many locations must report down before the monitor is down.' }
            @{ Name = 'WebsiteContentModified'; Json = 'website_content_modified'; Type = 'switch'; Doc = 'Alert when the website content changes.' }
        )
    }
    @{
        Noun = 'NotificationProfile'; Path = 'NotificationProfiles'; Id = 'ProfileId'; IdField = 'profile_id'
        Label = 'notification profile'; NameField = 'profile_name'; Help = 'notification profiles'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @(
            @{ Name = 'ProfileName'; Json = 'profile_name'; Type = 'string'; NewMandatory = $true }
            @{ Name = 'RcaNeeded'; Json = 'rca_needed'; Type = 'switch'; Doc = 'Attach a root cause analysis to down alerts.' }
            @{ Name = 'NotifyAfterExecutingActions'; Json = 'notify_after_executing_actions'; Type = 'switch'; Doc = 'Alert only after IT automation actions have run.' }
            @{ Name = 'SuppressAutomation'; Json = 'suppress_automation'; Type = 'switch'; Doc = 'Do not run IT automation actions for dependent monitors.' }
            @{ Name = 'DowntimeNotificationDelay'; Json = 'downtime_notification_delay'; Type = 'int'; Doc = 'Number of polls to wait before alerting on a down state.' }
            @{ Name = 'PersistentNotification'; Json = 'persistent_notification'; Type = 'int'; Doc = 'Repeat the alert every N polls while down.' }
            @{ Name = 'EscalationUserGroupId'; Json = 'escalation_user_group_id'; Type = 'string'; Doc = 'The user group alerted when a down alert is escalated.' }
            @{ Name = 'EscalationWaitTime'; Json = 'escalation_wait_time'; Type = 'int'; Doc = 'Minutes to wait before escalating.' }
            @{ Name = 'TemplateId'; Json = 'template_id'; Type = 'string'; Doc = 'Email template id (see Get-Site24x7EmailTemplate).' }
        )
    }
    @{
        Noun = 'LocationProfile'; Path = 'LocationProfiles'; Id = 'ProfileId'; IdField = 'profile_id'
        Label = 'location profile'; NameField = 'profile_name'; Help = 'location profiles'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @(
            @{ Name = 'ProfileName'; Json = 'profile_name'; Type = 'string'; NewMandatory = $true }
            @{ Name = 'PrimaryLocation'; Json = 'primary_location'; Type = 'string'; NewMandatory = $true; Doc = 'Location id, from Get-Site24x7LocationTemplate.' }
            @{ Name = 'SecondaryLocation'; Json = 'secondary_locations'; Type = 'string[]'; Doc = 'Location ids to fall back to.' }
            @{ Name = 'RestrictAlternateLocation'; Json = 'restrict_alt_loc'; Type = 'switch'; Doc = 'Never check from locations outside the profile.' }
        )
    }
    @{
        Noun = 'ResourceCheckProfile'; Path = 'ResourceCheckProfiles'; Id = 'ProfileId'; IdField = 'profile_id'
        Label = 'resource check profile'; NameField = 'profile_name'; Help = 'resource check profiles'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @( @{ Name = 'ProfileName'; Json = 'profile_name'; Type = 'string'; NewMandatory = $true } )
    }
    @{
        Noun = 'CredentialProfile'; Path = 'CredentialProfiles'; Id = 'CredentialProfileId'; IdField = 'credential_profile_id'
        Label = 'credential profile'; NameField = 'credential_name'; Help = 'credential profiles'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @(
            @{ Name = 'CredentialName'; Json = 'credential_name'; Type = 'string'; NewMandatory = $true }
            @{ Name = 'CredentialType'; Json = 'credential_type'; Type = 'int'; Doc = 'The API''s credential type number.' }
            @{ Name = 'Credential'; Json = 'username'; PasswordJson = 'password'; Type = 'credential'; Doc = 'The user name and password the profile holds.' }
        )
    }
    @{
        Noun = 'Tag'; Path = 'Tags'; Id = 'TagId'; IdField = 'tag_id'
        Label = 'tag'; NameField = 'tag_name'; Help = 'tags'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @(
            @{ Name = 'TagName'; Json = 'tag_name'; Type = 'string'; NewMandatory = $true }
            @{ Name = 'TagValue'; Json = 'tag_value'; Type = 'string'; Doc = 'The tag''s value.' }
            @{ Name = 'TagColor'; Json = 'tag_color'; Type = 'string'; Doc = 'A hex color such as #B7DA9E.' }
        )
    }
    @{
        Noun = 'OnCallSchedule'; Path = 'OnCallSchedules'; Id = 'OnCallScheduleId'; IdField = 'on_call_schedule_id'
        Label = 'on-call schedule'; NameField = 'schedule_name'; Help = 'on-call schedules'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @( @{ Name = 'ScheduleName'; Json = 'schedule_name'; Type = 'string' } )
    }
    @{
        Noun = 'AttributeAlertGroup'; Path = 'AttributeGroups'; Id = 'AttributeGroupId'; IdField = 'attribute_group_id'
        Label = 'attribute alert group'; NameField = 'display_name'; Help = 'attribute alert groups'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @( @{ Name = 'DisplayName'; Json = 'display_name'; Type = 'string' } )
    }
    @{
        Noun = 'EmailTemplate'; Path = 'EmailTemplates'; Id = 'TemplateId'; IdField = 'template_id'
        Label = 'email template'; NameField = 'display_name'; Help = 'email templates'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @( @{ Name = 'DisplayName'; Json = 'display_name'; Type = 'string' } )
    }
    @{
        Noun = 'BusinessHours'; Path = 'BusinessHours'; Id = 'BusinessHoursId'; IdField = 'business_hours_id'
        Label = 'business hours'; NameField = 'display_name'; Help = 'business hours definitions'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @( @{ Name = 'DisplayName'; Json = 'display_name'; Type = 'string' } )
    }
    @{
        Noun = 'OAuthProvider'; Path = 'OAuthProviders'; Id = 'ProviderId'; IdField = 'provider_id'
        Label = 'OAuth provider'; NameField = 'name'; Help = 'OAuth providers used by REST API monitors'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @( @{ Name = 'ProviderName'; Json = 'name'; Type = 'string' } )
    }
    @{
        Noun = 'WebToken'; Path = 'WebTokens'; Id = 'WebTokenId'; IdField = 'web_token_id'
        Label = 'web token'; NameField = 'name'; Help = 'web tokens'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @()
    }
    @{
        Noun = 'GlobalParameter'; Path = 'GlobalParameters'; Id = 'ParameterId'; IdField = 'param_id'
        Label = 'global parameter'; NameField = 'name'; Help = 'global parameters'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @(
            @{ Name = 'ParameterName'; Json = 'name'; Type = 'string' }
            @{ Name = 'Value'; Json = 'value'; Type = 'string'; Doc = 'The parameter''s value.' }
        )
    }
    @{
        Noun = 'ITAutomation'; Path = 'ITAutomation'; Id = 'ActionId'; IdField = 'action_id'
        Label = 'IT automation'; NameField = 'action_name'; Help = 'IT automation actions'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @(
            @{ Name = 'ActionName'; Json = 'action_name'; Type = 'string'; NewMandatory = $true }
            @{ Name = 'ActionType'; Json = 'action_type'; Type = 'int'; Doc = 'The automation type constant, for example 1 for URL action.' }
            @{ Name = 'ActionUrl'; Json = 'action_url'; Type = 'string'; Doc = 'The URL the action calls.' }
            @{ Name = 'ActionMethod'; Json = 'action_method'; Type = 'string'; Doc = 'P = POST, G = GET.' }
            @{ Name = 'ActionTimeout'; Json = 'action_timeout'; Type = 'int'; Doc = 'Seconds to wait for the action to finish.' }
        )
    }
    @{
        Noun = 'ConfigurationRule'; Path = 'ConfigurationRules'; Id = 'RuleId'; IdField = 'rule_id'
        Label = 'configuration rule'; NameField = 'rule_name'; Help = 'configuration rules'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @()
    }
    @{
        Noun = 'ScheduledReport'; Path = 'ScheduledReports'; Id = 'ReportId'; IdField = 'report_id'
        Label = 'scheduled report'; NameField = 'display_name'; Help = 'scheduled reports'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @( @{ Name = 'DisplayName'; Json = 'display_name'; Type = 'string' } )
    }
    @{
        Noun = 'SlaSetting'; Path = 'SlaSettings'; Id = 'SlaId'; IdField = 'sla_id'
        Label = 'SLA setting'; NameField = 'display_name'; Help = 'SLA settings'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @( @{ Name = 'DisplayName'; Json = 'display_name'; Type = 'string' } )
    }
    @{
        Noun = 'StatusPage'; Path = 'StatusPages'; Id = 'StatusPageId'; IdField = 'statuspage_id'
        Label = 'status page'; NameField = 'display_name'; Help = 'status pages'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @( @{ Name = 'DisplayName'; Json = 'display_name'; Type = 'string' } )
    }
    @{
        Noun = 'ThirdPartyIntegration'; Path = 'ThirdPartyServices'; Id = 'ServiceId'; IdField = 'service_id'
        Label = 'third-party integration'; NameField = 'name'; Help = 'third-party integrations (Slack, PagerDuty, webhooks and others)'
        Verbs = 'Get', 'Remove'
        Fields = @()
    }
    @{
        Noun = 'OnPremisePoller'; Path = 'OnPremisePollers'; Id = 'PollerId'; IdField = 'poller_id'
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

if ($TableOnly) { return $resources }

function Get-CSharpType([string] $type) {
    switch ($type) {
        'switch' { 'SwitchParameter' }
        'credential' { 'PSCredential' }
        default { $type }
    }
}

$script:sb = [System.Text.StringBuilder]::new()
function Line([string] $text = '') { [void]$script:sb.AppendLine($text) }

function ConvertTo-XmlText([string] $text) {
    $text.Replace('&', '&amp;').Replace('<', '&lt;').Replace('>', '&gt;')
}

function Write-Summary([string] $indent, [string] $text) {
    if ($text) { Line "$indent/// <summary>$(ConvertTo-XmlText $text)</summary>" }
}

function Write-Synopsis([string] $text) {
    Line '    /// <summary>'
    Line "    /// <para type=`"synopsis`">$(ConvertTo-XmlText $text)</para>"
    Line '    /// </summary>'
}

# The id parameter: positional, bound by property name from API objects through its IdField alias.
function Write-IdParameter($r, [string] $parameterSet) {
    $set = if ($parameterSet) { "ParameterSetName = $parameterSet, " } else { '' }
    Write-Summary '        ' "The $($r.Label)'s id."
    Line "        [Parameter(Mandatory = true, Position = 0, ${set}ValueFromPipelineByPropertyName = true)]"
    Line "        [Alias(`"$($r.IdField)`")]"
    Line '        [ValidateNotNullOrEmpty]'
    Line "        public string $($r.Id) { get; set; }"
    Line
}

function Write-Common($r) {
    Line "        protected override string ResourcePath => Endpoints.$($r.Path);"
    Line "        protected override string ResourceLabel => `"$($r.Label)`";"
    if ($r.ApiVersion) { Line "        protected override string ApiVersion => `"$($r.ApiVersion)`";" }
}

function Write-Field($r, $f, [bool] $override) {
    $doc = if ($f.Doc) { $f.Doc } elseif ($f.Name -like '*Name') { "The $($r.Label)'s name." } else { $null }
    Write-Summary '        ' $doc
    if ($override) {
        Line '        [Parameter(Mandatory = true, Position = __POS__)]'
    } else {
        Line '        [Parameter]'
    }
    if ($f.Type -eq 'credential') { Line '        [Credential]' }
    elseif ($f.Type -in 'string', 'string[]') { if ($override) { Line '        [ValidateNotNullOrEmpty]' } }
    $modifier = if ($override) { 'override ' } elseif ($f.NewMandatory) { 'virtual ' } else { '' }
    Line "        public $modifier$(Get-CSharpType $f.Type) $($f.Name) { get; set; }"
    Line
}

Line '// <auto-generated>'
Line '// Generated by tools/Build-ResourceCmdlets.ps1. Edit the table in that script, not this file.'
Line '// </auto-generated>'
Line 'using System.Collections;'
Line 'using System.Management.Automation;'
Line
Line 'namespace Site24x7.Cmdlets'
Line '{'

foreach ($r in $resources) {
    $noun = $r.Noun
    Line "    #region $noun"
    Line

    if ($r.Verbs -contains 'Get') {
        Write-Synopsis "Gets $($r.Help)."
        Line "    [Cmdlet(VerbsCommon.Get, `"Site24x7$noun`", DefaultParameterSetName = ParameterSets.List)]"
        Line '    [OutputType(typeof(PSObject))]'
        Line "    public sealed class GetSite24x7${noun}Command : GetResourceCmdlet"
        Line '    {'
        if (-not $r.ListOnly) {
            Write-IdParameter $r 'ParameterSets.ById'
            Line "        protected override string ResourceId => $($r.Id);"
        }
        Write-Common $r
        Line "        protected override string NameProperty => `"$($r.NameField)`";"
        Line '    }'
        Line
    }

    if (($r.Verbs -contains 'New') -or ($r.Verbs -contains 'Set')) {
        Line "    /// <summary>Settings shared by New-Site24x7$noun and Set-Site24x7$noun.</summary>"
        Line "    public abstract class Site24x7${noun}WriteCmdlet : WriteResourceCmdlet"
        Line '    {'
        foreach ($f in $r.Fields) { Write-Field $r $f $false }
        Write-Common $r
        Line
        Line '        protected override void BuildBody(IDictionary body)'
        Line '        {'
        foreach ($f in $r.Fields) {
            if ($f.Type -eq 'credential') {
                Line "            if ($($f.Name) != null && $($f.Name) != PSCredential.Empty)"
                Line '            {'
                Line "                body[`"$($f.Json)`"] = $($f.Name).UserName;"
                Line "                body[`"$($f.PasswordJson)`"] = $($f.Name).GetNetworkCredential().Password;"
                Line '            }'
            } else {
                Line "            AddIfBound(body, `"$($f.Json)`", nameof($($f.Name)), $($f.Name));"
            }
        }
        Line '        }'
        Line '    }'
        Line
    }

    if ($r.Verbs -contains 'New') {
        Write-Synopsis "Creates a $($r.Label). Fields without a parameter can be passed with -Property."
        Line "    [Cmdlet(VerbsCommon.New, `"Site24x7$noun`", SupportsShouldProcess = true)]"
        Line '    [OutputType(typeof(PSObject))]'
        Line "    public sealed class NewSite24x7${noun}Command : Site24x7${noun}WriteCmdlet"
        Line '    {'
        $pos = 0
        foreach ($f in @($r.Fields | Where-Object { $_.NewMandatory })) {
            $before = $script:sb.Length
            Write-Field $r $f $true
            [void]$script:sb.Replace('__POS__', "$pos", $before, $script:sb.Length - $before)
            $pos++
        }
        if ($pos -gt 0) { $script:sb.Length -= [Environment]::NewLine.Length }
        Line '    }'
        Line
    }

    if ($r.Verbs -contains 'Set') {
        Write-Synopsis "Updates a $($r.Label). Only the settings you pass are sent."
        Line "    [Cmdlet(VerbsCommon.Set, `"Site24x7$noun`", SupportsShouldProcess = true)]"
        Line '    [OutputType(typeof(PSObject))]'
        Line "    public sealed class SetSite24x7${noun}Command : Site24x7${noun}WriteCmdlet"
        Line '    {'
        Write-IdParameter $r $null
        Line "        protected override string ResourceId => $($r.Id);"
        Line '    }'
        Line
    }

    if ($r.Verbs -contains 'Remove') {
        Write-Synopsis "Deletes a $($r.Label)."
        Line "    [Cmdlet(VerbsCommon.Remove, `"Site24x7$noun`", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]"
        Line "    public sealed class RemoveSite24x7${noun}Command : RemoveResourceCmdlet"
        Line '    {'
        Write-IdParameter $r $null
        Line "        protected override string ResourceId => $($r.Id);"
        Write-Common $r
        Line '    }'
        Line
    }

    Line '    #endregion'
    Line
}

Line '}'

$dir = Split-Path $OutFile -Parent
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
$text = $script:sb.ToString() -replace "`r`n", "`n"
[System.IO.File]::WriteAllText($OutFile, $text, [System.Text.UTF8Encoding]::new($false))
Write-Verbose "Wrote $OutFile"
