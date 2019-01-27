using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Net.Http;

namespace PostDietProgress
{
    public static class Program
    {
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

        private static HttpClientHandler handler = new HttpClientHandler()
        {
            UseCookies = true
        };
        private static HttpClient httpClient = new HttpClient();

        static void Main(string[] args)
        {
            /* 定義ファイルからID,パスワード,ClientID、ClientTokenを取得 */
            string basePath = Directory.GetCurrentDirectory();

            IConfigurationRoot configuration = new ConfigurationBuilder()
           .SetBasePath(basePath)
           .AddJsonFile("App.config.json", optional: true)
           .Build();

            Settings.TanitaUserID = configuration["Setting:TanitaUserID"];
            Settings.TanitaUserPass = configuration["Setting:TanitaUserPass"];
            Settings.TanitaClientID = configuration["Setting:TanitaClientID"];
            Settings.TanitaClientSecretToken = configuration["Setting:TanitaClientSecretToken"];
            Settings.DiscordWebhookUrl = configuration["Setting:DiscordWebhookUrl"];
            Settings.OriginalWeight = Double.Parse(configuration["Setting:OriginalWeight"]);
            Settings.GoalWeight = Double.Parse(configuration["Setting:GoalWeight"]);

        }
    }
}
