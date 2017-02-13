using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jil;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
namespace SPMonitor
{
    class Program
    {
        public static HttpClient Client { get; set; }
        public static Stopwatch Clock { get; set; }
        public static string FormDigest { get; set; } = null;
        public static DateTime FormDigestLastUpdated { get; set; } = DateTime.MinValue;
        public static bool HasFired { get; set; } = true;
        public static Uri Url { get; set; }
        public static string ListName { get; } = "xxSPMonitorTestListxx";

        public static int Iterations { get; set; }
        static void Main(string[] args)
        {
            var isFirstRun = true;
            while (HasFired)
            {
                if (isFirstRun)
                {
                    isFirstRun = false;
                    var task = Task.Run(async () => await Initialize(args));
                    task.Wait(60000);
                }

                if (args.Count() < 3 || args.Any(a => a.Contains("?")))
                {
                    Logger.LogWarning("spmonitor.exe <siteurl> <interval> <iterations>" +
                        "<siteurl> Site url to a site where you have permission to create lists." +
                        "<interval> Number of Minutes between tests." +
                        "<iterations> Number of List Items to create per <interval>", " Usage ");
                    Environment.Exit(1);
                }

                // Simulate Event Loop
                // Code will not exit until killed to allow background threads to run
                Thread.Sleep(20);
            }
        }

        // Step 1: Initial State
        public static async Task Initialize(string[] args)
        {
            do
            {

                int itr = 0;
                int.TryParse(args[2], out itr);
                if (itr > 0)
                {
                    Iterations = itr;
                }
                else
                {
                    Iterations = 5;
                }



                int interval = SetInterval(args[1] ?? "15");
                Clock = new Stopwatch();

                var handler = new HttpClientHandler
                {
                    Credentials = CredentialCache.DefaultNetworkCredentials,
                    UseCookies = true
                };

                try
                {
                    Client = new HttpClient(handler);
                    var baseAddress = args[0].EndsWith("/") ? args[0] : $"{args[0]}/";

                    Client.BaseAddress = GetBaseAddress(baseAddress);

                    await CreateListIfNotExists();
                    await CreateListItem();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"{ex.Message}");
                }
                finally
                {
                    Thread.Sleep(interval);
                }
            } while (true);
        }


