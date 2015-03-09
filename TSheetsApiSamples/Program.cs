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
using Newtonsoft.Json.Linq;
using TSheets;

namespace TSheetsApiSamples
{
    /// <summary>
    /// This is a command line example that includes a few simple calls using the TSheets .NET API Library.
    /// It assumes/requires you already have your access token. If you do not have it yet:
    ///      - Visit your TSheets web dashboard
    ///      - Click on "Company Settings" in the menu bar on the left
    ///      - Click on "Add-ons"
    ///      - Locate the "API" add-on and either install it or open the preferences
    ///      - Create or edit an application and your access token will be provided
    ///      <seealso cref="http://developers.tsheets.com/docs/api/authentication"/>
    /// </summary>
    class Program
    {
        private static string _baseUri = "https://rest.tsheets.com/api/v1";

        private static ConnectionInfo _connection;
        private static IOAuth2 _authProvider;


        private static string _clientId;
        private static string _redirectUri;
        private static string _clientSecret;
        private static string _manualToken;


        static void Main(string[] args)
        {
            // _clientId, _redirectUri, and _clientSecret are needed by the API to connect to your
            // TSheets account.  To get these values for your account, log in to your TSheets account,
            // click on Company Settings -> Add-ons -> API Preferences and use the values for your
            // application. You can specify them through environment variables as shown here, or just
            // paste them into the code here directly.
            _clientId = Environment.GetEnvironmentVariable("TSHEETS_CLIENTID");
            _redirectUri = Environment.GetEnvironmentVariable("TSHEETS_REDIRECTURI");
            _clientSecret = Environment.GetEnvironmentVariable("TSHEETS_CLIENTSECRET");

            // If you want to use simple authentication with a static token (AuthenticateWithManualToken()),
            // click on the properties of your application in API Preferences, and click the Add Token link.
            // Set _manualToken to the value created.
            _manualToken = Environment.GetEnvironmentVariable("TSHEETS_MANUALTOKEN");


            // set up the ConnectionInfo object which tells the API how to connect to the server
            _connection = new ConnectionInfo(_baseUri, _clientId, _redirectUri, _clientSecret);

            // Choose which authentication method to use. AuthenticateWithBrowser will do a full OAuth2 forms
            // based authentication in a web browser form and prompt the user for credentials.  
            // AuthenticateWithManualToken will do simple AccessToken based authentication using 
            // a manually created token in the API add-on.
            AuthenticateWithBrowser();
            //AuthenticateWithManualToken();

            GetUserInfoSample();
            GetUsersSample();
            GetTimesheetsSample();

            ProjectReportSample();

            AddEditDeleteTimesheetSample();

            GetJobcodesByPageSample();
        }

        /// <summary>
        /// Shows how to set up authentication to use a static/manually created access token.
        /// To create a manual auth token, go to the API Add-on preferences in your TSheets account
        /// and click Add Token.
        /// </summary>
        private static void AuthenticateWithManualToken()
        {
            _authProvider = new StaticAuthentication(_manualToken);
        }

