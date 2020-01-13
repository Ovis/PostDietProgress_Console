using Newtonsoft.Json;
using PostDietProgress.Model;
using PostDietProgress.Service;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace PostDietProgress
{
    public static class Program
    {
        /// <summary>
        /// メイン処理
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static async Task Main(string[] args)
        {
            var setting = new Settings();

            var httpClient = new HttpClient();
            var handler = new HttpClientHandler() { UseCookies = true };
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "ja-JP");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            var dbSvs = new DatabaseService(setting);

            /* エンコードプロバイダーを登録(Shift-JIS用) */
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var healthPlanetSvs = new HealthPlanetService(httpClient, handler, dbSvs, setting);

            switch (args.Length)
            {
                case 0:
                    {
                        var discordService = new DiscordService(setting, httpClient, dbSvs, healthPlanetSvs);

                        /* エラーフラグ確認 */
                        var errorFlag = await dbSvs.GetSettingDbVal(SettingDbEnum.ErrorFlag);

                        switch (errorFlag)
                        {
                            case "1":
                                await healthPlanetSvs.GetRefreshToken();
                                try
                                {
                                    JsonConvert.DeserializeObject<InnerScan>(await healthPlanetSvs.GetHealthDataAsync());
                                }
                                catch
                                {
                                    await discordService.SendDiscordAsync("トークンを更新しましたが、データの取得に失敗しました。");
                                    await dbSvs.SetSettingDbVal(SettingDbEnum.ErrorFlag, "2");
                                    throw;
                                }
                                await dbSvs.SetSettingDbVal(SettingDbEnum.ErrorFlag, "0");
                                return;
                            case "2":
                                return;
                        }

                        /* 前回測定日時 */
                        var previousDate = await dbSvs.GetSettingDbVal(SettingDbEnum.PreviousMeasurementDate);

#if DEBUG
                        previousDate = null;
#endif
                        if (!string.IsNullOrEmpty(previousDate))
                        {
                            DateTime.TryParseExact(previousDate, "yyyyMMddHHmm", new CultureInfo("ja-JP"), DateTimeStyles.AssumeLocal, out var prevDate);
                            if (prevDate > setting.LocalTime.AddHours(-6))
                            {
                                return;
                            }
                        }

                        /* リクエストトークン取得 */
                        await healthPlanetSvs.GetHealthPlanetToken();

                        /* 身体データ取得 */
                        InnerScan healthData;
                        try
                        {
                            healthData = JsonConvert.DeserializeObject<InnerScan>(await healthPlanetSvs.GetHealthDataAsync());
                        }
                        catch
                        {
                            await discordService.SendDiscordAsync("身体データの取得に失敗しました。");
                            await dbSvs.SetSettingDbVal(SettingDbEnum.ErrorFlag, "1");
                            throw;
                        }

                        var healthList = healthData.Data;

                        /* 最新の日付のデータを取得 */
                        healthList.Sort((a, b) => string.CompareOrdinal(b.Date, a.Date));
                        var latestDate = healthList.First().Date;

                        if (latestDate.Equals(previousDate))
                        {
                            /* 前回から計測日が変わっていない(=計測してない)ので処理を終了 */
                            return;
                        }

                        /* 取得したデータから日時情報を抜き出す */
                        var healthDateList = healthList.Select(x => x.Date).Distinct().ToList();

                        /* HealthPlanetから取得した情報をDictionary化 */
                        var healthPlanetDataList = new List<HealthData>();
                        foreach (var healthDate in healthDateList)
                        {
                            healthPlanetDataList.Add(new HealthData(healthDate, healthList.Where(x => x.Date.Equals(healthDate)).Select(x => x).ToDictionary(x => x.Tag, x => x.Keydata)));
                        }

                        /* DBに登録した情報の取得 */
                        var acquiredHealthDataList = await dbSvs.GetHealthDataByDateList(healthDateList);

                        /* DB未登録のデータを取得 */
                        var unRegisteredHealthDataList = healthDateList.Where(y => y != latestDate).Except(acquiredHealthDataList.Select(x => x.DateTime).ToList()).ToList();

                        foreach (var healthDateTime in unRegisteredHealthDataList)
                        {
                            /* DB未登録データを登録する */
                            var health = healthPlanetDataList.First(x => x.DateTime == healthDateTime);
                            await dbSvs.SetHealthData(health);
                            if (setting.PostGoogleFit)
                            {
                                var googleFitService = new GoogleFitService(setting, httpClient);
                                await googleFitService.PostGoogleFit(health);
                            }
                        }

                        /* Discordに送るためのデータ */
                        var postHealthData = healthPlanetDataList.First(x => x.DateTime == latestDate);

                        /* Discordに送信 */
                        var sendData = await discordService.CreateSendDataAsync(postHealthData, healthData.Height, latestDate);
                        await discordService.SendDiscordAsync(sendData);

                        if (setting.PostGoogleFit)
                        {
                            var googleFitService = new GoogleFitService(setting, httpClient);
                            await googleFitService.PostGoogleFit(postHealthData);
                        }

                        /* 前回情報をDBに登録 */
                        await dbSvs.SetHealthData(postHealthData);
                        break;
                    }
                case 2:
                    {
                        var userId = args[0];
                        var passwd = args[1];
                        //初期処理
                        await dbSvs.CreateTable();
                        await healthPlanetSvs.OAuthProcessAsync(userId, passwd);
                        await dbSvs.SetSettingDbVal(SettingDbEnum.ErrorFlag, "0");

                        //GoogleAPI処理
                        if (setting.PostGoogleFit)
                        {
                            var googleFitService = new GoogleFitService(setting, httpClient);
                            await googleFitService.GetGoogleOAuth();
                        }

                        Console.WriteLine("初期処理が完了しました。");
                        return;
                    }
                default:
                    Console.WriteLine("引数の個数が誤っています。");
                    break;
            }
        }
    }
}
