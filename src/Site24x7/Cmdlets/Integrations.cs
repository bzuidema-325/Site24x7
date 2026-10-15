using System.Collections;
using System.Management.Automation;

namespace Site24x7.Cmdlets
{
    // https://www.site24x7.com/help/api/#third-party-integrations
    // Get-Site24x7ThirdPartyIntegration and Remove-Site24x7ThirdPartyIntegration are generated.

    /// <summary>Settings shared by New-Site24x7Webhook and Set-Site24x7Webhook.</summary>
    public abstract class WebhookWriteCmdlet : WriteResourceCmdlet
    {
        [Parameter]
        [ValidateNotNullOrEmpty]
        public virtual string Name { get; set; }

        [Parameter]
        [ValidateNotNullOrEmpty]
        public virtual string Url { get; set; }

        [Parameter]
        public MonitorHttpMethod Method { get; set; } = MonitorHttpMethod.Post;

        /// <summary>Seconds to wait for the webhook to respond.</summary>
        [Parameter]
        [ValidateRange(1, 45)]
        public int Timeout { get; set; }

        /// <summary>Which monitors raise the webhook.</summary>
        [Parameter]
        [Alias("selection_type")]
        public SelectionType SelectionType { get; set; }

        /// <summary>Monitors, groups or tags for -SelectionType (field name depends on the type; use -Property for others).</summary>
        [Parameter]
        public string[] Monitors { get; set; }

        /// <summary>Send the incident details as parameters.</summary>
        [Parameter]
        [Alias("send_incident_parameters")]
        public SwitchParameter SendIncidentParameters { get; set; }

        /// <summary>Request headers, as @{ 'Header-Name' = 'value' }.</summary>
        [Parameter]
        [Alias("custom_headers")]
        public Hashtable CustomHeaders { get; set; }

        /// <summary>A custom payload to send instead of the default one.</summary>
        [Parameter]
        [Alias("custom_parameters")]
        public string CustomParameters { get; set; }

        /// <summary>Call the webhook from an on-premise poller.</summary>
        [Parameter]
        [Alias("is_poller_webhook")]
        public SwitchParameter IsPollerWebhook { get; set; }

        [Parameter]
        [Alias("poller")]
        public string PollerId { get; set; }

        protected override string ResourcePath => Endpoints.Webhooks;
        protected override string ResourceLabel => "webhook";

        protected override void BuildBody(IDictionary body)
        {
            AddIfBound(body, "name", nameof(Name), Name);
            AddIfBound(body, "url", nameof(Url), Url);
            if (IsBound(nameof(Method)) || !IsBound("ServiceId"))
            {
                body["method"] = Method == MonitorHttpMethod.Get ? "G" : Method == MonitorHttpMethod.Put ? "PU" : Method == MonitorHttpMethod.Delete ? "D" : "P";
            }
            AddIfBound(body, "timeout", nameof(Timeout), Timeout);
            AddIfBound(body, "selection_type", nameof(SelectionType), SelectionType);
            AddIfBound(body, "monitors", nameof(Monitors), Monitors);
            AddIfBound(body, "send_incident_parameters", nameof(SendIncidentParameters), SendIncidentParameters);
            if (CustomHeaders != null)
            {
                var list = new ArrayList();
                foreach (DictionaryEntry e in CustomHeaders)
                {
                    list.Add(new Hashtable { { "name", e.Key }, { "value", e.Value } });
                }
                body["custom_headers"] = list;
            }
            if (IsBound(nameof(CustomParameters)))
            {
                body["send_custom_parameters"] = true;
                body["custom_parameters"] = CustomParameters;
            }
            AddIfBound(body, "is_poller_webhook", nameof(IsPollerWebhook), IsPollerWebhook);
            AddIfBound(body, "poller", nameof(PollerId), PollerId);
        }
    }

