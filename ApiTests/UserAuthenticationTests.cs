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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TSheets;

namespace TSheets
{
    [TestClass]
    public class UserAuthenticationTests
    {
        private string _baseUri = "https://rest.tsheets.com/api/v1";
        private string _clientId;
        private string _redirectUri;
        private string _clientSecret;

        [TestInitialize]
        public void GetServerSettings()
        {
            _clientId = Environment.GetEnvironmentVariable("TSHEETS_CLIENTID");
            _redirectUri = Environment.GetEnvironmentVariable("TSHEETS_REDIRECTURI");
            _clientSecret = Environment.GetEnvironmentVariable("TSHEETS_CLIENTSECRET");

            Assert.IsNotNull(_clientId);
            Assert.IsNotNull(_redirectUri);
            Assert.IsNotNull(_clientSecret);
        }

        [TestMethod]        
        public void TestFirstTimeAuth()
        {
            var connection = new ConnectionInfo(_baseUri, _clientId, _redirectUri, _clientSecret);
            var userAuth = new UserAuthentication(connection);

            var authToken = userAuth.GetAccessToken();
            Assert.IsNotNull(authToken);
        }

        [TestMethod]
        public void TestTokenRefresh()
        {
            var connection = new ConnectionInfo(_baseUri, _clientId, _redirectUri, _clientSecret);
            var userAuth = new UserAuthentication(connection);

            var authToken = userAuth.GetToken();

            var refreshedAuthToken = RestClient.RefreshToken(authToken, connection);
            Assert.IsNotNull(refreshedAuthToken);
            Assert.IsNotNull(refreshedAuthToken.access_token);
        }
    }
}
