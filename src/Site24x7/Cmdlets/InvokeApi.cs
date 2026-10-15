using System;
using System.Collections;
using System.Management.Automation;

namespace Site24x7.Cmdlets
{
    /// <summary>
    /// <para type="synopsis">Calls any Site24x7 API endpoint with the current session.</para>
    /// <para type="description">Use this for endpoints that have no dedicated cmdlet. -Endpoint is relative to the API root of your data center, for example "monitors" or "reports/summary/123". A full URL is accepted only inside the session's own API root, so the access token is never sent anywhere else.</para>
    /// </summary>
    /// <example>
    /// <code>Invoke-Site24x7Api GET 'monitors' -Query @{ type = 'URL' }</code>
    /// </example>
    /// <example>
    /// <code>Invoke-Site24x7Api POST 'monitors' -Body @{ display_name = 'Home'; type = 'URL'; website = 'https://example.com' }</code>
    /// </example>
    [Cmdlet(VerbsLifecycle.Invoke, "Site24x7Api", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class InvokeSite24x7ApiCommand : Site24x7Cmdlet
    {
        /// <summary>The HTTP method. Defaults to GET.</summary>
        [Parameter(Position = 0)]
        [ValidateSet("GET", "POST", "PUT", "PATCH", "DELETE", IgnoreCase = true)]
        public string Method { get; set; } = "GET";

        /// <summary>The endpoint path relative to the API root, for example "monitors/123".</summary>
        [Parameter(Mandatory = true, Position = 1)]
        [ValidateNotNullOrEmpty]
        public string Endpoint { get; set; }

        /// <summary>The request body: a hashtable, an object, or a JSON string. Not allowed with GET.</summary>
        [Parameter(ValueFromPipeline = true)]
        public object Body { get; set; }

        [Parameter]
        public Hashtable Query { get; set; }

        /// <summary>The Accept header API version. Defaults to 2.0.</summary>
        [Parameter]
        [ValidatePattern(@"^\d+\.\d+$")]
        public string ApiVersion { get; set; }

        /// <summary>Returns the whole response (code, message and data) instead of only data.</summary>
        [Parameter]
        public SwitchParameter Raw { get; set; }

        protected override object ErrorTarget => Endpoint;

        private bool IsGet => Method.Equals("GET", StringComparison.OrdinalIgnoreCase);

        protected override void ProcessRecordCore()
        {
            object body = Body is PSObject pso && !(pso.BaseObject is PSCustomObject) ? pso.BaseObject : Body;
            if (IsGet && body != null)
            {
                throw new Site24x7ArgumentException("A GET request cannot have a body. Use -Query, or another -Method.");
            }

            if (IsGet || ShouldProcess(Endpoint, Method.ToUpperInvariant()))
            {
                var result = Call(Method, Endpoint, body, Query, ApiVersion, Raw);
                if (Raw) WriteObject(result);
                else WriteData(result);
            }
        }
    }
}
