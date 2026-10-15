using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Management.Automation;

namespace Site24x7.Cmdlets
{
    // https://www.site24x7.com/help/api/#schedule-maintenances

    /// <summary>Settings shared by New-Site24x7Maintenance and Set-Site24x7Maintenance.</summary>
    public abstract class MaintenanceWriteCmdlet : WriteResourceCmdlet
    {
        protected const string TimePattern = "^([01][0-9]|2[0-3]):[0-5][0-9]$";

        [Parameter]
        [Alias("display_name")]
        [ValidateNotNullOrEmpty]
        public virtual string DisplayName { get; set; }

        [Parameter]
        public string Description { get; set; }

        [Parameter]
        [Alias("maintenance_type")]
        public virtual MaintenanceType MaintenanceType { get; set; }

        /// <summary>Daily start time, as hh:mm.</summary>
        [Parameter]
        [Alias("start_time")]
        [ValidatePattern(TimePattern)]
        public virtual string StartTime { get; set; }

        /// <summary>Daily end time, as hh:mm.</summary>
        [Parameter]
        [Alias("end_time")]
        [ValidatePattern(TimePattern)]
        public virtual string EndTime { get; set; }

        /// <summary>Start date for a Once window.</summary>
        [Parameter]
        [Alias("start_date")]
        public DateTime StartDate { get; set; }

        /// <summary>End date for a Once window.</summary>
        [Parameter]
        [Alias("end_date")]
        public DateTime EndDate { get; set; }

        /// <summary>Time zone id, for example "America/New_York". Defaults to the account's time zone.</summary>
        [Parameter]
        public string TimeZone { get; set; }

        /// <summary>Monitor ids the window applies to.</summary>
        [Parameter]
        public string[] Monitors { get; set; }

        /// <summary>Monitor group ids the window applies to.</summary>
        [Parameter]
        [Alias("monitor_groups")]
        public string[] MonitorGroups { get; set; }

        /// <summary>Tag ids the window applies to.</summary>
        [Parameter]
        public string[] Tags { get; set; }

        /// <summary>With -MonitorGroups, also include monitors in subgroups.</summary>
        [Parameter]
        [Alias("subgroup_monitors")]
        public SwitchParameter SubgroupMonitors { get; set; }

        /// <summary>Keep polling during the window (alerts are still suppressed).</summary>
        [Parameter]
        [Alias("perform_monitoring")]
        public SwitchParameter PerformMonitoring { get; set; }

        /// <summary>Weekly windows: the day the window starts.</summary>
        [Parameter]
        [Alias("start_day")]
        public DayOfWeek StartDay { get; set; }

        /// <summary>Weekly windows: the day the window ends.</summary>
        [Parameter]
        [Alias("end_day")]
        public DayOfWeek EndDay { get; set; }

        /// <summary>Weekly windows: repeat every 1 to 4 weeks.</summary>
        [Parameter]
        [Alias("execute_every")]
        [ValidateRange(1, 4)]
        public int ExecuteEvery { get; set; }

        /// <summary>Length of the window in minutes, for WeeklyByDay, MonthlyByDate and MonthlyByDay.</summary>
        [Parameter]
        [ValidateRange(1, 1440)]
        public int Duration { get; set; }

        /// <summary>WeeklyByDay windows: the days the window runs.</summary>
        [Parameter]
        [Alias("week_days")]
        public DayOfWeek[] WeekDays { get; set; }

        /// <summary>MonthlyByDate windows: the day of the month.</summary>
        [Parameter]
        [Alias("monthly_start_date")]
        [ValidateRange(1, 31)]
        public int MonthlyStartDate { get; set; }

        /// <summary>MonthlyByDay windows: which week of the month.</summary>
        [Parameter]
        [Alias("start_week")]
        public WeekOfMonth StartWeek { get; set; }

        /// <summary>Monthly and yearly windows: repeat every N months.</summary>
        [Parameter]
        [Alias("start_after")]
        [ValidateRange(1, 25)]
        public int StartAfter { get; set; }

        /// <summary>Recurring windows: the first date the schedule applies.</summary>
        [Parameter]
        [Alias("maintenance_start_on")]
        public DateTime MaintenanceStartOn { get; set; }

        /// <summary>Recurring windows: 0 = never ends, 1 = ends after -MaintenanceEndAfterTimes, 2 = ends on -MaintenanceEndOn.</summary>
        [Parameter]
        [Alias("maintenance_end_type")]
        [ValidateRange(0, 2)]
        public int MaintenanceEndType { get; set; }

        [Parameter]
        [Alias("maintenance_end_after_times")]
        public int MaintenanceEndAfterTimes { get; set; }

