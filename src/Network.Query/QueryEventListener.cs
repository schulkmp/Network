using System;

namespace Network.Query;

public interface QueryEventListener
{
    void OnConnected();
    void OnDisconnected();
    void OnPacket(QueryPacket packet);
    void OnException(Exception exception);
}