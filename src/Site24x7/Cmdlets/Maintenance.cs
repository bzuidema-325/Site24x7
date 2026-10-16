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
        /// <summary>The maintenance window's name.</summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public virtual string DisplayName { get; set; }

        /// <summary>A description of the window.</summary>
        [Parameter]
        public string Description { get; set; }

        /// <summary>How the window repeats. This decides which other settings are needed; see the description.</summary>
        [Parameter]
        public virtual MaintenanceType MaintenanceType { get; set; }

        /// <summary>Daily start time, as hh:mm.</summary>
        [Parameter]
        [ValidatePattern(TimeOfDayPattern)]
        public virtual string StartTime { get; set; }

        /// <summary>Daily end time, as hh:mm.</summary>
        [Parameter]
        [ValidatePattern(TimeOfDayPattern)]
        public virtual string EndTime { get; set; }

        /// <summary>Start date for a Once window.</summary>
        [Parameter]
        public DateTime StartDate { get; set; }

        /// <summary>End date for a Once window.</summary>
        [Parameter]
        public DateTime EndDate { get; set; }

        /// <summary>Time zone id, for example "America/New_York". Defaults to the account's time zone.</summary>
        [Parameter]
        public string TimeZone { get; set; }

        /// <summary>The monitors the window applies to.</summary>
        [Parameter]
        public string[] MonitorId { get; set; }

        /// <summary>The monitor groups the window applies to.</summary>
        [Parameter]
        public string[] MonitorGroupId { get; set; }

        /// <summary>The tags whose monitors the window applies to.</summary>
        [Parameter]
        public string[] TagId { get; set; }

        /// <summary>With -MonitorGroupId, also include monitors in subgroups.</summary>
        [Parameter]
        public SwitchParameter SubgroupMonitors { get; set; }

        /// <summary>Keep polling during the window (alerts are still suppressed).</summary>
        [Parameter]
        public SwitchParameter PerformMonitoring { get; set; }

        /// <summary>Weekly windows: the day the window starts.</summary>
        [Parameter]
        public DayOfWeek StartDay { get; set; }

        /// <summary>Weekly windows: the day the window ends.</summary>
        [Parameter]
        public DayOfWeek EndDay { get; set; }

        /// <summary>Weekly windows: repeat every 1 to 4 weeks.</summary>
        [Parameter]
        [ValidateRange(1, 4)]
        public int ExecuteEvery { get; set; }

        /// <summary>Length of the window in minutes, for WeeklyByDay, MonthlyByDate and MonthlyByDay.</summary>
        [Parameter]
        [ValidateRange(1, 1440)]
        public int Duration { get; set; }

        /// <summary>WeeklyByDay windows: the days the window runs.</summary>
        [Parameter]
        public DayOfWeek[] WeekDay { get; set; }

        /// <summary>MonthlyByDate windows: the day of the month.</summary>
        [Parameter]
        [ValidateRange(1, 31)]
        public int MonthlyStartDate { get; set; }

        /// <summary>MonthlyByDay windows: which week of the month.</summary>
        [Parameter]
        public WeekOfMonth StartWeek { get; set; }

        /// <summary>Monthly and yearly windows: repeat every N months.</summary>
        [Parameter]
        [ValidateRange(1, 25)]
        public int StartAfter { get; set; }

        /// <summary>Recurring windows: the first date the schedule applies.</summary>
        [Parameter]
        public DateTime MaintenanceStartOn { get; set; }

        /// <summary>Recurring windows: 0 = never ends, 1 = ends after -MaintenanceEndAfterTimes, 2 = ends on -MaintenanceEndOn.</summary>
        [Parameter]
        [ValidateRange(0, 2)]
        public int MaintenanceEndType { get; set; }

        /// <summary>Recurring windows: how many times the window runs before it ends.</summary>
        [Parameter]
        [ValidateRange(1, int.MaxValue)]
        public int MaintenanceEndAfterTimes { get; set; }

        /// <summary>Recurring windows: the date the schedule ends.</summary>
        [Parameter]
        public DateTime MaintenanceEndOn { get; set; }

        private protected override ResourceInfo Resource => Resources.Maintenance;

        protected override void BuildBody(IDictionary body)
        {
            AddIfBound(body, "display_name", nameof(DisplayName), DisplayName);
            AddIfBound(body, "description", nameof(Description), Description);
            AddIfBound(body, "maintenance_type", nameof(MaintenanceType), MaintenanceType);
            AddIfBound(body, "start_time", nameof(StartTime), StartTime);
            AddIfBound(body, "end_time", nameof(EndTime), EndTime);
            AddIfBound(body, "start_date", nameof(StartDate), ApiDate(StartDate));
            AddIfBound(body, "end_date", nameof(EndDate), ApiDate(EndDate));
            AddIfBound(body, "timezone", nameof(TimeZone), TimeZone);

            if (IsBound(nameof(MonitorId)))
            {
                body["selection_type"] = SelectionType.Monitor;
                body["monitors"] = MonitorId;
            }
            if (IsBound(nameof(MonitorGroupId)))
            {
                body["selection_type"] = SelectionType.MonitorGroup;
                body["monitor_groups"] = MonitorGroupId;
            }
            if (IsBound(nameof(TagId)))
            {
                body["selection_type"] = SelectionType.Tags;
                body["tags"] = TagId;
            }

            AddIfBound(body, "subgroup_monitors", nameof(SubgroupMonitors), SubgroupMonitors);
            AddIfBound(body, "perform_monitoring", nameof(PerformMonitoring), PerformMonitoring);
            AddIfBound(body, "start_day", nameof(StartDay), (int)StartDay);
            AddIfBound(body, "end_day", nameof(EndDay), (int)EndDay);
            AddIfBound(body, "execute_every", nameof(ExecuteEvery), ExecuteEvery);
            AddIfBound(body, "duration", nameof(Duration), Duration);
            if (IsBound(nameof(WeekDay)))
            {
                body["week_days"] = WeekDay.Select(d => (int)d).Distinct().OrderBy(d => d).ToArray();
            }
            AddIfBound(body, "monthly_start_date", nameof(MonthlyStartDate), MonthlyStartDate);
            AddIfBound(body, "start_week", nameof(StartWeek), StartWeek);
            AddIfBound(body, "start_after", nameof(StartAfter), StartAfter);
            AddIfBound(body, "maintenance_start_on", nameof(MaintenanceStartOn), ApiDate(MaintenanceStartOn));
            AddIfBound(body, "maintenance_end_type", nameof(MaintenanceEndType), MaintenanceEndType);
            AddIfBound(body, "maintenance_end_after_times", nameof(MaintenanceEndAfterTimes), MaintenanceEndAfterTimes);
            AddIfBound(body, "maintenance_end_on", nameof(MaintenanceEndOn), ApiDate(MaintenanceEndOn));
        }

        protected override void ValidateBody(IDictionary body)
        {
            base.ValidateBody(body);
            int selections = new[] { nameof(MonitorId), nameof(MonitorGroupId), nameof(TagId) }.Count(IsBound);
            if (selections > 1)
            {
                throw new Site24x7ArgumentException("Choose one of -MonitorId, -MonitorGroupId or -TagId.");
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Schedules a maintenance window.</para>
    /// <para type="description">Which other parameters are required depends on -MaintenanceType: Once needs -StartDate and -EndDate; WeeklyByTime needs -StartDay and -EndDay; WeeklyByDay needs -StartDay, -EndDay and -Duration; MonthlyByDate needs -MonthlyStartDate and -Duration; MonthlyByDay needs -StartWeek, -StartDay and -Duration. Choose what it covers with -MonitorId, -MonitorGroupId or -TagId.</para>
    /// </summary>
    /// <example>
    /// <code>New-Site24x7Maintenance 'Patch night' Once -StartDate 2026-11-01 -EndDate 2026-11-01 -StartTime 22:00 -EndTime 23:30 -MonitorGroupId $groupId</code>
    /// </example>
    [Cmdlet(VerbsCommon.New, "Site24x7Maintenance", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class NewSite24x7MaintenanceCommand : MaintenanceWriteCmdlet
    {
        /// <summary>The maintenance window's name.</summary>
        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        public override string DisplayName { get; set; }

        /// <summary>How the window repeats. This decides which other settings are needed; see the description.</summary>
        [Parameter(Mandatory = true, Position = 1)]
        public override MaintenanceType MaintenanceType { get; set; }

        /// <summary>Daily start time, as hh:mm.</summary>
        [Parameter(Mandatory = true)]
        [ValidatePattern(TimeOfDayPattern)]
        public override string StartTime { get; set; }

        /// <summary>Daily end time, as hh:mm.</summary>
        [Parameter(Mandatory = true)]
        [ValidatePattern(TimeOfDayPattern)]
        public override string EndTime { get; set; }

        protected override void ValidateBody(IDictionary body)
        {
            base.ValidateBody(body);

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
                throw new Site24x7ArgumentException($"A {MaintenanceType} maintenance also needs: -{string.Join(", -", missing)}.");
            }
            if (MaintenanceType == MaintenanceType.Once &&
                EndDate.Date + TimeSpan.Parse(EndTime, CultureInfo.InvariantCulture) <= StartDate.Date + TimeSpan.Parse(StartTime, CultureInfo.InvariantCulture))
            {
                throw new Site24x7ArgumentException("The maintenance ends before it starts. Check -StartDate, -StartTime, -EndDate and -EndTime.");
            }
            if (!HasField(body, "selection_type"))
            {
                throw new Site24x7ArgumentException("Choose what the maintenance covers with -MonitorId, -MonitorGroupId or -TagId.");
            }
            if (IsBound(nameof(SubgroupMonitors)) && !IsBound(nameof(MonitorGroupId)))
            {
                throw new Site24x7ArgumentException("-SubgroupMonitors only applies with -MonitorGroupId.");
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Updates a scheduled maintenance window. Only the settings you pass are sent.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "Site24x7Maintenance", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class SetSite24x7MaintenanceCommand : MaintenanceWriteCmdlet
    {
        /// <summary>The maintenance window's id.</summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("maintenance_id")]
        [ValidateNotNullOrEmpty]
        public string MaintenanceId { get; set; }

        protected override string ResourceId => MaintenanceId;
    }

    /// <summary>Selects what Start-/Stop-Site24x7Maintenance acts on, by id or by name.</summary>
    public abstract class MaintenanceNowCmdlet : Site24x7Cmdlet
    {
        protected const string MonitorIdSet = "MonitorId";
        protected const string MonitorNameSet = "MonitorName";
        protected const string GroupIdSet = "MonitorGroupId";
        protected const string GroupNameSet = "MonitorGroupName";
        protected const string TagIdSet = "TagId";
        protected const string TagNameSet = "TagNameValue";

        /// <summary>Monitors, by id. Monitors from Get-Site24x7Monitor can be piped in.</summary>
        [Parameter(Mandatory = true, ParameterSetName = MonitorIdSet, ValueFromPipelineByPropertyName = true)]
        [Alias("monitor_id")]
        [ValidateNotNullOrEmpty]
        public string[] MonitorId { get; set; }

        /// <summary>Monitors, by name.</summary>
        [Parameter(Mandatory = true, ParameterSetName = MonitorNameSet)]
        [ValidateNotNullOrEmpty]
        public string[] MonitorName { get; set; }

        /// <summary>Monitor groups, by id. Groups from Get-Site24x7MonitorGroup can be piped in.</summary>
        [Parameter(Mandatory = true, ParameterSetName = GroupIdSet, ValueFromPipelineByPropertyName = true)]
        [Alias("group_id")]
        [ValidateNotNullOrEmpty]
        public string[] MonitorGroupId { get; set; }

        /// <summary>Monitor groups, by name.</summary>
        [Parameter(Mandatory = true, ParameterSetName = GroupNameSet)]
        [ValidateNotNullOrEmpty]
        public string[] MonitorGroupName { get; set; }

        /// <summary>Tags, by id. Tags from Get-Site24x7Tag can be piped in.</summary>
        [Parameter(Mandatory = true, ParameterSetName = TagIdSet, ValueFromPipelineByPropertyName = true)]
        [Alias("tag_id")]
        [ValidateNotNullOrEmpty]
        public string[] TagId { get; set; }

        /// <summary>Tags, as "name:value" strings.</summary>
        [Parameter(Mandatory = true, ParameterSetName = TagNameSet)]
        [ValidateNotNullOrEmpty]
        public string[] TagNameValue { get; set; }

        protected bool ByName => ParameterSetName == MonitorNameSet || ParameterSetName == GroupNameSet || ParameterSetName == TagNameSet;

        protected void AddSelection(IDictionary body)
        {
            switch (ParameterSetName)
            {
                case MonitorIdSet: body["selection_type"] = SelectionType.Monitor; body["monitors"] = MonitorId; break;
                case MonitorNameSet: body["selection_type"] = SelectionType.Monitor; body["monitor_names"] = MonitorName; break;
                case GroupIdSet: body["selection_type"] = SelectionType.MonitorGroup; body["monitor_groups"] = MonitorGroupId; break;
                case GroupNameSet: body["selection_type"] = SelectionType.MonitorGroup; body["monitor_group_names"] = MonitorGroupName; break;
                case TagIdSet: body["selection_type"] = SelectionType.Tags; body["tags"] = TagId; break;
                case TagNameSet: body["selection_type"] = SelectionType.Tags; body["tag_name_values"] = TagNameValue; break;
            }
        }

        protected string Target
        {
            get
            {
                var items = MonitorId ?? MonitorName ?? MonitorGroupId ?? MonitorGroupName ?? TagId ?? TagNameValue;
                return items == null ? null : string.Join(", ", items);
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Puts monitors, monitor groups or tagged monitors into maintenance now.</para>
    /// <para type="description">Selecting by name (-MonitorName, -MonitorGroupName or -TagNameValue) calls an API path that has not been checked against a live Site24x7 account yet.</para>
    /// </summary>
    /// <example>
    /// <code>Start-Site24x7Maintenance -MonitorName web01, web02 -Duration 60 -PerformMonitoring</code>
    /// </example>
    /// <example>
    /// <code>Get-Site24x7Monitor -Name 'web-*' | Start-Site24x7Maintenance -Duration 30</code>
    /// </example>
    [Cmdlet(VerbsLifecycle.Start, "Site24x7Maintenance", SupportsShouldProcess = true, DefaultParameterSetName = MonitorIdSet)]
    [OutputType(typeof(PSObject))]
    public sealed class StartSite24x7MaintenanceCommand : MaintenanceNowCmdlet
    {
        /// <summary>How long the maintenance lasts, in minutes.</summary>
        [Parameter(Mandatory = true)]
        [ValidateRange(1, int.MaxValue)]
        public int Duration { get; set; }

        /// <summary>Keep polling during the maintenance (alerts are still suppressed).</summary>
        [Parameter]
        public SwitchParameter PerformMonitoring { get; set; }

        /// <summary>With monitor groups, also include monitors in subgroups.</summary>
        [Parameter(ParameterSetName = GroupIdSet)]
        [Parameter(ParameterSetName = GroupNameSet)]
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
    /// <para type="description">Selecting by name (-MonitorName, -MonitorGroupName or -TagNameValue) calls an API path that has not been checked against a live Site24x7 account yet.</para>
    /// </summary>
    [Cmdlet(VerbsLifecycle.Stop, "Site24x7Maintenance", SupportsShouldProcess = true, DefaultParameterSetName = MaintenanceSet)]
    [OutputType(typeof(PSObject))]
    public sealed class StopSite24x7MaintenanceCommand : MaintenanceNowCmdlet
    {
        private const string MaintenanceSet = "MaintenanceId";

        /// <summary>The maintenance window to end.</summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = MaintenanceSet, ValueFromPipelineByPropertyName = true)]
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
