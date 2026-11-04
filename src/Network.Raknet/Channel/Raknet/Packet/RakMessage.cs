namespace Network.Raknet.Channel.Raknet.Packet;

public sealed class RakMessage
{
    public RakMessage(object content)
    {
        Content = content;
    }

    public object Content { get; }

    public override string ToString() => $"RakMessage(Content={Content})";
}