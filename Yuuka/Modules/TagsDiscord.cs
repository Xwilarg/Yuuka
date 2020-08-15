using Discord;
using Discord.Commands;
using System.Threading.Tasks;

namespace Yuuka.Modules
{
    public sealed partial class Tags : ModuleBase
    {
        public async Task AddReactions(IUserMessage msg)
        {
            await msg.AddReactionsAsync(new[] { new Emoji("◀️"), new Emoji("▶️") });
        }
    }
}
