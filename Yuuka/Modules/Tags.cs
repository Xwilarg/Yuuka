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
using Yuuka.Database;

namespace Yuuka.Modules
{
    public sealed class Tags : ModuleBase
    {
        [Command("Count")]
        public async Task Count()
        {
            await ReplyAsync(embed: new Discord.EmbedBuilder
            {
                Color = Discord.Color.Blue,
                Title = Context.User.ToString(),
                Description = $"You uploaded {Program.P.Db.GetCount(Context.User.Id.ToString())} tags"
            }.Build());
        }

        [Command("Tag")]
        public async Task Tag(string tag)
        {
            var ttag = Program.P.Db.GetTag(tag);
            if (ttag == null)
                await ReplyAsync("This tag doesn't exist");
            else
            {
                var type = ttag.Type.ToString();
                await ReplyAsync(embed: new Discord.EmbedBuilder
                {
                    Color = Discord.Color.Blue,
                    Title = char.ToUpper(tag[0]) + string.Join("", tag.Skip(1)).ToLower(),
                    Fields = new List<Discord.EmbedFieldBuilder>
                    {
                        new Discord.EmbedFieldBuilder
                        {
                            Name = "Creation date",
                            Value = ttag.CreationTime.ToString("yyyy/MM/dd HH:mm:ss")
                        },
                        new Discord.EmbedFieldBuilder
                        {
                            Name = "Number of use",
                            Value = ttag.NbUsage
                        },
                        new Discord.EmbedFieldBuilder
                        {
                            Name = "Type",
                            Value = type[0] + string.Join("", type.Skip(1)).ToLower()
                        }
                    }
                }.Build());
            }
        }

        [Command("Help")]
        public async Task Help()
        {
            await ReplyAsync(embed: new Discord.EmbedBuilder
            {
                Color = Discord.Color.Blue,
                Title = "Help",
                Description =
                    "Help: Display this help\n" +
                    "Info: Display information about the bot\n" +
                    "List: List all the tags\n" +
                    "List text/image/audio: List all the text/image/audio tags\n" +
                    "Count: See how many tags you uploaded\n" +
                    "Random: Suggest a random tag\n" +
                    "Random text/image/audio: Suggestion a random text/image/audio tag\n" +
                    "Create tagName tagConten: Create a new tag given a name and a content, to upload image/audio tag, put the file in attachment"
            }.Build());
        }

        [Command("List")]
        public async Task List()
        {
            await ReplyAsync(embed: new Discord.EmbedBuilder
            {
                Color = Discord.Color.Blue,
                Title = "List of all the tags",
                Description = string.Join(", ", Program.P.Db.GetList())
            }.Build());
        }

        [Command("List text")]
        public async Task ListText()
        {
            await ReplyAsync(embed: new Discord.EmbedBuilder
            {
                Color = Discord.Color.Blue,
                Title = "List of all the text tags",
                Description = string.Join(", ", Program.P.Db.GetListWithType(TagType.TEXT))
            }.Build());
        }

        [Command("List image")]
        public async Task ListImage()
        {
            await ReplyAsync(embed: new Discord.EmbedBuilder
            {
                Color = Discord.Color.Blue,
                Title = "List of all the image tags",
                Description = string.Join(", ", Program.P.Db.GetListWithType(TagType.IMAGE))
            }.Build());
        }

        [Command("List audio")]
        public async Task ListAudio()
        {
            await ReplyAsync(embed: new Discord.EmbedBuilder
            {
                Color = Discord.Color.Blue,
                Title = "List of all the audio tags",
                Description = string.Join(", ", Program.P.Db.GetListWithType(TagType.AUDIO))
            }.Build());
        }

        [Command("Random"), Priority(1)]
        public async Task Random()
        {
            var random = Program.P.Db.GetRandom();
            var type = random.Type.ToString();
            await ReplyAsync(embed: new Discord.EmbedBuilder
            {
                Color = Discord.Color.Blue,
                Title = type[0] + string.Join("", type.Skip(1)).ToLower() + " tag suggestion",
                Description = $"Why not trying \"{random.Key}\""
            }.Build());
        }

