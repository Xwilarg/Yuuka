using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using DiscordBotsList.Api;
using DiscordUtils;
using Newtonsoft.Json;
using Sentry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Yuuka.Database;
using Yuuka.Modules;

namespace Yuuka
{
    public sealed class Program
    {
        public static async Task Main()
            => await new Program().MainAsync(); // Create a new instance of the bot

        public DiscordSocketClient Client { private set; get; }
        private readonly CommandService _commands = new CommandService();
        public static Program P; // Static reference to current class, to get the variables below

        public Random Rand { private set; get; }
        public DateTime StartTime { private set; get; }
        public HttpClient HttpClient { private set; get; }
        public Db Db { private set; get; }
        public ulong[] Whitelist { private set; get; }
        private Process _process;
        private string _sentryToken;
        private AuthDiscordBotListApi _topGGAPI = null;
        private string _topGGToken = null;
        private DateTime _lastDiscordBotsSent;
        public Dictionary<ulong, PendingDelete> PendingDelete { get; } = new Dictionary<ulong, PendingDelete>(); // Associate message id and PendingDelete

        /// Key: msg id, Value: page, tag
        public Dictionary<ulong, Tuple<int, Database.TagType>> Messages { private set; get; } // Msg that list tags
        public Dictionary<ulong, Tuple<int, Database.TagType>> MeMessages { private set; get; } // Msg that list tag about user

