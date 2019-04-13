using Dapper;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using PostDietProgress.Service;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using TimeZoneConverter;

namespace PostDietProgress
{
    public static class Program
    {
        private static readonly SQLiteConnectionStringBuilder SqlConnectionSb = new SQLiteConnectionStringBuilder { DataSource = "DietProgress.db" };

        enum HealthTag
        {
            WEIGHT = 6021, /* 体重 (kg) */
            BODYFATPERF = 6022, /* 体脂肪率(%) */
            MUSCLEMASS = 6023, /* 筋肉量(kg) */
            MUSCLESCORE = 6024, /* 筋肉スコア */
            VISCERALFATLEVEL2 = 6025, /* 内臓脂肪レベル2(小数点有り、手入力含まず) */
            VISCERALFATLEVEL = 6026, /* 内臓脂肪レベル(小数点無し、手入力含む) */
            BASALMETABOLISM = 6027, /* 基礎代謝量(kcal) */
            BODYAGE = 6028, /* 体内年齢(歳) */
            BONEQUANTITY = 6029 /* 推定骨量(kg) */
        }

        static async Task Main(string[] args)
        {
            var setting = new Settings();

            var httpClient = new HttpClient();
            var handler = new HttpClientHandler() { UseCookies = true };
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "ja-JP");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

            var healthPlanetSvs = new HealthPlanetService(httpClient, handler, setting);

            /* 前回測定日時 */
            var previousDate = "";

            /* データ保管用テーブル作成 */
            using (var dbConn = new SQLiteConnection(SqlConnectionSb.ToString()))
            {
                dbConn.Open();

                using (var cmd = dbConn.CreateCommand())
                {

                    cmd.CommandText = "CREATE TABLE IF NOT EXISTS [SETTING] (" +
                                                          "[KEY]  TEXT NOT NULL," +
                                                          "[VALUE] TEXT NOT NULL" +
                                                          ");";
                    cmd.ExecuteNonQuery();

                    using (var tran = dbConn.BeginTransaction())
                    {
                        try
                        {
                            var strBuilder = new StringBuilder();

                            strBuilder.AppendLine("INSERT INTO SETTING (KEY,VALUE) SELECT @Key, @Val WHERE NOT EXISTS(SELECT 1 FROM SETTING WHERE KEY = @Key)");
                            dbConn.Execute(strBuilder.ToString(), new { Key = "OAUTHTOKEN", Val = "" }, tran);
                            dbConn.Execute(strBuilder.ToString(), new { Key = "ACCESSTOKEN", Val = "" }, tran);
                            dbConn.Execute(strBuilder.ToString(), new { Key = "PREVIOUSMEASUREMENTDATE", Val = "" }, tran);
                            dbConn.Execute(strBuilder.ToString(), new { Key = "PREVIOUSWEIGHT", Val = "" }, tran);

                            tran.Commit();
                        }
                        catch
                        {
                            tran.Rollback();
                            return;
                        }
                    }

                    var dbObj = dbConn.Query<SettingDB>("SELECT KEY, VALUE FROM SETTING WHERE KEY = 'PREVIOUSMEASUREMENTDATE'").FirstOrDefault();

                    previousDate = dbObj?.Value;

                    if (previousDate != "")
                    {
                        if (!DateTime.TryParseExact(previousDate, "yyyyMMddHHmm", null, DateTimeStyles.AssumeLocal, out var prevDate))
                        {
                        }

                        if (prevDate > DateTime.Now.AddHours(-6))
                        {
                            return;
                        }
                    }
                }
            }

            /* 認証用データをスクレイピング */
            var doc = new HtmlAgilityPack.HtmlDocument();

