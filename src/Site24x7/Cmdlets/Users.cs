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
        /// <summary>The user's name.</summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public virtual string DisplayName { get; set; }

        /// <summary>The user's email address. New users are sent an invitation.</summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public virtual string EmailAddress { get; set; }

        /// <summary>The user's Site24x7 role.</summary>
        [Parameter]
        public UserRole UserRole { get; set; }

        /// <summary>The user's StatusIQ role.</summary>
        [Parameter]
        public StatusIQRole StatusIQRole { get; set; }

        /// <summary>The user's CloudSpend role.</summary>
        [Parameter]
        public CloudSpendRole CloudSpendRole { get; set; }

        /// <summary>The channels the user can be alerted through.</summary>
        [Parameter]
        public NotifyMedium[] NotifyMedium { get; set; }

        /// <summary>The user's job title.</summary>
        [Parameter]
        public JobTitle JobTitle { get; set; }

        /// <summary>The user groups the user belongs to.</summary>
        [Parameter]
        public string[] UserGroupId { get; set; }

        /// <summary>Which monitors the user can see (for restricted roles).</summary>
        [Parameter]
        public SelectionType SelectionType { get; set; }

        /// <summary>The monitor groups the user can see when -SelectionType is MonitorGroup.</summary>
        [Parameter]
        public string[] MonitorGroupId { get; set; }

        /// <summary>Text or HTML alert emails.</summary>
        [Parameter]
        public AlertEmailFormat AlertEmailFormat { get; set; }

        /// <summary>Days on which the user is not alerted.</summary>
        [Parameter]
        public DayOfWeek[] AlertDayExclusion { get; set; }

        /// <summary>Start of the daily alerting window, as hh:mm.</summary>
        [Parameter]
        [ValidatePattern(TimeOfDayPattern)]
        public string AlertStartTime { get; set; }

        /// <summary>End of the daily alerting window, as hh:mm.</summary>
        [Parameter]
        [ValidatePattern(TimeOfDayPattern)]
        public string AlertEndTime { get; set; }

        /// <summary>The channels used for down alerts.</summary>
        [Parameter]
        public NotifyMedium[] AlertDown { get; set; }

        /// <summary>The channels used for trouble alerts.</summary>
        [Parameter]
        public NotifyMedium[] AlertTrouble { get; set; }

        /// <summary>The channels used when a monitor is back up.</summary>
        [Parameter]
        public NotifyMedium[] AlertUp { get; set; }

        /// <summary>The channels used for AppLogs alerts.</summary>
        [Parameter]
        public NotifyMedium[] AlertAppLogs { get; set; }

        /// <summary>The channels used for anomaly alerts.</summary>
        [Parameter]
        public NotifyMedium[] AlertAnomaly { get; set; }

        /// <summary>The mobile number's country calling code, without +.</summary>
        [Parameter]
        [ValidatePattern(@"^\d{1,3}$")]
        public string MobileCountryCode { get; set; }

        /// <summary>The mobile number for SMS and voice alerts.</summary>
        [Parameter]
        public string MobileNumber { get; set; }

        /// <summary>The SMS provider for the mobile number.</summary>
        [Parameter]
        public SmsProvider MobileSmsProvider { get; set; }

        /// <summary>The voice call provider for the mobile number.</summary>
        [Parameter]
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
            AddIfBound(body, "user_groups", nameof(UserGroupId), UserGroupId);
            AddIfBound(body, "selection_type", nameof(SelectionType), SelectionType);
            AddIfBound(body, "monitor_groups", nameof(MonitorGroupId), MonitorGroupId);

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
        /// <summary>The user's id.</summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSets.ById, ValueFromPipelineByPropertyName = true)]
        [Alias("user_id")]
        [ValidateNotNullOrEmpty]
        public string UserId { get; set; }

        protected override string ResourcePath => Endpoints.Users;
        protected override string ResourceId => UserId;
        protected override string ResourceLabel => "user";
    }

    /// <summary>
    /// <para type="synopsis">Creates (invites) a user.</para>
    /// </summary>
    /// <example>
    /// <code>New-Site24x7User -DisplayName 'Jo Admin' -EmailAddress jo@example.com -UserRole Administrator -NotifyMedium Email -AlertDown Email -AlertUp Email -UserGroupId $ug</code>
    /// </example>
    [Cmdlet(VerbsCommon.New, "Site24x7User", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class NewSite24x7UserCommand : UserWriteCmdlet
    {
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public override string DisplayName { get; set; }

        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public override string EmailAddress { get; set; }
    }

    /// <summary>
    /// <para type="synopsis">Updates a user. Only the settings you pass are sent.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "Site24x7User", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class SetSite24x7UserCommand : UserWriteCmdlet
    {
        /// <summary>The user's id.</summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("user_id")]
        [ValidateNotNullOrEmpty]
        public string UserId { get; set; }

        protected override string ResourceId => UserId;
    }

    /// <summary>
    /// <para type="synopsis">Deletes a user.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "Site24x7User", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
    public sealed class RemoveSite24x7UserCommand : RemoveResourceCmdlet
    {
        /// <summary>The user's id.</summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("user_id")]
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
        /// <summary>The group's name.</summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public virtual string DisplayName { get; set; }

        /// <summary>The users in the group. Replaces the current members on Set.</summary>
        [Parameter]
        public string[] UserId { get; set; }

        /// <summary>0 = Site24x7, 1 = CloudSpend, 2 = StatusIQ.</summary>
        [Parameter]
        public int ProductId { get; set; }

        /// <summary>An attribute alert group for the user group. See Get-Site24x7AttributeAlertGroup.</summary>
        [Parameter]
        public string AttributeGroupId { get; set; }

        protected override string ResourcePath => Endpoints.UserGroups;
        protected override string ResourceLabel => "user group";

        protected override void BuildBody(IDictionary body)
        {
            AddIfBound(body, "display_name", nameof(DisplayName), DisplayName);
            AddIfBound(body, "users", nameof(UserId), UserId);
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
        /// <summary>The user group's id.</summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSets.ById, ValueFromPipelineByPropertyName = true)]
        [Alias("user_group_id")]
        [ValidateNotNullOrEmpty]
        public string UserGroupId { get; set; }

        protected override string ResourcePath => Endpoints.UserGroups;
        protected override string ResourceId => UserGroupId;
        protected override string ResourceLabel => "user group";
    }

    /// <summary>
    /// <para type="synopsis">Creates a user group.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "Site24x7UserGroup", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class NewSite24x7UserGroupCommand : UserGroupWriteCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        public override string DisplayName { get; set; }
    }

    /// <summary>
    /// <para type="synopsis">Updates a user group. Only the settings you pass are sent.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "Site24x7UserGroup", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class SetSite24x7UserGroupCommand : UserGroupWriteCmdlet
    {
        /// <summary>The user group's id.</summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("user_group_id")]
        [ValidateNotNullOrEmpty]
        public string UserGroupId { get; set; }

        protected override string ResourceId => UserGroupId;
    }

    /// <summary>
    /// <para type="synopsis">Deletes a user group.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "Site24x7UserGroup", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
    public sealed class RemoveSite24x7UserGroupCommand : RemoveResourceCmdlet
    {
        /// <summary>The user group's id.</summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("user_group_id")]
        [ValidateNotNullOrEmpty]
        public string UserGroupId { get; set; }

        protected override string ResourcePath => Endpoints.UserGroups;
        protected override string ResourceId => UserGroupId;
        protected override string ResourceLabel => "user group";
    }
}
