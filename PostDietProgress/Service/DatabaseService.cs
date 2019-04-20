﻿using Dapper;
using PostDietProgress.Model;
using System;
using System.Data.SQLite;
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

        public async Task CreateTable()
        {
            /* データ保管用テーブル作成 */
            using (var dbConn = new SQLiteConnection(Setting.SqlConnectionSb.ToString()))
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

        public async Task<string> GetSettingDbVal(SettingDbEnum keyType)
        {
            var key = GetDbKey(keyType);

            using (var dbConn = new SQLiteConnection(Setting.SqlConnectionSb.ToString()))
            {
                var dbObj = (await dbConn.QueryAsync<SettingDB>("SELECT KEY, VALUE FROM SETTING WHERE KEY = '" + key + "'")).FirstOrDefault();

                return dbObj == null ? null : (string.IsNullOrEmpty(dbObj.Value) ? null : dbObj.Value);
            }
        }

        public async Task SetSettingDbVal(SettingDbEnum keyType, string val)
        {
            var key = GetDbKey(keyType);

            using (var dbConn = new SQLiteConnection(Setting.SqlConnectionSb.ToString()))
            {
                await dbConn.OpenAsync();
                using (var tran = dbConn.BeginTransaction())
                {
                    try
                    {
                        var strBuilder = new StringBuilder();

                        strBuilder.AppendLine("UPDATE SETTING SET VALUE = @VAL WHERE KEY = @KEY");
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

        public async Task SetHealthData(string latestDate, HealthData healthData)
        {
            using (var dbConn = new SQLiteConnection(Setting.SqlConnectionSb.ToString()))
            {
                await dbConn.OpenAsync();
                using (var tran = dbConn.BeginTransaction())
                {
                    try
                    {
                        var strBuilder = new StringBuilder();

                        strBuilder.AppendLine("UPDATE SETTING SET VALUE = @VAL WHERE KEY = @KEY");
                        await dbConn.ExecuteAsync(strBuilder.ToString(), new { Key = "PREVIOUSMEASUREMENTDATE", Val = latestDate }, tran);
                        await dbConn.ExecuteAsync(strBuilder.ToString(), new { Key = "PREVIOUSWEIGHT", Val = healthData.Weight }, tran);


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

        public async Task<HealthData> GetPreviousDataAsync(string dateTime, DateTime now)
        {
            if (!DateTime.TryParseExact(dateTime, "yyyyMMddHHmm", null, DateTimeStyles.AssumeLocal, out DateTime thisTime))
            {
                thisTime = now;
            }

            var searchStartDateHour = thisTime.AddDays(-1).AddHours(-3).ToString("yyyyMMddHHmm");
            var searchEndDateHour = thisTime.AddDays(-1).AddHours(3).ToString("yyyyMMddHHmm");

            using (var dbConn = new SQLiteConnection(Setting.SqlConnectionSb.ToString()))
            {
                var sql = "SELECT * FROM HEALTHDATA WHERE DATETIME BETWEEN @START AND @END";
                try
                {
                    var dbObj = await dbConn.QueryAsync<HealthData>(sql, new { Start = searchStartDateHour, End = searchEndDateHour });
                    return dbObj.FirstOrDefault();
                }
                catch
                {
                    throw;
                }

            }
        }

        public string GetDbKey(SettingDbEnum keyType)
        {
            var key = "";
            switch (keyType)
            {
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