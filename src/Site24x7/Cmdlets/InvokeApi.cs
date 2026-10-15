using System.Collections;
using System.Management.Automation;

namespace Site24x7.Cmdlets
{
    /// <summary>
    /// <para type="synopsis">Calls any Site24x7 API endpoint with the current session.</para>
    /// <para type="description">Use this for endpoints that have no dedicated cmdlet. -Path is relative to the API root of your data center, for example "monitors" or "reports/summary/123".</para>
    /// </summary>
    /// <example>
    /// <code>Invoke-Site24x7Api -Method Get -Path 'monitors' -Query @{ type = 'URL' }</code>
    /// </example>
    [Cmdlet(VerbsLifecycle.Invoke, "Site24x7Api", SupportsShouldProcess = true)]
    [OutputType(typeof(PSObject))]
    public sealed class InvokeSite24x7ApiCommand : Site24x7Cmdlet
    {
        [Parameter(Position = 0)]
        [ValidateSet("GET", "POST", "PUT", "PATCH", "DELETE", IgnoreCase = true)]
        public string Method { get; set; } = "GET";

        [Parameter(Mandatory = true, Position = 1)]
        [ValidateNotNullOrEmpty]
        public string Path { get; set; }

        /// <summary>The request body: a hashtable, an object, or a JSON string.</summary>
        [Parameter(ValueFromPipeline = true)]
        public object Body { get; set; }

        [Parameter]
        public Hashtable Query { get; set; }

        /// <summary>The Accept header API version. Defaults to 2.0.</summary>
        [Parameter]
        public string ApiVersion { get; set; }

        /// <summary>Returns the whole response (code, message and data) instead of only data.</summary>
        [Parameter]
        public SwitchParameter Raw { get; set; }

        protected override object ErrorTarget => Path;

        protected override void ProcessRecordCore()
        {
            object body = Body is PSObject pso && !(pso.BaseObject is PSCustomObject) ? pso.BaseObject : Body;
            bool reads = Method.Equals("GET", System.StringComparison.OrdinalIgnoreCase);
            if (reads || ShouldProcess(Path, Method.ToUpperInvariant()))
            {
                var result = Call(Method, Path, body, Query, ApiVersion, Raw);
                if (Raw) WriteObject(result);
                else WriteData(result);
            }
        }
    }
}
