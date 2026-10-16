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
      Verbs       Which of Get, New, Set, Remove to generate. Areas whose New and Set are hand-written
                  (monitors, monitor groups and others) generate only Get and Remove here.
      GetPartial  Get-* is a partial class, so a hand-written part can add list filters
      Description Extra help text for the Get-* cmdlet; Example is a Get-* help example
      RemoveNote  Extra sentence for the Remove-* synopsis
      PropertyMandatory  New-* requires -Property, because the area has no typed fields
      ListOnly    Get has no id parameter (the API only lists)
      Fields      Typed body parameters: Name, Json (API field), Type (string, string[], int, switch,
                  credential, or an enum name), Doc, and NewMandatory to make it required on New-*.
                  A credential field sends the user name to Json and the password to PasswordJson.
                  String parameters never accept an empty value; clear a field with -Property.
      Help        Noun phrase for the Get-* synopsis

    A path marked "// verify" in src/Site24x7/Core/Endpoints.cs has not been checked against a
    live account yet. The generated help says so; tools/Test-Site24x7Endpoints.ps1 checks them.

    -TableOnly returns the table instead of writing the file; the tests use it. Each row then
    also carries ApiPath (the path string) and Verified.
#>
[CmdletBinding()]
param(
    [string] $OutFile = (Join-Path $PSScriptRoot '../src/Site24x7/Cmdlets/Generated/ResourceCmdlets.g.cs'),

    # Returns the resource table instead of writing the file. The tests use it to check every generated area.
    [switch] $TableOnly
)

