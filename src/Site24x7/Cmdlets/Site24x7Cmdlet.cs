using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Management.Automation;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;

namespace Site24x7.Cmdlets
{
    /// <summary>
    /// Base class for every cmdlet in the module. It lets Ctrl+C stop a request, and turns failures
    /// into PowerShell errors the same way everywhere: a problem with one pipeline item is a
    /// non-terminating error and the next item still runs; a problem found in BeginProcessing,
    /// before any input is read, stops the command.
    /// </summary>
    public abstract class Site24x7CmdletBase : PSCmdlet, IDisposable
    {
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();

        /// <summary>Cancelled when the user presses Ctrl+C or the pipeline stops.</summary>
        protected CancellationToken CancelToken => _cancel.Token;

        /// <summary>The object an error is reported against, usually the resource id.</summary>
        protected virtual object ErrorTarget => null;

        /// <summary>When true, every failure stops the command, as for Connect-Site24x7.</summary>
        protected virtual bool ErrorsAreTerminating => false;

        protected sealed override void BeginProcessing() => Run(BeginProcessingCore, terminating: true);

        protected sealed override void ProcessRecord() => Run(ProcessRecordCore, terminating: ErrorsAreTerminating);

        protected override void StopProcessing() => _cancel.Cancel();

        /// <summary>Checks that depend only on parameters given on the command line, not on pipeline input.</summary>
        protected virtual void BeginProcessingCore()
        {
        }

        /// <summary>The work done for each pipeline input.</summary>
        protected abstract void ProcessRecordCore();

        public void Dispose() => _cancel.Dispose();

        private void Run(Action action, bool terminating)
        {
            ErrorRecord error;
            try
            {
                action();
                return;
            }
            catch (OperationCanceledException) when (_cancel.IsCancellationRequested)
            {
                throw new PipelineStoppedException();
            }
            catch (Site24x7ApiException ex)
            {
                error = new ErrorRecord(ex, "Site24x7.Api" + (ex.ErrorCode.HasValue ? "." + ex.ErrorCode.Value.ToString(CultureInfo.InvariantCulture) : string.Empty), Categorize(ex.StatusCode), ErrorTarget)
                {
                    ErrorDetails = new ErrorDetails(ex.Message),
                };
            }
            catch (Site24x7ArgumentException ex)
            {
                error = new ErrorRecord(ex, "Site24x7.InvalidArgument", ErrorCategory.InvalidArgument, ErrorTarget);
            }
            catch (Site24x7Exception ex)
            {
                error = new ErrorRecord(ex, "Site24x7.Error", ErrorCategory.InvalidOperation, ErrorTarget);
            }
            catch (HttpRequestException ex)
            {
                error = new ErrorRecord(ex, "Site24x7.Connection", ErrorCategory.ConnectionError, ErrorTarget);
            }
            catch (TimeoutException ex)
            {
                error = new ErrorRecord(ex, "Site24x7.Timeout", ErrorCategory.OperationTimeout, ErrorTarget);
            }
            catch (Exception ex) when (!(ex is RuntimeException) && !(ex is OperationCanceledException) &&
                                       !(ex is OutOfMemoryException) && !(ex is StackOverflowException))
            {
                // Anything else (an ArgumentException from HttpClient, say) is still about this one item;
                // PowerShell's own exceptions, such as a stopped pipeline, pass through untouched.
                error = new ErrorRecord(ex, "Site24x7.Unexpected", ErrorCategory.NotSpecified, ErrorTarget);
            }

            if (terminating) ThrowTerminatingError(error);
            else WriteError(error);
        }

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

        protected bool IsBound(string parameterName) => MyInvocation.BoundParameters.ContainsKey(parameterName);

        protected void ThrowNotConnected()
        {
            ThrowTerminatingError(new ErrorRecord(
                new Site24x7Exception("You are not connected to Site24x7. Run Connect-Site24x7 first, or pass -Session."),
                "Site24x7.NotConnected",
                ErrorCategory.AuthenticationError,
                null));
        }
    }

    /// <summary>
    /// Base class for every cmdlet that calls the Site24x7 API. It resolves which session to use,
    /// sends the request, and writes the results.
    /// </summary>
    public abstract class Site24x7Cmdlet : Site24x7CmdletBase
    {
        /// <summary>hh:mm, 00:00 to 23:59.</summary>
        protected const string TimeOfDayPattern = "^([01][0-9]|2[0-3]):[0-5][0-9]$";

