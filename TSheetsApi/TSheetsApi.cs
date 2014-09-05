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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TSheets
{
    /// <summary>
    /// TSheets Rest Client API. Provides basic REST operations as well as methods to help with authentication and
    /// token retrieval.
    /// <seealso cref="http://developers.tsheets.com/docs/api/">API Documentation</seealso>
    /// </summary>    
    public class RestClient
    {
        private const string LIBRARY_VERSION = "1.0";
        private const string USER_AGENT = "API Toolkit-v" + LIBRARY_VERSION + " (.NET)";
        public const string API_VERSION_1 = "1";

        private ConnectionInfo _connectionInfo;
        private IOAuth2 _oauthProvider;

        /// <summary>
        /// RestClient Constructor
        /// </summary>
        /// <param name="connectionInfo">the server/API connection information</param>
        /// <param name="authProvider">authentication provider to use</param>
        public RestClient(ConnectionInfo connectionInfo, IOAuth2 authProvider)
        {
            _connectionInfo = connectionInfo;
            _oauthProvider = authProvider;
        }

        #region Object Operations
        /// <summary>
        /// Retrieves a list of objects from the API.
        /// </summary>
        /// <param name="objectType">The type of resource being requested</param>
        /// <param name="filters">Set of key/value pairs containing the filters to use
        /// on the retrieval. See the API documentation for details on the filters
        /// supported for each ObjectType.</param>
        /// <returns>json string data containing the results</returns>
        public string Get(ObjectType objectType, IEnumerable<KeyValuePair<string, string>> filters = null)
        {
            return SendAuthenticatedRequest("GET", GetEndPoint(objectType), filters, string.Empty);
        }

        /// <summary>
        /// Adds new objects
        /// </summary>
        /// <param name="objectType">The type of resource being added</param>
        /// <param name="jsonObjects">Collection of objects to add</param>
        /// <returns>json string data containing the results of the add</returns>
        public string Add(ObjectType objectType, IEnumerable<JObject> jsonObjects)
        {
            return Add(objectType, EncodeDataObjects(jsonObjects));
        }

        /// <summary>
        /// Adds new objects using raw json string data        
        /// </summary>
        /// <param name="objectType">The type of resource being added</param>
        /// <param name="jsonData">Raw json string data for the Add request</param>
        /// <returns>json string data containing the results of the add</returns>
        public string Add(ObjectType objectType, string jsonData)
        {
            return SendAuthenticatedRequest("POST", GetEndPoint(objectType), null, jsonData);
        }

        /// <summary>
        /// Applies edits to existing objects
        /// </summary>
        /// <param name="objectType">The type of resource being edited</param>
        /// <param name="jsonObjects">Collection of objects to edit</param>
        /// <returns>json string data containing the results of the edit</returns>
        public string Edit(ObjectType objectType, IEnumerable<JObject> jsonObjects)
        {
            return Edit(objectType, EncodeDataObjects(jsonObjects));
        }

        /// <summary>
        /// Applies edits to existing objects
        /// </summary>
        /// <param name="objectType">The type of resource being edited</param>
        /// <param name="jsonData">Raw json string data for the Edit request</param>
        /// <returns>json string data containing the results of the edit</returns>
        public string Edit(ObjectType objectType, string jsonData)
        {
            return SendAuthenticatedRequest("PUT", GetEndPoint(objectType), null, jsonData);
        }

        /// <summary>
        /// Deletes one or more objects
        /// </summary>        
        /// <param name="objectType">The type of resource being deleted</param>
        /// <param name="ids">Collection of IDs to delete</param>
        /// <remarks>Delete is currently only supported on ObjectType.Timesheets resources</remarks>
        /// <returns>json string data containing the results of the delete</returns>
        public string Delete(ObjectType objectType, IEnumerable<int> ids)
        {
            Dictionary<string, string> urlParams = new Dictionary<string, string>();
            urlParams.Add("ids", string.Join(",", ids));
            return SendAuthenticatedRequest("DELETE", GetEndPoint(objectType), urlParams);
        }

        /// <summary>
        /// Runs a report and returns the results
        /// </summary>
        /// <param name="reportType">The type of report to run</param>
        /// <param name="jsonData">json data string containing the report parameters</param>
        /// <seealso cref="http://developers.tsheets.com/docs/api/">See API Documentation for 
        /// report parameters required for the given report type</seealso>
        /// <returns></returns>
        public string GetReport(ReportType reportType, string jsonData)
        {
            return SendAuthenticatedRequest("POST", GetReportEndPoint(reportType), null, jsonData);
        }
        #endregion

        #region Authentication Methods
        /// <summary>
        /// Builds the authorization URL that is used to start the OAuth2 process to retrieve an
        /// access token.
        /// </summary>
        /// <param name="connectionInfo">the server/API connection information</param>
        /// <param name="state">arbitrary client state information to include during the OAuth2 negotiation</param>
        /// <returns>URL to the authorization page</returns>
        public static string BuildAuthorizationUri(ConnectionInfo connectionInfo, string state)
        {
            string authUri = string.Format("{0}/authorize?response_type=code&client_id={1}&redirect_uri={2}&state={3}",
                connectionInfo.BaseUri,
                Uri.EscapeDataString(connectionInfo.ClientId),
                Uri.EscapeDataString(connectionInfo.RedirectUri),
                Uri.EscapeDataString(state));

            return authUri;
        }

        /// <summary>
        /// Handles the exchange of the oath code for an access token
        /// </summary>
        /// <param name="authCode">The auth code to exchange</param>
        /// <param name="connectionInfo">the server/API connection information</param>
        /// <returns>The OAuthToken from the server</returns>
        public static OAuthToken GetAccessToken(string authCode, ConnectionInfo connectionInfo)
        {
            string grantUri = string.Format("{0}/grant", connectionInfo.BaseUri);

            // set up the form post data for the grant
            string postData = string.Format("grant_type=authorization_code&client_id={0}&client_secret={1}&code={2}&redirect_uri={3}",
                Uri.EscapeDataString(connectionInfo.ClientId),
                Uri.EscapeDataString(connectionInfo.ClientSecret),
                Uri.EscapeDataString(authCode),
                Uri.EscapeDataString(connectionInfo.RedirectUri));

            string responseData = SendRequest(grantUri, "POST", null, null, "application/x-www-form-urlencoded", postData);

            var newToken = OAuthToken.FromJson(responseData);
            newToken.issued = DateTime.UtcNow;
            return newToken;
        }

        /// <summary>
        /// Refreshes the access token
        /// </summary>
        /// <param name="refreshToken">The refresh token to exhange for a new access token</param>
        /// <param name="connectionInfo">the server/API connection information</param>
        /// <returns>The refreshed OAuthToken from the server</returns>
        public static OAuthToken RefreshToken(string refreshToken, ConnectionInfo connectionInfo)
        {
            string grantUri = string.Format("{0}/grant", connectionInfo.BaseUri);

            // set up the form post data for the refresh
            string postData = string.Format("grant_type=refresh_token&client_id={0}&client_secret={1}&refresh_token={2}",
                connectionInfo.ClientId,
                connectionInfo.ClientSecret,
                refreshToken);

            string responseData = SendRequest(grantUri, "POST", null, null, "application/x-www-form-urlencoded", postData);

            var newToken = OAuthToken.FromJson(responseData);
            newToken.issued = DateTime.UtcNow;
            return newToken;
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Sends an authenticated http request to the server by adding the access token to the authorization header.
        /// </summary>
        /// <param name="method">The http method to use</param>
        /// <param name="endpoint">The URL endpoint</param>
        /// <param name="requestParams">Collection of name/value pairs to be sent in the URL parameters</param>
        /// <param name="data">The data to send in the request body</param>
        /// <returns>results of the http request</returns>
        private string SendAuthenticatedRequest(string method, string endpoint, IEnumerable<KeyValuePair<string, string>> requestParams = null, string data = null)
        {
            var requestUrl = string.Format("{0}/{1}", _connectionInfo.BaseUri, endpoint);
            string[] headers = new string[] {
                "Authorization: Bearer " + _oauthProvider.GetAccessToken(),
            };

            return SendRequest(requestUrl, method, requestParams, headers, "application/json", data);
        }

        /// <summary>
        /// Simple http request/response wrapper
        /// </summary>
        /// <param name="url">Full URL for the request</param>
        /// <param name="method">The http method to use</param>
        /// <param name="requestParams">Collection of name/value pairs to be sent in the URL parameters</param>
        /// <param name="headers">Collection of headers to be sent in the request</param>
        /// <param name="contentType">The http content type of the request body</param>
        /// <param name="data">The data to send in the request body</param>
        /// <returns>results of the http request</returns>
        private static string SendRequest(string url, string method, IEnumerable<KeyValuePair<string, string>> requestParams = null, IEnumerable<string> headers = null, string contentType = null, string data = null)
        {
            var paramList = new List<string>();
            if (requestParams != null)
            {
                foreach (var requestParam in requestParams)
                {
                    paramList.Add(string.Format("{0}={1}", Uri.EscapeDataString(requestParam.Key), Uri.EscapeDataString(requestParam.Value)));
                }
            }
            var queryString = string.Join("&", paramList);

            var requestUrl = url;
            if (queryString.Length > 0)
            {
                requestUrl = string.Format("{0}?{1}", url, queryString);
            }

            try
            {
                var request = (HttpWebRequest)WebRequest.Create(requestUrl);
                request.UserAgent = USER_AGENT;
                request.Method = method;
                if (!string.IsNullOrWhiteSpace(contentType))
                {
                    request.ContentType = contentType;
                }
                else
                {
                    request.ContentType = "application/json";
                }
                if (headers != null)
                {
                    foreach (string header in headers)
                    {
                        request.Headers.Add(header);
                    }
                }

                if (!string.IsNullOrWhiteSpace(data))
                {
                    var requestData = Encoding.UTF8.GetBytes(data);
                    request.ContentLength = requestData.Length;

                    using (var requestStream = request.GetRequestStream())
                    {
                        requestStream.Write(requestData, 0, requestData.Length);
                    }
                }

                string responsestring = "";
                using (var response = request.GetResponse())
                {
                    using (var responseStream = response.GetResponseStream())
                    {
                        using (var reader = new StreamReader(response.GetResponseStream()))
                        {
                            responsestring = reader.ReadToEnd();
                        }
                    }
                }

                return responsestring;
            }
            catch (WebException ex)
            {
                string httpCode = string.Empty;

                if (ex.Status == WebExceptionStatus.ProtocolError)
                {
                    var httpResponse = ex.Response as HttpWebResponse;
                    if (httpResponse != null)
                    {
                        httpCode = ((int)httpResponse.StatusCode).ToString();
                    }
                }

                throw new ApiException(ex.Message, ex, requestUrl, httpCode);
            }
        }

        /// <summary>
        /// Helper to return the API endpoint for the given object type
        /// </summary>
        /// <param name="objectType">The object type</param>
        /// <returns>The associated endpoint</returns>
        private static string GetEndPoint(ObjectType objectType)
        {
            switch (objectType)
            {
                case ObjectType.Users:
                    return "users";
                case ObjectType.CurrentUser:
                    return "current_user";
                case ObjectType.EffectiveSettings:
                    return "effective_settings";
                case ObjectType.Jobcodes:
                    return "jobcodes";
                case ObjectType.JobcodeAssignments:
                    return "jobcode_assignments";
                case ObjectType.CustomFields:
                    return "customfields";
                case ObjectType.CustomFieldItems:
                    return "customfielditems";
                case ObjectType.Timesheets:
                    return "timesheets";
                case ObjectType.TimesheetsDeleted:
                    return "timesheets_deleted";
                case ObjectType.Geolocations:
                    return "geolocations";
                default:
                    Debug.Assert(false, "Unknown ObjectType: " + objectType);
                    throw new NotImplementedException("Unknown endpoint for ObjectType: " + objectType);
            }
        }

        /// <summary>
        /// Helper to return the API endpoint for the given report type
        /// </summary>
        /// <param name="reportType">The report type</param>
        /// <returns>The associated endpoint</returns>
        private static string GetReportEndPoint(ReportType reportType)
        {
            switch (reportType)
            {
                case ReportType.Project:
                    return "reports/project";
                case ReportType.Payroll:
                    return "reports/payroll";
                default:
                    Debug.Assert(false, "Unknown ReportType: " + reportType);
                    throw new NotImplementedException("Unknown endpoint for ReportType: " + reportType);
            }
        }

        /// <summary>
        /// Helper to wrap the given json objects into a data element array json
        /// string expected by many of the Add/Edit APIs
        /// </summary>
        /// <param name="objects">the objects to wrap</param>
        /// <returns>json string of the objects wrapped in data element</returns>
        private string EncodeDataObjects(IEnumerable<JObject> jsonObjects)
        {
            var wrappedData = new JObject();
            var dataArray = new JArray();
            foreach (var jsonObject in jsonObjects)
            {
                dataArray.Add(jsonObject);
            }
            wrappedData.Add("data", dataArray);

            return wrappedData.ToString();
        }
        #endregion
    }

    /// <summary>
    /// The ObjectTypes supported by the API
    /// </summary>
    public enum ObjectType
    {
        Users,
        CurrentUser,
        EffectiveSettings,
        Jobcodes,
        JobcodeAssignments,
        CustomFields,
        CustomFieldItems,
        Timesheets,
        TimesheetsDeleted,
        Geolocations,
    }

    /// <summary>
    /// The ReportTypes supported by the API
    /// </summary>
    public enum ReportType
    {
        Project,
        Payroll,
    }

    /// <summary>
    /// The API application/Server connection information
    /// </summary>
    public class ConnectionInfo
    {
        /// <summary>
        /// ConnectionInfo constructor
        /// </summary>
        /// <param name="baseUri">Base URL of the API (https://rest.tsheets.com/api/v1)</param>
        /// <param name="clientId">OAuth Client ID for this application</param>
        /// <param name="redirectUri">OAuth Redirect URI for this application</param>
        /// <param name="clientSecret">OAuth Client Secret for this application</param>
        public ConnectionInfo(string baseUri, string clientId, string redirectUri, string clientSecret)
        {
            BaseUri = baseUri;
            ClientId = clientId;
            RedirectUri = redirectUri;
            ClientSecret = clientSecret;
        }

        /// <summary>
        /// The base URL of the API (https://rest.tsheets.com/api/v1)
        /// </summary>
        public string BaseUri
        {
            get
            {
                return _baseUri;
            }

            set
            {
                _baseUri = value.TrimEnd('/');
            }
        }
        private string _baseUri;

        /// <summary>
        /// The Oauth Client ID configured for the API application
        /// </summary>
        public string ClientId
        {
            get;
            set;
        }

        /// <summary>
        /// The OAuth Redirect URI configured for the API application
        /// </summary>
        public string RedirectUri
        {
            get;
            set;
        }

        /// <summary>
        /// The OAuth Client Secret registered for the API application
        /// </summary>
        public string ClientSecret
        {
            get;
            set;
        }
    }

    /// <summary>
    /// Handles the OAuth2 token information & json request/responses used by the API
    /// </summary>
    public class OAuthToken
    {
        #region Properties
        // The authentication properties from a successful OAuth2 negotiation
        // i.e.:
        //{
        //    "access_token":"AccessToken1234",
        //    "expires_in":5184000,
        //    "token_type":"bearer",
        //    "scope":"",
        //    "refresh_token":"RefreshToken5678",
        //    "user_id":"12345",
        //    "client_url":"blakemoving"
        //}

        public string access_token { get; set; }
        public int expires_in { get; set; }
        public string token_type { get; set; }
        public string scope { get; set; }
        public string refresh_token { get; set; }
        public string user_id { get; set; }
        public string client_url { get; set; }

        /// <summary>
        /// DateTime the token was issued in UTC.  Use issued with expires_in to determine
        /// when to exchange for a new token
        /// </summary>
        public DateTime issued { get; set; }
        #endregion

        /// <summary>
        /// Creates an OAuthToken object from the OAuth2 response string
        /// </summary>
        /// <param name="jsonData">OAuth2 response string from the server</param>
        /// <returns>Corresponding OAuthToken object</returns>
        public static OAuthToken FromJson(string jsonData)
        {
            return JsonConvert.DeserializeObject<OAuthToken>(jsonData);
        }

        /// <summary>
        /// Returns the json string representation of the current object
        /// </summary>
        /// <returns>corresponding json string</returns>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        /// <summary>
        /// Determines if the token is expired or expiring within a week
        /// </summary>
        /// <returns>true if expired or expiring soon</returns>
        public bool NeedsRefresh()
        {
            int refreshSeconds = expires_in - (60 * 60 * 24 * 7); // one week before expires_in
            DateTime refreshTime = issued + new TimeSpan(0, 0, 0, refreshSeconds);

            return DateTime.UtcNow >= refreshTime;
        }
    }

    /// <summary>
    /// Interface for the OAuth2 access token information needed for API access
    /// </summary>
    public interface IOAuth2
    {
        /// <summary>
        /// Called by the client API class to retrieve a token that can be used for authentication.
        /// Implementation should handle obtaining a new token/refreshing token if necessary.
        /// </summary>
        /// <returns>Access token</returns>
        string GetAccessToken();
    }

    /// <summary>
    /// Exception class for errors from the API calls
    /// </summary>
    [Serializable]
    public class ApiException : Exception
    {
        /// <summary>
        /// Url that caused the exception
        /// </summary>
        public string Url
        {
            get;
            private set;
        }

        /// <summary>
        /// The HTTP error code (if any)
        /// </summary>
        public string HttpCode
        {
            get;
            private set;
        }

        /// <summary>
        /// Error text returned from the API call (only available on certain end points)
        /// </summary>
        public string ErrorText
        {
            get;
            private set;
        }

        /// <summary>
        /// Error code returned from the API call (only available on certain end points)
        /// </summary>
        public string ErrorCode
        {
            get;
            private set;
        }

        /// <summary>
        /// ApiException constructor
        /// </summary>
        public ApiException(string message, string url = "", string httpCode = "", string errorText = "", string errorCode = "")
            : base(message)
        {
            Url = url;
            HttpCode = httpCode;
            ErrorText = errorText;
            ErrorCode = errorCode;
        }

        /// <summary>
        /// ApiException constructor
        /// </summary>
        public ApiException(string message, Exception innerException, string url = "", string httpCode = "", string errorText = "", string errorCode = "")
            : base(message, innerException)
        {
            Url = url;
            HttpCode = httpCode;
            ErrorText = errorText;
            ErrorCode = errorCode;
        }

        #region Serialization
        protected ApiException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            Url = info.GetString("Url");
            HttpCode = info.GetString("HttpCode");
            ErrorText = info.GetString("ErrorText");
            ErrorCode = info.GetString("ErrorCode");
        }

        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException("info");
            info.AddValue("Url", Url);
            info.AddValue("HttpCode", HttpCode);
            info.AddValue("ErrorText", ErrorText);
            info.AddValue("ErrorCode", ErrorCode);
            base.GetObjectData(info, context);
        }
        #endregion
    }
}