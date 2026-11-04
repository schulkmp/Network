using System;

namespace Network.Rcon.Handler;

public sealed class RconHandler
{
    private readonly string _password;
    private readonly Func<string, string>? _commandExecutor;
    private readonly Action<int, string>? _responseWriter;

    public RconHandler(string password, Func<string, string>? commandExecutor = null, Action<int, string>? responseWriter = null)
    {
        _password = password;
        _commandExecutor = commandExecutor ?? (cmd => $"Executed: {cmd}");
        _responseWriter = responseWriter ?? delegate { };
    }

    public bool Handle(RconMessage message)
    {
        if (message.Type == RconMessage.AUTH)
        {
            bool isAuthorized = string.Equals(message.Body, _password, StringComparison.Ordinal);
            if (!isAuthorized)
            {
                _responseWriter(message.Id, "Authentication failed");
                return false;
            }

            _responseWriter(message.Id, string.Empty);
            return true;
        }

        if (message.Type == RconMessage.EXECCOMMAND)
        {
            string result = _commandExecutor(message.Body);
            _responseWriter(message.Id, result);
            return true;
        }

        return false;
    }
}