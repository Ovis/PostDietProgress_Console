using Newtonsoft.Json;
using PostDietProgress.Model;
using System;
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
        /// Discord投稿データ作成処理
        /// </summary>
        /// <param name="healthData">身体情報</param>
        /// <param name="height">身長</param>
        /// <param name="date">日付</param>
        /// <param name="previousDate">前回測定日付</param>
        /// <returns></returns>
        public async Task<string> CreateSendDataAsync(HealthData healthData, string height, string date)
        {
            var jst = new CultureInfo("ja-JP");
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TZConvert.GetTimeZoneInfo("Tokyo Standard Time"));

            if (!DateTime.TryParseExact(date, "yyyyMMddHHmm", null, DateTimeStyles.AssumeLocal, out var dt))
            {
                dt = localTime;
            }

            /* BMI */
            var cm = double.Parse(height) / 100;
            var weight = double.Parse(healthData.Weight);
            var bmi = Math.Round((weight / Math.Pow(cm, 2)), 2);

            /* 目標達成率 */
            var goal = Math.Round(((1 - (weight - Setting.GoalWeight) / (Setting.OriginalWeight - Setting.GoalWeight)) * 100), 2);

            /* 投稿文章 */
            var postData = dt.ToString("yyyy年MM月dd日(ddd)") + " " + dt.ToShortTimeString() + "のダイエット進捗" + Environment.NewLine
                          + "現在の体重:" + weight + "kg" + Environment.NewLine
                          + "BMI:" + bmi + Environment.NewLine
                          + "目標達成率:" + goal + "%" + Environment.NewLine;

            /* 前回測定データがあるならそれも投稿 */
            var previousHealthData = await DBSvs.GetPreviousDataAsync(healthData.DateTime, localTime);

            if (previousHealthData != null)
            {
                var previousWeight = double.Parse(previousHealthData.Weight);

                var diffWeight = Math.Round((weight - previousWeight), 2);

                DateTime.TryParseExact(previousHealthData.DateTime, "yyyyMMddHHmm", jst, DateTimeStyles.AssumeLocal, out DateTime prevDate);

                postData += "前日同時間帯測定(" + prevDate.ToString("yyyy年MM月dd日(ddd)") + " " + prevDate.ToShortTimeString() + ")から" + diffWeight.ToString() + "kgの変化" + Environment.NewLine;

                postData += diffWeight >= 0 ? (diffWeight == 0 ? "変わってない・・・。" : "増えてる・・・。") : "減った！";
            }

            return postData;
        }

        /// <summary>
        /// Discord投稿処理
        /// </summary>
        /// <param name="sendData"></param>
        /// <returns></returns>
        public async Task SendDiscordAsync(string sendData)
        {
            var jsonData = new DiscordJson
            {
                content = sendData
            };

            var json = JsonConvert.SerializeObject(jsonData);

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await HttpClient.PostAsync(Setting.DiscordWebhookUrl, content);

            using (var stream = (await response.Content.ReadAsStreamAsync()))
            using (var reader = (new StreamReader(stream, Encoding.UTF8, true)) as TextReader)
            {
                await reader.ReadToEndAsync();
            }
        }
    }
}
