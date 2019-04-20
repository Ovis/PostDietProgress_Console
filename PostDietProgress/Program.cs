using Newtonsoft.Json;
using PostDietProgress.Model;
using PostDietProgress.Service;
using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

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
            var dbSvs = new DatabaseService(setting);

            var healthPlanetSvs = new HealthPlanetService(httpClient, handler, dbSvs, setting);

            /* テーブル生成 */
            await dbSvs.CreateTable();

            /* 前回測定日時 */
            var previousDate = await dbSvs.GetPreviousDate();

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

            //OAuth処理
            await healthPlanetSvs.OAuthProcessAsync();

            /* 身体データ取得 */
            /* ログイン処理 */
            InnerScan healthData = null;
            try
            {
                healthData = JsonConvert.DeserializeObject<InnerScan>(await healthPlanetSvs.GetHealthData());
            }
            catch
            {
                try
                {
                    await healthPlanetSvs.OAuthProcessAsync(true);
                    healthData = JsonConvert.DeserializeObject<InnerScan>(await healthPlanetSvs.GetHealthData());
                }
                catch
                {
                    throw;
                }
            }
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
            var health = new HealthData(latestDate, healthList.Where(x => x.date.Equals(latestDate)).Select(x => x).ToDictionary(x => x.tag, x => x.keydata));

            /* Discordに送信 */
            var discordService = new DiscordService(setting, httpClient, dbSvs);
            await discordService.SendDiscord(health, healthData.height, latestDate);

            /* 前回情報をDBに登録 */
            await dbSvs.SetHealthData(latestDate, health);
        }


    }
}
