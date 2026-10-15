using System;
using System.Collections;
using System.Linq;
using System.Management.Automation;

namespace Site24x7.Cmdlets
{
    // https://www.site24x7.com/help/api/#users

    /// <summary>Settings shared by New-Site24x7User and Set-Site24x7User.</summary>
    public abstract class UserWriteCmdlet : WriteResourceCmdlet
    {
        private const string TimePattern = "^([01][0-9]|2[0-3]):[0-5][0-9]$";

        [Parameter]
        [Alias("display_name")]
        [ValidateNotNullOrEmpty]
        public virtual string DisplayName { get; set; }

        [Parameter]
        [Alias("email_address")]
        [ValidateNotNullOrEmpty]
        public virtual string EmailAddress { get; set; }

        [Parameter]
        [Alias("user_role")]
        public UserRole UserRole { get; set; }

        [Parameter]
        [Alias("statusiq_role")]
        public StatusIQRole StatusIQRole { get; set; }

        [Parameter]
        [Alias("cloudspend_role")]
        public CloudSpendRole CloudSpendRole { get; set; }

        /// <summary>The channels the user can be alerted through.</summary>
        [Parameter]
        [Alias("notify_medium")]
        public NotifyMedium[] NotifyMedium { get; set; }

        [Parameter]
        [Alias("job_title")]
        public JobTitle JobTitle { get; set; }

        /// <summary>User group ids the user belongs to.</summary>
        [Parameter]
        [Alias("user_groups")]
        public string[] UserGroups { get; set; }

        /// <summary>Which monitors the user can see (for restricted roles).</summary>
        [Parameter]
        [Alias("selection_type")]
        public SelectionType SelectionType { get; set; }

        /// <summary>Monitor group ids the user can see when -SelectionType is MonitorGroup.</summary>
        [Parameter]
        [Alias("monitor_groups")]
        public string[] MonitorGroups { get; set; }

        [Parameter]
        [Alias("alert_email_format")]
        public AlertEmailFormat AlertEmailFormat { get; set; }

        /// <summary>Days on which the user is not alerted.</summary>
        [Parameter]
        [Alias("alert_day_exclusion")]
        public DayOfWeek[] AlertDayExclusion { get; set; }

        /// <summary>Start of the daily alerting window, as hh:mm.</summary>
        [Parameter]
        [Alias("alert_start_time")]
        [ValidatePattern(TimePattern)]
        public string AlertStartTime { get; set; }

        /// <summary>End of the daily alerting window, as hh:mm.</summary>
        [Parameter]
        [Alias("alert_end_time")]
        [ValidatePattern(TimePattern)]
        public string AlertEndTime { get; set; }

        [Parameter]
        [Alias("alert_down")]
        public NotifyMedium[] AlertDown { get; set; }

        [Parameter]
        [Alias("alert_trouble")]
        public NotifyMedium[] AlertTrouble { get; set; }

        [Parameter]
        [Alias("alert_up")]
        public NotifyMedium[] AlertUp { get; set; }

        [Parameter]
        [Alias("alert_applogs")]
        public NotifyMedium[] AlertAppLogs { get; set; }

        [Parameter]
        [Alias("alert_anomaly")]
        public NotifyMedium[] AlertAnomaly { get; set; }

        [Parameter]
        [Alias("mobile_country_code")]
        [ValidatePattern(@"^\d{1,3}$")]
        public string MobileCountryCode { get; set; }

        [Parameter]
        [Alias("mobile_number")]
        public string MobileNumber { get; set; }

        [Parameter]
        [Alias("mobile_sms_provider")]
        public SmsProvider MobileSmsProvider { get; set; }

        [Parameter]
        [Alias("mobile_call_provider")]
        public CallProvider MobileCallProvider { get; set; }

        protected override string ResourcePath => Endpoints.Users;
        protected override string ResourceLabel => "user";

