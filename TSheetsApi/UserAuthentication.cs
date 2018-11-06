/*
Copyright (c) 2014 TSheets.com, LLC.

Permission is hereby granted, free of charge, to any person
obtaining a copy of this software and associated documentation
files (the "Software"), to deal in the Software without
restriction, including without limitation the rights to use,
copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the
Software is furnished to do so, subject to the following
conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Threading;
using System.Windows.Forms;

namespace TSheets
{
    /// <summary>
    /// Simple implementation of IOAuth2 using a static access token generated
    /// from the API add-on preferences
    /// </summary>
    public class StaticAuthentication : IOAuth2
    {
        public string AccessToken
        {
            get;
            set;
        }

        public StaticAuthentication(string accessToken)
        {
            AccessToken = accessToken;
        }

        public string GetAccessToken()
        {
            return AccessToken;
        }
    }

    /// <summary>
    /// TokenChanged event arguments
    /// </summary>
    public class TokenChangedEventArgs : EventArgs
    {
        internal TokenChangedEventArgs(OAuthToken token)
        {
            CurrentToken = token;
        }

        /// <summary>
        /// The new auth token. May be null if the current token is no longer valid
        /// </summary>
        public OAuthToken CurrentToken
        {
            get;
            internal set;
        }
    }

    /// <summary>
    /// Implementation of the IOAuth2Info interface for user/desktop authentication.
    /// Prompts the user for authentication information and handles the client side
    /// implementation of the OAuth2 protocol to the TSheets API.
    /// </summary>
    public class UserAuthentication : IOAuth2
    {
        // Authentication dialog customizations
        #region Auth Dialog preferences
        private System.Drawing.Size AuthDialogSize = new System.Drawing.Size(600, 600);
        private const string AuthDialogTitle = "TSheets Authentication";
        #endregion


        private string _state = Guid.NewGuid().ToString();
        private OAuthToken _token;
        private ConnectionInfo _connectionInfo;


        /// <summary>
        /// UserAuthentication constructor
        /// </summary>        
        /// <param name="connectionInfo">Server connection information</param>
        /// <param name="existingToken">Existing token to use if we already have one. May be null</param>
        public UserAuthentication(ConnectionInfo connectionInfo, OAuthToken existingToken = null)
        {
            _connectionInfo = connectionInfo;
            _token = existingToken;
        }

        /// <summary>
        /// Implements GetAccessToken interface
        /// </summary>
        /// <returns>The access token to use</returns>
        public string GetAccessToken()
        {
            return GetToken().access_token;
        }

        /// <summary>
        /// Returns the token if valid. Otherwise attempts to retrieve/refresh a new one
        /// </summary>
        /// <returns></returns>
        public OAuthToken GetToken()
        {
            if (_token == null)
            {
                return Authenticate();
            }
            else if (_token.NeedsRefresh())
            {
                var refreshToken = _token.refresh_token;
                _token = null;
                try
                {
                    _token = RestClient.RefreshToken(refreshToken, _connectionInfo);
                }
                catch (ApiException ex)
                {
                    if (ex.HttpCode != "404")
                    {
                        // unexpected refresh error
                        throw;
                    }
                    // else the server didn't like the refresh token so it's now invalid
                }
                
                OnTokenChanged(_token);
                return _token;
            }
            else
            {
                return _token;
            }
        }

        /// <summary>
        /// TokenChanged event to notify users when the auth token changes
        /// </summary>
        public event EventHandler<TokenChangedEventArgs> TokenChanged;

        /// <summary>
        /// Handles sending the TokenChanged event
        /// </summary>
        /// <param name="newToken">the new OAuth token</param>
        protected virtual void OnTokenChanged(OAuthToken newToken)
        {
            if (TokenChanged != null)
            {
                TokenChanged(this, new TokenChangedEventArgs(newToken));
            }
        }

        /// <summary>
        /// Runs the authentication process in a hosted browser form
        /// </summary>
        /// <returns>authenticated OAuthToken</returns>
        private OAuthToken Authenticate()
        {
            Exception threadException = null;

            _token = null;

            // The WebBrowser must run on an STA thread, so to be safe just fire up our own
            var authThread = new Thread(() =>
            {
                try
                {
                    _token = DoAuthentication();
                }
                catch (Exception ex)
                {
                    // capture it so we can throw it back on the calling thread
                    threadException = ex;
                }
            });

            authThread.SetApartmentState(ApartmentState.STA);
            authThread.Start();
            authThread.Join();

            OnTokenChanged(_token);
            if (threadException != null)
            {
                throw threadException;
            }

            return _token;
        }

        /// <summary>
        /// Performs the actual authentication in an embedded WebBrowser form
        /// </summary>
        /// <returns>the oauth token</returns>
        private OAuthToken DoAuthentication()
        {
            OAuthToken authToken = null;
            Exception authException = null;

            string authUri = RestClient.BuildAuthorizationUri(_connectionInfo, _state);

            // Create the form & embedded browser
            Form parentForm = new Form();
            parentForm.MaximizeBox = false;
            parentForm.MinimizeBox = false;
            parentForm.Size = AuthDialogSize;
            parentForm.Text = AuthDialogTitle;
            parentForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            parentForm.StartPosition = FormStartPosition.CenterScreen;
            WebBrowser webBrowser = new WebBrowser();
            webBrowser.Size = parentForm.Size;
            parentForm.Controls.Add(webBrowser);

            webBrowser.Navigated += (object sender, WebBrowserNavigatedEventArgs e) =>
            {
                try
                {
                    if (e.Url.AbsoluteUri.StartsWith(_connectionInfo.RedirectUri))
                    {
                        // we've been redirected, time to get an access token
                        var query = System.Web.HttpUtility.ParseQueryString(e.Url.Query);

                        if (string.Equals(_state, query["state"]) && !string.IsNullOrWhiteSpace(query["code"]))
                        {
                            if (!string.Equals(query["state"], _state, StringComparison.Ordinal))
                            {
                                throw new ApiException("Authorization code state mismatch", e.Url.ToString());
                            }
                            string authCode = query["code"];

                            // exchange the code for a token
                            authToken = RestClient.GetAccessToken(authCode, _connectionInfo);
                        }
                        else if (!string.IsNullOrWhiteSpace(query["error"]))
                        {
                            // if there was a problem, or the authorization was rejected, redirect will look like this:
                            // https://somedomain.com/callback?error=SOME_ERROR&error_description=SOME_DESCRIPTION

                            throw new ApiException("Error getting OAuth code", e.Url.ToString(), "200", query["error_description"], query["error"]);
                        }
                        else
                        {
                            throw new ApiException("Unexpected OAuth response", e.Url.ToString(), "200", string.Empty, string.Empty);
                        }

                        // stop running the form
                        Application.ExitThread();
                    }
                }
                catch (Exception ex)
                {
                    // capture it so we can send it back outside of the message loop event
                    authException = ex;
                    Application.ExitThread();
                }
            };

            // Now we can switch over to the auth dialog
            webBrowser.Navigate(authUri);
            Application.Run(parentForm);

            if (authException != null)
            {
                throw authException;
            }
            else if (authToken == null)
            {
                // if we have no authToken, the user probably closed the dialog              
                throw new ApiException("User closed authentication dialog", authUri);
            }
            else
            {
                return authToken;
            }
        }
    }
}
