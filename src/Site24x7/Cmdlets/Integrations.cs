using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace Site24x7.Cmdlets
{
    // https://www.site24x7.com/help/api/#third-party-integrations
    // Get-Site24x7Integration and Remove-Site24x7Integration are generated and cover every kind,
    // webhooks included. New- and Set-Site24x7Webhook are typed shortcuts for webhooks.

    /// <summary>Settings shared by New-Site24x7Webhook and Set-Site24x7Webhook.</summary>
    public abstract class WebhookWriteCmdlet : WriteResourceCmdlet
    {
        /// <summary>The integration's name.</summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public virtual string Name { get; set; }

        /// <summary>The URL Site24x7 calls.</summary>
        [Parameter]
        [ValidateNotNullOrEmpty]
        public virtual string Url { get; set; }

        /// <summary>The HTTP method. New webhooks default to POST.</summary>
        [Parameter]
        public WebhookMethod Method { get; set; }

        /// <summary>Seconds to wait for the webhook to respond. New webhooks default to 30.</summary>
        [Parameter]
        [ValidateRange(1, 45)]
        public int Timeout { get; set; }

        /// <summary>Raise the webhook for every monitor. New webhooks default to this unless -MonitorId, -MonitorGroupId or -TagId is given.</summary>
        [Parameter]
        public SwitchParameter AllMonitors { get; set; }

        /// <summary>Raise the webhook only for these monitors.</summary>
        [Parameter]
        public string[] MonitorId { get; set; }

        /// <summary>Raise the webhook only for the monitors in these groups.</summary>
        [Parameter]
        public string[] MonitorGroupId { get; set; }

        /// <summary>Raise the webhook only for monitors with these tags.</summary>
        [Parameter]
        public string[] TagId { get; set; }

        /// <summary>Send the incident details as parameters.</summary>
        [Parameter]
        public SwitchParameter SendIncidentParameters { get; set; }

        /// <summary>Request headers, as @{ 'Header-Name' = 'value' }.</summary>
        [Parameter]
        public Hashtable Header { get; set; }

        /// <summary>A custom payload to send instead of the default one.</summary>
        [Parameter]
        public string CustomParameters { get; set; }

        /// <summary>Call the webhook from this on-premise poller instead of from Site24x7's servers.</summary>
        [Parameter]
        public string PollerId { get; set; }

        private protected override ResourceInfo Resource => Resources.Webhook;

        protected override void BuildBody(IDictionary body)
        {
            AddIfBound(body, "name", nameof(Name), Name);
            AddIfBound(body, "url", nameof(Url), Url);
            if (IsBound(nameof(Method))) body["method"] = ApiCodes.Of(Method);
            AddIfBound(body, "timeout", nameof(Timeout), Timeout);
            if (AllMonitors) body["selection_type"] = SelectionType.All;
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
            AddIfBound(body, "send_incident_parameters", nameof(SendIncidentParameters), SendIncidentParameters);
            if (Header != null) body["custom_headers"] = ToNameValueList(Header);
            if (IsBound(nameof(CustomParameters)))
            {
                body["send_custom_parameters"] = true;
                body["custom_parameters"] = CustomParameters;
            }
            if (IsBound(nameof(PollerId)))
            {
                body["is_poller_webhook"] = true;
                body["poller"] = PollerId;
            }
        }

        protected override void ValidateBody(IDictionary body)
        {
            base.ValidateBody(body);
            int selections = (AllMonitors ? 1 : 0) + new[] { nameof(MonitorId), nameof(MonitorGroupId), nameof(TagId) }.Count(IsBound);
            if (selections > 1)
            {
                throw new Site24x7ArgumentException("Choose one of -AllMonitors, -MonitorId, -MonitorGroupId or -TagId.");
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Creates a webhook integration.</para>
    /// </summary>
    /// <example>
    /// <code>New-Site24x7Webhook 'Ops bot' https://hooks.example.com/site24x7 -SendIncidentParameters</code>
    /// </example>
    [Cmdlet(VerbsCommon.New, "Site24x7Webhook", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class NewSite24x7WebhookCommand : WebhookWriteCmdlet
    {
        /// <summary>The integration's name.</summary>
        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        public override string Name { get; set; }

        /// <summary>The URL Site24x7 calls.</summary>
        [Parameter(Mandatory = true, Position = 1)]
        [ValidateNotNullOrEmpty]
        public override string Url { get; set; }

        protected override void BuildBody(IDictionary body)
        {
            base.BuildBody(body);
            Default(body, "method", ApiCodes.Of(WebhookMethod.Post));
            Default(body, "timeout", 30);
            Default(body, "selection_type", SelectionType.All);
        }
    }

    /// <summary>
    /// <para type="synopsis">Updates a webhook integration. Only the settings you pass are sent.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "Site24x7Webhook", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class SetSite24x7WebhookCommand : WebhookWriteCmdlet
    {
        /// <summary>The webhook's integration id.</summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("service_id")]
        [ValidateNotNullOrEmpty]
        public string IntegrationId { get; set; }

        protected override string ResourceId => IntegrationId;
    }

    /// <summary>The -IntegrationType, path and label shared by New- and Set-Site24x7Integration.</summary>
    public abstract class IntegrationWriteCmdlet : WriteResourceCmdlet
    {
        /// <summary>The integration's path segment under /integration in the Site24x7 API documentation, for example slack, pager_duty, opsgenie or servicenow.</summary>
        [Parameter(Mandatory = true, Position = 1)]
        [ValidateNotNullOrEmpty]
        [ArgumentCompleter(typeof(IntegrationTypeCompleter))]
        public string IntegrationType { get; set; }

        /// <summary>The integration's fields, as a hashtable of the API's field names and values.</summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNull]
        public override Hashtable Property { get; set; }

        private protected override ResourceInfo Resource => Resources.Integration;
        protected override string ResourcePath => Endpoints.IntegrationRoot + "/" + Seg(IntegrationType);
        protected override string ResourceLabel => IntegrationType + " integration";
    }

    /// <summary>
    /// <para type="synopsis">Creates any third-party integration (Slack, PagerDuty, Microsoft Teams, Opsgenie, ServiceNow and others).</para>
    /// <para type="description">Pass the integration's fields in -Property, using the field names from the Site24x7 API documentation.</para>
    /// </summary>
    /// <example>
    /// <code>New-Site24x7Integration -IntegrationType slack -Property @{ name = 'Ops channel'; url = $slackHookUrl; selection_type = 0 }</code>
    /// </example>
    [Cmdlet(VerbsCommon.New, "Site24x7Integration", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class NewSite24x7IntegrationCommand : IntegrationWriteCmdlet
    {
    }

    /// <summary>
    /// <para type="synopsis">Updates any third-party integration. Only the fields in -Property are sent.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "Site24x7Integration", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class SetSite24x7IntegrationCommand : IntegrationWriteCmdlet
    {
        /// <summary>The integration's id.</summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("service_id")]
        [ValidateNotNullOrEmpty]
        public string IntegrationId { get; set; }

        protected override string ResourceId => IntegrationId;
    }

    /// <summary>Tab completion for the -Type parameter of the integration cmdlets.</summary>
    public sealed class IntegrationTypeCompleter : IArgumentCompleter
    {
        private static readonly string[] Types =
            { "slack", "pager_duty", "opsgenie", "servicenow", "ms_teams", "connectwise", "hangouts", "telegram", "moogsoft", "jira", "webhooks" };

        public IEnumerable<CompletionResult> CompleteArgument(
            string commandName, string parameterName, string wordToComplete, CommandAst commandAst, IDictionary fakeBoundParameters)
        {
            return Types
                .Where(t => t.StartsWith(wordToComplete ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                .Select(t => new CompletionResult(t));
        }
    }
}
