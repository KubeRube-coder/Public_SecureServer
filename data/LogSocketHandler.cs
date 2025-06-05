using Serilog.Core;
using Serilog.Events;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace SecureServer.data
{
    public class LogSocketHandler: ILogEventSink
    {
        private readonly IFormatProvider _formatProvider;
        public static ConcurrentQueue<string> LogBuffer = new();
        public static List<WebSocket> ConnectedSockets { get; } = new();
        private const int MaxLines = 100;

        public LogSocketHandler(IFormatProvider formatProvider)
        {
            _formatProvider = formatProvider;
        }

        public void Emit(LogEvent logEvent)
        {
            var log = logEvent.RenderMessage(_formatProvider);

            if (LogBuffer.Count >= MaxLines)
                LogBuffer.TryDequeue(out _);
            LogBuffer.Enqueue(log);

            var buffer = Encoding.UTF8.GetBytes(log);
            var segment = new ArraySegment<byte>(buffer);

            foreach (var socket in ConnectedSockets.ToList())
            {
                if (socket.State == WebSocketState.Open)
                    socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None).Wait();
                else
                    ConnectedSockets.Remove(socket);
            }
        }
    }
}
