namespace Network.Raknet.Channel.Raknet;

using System;
using System.Threading.Tasks;

public interface RakChannel : IDisposable
{
	RakChannelPipeline Pipeline { get; }

	ValueTask WriteAsync(ReadOnlyMemory<byte> data);

	ValueTask CloseAsync();
}