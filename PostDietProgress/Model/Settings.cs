using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using TimeZoneConverter;

namespace PostDietProgress.Model
{
    public class Settings
    {
        IConfigurationRoot _configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("App.config.json", optional: true)
            .Build();

        public string TanitaClientId => _configuration["Setting:TanitaClientID"];

        public string TanitaClientSecretToken => _configuration["Setting:TanitaClientSecretToken"];

        public string TanitaRequestToken { get; set; }

        public string DiscordWebhookUrl => _configuration["Setting:DiscordWebhookUrl"];

        public double OriginalWeight => Double.Parse(_configuration["Setting:OriginalWeight"]);

        public double GoalWeight => Double.Parse(_configuration["Setting:GoalWeight"]);

        public bool PostGoogleFit => bool.Parse(_configuration["Setting:GoogleFitSettings:Enabled"]);

        public string GoogleFitClientId => _configuration["Setting:GoogleFitSettings:ClientId"];

        public string GoogleFitClientSecret => _configuration["Setting:GoogleFitSettings:ClientSecret"];

        public SqliteConnectionStringBuilder SqlConnectionSb => new SqliteConnectionStringBuilder { DataSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DietProgress.db") };

        public DateTime LocalTime { get; set; }

        public Settings()
        {
            LocalTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TZConvert.GetTimeZoneInfo("Tokyo Standard Time"));
        }
    }
}
