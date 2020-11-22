using Discord.Commands;
using System.Threading.Tasks;
using Yuuka.Attribute;

namespace Yuuka.Modules
{
    public class Setting : ModuleBase
    {
        [Command("Prefix"), RequireAdmin]
        public async Task Prefix()
        {
            await ReplyAsync("Your current prefix is " + Program.P.Db.GetGuild(Context.Guild.Id).Prefix);
        }

        [Command("Prefix"), RequireAdmin]
        public async Task Prefix(string prefix)
        {
            await Program.P.Db.UpdatePrefixAsync(Context.Guild.Id, prefix);
            await ReplyAsync("Your prefix was updated to " + prefix);
        }
    }
}
