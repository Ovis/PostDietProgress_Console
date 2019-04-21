﻿using Newtonsoft.Json;
using PostDietProgress.Model;
using PostDietProgress.Service;
using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TimeZoneConverter;

namespace PostDietProgress
{
    public static class Program
    {
        /// <summary>
        /// メイン処理
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        static async Task Main(string[] args)
        {
            var setting = new Settings();

            var httpClient = new HttpClient();
            var handler = new HttpClientHandler() { UseCookies = true };
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "ja-JP");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            var dbSvs = new DatabaseService(setting);

            var healthPlanetSvs = new HealthPlanetService(httpClient, handler, dbSvs, setting);

            if (args.Length == 0)
            {
                /* 現在時刻(日本時間)取得 */
                var localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TZConvert.GetTimeZoneInfo("Tokyo Standard Time"));

                var discordService = new DiscordService(setting, httpClient, dbSvs);

                /* エラーフラグ確認 */
                var errorFlag = await dbSvs.GetSettingDbVal(SettingDbEnum.ErrorFlag);

                if(errorFlag == "1")
                {
                    return;
                }

                /* 前回測定日時 */
                var previousDate = await dbSvs.GetSettingDbVal(SettingDbEnum.PreviousMeasurememtDate);

                if (!string.IsNullOrEmpty(previousDate))
                {
                    DateTime.TryParseExact(previousDate, "yyyyMMddHHmm", new CultureInfo("ja-JP"), DateTimeStyles.AssumeLocal, out var prevDate);
                    if (prevDate > localTime.AddHours(-6))
                    {
                        return;
                    }
                }

                //リクエストトークン取得
                await healthPlanetSvs.GetHealthPlanetToken();

                /* 身体データ取得 */
                InnerScan healthData = null;
                try
                {
                    healthData = JsonConvert.DeserializeObject<InnerScan>(await healthPlanetSvs.GetHealthDataAsync());
                }
                catch
                {
                    await discordService.SendDiscordAsync("身体データの取得に失敗しました。トークンの有効期限が切れた可能性があります。");
                    await dbSvs.SetSettingDbVal(SettingDbEnum.ErrorFlag, "1");
                    throw;
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
                var sendData = await discordService.CreateSendDataAsync(health, healthData.height, latestDate);
                await discordService.SendDiscordAsync(sendData);

                /* 前回情報をDBに登録 */
                await dbSvs.SetHealthData(latestDate, health);
            }
            else if (args.Length == 2)
            {
                var userId = args[0];
                var passwd = args[1];
                //初期処理
                await dbSvs.CreateTable();
                await healthPlanetSvs.OAuthProcessAsync(userId, passwd);
                await dbSvs.SetSettingDbVal(SettingDbEnum.ErrorFlag, "0");
                Console.WriteLine("初期処理が完了しました。");
                return;
            }
            else
            {
                Console.WriteLine("引数の個数が誤っています。");
            }
        }
    }
}
