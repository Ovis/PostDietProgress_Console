using Microsoft.Extensions.Configuration;
using System;

namespace PostDietProgress
{
    public class Settings
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("App.config.json", optional: true)
            .Build();

        public String TanitaUserID => configuration["Setting:TanitaUserID"];

        public String TanitaUserPass => configuration["Setting:TanitaUserPass"];

        public String TanitaClientID => configuration["Setting:TanitaClientID"];

        public String TanitaClientSecretToken => configuration["Setting:TanitaClientSecretToken"];

        public String TanitaOAuthToken { get; set; }

        public String TanitaAccessToken { get; set; }
        
        public String DiscordWebhookUrl => configuration["Setting:DiscordWebhookUrl"];

        public Double OriginalWeight => Double.Parse(configuration["Setting:OriginalWeight"]);

        public Double GoalWeight => Double.Parse(configuration["Setting:GoalWeight"]);
    }
}
