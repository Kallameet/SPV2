using System;

namespace ChatMessage
{
    [Serializable]
    public class Message
    {
        public enum MessageType
        {
            Connect,
            Disconnect,
            ChatMessage,
            UsernameAlreadyTaken
        }

        public MessageType Type { get; set; }

        public String ChatMessage { get; set; }

        public String Username { get; set; }

        public DateTime MessageCreationTime { get; private set; }


        public Message()
        {
            MessageCreationTime = DateTime.Now;
        }
    }
}