        public static async Task CreateListIfNotExists()
        {

            try
            {
                var request = await GetStandardRequest(HttpMethod.Get, $"{Client.BaseAddress}_api/web/lists/GetByTitle('{ListName}')");

                var response = await Client.SendAsync(request);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    request = await GetStandardRequest(HttpMethod.Post, $"{Client.BaseAddress}_api/web/lists");

                    var payload = new StringContent
                        (
                            "{'__metadata': { 'type': 'SP.List' }, 'Title': '" + ListName + "', 'BaseTemplate': 100 }",
                            Encoding.UTF8
                        );
                    payload.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json;odata=verbose");

                    request.Content = payload;
                    response = await Client.SendAsync(request);


                    if (response.StatusCode != HttpStatusCode.Created)
                    {
                        throw new CreateListFailedException($"Received Status Code: {response.StatusCode} while creating the list.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message, "[ Exception ]");
            }
        }

        public static async Task CreateListItem()
        {
            for (int i = 0; i < Iterations; i++)
            {
                var log = new LogEntry();
                string correlationId = null;

                try
                {
                    Clock.Start();

                    log.Action = "CreateListItem";
                    log.StartedTime = DateTime.Now;

                    var request = await GetStandardRequest(HttpMethod.Post, $"{Client.BaseAddress}_api/web/lists/GetByTitle('{ListName}')/items");

                    var payload = new StringContent
                        (
                            "{'__metadata': { 'type': 'SP.Data.XxSPMonitorTestListxxListItem' }, 'Title': '" + Guid.NewGuid().ToString() + "' }",
                            Encoding.UTF8
                        );
                    payload.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json;odata=verbose");

                    request.Content = payload;
                    var response = await Client.SendAsync(request);

                    Clock.Stop();
                    log.EndedTime = DateTime.Now;
                    if (response.Headers.Any(kv => kv.Key == "SPRequestGuid"))
                    {
                        correlationId = response.Headers.FirstOrDefault(kv => kv.Key == "SPRequestGuid").Value.FirstOrDefault();
                    }

                    if (response.StatusCode != HttpStatusCode.Created)
                    {
                        log.TimeTakenInMilliseconds = Clock.ElapsedMilliseconds;
                        log.ExceptionType = "CreateListItemFailedException";
                        log.HttpStatusCode = response.StatusCode.ToString();
                        log.ExceptionMessage = $"Received Status Code: {response.StatusCode} while creating the list.";
                        log.IsSuccessful = false;
                        log.CorrelationId = correlationId;
                        Logger.Log.Add(log);
                        Logger.WriteLogFileToDisk();

                        throw new CreateListItemFailedException(log.ExceptionMessage);
                    }
                    else
                    {
                        log.TimeTakenInMilliseconds = Clock.ElapsedMilliseconds;
                        log.IsSuccessful = true;
                        log.CorrelationId = correlationId;
                        log.HttpStatusCode = response.StatusCode.ToString();
                        Logger.Log.Add(log);
                        Logger.WriteLogFileToDisk();
                    }
                    var timeThreshold = 5000;
                    if (log.TimeTakenInMilliseconds > timeThreshold)
                    {
                        Logger.LogWarning($"A request took longer than {timeThreshold} ms.\r\nTime Taken: {log.TimeTakenInMilliseconds}\r\nCorrelationId: {log.CorrelationId}");
                    }
                    Clock.Reset();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.Message, "[ Exception ]");

                    log.TimeTakenInMilliseconds = Clock.ElapsedMilliseconds;
                    log.IsSuccessful = false;
                    log.EndedTime = DateTime.Now;
                    log.ExceptionType = ex.GetType().FullName;
                    log.ExceptionMessage = ex.Message;
                    log.ExceptionStackTrace = ex.StackTrace;
                    log.CorrelationId = correlationId;
                    Logger.Log.Add(log);
                    Logger.WriteLogFileToDisk();
                }
            }
        }

        public static async Task<HttpRequestMessage> GetStandardRequest(HttpMethod method, string uri)
        {
            var request = new HttpRequestMessage(method, uri);
            request.Headers.Add("accept", "application/json;odata=verbose");
            // request.Headers.Add("content-type", "application/json;odata=verbose");
            var timeSpanDigest = DateTime.Now - FormDigestLastUpdated;
            var isFormDigestExpired = timeSpanDigest.Minutes > 20;
            if (string.IsNullOrEmpty(FormDigest) || isFormDigestExpired)
            {
                await RefreshContextInfo();
                request.Headers.Add("X-RequestDigest", FormDigest);
            }
            else request.Headers.Add("X-RequestDigest", FormDigest);
            return request;
        }

        public static int SetInterval(string intervalInSeconds)
        {
            int temp = 0;
            int.TryParse(intervalInSeconds, out temp);
            if (temp == 0)
            {
                Logger.LogError("You must enter a number for the interval parameter.\r\nThe default is 15 seconds.");
            }

            return temp * 1000;
        }

        static async Task<string> RefreshContextInfo()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{Client.BaseAddress}_api/contextinfo");
            request.Headers.Add("accept", "application/json;odata=verbose");

            var response = await Client.SendAsync(request);
            
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var contextInfo = JSON.DeserializeDynamic(await response.Content.ReadAsStringAsync());
                FormDigest = (string)contextInfo.d.GetContextWebInformation.FormDigestValue;
                FormDigestLastUpdated = DateTime.Now;
            }
            else
            {
                throw new HttpRequestException($"Error initializing request.  Status Code: {response.StatusCode}");
            }

            response.Dispose();
            return FormDigest;
        }
        static Uri GetBaseAddress(string uri)
        {
            Uri baseAddr = null;
            try
            {
                baseAddr = new Uri(uri.ToString());
                Url = baseAddr;
            }
            catch (ArgumentNullException)
            {
                Logger.LogError("You must specify the site you want to test against.");
                Environment.Exit(1);

            }
            catch (UriFormatException)
            {
                Logger.LogError($"The Url you provided {uri} could not be parsed.");
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                Logger.LogError($"An unknown exception has occurred trying to parse the Url");
                Logger.LogWarning(ex.Message, "Exception Details:");
                Logger.LogWarning(ex.StackTrace, "Exception Details:");
            }

            return baseAddr;
        }

    }

    public class LogEntry
    {
        public string Action;
        public DateTime EndedTime;
        public DateTime StartedTime;
        public long TimeTakenInMilliseconds;
        public bool IsSuccessful;
        public string HttpStatusCode;
        public string ExceptionType;
        public string ExceptionMessage;
        public string ExceptionStackTrace;
        public string CorrelationId;
    }



}