        /// <summary>
        /// Shows how to set up authentication to authenticate the user in an embedded browser form
        /// and get an OAuth2 token by prompting the user for credentials.
        /// </summary>
        private static void AuthenticateWithBrowser()
        {
            // The UserAuthentication class will handle the OAuth2 desktop
            // authentication flow using an embedded WebBrowser form, 
            // cache the returned token for later API usage, and handle token refreshes.
            var userAuthProvider = new UserAuthentication(_connection);
            _authProvider = userAuthProvider;

            // optionally register an event handler to be notified if/when the auth
            // token changes
            userAuthProvider.TokenChanged += userAuthProvider_TokenChanged;

            // Retrieve a token from the server
            // Note: the RestApi class will call this as needed so it isn't required
            // to call it before accessing the API. However, manually calling GetToken first
            // is recommended so the app can more gracefully handle authentication errors
            OAuthToken authToken = userAuthProvider.GetToken();


            // OAuth2 tokens can and should be cached across application uses so users
            // don't need to grant access every time they run the application.
            // To do this, call OAuthToken.ToJSon to get a serialized version of
            // the token that can be used later.  Be sure to treat this string as a 
            // user password and store it securely!
            // Note that this token will potentially be refreshed during API usage
            // using the OAuth2 token refresh protocol.  If that happens, your application
            // should overwrite the previously saved token with the new token value.
            // You can register for the TokenChanged event to be notified of any new/changed tokens
            // or you can call UserAuthentication.GetToken().ToJson() after using the API 
            // to manually retrieve the most current token.
            string savedToken = authToken.ToJson();

            // This can be restored into a UserAuthentication object later to reuse:
            OAuthToken restoredToken = OAuthToken.FromJson(savedToken);
            UserAuthentication restoredAuthProvider = new UserAuthentication(_connection, restoredToken);

            // Now the user will not be prompted when we call GetToken
            OAuthToken cachedToken = restoredAuthProvider.GetToken();
        }

        /// <summary>
        /// Event handler that will be called when the UserAuthentication OAuthToken changes
        /// </summary>
        static void userAuthProvider_TokenChanged(object sender, TokenChangedEventArgs e)
        {
            if (e.CurrentToken != null)
            {
                System.Console.WriteLine("Received new auth token:");
                System.Console.WriteLine(e.CurrentToken.ToJson());
            }
            else
            {
                System.Console.WriteLine("Token no longer valid");
            }
        }

        /// <summary>
        /// Shows how to get current logged in user information
        /// </summary>
        private static void GetUserInfoSample()
        {
            var tsheetsApi = new RestClient(_connection, _authProvider);
            var userData = tsheetsApi.Get(ObjectType.CurrentUser);
            var responseObject = JObject.Parse(userData);

            var userObject = responseObject.SelectToken("results.users.*");

            Console.WriteLine(string.Format("Current User: {0} {1}, email = {2}, client url = {3}",
                userObject["first_name"],
                userObject["last_name"],
                userObject["email"],
                userObject["client_url"]));
        }

        /// <summary>
        /// Shows how to get all users for the company
        /// </summary>
        private static void GetUsersSample()
        {
            var tsheetsApi = new RestClient(_connection, _authProvider);
            var userData = tsheetsApi.Get(ObjectType.Users);
            var responseObject = JObject.Parse(userData);

            var users = responseObject.SelectTokens("results.users.*");
            foreach (var userObject in users)
            {
                Console.WriteLine(string.Format("Current User: {0} {1}, email = {2}, client url = {3}",
                    userObject["first_name"],
                    userObject["last_name"],
                    userObject["email"],
                    userObject["client_url"]));
            }
        }

        /// <summary>
        /// Shows how to receive all timesheets for a given timeframe by using filter arguments
        /// and how to access the supplemental data in the response.
        /// Supplemental data will contain all of the user/jobcode/etc related data
        /// about the selected timesheets. API users should use the supplemental data when available
        /// rather than making additional calls to the server to receive that information.
        /// </summary>
        private static void GetTimesheetsSample()
        {
            var tsheetsApi = new RestClient(_connection, _authProvider);

            var filters = new Dictionary<string, string>();
            filters.Add("start_date", "2014-01-01");
            filters.Add("end_date", "2015-01-01");
            var timesheetData = tsheetsApi.Get(ObjectType.Timesheets, filters);

            var timesheetsObject = JObject.Parse(timesheetData);
            var allTimeSheets = timesheetsObject.SelectTokens("results.timesheets.*");
            foreach (var timesheet in allTimeSheets)
            {
                Console.WriteLine(string.Format("Timesheet: ID={0}, Duration={1}, Data={2}, tz={3}",
                    timesheet["id"], timesheet["duration"], timesheet["date"], timesheet["tz"]));

                // get the associated user for this timesheet
                var tsUser = timesheetsObject.SelectToken("supplemental_data.users." + timesheet["user_id"]);
                Console.WriteLine(string.Format("\tUser: {0} {1}", tsUser["first_name"], tsUser["last_name"]));
            }
        }

