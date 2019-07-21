using Google.Apis.Auth.OAuth2;
using Google.Apis.Fitness.v1;
using Google.Apis.Fitness.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Newtonsoft.Json;
using PostDietProgress.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PostDietProgress.Service
{
    public class GoogleFitService
    {
        #region prop
        private Settings Setting;
        private HttpClient HttpClient;
        #endregion

        private static readonly DateTime unixEpochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private const string userId = "me";
        private const string dataTypeName = "com.google.weight";
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public GoogleFitService(Settings settings, HttpClient client)
        {
            Setting = settings;
            HttpClient = client;
        }

        /// <summary>
        /// UNIX時間でのナノ秒値
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
        public static long GetUnixEpochNanoSeconds(DateTime dt)
        {
            return (dt.Ticks - unixEpochStart.Ticks) * 100;
        }

        /// <summary>
        /// GoogleFit投稿処理
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public async Task PostGoogleFit(HealthData health)
        {
            try
            {
                UserCredential credential;
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    new ClientSecrets
                    {
                        ClientId = Setting.GoogleFitClientId,
                        ClientSecret = Setting.GoogleFitClientSecret
                    },
                    new string[]
                    {
                        FitnessService.Scope.FitnessBodyRead,
                        FitnessService.Scope.FitnessBodyWrite
                    },
                    "user",
                    CancellationToken.None,
                    new FileDataStore("GoogleFitnessAuth", true)//trueにするとカレントパスに保存
                    );

                var fitnessService = new FitnessService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = Assembly.GetExecutingAssembly().GetName().Name
                });

                var dataSource = new DataSource()
                {
                    Type = "derived",
                    DataStreamName = "HealthPlanetCooperation",
                    Application = new Application()
                    {
                        Name = "TanitaHealthPlanet",
                        Version = "1"
                    },
                    DataType = new DataType()
                    {
                        Name = dataTypeName,
                        Field = new List<DataTypeField>()
                    {
                        new DataTypeField() {Name = "weight", Format = "floatPoint"}
                    }
                    },
                    Device = new Device()
                    {
                        Manufacturer = "Tanita",
                        Model = "RD-906",
                        Type = "scale",
                        Uid = "1000001",
                        Version = "1.0"
                    }
                };

                var dataSourceId = $"{dataSource.Type}:{dataSource.DataType.Name}:{Setting.GoogleFitClientId.Split('-')[0]}:{dataSource.Device.Manufacturer}:{dataSource.Device.Model}:{dataSource.Device.Uid}:{dataSource.DataStreamName}";

                var dataSrcList = await fitnessService.Users.DataSources.List(userId).ExecuteAsync();

                if (dataSrcList.DataSource.Select(s => s.DataStreamId).Any(s => s == dataSourceId))
                {
                    dataSource = fitnessService.Users.DataSources.Get(userId, dataSourceId).Execute();
                }
                else
                {
                    dataSource = fitnessService.Users.DataSources.Create(dataSource, userId).Execute();
                }

                var jst = new CultureInfo("ja-JP");

                if (!DateTime.TryParseExact(health.DateTime, "yyyyMMddHHmm", null, DateTimeStyles.AssumeLocal, out var dt))
                {
                    dt = Setting.LocalTime;
                }

                var postNanosec = GetUnixEpochNanoSeconds(dt.ToUniversalTime());
                var widthDataSource = new Dataset()
                {
                    DataSourceId = dataSourceId,
                    MaxEndTimeNs = postNanosec,
                    MinStartTimeNs = postNanosec,
                    Point = new List<DataPoint>()
                    {
                        new DataPoint()
                        {
                            DataTypeName = dataTypeName,
                            StartTimeNanos = postNanosec,
                            EndTimeNanos = postNanosec,
                            Value = new List<Value>()
                            {
                                new Value()
                                {
                                    FpVal = float.Parse(health.Weight)
                                }
                            }
                        }
                    }
                };

                var dataSetId = $"{postNanosec}-{postNanosec}";
                await fitnessService.Users.DataSources.Datasets.Patch(widthDataSource, userId, dataSourceId, dataSetId).ExecuteAsync();

            }
            catch (Exception e)
            {
                logger.Info(e.Message + " : " + e.StackTrace);
                throw;
            }
        }

        public async Task GetGoogleOAuth()
        {
            var deviceCode = await GetGoogleOAuthDeviceCode();

            await GetGoogleOAuthToken(deviceCode);
        }

        /// <summary>
        /// GoogleAPIOAuthデバイスコード取得処理
        /// </summary>
        /// <returns></returns>
        private async Task<string> GetGoogleOAuthDeviceCode()
        {
            try
            {
                var postString = new StringBuilder();
                postString.Append("client_id=" + Setting.GoogleFitClientId + "&");
                postString.Append("scope=" + FitnessService.Scope.FitnessBodyRead + " " + FitnessService.Scope.FitnessBodyWrite + "&");

                var content = new StringContent(postString.ToString(), Encoding.UTF8, "application/x-www-form-urlencoded");

                var response = await HttpClient.PostAsync("https://accounts.google.com/o/oauth2/device/code", content);

                using (var stream = (await response.Content.ReadAsStreamAsync()))
                using (var reader = (new StreamReader(stream, Encoding.UTF8, true)) as TextReader)
                {
                    var resultData = await reader.ReadToEndAsync();
                    var json = JsonConvert.DeserializeObject<GoogleUserCode>(resultData);

                    Console.WriteLine("Your UserCode is :" + json.user_code);
                    Console.WriteLine("Verification Url is :" + json.verification_url);
                    Console.WriteLine("Device authorization is required.");
                    Console.WriteLine("Press Enter after you approve.");
                    Console.ReadLine();

                    return json.device_code;
                }
            }
            catch (Exception e)
            {
                logger.Info(e.Message + " : " + e.StackTrace);
                throw;
            }
        }

        /// <summary>
        /// GoogleOAuthアクセストークン取得処理
        /// </summary>
        /// <param name="deviceCode"></param>
        /// <returns></returns>
        private async Task GetGoogleOAuthToken(string deviceCode)
        {
            try
            {
                var currentDateTime = DateTime.Now;
                var postString = new StringBuilder();
                postString.Append("client_id=" + Setting.GoogleFitClientId + "&");
                postString.Append("client_secret=" + Setting.GoogleFitClientSecret + "&");
                postString.Append("code=" + deviceCode + "&");
                postString.Append("grant_type=" + "http://oauth.net/grant_type/device/1.0" + "&");

                var content = new StringContent(postString.ToString(), Encoding.UTF8, "application/x-www-form-urlencoded");

                var response = await HttpClient.PostAsync("https://accounts.google.com/o/oauth2/token", content);

                using (var stream = (await response.Content.ReadAsStreamAsync()))
                using (var reader = (new StreamReader(stream, Encoding.UTF8, true)) as TextReader)
                {
                    var resultData = await reader.ReadToEndAsync();
                    var json = JsonConvert.DeserializeObject<GoogleOAuthToken>(resultData);

                    json.scope = FitnessService.Scope.FitnessBodyRead + " " + FitnessService.Scope.FitnessBodyWrite;
                    json.IssuedUtc = currentDateTime.AddHours(-9).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
                    json.Issued = currentDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'+09:00'");

                    var deseriarize = JsonConvert.SerializeObject(json);
                    var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GoogleFitnessAuth");
                    var fileName = Path.Combine(filePath, "Google.Apis.Auth.OAuth2.Responses.TokenResponse-user");

                    if (!Directory.Exists(filePath))
                    {
                        Directory.CreateDirectory(filePath);
                    }

                    using (var writer = new StreamWriter(fileName, false))
                    {
                        writer.WriteLine(deseriarize);
                    }
                }
            }
            catch (Exception e)
            {
                logger.Info(e.Message + " : " + e.StackTrace);
                throw;
            }
        }
    }
}
