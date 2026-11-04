using System;

namespace Network.Raknet.Handler.Codec.Raknet;

/// <summary>
/// Base class for inbound channel handlers. Mirrors Netty-style lifecycle callbacks
/// (channel registered/active/read/exception) so specific handlers can override only
/// the methods they need.
/// </summary>
/// <typeparam name="T">Type of messages this handler expects when reading.</typeparam>
public abstract class AdvancedChannelInboundHandler<T>
{
    /// <summary>Called when the channel is registered with its EventLoop or runtime.</summary>
    public virtual void ChannelRegistered()
    {
    }

    /// <summary>Called when the channel is unregistered from its EventLoop or runtime.</summary>
    public virtual void ChannelUnregistered()
    {
    }

    /// <summary>Called when the channel becomes active (connected / bound).</summary>
    public virtual void ChannelActive()
    {
    }

    /// <summary>Called when the channel becomes inactive (closed / disconnected).</summary>
    public virtual void ChannelInactive()
    {
    }

    /// <summary>Called when a message of type <typeparamref name="T"/> is read from the channel.</summary>
    public virtual void ChannelRead(T message)
    {
    }

    /// <summary>Called when the current read loop completes (useful to flush accumulated state).</summary>
    public virtual void ChannelReadComplete()
    {
    }

    /// <summary>Called to notify the handler of a user-defined event.</summary>
    public virtual void UserEventTriggered(object? evt)
    {
    }

    /// <summary>Called when an exception is raised during inbound processing.</summary>
    public virtual void ExceptionCaught(Exception exception)
    {
    }
}

/// <summary>
/// Convenience non-generic base class for handlers that accept object messages.
/// </summary>
public abstract class AdvancedChannelInboundHandler : AdvancedChannelInboundHandler<object>
{
}