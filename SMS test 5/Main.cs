using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Mainboi; // Adjusted for your namespace
using Mainboi; // Adjusted for your namespace
using Newtonsoft.Json.Linq;
using Dboy;
using Smguy;

namespace Mainboi
{
    class Program
    {
        private static dynamic _keys;
        private static SmsHandler _smsHandler;
        private static DiscordHandler _discordHandler;
        private static Dictionary<string, ulong> _phoneNumberToUserIdMapping;
        private static List<string> _messageHistory = new List<string>();

        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting Application...");

            LoadKeys();

            // Load the phoneNumberToUserId mapping
            _phoneNumberToUserIdMapping = _keys.discord.phoneNumberToUserId.ToObject<Dictionary<string, ulong>>();

            // Initialize DiscordHandler with the mapping
            _discordHandler = new DiscordHandler(
                _keys.discord.token.ToString(),
                ulong.Parse(_keys.discord.guildId.ToString()),
                ulong.Parse(_keys.discord.channelId.ToString()),
                _phoneNumberToUserIdMapping
            );

            await _discordHandler.LoginAsync();

            _smsHandler = new SmsHandler(
                _keys.flowroute.accessKey.ToString(),
                _keys.flowroute.secretKey.ToString(),
                _keys.flowroute.mmsMediaUrl.ToString());

            while (true) // Continuous loop
            {
                await HandleSmsMmsMessages();
                await Task.Delay(100); // Delay to prevent tight loop
            }
        }

        static void LoadKeys()
        {
            try
            {
                string keysJson = File.ReadAllText("keys.json");
                _keys = JsonConvert.DeserializeObject<dynamic>(keysJson);

                if (_keys == null)
                {
                    Console.WriteLine("Error: Unable to deserialize keys from keys.json.");
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading keys: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static async Task HandleSmsMmsMessages()
        {
            var messages = await _smsHandler.GetFlowrouteMessages(1);
            JToken latestMessage = null;

            foreach (var message in messages)
            {
                var messageId = message["id"]?.ToString();
                if (messageId == null || _smsHandler.IsMessageDisplayed(messageId))
                    continue;

                _smsHandler.MarkMessageAsDisplayed(messageId);
                latestMessage = message;
            }

            if (latestMessage != null)
            {
                var formattedMessage = _smsHandler.FormatSmsMessage(latestMessage);
                _messageHistory.Add(formattedMessage);

                // Send only the latest message to Discord
                await _discordHandler.SendMessageAsync(formattedMessage, latestMessage["attributes"]["to"].ToString());
            }
        }

        private static void RedrawConsole()
        {
            Console.Clear();

            // Display messages with the newest at the bottom
            foreach (var message in _messageHistory)
            {
                Console.WriteLine(message);
            }
        }
    }
}
