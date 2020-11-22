using Discord.Commands;
using System.Linq;
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

        [Command("Whitelist"), RequireAdmin]
        public async Task Whitelist()
        {
            var whitelist = Program.P.Db.GetGuild(Context.Guild.Id).AllowedRoles;
            if (whitelist.Length == 0)
                await ReplyAsync("You have no whitelist set");
            else
                await ReplyAsync("Your current whitelist allow the following roles: " + string.Join(", ", whitelist));
        }

        [Command("Whitelist"), RequireAdmin, Priority(-1)]
        public async Task Whitelist(params string[] roles)
        {
            if (roles.Length == 1 && roles[0].ToLower() == "none")
            {
                await Program.P.Db.UpdateWhitelistAsync(Context.Guild.Id, new string[0]);
                await ReplyAsync("Your whitelist was disabled");
            }
            else if (roles.Any(x => !ulong.TryParse(x, out _)))
                await ReplyAsync("You must provide a list of role id");
            else
            {
                await Program.P.Db.UpdateWhitelistAsync(Context.Guild.Id, roles);
                await ReplyAsync("Your whitelist was updated to " + string.Join(", ", roles));
            }
        }
    }
}
