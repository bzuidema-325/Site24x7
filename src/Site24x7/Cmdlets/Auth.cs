using System;
using System.Management.Automation;
using System.Security;

namespace Site24x7.Cmdlets
{
    /// <summary>Base for the sign-in cmdlets, which talk to Zoho Accounts rather than Site24x7.</summary>
    public abstract class ZohoAuthCmdlet : Site24x7CmdletBase
    {
        /// <summary>The data center the account lives in. Defaults to US.</summary>
        [Parameter]
        public Site24x7DataCenter DataCenter { get; set; } = Site24x7DataCenter.US;

        /// <summary>Overrides the Zoho Accounts URL for the data center. Rarely needed. Must be https.</summary>
        [Parameter]
        public Uri AccountsUri { get; set; }

        protected Uri ResolvedAccountsUri =>
            Secrets.CheckSecureUri(AccountsUri, nameof(AccountsUri)) ?? DataCenterEndpoints.AccountsBase(DataCenter);

        protected override void BeginProcessingCore() => _ = ResolvedAccountsUri;
    }

    /// <summary>
    /// <para type="synopsis">Exchanges a one-time grant code for a refresh token.</para>
    /// <para type="description">Create a Self Client in the Zoho API console for your data center, generate a grant code with the Site24x7 scopes you need, and pass it here within its validity window. Store the RefreshToken of the result in a secret store: Connect-Site24x7 uses it to sign in from then on. The tokens are SecureStrings.</para>
    /// </summary>
    /// <example>
    /// <code>$t = Request-Site24x7RefreshToken -ClientId $id -ClientSecret (Get-Secret ZohoClientSecret) -GrantCode $code -DataCenter EU
    /// Set-Secret Site24x7RefreshToken $t.RefreshToken</code>
    /// </example>
    [Cmdlet(VerbsLifecycle.Request, "Site24x7RefreshToken")]
    [OutputType(typeof(ZohoToken))]
    public sealed class RequestSite24x7RefreshTokenCommand : ZohoAuthCmdlet
    {
        /// <summary>The Self Client's client id.</summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string ClientId { get; set; }

        /// <summary>The Self Client's client secret.</summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNull]
        public SecureString ClientSecret { get; set; }

        /// <summary>The grant code generated in the Zoho API console.</summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string GrantCode { get; set; }

        protected override bool ErrorsAreTerminating => true;

        protected override void ProcessRecordCore()
        {
            var token = ZohoOAuth.ExchangeGrantCode(ResolvedAccountsUri, ClientId, Secrets.ToPlain(ClientSecret), GrantCode, CancelToken);
            WriteObject(token.ToPublic());
        }
    }

    /// <summary>
    /// <para type="synopsis">Gets a new access token from a refresh token.</para>
    /// <para type="description">Zoho allows at most 10 new access tokens per refresh token in 10 minutes. Connect-Site24x7 renews tokens for you, so this is only needed to use a token outside the module.</para>
    /// </summary>
    [Cmdlet(VerbsLifecycle.Request, "Site24x7AccessToken")]
    [OutputType(typeof(ZohoToken))]
    public sealed class RequestSite24x7AccessTokenCommand : ZohoAuthCmdlet
    {
        /// <summary>The Zoho Self Client's client id.</summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string ClientId { get; set; }

        /// <summary>The Self Client's client secret.</summary>
        [Parameter(Mandatory = true)]
        [ValidateNotNull]
        public SecureString ClientSecret { get; set; }

        /// <summary>The refresh token, for example from Request-Site24x7RefreshToken or a secret store.</summary>
        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNull]
        public SecureString RefreshToken { get; set; }

