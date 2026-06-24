using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordUrie
{
    public partial class Commands
    {
        [Command("AutoRoleEnabled"), RequireApplicationOwner]
        public static async ValueTask AutoRoleEnabled(SlashCommandContext ctx, bool isEnabled)
        {
            var urie = ctx.ServiceProvider.GetRequiredService<Urie>();
            var guildConfig = urie.ConfigData.Single(xr => xr.Guild.Id == ctx.Guild.Id);

            if (guildConfig.AutoRoleEnabled == isEnabled)
            {
                await ctx.RespondAsync("It is already set to " + isEnabled, true);
                return;
            }
            urie.ConfigData.Remove(guildConfig);
            guildConfig.AutoRoleEnabled = isEnabled;
            urie.ConfigData.Add(guildConfig);
            await urie.Database.SaveGuild(guildConfig);
            await ctx.RespondAsync("Auto role set to " + isEnabled, true);
        }

        [Command("AutoRoleSet"), RequireApplicationOwner]
        public static async ValueTask SetAutoRole(SlashCommandContext ctx, DiscordRole role)
        {
            var urie = ctx.ServiceProvider.GetRequiredService<Urie>();
            var guildConfig = urie.ConfigData.Single(xr => xr.Guild.Id == ctx.Guild.Id);
            urie.ConfigData.Remove(guildConfig);
            guildConfig.AutoRole = role;
            urie.ConfigData.Add(guildConfig);
            await urie.Database.SaveGuild(guildConfig);
            await ctx.RespondAsync("Auto role set to " + role.Name, true);

        }

    }
}