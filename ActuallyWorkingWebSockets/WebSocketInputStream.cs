using System;
using System.IO;
using System.Threading.Tasks;

namespace ActuallyWorkingWebSockets
{
	public class WebSocketInputStream : Stream
	{
		private readonly Stream Underlying;
		private Synchronized<Stream>.LockHolder UnderlyingHolder;
		private WebSocketProtocol.FrameHeader FrameHeader;
		private Stream FrameStream;
		private int FrameOffset;

		public WebSocketInputStream(WebSocketProtocol.FrameHeader initialHeader,
			Synchronized<Stream>.LockHolder underlyingHolder)
		{
			if (!underlyingHolder.LockedObject.CanRead)
				throw new ArgumentException("Need readable Stream", "underlying");

			Underlying = underlyingHolder.LockedObject;
			UnderlyingHolder = underlyingHolder;
			BeginReadingFrame(initialHeader);
		}

		protected override void Dispose(bool disposing)
		{
			if (UnderlyingHolder != null) {
				UnderlyingHolder.Dispose();
				UnderlyingHolder = null;
			}

			base.Dispose(disposing);
		}

		private void BeginReadingFrame(WebSocketProtocol.FrameHeader header)
		{
			System.Diagnostics.Debug.WriteLine(header.PayloadLength, "WSIS: got next frame");
			FrameHeader = header;
			FrameOffset = 0;

			if (FrameHeader.IsMasked)
				FrameStream = new MaskingStream(Underlying, FrameHeader.MaskData);
			else
				FrameStream = Underlying;
		}

		private async Task<bool> CheckEndOfFrame()
		{
			System.Diagnostics.Debug.WriteLineIf(FrameOffset >= FrameHeader.PayloadLength, FrameHeader.GroupIsComplete, "WSIS: end of frame, is group complete?");
			if (FrameOffset >= FrameHeader.PayloadLength)
				// end of frame
				if (FrameHeader.GroupIsComplete)
					return true;
				else
					// read another header and continue with the next group:
					BeginReadingFrame(await WebSocketProtocol.ReadFrameHeader(Underlying));
			return false;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (CheckEndOfFrame().Result)
				return 0;

			count = Math.Min(FrameHeader.PayloadLength - FrameOffset, count);

			int ret = FrameStream.Read(buffer, offset, count);
			FrameOffset += ret;
			return ret;
		}

		public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
		{
			if (await CheckEndOfFrame())
				return 0;

			count = Math.Min(FrameHeader.PayloadLength - FrameOffset, count);
			System.Diagnostics.Debug.Assert(count > 0, "WSIS: illegal count", String.Format("offset={2} count={0} buffer.Length={1}", count, buffer.Length, offset));
			int ret = await FrameStream.ReadAsync(buffer, offset, count, cancellationToken);
			FrameOffset += ret;
			return ret;
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
		public override long Length { get { throw new NotSupportedException(); } }
		public override long Position {
			get { throw new NotSupportedException(); }
			set { throw new NotSupportedException(); }
		}
	}
}

