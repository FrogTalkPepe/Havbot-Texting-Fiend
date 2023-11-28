using Discord;
using Discord.WebSocket;
using Discord.Commands;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Smguy;
using System.Text;
using System.Linq;

namespace Dboy
{
    public class DiscordHandler
    {
        private readonly DiscordSocketClient _discordClient;
        private readonly CommandService _commands;
        private readonly string _token;
        private readonly ulong _guildId;
        private readonly ulong _channelId;
        private readonly Dictionary<string, ulong> _phoneNumberToUserId;
        private readonly string _webhookUrl;
        private bool _connected = false;
        private readonly SmsHandler _smsHandler;

        public DiscordHandler(string token, ulong guildId, ulong channelId, Dictionary<string, ulong> phoneNumberToUserId, string webhookUrl, SmsHandler smsHandler)
        {
            _token = token;
            _guildId = guildId;
            _channelId = channelId;
            _phoneNumberToUserId = phoneNumberToUserId;
            _webhookUrl = webhookUrl;
            _smsHandler = smsHandler;
            _commands = new CommandService();

            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.DirectMessages | GatewayIntents.GuildMembers
            };

            _discordClient = new DiscordSocketClient(config);
            _discordClient.Log += LogAsync;
            _discordClient.Ready += OnClientReady;
            _discordClient.MessageReceived += OnMessageReceivedAsync;

            _commands.AddModuleAsync(typeof(CommandModule), services: null);
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }

        public async Task InitializeAsync()
        {
            await _commands.AddModulesAsync(assembly: Assembly.GetEntryAssembly(), services: null);
            await LoginAsync();
        }

        public async Task LoginAsync()
        {
            try
            {
                await _discordClient.LoginAsync(TokenType.Bot, _token);
                await _discordClient.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login failed: {ex.Message}");
            }
        }

        private async Task OnMessageReceivedAsync(SocketMessage socketMessage)
        {
            if (!(socketMessage is SocketUserMessage message)) return;

            // Command handling
            int argPos = 0;
            if (message.HasCharPrefix('!', ref argPos))
            {
                Console.WriteLine("Command received: " + message.Content);
                var context = new SocketCommandContext(_discordClient, message);
                await HandleCommandAsync(context, argPos);
            }
            else
            {
                await HandleNonCommandMessageAsync(socketMessage);
            }
        }
        private async Task HandleCommandAsync(SocketCommandContext context, int argPos)
        {
            Console.WriteLine($"Handling command: {context.Message.Content}");
            var result = await _commands.ExecuteAsync(context, argPos, services: null);
            if (!result.IsSuccess)
            {
                Console.WriteLine($"Command failed: {result.ErrorReason}");
            }
            else
            {
                Console.WriteLine("Command processed successfully.");
            }
        }

