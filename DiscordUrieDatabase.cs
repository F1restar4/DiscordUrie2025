using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Xml;
using DSharpPlus;
using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace DiscordUrie
{
    class Database(SqliteConnection conn, ServiceProvider provider)
    {
        public SqliteConnection Connection { get; set; } = conn;
        public ServiceProvider UrieProvider { get; set; } = provider;

        public async Task<int> CreateGuildDBAsync()
        {
            await Connection.OpenAsync();
            var command = new SqliteCommand("CREATE TABLE IF NOT EXISTS guilds (Id UNSIGNED INTEGER PRIMARY KEY," +
                                            "ReactionRolesEnabled INTEGER, ReactionRoles TEXT, AutoRoleEnabled INTEGER," +
                                            "AutoRole INTEGER, NotificationsEnabled INTEGER, NotificationChannel INTEGER);", Connection);
            var buh = await command.ExecuteNonQueryAsync();
            await Connection.CloseAsync();
            return buh;
        }

        public async Task<List<RawGuildData>> LoadGuilds()
        {
            await Connection.OpenAsync();
            var command = new SqliteCommand("SELECT * FROM guilds", Connection);
            var reader = await command.ExecuteReaderAsync();
            List<RawGuildData> Guilds = [];
            while (await reader.ReadAsync())
            {
                Guilds.Add(new RawGuildData(Convert.ToUInt64(reader["Id"]))
                {
                    ReactionRolesEnabled = Convert.ToBoolean(reader["ReactionRolesEnabled"]),
                    ReactionRoles = JsonConvert.DeserializeObject<List<RawReactionRoleData>>((string)reader["ReactionRoles"]),
                    AutoRoleEnabled = Convert.ToBoolean(reader["AutoRoleEnabled"]),
                    AutoRole = Convert.ToUInt64(reader["AutoRole"]),
                    NotificationsEnabled = Convert.ToBoolean(reader["NotificationsEnabled"]),
                    NotificationChannel = Convert.ToUInt64(reader["NotificationChannel"])
                });
            }
            await Connection.CloseAsync();
            return Guilds;
        }

        public async Task SaveGuilds(List<GuildData> data)
        {
            foreach (var cur in data)
            {
                await SaveGuild(cur);
            }
        }

        public async Task DeleteGuild(ulong guild)
        {
            await Connection.OpenAsync();
            var command = new SqliteCommand("DELETE FROM guilds WHERE Id = $id;", Connection);
            command.Parameters.AddWithValue("$id", guild);
            await command.ExecuteNonQueryAsync();
            await Connection.CloseAsync();
        }

        public async Task CreateGuildRow(ulong guild)
        {
            await Connection.OpenAsync();
            var command = new SqliteCommand("INSERT INTO guilds VALUES ($Id, false, $reactionRoles, false, 0, false, 0);", Connection);
            command.Parameters.AddWithValue("$Id", guild);
            command.Parameters.AddWithValue("$reactionRoles", JsonConvert.SerializeObject(new List<RawReactionRoleData>()));
            await command.ExecuteNonQueryAsync();
            await Connection.CloseAsync();
        }
        
        public async Task SaveGuild(GuildData data)
        {
            await Connection.OpenAsync();
            JsonSerializerSettings serializerSettings = new()
			{
				Formatting = Newtonsoft.Json.Formatting.None,
				NullValueHandling = NullValueHandling.Ignore
			};
            var command = new SqliteCommand("UPDATE guilds SET ReactionRolesEnabled = $reactionRolesEnabled, ReactionRoles = $reactionRoles, AutoRoleEnabled = $autoRoleEnabled, " +
                                            "AutoRole = $autoRole, NotificationsEnabled = $notificationsEnabled, NotificationChannel = $notificationChannel " +
                                            "WHERE Id = $id;", Connection);
            command.Parameters.AddWithValue("$reactionRolesEnabled", data.ReactionRolesEnabled);
            command.Parameters.AddWithValue("$reactionRoles", JsonConvert.SerializeObject(data.ReactionRoles.Select(xr => new RawReactionRoleData(xr.TargetMessage.Id, xr.TargetRole.Id, xr.TargetChannel.Id, xr.TargetReaction.GetDiscordName())), serializerSettings));
            command.Parameters.AddWithValue("$autoRoleEnabled", data.AutoRoleEnabled);
            command.Parameters.AddWithValue("$autoRole", data.AutoRole == null ? 0 : data.AutoRole.Id);
            command.Parameters.AddWithValue("$notificationsEnabled", data.NotificationsEnabled);
            command.Parameters.AddWithValue("$notificationChannel", data.NotificationChannel == null ? 0 : data.NotificationChannel.Id);
            command.Parameters.AddWithValue("$id", data.Guild.Id);
            await command.ExecuteNonQueryAsync();
            await Connection.CloseAsync();
        }

        public Task<RawGuildData> CreateGuildDefaultSettings(DiscordGuild guild)
            => Task.FromResult(new RawGuildData(guild.Id));

        public async Task<List<RawGuildData>> CreateGuildDefaultSettings(IEnumerable<DiscordGuild> Guilds)
        {
            List<RawGuildData> GuildData = [];
            foreach (DiscordGuild cur in Guilds)
            {
                GuildData.Add(await CreateGuildDefaultSettings(cur));
            }
            return GuildData;
        }

        public async Task<List<ReactionRoleData>> ParseReactionRoles(DiscordGuild guild, List<RawReactionRoleData> rawReactionRoles)
        {
            var client = UrieProvider.GetRequiredService<DiscordClient>();
            List<ReactionRoleData> outputData = [];
            var roles = await guild.GetRolesAsync();
            var channels = await guild.GetChannelsAsync();
            foreach (var cur in rawReactionRoles)
            {
                var channel = channels.Single(xr => xr.Id == cur.TargetChannel);
                outputData.Add(new(await channel.GetMessageAsync(cur.TargetMessage), roles.Single(xr => xr.Id == cur.TargetRole), channel, DiscordEmoji.FromName(client, cur.TargetReaction)));   
            }
            return outputData;
        }

        public async Task<GuildData> ParseEntity(RawGuildData rawData)
        {
            var client = UrieProvider.GetRequiredService<DiscordClient>();
            var guild = await client.GetGuildAsync(rawData.Id);
            return new GuildData(guild, rawData.ReactionRolesEnabled, await ParseReactionRoles(guild, rawData.ReactionRoles), rawData.AutoRoleEnabled, rawData.AutoRole != 0 ? await guild.GetRoleAsync(rawData.AutoRole) : null, rawData.NotificationsEnabled, rawData.NotificationChannel != 0 ? await guild.GetChannelAsync(rawData.NotificationChannel) : null);
        }

        public async Task<List<GuildData>> ParseEntities(List<RawGuildData> rawData)
        {
            var client = UrieProvider.GetRequiredService<DiscordClient>();
            List<GuildData> outputData = [];
            foreach (var cur in rawData)
            {
                outputData.Add(await ParseEntity(cur));
            }
            return outputData;
        }
    }

    class GuildData(DiscordGuild guild, bool reactionRolesEnabled, List<ReactionRoleData> reactionRoles, bool autoRoleEnabled, DiscordRole autoRole, bool notificationsEnabled, DiscordChannel notificationChannel)
    {
        public DiscordGuild Guild { get; set; } = guild;
        public bool ReactionRolesEnabled { get; set; } = reactionRolesEnabled;
        public List<ReactionRoleData> ReactionRoles { get; set; } = reactionRoles;
        public bool AutoRoleEnabled { get; set; } = autoRoleEnabled;
        public DiscordRole AutoRole { get; set; } = autoRole;
        public bool NotificationsEnabled { get; set; } = notificationsEnabled;
        public DiscordChannel NotificationChannel { get; set; } = notificationChannel;
    }

    class ReactionRoleData(DiscordMessage message, DiscordRole role, DiscordChannel channel, DiscordEmoji reaction)
    {
        public DiscordMessage TargetMessage { get; set; } = message;
        public DiscordRole TargetRole { get; set; } = role;
        public DiscordChannel TargetChannel { get; set; } = channel;
        public DiscordEmoji TargetReaction = reaction;
    }

    internal class RawGuildData(ulong ID)
    {
        public ulong Id { get; set; } = ID;
        public bool ReactionRolesEnabled { get; set; } = false;
        public List<RawReactionRoleData> ReactionRoles { get; set; } = [];
        public bool AutoRoleEnabled { get; set; } = false;
        public ulong AutoRole { get; set; } = 0;
        public bool NotificationsEnabled { get; set; } = false;
        public ulong NotificationChannel { get; set; } = 0;
    }

    internal class RawReactionRoleData(ulong message, ulong role, ulong channel, string reaction)
    {
        public ulong TargetMessage { get; set; } = message;
        public ulong TargetRole { get; set; } = role;
        public ulong TargetChannel { get; set; } = channel;
        public string TargetReaction { get; set; } = reaction;

    }
}