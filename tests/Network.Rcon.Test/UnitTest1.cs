using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Network.Rcon;
using Network.Rcon.Handler;

namespace Network.Rcon.Test;

public class UnitTest1
{
    [Fact]
    public void RconMessage_CreateAuth_HasExpectedValues()
    {
        var message = RconMessage.CreateAuth(42, "secret");

        Assert.Equal(42, message.Id);
        Assert.Equal(RconMessage.AUTH, message.Type);
        Assert.Equal("secret", message.Body);
    }

    [Fact]
    public void RconMessage_CreateExecuteCommand_HasExpectedValues()
    {
        var message = RconMessage.CreateExecuteCommand(10, "status");

        Assert.Equal(10, message.Id);
        Assert.Equal(RconMessage.EXECCOMMAND, message.Type);
        Assert.Equal("status", message.Body);
    }

    [Fact]
    public void RconCodec_EncodeAndDecode_RoundTrips()
    {
        var message = RconMessage.CreateExecuteCommand(7, "status");

        byte[] encoded = RconCodec.Encode(message);

        Assert.True(RconCodec.TryDecode(encoded, out var decoded, out int consumed));
        Assert.NotNull(decoded);
        Assert.Equal(encoded.Length, consumed);
        Assert.Equal(message.Id, decoded!.Id);
        Assert.Equal(message.Type, decoded.Type);
        Assert.Equal(message.Body, decoded.Body);
    }

    [Fact]
    public void RconCodec_TryDecode_InvalidPacket_ReturnsFalse()
    {
        byte[] invalid = new byte[] { 1, 2, 3, 4 }; 

        Assert.False(RconCodec.TryDecode(invalid, out _, out _));
    }

    [Fact]
    public void RconHandler_Auth_WithMatchingPassword_Succeeds()
    {
        var responses = new List<(int Id, string Body)>();
        var handler = new RconHandler("secret", _ => "ok", (id, body) => responses.Add((id, body)));

        bool result = handler.Handle(RconMessage.CreateAuth(11, "secret"));

        Assert.True(result);
        Assert.Contains(responses, x => x.Id == 11 && x.Body == string.Empty);
    }

    [Fact]
    public void RconHandler_Auth_WithWrongPassword_Fails()
    {
        var responses = new List<(int Id, string Body)>();
        var handler = new RconHandler("secret", _ => "ok", (id, body) => responses.Add((id, body)));

        bool result = handler.Handle(RconMessage.CreateAuth(12, "wrong-pass"));

        Assert.False(result);
        Assert.Contains(responses, x => x.Id == 12 && x.Body == "Authentication failed");
    }

    [Fact]
    public void RconHandler_ExecuteCommand_InvokesExecutorAndWritesResponse()
    {
        var responses = new List<(int Id, string Body)>();
        var handler = new RconHandler("secret", cmd => $"result:{cmd}", (id, body) => responses.Add((id, body)));

        bool result = handler.Handle(RconMessage.CreateExecuteCommand(21, "list"));

        Assert.True(result);
        Assert.Contains(responses, x => x.Id == 21 && x.Body == "result:list");
    }

    [Fact]
    public async Task RconNetworkListener_StartAndStop_CanExistWithoutThrowing()
    {
        var packets = new List<RconMessage>();

        using var listener = new RconNetworkListener("127.0.0.1", 0, packet => packets.Add(packet));

        listener.Start();
        await Task.Delay(150);
        listener.Stop();

        Assert.NotNull(listener);
    }
}