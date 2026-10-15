using System;
using System.Collections;
using System.Collections.Specialized;
using System.Management.Automation;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Site24x7.Cmdlets
{
    /// <summary>
    /// Base class for every cmdlet that calls the Site24x7 API. It resolves which session to use,
    /// sends the request, and turns API failures into PowerShell errors.
    /// </summary>
    public abstract class Site24x7Cmdlet : PSCmdlet
    {
        private Site24x7Session _session;

        /// <summary>
        /// An access token to use instead of the session from Connect-Site24x7. Kept for compatibility
        /// with scripts written for version 1 of the module.
        /// </summary>
        [Parameter]
        [Alias("access_token")]
        [ValidateNotNullOrEmpty]
        public string AccessToken { get; set; }

        /// <summary>The data center an -AccessToken belongs to. Defaults to the connected session's data center, or US.</summary>
        [Parameter]
        public Site24x7DataCenter? DataCenter { get; set; }

        /// <summary>A session from Connect-Site24x7 -PassThru, for working with more than one account at once.</summary>
        [Parameter]
        public Site24x7Session Session { get; set; }

        protected Site24x7Session ActiveSession
        {
            get
            {
                if (_session != null) return _session;

                if (Session != null)
                {
                    _session = Session;
                }
                else if (!string.IsNullOrEmpty(AccessToken))
                {
                    var current = Site24x7SessionState.Current;
                    var dc = DataCenter ?? current?.DataCenter ?? Site24x7DataCenter.US;
                    bool sameDc = current != null && current.DataCenter == dc;
                    _session = new Site24x7Session(dc, sameDc ? current.ApiBaseUri : null, sameDc ? current.AccountsUri : null)
                    {
                        AccessToken = AccessToken,
                        CustomerId = sameDc ? current.CustomerId : null,
                    };
                }
                else if (Site24x7SessionState.Current != null)
                {
                    _session = Site24x7SessionState.Current;
                }
                else
                {
                    ThrowTerminatingError(new ErrorRecord(
                        new Site24x7Exception("You are not connected to Site24x7. Run Connect-Site24x7 first, or pass -AccessToken."),
                        "Site24x7.NotConnected",
                        ErrorCategory.AuthenticationError,
                        null));
                }
                return _session;
            }
        }

        protected sealed override void ProcessRecord()
        {
            try
            {
                ProcessRecordCore();
            }
            catch (Site24x7ApiException ex)
            {
                WriteError(new ErrorRecord(ex, "Site24x7.Api" + (ex.ErrorCode.HasValue ? "." + ex.ErrorCode.Value : string.Empty), Categorize(ex.StatusCode), ErrorTarget)
                {
                    ErrorDetails = new ErrorDetails(ex.Message),
                });
            }
            catch (Site24x7Exception ex)
            {
                WriteError(new ErrorRecord(ex, "Site24x7.Error", ErrorCategory.InvalidOperation, ErrorTarget));
            }
            catch (HttpRequestException ex)
            {
                WriteError(new ErrorRecord(ex, "Site24x7.Connection", ErrorCategory.ConnectionError, ErrorTarget));
            }
            catch (TaskCanceledException ex)
            {
                WriteError(new ErrorRecord(new TimeoutException("The request to Site24x7 timed out.", ex), "Site24x7.Timeout", ErrorCategory.OperationTimeout, ErrorTarget));
            }
        }

        /// <summary>The work done for each pipeline input.</summary>
        protected abstract void ProcessRecordCore();

        /// <summary>The object an error is reported against, usually the resource id.</summary>
        protected virtual object ErrorTarget => null;

        protected object Call(string method, string path, object body = null, IDictionary query = null, string apiVersion = null, bool raw = false)
        {
            return Site24x7Client.Invoke(ActiveSession, method, path, query, body, apiVersion, raw, Trace);
        }

        private void Trace(string message, bool isDebug)
        {
            if (isDebug) WriteDebug(message);
            else WriteVerbose(message);
        }

        /// <summary>
        /// The type name added to output objects so the module's formatting applies, for example
        /// "Site24x7.Monitor". Null leaves objects as plain PSCustomObject.
        /// </summary>
        protected virtual string OutputTypeName => null;

        /// <summary>Writes API data to the pipeline, one object per array element.</summary>
        protected void WriteData(object data)
        {
            if (data == null) return;
            if (data is object[] items)
            {
                foreach (var item in items) WriteObject(Tag(item));
                return;
            }
            WriteObject(Tag(data));
        }

        private object Tag(object item)
        {
            var name = OutputTypeName;
            if (name != null && item is PSObject pso && pso.BaseObject is PSCustomObject && !pso.TypeNames.Contains(name))
            {
                pso.TypeNames.Insert(0, name);
            }
            return item;
        }

        protected bool IsBound(string parameterName) => MyInvocation.BoundParameters.ContainsKey(parameterName);

        /// <summary>Escapes a value for use as one segment of a URL path.</summary>
        protected static string Seg(string value) => Uri.EscapeDataString(value ?? string.Empty);

        /// <summary>
        /// Adds a field to a request body only when the matching parameter was given, so that
        /// false, 0 and empty values are sent when the caller asks for them, and nothing else is.
        /// </summary>
        protected void AddIfBound(IDictionary body, string field, string parameterName, object value)
        {
            if (IsBound(parameterName))
            {
                body[field] = value is SwitchParameter sw ? sw.IsPresent : value;
            }
        }

        /// <summary>Copies every entry of a -Property hashtable into the body, overriding typed parameters.</summary>
        protected static void Merge(IDictionary body, IDictionary extra)
        {
            if (extra == null) return;
            foreach (DictionaryEntry entry in extra)
            {
                body[Convert.ToString(entry.Key)] = entry.Value is PSObject pso && !(pso.BaseObject is PSCustomObject) ? pso.BaseObject : entry.Value;
            }
        }

        protected static OrderedDictionary NewBody() => new OrderedDictionary(StringComparer.Ordinal);

        private static ErrorCategory Categorize(HttpStatusCode? status)
        {
            switch (status)
            {
                case HttpStatusCode.BadRequest: return ErrorCategory.InvalidArgument;
                case HttpStatusCode.Unauthorized: return ErrorCategory.AuthenticationError;
                case HttpStatusCode.Forbidden: return ErrorCategory.PermissionDenied;
                case HttpStatusCode.NotFound: return ErrorCategory.ObjectNotFound;
                case HttpStatusCode.Conflict: return ErrorCategory.ResourceExists;
                default:
                    if ((int?)status == 429) return ErrorCategory.LimitsExceeded;
                    return ErrorCategory.InvalidOperation;
            }
        }
    }
}
