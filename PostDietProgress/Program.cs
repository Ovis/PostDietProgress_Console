﻿using Newtonsoft.Json;
using PostDietProgress.Service;
using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
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
            var discordService = new DiscordService(setting,httpClient,dbSvs);
            await discordService.SendDiscord(latestHealthData, healthData.height, latestDate, previousDate);

            /* 前回情報をDBに登録 */
            dbSvs.SetHealthData(latestDate, latestHealthData);
        }
    }
}