        private static readonly Regex PathSegmentPattern = new Regex(@"^[A-Za-z0-9_\-.:@~ ]+$", RegexOptions.CultureInvariant);

        /// <summary>A session from Connect-Site24x7 -PassThru. Defaults to the session of the last Connect-Site24x7 in this runspace.</summary>
        [Parameter]
        [ValidateNotNull]
        public Site24x7Session Session { get; set; }

        protected Site24x7Session ActiveSession
        {
            get
            {
                var session = Session ?? Site24x7SessionState.Current;
                if (session == null) ThrowNotConnected();
                return session;
            }
        }

        protected object Call(string method, string path, object body = null, IDictionary query = null, string apiVersion = null, bool raw = false)
        {
            var trace = new RequestTrace { Verbose = WriteVerbose, Debug = WriteDebug };
            return Site24x7Client.Invoke(ActiveSession, method, path, query, body, apiVersion, raw, trace, CancelToken);
        }

        /// <summary>
        /// The type name added to output objects so the module's formatting applies, for example
        /// "Site24x7.Monitor". Null leaves objects as plain PSCustomObject.
        /// </summary>
        protected virtual string OutputTypeName => null;

        /// <summary>The items of an API result: each element of an array, or the result itself.</summary>
        protected static IEnumerable<object> Items(object data)
        {
            if (data == null) yield break;
            if (data is object[] items)
            {
                foreach (var item in items) yield return item;
            }
            else
            {
                yield return data;
            }
        }

        /// <summary>Writes API data to the pipeline, one object per array element.</summary>
        protected void WriteData(object data)
        {
            foreach (var item in Items(data)) WriteObject(Tag(item));
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

        /// <summary>
        /// Escapes an id or code for use as one segment of a URL path. Values that could change which
        /// path is called, such as "..", "a/b" or an empty string, are refused.
        /// </summary>
        protected static string Seg(string value)
        {
            if (string.IsNullOrEmpty(value) || value == "." || value == ".." || !PathSegmentPattern.IsMatch(value))
            {
                throw new Site24x7ArgumentException($"'{value}' is not a valid id. Ids contain only letters, digits, spaces and - _ . : @ ~.");
            }
            return Uri.EscapeDataString(value);
        }

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

        /// <summary>Copies every entry of a hashtable into the body, overriding what is already there.</summary>
        protected static void Merge(IDictionary body, IDictionary extra)
        {
            if (extra == null) return;
            foreach (DictionaryEntry entry in extra)
            {
                body[Convert.ToString(entry.Key, CultureInfo.InvariantCulture)] = entry.Value is PSObject pso && !(pso.BaseObject is PSCustomObject) ? pso.BaseObject : entry.Value;
            }
        }

        /// <summary>A request body whose field names ignore case, so a -Property entry replaces the cmdlet's own field.</summary>
        protected static OrderedDictionary NewBody() => new OrderedDictionary(StringComparer.OrdinalIgnoreCase);

        /// <summary>A query string table whose keys ignore case, so a -Query entry replaces the cmdlet's own value.</summary>
        protected static Hashtable NewQuery() => new Hashtable(StringComparer.OrdinalIgnoreCase);

        /// <summary>Copies -Query entries into a table from NewQuery, replacing values with the same name.</summary>
        protected static void MergeQuery(Hashtable query, IDictionary extra)
        {
            if (extra == null) return;
            foreach (DictionaryEntry e in extra)
            {
                query[Convert.ToString(e.Key, CultureInfo.InvariantCulture)] = e.Value;
            }
        }

        /// <summary>A date as the API expects it in bodies and query strings.</summary>
        protected static string ApiDate(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        /// <summary>Converts @{ 'X-Name' = 'value' } into the API's [{ name, value }] list.</summary>
        protected static object[] ToNameValueList(IDictionary headers)
        {
            if (headers == null) return null;
            var list = new List<object>();
            foreach (DictionaryEntry e in headers)
            {
                list.Add(new Hashtable { { "name", Convert.ToString(e.Key, CultureInfo.InvariantCulture) }, { "value", Convert.ToString(e.Value, CultureInfo.InvariantCulture) } });
            }
            return list.ToArray();
        }
    }
}