        protected override void BuildBody(IDictionary body)
        {
            AddIfBound(body, "display_name", nameof(DisplayName), DisplayName);
            AddIfBound(body, "email_address", nameof(EmailAddress), EmailAddress);
            AddIfBound(body, "user_role", nameof(UserRole), UserRole);
            AddIfBound(body, "statusiq_role", nameof(StatusIQRole), StatusIQRole);
            AddIfBound(body, "cloudspend_role", nameof(CloudSpendRole), CloudSpendRole);
            AddIfBound(body, "notify_medium", nameof(NotifyMedium), Distinct(NotifyMedium));
            AddIfBound(body, "job_title", nameof(JobTitle), JobTitle);
            AddIfBound(body, "user_groups", nameof(UserGroups), UserGroups);
            AddIfBound(body, "selection_type", nameof(SelectionType), SelectionType);
            AddIfBound(body, "monitor_groups", nameof(MonitorGroups), MonitorGroups);

            var alert = new Hashtable();
            AddIfBound(alert, "email_format", nameof(AlertEmailFormat), AlertEmailFormat);
            if (IsBound(nameof(AlertDayExclusion)))
            {
                alert["dont_alert_on_days"] = AlertDayExclusion.Select(d => (int)d).Distinct().OrderBy(d => d).ToArray();
            }
            if (IsBound(nameof(AlertStartTime)) || IsBound(nameof(AlertEndTime)))
            {
                var period = new Hashtable();
                AddIfBound(period, "start_time", nameof(AlertStartTime), AlertStartTime);
                AddIfBound(period, "end_time", nameof(AlertEndTime), AlertEndTime);
                alert["alerting_period"] = period;
            }
            AddIfBound(alert, "down", nameof(AlertDown), Distinct(AlertDown));
            AddIfBound(alert, "trouble", nameof(AlertTrouble), Distinct(AlertTrouble));
            AddIfBound(alert, "up", nameof(AlertUp), Distinct(AlertUp));
            AddIfBound(alert, "applogs", nameof(AlertAppLogs), Distinct(AlertAppLogs));
            AddIfBound(alert, "anomaly", nameof(AlertAnomaly), Distinct(AlertAnomaly));
            if (alert.Count > 0) body["alert_settings"] = alert;

            var mobile = new Hashtable();
            AddIfBound(mobile, "country_code", nameof(MobileCountryCode), MobileCountryCode);
            AddIfBound(mobile, "mobile_number", nameof(MobileNumber), MobileNumber);
            AddIfBound(mobile, "sms_provider_id", nameof(MobileSmsProvider), MobileSmsProvider);
            AddIfBound(mobile, "call_provider_id", nameof(MobileCallProvider), MobileCallProvider);
            if (mobile.Count > 0) body["mobile_settings"] = mobile;
        }

        private static int[] Distinct(NotifyMedium[] values) => values?.Select(v => (int)v).Distinct().ToArray();
    }

    /// <summary>
    /// <para type="synopsis">Gets users.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "Site24x7User", DefaultParameterSetName = ParameterSets.List)]
    [OutputType(typeof(PSObject))]
    public sealed class GetSite24x7UserCommand : GetResourceCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSets.ById, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [Alias("user_id", "Id")]
        [ValidateNotNullOrEmpty]
        public string UserId { get; set; }

