namespace lotus_blue.Models
{
    public class SocialMediaMessage
    {
        public string Id { get; set; } // Auto-generated ID for this message (primary key).

        public string SocialMediaConversationId { get; set; } // Foreign key to the related conversation.
        public SocialMediaConversation Conversation { get; set; } // Navigation property.

        public string MessageId { get; set; } // Facebook message ID (if required).
        public string SenderId { get; set; } // ID of the sender (user or page).
        public string SenderName { get; set; } // Name of the sender (page or user).
        public string ReceiverId { get; set; } // ID of the recipient (user or page).
        public string ReceiverName { get; set; } // Name of the recipient (page or user).
        public string Text { get; set; } // Message content.
        public bool IsFromUser { get; set; } // True if the message was sent by the user.
        public DateTime Timestamp { get; set; } // Timestamp when the message was sent.

        public bool IsRead { get; set; } = false; // Indicates if the message has been read

    }
}