        protected override void ProcessRecordCore()
        {
            var token = ZohoOAuth.RefreshAccessToken(ResolvedAccountsUri, ClientId, Secrets.ToPlain(ClientSecret), Secrets.ToPlain(RefreshToken), CancelToken);
            WriteObject(token.ToPublic());
        }
    }

    /// <summary>
    /// <para type="synopsis">Revokes a refresh token so it can no longer be used.</para>
    /// </summary>
    [Cmdlet(VerbsSecurity.Revoke, "Site24x7RefreshToken", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
    [OutputType(typeof(string))]
    public sealed class RevokeSite24x7RefreshTokenCommand : ZohoAuthCmdlet
    {
        /// <summary>The refresh token, for example from Request-Site24x7RefreshToken or a secret store.</summary>
        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNull]
        public SecureString RefreshToken { get; set; }

        protected override void ProcessRecordCore()
        {
            if (ShouldProcess("refresh token", "Revoke"))
            {
                WriteObject(ZohoOAuth.RevokeRefreshToken(ResolvedAccountsUri, Secrets.ToPlain(RefreshToken), CancelToken));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Signs in to Site24x7 for this PowerShell session.</para>
    /// <para type="description">With a client id, client secret and refresh token, the session renews its access token automatically before it expires. With only an access token, the session lasts until that token expires (one hour). Every Site24x7 cmdlet in this runspace uses the session unless you pass -Session. Secrets are SecureStrings, which is what Get-Secret from Microsoft.PowerShell.SecretManagement returns.</para>
    /// </summary>
    /// <example>
    /// <code>Connect-Site24x7 -ClientId $id -ClientSecret (Get-Secret ZohoClientSecret) -RefreshToken (Get-Secret Site24x7RefreshToken) -DataCenter EU</code>
    /// </example>
    [Cmdlet(VerbsCommunications.Connect, "Site24x7", DefaultParameterSetName = RefreshTokenSet)]
    [OutputType(typeof(Site24x7Session))]
    public sealed class ConnectSite24x7Command : ZohoAuthCmdlet
    {
        private const string RefreshTokenSet = "RefreshToken";
        private const string AccessTokenSet = "AccessToken";

        /// <summary>The Zoho Self Client's client id.</summary>
        [Parameter(Mandatory = true, ParameterSetName = RefreshTokenSet)]
        [ValidateNotNullOrEmpty]
        public string ClientId { get; set; }

        /// <summary>The Self Client's client secret.</summary>
        [Parameter(Mandatory = true, ParameterSetName = RefreshTokenSet)]
        [ValidateNotNull]
        public SecureString ClientSecret { get; set; }

        /// <summary>The refresh token, for example from Request-Site24x7RefreshToken or a secret store.</summary>
        [Parameter(Mandatory = true, ParameterSetName = RefreshTokenSet)]
        [ValidateNotNull]
        public SecureString RefreshToken { get; set; }

        /// <summary>An access token to use until it expires, instead of a refresh token.</summary>
        [Parameter(Mandatory = true, ParameterSetName = AccessTokenSet)]
        [ValidateNotNull]
        public SecureString AccessToken { get; set; }

        /// <summary>Overrides the Site24x7 API URL for the data center. Rarely needed. Must be https.</summary>
        [Parameter]
        public Uri ApiBaseUri { get; set; }

        /// <summary>An MSP customer or Business Unit id (zaaid) to act on. See Get-Site24x7MspCustomer and Set-Site24x7Context.</summary>
        [Parameter]
        [ValidatePattern("^[A-Za-z0-9_-]+$")]
        public string CustomerId { get; set; }

        /// <summary>Returns the session object, for use with -Session.</summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }

        protected override bool ErrorsAreTerminating => true;

        protected override void BeginProcessingCore()
        {
            base.BeginProcessingCore();
            Secrets.CheckSecureUri(ApiBaseUri, nameof(ApiBaseUri));
        }

        protected override void ProcessRecordCore()
        {
            var apiBase = Secrets.CheckSecureUri(ApiBaseUri, nameof(ApiBaseUri)) ?? DataCenterEndpoints.ApiBase(DataCenter);
            TokenSource tokens;
            if (ParameterSetName == AccessTokenSet)
            {
                tokens = new TokenSource(ResolvedAccountsUri, null, null, null, Secrets.ToPlain(AccessToken));
            }
            else
            {
                tokens = new TokenSource(ResolvedAccountsUri, ClientId, Secrets.ToPlain(ClientSecret), Secrets.ToPlain(RefreshToken), null);
                WriteVerbose($"Requesting an access token from {tokens.AccountsUri}");
                tokens.GetAccessToken(CancelToken);
            }

            var session = new Site24x7Session(DataCenter, apiBase, tokens, CustomerId);
            Site24x7SessionState.Current = session;
            if (PassThru) WriteObject(session);
        }
    }

    /// <summary>
    /// <para type="synopsis">Ends the current Site24x7 session in this runspace.</para>
    /// </summary>
    [Cmdlet(VerbsCommunications.Disconnect, "Site24x7", SupportsShouldProcess = true)]
    public sealed class DisconnectSite24x7Command : Site24x7CmdletBase
    {
        /// <summary>Also revokes the session's refresh token at Zoho, so it can never be used again.</summary>
        [Parameter]
        public SwitchParameter RevokeRefreshToken { get; set; }

        protected override void ProcessRecordCore()
        {
            var session = Site24x7SessionState.Current;
            if (session == null || !ShouldProcess(session.ToString(), "Disconnect")) return;

            // The session ends even when revoking fails, so a failed revoke never leaves you signed in;
            // the failure is still reported.
            try
            {
                if (RevokeRefreshToken && session.Tokens.CanRefresh && ShouldProcess("refresh token", "Revoke"))
                {
                    ZohoOAuth.RevokeRefreshToken(session.AccountsUri, session.Tokens.RefreshToken, CancelToken);
                }
            }
            finally
            {
                Site24x7SessionState.Current = null;
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">Shows the current Site24x7 session in this runspace.</para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "Site24x7Session")]
    [OutputType(typeof(Site24x7Session))]
    public sealed class GetSite24x7SessionCommand : Site24x7CmdletBase
    {
        protected override void ProcessRecordCore()
        {
            var session = Site24x7SessionState.Current;
            if (session != null) WriteObject(session);
        }
    }

    /// <summary>
    /// <para type="synopsis">Switches the current session to an MSP customer or Business Unit, or back to your own account.</para>
    /// <para type="description">Requests then carry the zaaid cookie, as described under MSP API and Business Units API in the Site24x7 documentation. The current session is replaced by a new one; a session saved earlier with -PassThru keeps acting on its own customer.</para>
    /// </summary>
    /// <example>
    /// <code>Get-Site24x7MspCustomer -Name 'Contoso*' | Set-Site24x7Context</code>
    /// </example>
    [Cmdlet(VerbsCommon.Set, "Site24x7Context", DefaultParameterSetName = CustomerSet, SupportsShouldProcess = true)]
    [OutputType(typeof(Site24x7Session))]
    public sealed class SetSite24x7ContextCommand : Site24x7CmdletBase
    {
        private const string CustomerSet = "Customer";
        private const string ResetSet = "Reset";

        /// <summary>The customer's zaaid. MSP customer and Business Unit objects bind it from their zaaid property.</summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = CustomerSet, ValueFromPipelineByPropertyName = true)]
        [Alias("zaaid")]
        [ValidatePattern("^[A-Za-z0-9_-]+$")]
        public string CustomerId { get; set; }

        /// <summary>Returns to the account the token belongs to.</summary>
        [Parameter(Mandatory = true, ParameterSetName = ResetSet)]
        public SwitchParameter Reset { get; set; }

        /// <summary>Also returns the new session, to save and pass to -Session later.</summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }

        protected override bool ErrorsAreTerminating => true;

        protected override void ProcessRecordCore()
        {
            var session = Site24x7SessionState.Current;
            if (session == null) ThrowNotConnected();

            var target = Reset ? "your own account" : "customer " + CustomerId;
            if (!ShouldProcess(target, "Switch the Site24x7 session to")) return;

            var switched = session.WithCustomer(Reset ? null : CustomerId);
            Site24x7SessionState.Current = switched;
            if (PassThru) WriteObject(switched);
        }
    }
}
