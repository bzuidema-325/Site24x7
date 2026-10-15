using System.Collections;
using System.Management.Automation;

namespace Site24x7.Cmdlets
{
    // https://www.site24x7.com/help/api/#monitor-groups

    /// <summary>Settings shared by monitor groups and subgroups.</summary>
    public abstract class GroupWriteCmdlet : WriteResourceCmdlet
    {
        [Parameter]
        [Alias("display_name")]
        [ValidateNotNullOrEmpty]
        public virtual string DisplayName { get; set; }

        [Parameter]
        public string Description { get; set; }

        /// <summary>Ids of the monitors in the group.</summary>
        [Parameter]
        public string[] Monitors { get; set; }

        /// <summary>How many monitors must be down before the group is reported down.</summary>
        [Parameter]
        [Alias("health_threshold_count")]
        public int HealthThresholdCount { get; set; }

        [Parameter]
        [Alias("healthcheck_profile_id")]
        public string HealthcheckProfileId { get; set; }

        [Parameter]
        [Alias("notification_profile_id")]
        public string NotificationProfileId { get; set; }

        [Parameter]
        [Alias("user_group_ids")]
        public string[] UserGroupIds { get; set; }

        [Parameter]
        [Alias("on_call_schedule_id")]
        public string OnCallScheduleId { get; set; }

        [Parameter]
        [Alias("third_party_services")]
        public string[] ThirdPartyServices { get; set; }

        [Parameter]
        [Alias("group_type")]
        public int GroupType { get; set; }

        [Parameter]
        [Alias("tag_ids")]
        public string[] TagIds { get; set; }

        protected override void BuildBody(IDictionary body)
        {
            AddIfBound(body, "display_name", nameof(DisplayName), DisplayName);
            AddIfBound(body, "description", nameof(Description), Description);
            AddIfBound(body, "monitors", nameof(Monitors), Monitors);
            AddIfBound(body, "health_threshold_count", nameof(HealthThresholdCount), HealthThresholdCount);
            AddIfBound(body, "healthcheck_profile_id", nameof(HealthcheckProfileId), HealthcheckProfileId);
            AddIfBound(body, "notification_profile_id", nameof(NotificationProfileId), NotificationProfileId);
            AddIfBound(body, "user_group_ids", nameof(UserGroupIds), UserGroupIds);
            AddIfBound(body, "on_call_schedule_id", nameof(OnCallScheduleId), OnCallScheduleId);
            AddIfBound(body, "third_party_services", nameof(ThirdPartyServices), ThirdPartyServices);
            AddIfBound(body, "group_type", nameof(GroupType), GroupType);
            AddIfBound(body, "tag_ids", nameof(TagIds), TagIds);
        }
    }

    /// <summary>Monitor-group-only settings.</summary>
    public abstract class MonitorGroupWriteCmdlet : GroupWriteCmdlet
    {
        [Parameter]
        [Alias("dependency_resource_ids", "dependency_resources_ids")]
        public string[] DependencyResourceIds { get; set; }

        /// <summary>Suppress alerts for the group's monitors while a dependency is down.</summary>
        [Parameter]
        [Alias("suppress_alert")]
        public SwitchParameter SuppressAlert { get; set; }

        protected override string ResourcePath => Endpoints.MonitorGroups;
        protected override string ApiVersion => "2.1";
        protected override string ResourceLabel => "monitor group";

        protected override void BuildBody(IDictionary body)
        {
            base.BuildBody(body);
            AddIfBound(body, "dependency_resource_ids", nameof(DependencyResourceIds), DependencyResourceIds);
            AddIfBound(body, "suppress_alert", nameof(SuppressAlert), SuppressAlert);
        }
    }

    /// <summary>
    /// <para type="synopsis">Gets monitor groups.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "Site24x7Group", DefaultParameterSetName = ParameterSets.List)]
    [Alias("Get-Site24x7MonitorGroup")]
    [OutputType(typeof(PSObject))]
    public sealed class GetSite24x7GroupCommand : GetResourceCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSets.ById, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [Alias("group_id", "Id")]
        [ValidateNotNullOrEmpty]
        public string GroupId { get; set; }

