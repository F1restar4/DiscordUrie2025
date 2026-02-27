using System;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.MessageCommands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordUrie
{
    public partial class Commands
    {


        [Command("test")]
        public static async ValueTask test(SlashCommandContext ctx)
        {
            DiscordEmoji.TryFromName(ctx.Client, ":StaresThroughYourWindow:", out var emote);
            await ctx.RespondAsync(emote);

        }

        [Command("SetReactionRole"), RequireApplicationOwner]
        public static async ValueTask EstablishReactionRole(SlashCommandContext ctx, ulong targetMessage, string targetEmoji, DiscordRole targetRole)
        {
            var urie = ctx.ServiceProvider.GetRequiredService<Urie>();
            //FUCK this hack this SUCKS
            var FUCK = targetEmoji.TrimEnd('>');
            FUCK = FUCK.TrimStart('<');
            var dude = FUCK.Split(':');
            FUCK = dude[0] + dude[1];
            var emote = DiscordEmoji.FromName(ctx.Client, ":" + FUCK + ":");
            var guildConfig = urie.ConfigData.Single(xr => xr.Guild.Id == ctx.Guild.Id);
            if (guildConfig.ReactionRoles.Any(xr => xr.TargetMessage.Id == targetMessage && xr.TargetReaction == emote))
            {
                await ctx.RespondAsync("There's already a reaction role on this message with that emoji", true);
                return;
            }

            var Message = await ctx.Channel.GetMessageAsync(targetMessage);
            await Message.CreateReactionAsync(emote);
            urie.ConfigData.Remove(guildConfig);
            guildConfig.ReactionRoles.Add(new ReactionRoleData(Message, targetRole, ctx.Channel, emote));
            urie.ConfigData.Add(guildConfig);
            await urie.Database.SaveGuild(guildConfig);

        }

        [Command("RemoveReactionRole"), RequireApplicationOwner]
        public static async ValueTask RemoveReactionRole(SlashCommandContext ctx, ulong targetMessage, string targetEmoji)
        {
            var urie = ctx.ServiceProvider.GetRequiredService<Urie>();
            var emote = DiscordEmoji.FromName(ctx.Client, targetEmoji);
            var guildConfig = urie.ConfigData.Single(xr => xr.Guild.Id == ctx.Guild.Id);
            if (!guildConfig.ReactionRoles.Any(xr => xr.TargetMessage.Id == targetMessage && xr.TargetReaction == emote))
            {
                await ctx.RespondAsync("There's no reaction role with those parameters", true);
                return;
            }

            var storedReactionRole = guildConfig.ReactionRoles.Single(xr => xr.TargetChannel.Id == targetMessage && xr.TargetReaction == emote);
            urie.ConfigData.Remove(guildConfig);
            guildConfig.ReactionRoles.Remove(storedReactionRole);
            urie.ConfigData.Add(guildConfig);
            await urie.Database.SaveGuild(guildConfig);
            var Message = await ctx.Channel.GetMessageAsync(targetMessage);
            await Message.DeleteReactionsEmojiAsync(emote);
        }

        [Command("ReactionRolesEnabled"), RequireApplicationOwner]

        public static async ValueTask ReactionRolesEnabled(SlashCommandContext ctx, bool isEnabled)
        {
            var urie = ctx.ServiceProvider.GetRequiredService<Urie>();
            var guildConfig = urie.ConfigData.Single(xr => xr.Guild.Id == ctx.Guild.Id);
            if (guildConfig.ReactionRolesEnabled == isEnabled)
            {
                await ctx.RespondAsync("It is already set to " + isEnabled, true);
                return;
            }
            urie.ConfigData.Remove(guildConfig);
            guildConfig.ReactionRolesEnabled = isEnabled;
            urie.ConfigData.Add(guildConfig);
            await urie.Database.SaveGuild(guildConfig);
            await ctx.RespondAsync("Reaction roles set to " + isEnabled, true);
        }
    }
}