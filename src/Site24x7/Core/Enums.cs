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
}
