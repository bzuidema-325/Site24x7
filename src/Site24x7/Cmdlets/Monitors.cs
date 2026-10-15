using System;
using System.Collections;
using System.Linq;
using System.Management.Automation;

namespace Site24x7.Cmdlets
{
    // https://www.site24x7.com/help/api/#monitors
    //
    // Id parameters take an alias that is the API's own field name (monitor_id and so on), so
    // objects returned by one cmdlet can be piped to the next: Get-Site24x7Monitor | Disable-Site24x7Monitor.

    /// <summary>
    /// <para type="synopsis">Gets monitors.</para>
    /// <para type="description">Without -MonitorId, lists every monitor, optionally filtered by -Name (wildcards allowed) and -Type.</para>
    /// </summary>
    /// <example>
    /// <code>Get-Site24x7Monitor -Name 'web-*' -Type URL</code>
    /// </example>
    [Cmdlet(VerbsCommon.Get, "Site24x7Monitor", DefaultParameterSetName = ParameterSets.List)]
    [OutputType(typeof(PSObject))]
    public sealed class GetSite24x7MonitorCommand : GetResourceCmdlet
    {
        /// <summary>The monitor's id.</summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSets.ById, ValueFromPipelineByPropertyName = true)]
        [Alias("monitor_id")]
        [ValidateNotNullOrEmpty]
        public string MonitorId { get; set; }

        /// <summary>Only monitors of this type, for example URL, RESTAPI, SSL_CERT, SERVER. See Get-Site24x7MonitorType.</summary>
        [Parameter(ParameterSetName = ParameterSets.List)]
        [SupportsWildcards]
        [ValidateNotNullOrEmpty]
        public string Type { get; set; }

        protected override string ResourcePath => Endpoints.Monitors;
        protected override string ResourceId => MonitorId;
        protected override string ResourceLabel => "monitor";

        protected override void ProcessRecordCore()
        {
            if (!string.IsNullOrEmpty(MonitorId) || string.IsNullOrEmpty(Type))
            {
                base.ProcessRecordCore();
                return;
            }

            var pattern = new WildcardPattern(Type, WildcardOptions.IgnoreCase);
            var monitors = FilterByName(Call("GET", ResourcePath, query: Query));
            WriteData(monitors.Where(m => pattern.IsMatch(Convert.ToString(Json.Get(m, "type")) ?? string.Empty)).ToArray());
        }
    }

    /// <summary>Settings shared by every monitor type.</summary>
    public abstract class MonitorWriteCmdlet : WriteResourceCmdlet
    {
        /// <summary>The monitor's name.</summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public virtual string DisplayName { get; set; }

        /// <summary>How often to check, in minutes, as the API expects it (for example "1", "5", "15", "1440").</summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public string CheckFrequency { get; set; }

        /// <summary>Request timeout in seconds.</summary>
        [Parameter]
        [ValidateRange(1, 600)]
        public int Timeout { get; set; }

        /// <summary>Where the monitor checks from. See Get-Site24x7LocationProfile.</summary>
        [Parameter]
        public string LocationProfileId { get; set; }

        /// <summary>See Get-Site24x7NotificationProfile.</summary>
        [Parameter]
        public string NotificationProfileId { get; set; }

        /// <summary>See Get-Site24x7ThresholdProfile.</summary>
        [Parameter]
        public string ThresholdProfileId { get; set; }

        /// <summary>The user groups that are alerted. See Get-Site24x7UserGroup.</summary>
        [Parameter]
        public string[] UserGroupId { get; set; }

        /// <summary>The monitor groups the monitor belongs to. See Get-Site24x7Group.</summary>
        [Parameter]
        public string[] MonitorGroupId { get; set; }

        /// <summary>See Get-Site24x7Tag.</summary>
        [Parameter]
        public string[] TagId { get; set; }

        /// <summary>See Get-Site24x7OnCallSchedule.</summary>
        [Parameter]
        public string OnCallScheduleId { get; set; }

        /// <summary>Third-party integrations to alert. See Get-Site24x7ThirdPartyIntegration.</summary>
        [Parameter]
        public string[] ThirdPartyServiceId { get; set; }

        /// <summary>Monitors this one depends on; alerts are suppressed while they are down.</summary>
        [Parameter]
        public string[] DependencyResourceId { get; set; }

        protected override string ResourcePath => Endpoints.Monitors;
        protected override string ResourceLabel => "monitor";

        protected override void BuildBody(IDictionary body)
        {
            AddIfBound(body, "display_name", nameof(DisplayName), DisplayName);
            AddIfBound(body, "check_frequency", nameof(CheckFrequency), CheckFrequency);
            AddIfBound(body, "timeout", nameof(Timeout), Timeout);
            AddIfBound(body, "location_profile_id", nameof(LocationProfileId), LocationProfileId);
            AddIfBound(body, "notification_profile_id", nameof(NotificationProfileId), NotificationProfileId);
            AddIfBound(body, "threshold_profile_id", nameof(ThresholdProfileId), ThresholdProfileId);
            AddIfBound(body, "user_group_ids", nameof(UserGroupId), UserGroupId);
            AddIfBound(body, "monitor_groups", nameof(MonitorGroupId), MonitorGroupId);
            AddIfBound(body, "tag_ids", nameof(TagId), TagId);
            AddIfBound(body, "on_call_schedule_id", nameof(OnCallScheduleId), OnCallScheduleId);
            AddIfBound(body, "third_party_services", nameof(ThirdPartyServiceId), ThirdPartyServiceId);
            AddIfBound(body, "dependency_resource_ids", nameof(DependencyResourceId), DependencyResourceId);
        }
    }