        [Parameter]
        [Alias("maintenance_end_on")]
        public DateTime MaintenanceEndOn { get; set; }

        protected override string ResourcePath => Endpoints.Maintenance;
        protected override string ResourceLabel => "maintenance";

        protected static string Date(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        protected override void BuildBody(IDictionary body)
        {
            AddIfBound(body, "display_name", nameof(DisplayName), DisplayName);
            AddIfBound(body, "description", nameof(Description), Description);
            AddIfBound(body, "maintenance_type", nameof(MaintenanceType), MaintenanceType);
            AddIfBound(body, "start_time", nameof(StartTime), StartTime);
            AddIfBound(body, "end_time", nameof(EndTime), EndTime);
            AddIfBound(body, "start_date", nameof(StartDate), Date(StartDate));
            AddIfBound(body, "end_date", nameof(EndDate), Date(EndDate));
            AddIfBound(body, "timezone", nameof(TimeZone), TimeZone);

            if (IsBound(nameof(Monitors)))
            {
                body["selection_type"] = SelectionType.Monitor;
                body["monitors"] = Monitors;
            }
            if (IsBound(nameof(MonitorGroups)))
            {
                body["selection_type"] = SelectionType.MonitorGroup;
                body["monitor_groups"] = MonitorGroups;
            }
            if (IsBound(nameof(Tags)))
            {
                body["selection_type"] = SelectionType.Tags;
                body["tags"] = Tags;
            }

            AddIfBound(body, "subgroup_monitors", nameof(SubgroupMonitors), SubgroupMonitors);
            AddIfBound(body, "perform_monitoring", nameof(PerformMonitoring), PerformMonitoring);
            AddIfBound(body, "start_day", nameof(StartDay), (int)StartDay);
            AddIfBound(body, "end_day", nameof(EndDay), (int)EndDay);
            AddIfBound(body, "execute_every", nameof(ExecuteEvery), ExecuteEvery);
            AddIfBound(body, "duration", nameof(Duration), Duration);
            if (IsBound(nameof(WeekDays)))
            {
                body["week_days"] = WeekDays.Select(d => (int)d).Distinct().OrderBy(d => d).ToArray();
            }
            AddIfBound(body, "monthly_start_date", nameof(MonthlyStartDate), MonthlyStartDate);
            AddIfBound(body, "start_week", nameof(StartWeek), StartWeek);
            AddIfBound(body, "start_after", nameof(StartAfter), StartAfter);
            AddIfBound(body, "maintenance_start_on", nameof(MaintenanceStartOn), Date(MaintenanceStartOn));
            AddIfBound(body, "maintenance_end_type", nameof(MaintenanceEndType), MaintenanceEndType);
            AddIfBound(body, "maintenance_end_after_times", nameof(MaintenanceEndAfterTimes), MaintenanceEndAfterTimes);
            AddIfBound(body, "maintenance_end_on", nameof(MaintenanceEndOn), Date(MaintenanceEndOn));
        }
    }

    /// <summary>
    /// <para type="synopsis">Gets scheduled maintenance windows.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "Site24x7Maintenance", DefaultParameterSetName = ParameterSets.List)]
    [OutputType(typeof(PSObject))]
    public sealed class GetSite24x7MaintenanceCommand : GetResourceCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSets.ById, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [Alias("maintenance_id", "Id")]
        [ValidateNotNullOrEmpty]
        public string MaintenanceId { get; set; }

        protected override string ResourcePath => Endpoints.Maintenance;
        protected override string ResourceId => MaintenanceId;
    }

