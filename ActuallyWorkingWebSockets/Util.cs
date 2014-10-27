using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ActuallyWorkingWebSockets
{
	internal static class Util
	{
		public static Task<Socket> AcceptAsyncNew(this Socket socket)
		{
			return Task.Factory.FromAsync<Socket>(socket.BeginAccept, socket.EndAccept, null);
		}

		public static async Task<byte> ReadByteAsync(this Stream stream)
		{
			var buf = new byte[1];
			if (await stream.ReadAsync(buf, 0, 1) == 0)
				throw new EndOfStreamException();
			return buf[0];
		}

		public static async Task<byte[]> ReadAllBytesAsync(this Stream stream, int length)
		{
			int offset = 0;
			var buffer = new byte[length];
			while (offset < length) {
				int thisTime = await stream.ReadAsync(buffer, offset, buffer.Length - offset);
				offset += thisTime;
				if (thisTime == 0)
					throw new EndOfStreamException();
			}
			return buffer;
		}

		public static byte[] ReverseArray(byte[] data)
		{
			Array.Reverse(data);
			return data;
		}
	}
}