    /// <summary>
    /// <para type="synopsis">Creates a monitor of any type.</para>
    /// <para type="description">Pass the monitor type and its type-specific fields in -Property, using the field names from the Monitor Parameters section of the Site24x7 API documentation. For common types, the dedicated cmdlets (New-Site24x7WebsiteMonitor and others) are easier.</para>
    /// </summary>
    /// <example>
    /// <code>New-Site24x7Monitor -Type SSL_CERT -DisplayName 'example.com cert' -Property @{ domain_name = 'example.com'; port = 443; expire_days = 21 }</code>
    /// </example>
    [Cmdlet(VerbsCommon.New, "Site24x7Monitor", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class NewSite24x7MonitorCommand : MonitorWriteCmdlet
    {
        /// <summary>The monitor's name.</summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public override string DisplayName { get; set; }

        /// <summary>The monitor type code, for example URL, RESTAPI, SSL_CERT, PING, PORT, DNS, HEARTBEAT, SERVER.</summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string Type { get; set; }

        protected override void BuildBody(IDictionary body)
        {
            body["type"] = Type;
            base.BuildBody(body);
        }
    }

    /// <summary>
    /// <para type="synopsis">Updates a monitor.</para>
    /// <para type="description">Only the settings you pass are sent. Site24x7 requires some monitor types to be updated with their full definition; if an update is rejected for missing fields, add -MergeWithCurrent to send the current definition with your changes applied.</para>
    /// </summary>
    /// <example>
    /// <code>Set-Site24x7Monitor 123 -CheckFrequency 1</code>
    /// </example>
    [Cmdlet(VerbsCommon.Set, "Site24x7Monitor", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class SetSite24x7MonitorCommand : MonitorWriteCmdlet
    {
        /// <summary>The monitor's id.</summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("monitor_id")]
        [ValidateNotNullOrEmpty]
        public string MonitorId { get; set; }

        /// <summary>Reads the monitor first and sends its current definition with your changes applied.</summary>
        [Parameter]
        public SwitchParameter MergeWithCurrent { get; set; }

        protected override string ResourceId => MonitorId;

        protected override IDictionary PrepareUpdate(IDictionary changes)
        {
            if (!MergeWithCurrent) return changes;

            var body = NewBody();
            if (Call("GET", ItemPath) is PSObject current)
            {
                foreach (var p in current.Properties)
                {
                    if (p.Name != "monitor_id") body[p.Name] = p.Value;
                }
            }
            Merge(body, changes);
            return body;
        }
    }

    /// <summary>
    /// <para type="synopsis">Deletes a monitor.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "Site24x7Monitor", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
    public sealed class RemoveSite24x7MonitorCommand : RemoveResourceCmdlet
    {
        /// <summary>The monitor's id.</summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("monitor_id")]
        [ValidateNotNullOrEmpty]
        public string MonitorId { get; set; }

        protected override string ResourcePath => Endpoints.Monitors;
        protected override string ResourceId => MonitorId;
        protected override string ResourceLabel => "monitor";
    }

    /// <summary>Enable- and Disable-Site24x7Monitor: PUT {path}/{id} with no body.</summary>
    public abstract class MonitorStateCmdlet : ResourceCmdlet
    {
        /// <summary>The monitor's id.</summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("monitor_id")]
        [ValidateNotNullOrEmpty]
        public string MonitorId { get; set; }

        protected override string ResourceId => MonitorId;
        protected override string ResourceLabel => "monitor";
        protected override string OutputTypeName => null;

        /// <summary>The ShouldProcess action, for example "Suspend monitor".</summary>
        protected abstract string Action { get; }

        protected override void ProcessRecordCore()
        {
            var path = ItemPath;
            if (ShouldProcess(MonitorId, Action))
            {
                WriteData(Call("PUT", path));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Activates (resumes) a suspended monitor.</para>
    /// </summary>
    [Cmdlet(VerbsLifecycle.Enable, "Site24x7Monitor", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class EnableSite24x7MonitorCommand : MonitorStateCmdlet
    {
        protected override string ResourcePath => Endpoints.MonitorActivate;
        protected override string Action => "Activate monitor";
    }

    /// <summary>
    /// <para type="synopsis">Suspends a monitor.</para>
    /// </summary>
    /// <example>
    /// <code>Get-Site24x7Monitor -Name 'web-*' | Disable-Site24x7Monitor</code>
    /// </example>
    [Cmdlet(VerbsLifecycle.Disable, "Site24x7Monitor", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class DisableSite24x7MonitorCommand : MonitorStateCmdlet
    {
        protected override string ResourcePath => Endpoints.MonitorSuspend;
        protected override string Action => "Suspend monitor";
    }

    /// <summary>
    /// <para type="synopsis">Lists the monitor type codes the API accepts.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "Site24x7MonitorType")]
    [OutputType(typeof(PSObject))]
    public sealed class GetSite24x7MonitorTypeCommand : Site24x7Cmdlet
    {
        protected override void ProcessRecordCore() => WriteData(Call("GET", Endpoints.MonitorTypeConstants));
    }

    /// <summary>
    /// <para type="synopsis">Gets the device key used to install Site24x7 server and on-premise poller agents.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "Site24x7DeviceKey")]
    [OutputType(typeof(PSObject))]
    public sealed class GetSite24x7DeviceKeyCommand : Site24x7Cmdlet
    {
        protected override void ProcessRecordCore() => WriteData(Call("GET", Endpoints.DeviceKey));
    }
}
