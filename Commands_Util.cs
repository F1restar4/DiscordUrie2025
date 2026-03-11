using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using System.ComponentModel;

namespace DiscordUrie
{
    public partial class Commands
    {

        public class globals
        {
            public SlashCommandContext ctx;
        }

        [Command("eval"), RequireApplicationOwner, Description("Evaluate code in context")]
        public async ValueTask Eval(SlashCommandContext ctx, string code)
        {
            var builder = new DiscordEmbedBuilder()
            {
                Title = "Evaluating.",
                Color = new DiscordColor(0, 255, 255)
            };
            await ctx.RespondAsync(builder);
            var globals = new globals
            {
                ctx = ctx
            };
            var scriptOptions = ScriptOptions.Default.WithImports("System", "System.Collections.Generic", "System.Diagnostics", "System.Linq",
				"System.Reflection", "System.Text", "System.Threading.Tasks", "DSharpPlus", "DSharpPlus.Commands", "DSharpPlus.Entities", "DSharpPlus.EventArgs", "DSharpPlus.Exceptions")
                .WithReferences(AppDomain.CurrentDomain.GetAssemblies().Where(xa => !xa.IsDynamic && !string.IsNullOrWhiteSpace(xa.Location)));
            object result;
            try
            {
                result = await CSharpScript.EvaluateAsync(code, scriptOptions, globals, typeof(globals));
            }
            catch (CompilationErrorException ex)
            {
                builder = new DiscordEmbedBuilder()
                {
                    Title = "An error occurred",
                    Color = new DiscordColor(255, 0, 0),
                    Description = string.Join('\n', ex.Diagnostics.Take(3))
                };
                await ctx.EditResponseAsync(builder);
                return;
            }

            builder = new DiscordEmbedBuilder()
            {
                Title = "Evaluation successful",
                Color = new DiscordColor(0, 255, 0)
            };
            builder.AddField("Result", result != null ? result.ToString() : "Code didn't return a value");
            if (result != null)
                builder.AddField("Return type", result.GetType().ToString());
            await ctx.EditResponseAsync(builder);
        }
    }
}