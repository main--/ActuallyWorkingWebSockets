using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ActuallyWorkingWebSockets
{
	public class WebSocketSession
	{
		private readonly Stream SocketStream;

		/// <summary>
		/// Gets or sets a value indicating whether this <see cref="ActuallyWorkingWebSockets.WebSocketSession"/> is masking *outgoing* data.
		/// </summary>
		/// <value><c>true</c> if masking; otherwise, <c>false</c>.</value>
		public bool Masking { get; set; }

		public WebSocketSession(Stream stream)
		{
			SocketStream = stream;
		}

		public Task SendTextMessage(string message)
		{
			return WebSocketProtocol.SendTextFrame(SocketStream, message, Masking);
		}

		public Task SendStream(Stream stream)
		{
			return WebSocketProtocol.SendStream(SocketStream, stream, Masking);
		}

		public async Task<string> ReceiveTextMessage()
		{
			var read = await WebSocketProtocol.ReadFrameGroup(SocketStream) as string;
			if (read == null)
				throw new InvalidOperationException("wanted to read string but got something else");
			return read;
		}

		public async Task<Stream> ReceiveBinaryMessage()
		{
			var read = await WebSocketProtocol.ReadFrameGroup(SocketStream) as Stream;
			if (read == null)
				throw new InvalidOperationException("wanted to read stream but got something else");
			return read;
		}
	}
}

