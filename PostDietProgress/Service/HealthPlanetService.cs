using Newtonsoft.Json;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
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
        private DatabaseService DbSvs;
        #endregion

        public HealthPlanetService(HttpClient client, HttpClientHandler handler, DatabaseService dbSvs, Settings setting)
        {
            HttpClient = client;
            Handler = handler;
            Setting = setting;
            DbSvs = dbSvs;
        }

        /// <summary>
        /// リクエストトークン取得処理(DBから取得)
        /// </summary>
        /// <returns></returns>
        public async Task GetHealthPlanetToken()
        {
            /* エンコードプロバイダーを登録(Shift-JIS用) */
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TZConvert.GetTimeZoneInfo("Tokyo Standard Time"));

            DateTime.TryParseExact(await DbSvs.GetSettingDbVal(SettingDbEnum.ExpiresIn), "yyyyMMddHHmm", new CultureInfo("ja-JP"), DateTimeStyles.AssumeLocal, out var expireDate);

            try
            {
                if (expireDate < localTime)
                {
                    /* 有効期限が切れている場合はリフレッシュトークンで改めて取得 */
                    var refreshToken = await DbSvs.GetSettingDbVal(SettingDbEnum.RefreshToken);
                    Setting.TanitaRequestToken = await GetTokenAsync(localTime, await RequestTokenAsync(refreshToken, true));
                }
                else
                {
                    Setting.TanitaRequestToken = await DbSvs.GetSettingDbVal(SettingDbEnum.RequestToken);
                }
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// HealthPlanetOAuth処理
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="passwd"></param>
        /// <returns></returns>
        public async Task OAuthProcessAsync(string userId, string passwd)
        {
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TZConvert.GetTimeZoneInfo("Tokyo Standard Time"));

            /* 認証用データをスクレイピング */
            var doc = new HtmlAgilityPack.HtmlDocument();

            /* エンコードプロバイダーを登録(Shift-JIS用) */
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            /* 認証処理 */
            /* ログイン処理 */
            var htmlData = await LoginProcess(userId, passwd);

            doc.LoadHtml(htmlData);

            var oAuthToken = doc.DocumentNode.SelectSingleNode("//input[@type='hidden' and @name='oauth_token']").Attributes["value"].Value;

            doc.LoadHtml(await GetApprovalCode(oAuthToken));

            var authCode = doc.DocumentNode.SelectSingleNode("//textarea[@readonly='readonly' and @id='code']").InnerText;

            /* リクエストトークン取得処理 */
            await GetTokenAsync(localTime, await RequestTokenAsync(authCode));
        }

        /// <summary>
        /// ログイン認証処理
        /// </summary>
        /// <returns></returns>
        public async Task<string> LoginProcess(string userId, string passwd)
        {
            /* ログイン認証先URL */
            var authUrl = new StringBuilder();
            authUrl.Append("https://www.healthplanet.jp/oauth/auth?");
            authUrl.Append("client_id=" + Setting.TanitaClientID);
            authUrl.Append("&redirect_uri=https://localhost/");
            authUrl.Append("&scope=innerscan");
            authUrl.Append("&response_type=code");

            var postString = new StringBuilder();
            postString.Append("loginId=" + userId + "&");
            postString.Append("passwd=" + passwd + "&");
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
        /// トークン情報取得
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<string> RequestTokenAsync(string token, bool reFlg = false)
        {
            var grantType = reFlg ? "refresh_token" : "authorization_code";
            var code = reFlg ? "refresh_token" : "code";

            var postString = new StringBuilder();
            postString.Append("client_id=" + Setting.TanitaClientID + "&");
            postString.Append("client_secret=" + Setting.TanitaClientSecretToken + "&");
            postString.Append("redirect_uri=" + HttpUtility.UrlEncode("http://localhost/", Encoding.GetEncoding("shift_jis")) + "&");
            postString.Append(code + "=" + token + "&");
            postString.Append("grant_type=" + grantType);

            var contentShift = new StringContent(postString.ToString(), Encoding.GetEncoding("shift_jis"), "application/x-www-form-urlencoded");

            var response = await HttpClient.PostAsync("https://www.healthplanet.jp/oauth/token", contentShift);

            using (var stream = (await response.Content.ReadAsStreamAsync()))
            using (var reader = (new StreamReader(stream, Encoding.GetEncoding("Shift_JIS"), true)) as TextReader)
            {
                return await reader.ReadToEndAsync();
            }
        }

        /// <summary>
        /// リクエストトークン取得処理
        /// </summary>
        /// <param name="localTime"></param>
        /// <returns></returns>
        public async Task<string> GetTokenAsync(DateTime localTime, string jsonData)
        {
            var tokenData = JsonConvert.DeserializeObject<Token>(jsonData);

            await DbSvs.SetSettingDbVal(SettingDbEnum.RequestToken, tokenData.access_token);
            await DbSvs.SetSettingDbVal(SettingDbEnum.ExpiresIn, localTime.AddDays(30).ToString("yyyyMMddHHmm"));
            await DbSvs.SetSettingDbVal(SettingDbEnum.RefreshToken, tokenData.refresh_token);

            return tokenData.access_token;
        }

        /// <summary>
        /// 身体データ取得
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetHealthDataAsync()
        {
            var postString = new StringBuilder();
            /* アクセストークン */
            postString.Append("access_token=" + Setting.TanitaRequestToken + "&");
            /* 測定日付で取得 */
            postString.Append("date=1&");
            /* 取得期間From,To */
            var jst = TZConvert.GetTimeZoneInfo("Tokyo Standard Time");
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, jst);
            postString.Append("from=" + localTime.AddDays(-2).ToString("yyyyMMdd") + "000000" + "&");
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

        /// <summary>
        /// 今週の平均体重を取得
        /// </summary>
        /// <param name="localTime"></param>
        /// <returns></returns>
        public async Task<double> GetWeekAverageWeightAsync(DateTime localTime)
        {
            /* 今週の体重を取得 */
            var thisWeekData = await DbSvs.GetThisWeekHealthData(localTime);
            var weightSum = 0.0;

            try
            {
                foreach (var data in thisWeekData)
                {
                    weightSum += double.Parse(data.Weight);
                }
                var count = thisWeekData.Select(d => !string.IsNullOrEmpty(d.Weight)).Count();
                return Math.Round(weightSum / count, 2);
            }
            catch (Exception)
            {

                return 0;
            }

        }
    }
}
