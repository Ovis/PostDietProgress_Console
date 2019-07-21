using Microsoft.Extensions.Configuration;
using System;
using System.Data.SQLite;
using TimeZoneConverter;

namespace PostDietProgress
{
    public class Settings
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("App.config.json", optional: true)
            .Build();

        public String TanitaClientID => configuration["Setting:TanitaClientID"];

        public String TanitaClientSecretToken => configuration["Setting:TanitaClientSecretToken"];

        public String TanitaRequestToken { get; set; }

        public String DiscordWebhookUrl => configuration["Setting:DiscordWebhookUrl"];

        public Double OriginalWeight => Double.Parse(configuration["Setting:OriginalWeight"]);

        public Double GoalWeight => Double.Parse(configuration["Setting:GoalWeight"]);

        public bool PostGoogleFit => bool.Parse(configuration["Setting:GoogleFitSettings:Enabled"]);

        public String GoogleFitClientId => configuration["Setting:GoogleFitSettings:ClientId"];

        public String GoogleFitClientSecret => configuration["Setting:GoogleFitSettings:ClientSecret"];

        public SQLiteConnectionStringBuilder SqlConnectionSb => new SQLiteConnectionStringBuilder { DataSource = "DietProgress.db" };

        public DateTime LocalTime { get; set; }

        public Settings()
        {
            LocalTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TZConvert.GetTimeZoneInfo("Tokyo Standard Time"));
        }
    }
}
