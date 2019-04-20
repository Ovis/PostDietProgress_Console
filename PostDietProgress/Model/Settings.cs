using Microsoft.Extensions.Configuration;
using System;
using System.Data.SQLite;

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

        public SQLiteConnectionStringBuilder SqlConnectionSb => new SQLiteConnectionStringBuilder { DataSource = "DietProgress.db" };
    }
}
