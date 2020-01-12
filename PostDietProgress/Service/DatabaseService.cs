using Dapper;
using Microsoft.Data.Sqlite;
using PostDietProgress.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PostDietProgress.Service
{
    public class DatabaseService
    {
        Settings Setting;
        public DatabaseService(Settings setting)
        {
            Setting = setting;
        }

        /// <summary>
        /// テーブル生成
        /// </summary>
        /// <returns></returns>
        public async Task CreateTable()
        {
            /* データ保管用テーブル作成 */
            using (var dbConn = new SqliteConnection(Setting.SqlConnectionSb.ToString()))
            {
                await dbConn.OpenAsync();
                using (var cmd = dbConn.CreateCommand())
                {

                    cmd.CommandText = "CREATE TABLE IF NOT EXISTS [SETTING] (" +
                                                          "[KEY]  TEXT NOT NULL," +
                                                          "[VALUE] TEXT NOT NULL" +
                                                          ");";
                    await cmd.ExecuteNonQueryAsync();

                    using (var tran = dbConn.BeginTransaction())
                    {
                        try
                        {
                            var strBuilder = new StringBuilder();

                            strBuilder.AppendLine("INSERT INTO SETTING (KEY,VALUE) SELECT @Key, @Val WHERE NOT EXISTS(SELECT 1 FROM SETTING WHERE KEY = @Key)");
                            await dbConn.ExecuteAsync(strBuilder.ToString(), new { Key = "REQUESTTOKEN", Val = "" }, tran);
                            await dbConn.ExecuteAsync(strBuilder.ToString(), new { Key = "EXPIRESIN", Val = "" }, tran);
                            await dbConn.ExecuteAsync(strBuilder.ToString(), new { Key = "REFRESHTOKEN", Val = "" }, tran);
                            await dbConn.ExecuteAsync(strBuilder.ToString(), new { Key = "PREVIOUSMEASUREMENTDATE", Val = "" }, tran);
                            await dbConn.ExecuteAsync(strBuilder.ToString(), new { Key = "PREVIOUSWEIGHT", Val = "" }, tran);
                            await dbConn.ExecuteAsync(strBuilder.ToString(), new { Key = "PREVWEEKWEIGHT", Val = "" }, tran);
                            await dbConn.ExecuteAsync(strBuilder.ToString(), new { Key = "ERRORFLAG", Val = "0" }, tran);

                            tran.Commit();
                        }
                        catch
                        {
                            tran.Rollback();
                            throw;
                        }
                    }

                    var tblCreateText = new StringBuilder();
                    tblCreateText.AppendLine("CREATE TABLE IF NOT EXISTS [HEALTHDATA] (");
                    tblCreateText.AppendLine("[DATETIME] TEXT NOT NULL,");
                    tblCreateText.AppendLine(" [WEIGHT] TEXT,");
                    tblCreateText.AppendLine(" [BODYFATPERF] TEXT,");
                    tblCreateText.AppendLine(" [MUSCLEMASS] TEXT,");
                    tblCreateText.AppendLine(" [MUSCLESCORE] TEXT,");
                    tblCreateText.AppendLine(" [VISCERALFATLEVEL2] TEXT,");
                    tblCreateText.AppendLine(" [VISCERALFATLEVEL] TEXT,");
                    tblCreateText.AppendLine(" [BASALMETABOLISM] TEXT,");
                    tblCreateText.AppendLine(" [BODYAGE] TEXT,");
                    tblCreateText.AppendLine(" [BONEQUANTITY] TEXT");
                    tblCreateText.AppendLine(");");
                    cmd.CommandText = tblCreateText.ToString();

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// 設定データDB取得
        /// </summary>
        /// <param name="keyType"></param>
        /// <returns></returns>
        public async Task<string> GetSettingDbVal(SettingDbEnum keyType)
        {
            var key = GetDbKey(keyType);

            using (var dbConn = new SqliteConnection(Setting.SqlConnectionSb.ToString()))
            {
                var dbObj = (await dbConn.QueryAsync<SettingDB>("SELECT KEY, VALUE FROM SETTING WHERE KEY = '" + key + "'")).FirstOrDefault();

                return dbObj == null ? null : (string.IsNullOrEmpty(dbObj.Value) ? null : dbObj.Value);
            }
        }

        /// <summary>
        /// 設定データDB登録
        /// </summary>
        /// <param name="keyType"></param>
        /// <param name="val"></param>
        /// <returns></returns>
        public async Task SetSettingDbVal(SettingDbEnum keyType, string val)
        {
            var key = GetDbKey(keyType);

            using (var dbConn = new SqliteConnection(Setting.SqlConnectionSb.ToString()))
            {
                await dbConn.OpenAsync();
                using (var tran = dbConn.BeginTransaction())
                {
                    try
                    {
                        var strBuilder = new StringBuilder();

                        strBuilder.AppendLine("UPDATE SETTING SET VALUE = @Val WHERE KEY = @Key");
                        await dbConn.ExecuteAsync(strBuilder.ToString(), new { Key = key, Val = val }, tran);

                        tran.Commit();
                    }
                    catch
                    {
                        tran.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// 身体データDB登録
        /// </summary>
        /// <param name="latestDate"></param>
        /// <param name="healthData"></param>
        /// <returns></returns>
        public async Task SetHealthData(string latestDate, HealthData healthData)
        {
            await SetSettingDbVal(SettingDbEnum.PreviousMeasurememtDate, latestDate);
            await SetSettingDbVal(SettingDbEnum.PreviousWeight, healthData.Weight);

            using (var dbConn = new SqliteConnection(Setting.SqlConnectionSb.ToString()))
            {
                await dbConn.OpenAsync();
                using (var tran = dbConn.BeginTransaction())
                {
                    try
                    {
                        var healthDataText = new StringBuilder();

                        healthDataText.AppendLine("INSERT INTO HEALTHDATA (");
                        healthDataText.AppendLine("DATETIME,WEIGHT,BODYFATPERF,MUSCLEMASS,MUSCLESCORE,VISCERALFATLEVEL2,VISCERALFATLEVEL,BASALMETABOLISM,BODYAGE,BONEQUANTITY");
                        healthDataText.AppendLine(") VALUES (");
                        healthDataText.AppendLine("@DATETIME,@WEIGHT,@BODYFATPERF,@MUSCLEMASS,@MUSCLESCORE,@VISCERALFATLEVEL2,@VISCERALFATLEVEL,@BASALMETABOLISM,@BODYAGE,@BONEQUANTITY");
                        healthDataText.AppendLine(")");

                        await dbConn.ExecuteAsync(healthDataText.ToString(), healthData, tran);

                        tran.Commit();
                    }
                    catch
                    {
                        tran.Rollback();
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 計測時間の一日前頃の身体データ取得
        /// </summary>
        /// <param name="dateTime"></param>
        /// <param name="now"></param>
        /// <returns></returns>
        public async Task<HealthData> GetPreviousDataAsync(string dateTime)
        {
            if (!DateTime.TryParseExact(dateTime, "yyyyMMddHHmm", new CultureInfo("ja-JP"), DateTimeStyles.AssumeLocal, out DateTime thisTime))
            {
                thisTime = Setting.LocalTime;
            }

            var searchStartDateHour = thisTime.AddDays(-1).AddHours(-6).ToString("yyyyMMddHHmm");
            var searchEndDateHour = thisTime.AddDays(-1).AddHours(6).ToString("yyyyMMddHHmm");

            using (var dbConn = new SqliteConnection(Setting.SqlConnectionSb.ToString()))
            {
                var sql = "SELECT * FROM HEALTHDATA WHERE DATETIME BETWEEN @START AND @END";
                try
                {
                    var dbObj = await dbConn.QueryAsync<HealthData>(sql, new { START = searchStartDateHour, END = searchEndDateHour });

                    /* 複数取得できる場合、一番近しいものを取得 */
                    var tmp = long.MaxValue;
                    HealthData result = null;
                    foreach (var item in dbObj)
                    {
                        var diff = long.Parse(dateTime) - long.Parse(item.DateTime);
                        if (tmp > diff)
                        {
                            tmp = diff;
                            result = item;
                        }
                    }
                    return result;
                }
                catch
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// 一週間の計測データ取得
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public async Task<List<HealthData>> GetThisWeekHealthData()
        {
            var searchStartDateHour = Setting.LocalTime.AddDays(-7).AddHours(-6).ToString("yyyyMMddHHmm");
            var searchEndDateHour = Setting.LocalTime.ToString("yyyyMMddHHmm");

            using (var dbConn = new SqliteConnection(Setting.SqlConnectionSb.ToString()))
            {
                var sql = "SELECT * FROM HEALTHDATA WHERE DATETIME BETWEEN @START AND @END";
                try
                {
                    return (await dbConn.QueryAsync<HealthData>(sql, new { START = searchStartDateHour, END = searchEndDateHour })).ToList();
                }
                catch
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// 設定テーブルカラムキー取得
        /// </summary>
        /// <param name="keyType"></param>
        /// <returns></returns>
        public string GetDbKey(SettingDbEnum keyType)
        {
            var key = "";
            switch (keyType)
            {
                case SettingDbEnum.PreviousWeight:
                    key = "PREVIOUSWEIGHT";
                    break;
                case SettingDbEnum.PrevWeekWeight:
                    key = "PREVWEEKWEIGHT";
                    break;
                case SettingDbEnum.PreviousMeasurememtDate:
                    key = "PREVIOUSMEASUREMENTDATE";
                    break;
                case SettingDbEnum.RequestToken:
                    key = "REQUESTTOKEN";
                    break;
                case SettingDbEnum.ExpiresIn:
                    key = "EXPIRESIN";
                    break;
                case SettingDbEnum.RefreshToken:
                    key = "REFRESHTOKEN";
                    break;
                case SettingDbEnum.ErrorFlag:
                    key = "ERRORFLAG";
                    break;
                default:
                    break;
            }

            return key;
        }
    }
}