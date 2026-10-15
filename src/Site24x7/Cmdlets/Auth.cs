using System;
using System.Management.Automation;
using System.Net.Http;

namespace Site24x7.Cmdlets
{
    /// <summary>Base for the sign-in cmdlets, which talk to Zoho Accounts rather than Site24x7.</summary>
    public abstract class ZohoAuthCmdlet : PSCmdlet
    {
        /// <summary>The data center the account lives in. Defaults to US.</summary>
        [Parameter]
        public Site24x7DataCenter DataCenter { get; set; } = Site24x7DataCenter.US;

        /// <summary>Overrides the Zoho Accounts URL for the data center. Rarely needed.</summary>
        [Parameter]
        public Uri AccountsUri { get; set; }

        protected Uri ResolvedAccountsUri
        {
            get
            {
                var uri = AccountsUri ?? DataCenterEndpoints.AccountsBase(DataCenter);
                var s = uri.ToString();
                return s.EndsWith("/", StringComparison.Ordinal) ? uri : new Uri(s + "/");
            }
        }

        protected sealed override void ProcessRecord()
        {
            try
            {
                ProcessRecordCore();
            }
            catch (Site24x7Exception ex)
            {
                ThrowTerminatingError(new ErrorRecord(ex, "Site24x7.Auth", ErrorCategory.AuthenticationError, null));
            }
            catch (HttpRequestException ex)
            {
                ThrowTerminatingError(new ErrorRecord(ex, "Site24x7.Connection", ErrorCategory.ConnectionError, null));
            }
        }

        protected abstract void ProcessRecordCore();
    }

    /// <summary>
    /// <para type="synopsis">Exchanges a one-time grant code for an access token and a refresh token.</para>
    /// <para type="description">Create a Self Client in the Zoho API console for your data center, generate a grant code with the Site24x7 scopes you need, and pass it here within its validity window. Keep the refresh token: Connect-Site24x7 uses it to sign in from then on.</para>
    /// </summary>
    [Cmdlet(VerbsData.Initialize, "ZohoToken")]
    [OutputType(typeof(PSObject))]
    public sealed class InitializeZohoTokenCommand : ZohoAuthCmdlet
    {
        [Parameter(Mandatory = true)]
        [Alias("client_id")]
        [ValidateNotNullOrEmpty]
        public string ClientId { get; set; }

        [Parameter(Mandatory = true)]
        [Alias("client_secret")]
        [ValidateNotNullOrEmpty]
        public string ClientSecret { get; set; }

        [Parameter(Mandatory = true)]
        [Alias("oneTimeCode", "Code")]
        [ValidateNotNullOrEmpty]
        public string GrantCode { get; set; }

        protected override void ProcessRecordCore()
        {
            var token = ZohoOAuth.ExchangeGrantCode(ResolvedAccountsUri, ClientId, ClientSecret, GrantCode);
            var result = new PSObject();
            result.Properties.Add(new PSNoteProperty("access_token", token.AccessToken));
            result.Properties.Add(new PSNoteProperty("refresh_token", token.RefreshToken));
            result.Properties.Add(new PSNoteProperty("access_token_expires", token.ExpiresAt));
            result.Properties.Add(new PSNoteProperty("api_domain", token.ApiDomain));
            result.Properties.Add(new PSNoteProperty("token_type", token.TokenType));
            result.Properties.Add(new PSNoteProperty("scope", token.Scope));
            WriteObject(result);
        }
    }

    /// <summary>
    /// <para type="synopsis">Gets a new access token from a refresh token.</para>
    /// <para type="description">Zoho allows at most 10 new access tokens per refresh token in 10 minutes. Connect-Site24x7 does this for you automatically.</para>
    /// </summary>
    [Cmdlet(VerbsLifecycle.Request, "ZohoAccessToken")]
    [OutputType(typeof(PSObject))]
    public sealed class RequestZohoAccessTokenCommand : ZohoAuthCmdlet
    {
        [Parameter(Mandatory = true)]
        [Alias("client_id")]
        [ValidateNotNullOrEmpty]
        public string ClientId { get; set; }

        [Parameter(Mandatory = true)]
        [Alias("client_secret")]
        [ValidateNotNullOrEmpty]
        public string ClientSecret { get; set; }

        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
        [Alias("refresh_token")]
        [ValidateNotNullOrEmpty]
        public string RefreshToken { get; set; }

        protected override void ProcessRecordCore()
        {
            var token = ZohoOAuth.RefreshAccessToken(ResolvedAccountsUri, ClientId, ClientSecret, RefreshToken);
            var result = new PSObject();
            result.Properties.Add(new PSNoteProperty("access_token", token.AccessToken));
            result.Properties.Add(new PSNoteProperty("access_token_expires", token.ExpiresAt));
            result.Properties.Add(new PSNoteProperty("api_domain", token.ApiDomain));
            result.Properties.Add(new PSNoteProperty("token_type", token.TokenType));
            WriteObject(result);
        }
    }

