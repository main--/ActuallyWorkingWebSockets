using System;
using System.IO;
using System.Threading.Tasks;

namespace ActuallyWorkingWebSockets
{
	public class WebSocketInputStream : Stream
	{
		private readonly Stream Underlying;
		private WebSocketProtocol.FrameHeader FrameHeader;
		private Stream FrameStream;
		private int FrameOffset;

		public WebSocketInputStream(WebSocketProtocol.FrameHeader initialHeader, Stream underlying)
		{
			if (!underlying.CanRead)
				throw new ArgumentException("Need readable Stream", "underlying");

			Underlying = underlying;
			BeginReadingFrame(initialHeader);
		}

		private void BeginReadingFrame(WebSocketProtocol.FrameHeader header)
		{
			FrameHeader = header;
			FrameOffset = 0;

			if (FrameHeader.IsMasked)
				FrameStream = new MaskingStream(Underlying, FrameHeader.MaskData);
			else
				FrameStream = Underlying;
		}

		private bool CheckEndOfFrame()
		{
			if (FrameOffset >= FrameHeader.PayloadLength)
				// end of frame
				if (FrameHeader.GroupIsComplete)
					return true;
				else
					// read another header and continue with the next group:
					BeginReadingFrame(WebSocketProtocol.ReadFrameHeader(Underlying).Result);
			return false;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (CheckEndOfFrame())
				return 0;
			var bytesToRead = Math.Min(FrameHeader.PayloadLength - FrameOffset, count);
			FrameOffset += count;
			return FrameStream.Read(buffer, offset, bytesToRead);
		}

		public override System.Threading.Tasks.Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
		{
			if (CheckEndOfFrame())
				return Task.FromResult(0);
			var bytesToRead = Math.Min(FrameHeader.PayloadLength - FrameOffset, count);
			FrameOffset += count;
			return FrameStream.ReadAsync(buffer, offset, bytesToRead, cancellationToken);
		}

		public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
		{
			if (CheckEndOfFrame())
				count = 0; // I don't wanna mess with IAsyncResult, so let's do it the simple but inefficient way
			var bytesToRead = Math.Min(FrameHeader.PayloadLength - FrameOffset, count);
			FrameOffset += count;
			return FrameStream.BeginRead(buffer, offset, bytesToRead, callback, state);
		}

		public override void Flush()
		{
			FrameStream.Flush();
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException();
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException();
		}

		public override bool CanRead { get { return true; } }
		public override bool CanWrite { get { return false; } }

		public override bool CanSeek { get { return false; } }
		public override long Length { get { return FrameStream.Length; } }
		public override long Position {
			get { return FrameStream.Position; }
			set { throw new NotSupportedException(); }
		}
	}
}

