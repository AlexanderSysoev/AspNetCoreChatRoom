using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace AspNetCoreChatRoom
{
    public class ChatWebSocketMiddleware
    {
        private static ConcurrentDictionary<string, WebSocket> _sockets = new ConcurrentDictionary<string, WebSocket>();

        private readonly RequestDelegate _next;
        private readonly ChatHistoryRepository _chatHistoryRepository;

        public ChatWebSocketMiddleware(RequestDelegate next, ChatHistoryRepository chatHistoryRepository)
        {
            _next = next;
            _chatHistoryRepository = chatHistoryRepository;
        }

        public async Task Invoke(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                await _next.Invoke(context);
                return;
            }

            var ct = context.RequestAborted;
            var currentSocket = await context.WebSockets.AcceptWebSocketAsync();
            var socketId = Guid.NewGuid().ToString();

            _sockets.TryAdd(socketId, currentSocket);

            await PublishMessage(new Message(MessageType.UserCountChanged, $"В чате человек {_sockets.Count(s => s.Value.State == WebSocketState.Open)}"), ct);

            foreach (var message in _chatHistoryRepository.GetHistory())
            {
                await SendMessageAsync(currentSocket, message, ct);
            }

            while (true)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                var message = await ReceiveMessageAsync(currentSocket, ct);
                _chatHistoryRepository.Add(message);

                if(message != null && string.IsNullOrEmpty(message.Text))
                {
                    if(currentSocket.State != WebSocketState.Open)
                    {
                        break;
                    }

                    continue;
                }

                await PublishMessage(message, ct);
            }

            WebSocket dummy;
            _sockets.TryRemove(socketId, out dummy);
            await PublishMessage(new Message(MessageType.UserCountChanged, $"В чате человек: {_sockets.Count(s => s.Value.State == WebSocketState.Open)}"), ct);


            await currentSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", ct);
            currentSocket.Dispose();
        }

        private static Task SendMessageAsync(WebSocket socket, Message message, CancellationToken ct = default(CancellationToken))
        {
            var buffer = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));
            var segment = new ArraySegment<byte>(buffer);
            return socket.SendAsync(segment, WebSocketMessageType.Text, true, ct);
        }

        private static async Task PublishMessage(Message message, CancellationToken ct = default(CancellationToken))
        {
            foreach (var socket in _sockets)
            {
                if(socket.Value.State != WebSocketState.Open)
                {
                    continue;
                }

                await SendMessageAsync(socket.Value, message, ct);
            }
        }

        private static async Task<Message> ReceiveMessageAsync(WebSocket socket, CancellationToken ct = default(CancellationToken))
        {
            var buffer = new ArraySegment<byte>(new byte[8192]);
            using (var ms = new MemoryStream())
            {
                WebSocketReceiveResult result;
                do
                {
                    ct.ThrowIfCancellationRequested();

                    result = await socket.ReceiveAsync(buffer, ct);
                    ms.Write(buffer.Array, buffer.Offset, result.Count);
                }
                while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);
                if (result.MessageType != WebSocketMessageType.Text)
                {
                    return null;
                }

                // Encoding UTF8: https://tools.ietf.org/html/rfc6455#section-5.6
                using (var reader = new StreamReader(ms, Encoding.UTF8))
                {
                    return JsonConvert.DeserializeObject<Message>(await reader.ReadToEndAsync());
                }
            }
        }
    }
}
