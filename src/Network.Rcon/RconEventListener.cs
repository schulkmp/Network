using System;

namespace Network.Rcon;

public interface RconEventListener
{
    void OnConnected();
    void OnDisconnected();
    void OnMessage(RconMessage message);
    void OnException(Exception exception);
}