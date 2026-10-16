namespace Site24x7
{
    /// <summary>
    /// Every API path the module uses, relative to the data center's API root (for example
    /// https://www.site24x7.com/api/). Keeping them in one place makes a path change a one-line fix.
    ///
    /// Paths marked "verify" could not be checked against the live documentation when this module
    /// was written; the rest come from the documentation or from version 1 of the module.
    /// </summary>
    internal static class Endpoints
    {
        // Monitors
        public const string Monitors = "monitors";
        public const string MonitorActivate = "monitors/activate";
        public const string MonitorSuspend = "monitors/suspend";
        public const string MonitorTypeConstants = "monitor_type_constants";
        public const string DeviceKey = "device_key";

        // Groups
        public const string MonitorGroups = "monitor_groups";
        public const string MonitorGroupAddMonitors = "monitor_groups/add_monitors";
        public const string Subgroups = "subgroups";

        // Status and reports
        public const string CurrentStatus = "current_status";
        public const string Reports = "reports";
        public const string LogReports = "reports/log_reports";
        public const string Alarms = "alarms"; // verify
        public const string AlertLogs = "alert_logs"; // verify

        // Users and alerting
        public const string Users = "users";
        public const string UserGroups = "user_groups";
        public const string AttributeGroups = "attribute_groups"; // verify
        public const string OnCallSchedules = "on_call_schedules";

        // Configuration profiles
        public const string LocationProfiles = "location_profiles";
        public const string LocationTemplate = "location_template";
        public const string ThresholdProfiles = "threshold_profiles";
        public const string NotificationProfiles = "notification_profiles";
        public const string ResourceCheckProfiles = "resource_profiles"; // verify
        public const string CredentialProfiles = "credential_profiles";
        public const string EmailTemplates = "email_templates";
        public const string BusinessHours = "business_hours";
        public const string OAuthProviders = "oauth_providers"; // verify
        public const string WebTokens = "web_tokens"; // verify
        public const string GlobalParameters = "global_params"; // verify

        // Operations
        public const string Maintenance = "maintenance";
        public const string StartMaintenance = "start_maintenance";
        public const string EndMaintenance = "end_maintenance";
        public const string StartMaintenanceByName = "start_maintenance/names"; // as in version 1; verify
        public const string EndMaintenanceByName = "end_maintenance/name"; // as in version 1; verify
        public const string Tags = "tags";
        public const string ITAutomation = "it_automation";
        public const string ITAutomationLogs = "automation_logs"; // verify
        public const string AuditLogs = "audit_logs"; // verify
        public const string ConfigurationRules = "config_rules"; // verify
        public const string ScheduledReports = "schedule_reports"; // verify
        public const string SlaSettings = "sla_settings"; // verify
        public const string StatusPages = "statuspages"; // verify
        public const string OnPremisePollers = "on_premise_poller"; // verify

        // Integrations
        public const string ThirdPartyServices = "third_party_services";
        public const string IntegrationRoot = "integration";
        public const string Webhooks = "integration/webhooks";

        // MSP and Business Units
        public const string MspCustomers = "short/msp/customers";
        public const string BusinessUnits = "short/bu/business_units";
    }
}
