﻿using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using GameServer.Core.Network;

namespace GameServer.Handlers
{
    public class ChatPacketHandler : PacketHandler
    {
        private const int MAX_MESSAGE_LENGTH = 150;
        private readonly Regex _sanitizePattern = new(@"[^\u0020-\u007E]", RegexOptions.Compiled);
        private readonly ConcurrentDictionary<string, GameClient> _clients;

        public ChatPacketHandler(ConcurrentDictionary<string, GameClient> clients) : base()
        {
            _clients = clients;
        }

        public async Task<string?> ReadMessage(GameClient sourceClient, PacketReader reader)
        {
            try
            {
                var length = await reader.ReadU16();
                if (length <= 0 || length > MAX_MESSAGE_LENGTH) return null;

                var buffer = new byte[length];
                await sourceClient.GetStream().ReadAsync(buffer, 0, length);
                var message = _sanitizePattern.Replace(Encoding.ASCII.GetString(buffer).Trim(), string.Empty);

                return string.IsNullOrEmpty(message) ? null : message;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Chat] Error reading message: {ex.Message}");
                await SendGameMessage(sourceClient, "An error occurred while processing your message.");
                return null;
            }
        }

        public async Task BroadcastChatMessage(GameClient sender, string message)
        {
            if (sender.Data.IsMuted) { await SendGameMessage(sender, $"You are muted and cannot type in chat!"); return; }

            var writer = new PacketWriter();
            writer.WriteU8(4); // Chat opcode
            var totalLength = 4 + Encoding.ASCII.GetByteCount(sender.Data.Username) + 4 + Encoding.ASCII.GetByteCount(message) + 1;
            writer.WriteU16((ushort)totalLength);
            writer.WriteString(sender.Data.Username);
            writer.WriteString(message);
            writer.WriteU8(sender.Data.Rank);

            var responseData = writer.ToArray();

            var tasks = new List<ValueTask>();
            foreach (var client in _clients.Values)
            {
                if (client.Data.IsAuthenticated)
                {
                    tasks.Add(client.GetStream().WriteAsync(responseData));
                }
            }

            await Task.WhenAll(tasks.Select(t => t.AsTask()));
        }

        public async Task SendGameMessage(GameClient client, string message)
        {
            if (!client.Data.IsAuthenticated) return;

            var writer = new PacketWriter();
            writer.WriteU8(6);  // Game message opcode
            writer.WriteU16((ushort)(4 + Encoding.ASCII.GetByteCount(message)));
            writer.WriteString(message);

            await client.GetStream().WriteAsync(writer.ToArray());
        }

        public async Task BroadcastGameMessage(string message)
        {
            var writer = new PacketWriter();
            writer.WriteU8(6);  // Game message opcode
            writer.WriteU16((ushort)(4 + Encoding.ASCII.GetByteCount(message)));
            writer.WriteString(message);

            var packet = writer.ToArray();
            var tasks = new List<ValueTask>();

            foreach (var client in _clients.Values)
            {
                if (client.Data.IsAuthenticated)
                {
                    tasks.Add(client.GetStream().WriteAsync(packet));
                }
            }

            await Task.WhenAll(tasks.Select(t => t.AsTask()));
        }

        public async Task SendBattleInitiation(GameClient sender)
        {
            var packet = CreatePacket(30, buffer =>
            {
                buffer.WriteBits(4, 1);      // Battle initiation mask
                buffer.WriteBits(8, 1);      // Battle ID for PoC
            });

            await sender.GetStream().WriteAsync(packet);
        }
    }
}