            /* エンコードプロバイダーを登録(Shift-JIS用) */
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            /* 認証処理 */
            using (var dbConn = new SQLiteConnection(SqlConnectionSb.ToString()))
            {
                dbConn.Open();

                var dbObj = dbConn.Query<SettingDB>("SELECT KEY, VALUE FROM SETTING WHERE KEY = 'OAUTHTOKEN'").FirstOrDefault();

                if (dbObj != null && string.IsNullOrEmpty(dbObj.Value))
                {
                    /* ログイン処理 */
                    var htmlData = await healthPlanetSvs.LoginProcess();

                    doc.LoadHtml(htmlData);

                    setting.TanitaOAuthToken = doc.DocumentNode.SelectSingleNode("//input[@type='hidden' and @name='oauth_token']").Attributes["value"].Value;

                    using (SQLiteCommand cmd = dbConn.CreateCommand())
                    {
                        using (var tran = dbConn.BeginTransaction())
                        {
                            try
                            {
                                var strBuilder = new StringBuilder();

                                strBuilder.AppendLine("UPDATE SETTING SET VALUE = @VAL WHERE KEY = @KEY");
                                dbConn.Execute(strBuilder.ToString(), new { Key = "OAUTHTOKEN", Val = setting.TanitaOAuthToken }, tran);

                                tran.Commit();
                            }
                            catch
                            {
                                tran.Rollback();
                                return;
                            }
                        }
                    }
                }
                else
                {
                    if (dbObj != null)
                    {
                        setting.TanitaOAuthToken = dbObj.Value;
                    }
                    else
                    {
                        return;
                    }
                }
            }

            /*リクエストトークン取得処理 */
            using (var dbConn = new SQLiteConnection(SqlConnectionSb.ToString()))
            {
                dbConn.Open();

                var dbObj = dbConn.Query<SettingDB>("SELECT KEY, VALUE FROM SETTING WHERE KEY = 'ACCESSTOKEN'").FirstOrDefault();

                if (string.IsNullOrEmpty(dbObj?.Value))
                {
                    /* ログイン処理 */
                    doc.LoadHtml(await healthPlanetSvs.GetApprovalCode(setting.TanitaOAuthToken));

                    var authCode = doc.DocumentNode.SelectSingleNode("//textarea[@readonly='readonly' and @id='code']").InnerText;

                    /* リクエストトークン処理 */
                    setting.TanitaAccessToken = JsonConvert.DeserializeObject<Token>(await healthPlanetSvs.GetAccessToken(authCode)).access_token;

                    using (dbConn.CreateCommand())
                    {
                        using (var tran = dbConn.BeginTransaction())
                        {
                            try
                            {
                                var strBuilder = new StringBuilder();

                                strBuilder.AppendLine("UPDATE SETTING SET VALUE = @VAL WHERE KEY = @KEY");
                                dbConn.Execute(strBuilder.ToString(), new { Key = "ACCESSTOKEN", Val = setting.TanitaAccessToken }, tran);

                                tran.Commit();
                            }
                            catch
                            {
                                tran.Rollback();
                                return;
                            }
                        }
                    }
                }
                else
                {
                    setting.TanitaAccessToken = dbObj.Value;
                }
            }

            /* 身体データ取得 */
            /* ログイン処理 */

            var healthData = JsonConvert.DeserializeObject<InnerScan>(await healthPlanetSvs.GetHealthData());

            var healthList = healthData.data;

            /* 最新の日付のデータを取得 */
            healthList.Sort((a, b) => string.Compare(b.date, a.date));
            var latestDate = healthList.First().date.ToString();

            if (latestDate.Equals(previousDate))
            {
                //前回から計測日が変わっていない(=計測してない)ので処理を終了
                return;
            }

            /* Discordに送るためのデータをDictionary化 */
            var latestHealthData = healthList.Where(x => x.date.Equals(latestDate)).Select(x => x).ToDictionary(x => x.tag, x => x.keydata);

            /* Discordに送信 */
            await SendDiscord(latestHealthData, healthData.height, latestDate, previousDate, setting, httpClient);

