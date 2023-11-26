using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dboy
{
    public class DiscordHandler
    {
        private readonly DiscordSocketClient _discordClient;
        private readonly string _token;
        private readonly ulong _guildId;
        private readonly ulong _channelId;
        private readonly Dictionary<string, ulong> _phoneNumberToUserId;
        private bool _connected = false;

        public DiscordHandler(string token, ulong guildId, ulong channelId, Dictionary<string, ulong> phoneNumberToUserId)
        {
            _token = token;
            _guildId = guildId;
            _channelId = channelId;
            _phoneNumberToUserId = phoneNumberToUserId;

            _discordClient = new DiscordSocketClient();
            _discordClient.Ready += OnClientReady;
        }

        public async Task LoginAsync()
        {
            await _discordClient.LoginAsync(TokenType.Bot, _token);
            await _discordClient.StartAsync();
        }

        public async Task LogoutAsync()
        {
            await _discordClient.LogoutAsync();
            await _discordClient.StopAsync();
        }

        public async Task SendMessageAsync(string message, string toPhoneNumber)
        {
            await WaitForConnectionAsync();

            var guild = _discordClient.GetGuild(_guildId);
            var channel = guild.GetTextChannel(_channelId);

            if (_phoneNumberToUserId.TryGetValue(toPhoneNumber, out var userId))
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
                }
                else
                {
                    Console.WriteLine($"User with ID {userId} not found in guild.");
                }
            }
            else
            {
                Console.WriteLine($"Phone number {toPhoneNumber} not associated with a Discord user.");
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
    }
}
