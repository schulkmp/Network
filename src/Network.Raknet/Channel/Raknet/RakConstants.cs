namespace Network.Raknet.Channel.Raknet;

public static class RakConstants
{
    // Default MTU used for fragmentation heuristics (bytes)
    public const int DefaultMtu = 1200;

    // Reserve space for headers; this is conservative and can be tuned
    public const int ConservativeHeaderSize = 64;

    // Maximum payload per fragment we will use by default
    public static int DefaultMaxFragmentPayload => DefaultMtu - ConservativeHeaderSize;
}