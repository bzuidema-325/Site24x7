using System;
using System.Net;

namespace Site24x7
{
    /// <summary>A failure reported by the module itself, such as a missing session.</summary>
    public class Site24x7Exception : Exception
    {
        public Site24x7Exception(string message) : base(message)
        {
        }

        public Site24x7Exception(string message, Exception inner) : base(message, inner)
        {
        }
    }

    /// <summary>A parameter value the module cannot use, found before anything is sent.</summary>
    public class Site24x7ArgumentException : Site24x7Exception
    {
        public Site24x7ArgumentException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// A failure reported by the Site24x7 or Zoho API. ErrorCode and ErrorInfo carry the
    /// "code" and "error_info" fields from the response body when the API sends them.
    /// </summary>
    public class Site24x7ApiException : Site24x7Exception
    {
        public Site24x7ApiException(string message, HttpStatusCode? statusCode, int? errorCode, object errorInfo, string responseBody)
            : base(message)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
            ErrorInfo = errorInfo;
            ResponseBody = responseBody;
        }

        public HttpStatusCode? StatusCode { get; }

        public int? ErrorCode { get; }

        public object ErrorInfo { get; }

        public string ResponseBody { get; }
    }
}
