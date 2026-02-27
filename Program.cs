

namespace DiscordUrie
{
    class Bot
    {
        static async Task Main(string[] args)
        {
            var Urie = new Urie();
            await Urie.StartAsync();
            await Task.Delay(-1);
        }
    }
}