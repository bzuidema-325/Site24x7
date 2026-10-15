namespace Site24x7
{
    // Constants from https://www.site24x7.com/help/api/#constants.
    // Enums are sent to the API as their numeric value, and PowerShell accepts either the name or the number.

    public enum UserRole
    {
        NoAccess = 0,
        SuperAdministrator = 1,
        Administrator = 2,
        Operator = 3,
        BillingContact = 4,
        SpokesPerson = 5,
        HostingProvider = 6,
        ReadOnly = 10,
    }

    public enum CloudSpendRole
    {
        NoAccess = 0,
        CostAdministrator = 11,
        CostUser = 12,
    }

    public enum StatusIQRole
    {
        NoAccess = 0,
        StatusIQSuperAdministrator = 21,
        StatusIQAdministrator = 22,
        StatusIQSpokesPerson = 23,
        StatusIQBillingContact = 24,
        StatusIQReadOnly = 25,
    }

    public enum NotifyMedium
    {
        Email = 1,
        SMS = 2,
        VoiceCall = 3,
        IM = 4,
        Twitter = 5,
    }

    public enum AlertEmailFormat
    {
        Text = 0,
        HTML = 1,
    }

    public enum JobTitle
    {
        ITEngineer = 1,
        CloudEngineer = 2,
        DevOpsEngineer = 3,
        Webmaster = 4,
        CEO_CTO = 5,
        InternalIT = 6,
        Others = 7,
    }

    public enum SmsProvider
    {
        Clickatell = 1,
        BulkSMS = 2,
        Twilio = 4,
    }

    public enum CallProvider
    {
        Twilio = 1,
    }

    public enum MonitorStatus
    {
        Down = 0,
        Up = 1,
        Trouble = 2,
        Suspended = 5,
        Maintenance = 7,
        Discovery = 9,
        DiscoveryError = 10,
    }

    public enum AlertingStatus
    {
        Down = 10,
        Trouble = 20,
        Up = 30,
    }

    /// <summary>Which resources a maintenance window, report or action applies to.</summary>
    public enum SelectionType
    {
        All = 0,
        MonitorGroup = 1,
        Monitor = 2,
        Tags = 3,
        MonitorType = 4,
    }

    public enum MaintenanceType
    {
        Daily = 1,
        WeeklyByTime = 2,
        Once = 3,
        MonthlyByDate = 5,
        MonthlyByDay = 6,
        WeeklyByDay = 8,
        Yearly = 9,
    }

    public enum WeekOfMonth
    {
        First = 1,
        Second = 2,
        Third = 3,
        Fourth = 4,
        Last = 5,
    }

    /// <summary>Report periods. Use Custom together with -StartDate and -EndDate.</summary>
    public enum ReportPeriod
    {
        Last1Hour = 0,
        Last24Hours = 1,
        Last7Days = 2,
        Last30Days = 3,
        Last1Year = 4,
        Custom = 50,
    }

    /// <summary>HTTP methods for Website and REST API monitors.</summary>
    public enum MonitorHttpMethod
    {
        Get,
        Post,
        Head,
        Put,
        Patch,
        Delete,
    }

    /// <summary>HTTP methods a webhook integration can use.</summary>
    public enum WebhookMethod
    {
        Get,
        Post,
        Put,
        Delete,
    }

    /// <summary>DNS record types, by their DNS type number.</summary>
    public enum DnsLookupType
    {
        A = 1,
        NS = 2,
        CNAME = 5,
        SOA = 6,
        PTR = 12,
        MX = 15,
        TXT = 16,
        AAAA = 28,
        SRV = 33,
        DS = 43,
        DNSKEY = 48,
        CAA = 257,
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

    /// <summary>How enum values are spelled in the API where it does not use numbers.</summary>
    internal static class ApiCodes
    {
        public static string Of(MonitorHttpMethod method)
        {
            switch (method)
            {
                case MonitorHttpMethod.Post: return "P";
                case MonitorHttpMethod.Head: return "H";
                case MonitorHttpMethod.Put: return "PU";
                case MonitorHttpMethod.Patch: return "PA";
                case MonitorHttpMethod.Delete: return "D";
                default: return "G";
            }
        }

        public static string Of(WebhookMethod method)
        {
            switch (method)
            {
                case WebhookMethod.Get: return "G";
                case WebhookMethod.Put: return "PU";
                case WebhookMethod.Delete: return "D";
                default: return "P";
            }
        }

        public static string Of(ReportType report)
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
}
