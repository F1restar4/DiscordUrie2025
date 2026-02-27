using System;
using System.IO;
using Newtonsoft.Json;
using DSharpPlus.Entities;

namespace DiscordUrie
{
    public class DiscordUrieBootConfig
    {
        [JsonProperty]
        public string Token { get; set; }
        [JsonProperty]
        public DiscordActivity Activity { get; set; }

        [JsonConstructor]
        public DiscordUrieBootConfig(string token, DiscordActivity activity)
        {
            Token = token;
            Activity = activity;
        }

    }

    public static class DiscordUrieBootConfigHelpers
    {
        public static DiscordUrieBootConfig GetConfig()
        {
            if (!File.Exists("BootConfig.json"))
            {
                var config = new DiscordUrieBootConfig("", new DiscordActivity("Trans Rights", DiscordActivityType.Custom));
                File.WriteAllText("BootConfig.json", JsonConvert.SerializeObject(config, Formatting.Indented));
                Console.WriteLine("NO fuckin CONFIG, fill it out");
                Environment.Exit(-1);
                return null;
            }

            var data = JsonConvert.DeserializeObject<DiscordUrieBootConfig>(File.ReadAllText("BootConfig.json"));
            if (data == null || String.IsNullOrEmpty(data.Token))
            {
                Console.WriteLine("BootConfig invalid.");
                Environment.Exit(-1);
                return null;
            }
            return data;
        }

        public async static Task SaveConfig(DiscordUrieBootConfig config)
            => await File.WriteAllTextAsync("BootConfig.json", JsonConvert.SerializeObject(config, Formatting.Indented));
    }
}