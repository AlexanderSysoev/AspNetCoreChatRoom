using System.Collections.Generic;

namespace AspNetCoreChatRoom
{
    //TODO: replace with real database
    public class ChatHistoryRepository
    {
        private readonly List<Message> _list;

        public ChatHistoryRepository()
        {
            _list = new List<Message>();
        }

        public void Add(Message message)
        {
            _list.Add(message);
        }

        public IReadOnlyCollection<Message> GetHistory()
        {
            return _list;
        }
    }
}