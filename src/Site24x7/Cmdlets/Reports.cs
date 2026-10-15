using System;
using System.Collections;
using System.Management.Automation;

namespace Site24x7.Cmdlets
{
    // https://www.site24x7.com/help/api/#current-status

    /// <summary>
    /// <para type="synopsis">Gets the current up, down or trouble status of monitors.</para>
    /// <para type="description">Without a scope, returns the status of every monitor and monitor group (see the monitors and monitor_groups properties of the result).</para>
    /// </summary>
    /// <example>
    /// <code>(Get-Site24x7CurrentStatus).monitors | Where-Object status -eq 0</code>
    /// </example>
    [Cmdlet(VerbsCommon.Get, "Site24x7CurrentStatus", DefaultParameterSetName = AllSet)]
    [OutputType(typeof(PSObject))]
    public sealed class GetSite24x7CurrentStatusCommand : Site24x7Cmdlet
    {
        private const string AllSet = "All";
        private const string MonitorSet = "Monitor";
        private const string GroupSet = "Group";
        private const string TypeSet = "Type";

        /// <summary>One monitor's status.</summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = MonitorSet, ValueFromPipelineByPropertyName = true)]
        [Alias("monitor_id")]
        [ValidateNotNullOrEmpty]
        public string MonitorId { get; set; }

        /// <summary>The status of one monitor group's monitors.</summary>
        [Parameter(Mandatory = true, ParameterSetName = GroupSet, ValueFromPipelineByPropertyName = true)]
        [Alias("group_id")]
        [ValidateNotNullOrEmpty]
        public string GroupId { get; set; }

        /// <summary>The status of every monitor of a type, such as URL or SERVER.</summary>
        [Parameter(Mandatory = true, ParameterSetName = TypeSet)]
        [ValidateNotNullOrEmpty]
        public string MonitorType { get; set; }

        /// <summary>Include APM Insight monitors.</summary>
        [Parameter]
        public SwitchParameter IncludeApm { get; set; }

        /// <summary>Include monitor group status.</summary>
        [Parameter]
        public SwitchParameter IncludeGroups { get; set; }

        /// <summary>Include per-location status.</summary>
        [Parameter]
        public SwitchParameter IncludeLocations { get; set; }

        /// <summary>Include suspended monitors.</summary>
        [Parameter]
        public SwitchParameter IncludeSuspended { get; set; }

        protected override object ErrorTarget => MonitorId ?? GroupId ?? MonitorType;

        protected override void ProcessRecordCore()
        {
            string path;
            switch (ParameterSetName)
            {
                case MonitorSet: path = Endpoints.CurrentStatus + "/" + Seg(MonitorId); break;
                case GroupSet: path = Endpoints.CurrentStatus + "/group/" + Seg(GroupId); break;
                case TypeSet: path = Endpoints.CurrentStatus + "/type/" + Seg(MonitorType); break;
                default: path = Endpoints.CurrentStatus; break;
            }

            var query = NewQuery();
            if (IsBound(nameof(IncludeApm))) query["apm_required"] = IncludeApm.IsPresent;
            if (IsBound(nameof(IncludeGroups))) query["group_required"] = IncludeGroups.IsPresent;
            if (IsBound(nameof(IncludeLocations))) query["locations_required"] = IncludeLocations.IsPresent;
            if (IsBound(nameof(IncludeSuspended))) query["suspended_required"] = IncludeSuspended.IsPresent;

            WriteData(Call("GET", path, query: query));
        }
    }

    // https://www.site24x7.com/help/api/#reports

    /// <summary>
    /// <para type="synopsis">Gets a monitor, monitor group or monitor type report.</para>
    /// <para type="description">Choose the report with -Report and the period with -Period (or -StartDate and -EndDate). Report-specific options, such as granularity or locations for Performance, go in -Query.</para>
    /// </summary>
    /// <example>
    /// <code>Get-Site24x7Report AvailabilitySummary $monitorId -Period Last30Days</code>
    /// </example>
    [Cmdlet(VerbsCommon.Get, "Site24x7Report", DefaultParameterSetName = MonitorSet)]
    [OutputType(typeof(PSObject))]
    public sealed class GetSite24x7ReportCommand : Site24x7Cmdlet
    {
        private const string MonitorSet = "Monitor";
        private const string GroupSet = "Group";
        private const string TypeSet = "Type";

