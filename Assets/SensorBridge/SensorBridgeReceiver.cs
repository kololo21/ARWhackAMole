using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SensorBridge
{
    // Editor / Windows / macOS / Linux. WebGL needs a JavaScript WebSocket bridge.
    public class SensorBridgeReceiver : MonoBehaviour
    {
        public string serverUrl = "ws://127.0.0.1:8787/ws";
        public string roomKey = "";
        public string Status { get; private set; } = "Disconnected";
        public event Action<SensorPacket> SampleReceived;
        public event Action<string, Gesture, bool> GestureReceived;
        public event Action<string, bool> PresenceChanged;
        public event Action<FeedbackAck> FeedbackAcknowledged;
        private CancellationTokenSource cancellation;
        private int generation;
        private ClientWebSocket feedbackSocket;
        private CancellationToken feedbackToken;
        private readonly object feedbackGate = new object();
        private readonly SemaphoreSlim feedbackSend = new SemaphoreSlim(1, 1);
        private readonly ConcurrentQueue<Inbox> inbox = new ConcurrentQueue<Inbox>();
        private struct Inbox { public int generation; public string text; public bool status; public long receivedTicks; }
        private void Enqueue(int id, string text, bool status = false) => inbox.Enqueue(new Inbox { generation = id, text = text, status = status, receivedTicks = DateTime.UtcNow.Ticks });

        public void Connect()
        {
            Disconnect();
            if (!System.Text.RegularExpressions.Regex.IsMatch(roomKey ?? "", "^[a-zA-Z0-9_-]{16,64}$")) { Status = "Enter the room key from the PC monitor (16-64 characters)."; return; }
            if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri) || (uri.Scheme != "ws" && uri.Scheme != "wss")) { Status = "Invalid WebSocket URL"; return; }
#if UNITY_WEBGL && !UNITY_EDITOR
            Status = "This demo supports Editor and desktop. WebGL requires a JS bridge.";
#else
            cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            int id = generation;
            string key = roomKey;
            Status = "Connecting...";
            _ = Task.Run(() => ReceiveLoop(uri, key, id, token));
#endif
        }

        public void Disconnect()
        {
            generation++;
            lock (feedbackGate) feedbackSocket = null;
            cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null;
            while (inbox.TryDequeue(out _)) { }
            Status = "Disconnected";
            PresenceChanged?.Invoke("red", false); PresenceChanged?.Invoke("blue", false);
        }

        private async Task ReceiveLoop(Uri uri, string key, int id, CancellationToken token)
        {
            int attempt = 0;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using (var ws = new ClientWebSocket())
                    {
                        ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(10);
                        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                        {
                            timeout.CancelAfter(10000);
                            await ws.ConnectAsync(uri, timeout.Token);
                        }
                        var join = Encoding.UTF8.GetBytes("{\"type\":\"join\",\"role\":\"receiver\",\"room\":\"" + key + "\"}");
                        await ws.SendAsync(new ArraySegment<byte>(join), WebSocketMessageType.Text, true, token);
                        lock (feedbackGate) { if (id == generation && !token.IsCancellationRequested) { feedbackSocket = ws; feedbackToken = token; } }
                        var bytes = new byte[8192];
                        while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
                        {
                            using (var message = new MemoryStream())
                            {
                                WebSocketReceiveResult result;
                                do
                                {
                                    result = await ws.ReceiveAsync(new ArraySegment<byte>(bytes), token);
                                    if (result.MessageType == WebSocketMessageType.Close) throw new IOException("Server closed connection: " + result.CloseStatusDescription);
                                    if (result.MessageType != WebSocketMessageType.Text) throw new IOException("Unexpected binary message");
                                    message.Write(bytes, 0, result.Count);
                                    if (message.Length > 65536) throw new IOException("Message too large");
                                } while (!result.EndOfMessage);
                                // Keep a bounded queue; newest sensor states matter more than history.
                                if (inbox.Count > 120) throw new IOException("Receiver is falling behind");
                                Enqueue(id, Encoding.UTF8.GetString(message.ToArray()));
                                attempt = 0;
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (Exception e) { Enqueue(id, "Reconnecting: " + e.Message, true); }
                lock (feedbackGate) { if (id == generation) feedbackSocket = null; }
                try { await Task.Delay(Math.Min(10000, 500 * (1 << Math.Min(attempt++, 4))), token); }
                catch (OperationCanceledException) { break; }
            }
        }

        // Call after an actual game decision, e.g. await receiver.SendFeedback("red", "hit").
        // true means sent to the relay, not that phone hardware actually vibrated.
        public async Task<bool> SendFeedback(string player, string kind = "hit", string message = "", double durationMs = 1500, string id = null)
        {
            if ((player != "red" && player != "blue") || (kind != "calibrate" && kind != "hit" && kind != "ready" && kind != "error" && kind != "test" && kind != "warning" && kind != "countdown" && kind != "contest") || (message ?? "").Length > 140 || double.IsNaN(durationMs) || durationMs < 0 || durationMs > 15000) return false;
            var command = new FeedbackCommand { player=player,kind=kind,message=message??"",durationMs=durationMs,id=id??Guid.NewGuid().ToString("N") };
            var payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(command));
            await feedbackSend.WaitAsync();
            try
            {
                ClientWebSocket ws; CancellationToken token;
                lock (feedbackGate) { ws = feedbackSocket; token = feedbackToken; }
                if (ws == null || ws.State != WebSocketState.Open || token.IsCancellationRequested) return false;
                await ws.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, token);
                return true;
            }
            catch (Exception) { return false; }
            finally { feedbackSend.Release(); }
        }

        private void Update()
        {
            int budget = 120;
            while (budget-- > 0 && inbox.TryDequeue(out var item))
            {
                if (item.generation != generation) continue;
                if (item.status)
                {
                    Status = item.text;
                    PresenceChanged?.Invoke("red", false); PresenceChanged?.Invoke("blue", false);
                    continue;
                }
                try
                {
                    var envelope = JsonUtility.FromJson<Envelope>(item.text);
                    if (envelope.type == "joined") Status = "Connected";
                    else if (envelope.type == "feedbackAck") FeedbackAcknowledged?.Invoke(JsonUtility.FromJson<FeedbackAck>(item.text));
                    else if (envelope.type == "presence" && envelope.players != null)
                        foreach (var player in envelope.players) PresenceChanged?.Invoke(player.player, player.connected);
                    else if (envelope.type == "sample")
                    {
                        double queueAgeMs = (DateTime.UtcNow.Ticks - item.receivedTicks) / (double)TimeSpan.TicksPerMillisecond;
                        if (queueAgeMs > 2000) continue;
                        var p = JsonUtility.FromJson<SensorPacket>(item.text);
                        if (p.version != 1 || p.sensors == null || (p.player != "red" && p.player != "blue")) continue;
                        SampleReceived?.Invoke(p);
                        if (queueAgeMs < 500 && p.gestures != null) foreach (var g in p.gestures)
                            if (p.timestampMs - g.timestampMs >= 0 && p.timestampMs - g.timestampMs < 500) GestureReceived?.Invoke(p.player, g, p.simulated);
                    }
                }
                catch (Exception e) { Debug.LogWarning("Sensor Bridge message error: " + e.Message); }
            }
        }
        private void OnDisable() => Disconnect();
        private void OnApplicationQuit() => Disconnect();
    }
}
