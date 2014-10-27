using System;
using System.IO;

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

		void CheckEndOfFrame()
		{
			if (FrameOffset >= FrameHeader.PayloadLength)
				// end of frame
				if (FrameHeader.GroupIsComplete)
					return 0;
				else
					// read another header and continue with the next group:
					BeginReadingFrame(WebSocketProtocol.ReadFrameHeader(Underlying).Result);
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			CheckEndOfFrame();
			var bytesToRead = Math.Min(FrameHeader.PayloadLength - FrameOffset, count);
			FrameOffset += count;
			return FrameStream.Read(buffer, offset, bytesToRead);
		}

		public override System.Threading.Tasks.Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
		{
			CheckEndOfFrame();
			var bytesToRead = Math.Min(FrameHeader.PayloadLength - FrameOffset, count);
			FrameOffset += count;
			return FrameStream.ReadAsync(buffer, offset, bytesToRead, cancellationToken);
		}

		public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
		{
			CheckEndOfFrame();
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

