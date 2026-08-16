using static lotus_blue.Models.Common;

namespace lotus_blue.Models
{
    public class SocialMediaConversation
    {
        public string Id { get; set; } // Use string as the primary key.

        public int? LuxiraUserId { get; set; }

        public string UserId { get; set; } // User ID from the social media platform.
        public string UserName { get; set; } // User's name (cached).

        public string PageId { get; set; } // Page ID this conversation is linked to.
        public string PageName { get; set; } // Name of the page linked to the conversation.

        public bool Gender { get; set; } // User's gender.
        public bool IsArchived { get; set; } // Optional: Archive old conversations.
        public bool IsOrder { get; set; } // Optional: Hide conversation from main view.
        public bool IsRead { get; set; } // Indicates if the conversation has been read.

        public DateTime CreatedAt { get; set; } // Timestamp when the conversation was created.
        public DateTime UpdatedAt { get; set; } // Timestamp of the last message.

        public SocialMediaType SocialMediaType { get; set; } // Platform the message comes from.

        public List<SocialMediaMessage>? Messages { get; set; } = new(); // Related messages.

        public Countries? Country { get; set; }

        public string? ProductName { get; set; } // New property for the product name.

        // ✅ New property to track if the offer message was sent
        public bool IsOfferSent { get; set; } = false;
    }
    public enum SocialMediaType
    {
        Facebook = 1,
        Twitter = 2,
        Instagram = 3,
        Whatsapp = 4
    }


}