        /// <summary>
        /// The Get api calls can potentially return many records from the server. The TSheets rest APIs
        /// support a paging request model so API clients can request records in smaller chunks.
        /// This sample shows how to request all available jobcodes using paging filters to retrieve
        /// the records this way.
        /// </summary>
        private static void GetJobcodesByPageSample()
        {
            var tsheetsApi = new RestClient(_connection, _authProvider);
            var filters = new Dictionary<string, string>();

            // start by requesting the first page
            int currentPage = 1;

            // and set our items per page to be 2
            // Note: 50 is the recommended per_page value for normal usage. This sample
            // is using a smaller number to make the sample more clear. Be sure to 
            // manually create >2 jobcodes in your account to see the paging happen.
            filters["per_page"] = "2";

            bool moreData = true;
            while (moreData)
            {
                filters["page"] = currentPage.ToString();

                var getResponse = tsheetsApi.Get(ObjectType.Jobcodes, filters);
                var responseObject = JObject.Parse(getResponse);

                // see if we have more pages to retrieve
                moreData = bool.Parse(responseObject.SelectToken("more").ToString());

                // increment to the next page
                currentPage++;

                var jobcodes = responseObject.SelectTokens("results.jobcodes.*");
                foreach (var jobcode in jobcodes)
                {
                    Console.WriteLine(string.Format("Jobcode Name: {0}, type = {1}, shortcode = {2}",
                        jobcode["name"],
                        jobcode["type"],
                        jobcode["short_code"]));
                }
            }
        }


        /// <summary>
        /// Shows how to create a user, create a jobcode, log time against it, and then run a project report
        /// that shows them
        /// </summary>
        public static void ProjectReportSample()
        {
            var tsheetsApi = new RestClient(_connection, _authProvider);

            DateTime today = DateTime.Now;
            string todayString = today.ToString("yyyy-MM-dd");
            DateTime tomorrow = today + new TimeSpan(1, 0, 0, 0);
            string tomorrowString = tomorrow.ToString("yyyy-MM-dd");

            // create a user
            int userId = CreateUser(tsheetsApi);

            // now create a jobcode we can log time against
            int jobCodeId = CreateJobCode(tsheetsApi);

            // log some time
            {
                var timesheetObjects = new List<JObject>();
                dynamic timesheet = new JObject();
                timesheet.user_id = userId;
                timesheet.jobcode_id = jobCodeId;
                timesheet.type = "manual";
                timesheet.duration = 3600;
                timesheet.date = todayString;
                timesheetObjects.Add(timesheet);

                timesheet = new JObject();
                timesheet.user_id = userId;
                timesheet.jobcode_id = jobCodeId;
                timesheet.type = "manual";
                timesheet.duration = 7200;
                timesheet.date = todayString;
                timesheetObjects.Add(timesheet);

                var addTimesheetResponse = tsheetsApi.Add(ObjectType.Timesheets, timesheetObjects);
                Console.WriteLine(addTimesheetResponse);

                var addedTimesheets = JObject.Parse(addTimesheetResponse);
            }

            // and run the report
            {
                dynamic reportOptions = new JObject();
                reportOptions.data = new JObject();
                reportOptions.data.start_date = todayString;
                reportOptions.data.end_date = tomorrowString;

                var projectReport = tsheetsApi.GetReport(ReportType.Project, reportOptions.ToString());

                Console.WriteLine(projectReport);
            }
        }

