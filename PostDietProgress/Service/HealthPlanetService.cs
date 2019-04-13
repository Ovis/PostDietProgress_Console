using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using TimeZoneConverter;

namespace PostDietProgress.Service
{
    public class HealthPlanetService
    {
        #region prop
        private HttpClientHandler Handler;
        private HttpClient HttpClient;
        private Settings Setting;
        #endregion

        public HealthPlanetService(HttpClient client, HttpClientHandler handler, Settings setting)
        {
            HttpClient = client;
            Handler = handler;
            Setting = setting;
        }

        /// <summary>
        /// ログイン認証処理
        /// </summary>
        /// <returns></returns>
        public async Task<string> LoginProcess()
        {
            /* ログイン認証先URL */
            var authUrl = new StringBuilder();
            authUrl.Append("https://www.healthplanet.jp/oauth/auth?");
            authUrl.Append("client_id=" + Setting.TanitaClientID);
            authUrl.Append("&redirect_uri=https://localhost/");
            authUrl.Append("&scope=innerscan");
            authUrl.Append("&response_type=code");

            var postString = new StringBuilder();
            postString.Append("loginId=" + Setting.TanitaUserID + "&");
            postString.Append("passwd=" + Setting.TanitaUserPass + "&");
            postString.Append("send=1&");
            postString.Append("url=" + HttpUtility.UrlEncode(authUrl.ToString(), Encoding.GetEncoding("shift_jis")));

            var contentShift = new StringContent(postString.ToString(), Encoding.GetEncoding("shift_jis"), "application/x-www-form-urlencoded");

            var response = await HttpClient.PostAsync("https://www.healthplanet.jp/login_oauth.do", contentShift);

            var cookies = Handler.CookieContainer.GetCookies(new Uri("https://www.healthplanet.jp/"));

            using (var stream = (await response.Content.ReadAsStreamAsync()))
            using (var reader = (new StreamReader(stream, Encoding.GetEncoding("Shift_JIS"), true)) as TextReader)
            {
                return await reader.ReadToEndAsync();
            }
        }

        /// <summary>
        /// トークン取得コード取得
        /// </summary>
        /// <param name="oAuthToken"></param>
        /// <returns></returns>
        public async Task<string> GetApprovalCode(string oAuthToken)
        {
            var postString = new StringBuilder();
            postString.Append("approval=true&");
            postString.Append("oauth_token=" + oAuthToken + "&");

            var contentShift = new StringContent(postString.ToString(), Encoding.GetEncoding("shift_jis"), "application/x-www-form-urlencoded");

            var response = await HttpClient.PostAsync("https://www.healthplanet.jp/oauth/approval.do", contentShift);

            using (var stream = (await response.Content.ReadAsStreamAsync()))
            using (var reader = (new StreamReader(stream, Encoding.GetEncoding("Shift_JIS"), true)) as TextReader)
            {
                return await reader.ReadToEndAsync();
            }
        }

        /// <summary>
        /// アクセストークン取得
        /// </summary>
        /// <param name="oAuthToken"></param>
        /// <returns></returns>
        public async Task<string> GetAccessToken(string oAuthToken)
        {
            var postString = new StringBuilder();
            postString.Append("client_id=" + Setting.TanitaClientID + "&");
            postString.Append("client_secret=" + Setting.TanitaClientSecretToken + "&");
            postString.Append("redirect_uri=" + HttpUtility.UrlEncode("http://localhost/", Encoding.GetEncoding("shift_jis")) + "&");
            postString.Append("code=" + oAuthToken + "&");
            postString.Append("grant_type=authorization_code");

            var contentShift = new StringContent(postString.ToString(), Encoding.GetEncoding("shift_jis"), "application/x-www-form-urlencoded");

            var response = await HttpClient.PostAsync("https://www.healthplanet.jp/oauth/token", contentShift);

            using (var stream = (await response.Content.ReadAsStreamAsync()))
            using (var reader = (new StreamReader(stream, Encoding.GetEncoding("Shift_JIS"), true)) as TextReader)
            {
                return await reader.ReadToEndAsync();
            }
        }

        /// <summary>
        /// 身体データ取得
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetHealthData()
        {
            var postString = new StringBuilder();
            /* アクセストークン */
            postString.Append("access_token=" + Setting.TanitaAccessToken + "&");
            /* 測定日付で取得 */
            postString.Append("date=1&");
            /* 取得期間From,To */
            var jst = TZConvert.GetTimeZoneInfo("Tokyo Standard Time");
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, jst);
            postString.Append("from=" + localTime.AddMonths(-3).ToString("yyyyMMdd") + "000000" + "&");
            postString.Append("to=" + localTime.ToString("yyyyMMdd") + "235959" + "&");
            /* 取得データ */
            postString.Append("tag=6021,6022,6023,6024,6025,6026,6027,6028,6029" + "&");

            var contentShift = new StringContent(postString.ToString(), Encoding.UTF8, "application/x-www-form-urlencoded");

            var response = await HttpClient.PostAsync("https://www.healthplanet.jp/status/innerscan.json", contentShift);

            using (var stream = (await response.Content.ReadAsStreamAsync()))
            using (var reader = (new StreamReader(stream, Encoding.UTF8, true)) as TextReader)
            {
                return await reader.ReadToEndAsync();
            }
        }
    }
}
