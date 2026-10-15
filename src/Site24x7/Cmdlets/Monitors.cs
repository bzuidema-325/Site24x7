using System;
using System.Collections;
using System.Linq;
using System.Management.Automation;

namespace Site24x7.Cmdlets
{
    // https://www.site24x7.com/help/api/#monitors

    /// <summary>
    /// <para type="synopsis">Gets monitors.</para>
    /// <para type="description">Without -MonitorId, lists every monitor, optionally filtered by -Name (wildcards allowed) and -Type.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "Site24x7Monitor", DefaultParameterSetName = ParameterSets.List)]
    [OutputType(typeof(PSObject))]
    public sealed class GetSite24x7MonitorCommand : GetResourceCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSets.ById, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [Alias("monitor_id", "Id")]
        [ValidateNotNullOrEmpty]
        public string MonitorId { get; set; }

        /// <summary>Only monitors of this type, for example URL, RESTAPI, SSL_CERT, SERVER. See Get-Site24x7MonitorType.</summary>
        [Parameter(ParameterSetName = ParameterSets.List)]
        [SupportsWildcards]
        public string Type { get; set; }

        protected override string ResourcePath => Endpoints.Monitors;
        protected override string ResourceId => MonitorId;

        protected override void ProcessRecordCore()
        {
            if (!string.IsNullOrEmpty(MonitorId) || string.IsNullOrEmpty(Type))
            {
                base.ProcessRecordCore();
                return;
            }

            var data = FilterByName(Call("GET", ResourcePath, query: Query));
            var pattern = new WildcardPattern(Type, WildcardOptions.IgnoreCase);
            if (data is object[] items)
            {
                WriteData(items.Where(i => pattern.IsMatch(Convert.ToString(Json.Get(i, "type")) ?? string.Empty)).ToArray());
            }
        }
    }

    /// <summary>Settings shared by every monitor type.</summary>
    public abstract class MonitorWriteCmdlet : WriteResourceCmdlet
    {
        [Parameter]
        [Alias("display_name")]
        [ValidateNotNullOrEmpty]
        public virtual string DisplayName { get; set; }

        /// <summary>How often to check, in minutes, as the API expects it (for example "1", "5", "15", "1440").</summary>
        [Parameter]
        [Alias("check_frequency")]
        public string CheckFrequency { get; set; }

        /// <summary>Request timeout in seconds.</summary>
        [Parameter]
        public int Timeout { get; set; }

        [Parameter]
        [Alias("location_profile_id")]
        public string LocationProfileId { get; set; }

        [Parameter]
        [Alias("notification_profile_id")]
        public string NotificationProfileId { get; set; }

        [Parameter]
        [Alias("threshold_profile_id")]
        public string ThresholdProfileId { get; set; }

        [Parameter]
        [Alias("user_group_ids")]
        public string[] UserGroupIds { get; set; }

        [Parameter]
        [Alias("monitor_groups")]
        public string[] MonitorGroups { get; set; }

        [Parameter]
        [Alias("tag_ids")]
        public string[] TagIds { get; set; }

        [Parameter]
        [Alias("on_call_schedule_id")]
        public string OnCallScheduleId { get; set; }

        [Parameter]
        [Alias("third_party_services")]
        public string[] ThirdPartyServices { get; set; }

        [Parameter]
        [Alias("dependency_resource_ids")]
        public string[] DependencyResourceIds { get; set; }

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
            AddIfBound(body, "user_group_ids", nameof(UserGroupIds), UserGroupIds);
            AddIfBound(body, "monitor_groups", nameof(MonitorGroups), MonitorGroups);
            AddIfBound(body, "tag_ids", nameof(TagIds), TagIds);
            AddIfBound(body, "on_call_schedule_id", nameof(OnCallScheduleId), OnCallScheduleId);
            AddIfBound(body, "third_party_services", nameof(ThirdPartyServices), ThirdPartyServices);
            AddIfBound(body, "dependency_resource_ids", nameof(DependencyResourceIds), DependencyResourceIds);
        }

        /// <summary>Sets a field to a default when the caller did not supply it in any form.</summary>
        protected void Default(IDictionary body, string field, string parameterName, object value)
        {
            if (!IsBound(parameterName) && !body.Contains(field) && (Property == null || !Property.ContainsKey(field)))
            {
                body[field] = value;
            }
        }

        /// <summary>Converts @{ 'X-Name' = 'value' } into the API's [{ name, value }] list.</summary>
        protected static object[] ToNameValueList(IDictionary headers)
        {
            return headers?.Cast<DictionaryEntry>()
                .Select(e => (object)new Hashtable { { "name", Convert.ToString(e.Key) }, { "value", Convert.ToString(e.Value) } })
                .ToArray();
        }
    }

    /// <summary>
    /// <para type="synopsis">Creates a monitor of any type.</para>
    /// <para type="description">Pass the monitor type and its type-specific fields in -Property, using the field names from the Monitor Parameters section of the Site24x7 API documentation. For common types, the dedicated cmdlets (New-Site24x7WebsiteMonitor and others) are easier.</para>
    /// </summary>
    /// <example>
    /// <code>New-Site24x7Monitor -Type URL -DisplayName 'Home page' -Property @{ website = 'https://example.com'; http_method = 'G'; timeout = 30; check_frequency = '5' }</code>
    /// </example>
    [Cmdlet(VerbsCommon.New, "Site24x7Monitor", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class NewSite24x7MonitorCommand : MonitorWriteCmdlet
    {
        [Parameter(Mandatory = true)]
        [Alias("display_name")]
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

        protected override void ProcessRecordCore()
        {
            var body = CreateBody();
            if (ShouldProcess(Describe(body, ResourceLabel), "Create monitor"))
            {
                WriteData(Call("POST", ResourcePath, body));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Updates a monitor.</para>
    /// <para type="description">Only the settings you pass are sent. Site24x7 requires some monitor types to be updated with their full definition; if an update is rejected for missing fields, add -MergeWithCurrent to send the current definition with your changes applied.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "Site24x7Monitor", SupportsShouldProcess = true)]
    [Alias("Update-Site24x7Monitor")]
    [OutputType(typeof(PSObject))]
    public sealed class SetSite24x7MonitorCommand : MonitorWriteCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("monitor_id", "Id")]
        [ValidateNotNullOrEmpty]
        public string MonitorId { get; set; }

        /// <summary>Reads the monitor first and sends its current definition with your changes applied.</summary>
        [Parameter]
        public SwitchParameter MergeWithCurrent { get; set; }

        protected override string ResourceId => MonitorId;

        protected override void ProcessRecordCore()
        {
            var changes = CreateBody();
            if (changes.Count == 0)
            {
                throw new Site24x7Exception($"Nothing to update on monitor {MonitorId}. Pass at least one setting or -Property.");
            }

            IDictionary body = changes;
            if (MergeWithCurrent)
            {
                var current = Call("GET", ItemPath) as PSObject;
                body = NewBody();
                if (current != null)
                {
                    foreach (var p in current.Properties)
                    {
                        if (p.Name == "monitor_id") continue;
                        body[p.Name] = p.Value;
                    }
                }
                Merge(body, changes);
            }

            if (ShouldProcess(MonitorId, "Update monitor"))
            {
                WriteData(Call("PUT", ItemPath, body));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Deletes a monitor.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "Site24x7Monitor", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
    public sealed class RemoveSite24x7MonitorCommand : RemoveResourceCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [Alias("monitor_id", "Id")]
        [ValidateNotNullOrEmpty]
        public string MonitorId { get; set; }

        protected override string ResourcePath => Endpoints.Monitors;
        protected override string ResourceId => MonitorId;
        protected override string ResourceLabel => "monitor";
    }

    /// <summary>
    /// <para type="synopsis">Activates (resumes) a suspended monitor.</para>
    /// </summary>
    [Cmdlet(VerbsLifecycle.Enable, "Site24x7Monitor", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class EnableSite24x7MonitorCommand : ResourceCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [Alias("monitor_id", "Id")]
        [ValidateNotNullOrEmpty]
        public string MonitorId { get; set; }

        protected override string ResourcePath => Endpoints.MonitorActivate;
        protected override string ResourceId => MonitorId;

        protected override void ProcessRecordCore()
        {
            if (ShouldProcess(MonitorId, "Activate monitor"))
            {
                WriteData(Call("PUT", ItemPath));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Suspends a monitor.</para>
    /// </summary>
    [Cmdlet(VerbsLifecycle.Disable, "Site24x7Monitor", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class DisableSite24x7MonitorCommand : ResourceCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [Alias("monitor_id", "Id")]
        [ValidateNotNullOrEmpty]
        public string MonitorId { get; set; }

        protected override string ResourcePath => Endpoints.MonitorSuspend;
        protected override string ResourceId => MonitorId;

        protected override void ProcessRecordCore()
        {
            if (ShouldProcess(MonitorId, "Suspend monitor"))
            {
                WriteData(Call("PUT", ItemPath));
            }
        }
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
