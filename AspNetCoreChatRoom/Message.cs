namespace AspNetCoreChatRoom
{
    public class Message
    {
        public Message(MessageType type, string text)
        {
            Type = type;
            Text = text;
        }

        public MessageType Type { get; }

        public string Text { get; }
    }

    public enum MessageType
    {
        UserCountChanged = 0,
        Send = 1
    }
}