        protected override string ResourcePath => Endpoints.Users;
        protected override string ResourceId => UserId;
    }

    /// <summary>
    /// <para type="synopsis">Creates (invites) a user.</para>
    /// </summary>
    /// <example>
    /// <code>New-Site24x7User -DisplayName 'Jo Admin' -EmailAddress jo@example.com -UserRole Administrator -NotifyMedium Email -AlertDown Email -AlertUp Email -UserGroups $ug</code>
    /// </example>
    [Cmdlet(VerbsCommon.New, "Site24x7User", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class NewSite24x7UserCommand : UserWriteCmdlet
    {
        [Parameter(Mandatory = true)]
        [Alias("display_name")]
        [ValidateNotNullOrEmpty]
        public override string DisplayName { get; set; }

        [Parameter(Mandatory = true)]
        [Alias("email_address")]
        [ValidateNotNullOrEmpty]
        public override string EmailAddress { get; set; }

        protected override void ProcessRecordCore()
        {
            var body = CreateBody();
            if (ShouldProcess(Describe(body, ResourceLabel), "Create user"))
            {
                WriteData(Call("POST", ResourcePath, body));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Updates a user. Only the settings you pass are sent.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "Site24x7User", SupportsShouldProcess = true)]
    [Alias("Update-Site24x7User")]
    [OutputType(typeof(PSObject))]
    public sealed class SetSite24x7UserCommand : UserWriteCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("user_id", "Id")]
        [ValidateNotNullOrEmpty]
        public string UserId { get; set; }

        protected override string ResourceId => UserId;

        protected override void ProcessRecordCore()
        {
            var body = CreateBody();
            if (body.Count == 0) throw new Site24x7Exception($"Nothing to update on user {UserId}.");
            if (ShouldProcess(UserId, "Update user"))
            {
                WriteData(Call("PUT", ItemPath, body));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Deletes a user.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "Site24x7User", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
    public sealed class RemoveSite24x7UserCommand : RemoveResourceCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [Alias("user_id", "Id")]
        [ValidateNotNullOrEmpty]
        public string UserId { get; set; }

        protected override string ResourcePath => Endpoints.Users;
        protected override string ResourceId => UserId;
        protected override string ResourceLabel => "user";
    }

    // https://www.site24x7.com/help/api/#user-groups

    /// <summary>Settings shared by New-Site24x7UserGroup and Set-Site24x7UserGroup.</summary>
    public abstract class UserGroupWriteCmdlet : WriteResourceCmdlet
    {
        [Parameter]
        [Alias("display_name")]
        [ValidateNotNullOrEmpty]
        public virtual string DisplayName { get; set; }

        /// <summary>User ids in the group.</summary>
        [Parameter]
        public string[] Users { get; set; }

        /// <summary>0 = Site24x7, 1 = CloudSpend, 2 = StatusIQ.</summary>
        [Parameter]
        [Alias("product_id")]
        public int ProductId { get; set; }

        [Parameter]
        [Alias("attribute_group_id")]
        public string AttributeGroupId { get; set; }

        protected override string ResourcePath => Endpoints.UserGroups;
        protected override string ResourceLabel => "user group";

        protected override void BuildBody(IDictionary body)
        {
            AddIfBound(body, "display_name", nameof(DisplayName), DisplayName);
            AddIfBound(body, "users", nameof(Users), Users);
            AddIfBound(body, "product_id", nameof(ProductId), ProductId);
            AddIfBound(body, "attribute_group_id", nameof(AttributeGroupId), AttributeGroupId);
        }
    }

    /// <summary>
    /// <para type="synopsis">Gets user groups.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "Site24x7UserGroup", DefaultParameterSetName = ParameterSets.List)]
    [OutputType(typeof(PSObject))]
    public sealed class GetSite24x7UserGroupCommand : GetResourceCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSets.ById, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [Alias("user_group_id", "Id")]
        [ValidateNotNullOrEmpty]
        public string UserGroupId { get; set; }

        protected override string ResourcePath => Endpoints.UserGroups;
        protected override string ResourceId => UserGroupId;
    }

    /// <summary>
    /// <para type="synopsis">Creates a user group.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "Site24x7UserGroup", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class NewSite24x7UserGroupCommand : UserGroupWriteCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        [Alias("display_name")]
        [ValidateNotNullOrEmpty]
        public override string DisplayName { get; set; }

        protected override void ProcessRecordCore()
        {
            var body = CreateBody();
            if (ShouldProcess(Describe(body, ResourceLabel), "Create user group"))
            {
                WriteData(Call("POST", ResourcePath, body));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Updates a user group. Only the settings you pass are sent.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "Site24x7UserGroup", SupportsShouldProcess = true)]
    [Alias("Update-Site24x7Usergroup")]
    [OutputType(typeof(PSObject))]
    public sealed class SetSite24x7UserGroupCommand : UserGroupWriteCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("user_group_id", "Id")]
        [ValidateNotNullOrEmpty]
        public string UserGroupId { get; set; }

        protected override string ResourceId => UserGroupId;

        protected override void ProcessRecordCore()
        {
            var body = CreateBody();
            if (body.Count == 0) throw new Site24x7Exception($"Nothing to update on user group {UserGroupId}.");
            if (ShouldProcess(UserGroupId, "Update user group"))
            {
                WriteData(Call("PUT", ItemPath, body));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Deletes a user group.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "Site24x7UserGroup", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
    public sealed class RemoveSite24x7UserGroupCommand : RemoveResourceCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [Alias("user_group_id", "Id")]
        [ValidateNotNullOrEmpty]
        public string UserGroupId { get; set; }

        protected override string ResourcePath => Endpoints.UserGroups;
        protected override string ResourceId => UserGroupId;
        protected override string ResourceLabel => "user group";
    }
}
