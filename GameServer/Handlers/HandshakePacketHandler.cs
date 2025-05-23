﻿using GameServer.Core.Network;
using System.Net.Sockets;


namespace GameServer.Handlers
{
    public class HandshakePacketHandler
    {
        public async Task HandleHandshake(NetworkStream stream)
        {
            var response = new PacketWriter();
            response.WriteU8(0);
            await stream.WriteAsync(response.ToArray());
        }
    }
}