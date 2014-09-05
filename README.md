api_dotnet
==========

.NET Helper Library for TSheets API
This library provides basic REST operations as well as methods to help with authentication and token retrieval.

Synopsis
==========
```cs
var connectionInfo = new TSheets.ConnectionInfo("https://rest.tsheets.com/api/v1", "your_client_id", "your_redirect_uri", "your_client_secret");
var authProvider = new TSheets.StaticAuthentication("your_auth_token");
var tsheetsClient = new RestClient(connectionInfo, authProvider);
var jobCodes = tsheetsApi.Get(ObjectType.Jobcodes);
```

Examples
==========
See the examples in the included TSheetsApiSamples project.

API Documentation
==========
Full API documentation can be found at http://developers.tsheets.com/docs/api/