$resources = @(
    @{
        Noun = 'Monitor'; Path = 'Monitors'; Id = 'MonitorId'; IdField = 'monitor_id'
        Label = 'monitor'; NameField = 'display_name'; Help = 'monitors'
        Verbs = 'Get', 'Remove'; GetPartial = $true; Fields = @()
        Description = 'Without -MonitorId, lists every monitor, optionally filtered by -Name (wildcards allowed) and -MonitorType.'
        Example = "Get-Site24x7Monitor -Name 'web-*' -MonitorType URL"
    }
    @{
        Noun = 'MonitorGroup'; Path = 'MonitorGroups'; Id = 'MonitorGroupId'; IdField = 'group_id'
        Label = 'monitor group'; NameField = 'display_name'; Help = 'monitor groups'
        Verbs = 'Get', 'Remove'; ApiVersion = '2.1'; Fields = @()
        RemoveNote = 'Its monitors are not deleted.'
    }
    @{
        Noun = 'Subgroup'; Path = 'Subgroups'; Id = 'SubgroupId'; IdField = 'group_id'
        Label = 'subgroup'; NameField = 'display_name'; Help = 'subgroups of monitor groups'
        Verbs = 'Get', 'Remove'; Fields = @()
    }
    @{
        Noun = 'User'; Path = 'Users'; Id = 'UserId'; IdField = 'user_id'
        Label = 'user'; NameField = 'display_name'; Help = 'users'
        Verbs = 'Get', 'Remove'; Fields = @()
    }
    @{
        Noun = 'UserGroup'; Path = 'UserGroups'; Id = 'UserGroupId'; IdField = 'user_group_id'
        Label = 'user group'; NameField = 'display_name'; Help = 'user groups'
        Verbs = 'Get', 'Remove'; Fields = @()
    }
    @{
        Noun = 'Maintenance'; Path = 'Maintenance'; Id = 'MaintenanceId'; IdField = 'maintenance_id'
        Label = 'maintenance window'; NameField = 'display_name'; Help = 'scheduled maintenance windows'
        Verbs = 'Get', 'Remove'; Fields = @()
    }
    @{
        Noun = 'Integration'; Path = 'ThirdPartyServices'; Id = 'IntegrationId'; IdField = 'service_id'
        Label = 'integration'; NameField = 'name'; Help = 'third-party integrations (Slack, PagerDuty, webhooks and others)'
        Verbs = 'Get', 'Remove'; Fields = @()
    }
    @{
        Noun = 'ThresholdProfile'; Path = 'ThresholdProfiles'; Id = 'ProfileId'; IdField = 'profile_id'
        Label = 'threshold profile'; NameField = 'profile_name'; Help = 'threshold profiles'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        ApiVersion = '2.1'
        Fields = @(
            @{ Name = 'ProfileName'; Json = 'profile_name'; Type = 'string'; NewMandatory = $true }
            @{ Name = 'MonitorType'; Json = 'type'; Type = 'string'; NewMandatory = $true; Doc = 'The monitor type the profile applies to, for example URL or SERVER.' }
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
        Fields = @( @{ Name = 'ScheduleName'; Json = 'schedule_name'; Type = 'string'; NewMandatory = $true } )
    }
    @{
        Noun = 'AttributeAlertGroup'; Path = 'AttributeGroups'; Id = 'AttributeGroupId'; IdField = 'attribute_group_id'
        Label = 'attribute alert group'; NameField = 'display_name'; Help = 'attribute alert groups'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @( @{ Name = 'DisplayName'; Json = 'display_name'; Type = 'string'; NewMandatory = $true } )
    }
    @{
        Noun = 'EmailTemplate'; Path = 'EmailTemplates'; Id = 'TemplateId'; IdField = 'template_id'
        Label = 'email template'; NameField = 'display_name'; Help = 'email templates'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @( @{ Name = 'DisplayName'; Json = 'display_name'; Type = 'string'; NewMandatory = $true } )
    }
    @{
        Noun = 'BusinessHours'; Path = 'BusinessHours'; Id = 'BusinessHoursId'; IdField = 'business_hours_id'
        Label = 'business hours'; NameField = 'display_name'; Help = 'business hours definitions'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @( @{ Name = 'DisplayName'; Json = 'display_name'; Type = 'string'; NewMandatory = $true } )
    }
    @{
        Noun = 'OAuthProvider'; Path = 'OAuthProviders'; Id = 'ProviderId'; IdField = 'provider_id'
        Label = 'OAuth provider'; NameField = 'name'; Help = 'OAuth providers used by REST API monitors'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @( @{ Name = 'ProviderName'; Json = 'name'; Type = 'string'; NewMandatory = $true } )
    }
    @{
        Noun = 'WebToken'; Path = 'WebTokens'; Id = 'WebTokenId'; IdField = 'web_token_id'
        Label = 'web token'; NameField = 'name'; Help = 'web tokens'
        Verbs = 'Get', 'New', 'Set', 'Remove'; PropertyMandatory = $true
        Fields = @()
    }
    @{
        Noun = 'GlobalParameter'; Path = 'GlobalParameters'; Id = 'ParameterId'; IdField = 'param_id'
        Label = 'global parameter'; NameField = 'name'; Help = 'global parameters'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @(
            @{ Name = 'ParameterName'; Json = 'name'; Type = 'string'; NewMandatory = $true }
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
        Verbs = 'Get', 'New', 'Set', 'Remove'; PropertyMandatory = $true
        Fields = @()
    }
    @{
        Noun = 'ScheduledReport'; Path = 'ScheduledReports'; Id = 'ReportId'; IdField = 'report_id'
        Label = 'scheduled report'; NameField = 'display_name'; Help = 'scheduled reports'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @( @{ Name = 'DisplayName'; Json = 'display_name'; Type = 'string'; NewMandatory = $true } )
    }
    @{
        Noun = 'SlaSetting'; Path = 'SlaSettings'; Id = 'SlaId'; IdField = 'sla_id'
        Label = 'SLA setting'; NameField = 'display_name'; Help = 'SLA settings'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @( @{ Name = 'DisplayName'; Json = 'display_name'; Type = 'string'; NewMandatory = $true } )
    }
    @{
        Noun = 'StatusPage'; Path = 'StatusPages'; Id = 'StatusPageId'; IdField = 'statuspage_id'
        Label = 'status page'; NameField = 'display_name'; Help = 'status pages'
        Verbs = 'Get', 'New', 'Set', 'Remove'
        Fields = @( @{ Name = 'DisplayName'; Json = 'display_name'; Type = 'string'; NewMandatory = $true } )
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

# The API path of each Endpoints constant, and whether it is marked "// verify".
$endpointsFile = Join-Path $PSScriptRoot '../src/Site24x7/Core/Endpoints.cs'
$endpoints = @{}
foreach ($line in Get-Content $endpointsFile) {
    if ($line -match 'public const string (\w+) = "([^"]*)";(.*)$') {
        $name, $path, $comment = $Matches[1], $Matches[2], $Matches[3]
        $endpoints[$name] = @{ Path = $path; Verified = $comment -notmatch '\bverify\b' }
    }
}
foreach ($r in $resources) {
    if (-not $endpoints.ContainsKey($r.Path)) { throw "Endpoints.$($r.Path) does not exist in $endpointsFile." }
    $r.ApiPath = $endpoints[$r.Path].Path
    $r.Verified = $endpoints[$r.Path].Verified
}

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

# Help for a cmdlet: the synopsis, then any description paragraphs, then examples.
function Write-Help($r, [string] $synopsis, [string[]] $description = @(), [string[]] $examples = @()) {
    if (-not $r.Verified) {
        $description += "The API path this cmdlet calls ($($r.ApiPath)) has not been checked against a live Site24x7 account yet. tools/Test-Site24x7Endpoints.ps1 in the module's repository checks it."
    }
    Line '    /// <summary>'
    Line "    /// <para type=`"synopsis`">$(ConvertTo-XmlText $synopsis)</para>"
    foreach ($d in $description | Where-Object { $_ }) {
        Line "    /// <para type=`"description`">$(ConvertTo-XmlText $d)</para>"
    }
    Line '    /// </summary>'
    foreach ($e in $examples | Where-Object { $_ }) {
        Line '    /// <example>'
        Line "    /// <code>$(ConvertTo-XmlText $e)</code>"
        Line '    /// </example>'
    }
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
    Line "        private protected override ResourceInfo Resource => Resources.$($r.Noun);"
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
    elseif ($f.Type -in 'string', 'string[]') { Line '        [ValidateNotNullOrEmpty]' }
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
Line '    internal static partial class Resources'
Line '    {'
foreach ($r in $resources) {
    $version = if ($r.ApiVersion) { ", `"$($r.ApiVersion)`"" } else { '' }
    Line "        public static readonly ResourceInfo $($r.Noun) = new ResourceInfo(Endpoints.$($r.Path), `"$($r.Label)`"$version);"
}
Line '    }'
Line

foreach ($r in $resources) {
    $noun = $r.Noun
    Line "    #region $noun"
    Line

    if ($r.Verbs -contains 'Get') {
        Write-Help $r "Gets $($r.Help)." @($r.Description) @($r.Example)
        $partial = if ($r.GetPartial) { 'partial ' } else { '' }
        Line "    [Cmdlet(VerbsCommon.Get, `"Site24x7$noun`", DefaultParameterSetName = ParameterSets.List)]"
        Line '    [OutputType(typeof(PSObject))]'
        Line "    public sealed ${partial}class GetSite24x7${noun}Command : GetResourceCmdlet"
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
        $how = if ($r.PropertyMandatory) { "Pass its fields with -Property, using the API's field names." } else { 'Fields without a parameter can be passed with -Property.' }
        Write-Help $r "Creates a $($r.Label). $how"
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
        if ($r.PropertyMandatory) {
            Write-Summary '        ' "The $($r.Label)'s fields, as a hashtable of the API's field names and values."
            Line '        [Parameter(Mandatory = true, Position = 0)]'
            Line '        [ValidateNotNull]'
            Line '        public override Hashtable Property { get; set; }'
            Line
            $pos++
        }
        if ($pos -gt 0) { $script:sb.Length -= [Environment]::NewLine.Length }
        Line '    }'
        Line
    }

    if ($r.Verbs -contains 'Set') {
        Write-Help $r "Updates a $($r.Label). Only the settings you pass are sent."
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
        $synopsis = "Deletes a $($r.Label)."
        if ($r.RemoveNote) { $synopsis += " $($r.RemoveNote)" }
        Write-Help $r $synopsis
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
