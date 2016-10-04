using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ActuallyWorkingWebSockets
{
	internal static class Util
	{
		public static Task<Socket> AcceptAsyncNew(this Socket socket)
		{
			return Task.Factory.FromAsync<Socket>(socket.BeginAccept, socket.EndAccept, null);
		}

		public static async Task<byte> ReadByteAsync(this Stream stream, CancellationToken token)
		{
			var buf = new byte[1];
			if (await stream.ReadAsync(buf, 0, 1, token) == 0)
				throw new EndOfStreamException();
			return buf[0];
		}

		public static async Task<byte[]> ReadAllBytesAsync(this Stream stream, int length, CancellationToken token)
		{
			int offset = 0;
			var buffer = new byte[length];
			while (offset < length) {
				int thisTime = await stream.ReadAsync(buffer, offset, buffer.Length - offset, token);
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

		public static ControlFrame.Type ToControlFrameType(this FrameOpcode opcode)
		{
			switch (opcode) {
			case FrameOpcode.Close:
				return ControlFrame.Type.Close;
			case FrameOpcode.Ping:
				return ControlFrame.Type.Ping;
			case FrameOpcode.Pong:
				return ControlFrame.Type.Pong;
			default:
				throw new InvalidOperationException("that opcode is not for a control frame!");
			}
		}

		public static FrameOpcode ToControlFrameOpcode(this ControlFrame.Type opcode)
		{
			switch (opcode) {
			case ControlFrame.Type.Close:
				return FrameOpcode.Close;
			case ControlFrame.Type.Ping:
				return FrameOpcode.Ping;
			case ControlFrame.Type.Pong:
				return FrameOpcode.Pong;
			default:
				throw new InvalidOperationException("that opcode is not for a control frame!");
			}
		}
	}
}

