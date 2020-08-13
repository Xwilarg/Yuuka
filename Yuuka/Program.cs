using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using DiscordUtils;
using Newtonsoft.Json;
using System;
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

            // Init others variables
            P = this;
            Rand = new Random();
            HttpClient = new HttpClient();
            Db = new Db();
            try
            {
                await Db.InitAsync("Yuuka");
            }
            catch (SocketException)
            {
                if (!File.Exists("rethinkdb.exe"))
                    throw;
                await Utils.Log(new LogMessage(LogSeverity.Warning, "Initialisation", "ReThinkdb not started, starting my own...", null));
                Process.Start("rethinkdb.exe");
                await Db.InitAsync("Yuuka");
            }

            // MAke sure the coma separator is a '.' and not a ','
            CultureInfo culture = (CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();
            culture.NumberFormat.NumberDecimalSeparator = ".";
            Thread.CurrentThread.CurrentCulture = culture;

            await _commands.AddModuleAsync<Communication>(null);
            await _commands.AddModuleAsync<Tags>(null);

            Client.MessageReceived += HandleCommandAsync;

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
                            await Tags.Show(context, msg.Content.Substring(pos).ToLower()); // We do that here because we can't create an empty command
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
