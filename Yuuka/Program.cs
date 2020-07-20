using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordUtils;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using Yuuka.Modules;

namespace Yuuka
{
    class Program
    {
        public static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public DiscordSocketClient Client { private set; get; }
        private readonly CommandService _commands = new CommandService();
        public static Program P;
        public Random Rand { private set; get; }

        public DateTime StartTime { private set; get; }

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

            P = this;
            Rand = new Random();

            await _commands.AddModuleAsync<Communication>(null);

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
            if (msg.HasMentionPrefix(Client.CurrentUser, ref pos) || msg.HasStringPrefix("y.", ref pos))
            {
                SocketCommandContext context = new SocketCommandContext(Client, msg);
                await _commands.ExecuteAsync(context, pos, null);
            }
        }
    }
}