        protected override string ResourcePath => Endpoints.MonitorGroups;
        protected override string ResourceId => GroupId;
        protected override string ApiVersion => "2.1";
    }

    /// <summary>
    /// <para type="synopsis">Creates a monitor group.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "Site24x7Group", SupportsShouldProcess = true)]
    [Alias("New-Site24x7MonitorGroup")]
    [OutputType(typeof(PSObject))]
    public sealed class NewSite24x7GroupCommand : MonitorGroupWriteCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        [Alias("display_name")]
        [ValidateNotNullOrEmpty]
        public override string DisplayName { get; set; }

        protected override void ProcessRecordCore()
        {
            var body = CreateBody();
            if (ShouldProcess(Describe(body, ResourceLabel), "Create monitor group"))
            {
                WriteData(Call("POST", ResourcePath, body, apiVersion: ApiVersion));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Updates a monitor group. Only the settings you pass are sent.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "Site24x7Group", SupportsShouldProcess = true)]
    [Alias("Update-Site24x7Group", "Set-Site24x7MonitorGroup")]
    [OutputType(typeof(PSObject))]
    public sealed class SetSite24x7GroupCommand : MonitorGroupWriteCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("group_id", "Id")]
        [ValidateNotNullOrEmpty]
        public string GroupId { get; set; }

        protected override string ResourceId => GroupId;

        protected override void ProcessRecordCore()
        {
            var body = CreateBody();
            if (body.Count == 0) throw new Site24x7Exception($"Nothing to update on monitor group {GroupId}.");
            if (ShouldProcess(GroupId, "Update monitor group"))
            {
                WriteData(Call("PUT", ItemPath, body, apiVersion: ApiVersion));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Deletes a monitor group. Its monitors are not deleted.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "Site24x7Group", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
    [Alias("Remove-Site24x7MonitorGroup")]
    public sealed class RemoveSite24x7GroupCommand : RemoveResourceCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [Alias("group_id", "Id")]
        [ValidateNotNullOrEmpty]
        public string GroupId { get; set; }

        protected override string ResourcePath => Endpoints.MonitorGroups;
        protected override string ResourceId => GroupId;
        protected override string ApiVersion => "2.1";
        protected override string ResourceLabel => "monitor group";
    }

    /// <summary>
    /// <para type="synopsis">Adds monitors to a monitor group, by id or by name.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "Site24x7GroupMonitor", SupportsShouldProcess = true, DefaultParameterSetName = "ID")]
    [Alias("Update-Site24x7Group_AddMonitor")]
    [OutputType(typeof(PSObject))]
    public sealed class AddSite24x7GroupMonitorCommand : Site24x7Cmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ID", ValueFromPipelineByPropertyName = true)]
        [Alias("group_id")]
        [ValidateNotNullOrEmpty]
        public string GroupId { get; set; }

        [Parameter(Mandatory = true, Position = 1, ParameterSetName = "ID")]
        [Alias("monitor_ids")]
        [ValidateNotNullOrEmpty]
        public string[] MonitorIds { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "Name")]
        [Alias("group_name")]
        [ValidateNotNullOrEmpty]
        public string GroupName { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "Name")]
        [Alias("monitor_names")]
        [ValidateNotNullOrEmpty]
        public string[] MonitorNames { get; set; }

        protected override object ErrorTarget => GroupId ?? GroupName;

        protected override void ProcessRecordCore()
        {
            var body = NewBody();
            if (ParameterSetName == "ID")
            {
                body["group_id"] = GroupId;
                body["monitor_ids"] = MonitorIds;
            }
            else
            {
                body["group_name"] = GroupName;
                body["monitor_names"] = MonitorNames;
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
        [Alias("top_group_id")]
        public virtual string TopGroupId { get; set; }

        /// <summary>The group or subgroup directly above this one.</summary>
        [Parameter]
        [Alias("parent_group_id")]
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
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSets.ById, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [Alias("group_id", "Id")]
        [ValidateNotNullOrEmpty]
        public string SubgroupId { get; set; }

        protected override string ResourcePath => Endpoints.Subgroups;
        protected override string ResourceId => SubgroupId;
    }

    /// <summary>
    /// <para type="synopsis">Creates a subgroup inside a monitor group.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "Site24x7Subgroup", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class NewSite24x7SubgroupCommand : SubgroupWriteCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        [Alias("display_name")]
        [ValidateNotNullOrEmpty]
        public override string DisplayName { get; set; }

        [Parameter(Mandatory = true)]
        [Alias("top_group_id")]
        [ValidateNotNullOrEmpty]
        public override string TopGroupId { get; set; }

        [Parameter(Mandatory = true)]
        [Alias("parent_group_id")]
        [ValidateNotNullOrEmpty]
        public override string ParentGroupId { get; set; }

        protected override void ProcessRecordCore()
        {
            var body = CreateBody();
            if (ShouldProcess(Describe(body, ResourceLabel), "Create subgroup"))
            {
                WriteData(Call("POST", ResourcePath, body));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Updates a subgroup. Only the settings you pass are sent.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "Site24x7Subgroup", SupportsShouldProcess = true)]
    [Alias("Update-Site24x7Subgroup")]
    [OutputType(typeof(PSObject))]
    public sealed class SetSite24x7SubgroupCommand : SubgroupWriteCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("group_id", "Id")]
        [ValidateNotNullOrEmpty]
        public string SubgroupId { get; set; }

        protected override string ResourceId => SubgroupId;

        protected override void ProcessRecordCore()
        {
            var body = CreateBody();
            if (body.Count == 0) throw new Site24x7Exception($"Nothing to update on subgroup {SubgroupId}.");
            if (ShouldProcess(SubgroupId, "Update subgroup"))
            {
                WriteData(Call("PUT", ItemPath, body));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Deletes a subgroup.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "Site24x7Subgroup", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
    public sealed class RemoveSite24x7SubgroupCommand : RemoveResourceCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [Alias("group_id", "Id")]
        [ValidateNotNullOrEmpty]
        public string SubgroupId { get; set; }

        protected override string ResourcePath => Endpoints.Subgroups;
        protected override string ResourceId => SubgroupId;
        protected override string ResourceLabel => "subgroup";
    }
}