        private async Task HandleNonCommandMessageAsync(SocketMessage socketMessage)
        {
            try
            {
                if (socketMessage.Author.IsBot || socketMessage.Channel.Id != _channelId)
                    return;

                var messageContent = socketMessage.Content;
                var senderUserId = socketMessage.Author.Id;

                if (IsReplyToSms(socketMessage))
                {
                    string phoneNumber = ExtractPhoneNumberFromReply(socketMessage);
                    string senderPhoneNumber = FindPhoneNumberByUserId(senderUserId);
                    if (!string.IsNullOrEmpty(senderPhoneNumber))
                    {
                        await _smsHandler.SendSMSMMSAsync(senderPhoneNumber, phoneNumber, messageContent);
                        Console.WriteLine($"Sent SMS/MMS to {phoneNumber}: {messageContent}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in message handling: {ex.Message}");
            }
        }

        private string ExtractPhoneNumberFromReply(SocketMessage message)
        {
            var words = message.Content.Split(' ');
            foreach (var word in words)
            {
                if (word.Length == 10 && long.TryParse(word, out _))
                {
                    return word;
                }
            }
            return string.Empty;
        }

        private bool IsReplyToSms(SocketMessage message)
        {
            return message.Reference != null && message.Reference.MessageId.IsSpecified;
        }

        private string? FindPhoneNumberByUserId(ulong userId)
        {
            foreach (var pair in _phoneNumberToUserId)
            {
                if (pair.Value == userId)
                {
                    return pair.Key;
                }
            }
            return null;
        }

        private async Task SendUserMessage(ulong userId, string message)
        {
            try
            {
                var user = _discordClient.GetUser(userId);
                if (user != null)
                {
                    await user.SendMessageAsync(message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending user message: {ex.Message}");
            }
        }

        public async Task SendMessageAsync(string message, string toPhoneNumber)
        {
            await WaitForConnectionAsync();

            try
            {
                var guild = _discordClient.GetGuild(_guildId);
                var channel = guild?.GetTextChannel(_channelId);

                if (channel != null && _phoneNumberToUserId.TryGetValue(toPhoneNumber, out var userId))
                {
                    var user = guild.GetUser(userId);
                    if (user != null)
                    {
                        var embed = new EmbedBuilder()
                            .WithDescription(message)
                            .WithColor(Color.Blue)
                            .WithCurrentTimestamp()
                            .Build();

                        await channel.SendMessageAsync(user.Mention, false, embed);
                        Console.WriteLine($"Sent message to {toPhoneNumber}: {message}");
                    }
                    else
                    {
                        await SendWebhookMessageAsync(message);
                    }
                }
                else
                {
                    Console.WriteLine($"Phone number {toPhoneNumber} not associated with a Discord user.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending Discord message: {ex.Message}");
            }
        }

        private async Task WaitForConnectionAsync()
        {
            while (!_connected)
            {
                await Task.Delay(1000);
            }
        }

        private Task OnClientReady()
        {
            _connected = true;
            return Task.CompletedTask;
        }

        public class CommandModule : ModuleBase<SocketCommandContext>
        {
            private readonly SmsHandler _smsHandler;
            private readonly Dictionary<string, ulong> _phoneNumberToUserId;
            private readonly DiscordHandler _discordHandler; // Reference to DiscordHandler

            // Constructor needs to be updated to accept DiscordHandler as well.
            public CommandModule(SmsHandler smsHandler, Dictionary<string, ulong> phoneNumberToUserId, DiscordHandler discordHandler)
            {
                _smsHandler = smsHandler;
                _phoneNumberToUserId = phoneNumberToUserId;
                _discordHandler = discordHandler;
            }

            [Command("help")]
            [Summary("Shows help information.")]
            public async Task HelpAsync()
            {
                string helpMessage = "Available Commands:\n" +
                                     "- `!help`: Shows this help message.\n" +
                                     "- `!msg <PhoneNumber> <Message>`: Send an SMS to the specified phone number.\n" +
                                     "For `!msg`, `<PhoneNumber>` should be in the format 1NXXNXXXXXX and `<Message>` is the text you want to send.";
                await ReplyAsync(helpMessage);
            }

            [Command("msg")]
            [Summary("Sends an SMS message.")]
            public async Task MsgAsync(string phoneNumber, [Remainder] string message)
            {
                var senderUserId = Context.User.Id;
                string senderPhoneNumber = _phoneNumberToUserId.FirstOrDefault(p => p.Value == senderUserId).Key;
                var response = await _smsHandler.SendSMSMMSAsync(senderPhoneNumber, phoneNumber, message);

                if (response != null)
                {
                    // Send a direct message to the user
                    await _discordHandler.SendUserMessage(senderUserId, $"Message sent successfully to {phoneNumber}.");
                    await ReplyAsync($"Message sent to {phoneNumber}: {message}"); // Reply in the channel as well.
                }
                else
                {
                    await _discordHandler.SendUserMessage(senderUserId, $"Failed to send message to {phoneNumber}.");
                    await ReplyAsync($"Failed to send message to {phoneNumber}."); // Reply in the channel as well.
                }
            }
        }

        private async Task SendWebhookMessageAsync(string message)
        {
            try
            {
                var httpClient = new HttpClient();
                var payload = new { content = message };
                var serializedPayload = JsonConvert.SerializeObject(payload);
                var requestContent = new StringContent(serializedPayload, Encoding.UTF8, "application/json");

                await httpClient.PostAsync(_webhookUrl, requestContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending webhook message: {ex.Message}");
            }
        }
    }
}
