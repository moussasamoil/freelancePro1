using Newtonsoft.Json;

namespace lotus_blue.Models.WebHooksViewModel
{
    public class FacebookWebhookBotViewModel
    {
    }
    // View Models
    public class UserViewModel
    {
        private static readonly Dictionary<string, UserViewModel> TrackedUsers = new();

        public string ConversationId { get; set; }
        public string Name { get; set; }
        public string StoreName { get; set; }  // Store Name or Page Name
    }

    public class MessageViewModel
    {
        public string SenderName { get; set; }
        public string MessageText { get; set; }
    }

    // Models for deserialization
    public class ConversationList
    {
        [JsonProperty("data")]
        public List<Conversation> Data { get; set; }
    }

    public class Conversation
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("participants")]
        public ParticipantList Participants { get; set; }
    }

    public class ParticipantList
    {
        [JsonProperty("data")]
        public List<Participant> Data { get; set; }
    }

    public class Participant
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class MessageList
    {
        public List<Message> Data { get; set; }
    }

    public class Message
    {
        [JsonProperty("message")]  // Maps to the JSON property "message"
        public string MessageText { get; set; }

        [JsonProperty("from")]
        public Sender From { get; set; }
    }

    public class Sender
    {
        public string Name { get; set; }
    }

    public record PageConfig(
        string AccessToken,
        List<string> PublicMessages,
        string PrivateMessage,
        string PageName,
        string PagePhoto,
        bool Gender // true for male, false for female
    );
}
