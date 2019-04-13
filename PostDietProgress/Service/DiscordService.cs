using Newtonsoft.Json;
using PostDietProgress.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TimeZoneConverter;

namespace PostDietProgress.Service
{
    class DiscordService
    {
        Settings Setting;
        HttpClient HttpClient;
        DatabaseService DBSvs;

        public DiscordService(Settings setting, HttpClient httpClient, DatabaseService dbSvs)
        {
            Setting = setting;
            HttpClient = httpClient;
            DBSvs = dbSvs;
        }

        /// <summary>
        /// Discord投稿処理
        /// </summary>
        /// <param name="dic">身体情報</param>
        /// <param name="height">身長</param>
        /// <param name="date">日付</param>
        /// <param name="previousDate">前回測定日付</param>
        /// <returns></returns>
        public async Task<string> SendDiscord(Dictionary<String, String> dic, string height, string date, string previousDate)
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
            var goal = Math.Round(((1 - (weight - Setting.GoalWeight) / (Setting.OriginalWeight - Setting.GoalWeight)) * 100), 2);

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
                var previousWeight = double.Parse(DBSvs.GetPreviousData());

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

            var response = await HttpClient.PostAsync(Setting.DiscordWebhookUrl, content);

            using (var stream = (await response.Content.ReadAsStreamAsync()))
            using (var reader = (new StreamReader(stream, Encoding.UTF8, true)) as TextReader)
            {
                return await reader.ReadToEndAsync();
            }
        }
    }
}