    /// <summary>
    /// <para type="synopsis">Schedules a maintenance window.</para>
    /// <para type="description">Which other parameters are required depends on -MaintenanceType: Once needs -StartDate and -EndDate; WeeklyByTime needs -StartDay and -EndDay; WeeklyByDay needs -StartDay, -EndDay and -Duration; MonthlyByDate needs -MonthlyStartDate and -Duration; MonthlyByDay needs -StartWeek, -StartDay and -Duration. Choose what it covers with -Monitors, -MonitorGroups or -Tags.</para>
    /// </summary>
    /// <example>
    /// <code>New-Site24x7Maintenance -DisplayName 'Patch night' -MaintenanceType Once -StartDate 2026-11-01 -EndDate 2026-11-01 -StartTime 22:00 -EndTime 23:30 -MonitorGroups $groupId</code>
    /// </example>
    [Cmdlet(VerbsCommon.New, "Site24x7Maintenance", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class NewSite24x7MaintenanceCommand : MaintenanceWriteCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        [Alias("display_name")]
        [ValidateNotNullOrEmpty]
        public override string DisplayName { get; set; }

        [Parameter(Mandatory = true, Position = 1)]
        [Alias("maintenance_type")]
        public override MaintenanceType MaintenanceType { get; set; }

        [Parameter(Mandatory = true)]
        [Alias("start_time")]
        [ValidatePattern(TimePattern)]
        public override string StartTime { get; set; }

        [Parameter(Mandatory = true)]
        [Alias("end_time")]
        [ValidatePattern(TimePattern)]
        public override string EndTime { get; set; }

        protected override void ProcessRecordCore()
        {
            RequireForType();
            var body = CreateBody();
            if (!body.Contains("selection_type") && (Property == null || !Property.ContainsKey("selection_type")))
            {
                throw new Site24x7Exception("Choose what the maintenance covers with -Monitors, -MonitorGroups or -Tags.");
            }
            if (ShouldProcess(Describe(body, ResourceLabel), "Create maintenance"))
            {
                WriteData(Call("POST", ResourcePath, body));
            }
        }

        private void RequireForType()
        {
            string[] required;
            switch (MaintenanceType)
            {
                case MaintenanceType.Once: required = new[] { nameof(StartDate), nameof(EndDate) }; break;
                case MaintenanceType.WeeklyByTime: required = new[] { nameof(StartDay), nameof(EndDay) }; break;
                case MaintenanceType.WeeklyByDay: required = new[] { nameof(StartDay), nameof(EndDay), nameof(Duration) }; break;
                case MaintenanceType.MonthlyByDate: required = new[] { nameof(MonthlyStartDate), nameof(Duration) }; break;
                case MaintenanceType.MonthlyByDay: required = new[] { nameof(StartWeek), nameof(StartDay), nameof(Duration) }; break;
                default: required = new string[0]; break;
            }

            var missing = required.Where(p => !IsBound(p)).ToArray();
            if (missing.Length > 0)
            {
                throw new Site24x7Exception($"A {MaintenanceType} maintenance also needs: -{string.Join(", -", missing)}.");
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Updates a scheduled maintenance window. Only the settings you pass are sent.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "Site24x7Maintenance", SupportsShouldProcess = true)]
    [Alias("Update-Site24x7Maintenance")]
    [OutputType(typeof(PSObject))]
    public sealed class SetSite24x7MaintenanceCommand : MaintenanceWriteCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("maintenance_id", "Id")]
        [ValidateNotNullOrEmpty]
        public string MaintenanceId { get; set; }

        protected override string ResourceId => MaintenanceId;

        protected override void ProcessRecordCore()
        {
            var body = CreateBody();
            if (body.Count == 0) throw new Site24x7Exception($"Nothing to update on maintenance {MaintenanceId}.");
            if (ShouldProcess(MaintenanceId, "Update maintenance"))
            {
                WriteData(Call("PUT", ItemPath, body));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Deletes a scheduled maintenance window.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "Site24x7Maintenance", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
    public sealed class RemoveSite24x7MaintenanceCommand : RemoveResourceCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [Alias("maintenance_id", "Id")]
        [ValidateNotNullOrEmpty]
        public string MaintenanceId { get; set; }

        protected override string ResourcePath => Endpoints.Maintenance;
        protected override string ResourceId => MaintenanceId;
        protected override string ResourceLabel => "maintenance";
    }

    /// <summary>Selects what Start-/Stop-Site24x7Maintenance acts on, by id or by name.</summary>
    public abstract class MaintenanceNowCmdlet : Site24x7Cmdlet
    {
        protected const string MonitorsSet = "Monitors";
        protected const string MonitorNamesSet = "MonitorNames";
        protected const string GroupsSet = "MonitorGroups";
        protected const string GroupNamesSet = "MonitorGroupNames";
        protected const string TagsSet = "Tags";
        protected const string TagNamesSet = "TagNameValues";

        [Parameter(Mandatory = true, ParameterSetName = MonitorsSet)]
        [Alias("monitor_ids")]
        [ValidateNotNullOrEmpty]
        public string[] Monitors { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = MonitorNamesSet)]
        [Alias("monitor_names")]
        [ValidateNotNullOrEmpty]
        public string[] MonitorNames { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = GroupsSet)]
        [Alias("monitor_groups")]
        [ValidateNotNullOrEmpty]
        public string[] MonitorGroups { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = GroupNamesSet)]
        [Alias("monitor_group_names")]
        [ValidateNotNullOrEmpty]
        public string[] MonitorGroupNames { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = TagsSet)]
        [ValidateNotNullOrEmpty]
        public string[] Tags { get; set; }

        /// <summary>Tags as "name:value" strings.</summary>
        [Parameter(Mandatory = true, ParameterSetName = TagNamesSet)]
        [Alias("tag_name_values")]
        [ValidateNotNullOrEmpty]
        public string[] TagNameValues { get; set; }

        protected bool ByName => ParameterSetName == MonitorNamesSet || ParameterSetName == GroupNamesSet || ParameterSetName == TagNamesSet;

        protected void AddSelection(IDictionary body)
        {
            switch (ParameterSetName)
            {
                case MonitorsSet: body["selection_type"] = SelectionType.Monitor; body["monitors"] = Monitors; break;
                case MonitorNamesSet: body["selection_type"] = SelectionType.Monitor; body["monitor_names"] = MonitorNames; break;
                case GroupsSet: body["selection_type"] = SelectionType.MonitorGroup; body["monitor_groups"] = MonitorGroups; break;
                case GroupNamesSet: body["selection_type"] = SelectionType.MonitorGroup; body["monitor_group_names"] = MonitorGroupNames; break;
                case TagsSet: body["selection_type"] = SelectionType.Tags; body["tags"] = Tags; break;
                case TagNamesSet: body["selection_type"] = SelectionType.Tags; body["tag_name_values"] = TagNameValues; break;
            }
        }

        protected string Target
        {
            get
            {
                var items = Monitors ?? MonitorNames ?? MonitorGroups ?? MonitorGroupNames ?? Tags ?? TagNameValues;
                return items == null ? null : string.Join(", ", items);
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Puts monitors, monitor groups or tagged monitors into maintenance now.</para>
    /// </summary>
    /// <example>
    /// <code>Start-Site24x7Maintenance -MonitorNames 'web01','web02' -Duration 60 -PerformMonitoring</code>
    /// </example>
    [Cmdlet(VerbsLifecycle.Start, "Site24x7Maintenance", SupportsShouldProcess = true, DefaultParameterSetName = MonitorsSet)]
    [OutputType(typeof(PSObject))]
    public sealed class StartSite24x7MaintenanceCommand : MaintenanceNowCmdlet
    {
        /// <summary>How long the maintenance lasts, in minutes.</summary>
        [Parameter(Mandatory = true)]
        [ValidateRange(1, int.MaxValue)]
        public int Duration { get; set; }

        [Parameter]
        [Alias("perform_monitoring")]
        public SwitchParameter PerformMonitoring { get; set; }

        /// <summary>With monitor groups, also include monitors in subgroups.</summary>
        [Parameter(ParameterSetName = GroupsSet)]
        [Parameter(ParameterSetName = GroupNamesSet)]
        [Alias("subgroup_monitors")]
        public SwitchParameter SubgroupMonitors { get; set; }

        protected override object ErrorTarget => Target;

        protected override void ProcessRecordCore()
        {
            var body = NewBody();
            body["duration"] = Duration;
            AddSelection(body);
            AddIfBound(body, "perform_monitoring", nameof(PerformMonitoring), PerformMonitoring);
            AddIfBound(body, "subgroup_monitors", nameof(SubgroupMonitors), SubgroupMonitors);

            if (ShouldProcess(Target, "Start maintenance"))
            {
                WriteData(Call("POST", ByName ? Endpoints.StartMaintenanceByName : Endpoints.StartMaintenance, body));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Ends maintenance now, for a maintenance window or for monitors, monitor groups or tags.</para>
    /// </summary>
    [Cmdlet(VerbsLifecycle.Stop, "Site24x7Maintenance", SupportsShouldProcess = true, DefaultParameterSetName = MaintenanceSet)]
    [OutputType(typeof(PSObject))]
    public sealed class StopSite24x7MaintenanceCommand : MaintenanceNowCmdlet
    {
        private const string MaintenanceSet = "MaintenanceId";

        [Parameter(Mandatory = true, Position = 0, ParameterSetName = MaintenanceSet, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [Alias("maintenance_id")]
        [ValidateNotNullOrEmpty]
        public string MaintenanceId { get; set; }

        protected override object ErrorTarget => MaintenanceId ?? Target;

        protected override void ProcessRecordCore()
        {
            var body = NewBody();
            if (ParameterSetName == MaintenanceSet)
            {
                body["maintenance_id"] = MaintenanceId;
            }
            else
            {
                AddSelection(body);
            }

            if (ShouldProcess(MaintenanceId ?? Target, "End maintenance"))
            {
                WriteData(Call("POST", ByName ? Endpoints.EndMaintenanceByName : Endpoints.EndMaintenance, body));
            }
        }
    }
}