    /// <summary>
    /// <para type="synopsis">Revokes a refresh token so it can no longer be used.</para>
    /// </summary>
    [Cmdlet(VerbsSecurity.Revoke, "ZohoRefreshToken", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
    [OutputType(typeof(string))]
    public sealed class RevokeZohoRefreshTokenCommand : ZohoAuthCmdlet
    {
        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
        [Alias("refresh_token")]
        [ValidateNotNullOrEmpty]
        public string RefreshToken { get; set; }

        protected override void ProcessRecordCore()
        {
            if (ShouldProcess("refresh token", "Revoke"))
            {
                WriteObject(ZohoOAuth.RevokeRefreshToken(ResolvedAccountsUri, RefreshToken));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Signs in to Site24x7 for this PowerShell session.</para>
    /// <para type="description">With a client id, client secret and refresh token, the session renews its access token automatically before it expires. With only an access token, the session lasts until that token expires (one hour). Every Site24x7 cmdlet uses this session unless you pass -Session or -AccessToken.</para>
    /// </summary>
    /// <example>
    /// <code>Connect-Site24x7 -ClientId $id -ClientSecret $secret -RefreshToken $refresh -DataCenter EU</code>
    /// </example>
    [Cmdlet(VerbsCommunications.Connect, "Site24x7", DefaultParameterSetName = RefreshTokenSet)]
    [OutputType(typeof(Site24x7Session))]
    public sealed class ConnectSite24x7Command : ZohoAuthCmdlet
    {
        private const string RefreshTokenSet = "RefreshToken";
        private const string AccessTokenSet = "AccessToken";

        [Parameter(Mandatory = true, ParameterSetName = RefreshTokenSet)]
        [Alias("client_id")]
        [ValidateNotNullOrEmpty]
        public string ClientId { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = RefreshTokenSet)]
        [Alias("client_secret")]
        [ValidateNotNullOrEmpty]
        public string ClientSecret { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = RefreshTokenSet)]
        [Alias("refresh_token")]
        [ValidateNotNullOrEmpty]
        public string RefreshToken { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = AccessTokenSet)]
        [Alias("access_token")]
        [ValidateNotNullOrEmpty]
        public string AccessToken { get; set; }

        /// <summary>Overrides the Site24x7 API URL for the data center. Rarely needed.</summary>
        [Parameter]
        public Uri ApiBaseUri { get; set; }

        /// <summary>An MSP customer or Business Unit id (zaaid) to act on. See Get-Site24x7MspCustomer.</summary>
        [Parameter]
        [Alias("zaaid")]
        public string CustomerId { get; set; }

        /// <summary>Returns the session object.</summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }

        protected override void ProcessRecordCore()
        {
            var session = new Site24x7Session(DataCenter, ApiBaseUri, ResolvedAccountsUri)
            {
                CustomerId = string.IsNullOrEmpty(CustomerId) ? null : CustomerId,
            };

            if (ParameterSetName == AccessTokenSet)
            {
                session.AccessToken = AccessToken;
            }
            else
            {
                session.ClientId = ClientId;
                session.ClientSecret = ClientSecret;
                session.RefreshToken = RefreshToken;
                WriteVerbose($"Requesting an access token from {session.AccountsUri}");
                session.GetAccessToken(forceRefresh: true);
            }

            Site24x7SessionState.Current = session;
            if (PassThru) WriteObject(session);
        }
    }

    /// <summary>
    /// <para type="synopsis">Ends the current Site24x7 session.</para>
    /// </summary>
    [Cmdlet(VerbsCommunications.Disconnect, "Site24x7", SupportsShouldProcess = true)]
    public sealed class DisconnectSite24x7Command : PSCmdlet
    {
        /// <summary>Also revokes the session's refresh token at Zoho, so it can never be used again.</summary>
        [Parameter]
        public SwitchParameter RevokeRefreshToken { get; set; }

        protected override void ProcessRecord()
        {
            var session = Site24x7SessionState.Current;
            if (session == null) return;

            if (RevokeRefreshToken && !string.IsNullOrEmpty(session.RefreshToken) && ShouldProcess("refresh token", "Revoke"))
            {
                try
                {
                    ZohoOAuth.RevokeRefreshToken(session.AccountsUri, session.RefreshToken);
                }
                catch (Exception ex) when (ex is Site24x7Exception || ex is HttpRequestException)
                {
                    WriteError(new ErrorRecord(ex, "Site24x7.Revoke", ErrorCategory.AuthenticationError, null));
                }
            }

            Site24x7SessionState.Current = null;
        }
    }

    /// <summary>
    /// <para type="synopsis">Shows the current Site24x7 session.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "Site24x7Session")]
    [OutputType(typeof(Site24x7Session))]
    public sealed class GetSite24x7SessionCommand : PSCmdlet
    {
        protected override void ProcessRecord()
        {
            if (Site24x7SessionState.Current != null) WriteObject(Site24x7SessionState.Current);
        }
    }

    /// <summary>
    /// <para type="synopsis">Switches the current session to an MSP customer or Business Unit, or back to your own account.</para>
    /// <para type="description">Requests then carry the zaaid cookie, as described under MSP API and Business Units API in the Site24x7 documentation.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "Site24x7Context", DefaultParameterSetName = "Customer")]
    [OutputType(typeof(Site24x7Session))]
    public sealed class SetSite24x7ContextCommand : PSCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Customer", ValueFromPipelineByPropertyName = true)]
        [Alias("zaaid")]
        [ValidateNotNullOrEmpty]
        public string CustomerId { get; set; }

        /// <summary>Returns to the account the token belongs to.</summary>
        [Parameter(Mandatory = true, ParameterSetName = "Reset")]
        public SwitchParameter Reset { get; set; }

        [Parameter]
        public SwitchParameter PassThru { get; set; }

        protected override void ProcessRecord()
        {
            var session = Site24x7SessionState.Current;
            if (session == null)
            {
                ThrowTerminatingError(new ErrorRecord(
                    new Site24x7Exception("You are not connected to Site24x7. Run Connect-Site24x7 first."),
                    "Site24x7.NotConnected", ErrorCategory.AuthenticationError, null));
                return;
            }

            session.CustomerId = Reset ? null : CustomerId;
            if (PassThru) WriteObject(session);
        }
    }
}
