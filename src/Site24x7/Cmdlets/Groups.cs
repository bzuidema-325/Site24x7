using System.Collections;
using System.Management.Automation;

namespace Site24x7.Cmdlets
{
    // https://www.site24x7.com/help/api/#monitor-groups

    /// <summary>Settings shared by monitor groups and subgroups.</summary>
    public abstract class GroupWriteCmdlet : WriteResourceCmdlet
    {
        /// <summary>The group's name.</summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public virtual string DisplayName { get; set; }

        /// <summary>A description of the group.</summary>
        [Parameter]
        public string Description { get; set; }

        /// <summary>The monitors in the group. Replaces the current members on Set.</summary>
        [Parameter]
        public string[] MonitorId { get; set; }

        /// <summary>How many monitors must be down before the group is reported down.</summary>
        [Parameter]
        [ValidateRange(0, int.MaxValue)]
        public int HealthThresholdCount { get; set; }

        /// <summary>The health check profile that decides the group's status.</summary>
        [Parameter]
        public string HealthcheckProfileId { get; set; }

        /// <summary>See Get-Site24x7NotificationProfile.</summary>
        [Parameter]
        public string NotificationProfileId { get; set; }

        /// <summary>The user groups that are alerted.</summary>
        [Parameter]
        public string[] UserGroupId { get; set; }

        /// <summary>See Get-Site24x7OnCallSchedule.</summary>
        [Parameter]
        public string OnCallScheduleId { get; set; }

        /// <summary>Third-party integrations to alert.</summary>
        [Parameter]
        public string[] ThirdPartyServiceId { get; set; }

        /// <summary>The API's group type number.</summary>
        [Parameter]
        public int GroupType { get; set; }

        /// <summary>See Get-Site24x7Tag.</summary>
        [Parameter]
        public string[] TagId { get; set; }

        protected override void BuildBody(IDictionary body)
        {
            AddIfBound(body, "display_name", nameof(DisplayName), DisplayName);
            AddIfBound(body, "description", nameof(Description), Description);
            AddIfBound(body, "monitors", nameof(MonitorId), MonitorId);
            AddIfBound(body, "health_threshold_count", nameof(HealthThresholdCount), HealthThresholdCount);
            AddIfBound(body, "healthcheck_profile_id", nameof(HealthcheckProfileId), HealthcheckProfileId);
            AddIfBound(body, "notification_profile_id", nameof(NotificationProfileId), NotificationProfileId);
            AddIfBound(body, "user_group_ids", nameof(UserGroupId), UserGroupId);
            AddIfBound(body, "on_call_schedule_id", nameof(OnCallScheduleId), OnCallScheduleId);
            AddIfBound(body, "third_party_services", nameof(ThirdPartyServiceId), ThirdPartyServiceId);
            AddIfBound(body, "group_type", nameof(GroupType), GroupType);
            AddIfBound(body, "tag_ids", nameof(TagId), TagId);
        }
    }

    /// <summary>Monitor-group-only settings.</summary>
    public abstract class MonitorGroupWriteCmdlet : GroupWriteCmdlet
    {
        /// <summary>Monitors the group depends on.</summary>
        [Parameter]
        public string[] DependencyResourceId { get; set; }

        /// <summary>Suppress alerts for the group's monitors while a dependency is down.</summary>
        [Parameter]
        public SwitchParameter SuppressAlert { get; set; }

        protected override string ResourcePath => Endpoints.MonitorGroups;
        protected override string ApiVersion => "2.1";
        protected override string ResourceLabel => "monitor group";

        protected override void BuildBody(IDictionary body)
        {
            base.BuildBody(body);
            AddIfBound(body, "dependency_resource_ids", nameof(DependencyResourceId), DependencyResourceId);
            AddIfBound(body, "suppress_alert", nameof(SuppressAlert), SuppressAlert);
        }
    }

    /// <summary>
    /// <para type="synopsis">Gets monitor groups.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "Site24x7Group", DefaultParameterSetName = ParameterSets.List)]
    [OutputType(typeof(PSObject))]
    public sealed class GetSite24x7GroupCommand : GetResourceCmdlet
    {
        /// <summary>The monitor group's id.</summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSets.ById, ValueFromPipelineByPropertyName = true)]
        [Alias("group_id")]
        [ValidateNotNullOrEmpty]
        public string GroupId { get; set; }

        protected override string ResourcePath => Endpoints.MonitorGroups;
        protected override string ResourceId => GroupId;
        protected override string ApiVersion => "2.1";
        protected override string ResourceLabel => "monitor group";
    }

