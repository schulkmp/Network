using System;

namespace Network.Rcon;

public class RconMessage
{
    public const int AUTH = 3;
    public const int AUTH_RESPONSE = 2;
    public const int EXECCOMMAND = 2;
    public const int RESPONSE_VALUE = 0;

    private readonly int _id;
    private readonly int _type;
    private readonly string _body;

    public RconMessage(int id, int type, string body)
    {
        if (body is null)
        {
            throw new ArgumentNullException(nameof(body));
        }

        _id = id;
        _type = type;
        _body = body;
    }

    public int Id => _id;
    public int Type => _type;
    public string Body => _body;

    public static RconMessage CreateAuth(int id, string password) => new(id, AUTH, password);
    public static RconMessage CreateExecuteCommand(int id, string command) => new(id, EXECCOMMAND, command);
    public static RconMessage CreateResponse(int id, string body) => new(id, RESPONSE_VALUE, body);

    public int getId() => _id;
    public int getType() => _type;
    public string getBody() => _body;

    public override string ToString() => $"RconMessage(Id={_id}, Type={_type}, Body={_body})";
}