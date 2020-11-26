using Discord;
using Discord.Commands;
using DiscordUtils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Yuuka.Modules
{
    public sealed class Communication : ModuleBase
    {
        [Command("BotInfo")]
        public async Task Info()
        {
            await ReplyAsync(embed: new EmbedBuilder()
            {
                Color = Color.Purple,
                Fields = new List<EmbedFieldBuilder>
                {
                    new EmbedFieldBuilder
                    {
                        Name = "Uptime",
                        Value = Utils.TimeSpanToString(DateTime.Now.Subtract(Program.P.StartTime)),
                        IsInline = true
                    },
                    new EmbedFieldBuilder
                    {
                        Name = "Creator",
                        Value = "Zirk#0001",
                        IsInline = true
                    },
                    new EmbedFieldBuilder
                    {
                        Name = "Account creation",
                        Value = Program.P.Client.CurrentUser.CreatedAt.ToString("HH:mm:ss dd/MM/yy"),
                        IsInline = true
                    },
                    new EmbedFieldBuilder
                    {
                        Name = "Last version",
                        Value = new FileInfo(Assembly.GetEntryAssembly().Location).LastWriteTimeUtc.ToString("HH:mm:ss dd/MM/yy"),
                        IsInline = true
                    },
                    new EmbedFieldBuilder
                    {
                        Name = "Guild Count",
                        Value = Program.P.Client.Guilds.Count,
                        IsInline = true
                    },
                    new EmbedFieldBuilder
                    {
                        Name = "GitHub",
                        Value = "https://github.com/Xwilarg/Yuuka",
                        IsInline = true
                    },
                    new EmbedFieldBuilder
                    {
                        Name = "top.gg Page",
                        Value = "https://top.gg/bot/734788725388869742",
                    }
                }
            }.Build());
        }
    }
}
