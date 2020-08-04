using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordUtils;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Yuuka.Database;
using Yuuka.Modules;

namespace Yuuka
{
    public sealed class Program
    {
        public static async Task Main()
            => await new Program().MainAsync();

        public DiscordSocketClient Client { private set; get; }
        private readonly CommandService _commands = new CommandService();
        public static Program P;
        public Random Rand { private set; get; }

        public DateTime StartTime { private set; get; }

        public HttpClient HttpClient { private set; get; }

        public Db Db { private set; get; }

        public ulong[] Whitelist { private set; get; }

        private Program()
        {
            Client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Verbose,
            });
            Client.Log += Utils.Log;
            _commands.Log += Utils.LogError;
        }

        private async Task MainAsync()
        {
            dynamic json = JsonConvert.DeserializeObject(File.ReadAllText("Keys/Credentials.json"));
            if (json.botToken == null)
                throw new NullReferenceException("Your Credentials.json is missing mandatory information, it must at least contains botToken and ownerId");

            if (!File.Exists("Keys/Whitelist.txt"))
                throw new FileNotFoundException("Missing Whitelist file");
            Whitelist = File.ReadAllLines("Keys/Whitelist.txt").Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => ulong.Parse(x.Split(new[] { "//" }, StringSplitOptions.None)[0].Trim())).ToArray();

            P = this;
            Rand = new Random();
            HttpClient = new HttpClient();
            Db = new Db();
            await Db.InitAsync("Yuuka");

            CultureInfo culture = (CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();
            culture.NumberFormat.NumberDecimalSeparator = ".";
            Thread.CurrentThread.CurrentCulture = culture;

            await _commands.AddModuleAsync<Communication>(null);
            await _commands.AddModuleAsync<Tags>(null);

            Client.MessageReceived += HandleCommandAsync;

            StartTime = DateTime.Now;
            await Client.LoginAsync(TokenType.Bot, (string)json.botToken);
            await Client.StartAsync();

            await Task.Delay(-1);
        }

        private async Task HandleCommandAsync(SocketMessage arg)
        {
            SocketUserMessage msg = arg as SocketUserMessage;
            if (msg == null || arg.Author.IsBot) return;
            int pos = 0;
            if (msg.HasMentionPrefix(Client.CurrentUser, ref pos) || msg.HasStringPrefix(".", ref pos))
            {
                SocketCommandContext context = new SocketCommandContext(Client, msg);
                var result = await _commands.ExecuteAsync(context, pos, null);
                if (!result.IsSuccess && msg.Content.Split(' ').Length == 1) // Command failed & message have only one argument
                    _ = Task.Run(async () => {
                        try
                        {
                            await Tags.Show(context, msg.Content.Substring(pos).ToLower());
                        }
                        catch (Exception e)
                        {
                            await Utils.LogError(new LogMessage(LogSeverity.Error, e.Source, e.Message, e));
                        }
                    });
            }
        }
    }
}
