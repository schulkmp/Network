namespace Network.Raknet.Channel.Raknet;

using System;
using System.Collections.Generic;
using Network.Raknet.Handler.Codec.Raknet;

public sealed class RakChannelPipeline
{
    private readonly List<AdvancedChannelInboundHandler> _inboundHandlers = new();

    public RakChannelPipeline()
    {
    }

    public RakChannelPipeline AddLast(AdvancedChannelInboundHandler handler)
    {
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        _inboundHandlers.Add(handler);
        return this;
    }

    public void FireChannelRegistered()
    {
        foreach (var h in _inboundHandlers) h.ChannelRegistered();
    }

    public void FireChannelActive()
    {
        foreach (var h in _inboundHandlers) h.ChannelActive();
    }

    public void FireChannelRead(object message)
    {
        foreach (var h in _inboundHandlers) h.ChannelRead(message);
        foreach (var h in _inboundHandlers) h.ChannelReadComplete();
    }

    public void FireExceptionCaught(Exception ex)
    {
        foreach (var h in _inboundHandlers) h.ExceptionCaught(ex);
    }
}