        private Program()
        {
            Client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Verbose,
            });
            Client.Log += Utils.Log;
            _commands.Log += Utils.LogError;
            _commands.Log += SentryError;
        }

        private Task SentryError(LogMessage arg)
        {
            if (_sentryToken == null)
                return Task.CompletedTask;
            if (arg.Exception is CommandException ce)
                SentrySdk.CaptureException(new Exception(ce.Context.Message.ToString(), arg.Exception));
            else
                SentrySdk.CaptureException(arg.Exception);

            return Task.CompletedTask;
        }

        ~Program()
        {
            if (_process != null && !_process.HasExited)
                _process.Kill();
        }

        private async Task MainAsync()
        {
            // Init credentials
            string botToken;
            if (!File.Exists("Keys/Credentials.json"))
            {
                Console.WriteLine("Enter your bot token");
                botToken = Console.ReadLine();
                if (!Directory.Exists("Keys"))
                    Directory.CreateDirectory("Keys");
                File.WriteAllText("Keys/Credentials.json", "{\"botToken\": \"" + botToken + "\"}");
                Console.Clear();
                await Utils.Log(new LogMessage(LogSeverity.Info, "Initialisation", "Your bot token was saved at Keys/Credentials.json", null));
            }
            else
            {
                dynamic json = JsonConvert.DeserializeObject(File.ReadAllText("Keys/Credentials.json"));
                botToken = json.botToken;
                _sentryToken = json.sentryToken;
                SentrySdk.Init(_sentryToken);
                _topGGToken = json.topGGToken;
            }

            // Init whitelist
            if (!File.Exists("Keys/Whitelist.txt"))
            {
                Whitelist = null;
                await Utils.Log(new LogMessage(LogSeverity.Warning, "Initialisation", "You have no whitelist, that means that anyone will be able to create tags!\n" +
                    "If you want to add one, create a file named \"Whitelist\" in the folder \"Keys\" and write the ID of users that can create tags.\n" +
                    "If you don't know how to get user's ID, please refer to https://support.discord.com/hc/en-us/articles/206346498", null));
            }
            else
                Whitelist = File.ReadAllLines("Keys/Whitelist.txt").Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => ulong.Parse(x.Split(new[] { "//" }, StringSplitOptions.None)[0].Trim())).ToArray();

            Messages = new Dictionary<ulong, Tuple<int, Database.TagType>>();
            MeMessages = new Dictionary<ulong, Tuple<int, Database.TagType>>();

            // Init others variables
            P = this;
            Rand = new Random();
            HttpClient = new HttpClient();
            Db = new Db();
            _process = null;
            try
            {
                await Db.InitAsync("Yuuka");
            }
            catch (SocketException)
            {
                if (!File.Exists("rethinkdb.exe"))
                    throw;
                await Utils.Log(new LogMessage(LogSeverity.Warning, "Initialisation", "ReThinkdb not started, starting my own...", null));
                _process = Process.Start("rethinkdb.exe");
                await Db.InitAsync("Yuuka");
            }

            // MAke sure the coma separator is a '.' and not a ','
            CultureInfo culture = (CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();
            culture.NumberFormat.NumberDecimalSeparator = ".";
            Thread.CurrentThread.CurrentCulture = culture;

            await _commands.AddModuleAsync<Communication>(null);
            await _commands.AddModuleAsync<Tags>(null);
            await _commands.AddModuleAsync<Setting>(null);

            Client.MessageReceived += HandleCommandAsync;
            Client.ReactionAdded += ReactionAdded;
            Client.GuildAvailable += GuildJoined;
            Client.JoinedGuild += GuildJoined;
            Client.Connected += Connected;
            Client.JoinedGuild += UpdateDiscordBots;
            Client.LeftGuild += UpdateDiscordBots;

            StartTime = DateTime.Now;
            try
            {
                await Client.LoginAsync(TokenType.Bot, botToken);
            }
            catch (HttpException)
            {
                await Utils.LogError(new LogMessage(LogSeverity.Critical, "Authentification", "An HTTP error occured, this probably means your token is invalid. If it's the case, please delete Keys/Credentials"));
                Console.WriteLine("Press any to continue...");
                Console.ReadKey();
                return;
            }
            await Client.StartAsync();

            await Task.Delay(-1);
        }

        private Task Connected()
        {
            if (_topGGToken == null)
                return Task.CompletedTask;

            _topGGAPI = new AuthDiscordBotListApi(Client.CurrentUser.Id, _topGGToken);

            return Task.CompletedTask;
        }

        private async Task UpdateDiscordBots(SocketGuild _)
        {
            if (_topGGAPI != null && _lastDiscordBotsSent.AddMinutes(10).CompareTo(DateTime.Now) < 0)
            {
                _lastDiscordBotsSent = DateTime.Now;
                await _topGGAPI.UpdateStats(Client.Guilds.Count);
            }
        }

        private async Task ReactionAdded(Cacheable<IUserMessage, ulong> msg, ISocketMessageChannel chan, SocketReaction react)
        {
            _ = Task.Run(async () =>
            {
                string emote = react.Emote.ToString();
                var guildId = (chan as ITextChannel).GuildId;
                if (react.User.Value.Id != Client.CurrentUser.Id && (emote == "◀️" || emote == "▶️") && Messages.ContainsKey(msg.Id))
                {
                    var elem = Messages[msg.Id];
                    var dMsg = await msg.GetOrDownloadAsync();
                    var page = elem.Item1;
                    var author = dMsg.Author as IGuildUser;
                    if (emote == "◀️") page--;
                    else if (emote == "▶️") page++;
                    var count = elem.Item2 == Database.TagType.NONE ? Db.Count(guildId) : Db.Count(guildId, elem.Item2);
                    if (page == 0 || page > (count / 100) + 1)
                        return;
                    if (page == (count / 100) + 1 && count % 100 == 0)
                        return;
                    string type = "";
                    switch (elem.Item2)
                    {
                        case Database.TagType.TEXT: type = "text"; break;
                        case Database.TagType.IMAGE: type = "image"; break;
                        case Database.TagType.AUDIO: type = "audio"; break;
                    }
                    await dMsg.ModifyAsync(x => x.Embed = new EmbedBuilder
                    {
                        Color = Color.Blue,
                        Title = $"List of all the{type} tags",
                        Description = string.Join(", ", elem.Item2 == Database.TagType.NONE ? Db.GetList(guildId, author.Id.ToString(), page, false) : Db.GetListWithType(guildId, author.Id.ToString(), elem.Item2, page, false))
                    }.Build());
                    Messages[msg.Id] = new Tuple<int, Database.TagType>(page, elem.Item2);
                    if (author != null && author.GuildPermissions.ManageMessages)
                        await dMsg.RemoveReactionAsync(react.Emote, react.User.Value);
                }

                // TODO: Refactor to not copy code
                if (react.User.Value.Id != Client.CurrentUser.Id && (emote == "◀️" || emote == "▶️") && MeMessages.ContainsKey(msg.Id))
                {
                    var elem = MeMessages[msg.Id];
                    var dMsg = await msg.GetOrDownloadAsync();
                    var page = elem.Item1;
                    var author = dMsg.Author as IGuildUser;
                    if (emote == "◀️") page--;
                    else if (emote == "▶️") page++;
                    var count = elem.Item2 == Database.TagType.NONE ? Db.Count(guildId) : Db.Count(guildId, elem.Item2);
                    if (page == 0 || page > (count / 100) + 1)
                        return;
                    if (page == (count / 100) + 1 && count % 100 == 0)
                        return;
                    string type = "";
                    switch (elem.Item2)
                    {
                        case Database.TagType.TEXT: type = "text"; break;
                        case Database.TagType.IMAGE: type = "image"; break;
                        case Database.TagType.AUDIO: type = "audio"; break;
                    }
                    await dMsg.ModifyAsync(x => x.Embed = new EmbedBuilder
                    {
                        Color = Color.Blue,
                        Title = $"List of all the{type} tags",
                        Description = string.Join(", ", elem.Item2 == Database.TagType.NONE ? Db.GetList(guildId, author.Id.ToString(), page, true) : Db.GetListWithType(guildId, author.Id.ToString(), elem.Item2, page, true))
                    }.Build());
                    MeMessages[msg.Id] = new Tuple<int, Database.TagType>(page, elem.Item2);
                    if (author != null && author.GuildPermissions.ManageMessages)
                        await dMsg.RemoveReactionAsync(react.Emote, react.User.Value);
                }

                if (react.User.Value.Id != Client.CurrentUser.Id && (emote == "✅" || emote == "❌") && PendingDelete.ContainsKey(msg.Id))
                {
                    var delete = PendingDelete[msg.Id];
                    if (delete.UserId != react.UserId)
                        return;

                    var dMsg = await msg.GetOrDownloadAsync();
                    if (emote == "❌")
                        await dMsg.DeleteAsync();
                    else
                    {
                        await Db.DeleteTagAsync((chan as ITextChannel).Guild, react.User.Value as IGuildUser, delete.Tag);
                        await dMsg.ModifyAsync(x => x.Embed = new EmbedBuilder
                        {
                            Title = "Your tag was deleted",
                            Color = Color.Green
                        }.Build());
                    }

                    PendingDelete.Remove(msg.Id);
                    var author = dMsg.Author as IGuildUser;
                    if (author != null && author.GuildPermissions.ManageMessages)
                        await dMsg.RemoveReactionAsync(react.Emote, react.User.Value);
                }
            });
        }

        private async Task HandleCommandAsync(SocketMessage arg)
        {
            SocketUserMessage msg = arg as SocketUserMessage;
            if (msg == null || arg.Author.IsBot) return;
            int pos = 0;
            ITextChannel textChan = msg.Channel as ITextChannel;
            if (textChan == null) // Can't send message in PM
                return;
            if (msg.HasMentionPrefix(Client.CurrentUser, ref pos) || msg.HasStringPrefix(Db.GetGuild(textChan.GuildId).Prefix, ref pos))
            {
                SocketCommandContext context = new SocketCommandContext(Client, msg);
                var result = await _commands.ExecuteAsync(context, pos, null);
                if (!result.IsSuccess && msg.Content.Split(' ').Length == 1) // Command failed & message have only one argument
                {
                    switch (result.Error)
                    {
                        case CommandError.BadArgCount:
                        case CommandError.ParseFailed:
                        case CommandError.UnmetPrecondition:
                            await msg.Channel.SendMessageAsync(result.ErrorReason);
                            break;

                        default:
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await Tags.Show(context, msg.Content.Substring(pos).ToLower()); // We do that here because we can't create an empty command
                                }
                                catch (Exception e)
                                {
                                    await Utils.LogError(new LogMessage(LogSeverity.Error, e.Source, e.Message, e));
                                    await context.Channel.SendMessageAsync("", false, new EmbedBuilder()
                                    {
                                        Color = Color.Red,
                                        Title = e.GetType().ToString(),
                                        Description = "An error occured while executing last command.\nHere are some details about it: " + e.InnerException.Message
                                    }.Build());
                                }
                            });
                            break;
                    }
                }
            }
        }

        private async Task GuildJoined(SocketGuild guild)
        {
            await Db.InitGuildAsync(guild);
        }
    }
}
