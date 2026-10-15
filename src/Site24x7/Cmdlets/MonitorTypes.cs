using System.Collections;
using System.Management.Automation;

namespace Site24x7.Cmdlets
{
    // Field names follow the Monitor Parameters section of https://www.site24x7.com/help/api/.
    // Anything not covered by a parameter can be passed with -Property.

    /// <summary>Base for the type-specific New-Site24x7*Monitor cmdlets.</summary>
    public abstract class NewTypedMonitorCmdlet : MonitorWriteCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        [Alias("display_name")]
        [ValidateNotNullOrEmpty]
        public override string DisplayName { get; set; }

        protected override string OutputTypeName => "Site24x7.Monitor";

        /// <summary>The API's type code for this monitor.</summary>
        protected abstract string MonitorType { get; }

        protected override void BuildBody(IDictionary body)
        {
            body["type"] = MonitorType;
            base.BuildBody(body);
        }

        protected override void ProcessRecordCore()
        {
            var body = CreateBody();
            if (ShouldProcess(Describe(body, MonitorType), $"Create {MonitorType} monitor"))
            {
                WriteData(Call("POST", ResourcePath, body));
            }
        }
    }

    /// <summary>HTTP verbs as the API encodes them for Website and REST API monitors.</summary>
    public enum MonitorHttpMethod
    {
        Get,
        Post,
        Head,
        Put,
        Patch,
        Delete,
    }

    /// <summary>Settings shared by the HTTP-based monitors.</summary>
    public abstract class NewHttpMonitorCmdlet : NewTypedMonitorCmdlet
    {
        [Parameter(Mandatory = true, Position = 1)]
        [Alias("Url")]
        [ValidateNotNullOrEmpty]
        public string Website { get; set; }

        [Parameter]
        [Alias("http_method")]
        public MonitorHttpMethod HttpMethod { get; set; } = MonitorHttpMethod.Get;

        /// <summary>Request headers, as @{ 'Header-Name' = 'value' }.</summary>
        [Parameter]
        [Alias("custom_headers")]
        public Hashtable CustomHeaders { get; set; }

        [Parameter]
        [Alias("auth_user")]
        public string AuthUser { get; set; }

        [Parameter]
        [Alias("auth_pass")]
        public string AuthPassword { get; set; }

        /// <summary>Raises an alert when the response does not contain this text.</summary>
        [Parameter]
        public string Keyword { get; set; }

        /// <summary>Raises an alert when the response contains this text.</summary>
        [Parameter]
        public string UnmatchingKeyword { get; set; }

        [Parameter]
        [Alias("match_case")]
        public SwitchParameter MatchCase { get; set; }

        [Parameter]
        [Alias("user_agent")]
        public string UserAgent { get; set; }

        [Parameter]
        [Alias("use_ipv6")]
        public SwitchParameter UseIPv6 { get; set; }

        protected override void BuildBody(IDictionary body)
        {
            base.BuildBody(body);
            body["website"] = Website;
            body["http_method"] = EncodeMethod(HttpMethod);
            Default(body, "timeout", nameof(Timeout), 30);
            Default(body, "check_frequency", nameof(CheckFrequency), "5");
            if (CustomHeaders != null) body["custom_headers"] = ToNameValueList(CustomHeaders);
            AddIfBound(body, "auth_user", nameof(AuthUser), AuthUser);
            AddIfBound(body, "auth_pass", nameof(AuthPassword), AuthPassword);
            if (IsBound(nameof(Keyword))) body["matching_keyword"] = new Hashtable { { "value", Keyword }, { "severity", 2 } };
            if (IsBound(nameof(UnmatchingKeyword))) body["unmatching_keyword"] = new Hashtable { { "value", UnmatchingKeyword }, { "severity", 2 } };
            AddIfBound(body, "match_case", nameof(MatchCase), MatchCase);
            AddIfBound(body, "user_agent", nameof(UserAgent), UserAgent);
            AddIfBound(body, "use_ipv6", nameof(UseIPv6), UseIPv6);
        }

        private static string EncodeMethod(MonitorHttpMethod method)
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
    }

    /// <summary>
    /// <para type="synopsis">Creates a Website (URL) monitor.</para>
    /// <para type="description">Defaults: GET, 30 second timeout, checked every 5 minutes.</para>
    /// </summary>
    /// <example>
    /// <code>New-Site24x7WebsiteMonitor 'Home page' https://example.com -LocationProfileId $loc -NotificationProfileId $np -ThresholdProfileId $tp -UserGroupIds $ug</code>
    /// </example>
    [Cmdlet(VerbsCommon.New, "Site24x7WebsiteMonitor", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class NewSite24x7WebsiteMonitorCommand : NewHttpMonitorCmdlet
    {
        protected override string MonitorType => "URL";
    }

    /// <summary>
    /// <para type="synopsis">Creates a REST API monitor.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "Site24x7RestApiMonitor", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class NewSite24x7RestApiMonitorCommand : NewHttpMonitorCmdlet
    {
        /// <summary>The request body to send, for example a JSON string.</summary>
        [Parameter]
        [Alias("request_param")]
        public string RequestBody { get; set; }

        /// <summary>The request body format: J (JSON), T (text), X (XML) or F (form).</summary>
        [Parameter]
        [Alias("request_content_type")]
        [ValidateSet("J", "T", "X", "F")]
        public string RequestContentType { get; set; }

        /// <summary>The id of an OAuth provider (see Get-Site24x7OAuthProvider) used to authenticate.</summary>
        [Parameter]
        [Alias("oauth2_provider")]
        public string OAuthProviderId { get; set; }

        protected override string MonitorType => "RESTAPI";

        protected override void BuildBody(IDictionary body)
        {
            base.BuildBody(body);
            AddIfBound(body, "request_param", nameof(RequestBody), RequestBody);
            AddIfBound(body, "request_content_type", nameof(RequestContentType), RequestContentType);
            AddIfBound(body, "oauth2_provider", nameof(OAuthProviderId), OAuthProviderId);
        }
    }

    /// <summary>
    /// <para type="synopsis">Creates an SSL/TLS certificate monitor.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "Site24x7SslMonitor", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class NewSite24x7SslMonitorCommand : NewTypedMonitorCmdlet
    {
        [Parameter(Mandatory = true, Position = 1)]
        [Alias("domain_name")]
        [ValidateNotNullOrEmpty]
        public string DomainName { get; set; }

        [Parameter]
        [ValidateRange(1, 65535)]
        public int Port { get; set; } = 443;

        /// <summary>Alert this many days before the certificate expires. Defaults to 30.</summary>
        [Parameter]
        [Alias("expire_days")]
        public int ExpireDays { get; set; } = 30;

        protected override string MonitorType => "SSL_CERT";

        protected override void BuildBody(IDictionary body)
        {
            base.BuildBody(body);
            body["domain_name"] = DomainName;
            body["port"] = Port;
            body["expire_days"] = ExpireDays;
            Default(body, "timeout", nameof(Timeout), 30);
        }
    }

    /// <summary>
    /// <para type="synopsis">Creates a Ping monitor.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "Site24x7PingMonitor", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class NewSite24x7PingMonitorCommand : NewTypedMonitorCmdlet
    {
        [Parameter(Mandatory = true, Position = 1)]
        [Alias("host_name")]
        [ValidateNotNullOrEmpty]
        public string HostName { get; set; }

        [Parameter]
        [Alias("use_ipv6")]
        public SwitchParameter UseIPv6 { get; set; }

        protected override string MonitorType => "PING";

        protected override void BuildBody(IDictionary body)
        {
            base.BuildBody(body);
            body["host_name"] = HostName;
            Default(body, "timeout", nameof(Timeout), 10);
            Default(body, "check_frequency", nameof(CheckFrequency), "5");
            AddIfBound(body, "use_ipv6", nameof(UseIPv6), UseIPv6);
        }
    }

    /// <summary>
    /// <para type="synopsis">Creates a TCP Port monitor.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "Site24x7PortMonitor", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class NewSite24x7PortMonitorCommand : NewTypedMonitorCmdlet
    {
        [Parameter(Mandatory = true, Position = 1)]
        [Alias("host_name")]
        [ValidateNotNullOrEmpty]
        public string HostName { get; set; }

        [Parameter(Mandatory = true, Position = 2)]
        [ValidateRange(1, 65535)]
        public int Port { get; set; }

        [Parameter]
        [Alias("use_ssl")]
        public SwitchParameter UseSsl { get; set; }

        /// <summary>Text to send after connecting.</summary>
        [Parameter]
        public string Command { get; set; }

        /// <summary>Alert when the port is open instead of when it is closed.</summary>
        [Parameter]
        [Alias("invert_port_check")]
        public SwitchParameter InvertPortCheck { get; set; }

        protected override string MonitorType => "PORT";

        protected override void BuildBody(IDictionary body)
        {
            base.BuildBody(body);
            body["host_name"] = HostName;
            body["port"] = Port;
            Default(body, "timeout", nameof(Timeout), 10);
            Default(body, "check_frequency", nameof(CheckFrequency), "5");
            AddIfBound(body, "use_ssl", nameof(UseSsl), UseSsl);
            AddIfBound(body, "command", nameof(Command), Command);
            AddIfBound(body, "invert_port_check", nameof(InvertPortCheck), InvertPortCheck);
        }
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

    /// <summary>
    /// <para type="synopsis">Creates a DNS Server monitor.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "Site24x7DnsMonitor", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class NewSite24x7DnsMonitorCommand : NewTypedMonitorCmdlet
    {
        /// <summary>The name server to query.</summary>
        [Parameter(Mandatory = true, Position = 1)]
        [Alias("dns_host")]
        [ValidateNotNullOrEmpty]
        public string DnsHost { get; set; }

        /// <summary>The name to look up.</summary>
        [Parameter(Mandatory = true, Position = 2)]
        [Alias("domain_name")]
        [ValidateNotNullOrEmpty]
        public string DomainName { get; set; }

        [Parameter]
        [Alias("lookup_type")]
        public DnsLookupType LookupType { get; set; } = DnsLookupType.A;

        [Parameter]
        [Alias("dns_port")]
        [ValidateRange(1, 65535)]
        public int DnsPort { get; set; } = 53;

        protected override string MonitorType => "DNS";

        protected override void BuildBody(IDictionary body)
        {
            base.BuildBody(body);
            body["dns_host"] = DnsHost;
            body["domain_name"] = DomainName;
            body["lookup_type"] = (int)LookupType;
            body["dns_port"] = DnsPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Default(body, "timeout", nameof(Timeout), 10);
            Default(body, "check_frequency", nameof(CheckFrequency), "5");
        }
    }

    /// <summary>
    /// <para type="synopsis">Creates a Heartbeat monitor, which alerts when your job stops calling its ping URL.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "Site24x7HeartbeatMonitor", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class NewSite24x7HeartbeatMonitorCommand : NewTypedMonitorCmdlet
    {
        /// <summary>The unique name used in the monitor's ping URL.</summary>
        [Parameter(Mandatory = true, Position = 1)]
        [Alias("name_in_ping_url")]
        [ValidateNotNullOrEmpty]
        public string NameInPingUrl { get; set; }

        protected override string MonitorType => "HEARTBEAT";

        protected override void BuildBody(IDictionary body)
        {
            base.BuildBody(body);
            body["name_in_ping_url"] = NameInPingUrl;
        }
    }

    /// <summary>
    /// <para type="synopsis">Creates a Domain Expiry monitor.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.New, "Site24x7DomainExpiryMonitor", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class NewSite24x7DomainExpiryMonitorCommand : NewTypedMonitorCmdlet
    {
        /// <summary>The domain to watch, for example example.com.</summary>
        [Parameter(Mandatory = true, Position = 1)]
        [Alias("host_name", "DomainName")]
        [ValidateNotNullOrEmpty]
        public string HostName { get; set; }

        /// <summary>Alert this many days before the domain expires. Defaults to 30.</summary>
        [Parameter]
        [Alias("expire_days")]
        public int ExpireDays { get; set; } = 30;

        /// <summary>A specific WHOIS server to query.</summary>
        [Parameter]
        [Alias("whois_server")]
        public string WhoisServer { get; set; }

        protected override string MonitorType => "DOMAINEXPIRY";

        protected override void BuildBody(IDictionary body)
        {
            base.BuildBody(body);
            body["host_name"] = HostName;
            body["expire_days"] = ExpireDays;
            AddIfBound(body, "whois_server", nameof(WhoisServer), WhoisServer);
            Default(body, "timeout", nameof(Timeout), 10);
        }
    }
}
