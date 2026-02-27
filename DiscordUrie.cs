using System;
using System.Threading.Tasks;
using System.Linq;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Extensions;
using Microsoft.Extensions.DependencyInjection;
using DSharpPlus.Interactivity.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using DSharpPlus.Entities.AutoModeration;
using DSharpPlus.EventArgs;
using System.Runtime.CompilerServices;
using DSharpPlus.Commands.Processors.MessageCommands;
using Microsoft.Extensions.Options;


namespace DiscordUrie
{
    class Urie
    {

        public DiscordUrieBootConfig BootConfig { get; set; }
        public Database Database { get; set; }
        public List<GuildData> ConfigData { get; set; }
        public ServiceProvider UrieProvider { get; set; }

        public Urie()
        {
            ServiceCollection UrieService = new();
            UrieService.AddSingleton(this);
            BootConfig = DiscordUrieBootConfigHelpers.GetConfig();
            UrieService.AddLogging(x => x.AddConsole());
            UrieService.AddDiscordClient(BootConfig.Token, DSharpPlus.DiscordIntents.All);
            UrieService.AddCommandsExtension((IServiceProvider provider, CommandsExtension extension) =>
            {
                extension.AddCommands(typeof(DiscordUrie.Commands));
                SlashCommandProcessor slashCommandProcessor = new();
                extension.AddProcessor(slashCommandProcessor);
                MessageCommandProcessor messageCommandProcessor = new();
                extension.AddProcessor(messageCommandProcessor);
            }, new CommandsConfiguration()
            {
                RegisterDefaultCommandProcessors = false,
                //DebugGuildId = 503219562855006208,
            });
            UrieService.AddInteractivityExtension();
            UrieService.ConfigureEventHandlers
            (
                xr => xr.HandleGuildDownloadCompleted(GuildDownloadComplete)
                .HandleGuildCreated(NewGuild)
                .HandleGuildDeleted(GuildRemoved)
                .HandleMessageReactionAdded(AddReaction)
                .HandleMessageReactionRemoved(RemoveReaction)
        
            );

            UrieProvider = UrieService.BuildServiceProvider();
            Database = new(new SqliteConnection("Data Source=DiscordUrieDB.db;"), UrieProvider);
            var client = UrieProvider.GetRequiredService<DiscordClient>();
            
        }
        
        public async Task AddReaction(DiscordClient client, MessageReactionAddedEventArgs e)
        {
            if (e.User.IsCurrent || e.Guild == null)
                return;

            var guildConfig = ConfigData.Single(xr => xr.Guild.Id == e.Guild.Id);
            if (!guildConfig.ReactionRolesEnabled)
                return;
            if (!guildConfig.ReactionRoles.Any(xr => xr.TargetMessage == e.Message && xr.TargetReaction == e.Emoji))
                return;

            var reactionRole = guildConfig.ReactionRoles.Single(xr => xr.TargetMessage == e.Message && xr.TargetReaction == e.Emoji);
            var member = await e.Guild.GetMemberAsync(e.User.Id);
            await member.GrantRoleAsync(reactionRole.TargetRole, "Reaction Role Grant");
        }

        public async Task RemoveReaction(DiscordClient client, MessageReactionRemovedEventArgs e)
        {
            if (e.User.IsCurrent)
                return;

            var guildConfig = ConfigData.Single(xr => xr.Guild.Id == e.Guild.Id);
            if (!guildConfig.ReactionRolesEnabled)
                return;
            if (!guildConfig.ReactionRoles.Any(xr => xr.TargetMessage == e.Message && xr.TargetReaction == e.Emoji))
                return;

            var reactionRole = guildConfig.ReactionRoles.Single(xr => xr.TargetMessage == e.Message && xr.TargetReaction == e.Emoji);
            var member = await e.Guild.GetMemberAsync(e.User.Id);
            await member.RevokeRoleAsync(reactionRole.TargetRole, "Reaction Role Removal");
        }

        public async Task GuildRemoved(DiscordClient client, GuildDeletedEventArgs e)
        {
            if (!ConfigData.Any(xr => xr.Guild.Id == e.Guild.Id))
                return;
            var targetConfig = ConfigData.Single(xr => xr.Guild.Id == e.Guild.Id);
            await Database.DeleteGuild(e.Guild.Id);
            ConfigData.Remove(targetConfig);
        }

        public async Task CreateNewGuildData(DiscordGuild guild)
        {
            if (ConfigData.Any(xr => xr.Guild.Id == guild.Id))
                return;
            
            var data = await Database.ParseEntity(await Database.CreateGuildDefaultSettings(guild));
            await Database.CreateGuildRow(guild.Id);
            ConfigData.Add(data);
        }

        public async Task NewGuild(DiscordClient client, GuildCreatedEventArgs e)
            => await CreateNewGuildData(e.Guild);

        public async Task CleanupConfigs()
        {
            var client = UrieProvider.GetRequiredService<DiscordClient>();
            var guilds = client.GetGuildsAsync();
            List<GuildData> config = new(ConfigData);
            await foreach (var cur in guilds)
            {
                if (!ConfigData.Any(xr => xr.Guild.Id == cur.Id))
                {
                    await CreateNewGuildData(cur);
                    continue;
                }
                var curConfig = ConfigData.Single(xr => xr.Guild.Id == cur.Id);
                config.Remove(curConfig);
            }
            if (config.Count != 0)
            {
                foreach (var cur in config)
                {
                    ConfigData.Remove(cur);
                }
            }
        }

        public async Task CheckReactionRoles()
        {
            var client = UrieProvider.GetRequiredService<DiscordClient>();
            var guilds = client.GetGuildsAsync();
            await foreach (var cur in guilds)
            {
                var curConfig = ConfigData.Single(xr => xr.Guild.Id == cur.Id);
                var allMembers = cur.GetAllMembersAsync();
                foreach (var cm in curConfig.ReactionRoles)
                {
                    var reactions = cm.TargetMessage.GetReactionsAsync(cm.TargetReaction);
                    await foreach (var user in reactions)
                    {
                        var member = await cur.GetMemberAsync(user.Id);
                        if (member.Roles.Any(xr => xr == cm.TargetRole))
                            break;
                        await member.GrantRoleAsync(cm.TargetRole);
                    }
                    await foreach (var member in allMembers)
                    {
                        if (member.Roles.Any(xr => xr == cm.TargetRole) && !await reactions.AnyAsync(xr => xr.Id == member.Id))
                            await member.RevokeRoleAsync(cm.TargetRole);
                    }
                }
            }
        }

        public async Task GuildDownloadComplete(DiscordClient client, GuildDownloadCompletedEventArgs e)
        {
            var rawData = await Database.LoadGuilds();
            ConfigData = await Database.ParseEntities(rawData);
            await CleanupConfigs();
            await CheckReactionRoles();
        }

        public async Task StartAsync()
        {
            await Database.CreateGuildDBAsync();
            await UrieProvider.GetRequiredService<DiscordClient>().ConnectAsync(BootConfig.Activity, DiscordUserStatus.Online);
            UrieProvider.GetRequiredService<ILogger<Urie>>().LogInformation("Connected");
        }
    }
}