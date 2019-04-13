using Newtonsoft.Json;
using PostDietProgress.Model;
using PostDietProgress.Service;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TimeZoneConverter;

namespace PostDietProgress
{
    public static class Program
    {
        static async Task Main(string[] args)
        {
            var setting = new Settings();

            var httpClient = new HttpClient();
            var handler = new HttpClientHandler() { UseCookies = true };
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "ja-JP");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

            var healthPlanetSvs = new HealthPlanetService(httpClient, handler, setting);

            var dbSvs = new DatabaseService(setting);

            /* テーブル生成 */
            dbSvs.CreateTable();

            /* 前回測定日時 */
            var previousDate = dbSvs.GetPreviousDate();

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

            /* 認証用データをスクレイピング */
            var doc = new HtmlAgilityPack.HtmlDocument();

            /* エンコードプロバイダーを登録(Shift-JIS用) */
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            /* 認証処理 */
            var ret = dbSvs.GetOAuthToken();

            if (ret == null)
            {
                /* ログイン処理 */
                var htmlData = await healthPlanetSvs.LoginProcess();

                doc.LoadHtml(htmlData);

                setting.TanitaOAuthToken = doc.DocumentNode.SelectSingleNode("//input[@type='hidden' and @name='oauth_token']").Attributes["value"].Value;

                dbSvs.SetOAuthToken();
            }
            else
            {
                setting.TanitaOAuthToken = ret;
            }

            /*リクエストトークン取得処理 */
            ret = dbSvs.GetAccessToken();

            if (ret == null)
            {
                /* ログイン処理 */
                doc.LoadHtml(await healthPlanetSvs.GetApprovalCode(setting.TanitaOAuthToken));

                var authCode = doc.DocumentNode.SelectSingleNode("//textarea[@readonly='readonly' and @id='code']").InnerText;

                /* リクエストトークン処理 */
                setting.TanitaAccessToken = JsonConvert.DeserializeObject<Token>(await healthPlanetSvs.GetAccessToken(authCode)).access_token;
                dbSvs.SetAccessToken();
            }
            else
            {
                setting.TanitaAccessToken = ret;
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
            await SendDiscord(latestHealthData, healthData.height, latestDate, previousDate, setting, httpClient, dbSvs);

            /* 前回情報をDBに登録 */
            dbSvs.SetHealthData(latestDate, latestHealthData);
        }

        /// <summary>
        /// Discord投稿処理
        /// </summary>
        /// <param name="dic">身体情報</param>
        /// <param name="height">身長</param>
        /// <param name="date">日付</param>
        /// <param name="previousDate">前回測定日付</param>
        /// <returns></returns>
        public static async Task<string> SendDiscord(Dictionary<String, String> dic, string height, string date, string previousDate, Settings setting, HttpClient httpClient, DatabaseService dbSvs)
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
                var previousWeight = double.Parse(dbSvs.GetPreviousData());

                var diffWeight = Math.Round((weight - previousWeight), 2);

                postData += "前回測定(" + prevDate.ToString("yyyy年MM月dd日(ddd)") + " " + dt.ToShortTimeString() + ")から" + diffWeight.ToString() + "kgの変化" + Environment.NewLine;

                postData += diffWeight >= 0 ? "増えてる・・・。" : "減った！";

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
