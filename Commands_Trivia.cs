using System;
using System.Linq;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using DSharpPlus.Commands.Processors.SlashCommands;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firestar4.OpenTDBWrapper;
using DSharpPlus.Commands;
using System.ComponentModel;
using DSharpPlus.Interactivity.Extensions;

namespace DiscordUrie
{
    public partial class Commands 
    {
        List<String> Insults = new List<string>
		{
			"dumbass",
			"shit face",
			"brainless",
			"republican",
			"shit ass",
			"stupid",
			"dingus",
			"bozo",
			"dipstick",
			"fart head",
			"bird brain",
			"cracker",
			"bonehead",
			"neanderthal",
			"cave man",
			"ass face"
		};

		List<String> SmartyPantsWords = new List<string>
		{
			"smarty pants",
			"nerd",
			"giga brain",
			"guy who googled the answer",
			"genius",
			"smarty head",
			"hyper chad",
			"smartass",
			"cheater",
			"intellectual",

		};

        [Command("trivia"), Description("Gives a \"fun\" trivia question to answer")]
		public async Task TriviaQuestion(SlashCommandContext ctx)
		{
			var question = await OpenTDBWrapper.GetQuestionAsync();
			List<string> AllAnswers = question.IncorrectAnswers;
			AllAnswers.Add(question.CorrectAnswer);
			AllAnswers = AllAnswers.OrderBy(a => Guid.NewGuid()).ToList();
			DiscordEmbedBuilder builder = new DiscordEmbedBuilder
			{
				Title = $"Trivia Question for {ctx.Member.DisplayName}",
			};
			builder.AddField(question.Question, "You have twelve seconds.");
			builder.AddField("Difficulty", $"`{question.Difficulty}`", true);
			builder.AddField("Category", $"`{question.Category}`", true);
			builder.WithColor(new DiscordColor("00ffff"));
			DiscordInteractionResponseBuilder messageBuilder = new DiscordInteractionResponseBuilder();
			var embed = builder.Build();
			messageBuilder.AddEmbed(embed);
			List<DiscordButtonComponent> Buttons = new List<DiscordButtonComponent>();
			foreach (var cur in AllAnswers)
				Buttons.Add(new(DiscordButtonStyle.Secondary, cur, cur));

            messageBuilder.AddActionRowComponent(Buttons);

            await ctx.RespondAsync(messageBuilder);
            var message = await ctx.GetResponseAsync();
			var Interaction = await message.WaitForButtonAsync(ctx.Member, TimeSpan.FromSeconds(12));
			Buttons.Clear();
			if (Interaction.TimedOut)
			{
				var webhookbuilder = new DiscordWebhookBuilder().AddEmbed(embed);
				foreach (var cur in AllAnswers)
				{
					if (cur == question.CorrectAnswer)
					{
						Buttons.Add(new DiscordButtonComponent(DiscordButtonStyle.Success, $"{cur}", cur, true));
						continue;
					}
					Buttons.Add(new DiscordButtonComponent(DiscordButtonStyle.Secondary, cur, cur, true));
				}
				webhookbuilder.AddActionRowComponent(Buttons);
				webhookbuilder.WithContent($"Looks like you ran out of time, {Insults.OrderBy(x => Guid.NewGuid()).First()}.");
				await ctx.EditResponseAsync(webhookbuilder);
				return;
			}

			var Result = Interaction.Result;
			if (Result.Id == question.CorrectAnswer)
			{
				foreach (var cur in AllAnswers)
				{
					if (cur == question.CorrectAnswer)
					{
						Buttons.Add(new DiscordButtonComponent(DiscordButtonStyle.Success, cur, cur, true));
						continue;
					}
					Buttons.Add(new DiscordButtonComponent(DiscordButtonStyle.Secondary, cur, cur, true));
				}
				await Result.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage,
				new DiscordInteractionResponseBuilder().AddActionRowComponent(Buttons).WithContent($"Good job, {SmartyPantsWords.OrderBy(x => Guid.NewGuid()).First()}, that's correct.").AddEmbed(embed));
				return;
			}

			foreach (var cur in AllAnswers)
			{
				if (cur == question.CorrectAnswer)
				{
					Buttons.Add(new DiscordButtonComponent(DiscordButtonStyle.Success, cur, cur, true));
					continue;
				}
				if (cur == Result.Id)
				{
					Buttons.Add(new DiscordButtonComponent(DiscordButtonStyle.Danger, cur, cur, true));
					continue;
				}
				Buttons.Add(new DiscordButtonComponent(DiscordButtonStyle.Secondary, cur, cur, true));
			}
			await Result.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage,
			new DiscordInteractionResponseBuilder().AddActionRowComponent(Buttons).WithContent($"Thats wrong, {Insults.OrderBy(x => Guid.NewGuid()).First()}. The correct answer was `{question.CorrectAnswer}`").AddEmbed(embed));
		}

    }
}