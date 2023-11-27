using Discord;
using Discord.WebSocket;
using System;
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

            _discordClient = new DiscordSocketClient();
            _discordClient.Ready += OnClientReady;
            _discordClient.MessageReceived += OnMessageReceivedAsync;
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

        public async Task LogoutAsync()
        {
            await _discordClient.LogoutAsync();
            await _discordClient.StopAsync();
        }

        private async Task OnMessageReceivedAsync(SocketMessage socketMessage)
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
                else if (messageContent.StartsWith("!!"))
                {
                    await HandleCommandAsync(messageContent, senderUserId);
                }

                if (socketMessage.MentionedUsers.Any(user => user.Id == _discordClient.CurrentUser.Id))
                {
                    Console.WriteLine($"Bot mentioned by {socketMessage.Author.Username}: {socketMessage.Content}");
                }

                if (IsReplyToBot(socketMessage))
                {
                    Console.WriteLine($"Bot replied by {socketMessage.Author.Username}: {socketMessage.Content}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in message handling: {ex.Message}");
            }
        }

        private bool IsReplyToSms(SocketMessage message)
        {
            // Placeholder implementation. Update this with your own logic.
            return false; // Modify this line with your logic
        }

        private string ExtractPhoneNumberFromReply(SocketMessage message)
        {
            // Placeholder implementation. Update this with your own logic.
            return string.Empty;
        }

        private bool IsReplyToBot(SocketMessage message)
        {
            // Placeholder implementation. Update this with your logic to identify if the message is a reply to the bot.
            return false; // Modify this line with your logic
        }

        private async Task HandleCommandAsync(string command, ulong senderUserId)
        {
            try
            {
                var parts = command.Split(' ');
                switch (parts[0].ToLower())
                {
                    case "!!msg":
                        if (parts.Length < 3)
                        {
                            await SendUserMessage(senderUserId, "Usage: !!msg <PhoneNumber> <Message>");
                            return;
                        }
                        await ProcessMsgCommand(parts, senderUserId);
                        break;
                    case "!!help":
                        string helpCommand = parts.Length > 1 ? parts[1] : null;
                        await DisplayHelpMessage(senderUserId, helpCommand);
                        break;
                        // Add other commands here
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing command: {ex.Message}");
            }
        }

        private async Task ProcessMsgCommand(string[] parts, ulong senderUserId)
        {
            string phoneNumber = parts[1];
            string message = string.Join(" ", parts, 2, parts.Length - 2);

            var phoneNumberToSendFrom = FindPhoneNumberByUserId(senderUserId);
            if (phoneNumberToSendFrom != null)
            {
                await _smsHandler.SendSMSMMSAsync(phoneNumberToSendFrom, phoneNumber, message);
                await SendUserMessage(senderUserId, $"Message sent to {phoneNumber}");
            }
            else
            {
                await SendUserMessage(senderUserId, "You are not authorized or do not have an associated phone number.");
            }
        }

        private async Task DisplayHelpMessage(ulong userId, string command = null)
        {
            var user = _discordClient.GetUser(userId);
            if (user == null) return;

            string helpMessage;

            switch (command?.ToLower())
            {
                case "msg":
                    helpMessage = "Use `!!msg <PhoneNumber> <Message>` to send an SMS.\n" +
                                  "- `<PhoneNumber>` should be in the format 1NXXNXXXXXX.\n" +
                                  "- `<Message>` is the text you want to send.";
                    break;
                default:
                    helpMessage = "Available Commands:\n" +
                                  "- `!!help`: Shows this help message.\n" +
                                  "- `!!help <command>`: Shows help about a specific command.\n" +
                                  "- `!!msg <PhoneNumber> <Message>`: Send an SMS to the specified phone number.";
                    break;
            }

            await user.SendMessageAsync(helpMessage);
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

        private string FindPhoneNumberByUserId(ulong userId)
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
