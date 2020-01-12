using Newtonsoft.Json;
using PostDietProgress.Model;
using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace PostDietProgress.Service
{
    class DiscordService
    {
        Settings _setting;
        HttpClient _httpClient;
        DatabaseService _dbSvs;
        HealthPlanetService _healthPlanetSvs;

        public DiscordService(Settings setting, HttpClient httpClient, DatabaseService dbSvs, HealthPlanetService healthPlanetSvs)
        {
            _setting = setting;
            _httpClient = httpClient;
            _dbSvs = dbSvs;
            _healthPlanetSvs = healthPlanetSvs;
        }

        /// <summary>
        /// Discord投稿データ作成処理
        /// </summary>
        /// <param name="healthData">身体情報</param>
        /// <param name="height">身長</param>
        /// <param name="date">日付</param>
        /// <returns></returns>
        public async Task<string> CreateSendDataAsync(HealthData healthData, string height, string date)
        {
            var jst = new CultureInfo("ja-JP");
            var utc = new CultureInfo("en-US");

            if (!DateTime.TryParseExact(date, "yyyyMMddHHmm", null, DateTimeStyles.AssumeLocal, out var dt))
            {
                dt = _setting.LocalTime;
            }

            /* BMI */
            var cm = double.Parse(height) / 100;
            var weight = double.Parse(healthData.Weight);
            var bmi = Math.Round((weight / Math.Pow(cm, 2)), 2);

            /* 目標達成率 */
            var goal = Math.Round(((1 - (weight - _setting.GoalWeight) / (_setting.OriginalWeight - _setting.GoalWeight)) * 100), 2);

            /* 投稿文章 */
            var postData = dt.ToString("yyyy年MM月dd日(ddd)") + " " + dt.ToShortTimeString() + "のダイエット進捗" + Environment.NewLine
                          + "現在の体重:" + weight + "kg" + Environment.NewLine
                          + "BMI:" + bmi + Environment.NewLine
                          + "目標達成率:" + goal + "%" + Environment.NewLine;

            /* 前回測定データがあるならそれも投稿 */
            var previousHealthData = await _dbSvs.GetPreviousDataAsync(healthData.DateTime);

            if (previousHealthData != null)
            {
                var previousWeight = double.Parse(previousHealthData.Weight);

                var diffWeight = Math.Round((weight - previousWeight), 2);

                DateTime.TryParseExact(previousHealthData.DateTime, "yyyyMMddHHmm", jst, DateTimeStyles.AssumeLocal, out DateTime prevDate);

                postData += "前日同時間帯測定(" + prevDate.ToString("yyyy年MM月dd日(ddd)") + " " + prevDate.ToShortTimeString() + ")から" + diffWeight + "kgの変化" + Environment.NewLine;

                postData += diffWeight >= 0 ? (Math.Abs(diffWeight) < 0.00000001 ? "変わってない・・・。" : "増えてる・・・。") : "減った！";
            }

            /* 日曜日なら移動平均計算 */
            var tmpHm = new TimeSpan(dt.Ticks);
            var nightStart = new TimeSpan(18, 00, 00);
            var nightEnd = new TimeSpan(05, 00, 00);
            if (dt.ToString("ddd", utc) == "Sun" && (tmpHm > nightStart || tmpHm < nightEnd))
            {
                /* 前週の体重平均値を取得 */
                var prevWeekWeight = await _dbSvs.GetSettingDbVal(SettingDbEnum.PrevWeekWeight);
                if (!string.IsNullOrEmpty(prevWeekWeight))
                {
                    var thisWeekWeightAverage = await _healthPlanetSvs.GetWeekAverageWeightAsync();

                    var averageWeight = Math.Round((thisWeekWeightAverage - double.Parse(prevWeekWeight)), 2);

                    postData += "前週の平均体重:" + prevWeekWeight + "kg   今週の平均体重:" + thisWeekWeightAverage + "kg" + Environment.NewLine;
                    postData += "移動平均値: " + averageWeight + "kg" + Environment.NewLine;
                    //2進浮動小数点数の比較のため
                    //https://dobon.net/vb/dotnet/beginner/floatingpointerror.html
                    postData += averageWeight >= 0 ? (Math.Abs(averageWeight) < 0.00000001 ? "変わってない・・・。" : "増えてる・・・。") : "減った！";
                }
                else
                {
                    var thisWeekWeightAverage = await _healthPlanetSvs.GetWeekAverageWeightAsync();
                    await _dbSvs.SetSettingDbVal(SettingDbEnum.PrevWeekWeight, thisWeekWeightAverage.ToString(CultureInfo.CurrentCulture));
                }
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
                Content = sendData
            };

            var json = JsonConvert.SerializeObject(jsonData);

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_setting.DiscordWebhookUrl, content);

            using (var stream = (await response.Content.ReadAsStreamAsync()))
            using (var reader = (new StreamReader(stream, Encoding.UTF8, true)) as TextReader)
            {
                await reader.ReadToEndAsync();
            }
        }
    }
}