        /// <summary>Which report.</summary>
        [Parameter(Mandatory = true, Position = 0)]
        public ReportType Report { get; set; }

        /// <summary>A report for one monitor.</summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = MonitorSet, ValueFromPipelineByPropertyName = true)]
        [Alias("monitor_id")]
        [ValidateNotNullOrEmpty]
        public string MonitorId { get; set; }

        /// <summary>A report for one monitor group.</summary>
        [Parameter(Mandatory = true, ParameterSetName = GroupSet, ValueFromPipelineByPropertyName = true)]
        [Alias("group_id")]
        [ValidateNotNullOrEmpty]
        public string GroupId { get; set; }

        /// <summary>A report for every monitor of a type, such as URL.</summary>
        [Parameter(Mandatory = true, ParameterSetName = TypeSet)]
        [ValidateNotNullOrEmpty]
        public string MonitorType { get; set; }

        /// <summary>The period to report on. Defaults to the last 24 hours.</summary>
        [Parameter]
        public ReportPeriod Period { get; set; } = ReportPeriod.Last24Hours;

        /// <summary>Start of a custom period. Sets -Period to Custom.</summary>
        [Parameter]
        public DateTime StartDate { get; set; }

        /// <summary>End of a custom period. Sets -Period to Custom.</summary>
        [Parameter]
        public DateTime EndDate { get; set; }

        /// <summary>Other query string options for the report. They override the values the cmdlet sets.</summary>
        [Parameter]
        public Hashtable Query { get; set; }

        protected override object ErrorTarget => MonitorId ?? GroupId ?? MonitorType;

        protected override void BeginProcessingCore()
        {
            if (IsBound(nameof(StartDate)) && IsBound(nameof(EndDate)) && EndDate < StartDate)
            {
                throw new Site24x7ArgumentException("-EndDate is before -StartDate.");
            }
        }

        protected override void ProcessRecordCore()
        {
            string scope;
            switch (ParameterSetName)
            {
                case GroupSet: scope = "group/" + Seg(GroupId); break;
                case TypeSet: scope = "monitor_type/" + Seg(MonitorType); break;
                default: scope = Seg(MonitorId); break;
            }

            var query = NewQuery();
            bool custom = IsBound(nameof(StartDate)) || IsBound(nameof(EndDate));
            query["period"] = custom ? ReportPeriod.Custom : Period;
            if (IsBound(nameof(StartDate))) query["start_date"] = ApiDate(StartDate);
            if (IsBound(nameof(EndDate))) query["end_date"] = ApiDate(EndDate);
            MergeQuery(query, Query);

            WriteData(Call("GET", $"{Endpoints.Reports}/{ApiCodes.Of(Report)}/{scope}", query: query));
        }
    }

    /// <summary>
    /// <para type="synopsis">Gets the poll-by-poll log report of a monitor for one day.</para>
    /// </summary>
    /// <example>
    /// <code>Get-Site24x7LogReport $monitorId -Date (Get-Date).AddDays(-1)</code>
    /// </example>
    [Cmdlet(VerbsCommon.Get, "Site24x7LogReport")]
    [OutputType(typeof(PSObject))]
    public sealed class GetSite24x7LogReportCommand : Site24x7Cmdlet
    {
        /// <summary>The monitor's id.</summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("monitor_id")]
        [ValidateNotNullOrEmpty]
        public string MonitorId { get; set; }

        /// <summary>The day to report on. Defaults to today.</summary>
        [Parameter(Position = 1)]
        public DateTime Date { get; set; } = DateTime.Today;

        /// <summary>Other query string options. They override the values the cmdlet sets.</summary>
        [Parameter]
        public Hashtable Query { get; set; }

        protected override object ErrorTarget => MonitorId;

        protected override void ProcessRecordCore()
        {
            var query = NewQuery();
            query["date"] = ApiDate(Date);
            MergeQuery(query, Query);
            WriteData(Call("GET", Endpoints.LogReports + "/" + Seg(MonitorId), query: query));
        }
    }
}
