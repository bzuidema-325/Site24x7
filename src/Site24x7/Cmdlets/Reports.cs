using System;
using System.Collections;
using System.Globalization;
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
    [Cmdlet(VerbsCommon.Get, "Site24x7CurrentStatus", DefaultParameterSetName = "All")]
    [OutputType(typeof(PSObject))]
    public sealed class GetSite24x7CurrentStatusCommand : Site24x7Cmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Monitor", ValueFromPipelineByPropertyName = true)]
        [Alias("monitor_id")]
        [ValidateNotNullOrEmpty]
        public string MonitorId { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "Group", ValueFromPipelineByPropertyName = true)]
        [Alias("group_id")]
        [ValidateNotNullOrEmpty]
        public string GroupId { get; set; }

        /// <summary>A monitor type code such as URL or SERVER.</summary>
        [Parameter(Mandatory = true, ParameterSetName = "Type")]
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
                case "Monitor": path = Endpoints.CurrentStatus + "/" + Seg(MonitorId); break;
                case "Group": path = Endpoints.CurrentStatus + "/group/" + Seg(GroupId); break;
                case "Type": path = Endpoints.CurrentStatus + "/type/" + Seg(MonitorType); break;
                default: path = Endpoints.CurrentStatus; break;
            }

            var query = new Hashtable();
            if (IsBound(nameof(IncludeApm))) query["apm_required"] = IncludeApm.IsPresent;
            if (IsBound(nameof(IncludeGroups))) query["group_required"] = IncludeGroups.IsPresent;
            if (IsBound(nameof(IncludeLocations))) query["locations_required"] = IncludeLocations.IsPresent;
            if (IsBound(nameof(IncludeSuspended))) query["suspended_required"] = IncludeSuspended.IsPresent;

            WriteData(Call("GET", path, query: query));
        }
    }

    /// <summary>The report areas under /reports.</summary>
    public enum ReportType
    {
        Summary,
        AvailabilitySummary,
        Performance,
        Outage,
        BusyHours,
        Trend,
    }

    // https://www.site24x7.com/help/api/#reports

    /// <summary>
    /// <para type="synopsis">Gets a monitor, monitor group or monitor type report.</para>
    /// <para type="description">Choose the report with -Report and the period with -Period (or -StartDate and -EndDate). Report-specific options, such as granularity or locations for Performance, go in -Query.</para>
    /// </summary>
    /// <example>
    /// <code>Get-Site24x7Report -Report AvailabilitySummary -MonitorId $id -Period Last30Days</code>
    /// </example>
    [Cmdlet(VerbsCommon.Get, "Site24x7Report", DefaultParameterSetName = "Monitor")]
    [OutputType(typeof(PSObject))]
    public sealed class GetSite24x7ReportCommand : Site24x7Cmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        public ReportType Report { get; set; }

        [Parameter(Mandatory = true, Position = 1, ParameterSetName = "Monitor", ValueFromPipelineByPropertyName = true)]
        [Alias("monitor_id")]
        [ValidateNotNullOrEmpty]
        public string MonitorId { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "Group", ValueFromPipelineByPropertyName = true)]
        [Alias("group_id")]
        [ValidateNotNullOrEmpty]
        public string GroupId { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "Type")]
        [ValidateNotNullOrEmpty]
        public string MonitorType { get; set; }

        [Parameter]
        public ReportPeriod Period { get; set; } = ReportPeriod.Last24Hours;

        /// <summary>Start of a custom period. Sets -Period to Custom.</summary>
        [Parameter]
        public DateTime StartDate { get; set; }

        /// <summary>End of a custom period. Sets -Period to Custom.</summary>
        [Parameter]
        public DateTime EndDate { get; set; }

        /// <summary>Other query string options for the report.</summary>
        [Parameter]
        public Hashtable Query { get; set; }

        protected override object ErrorTarget => MonitorId ?? GroupId ?? MonitorType;

        protected override void ProcessRecordCore()
        {
            string scope;
            switch (ParameterSetName)
            {
                case "Group": scope = "group/" + Seg(GroupId); break;
                case "Type": scope = "monitor_type/" + Seg(MonitorType); break;
                default: scope = Seg(MonitorId); break;
            }

            var query = new Hashtable();
            bool custom = IsBound(nameof(StartDate)) || IsBound(nameof(EndDate));
            query["period"] = custom ? ReportPeriod.Custom : Period;
            if (IsBound(nameof(StartDate))) query["start_date"] = StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (IsBound(nameof(EndDate))) query["end_date"] = EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (Query != null)
            {
                foreach (DictionaryEntry e in Query) query[e.Key] = e.Value;
            }

            WriteData(Call("GET", $"{Endpoints.Reports}/{Segment(Report)}/{scope}", query: query));
        }

        private static string Segment(ReportType report)
        {
            switch (report)
            {
                case ReportType.AvailabilitySummary: return "availability_summary";
                case ReportType.Performance: return "performance";
                case ReportType.Outage: return "outage";
                case ReportType.BusyHours: return "busy_hours";
                case ReportType.Trend: return "trend";
                default: return "summary";
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Gets the poll-by-poll log report of a monitor for one day.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "Site24x7LogReport")]
    [OutputType(typeof(PSObject))]
    public sealed class GetSite24x7LogReportCommand : Site24x7Cmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [Alias("monitor_id")]
        [ValidateNotNullOrEmpty]
        public string MonitorId { get; set; }

        /// <summary>The day to report on. Defaults to today.</summary>
        [Parameter(Position = 1)]
        public DateTime Date { get; set; } = DateTime.Today;

        [Parameter]
        public Hashtable Query { get; set; }

        protected override object ErrorTarget => MonitorId;

        protected override void ProcessRecordCore()
        {
            var query = new Hashtable { { "date", Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) } };
            if (Query != null)
            {
                foreach (DictionaryEntry e in Query) query[e.Key] = e.Value;
            }
            WriteData(Call("GET", Endpoints.LogReports + "/" + Seg(MonitorId), query: query));
        }
    }
}
