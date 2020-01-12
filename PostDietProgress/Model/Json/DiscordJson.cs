using Newtonsoft.Json;
using System;

namespace PostDietProgress.Model.Json
{
    [Serializable]
    sealed class DiscordJson
    {
        [JsonProperty("content")]
        public string Content;
    }
}
