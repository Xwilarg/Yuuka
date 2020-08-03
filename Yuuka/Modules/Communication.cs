using Discord.Commands;
using DiscordUtils;
using System.Threading.Tasks;

namespace Yuuka.Modules
{
    public sealed class Communication : ModuleBase
    {
        [Command("Info")]
        public async Task Info()
        {
            await ReplyAsync(embed: Utils.GetBotInfo(Program.P.StartTime, "Yuuka", Program.P.Client.CurrentUser));
        }
    }
}
