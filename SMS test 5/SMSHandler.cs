using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Smguy
{
    public class SmsHandler
    {
        private readonly string _accessKey;
        private readonly string _secretKey;
        private readonly string _mmsMediaUrl;
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly HashSet<string> _displayedMessageIds = new HashSet<string>();

        public SmsHandler(string accessKey, string secretKey, string mmsMediaUrl)
        {
            _accessKey = accessKey;
            _secretKey = secretKey;
            _mmsMediaUrl = mmsMediaUrl;

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{accessKey}:{secretKey}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        public async Task<JArray> GetFlowrouteMessages(int limit = 50)
        {
            string startDate = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ");
            var url = $"https://api.flowroute.com/v2.2/messages?start_date={startDate}&limit={limit}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var responseObject = JObject.Parse(responseContent);
                    var messages = responseObject["data"]?.ToObject<JArray>() ?? new JArray();
                    return messages;
                }
                else
                {
                    Console.WriteLine($"Error retrieving messages from Flowroute: {response.StatusCode}");
                    return new JArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving messages from Flowroute: {ex.Message}");
                return new JArray();
            }
        }

        public bool IsMessageDisplayed(string messageId)
        {
            return _displayedMessageIds.Contains(messageId);
        }

        public void MarkMessageAsDisplayed(string messageId)
        {
            _displayedMessageIds.Add(messageId);
        }

        public string FormatSmsMessage(JToken message)
        {
            var attributes = message["attributes"];
            var body = attributes?["body"]?.ToString();
            var from = attributes?["from"]?.ToString();
            var to = attributes?["to"]?.ToString();
            var timestamp = attributes?["timestamp"]?.ToString();
            var isMms = (bool)(attributes?["is_mms"] ?? false);
            var formattedTimestamp = ParseTimestamp(timestamp);

            string formattedMessage = $"From: {from}\nTo: {to}\nTimestamp: {formattedTimestamp}\nBody: {body}\n";

            if (isMms && message["relationships"]?["media"]?["data"] != null)
            {
                JArray mediaData = (JArray)message["relationships"]["media"]["data"];
                foreach (var media in mediaData)
                {
                    string mediaId = media["id"].ToString();
                    string mediaUrl = $"{_mmsMediaUrl}{mediaId}";
                    formattedMessage += $"Media URL: {mediaUrl}\n";
                }
            }

            return formattedMessage;
        }

        private string ParseTimestamp(string timestamp)
        {
            if (DateTime.TryParse(timestamp, null, System.Globalization.DateTimeStyles.AssumeUniversal, out var dateTime))
            {
                dateTime = dateTime.ToLocalTime();
                return dateTime.ToString("yyyy-MM-dd hh:mm:ss tt");
            }

            return timestamp;
        }
    }
}