    /// <summary>
    /// <para type="synopsis">Creates a monitor group.</para>
    /// </summary>
    /// <example>
    /// <code>New-Site24x7Group 'Web servers' -MonitorId 1001, 1002 -HealthThresholdCount 1</code>
    /// </example>
    [Cmdlet(VerbsCommon.New, "Site24x7Group", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class NewSite24x7GroupCommand : MonitorGroupWriteCmdlet
    {
        /// <summary>The group's name.</summary>
        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        public override string DisplayName { get; set; }
    }

    /// <summary>
    /// <para type="synopsis">Updates a monitor group. Only the settings you pass are sent.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "Site24x7Group", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class SetSite24x7GroupCommand : MonitorGroupWriteCmdlet
    {
        /// <summary>The monitor group's id.</summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("group_id")]
        [ValidateNotNullOrEmpty]
        public string GroupId { get; set; }

        protected override string ResourceId => GroupId;
    }

    /// <summary>
    /// <para type="synopsis">Deletes a monitor group. Its monitors are not deleted.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "Site24x7Group", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
    public sealed class RemoveSite24x7GroupCommand : RemoveResourceCmdlet
    {
        /// <summary>The monitor group's id.</summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("group_id")]
        [ValidateNotNullOrEmpty]
        public string GroupId { get; set; }

        protected override string ResourcePath => Endpoints.MonitorGroups;
        protected override string ResourceId => GroupId;
        protected override string ApiVersion => "2.1";
        protected override string ResourceLabel => "monitor group";
    }

    /// <summary>
    /// <para type="synopsis">Adds monitors to a monitor group, by id or by name.</para>
    /// <para type="description">Unlike Set-Site24x7Group -MonitorId, this keeps the group's current members.</para>
    /// </summary>
    /// <example>
    /// <code>Add-Site24x7GroupMonitor -GroupName 'Web servers' -MonitorName web01, web02</code>
    /// </example>
    [Cmdlet(VerbsCommon.Add, "Site24x7GroupMonitor", SupportsShouldProcess = true, DefaultParameterSetName = ParameterSets.ById)]
    [OutputType(typeof(PSObject))]
    public sealed class AddSite24x7GroupMonitorCommand : Site24x7Cmdlet
    {
        /// <summary>The monitor group's id.</summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSets.ById, ValueFromPipelineByPropertyName = true)]
        [Alias("group_id")]
        [ValidateNotNullOrEmpty]
        public string GroupId { get; set; }

        /// <summary>The ids of the monitors to add.</summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSets.ById)]
        [ValidateNotNullOrEmpty]
        public string[] MonitorId { get; set; }

        /// <summary>The monitor group's name.</summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSets.ByName)]
        [ValidateNotNullOrEmpty]
        public string GroupName { get; set; }

        /// <summary>The names of the monitors to add.</summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSets.ByName)]
        [ValidateNotNullOrEmpty]
        public string[] MonitorName { get; set; }

        protected override object ErrorTarget => GroupId ?? GroupName;

        protected override void ProcessRecordCore()
        {
            var body = NewBody();
            if (ParameterSetName == ParameterSets.ById)
            {
                body["group_id"] = GroupId;
                body["monitor_ids"] = MonitorId;
            }
            else
            {
                body["group_name"] = GroupName;
                body["monitor_names"] = MonitorName;
            }

            if (ShouldProcess(GroupId ?? GroupName, "Add monitors to group"))
            {
                WriteData(Call("PUT", Endpoints.MonitorGroupAddMonitors, body));
            }
        }
    }

    // Subgroups: https://www.site24x7.com/help/api/#subgroups

    /// <summary>Subgroup-only settings.</summary>
    public abstract class SubgroupWriteCmdlet : GroupWriteCmdlet
    {
        /// <summary>The top-level monitor group the subgroup belongs to.</summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public virtual string TopGroupId { get; set; }

        /// <summary>The group or subgroup directly above this one.</summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public virtual string ParentGroupId { get; set; }

        protected override string ResourcePath => Endpoints.Subgroups;
        protected override string ResourceLabel => "subgroup";

        protected override void BuildBody(IDictionary body)
        {
            base.BuildBody(body);
            AddIfBound(body, "top_group_id", nameof(TopGroupId), TopGroupId);
            AddIfBound(body, "parent_group_id", nameof(ParentGroupId), ParentGroupId);
        }
    }

    /// <summary>
    /// <para type="synopsis">Gets subgroups.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "Site24x7Subgroup", DefaultParameterSetName = ParameterSets.List)]
    [OutputType(typeof(PSObject))]
    public sealed class GetSite24x7SubgroupCommand : GetResourceCmdlet
    {
        /// <summary>The subgroup's id.</summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSets.ById, ValueFromPipelineByPropertyName = true)]
        [Alias("group_id")]
        [ValidateNotNullOrEmpty]
        public string SubgroupId { get; set; }

        protected override string ResourcePath => Endpoints.Subgroups;
        protected override string ResourceId => SubgroupId;
        protected override string ResourceLabel => "subgroup";
    }

    /// <summary>
    /// <para type="synopsis">Creates a subgroup inside a monitor group.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "Site24x7Subgroup", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class NewSite24x7SubgroupCommand : SubgroupWriteCmdlet
    {
        /// <summary>The subgroup's name.</summary>
        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        public override string DisplayName { get; set; }

        /// <summary>The top-level monitor group the subgroup belongs to.</summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public override string TopGroupId { get; set; }

        /// <summary>The group or subgroup directly above this one.</summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public override string ParentGroupId { get; set; }
    }

    /// <summary>
    /// <para type="synopsis">Updates a subgroup. Only the settings you pass are sent.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "Site24x7Subgroup", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class SetSite24x7SubgroupCommand : SubgroupWriteCmdlet
    {
        /// <summary>The subgroup's id.</summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("group_id")]
        [ValidateNotNullOrEmpty]
        public string SubgroupId { get; set; }

        protected override string ResourceId => SubgroupId;
    }

    /// <summary>
    /// <para type="synopsis">Deletes a subgroup.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "Site24x7Subgroup", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
    public sealed class RemoveSite24x7SubgroupCommand : RemoveResourceCmdlet
    {
        /// <summary>The subgroup's id.</summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("group_id")]
        [ValidateNotNullOrEmpty]
        public string SubgroupId { get; set; }

        protected override string ResourcePath => Endpoints.Subgroups;
        protected override string ResourceId => SubgroupId;
        protected override string ResourceLabel => "subgroup";
    }
}