        [Command("Random text"), Priority(1)]
        public async Task RandomText()
        {
            var random = Program.P.Db.GetRandomWithType(TagType.TEXT);
            await ReplyAsync(embed: new Discord.EmbedBuilder
            {
                Color = Discord.Color.Blue,
                Title = "Text tag suggestion",
                Description = $"Why not trying \"{random.Key}\""
            }.Build());
        }

        [Command("Random image"), Priority(1)]
        public async Task RandomImage()
        {
            var random = Program.P.Db.GetRandomWithType(TagType.IMAGE);
            await ReplyAsync(embed: new Discord.EmbedBuilder
            {
                Color = Discord.Color.Blue,
                Title = "Image tag suggestion",
                Description = $"Why not trying \"{random.Key}\""
            }.Build());
        }

        [Command("Random audio"), Priority(1)]
        public async Task RandomAudio()
        {
            var random = Program.P.Db.GetRandomWithType(TagType.AUDIO);
            await ReplyAsync(embed: new Discord.EmbedBuilder
            {
                Color = Discord.Color.Blue,
                Title = "Audio tag suggestion",
                Description = $"Why not trying \"{random.Key}\""
            }.Build());
        }

        [Command("Create")]
        public async Task Create(string key, [Remainder]string content = "")
        {
            if (key.Any(x => !char.IsLetterOrDigit(x) && x != '_'))
            {
                await ReplyAsync("Your tag name can only contains alphanumeric characters and underscores");
                return;
            }
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
                    if (Utils.IsImage(extension))
                    {
                        type = TagType.IMAGE;
                        tContent = await Program.P.HttpClient.GetByteArrayAsync(att.Url);
                    }
                    else if (extension == ".mp3" || extension == ".wav" || extension == ".ogg")
                    {
                        type = TagType.AUDIO;
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
            if (await Program.P.Db.AddTagAsync(type, key, Context.User, tContent, extension, Context.Guild.Id.ToString()))
                await ReplyAsync("Your tag was created.");
            else
                await ReplyAsync("This tag already exist.");
        }

        public static async Task Show(ICommandContext context, string key)
        {
            var ttag = await Program.P.Db.SendTag(key);
            if (ttag == null)
                await context.Channel.SendMessageAsync("There is no tag with this name.");
            else
            {
                if (ttag.Type == TagType.TEXT)
                    await context.Channel.SendMessageAsync((string)ttag.Content);
                else if (ttag.Type == TagType.IMAGE)
                {
                    using MemoryStream ms = new MemoryStream((byte[])ttag.Content);
                    await context.Channel.SendFileAsync(ms, "Image" + ttag.Extension);
                }
                else if (ttag.Type == TagType.AUDIO)
                {
                    Discord.IGuildUser guildUser = context.User as Discord.IGuildUser;
                    if (guildUser.VoiceChannel == null)
                        await context.Channel.SendMessageAsync("You must be in a vocal channel for vocal tags.");
                    else
                    {
                        IAudioClient audioClient = await guildUser.VoiceChannel.ConnectAsync();
                        if (!File.Exists("ffmpeg.exe"))
                            throw new FileNotFoundException("ffmpeg.exe was not found near the bot executable.");
                        string vOutput = "";
                        string fileName = "audio" + Program.P.Rand.Next(0, 1000000) + ttag.Extension;
                        File.WriteAllBytes(fileName, (byte[])ttag.Content);
                        Process process = Process.Start(new ProcessStartInfo
                        {
                            FileName = "ffmpeg.exe",
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
                        var match = Regex.Match(vOutput, "max_volume: ([-0-9.]+) dB");
                        double volume = double.Parse(Regex.Match(vOutput, "mean_volume: ([-0-9.]+) dB").Groups[1].Value);
                        double objective = -30 - volume;
                        process = Process.Start(new ProcessStartInfo
                        {
                            FileName = "ffmpeg.exe",
                            Arguments = $"-hide_banner -loglevel panic -i {fileName} -af volume={objective}dB -ac 2 -f s16le -ar 48000 pipe:",
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
