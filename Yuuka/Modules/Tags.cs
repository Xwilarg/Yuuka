using Discord.Commands;
using DiscordUtils;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Yuuka.Database;

namespace Yuuka.Modules
{
    public sealed class Tags : ModuleBase
    {
        [Command("Create")]
        public async Task Create(string key, [Remainder]string content = "")
        {
            object tContent;
            TagType type;
            string extension = null;
            if (Context.Message.Attachments.Count > 0)
            {
                var att = Context.Message.Attachments.ElementAt(0);
                if (att.Size > 8000000)
                {
                    await ReplyAsync("Your file can't be more than 8MB.");
                    return;
                }
                else
                {
                    extension = Utils.GetExtension(att.Filename);
                    if (Utils.IsImage(Utils.GetExtension(att.Filename)))
                    {
                        type = TagType.IMAGE;
                        tContent = await Program.P.HttpClient.GetByteArrayAsync(att.Url);
                    }
                    else
                    {
                        await ReplyAsync("Your file must be an image or a music.");
                        return;
                    }
                }
            }
            else
            {
                if (content == "")
                    return;
                type = TagType.TEXT;
                tContent = content;
            }
            if (await Program.P.Db.AddTagAsync(type, key, Context.User, tContent, extension))
                await ReplyAsync("Your tag was created.");
            else
                await ReplyAsync("This tag already exist.");
        }

        public static async Task Show(ICommandContext context, string key)
        {
            var tag = Program.P.Db.GetTag(key);
            if (!tag.HasValue)
                await context.Channel.SendMessageAsync("There is no tag with this name.");
            else
            {
                var ttag = tag.Value;
                if (ttag.Type == Database.TagType.TEXT)
                    await context.Channel.SendMessageAsync((string)ttag.Content);
                else if (ttag.Type == Database.TagType.IMAGE)
                {
                    using MemoryStream ms = new MemoryStream((byte[])ttag.Content);
                    await context.Channel.SendFileAsync(ms, "Image" + ttag.Extension);
                }
            }
        }
    }
}
