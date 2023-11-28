namespace Dboy
{
    public class Keys
    {
        public DiscordKeys discord { get; set; }
        public FlowrouteKeys flowroute { get; set; }
    }

    public class DiscordKeys
    {
        public string token { get; set; }
        public string guildId { get; set; }
        public string channelId { get; set; }
        public Dictionary<string, ulong> phoneNumberToUserId { get; set; }
        public string webhookUrl { get; set; }
    }

    public class FlowrouteKeys
    {
        public string secretKey { get; set; }
        public string accessKey { get; set; }
        public string webhookCallbackUrl { get; set; }
        public string mmsMediaUrl { get; set; }
    }
    
}