            /* 前回情報をDBに登録 */
            using (var dbConn = new SQLiteConnection(SqlConnectionSb.ToString()))
            {
                dbConn.Open();

                using (var cmd = dbConn.CreateCommand())
                {
                    using (var tran = dbConn.BeginTransaction())
                    {
                        try
                        {
                            var strBuilder = new StringBuilder();

                            strBuilder.AppendLine("UPDATE SETTING SET VALUE = @VAL WHERE KEY = @KEY");
                            dbConn.Execute(strBuilder.ToString(), new { Key = "PREVIOUSMEASUREMENTDATE", Val = latestDate }, tran);
                            dbConn.Execute(strBuilder.ToString(), new { Key = "PREVIOUSWEIGHT", Val = latestHealthData[((int)HealthTag.WEIGHT).ToString()] }, tran);

                            tran.Commit();
                        }
                        catch
                        {
                            tran.Rollback();
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Discord投稿処理
        /// </summary>
        /// <param name="dic">身体情報</param>
        /// <param name="height">身長</param>
        /// <param name="date">日付</param>
        /// <param name="previousDate">前回測定日付</param>
        /// <returns></returns>
        public static async Task<string> SendDiscord(Dictionary<String, String> dic, string height, string date, string previousDate, Settings setting, HttpClient httpClient)
        {
            var jst = TZConvert.GetTimeZoneInfo("Tokyo Standard Time");
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, jst);

            if (!DateTime.TryParseExact(date, "yyyyMMddHHmm", null, DateTimeStyles.AssumeLocal, out var dt))
            {
                dt = localTime;
            }

            /* BMI */
            var cm = double.Parse(height) / 100;
            var weight = double.Parse(dic[((int)HealthTag.WEIGHT).ToString()].ToString());
            var bmi = Math.Round((weight / Math.Pow(cm, 2)), 2);

            /* 目標達成率 */
            var goal = Math.Round(((1 - (weight - setting.GoalWeight) / (setting.OriginalWeight - setting.GoalWeight)) * 100), 2);

            /* 投稿文章 */
            var postData = dt.ToString("yyyy年MM月dd日(ddd)") + " " + dt.ToShortTimeString() + "のダイエット進捗" + Environment.NewLine
                          + "現在の体重:" + weight + "kg" + Environment.NewLine
                          + "BMI:" + bmi + Environment.NewLine
                          + "目標達成率:" + goal + "%" + Environment.NewLine;

            if (previousDate != "")
            {
                var prevDate = new DateTime();
                if (!DateTime.TryParseExact(previousDate, "yyyyMMddHHmm", null, DateTimeStyles.AssumeLocal, out prevDate))
                {
                }

                /* 前回測定データがあるならそれも投稿 */
                using (var dbConn = new SQLiteConnection(SqlConnectionSb.ToString()))
                {
                    dbConn.Open();

                    var dbObj = dbConn.Query<SettingDB>("SELECT KEY, VALUE FROM SETTING WHERE KEY = 'PREVIOUSWEIGHT'").FirstOrDefault();

                    var previousWeight = double.Parse(dbObj.Value);

                    var diffWeight = Math.Round((weight - previousWeight), 2);

                    postData += "前回測定(" + prevDate.ToString("yyyy年MM月dd日(ddd)") + " " + dt.ToShortTimeString() + ")から" + diffWeight.ToString() + "kgの変化" + Environment.NewLine;

                    postData += diffWeight >= 0 ? "増えてる・・・。" : "減った！";
                }
            }

            var jsonData = new DiscordJson
            {
                content = postData
            };

            var json = JsonConvert.SerializeObject(jsonData);

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(setting.DiscordWebhookUrl, content);

            using (var stream = (await response.Content.ReadAsStreamAsync()))
            using (var reader = (new StreamReader(stream, Encoding.UTF8, true)) as TextReader)
            {
                return await reader.ReadToEndAsync();
            }
        }
    }
}