        /// <summary>
        /// Shows how to add, edit, and delete a timesheet
        /// </summary>
        private static void AddEditDeleteTimesheetSample()
        {
            var tsheetsApi = new RestClient(_connection, _authProvider);

            DateTime today = DateTime.Now;
            string todayString = today.ToString("yyyy-MM-dd");
            DateTime yesterday = today - new TimeSpan(1, 0, 0, 0);
            string yesterdayString = yesterday.ToString("yyyy-MM-dd");

            // create a user
            int userId = CreateUser(tsheetsApi);

            // now create a jobcode we can log time against
            int jobCodeId = CreateJobCode(tsheetsApi);

            // add a couple of timesheets
            var timesheetsToAdd = new List<JObject>();
            dynamic timesheet = new JObject();
            timesheet.user_id = userId;
            timesheet.jobcode_id = jobCodeId;
            timesheet.type = "manual";
            timesheet.duration = 3600;
            timesheet.date = todayString;
            timesheetsToAdd.Add(timesheet);

            timesheet = new JObject();
            timesheet.user_id = userId;
            timesheet.jobcode_id = jobCodeId;
            timesheet.type = "manual";
            timesheet.duration = 7200;
            timesheet.date = todayString;
            timesheetsToAdd.Add(timesheet);

            var result = tsheetsApi.Add(ObjectType.Timesheets, timesheetsToAdd);
            Console.WriteLine(result);

            // pull out the ids of the new timesheets
            var addedTimesheets = JObject.Parse(result).SelectTokens("results.timesheets.*");
            var timesheetIds = new List<int>();
            foreach (var ts in addedTimesheets)
            {
                timesheetIds.Add((int)ts["id"]);
            }

            // make some edits
            var timesheetsToEdit = new List<JObject>();
            timesheet = new JObject();
            timesheet.id = timesheetIds[0];
            timesheet.date = yesterdayString;
            timesheetsToEdit.Add(timesheet);
            timesheet = new JObject();
            timesheet.id = timesheetIds[1];
            timesheet.date = yesterdayString;
            timesheetsToEdit.Add(timesheet);
            result = tsheetsApi.Edit(ObjectType.Timesheets, timesheetsToEdit);
            Console.WriteLine(result);

            // and delete them
            result = tsheetsApi.Delete(ObjectType.Timesheets, timesheetIds);
            Console.WriteLine(result);
        }

        /// <summary>
        /// Helper to create random string so we don't get duplicate name conflicts if samples
        /// are run multiple times
        /// </summary>
        private static string CreateRandomString()
        {
            var randData = new Random().Next(1000).ToString();
            return randData;
        }

        /// <summary>
        /// Helper to create a random job code
        /// </summary>
        private static int CreateJobCode(RestClient tsheetsApi)
        {
            var jobCodeObjects = new List<JObject>();
            dynamic jobCode = new JObject();
            jobCode.name = "jc" + CreateRandomString();
            jobCode.assigned_to_all = true;
            jobCodeObjects.Add(jobCode);

            var addJobCodeResponse = tsheetsApi.Add(ObjectType.Jobcodes, jobCodeObjects);
            Console.WriteLine(addJobCodeResponse);

            // get the job code ID so we can use it later
            var addedJobCode = JObject.Parse(addJobCodeResponse).SelectToken("results.jobcodes.1");
            return (int)addedJobCode["id"];
        }

        /// <summary>
        /// Helper to create a random user
        /// </summary>
        private static int CreateUser(RestClient tsheetsApi)
        {
            var userObjects = new List<JObject>();
            dynamic user = new JObject();
            user.username = "user" + CreateRandomString();
            user.password = "Pa$$W0rd";
            user.first_name = "first";
            user.last_name = "last";
            userObjects.Add(user);

            var addUserResponse = tsheetsApi.Add(ObjectType.Users, userObjects);
            Console.WriteLine(addUserResponse);

            // get the user ID so we can use it later
            var addedUser = JObject.Parse(addUserResponse).SelectToken("results.users.1");
            return (int)addedUser["id"];
        }
    }
}