    /// <summary>
    /// <para type="synopsis">Creates a webhook integration.</para>
    /// </summary>
    /// <example>
    /// <code>New-Site24x7Webhook -Name 'Ops bot' -Url https://hooks.example.com/site24x7 -SelectionType All -SendIncidentParameters</code>
    /// </example>
    [Cmdlet(VerbsCommon.New, "Site24x7Webhook", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class NewSite24x7WebhookCommand : WebhookWriteCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        public override string Name { get; set; }

        [Parameter(Mandatory = true, Position = 1)]
        [ValidateNotNullOrEmpty]
        public override string Url { get; set; }

        protected override void ProcessRecordCore()
        {
            var body = CreateBody();
            if (!body.Contains("timeout")) body["timeout"] = 30;
            if (!body.Contains("selection_type")) body["selection_type"] = SelectionType.All;
            if (ShouldProcess(Name, "Create webhook"))
            {
                WriteData(Call("POST", ResourcePath, body));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Updates a webhook integration. Only the settings you pass are sent.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "Site24x7Webhook", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class SetSite24x7WebhookCommand : WebhookWriteCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("service_id", "Id")]
        [ValidateNotNullOrEmpty]
        public string ServiceId { get; set; }

        protected override string ResourceId => ServiceId;

        protected override void ProcessRecordCore()
        {
            var body = CreateBody();
            if (body.Count == 0) throw new Site24x7Exception($"Nothing to update on webhook {ServiceId}.");
            if (ShouldProcess(ServiceId, "Update webhook"))
            {
                WriteData(Call("PUT", ItemPath, body));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Creates any third-party integration (Slack, PagerDuty, Microsoft Teams, Opsgenie, ServiceNow and others).</para>
    /// <para type="description">-Type is the integration's path segment under /integration in the Site24x7 API documentation, for example slack, pager_duty, opsgenie or servicenow. Pass the integration's fields in -Property.</para>
    /// </summary>
    /// <example>
    /// <code>New-Site24x7Integration -Type slack -Property @{ name = 'Ops channel'; url = $slackHookUrl; selection_type = 0 }</code>
    /// </example>
    [Cmdlet(VerbsCommon.New, "Site24x7Integration", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class NewSite24x7IntegrationCommand : WriteResourceCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        [ArgumentCompleter(typeof(IntegrationTypeCompleter))]
        public string Type { get; set; }

        protected override string ResourcePath => Endpoints.IntegrationRoot + "/" + Seg(Type);
        protected override string ResourceLabel => Type + " integration";

        protected override void ProcessRecordCore()
        {
            var body = CreateBody();
            if (body.Count == 0) throw new Site24x7Exception("Pass the integration's settings with -Property.");
            if (ShouldProcess(Describe(body, ResourceLabel), "Create " + ResourceLabel))
            {
                WriteData(Call("POST", ResourcePath, body));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Updates any third-party integration. Only the fields in -Property are sent.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "Site24x7Integration", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class SetSite24x7IntegrationCommand : WriteResourceCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        [ArgumentCompleter(typeof(IntegrationTypeCompleter))]
        public string Type { get; set; }

        [Parameter(Mandatory = true, Position = 1, ValueFromPipelineByPropertyName = true)]
        [Alias("service_id", "Id")]
        [ValidateNotNullOrEmpty]
        public string ServiceId { get; set; }

        protected override string ResourcePath => Endpoints.IntegrationRoot + "/" + Seg(Type);
        protected override string ResourceId => ServiceId;
        protected override string ResourceLabel => Type + " integration";

        protected override void ProcessRecordCore()
        {
            var body = CreateBody();
            if (body.Count == 0) throw new Site24x7Exception("Pass the settings to change with -Property.");
            if (ShouldProcess(ServiceId, "Update " + ResourceLabel))
            {
                WriteData(Call("PUT", ItemPath, body));
            }
        }
    }
}

namespace Site24x7.Cmdlets
{
    /// <summary>Tab completion for the -Type parameter of the integration cmdlets.</summary>
    public sealed class IntegrationTypeCompleter : System.Management.Automation.IArgumentCompleter
    {
        private static readonly string[] Types =
            { "slack", "pager_duty", "opsgenie", "servicenow", "ms_teams", "connectwise", "hangouts", "telegram", "moogsoft", "jira", "webhooks" };

        public System.Collections.Generic.IEnumerable<System.Management.Automation.CompletionResult> CompleteArgument(
            string commandName, string parameterName, string wordToComplete,
            System.Management.Automation.Language.CommandAst commandAst, System.Collections.IDictionary fakeBoundParameters)
        {
            foreach (var t in Types)
            {
                if (t.StartsWith(wordToComplete ?? string.Empty, System.StringComparison.OrdinalIgnoreCase))
                {
                    yield return new System.Management.Automation.CompletionResult(t);
                }
            }
        }
    }
}
