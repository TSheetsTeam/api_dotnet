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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TSheets
{
    [TestClass]
    public class ApiTests
    {
        private static string _baseUri = "https://rest.tsheets.com/api/v1";
        private static string TestAccessToken;

        private static ConnectionInfo _connection;
        private static StaticAuthentication _auth;
        private static RestClient _api;

        [TestInitialize]
        public void GetServerSettings()
        {
            
            string clientId;
            string redirectUri;
            string clientSecret;

            clientId = Environment.GetEnvironmentVariable("TSHEETS_CLIENTID");
            redirectUri = Environment.GetEnvironmentVariable("TSHEETS_REDIRECTURI");
            clientSecret = Environment.GetEnvironmentVariable("TSHEETS_CLIENTSECRET");
            TestAccessToken = Environment.GetEnvironmentVariable("TSHEETS_MANUALTOKEN");

            _connection = new ConnectionInfo(_baseUri, clientId, redirectUri, clientSecret);
            _auth = new StaticAuthentication(TestAccessToken);
            _api = new RestClient(_connection, _auth);
        }

        [TestMethod]
        public void TestOAuthToken()
        {
            string testToken = @"
            {
                ""access_token"":""accesstoken"",
                ""expires_in"":5184000,
                ""token_type"":""bearer"",
                ""scope"":"""",
                ""refresh_token"":""refreshtoken"",
                ""user_id"":""12345"",
                ""client_url"":""blakemoving"",
                ""issued"":""" + DateTime.UtcNow.ToString("o") + @"""
            }";

            var parsedToken = OAuthToken.FromJson(testToken);
            Assert.IsTrue(string.Equals(parsedToken.access_token, "accesstoken"));
            Assert.AreEqual(parsedToken.expires_in, 5184000);
            Assert.IsTrue(string.Equals(parsedToken.token_type, "bearer"));
            Assert.IsTrue(string.Equals(parsedToken.scope, ""));
            Assert.IsTrue(string.Equals(parsedToken.refresh_token, "refreshtoken"));
            Assert.IsTrue(string.Equals(parsedToken.user_id, "12345"));
            Assert.IsTrue(string.Equals(parsedToken.client_url, "blakemoving"));
            Assert.IsTrue(parsedToken.issued.Year == DateTime.Now.Year);

            Assert.IsFalse(parsedToken.NeedsRefresh());

            // if expire_in is a month from now, don't need refresh
            const int SPD = 60 * 60 * 24;
            parsedToken.expires_in = SPD * 30;
            Assert.IsFalse(parsedToken.NeedsRefresh());

            // if expires_in is a week from now, time to start refreshing
            parsedToken.expires_in = SPD * 7;
            Assert.IsTrue(parsedToken.NeedsRefresh());

            // tomorrow - need refresh
            parsedToken.expires_in = SPD;
            Assert.IsTrue(parsedToken.NeedsRefresh());

            parsedToken.expires_in = SPD * -7;
            Assert.IsTrue(parsedToken.NeedsRefresh());

            parsedToken.expires_in = 0;
            Assert.IsTrue(parsedToken.NeedsRefresh());
        }

        [TestMethod]
        public void TestGetCurrentUser()
        {
            var result = _api.Get(ObjectType.CurrentUser);
            Assert.IsTrue(!string.IsNullOrWhiteSpace(result));

            // and see if it's valid json
            var json = JObject.Parse(result);
            Assert.IsNotNull(json);

            var user = json.SelectToken("results.users.*");
            Assert.IsNotNull(user);

            Assert.IsTrue(!string.IsNullOrWhiteSpace((string)user["id"]));
            Assert.IsTrue(!string.IsNullOrWhiteSpace((string)user["first_name"]));
        }

        [TestMethod]
        public void TestBadAuthToken()
        {
            StaticAuthentication badAuth = new StaticAuthentication("garbagein_garbageout");
            RestClient api = new RestClient(_connection, badAuth);

            try
            {
                var result = api.Get(ObjectType.CurrentUser);
                Assert.Fail("Did not get exception on invalid auth token!");
            }
            catch (ApiException ex)
            {
                Assert.IsTrue(string.Equals(ex.HttpCode, "401"));
            }
        }

        [TestMethod]
        public void TestGetTimesheets()
        {
            // make the API easier to use for filters - make it easier to provide
            // key/value pair arguments - just an array probably easier
            var filters = new Dictionary<string, string>();
            filters.Add("start_date", "2014-01-01");
            filters.Add("end_date", "2015-01-01");
            var timesheets = _api.Get(ObjectType.Timesheets, filters);
            Assert.IsTrue(!string.IsNullOrWhiteSpace(timesheets));

            // and see if it's valid json
            var json = JObject.Parse(timesheets);
            Assert.IsNotNull(json);

            var allTimeSheets = json.SelectTokens("results.timesheets.*");
            Assert.IsNotNull(allTimeSheets);
            foreach (var ts in allTimeSheets)
            {
                Assert.IsTrue(!string.IsNullOrWhiteSpace(ts["id"].ToString()));
                Assert.IsTrue(!string.IsNullOrWhiteSpace(ts["duration"].ToString()));
                Assert.IsTrue(!string.IsNullOrWhiteSpace(ts["date"].ToString()));
                Assert.IsTrue(!string.IsNullOrWhiteSpace(ts["tz"].ToString()));
            }
        }

        [TestMethod]
        public void TestAddEditJobCode()
        {
            var rand = new Random();

            var jobcodes = new List<JObject>();

            dynamic jcd1 = new JObject();
            jcd1.name = "jc" + rand.Next(1000).ToString();
            jcd1.short_code = "sc" + rand.Next(1000).ToString();
            jcd1.billable = true;
            jcd1.assigned_to_all = true;
            jcd1.billable_rate = 25.4f;
            jobcodes.Add(jcd1);

            dynamic jcd2 = new JObject();
            jcd2.name = "jc" + rand.Next(1000).ToString();
            jcd2.short_code = "sc" + rand.Next(1000).ToString();
            jcd2.billable = false;
            jcd2.assigned_to_all = false;
            jobcodes.Add(jcd2);

            var response = _api.Add(ObjectType.Jobcodes, jobcodes);

            var json = JObject.Parse(response);
            Assert.IsNotNull(json);

            var savedJc = json.SelectToken("results.jobcodes.1");
            Assert.IsNotNull(savedJc);
            Assert.IsTrue(string.Equals(jcd1.name.ToString(), savedJc["name"].ToString()));

            savedJc = json.SelectToken("results.jobcodes.2");
            Assert.IsNotNull(savedJc);
            Assert.IsTrue(string.Equals(jcd2.name.ToString(), savedJc["name"].ToString()));


            // try to edit the 2nd one
            dynamic editJc = new JObject();
            editJc.id = savedJc["id"];
            editJc.name = savedJc["name"] + "edited";
            editJc.short_code = savedJc["short_code"] + "e";

            var editJobCodes = new List<JObject>();
            editJobCodes.Add(editJc);
            var editResponse = _api.Edit(ObjectType.Jobcodes, editJobCodes);

            json = JObject.Parse(editResponse);
            Assert.IsNotNull(json);

            savedJc = json.SelectToken("results.jobcodes.1");
            Assert.IsNotNull(savedJc);
            Assert.IsTrue(string.Equals(editJc.name.ToString(), savedJc["name"].ToString()));
        }

        [TestMethod]
        public void TestAddDeleteTimesheet()
        {
            TestAddDeleteTimesheet(true);
        }

        private void TestAddDeleteTimesheet(bool delete)
        {
            var result = _api.Get(ObjectType.CurrentUser);
            var currentUser = JObject.Parse(result).SelectToken("results.users.*");

            // and get a random jobcode to log time against
            var jobCodes = JObject.Parse(_api.Get(ObjectType.Jobcodes));
            int jobCodeId = 0;
            var testJobCodes = jobCodes.SelectTokens("results.jobcodes.*");
            foreach (var jc in testJobCodes)
            {
                jobCodeId = (int)jc["id"];
                break;
            }

            var timesheetsToAdd = new List<JObject>();

            DateTime today = DateTime.Now;
            string todayString = today.ToString("yyy-MM-dd");

            dynamic timesheet = new JObject();
            timesheet.user_id = currentUser["id"];
            timesheet.jobcode_id = jobCodeId;
            timesheet.type = "manual";
            timesheet.duration = 3600;
            timesheet.date = todayString;
            timesheetsToAdd.Add(timesheet);

            timesheet = new JObject();
            timesheet.user_id = currentUser["id"];
            timesheet.jobcode_id = jobCodeId;
            timesheet.type = "manual";
            timesheet.duration = 7200;
            timesheet.date = todayString;
            timesheetsToAdd.Add(timesheet);

            result = _api.Add(ObjectType.Timesheets, timesheetsToAdd);
            var addedTimesheets = JObject.Parse(result).SelectTokens("results.timesheets.*");
            Assert.IsNotNull(addedTimesheets);

            if (delete)
            {
                var idsToDelete = new List<int>();
                foreach (var ts in addedTimesheets)
                {
                    idsToDelete.Add((int)ts["id"]);
                }

                Assert.IsTrue(idsToDelete.Count > 0);
                result = _api.Delete(ObjectType.Timesheets, idsToDelete);
                var deleteResult = JObject.Parse(result);
                Assert.IsNotNull(deleteResult);
            }
        }

        [TestMethod]
        public void TestProjectReport()
        {
            // create some data to report against
            TestAddDeleteTimesheet(false);

            DateTime today = DateTime.Now;
            string todayString = today.ToString("yyyy-MM-dd");
            DateTime tomorrow = today + new TimeSpan(1, 0, 0, 0);
            string tomorrowString = tomorrow.ToString("yyyy-MM-dd");

            dynamic reportData = new JObject();
            reportData.data = new JObject();
            reportData.data.start_date = todayString;
            reportData.data.end_date = tomorrowString;

            var projectReport = _api.GetReport(ReportType.Project, reportData.ToString());
            var projectObject = JObject.Parse(projectReport);

            var usersInReport = projectObject.SelectTokens("results.project_report.totals.users.*");
            Assert.IsNotNull(usersInReport);
            float total = 0.0f;
            foreach (var user in usersInReport)
            {
                float current;
                if (float.TryParse(user.Value, out current))
                {
                    total += current;
                }
            }
            Assert.IsTrue(total > 0);
        }

        [TestMethod]
        public void TestPayrollReport()
        {
            dynamic reportData = new JObject();
            reportData.data = new JObject();
            reportData.data.start_date = "2014-07-01";
            reportData.data.end_date = "2014-07-31";

            var payrollReport = _api.GetReport(ReportType.Payroll, reportData.ToString());
            var payrollObject = JObject.Parse(payrollReport);
            var userTotals = payrollObject.SelectTokens("results.payroll_report.*");
            int total_work_seconds = 0;
            foreach (var userTotal in userTotals)
            {
                total_work_seconds += userTotal["total_work_seconds"].Value;
            }
            Assert.IsTrue(total_work_seconds > 0);
        }

        [TestMethod]
        public void TestLastModEndpoints()
        {
            var baseTimeStampResponse = _api.Get(ObjectType.LastModified);
            Assert.IsTrue(!string.IsNullOrWhiteSpace(baseTimeStampResponse));
            var baseTimeStampObjects = JObject.Parse(baseTimeStampResponse);
            Assert.IsNotNull(baseTimeStampObjects);
            var baseLastModTimestamps = baseTimeStampObjects.SelectToken("results.*");
            
            System.Threading.Thread.Sleep(2000);

            // no changes - timestamps should be the same
            var lastModTimestamps = JObject.Parse(_api.Get(ObjectType.LastModified)).SelectToken("results.*");
            Assert.IsTrue(string.Equals(baseLastModTimestamps["current_user"], lastModTimestamps["current_user"]));
            Assert.IsTrue(string.Equals(baseLastModTimestamps["customfields"], lastModTimestamps["customfields"]));
            Assert.IsTrue(string.Equals(baseLastModTimestamps["jobcodes"], lastModTimestamps["jobcodes"]));
            Assert.IsTrue(string.Equals(baseLastModTimestamps["timesheets"], lastModTimestamps["timesheets"]));
            Assert.IsTrue(string.Equals(baseLastModTimestamps["timesheets_deleted"], lastModTimestamps["timesheets_deleted"]));
            Assert.IsTrue(string.Equals(baseLastModTimestamps["users"], lastModTimestamps["users"]));

            TestAddDeleteTimesheet();

            // timesheets_deleted endpoint should show that a change has been made
            lastModTimestamps = JObject.Parse(_api.Get(ObjectType.LastModified)).SelectToken("results.*");            
            Assert.IsTrue(string.Equals(baseLastModTimestamps["current_user"], lastModTimestamps["current_user"]));
            Assert.IsTrue(string.Equals(baseLastModTimestamps["customfields"], lastModTimestamps["customfields"]));
            Assert.IsTrue(string.Equals(baseLastModTimestamps["jobcodes"], lastModTimestamps["jobcodes"]));                        
            Assert.IsTrue(string.Equals(baseLastModTimestamps["timesheets"], lastModTimestamps["timesheets"]));
            Assert.IsFalse(string.Equals(baseLastModTimestamps["timesheets_deleted"], lastModTimestamps["timesheets_deleted"]));
            Assert.IsTrue(string.Equals(baseLastModTimestamps["users"], lastModTimestamps["users"]));
        }
    }
}
