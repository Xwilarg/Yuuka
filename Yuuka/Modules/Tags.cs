using Discord;
using Discord.Audio;
using Discord.Commands;
using DiscordUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Yuuka.Modules
{
    public sealed partial class Tags : ModuleBase
    {
        [Command("UserInfo")]
        public async Task Count()
        {
            await ReplyAsync(embed: new EmbedBuilder
            {
                Color = Color.Blue,
                Title = Context.User.ToString(),
                Description = $"You uploaded {Program.P.Db.GetCount(Context.Guild.Id, Context.User.Id.ToString())} tags in this guild\n" +
                    $"In these tags, {Program.P.Db.GetDescriptionCount(Context.Guild.Id, Context.User.Id.ToString())} have a description set\n" +
                    $"All the tags you uploaded takes {(Program.P.Db.GetUploadSize(Context.User.Id.ToString()) / 1000000.0).ToString("0.00")} Mo in memory"
            }.Build());
        }

        [Command("Description")]
        public async Task Description(string tag, [Remainder]string description)
        {
            var ttag = Program.P.Db.GetTag(Context.Guild.Id, tag);
            if (tag == null)
                await ReplyAsync("This tag does not exist.");
            else if (ttag.UserId != Context.User.Id.ToString())
                await ReplyAsync("This tag wasn't created by you");
            else
            {
                await Program.P.Db.SetDescriptionAsync(Context.Guild.Id, ttag, description);
                await ReplyAsync("The description of the tag " + tag + " was updated.");
            }
        }

        [Command("Tag"), Alias("Info")]
        public async Task Tag(string tag)
        {
            var ttag = Program.P.Db.GetTag(Context.Guild.Id, tag);
            if (ttag == null)
                await ReplyAsync("This tag doesn't exist");
            else
            {
                var type = ttag.Type.ToString();
                var list = new List<EmbedFieldBuilder>
                {
                    new EmbedFieldBuilder
                    {
                        Name = "Creation date",
                        Value = ttag.CreationTime.ToString("yyyy/MM/dd HH:mm:ss"),
                        IsInline = true
                    },
                    new EmbedFieldBuilder
                    {
                        Name = "Creator",
                        Value = ttag.User,
                        IsInline = true
                    },
                    new EmbedFieldBuilder
                    {
                        Name = "Type",
                        Value = type[0] + string.Join("", type.Skip(1)).ToLower(),
                        IsInline = true
                    },
                    new EmbedFieldBuilder
                    {
                        Name = "Count",
                        Value = ttag.NbUsage,
                        IsInline = true
                    },
                    new EmbedFieldBuilder
                    {
                        Name = "Description",
                        Value = ttag.Description == "" ? "No description was added" : ttag.Description
                    }
                };
                if (ttag.Type == Database.TagType.TEXT)
                    list.Add(new EmbedFieldBuilder
                    {
                        Name = "Content",
                        Value = (string)ttag.Content
                    });
                var embed = new EmbedBuilder
                {
                    Color = Color.Blue,
                    Title = char.ToUpper(tag[0]) + string.Join("", tag.Skip(1)).ToLower(),
                    Fields = list
                }.Build();
                if (ttag.Type == Database.TagType.IMAGE)
                {
                    using MemoryStream ms = new MemoryStream((byte[])ttag.Content);
                    await Context.Channel.SendFileAsync(ms, "Image" + ttag.Extension, embed: embed);
                }
                else
                    await ReplyAsync(embed: embed);
            }
        }

        [Command("Help")]
        public async Task Help()
        {
            await ReplyAsync(embed: new EmbedBuilder
            {
                Color = Color.Blue,
                Title = "Help",
                Description =
                    "**Help**: Display this help\n" +
                    "**Description descriptionOfTheTag**: Set the description in one of your tag\n" +
                    "**BotInfo**: Display information about the bot\n" +
                    "**Info tagName**: Display information about a tag\n" +
                    "**List**: List all the tags\n" +
                    "**List text/image/audio**: List all the text/image/audio tags\n" +
                    "**UserInfo**: Get information about yourself\n" +
                    "**Random**: Suggest a random tag\n" +
                    "**Random text/image/audio**: Suggestion a random text/image/audio tag\n" +
                    "**Create tagName tagContent**: Create a new tag given a name and a content, to upload image/audio tag, put the file in attachment\n" +
                    "**Delete tagName**: Delete an existing tag"
            }.Build());
        }

        [Command("List")]
        public async Task List()
        {
            await ListInternalAsync(new EmbedBuilder
            {
                Color = Color.Blue,
                Title = "List of all the tags",
                Description = string.Join(", ", Program.P.Db.GetList(Context.Guild.Id, 1))
            }.Build(), Database.TagType.NONE);
        }

        [Command("List text")]
        public async Task ListText()
        {
            await ListInternalAsync(new EmbedBuilder
            {
                Color = Color.Blue,
                Title = "List of all the text tags",
                Description = string.Join(", ", Program.P.Db.GetListWithType(Context.Guild.Id, Database.TagType.TEXT, 1))
            }.Build(), Database.TagType.TEXT);
        }

        [Command("List image")]
        public async Task ListImage()
        {
            await ListInternalAsync(new EmbedBuilder
            {
                Color = Color.Blue,
                Title = "List of all the image tags",
                Description = string.Join(", ", Program.P.Db.GetListWithType(Context.Guild.Id, Database.TagType.IMAGE, 1))
            }.Build(), Database.TagType.IMAGE);
        }

        [Command("List audio")]
        public async Task ListAudio()
        {
            await ListInternalAsync(new EmbedBuilder
            {
                Color = Color.Blue,
                Title = "List of all the audio tags",
                Description = string.Join(", ", Program.P.Db.GetListWithType(Context.Guild.Id, Database.TagType.AUDIO, 1))
            }.Build(), Database.TagType.AUDIO);
        }

        private async Task ListInternalAsync(Embed embed, Database.TagType type)
        {
            var msg = await ReplyAsync(embed: embed);
            if ((type == Database.TagType.NONE ? Program.P.Db.Count(Context.Guild.Id) : Program.P.Db.Count(Context.Guild.Id, type)) > 100)
            {
                Program.P.Messages.Add(msg.Id, new Tuple<int, Database.TagType>(1, type));
                await AddReactions(msg);
            }
        }

        [Command("Random"), Priority(1)]
        public async Task Random()
        {
            var random = Program.P.Db.GetRandom(Context.Guild.Id);
            if (random == null)
            {
                await ReplyAsync("There is no tag available");
                return;
            }
            var type = random.Type.ToString();
            await ReplyAsync(embed: new EmbedBuilder
            {
                Color = Color.Blue,
                Title = type[0] + string.Join("", type.Skip(1)).ToLower() + " tag suggestion",
                Description = $"Why not trying \"{random.Key}\"",
                Footer = new EmbedFooterBuilder
                {
                    Text = random.Description
                }
            }.Build());
        }

        [Command("Random text"), Priority(1)]
        public async Task RandomText()
        {
            var random = Program.P.Db.GetRandomWithType(Context.Guild.Id, Database.TagType.TEXT);
            if (random == null)
            {
                await ReplyAsync("There is no text tag available");
                return;
            }
            await ReplyAsync(embed: new EmbedBuilder
            {
                Color = Color.Blue,
                Title = "Text tag suggestion",
                Description = $"Why not trying \"{random.Key}\"",
                Footer = new EmbedFooterBuilder
                {
                    Text = random.Description
                }
            }.Build());
        }

        [Command("Random image"), Priority(1)]
        public async Task RandomImage()
        {
            var random = Program.P.Db.GetRandomWithType(Context.Guild.Id, Database.TagType.IMAGE);
            if (random == null)
            {
                await ReplyAsync("There is no image tag available");
                return;
            }
            await ReplyAsync(embed: new EmbedBuilder
            {
                Color = Color.Blue,
                Title = "Image tag suggestion",
                Description = $"Why not trying \"{random.Key}\"",
                Footer = new EmbedFooterBuilder
                {
                    Text = random.Description
                }
            }.Build());
        }

        [Command("Random audio"), Priority(1)]
        public async Task RandomAudio()
        {
            var random = Program.P.Db.GetRandomWithType(Context.Guild.Id, Database.TagType.AUDIO);
            if (random == null)
            {
                await ReplyAsync("There is no audio tag available");
                return;
            }
            await ReplyAsync(embed: new EmbedBuilder
            {
                Color = Color.Blue,
                Title = "Audio tag suggestion",
                Description = $"Why not trying \"{random.Key}\"",
                Footer = new EmbedFooterBuilder
                {
                    Text = random.Description
                }
            }.Build());
        }

        [Command("Delete")]
        public async Task Delete(string key)
        {
            var error = Program.P.Db.CanDeleteTag(Context.Guild.Id, Context.User.Id, key);
            if (error != null)
                await ReplyAsync(error);
            else
            {
                var reply = await ReplyAsync(embed: new EmbedBuilder
                {
                    Title = $"Are you sure you want to delete {key}?",
                    Color = Color.Blue
                }.Build());
                Program.P.PendingDelete.Add(reply.Id, new PendingDelete
                {
                    Tag = key,
                    UserId = Context.User.Id
                });
                await reply.AddReactionsAsync(new[] { new Emoji("✅"), new Emoji("❌") });
            }
        }

        [Command("Create")]
        public async Task Create(string key, [Remainder]string content = "")
        {
            if (Program.P.Whitelist != null && !Program.P.Whitelist.Contains(Context.User.Id))
            {
                await ReplyAsync("You need to be whitelisted to create tags. For this, please contact Zirk#0001 on Discord.");
                return;
            }
            if (key.Any(x => !char.IsLetterOrDigit(x) && x != '_'))
            {
                await ReplyAsync("Your tag name can only contains alphanumeric characters and underscores");
                return;
            }
            object tContent;
            Database.TagType type;
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
                    if (Utils.IsImage(extension))
                    {
                        type = Database.TagType.IMAGE;
                        tContent = await Program.P.HttpClient.GetByteArrayAsync(att.Url);
                    }
                    else if (extension == ".mp3" || extension == ".wav" || extension == ".ogg") // Theorically FFMPEG can handle way more than that
                    {
                        type = Database.TagType.AUDIO;
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
                {
                    await ReplyAsync("You must give the content of your tag");
                    return;
                }
                type = Yuuka.Database.TagType.TEXT;
                tContent = content;
            }
            if (await Program.P.Db.AddTagAsync(Context.Guild.Id, type, key, Context.User, tContent, extension, Context.Guild.Id.ToString()))
                await ReplyAsync("Your tag was created.");
            else
                await ReplyAsync("This tag already exist.");
        }

        public static async Task Show(ICommandContext context, string key)
        {
            if (key.Any(x => !char.IsLetterOrDigit(x) && x != '_') || string.IsNullOrEmpty(key)) // To avoid that the bot react at anything
                return;
            var ttag = Program.P.Db.SendTag(context.Guild.Id, key);
            if (ttag == null)
                await context.Channel.SendMessageAsync("There is no tag with this name.");
            else
            {
                if (ttag.Type == Database.TagType.TEXT)
                    await context.Channel.SendMessageAsync((string)ttag.Content);
                else if (ttag.Type == Database.TagType.IMAGE)
                {
                    using MemoryStream ms = new MemoryStream((byte[])ttag.Content);
                    await context.Channel.SendFileAsync(ms, "Image" + ttag.Extension);
                }
                else if (ttag.Type == Database.TagType.AUDIO)
                {
                    IGuildUser guildUser = context.User as IGuildUser;
                    if (guildUser.VoiceChannel == null)
                        await context.Channel.SendMessageAsync("You must be in a vocal channel for vocal tags.");
                    else
                    {
                        IAudioClient audioClient = await guildUser.VoiceChannel.ConnectAsync();
                        string vOutput = "";

                        // Download the file to play
                        string fileName = "audio" + Program.P.Rand.Next(0, 1000000) + ttag.Extension;
                        File.WriteAllBytes(fileName, (byte[])ttag.Content);

                        // Get the current volume of the audio
                        Process process = Process.Start(new ProcessStartInfo
                        {
                            FileName = "ffmpeg",
                            Arguments = $"-i {fileName} -filter:a volumedetect -f null /dev/null:",
                            UseShellExecute = false,
                            RedirectStandardError = true
                        });
                        process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
                        {
                            vOutput += e.Data;
                        };
                        process.BeginErrorReadLine();
                        process.WaitForExit();
                        double volume = double.Parse(Regex.Match(vOutput, "mean_volume: ([-0-9.]+) dB").Groups[1].Value);

                        // Set the new volume so it's neither too loud, neither not enough
                        double objective = -30 - volume;

                        // Add a delay if the audio is too short, if we don't the audio is somehow not played
                        string delay = Regex.Match(vOutput, "Duration: 00:00:00").Success ? ",\"adelay=1000|1000\"" : "";
                        process = Process.Start(new ProcessStartInfo
                        {
                            FileName = "ffmpeg",
                            Arguments = $"-hide_banner -loglevel panic -i {fileName} -af volume={objective}dB{delay} -ac 2 -f s16le -ar 48000 pipe:",
                            UseShellExecute = false,
                            RedirectStandardOutput = true
                        });
                        using Stream output = process.StandardOutput.BaseStream;
                        using AudioOutStream discord = audioClient.CreatePCMStream(AudioApplication.Music);
                        try
                        {
                            await output.CopyToAsync(discord);
                        }
                        catch (OperationCanceledException)
                        { }
                        await discord.FlushAsync();
                        await audioClient.StopAsync();
                        File.Delete(fileName);
                    }
                }
            }
        }
    }
}
