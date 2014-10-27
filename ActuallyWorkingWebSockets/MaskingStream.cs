using System;
using System.IO;
using System.Threading.Tasks;

namespace ActuallyWorkingWebSockets
{
	public class MaskingStream : Stream
	{
		private readonly Stream Underlying;
		private readonly byte[] MaskData;
		private int MaskOffsetRead, MaskOffsetWrite;

		public MaskingStream(Stream underlying, byte[] maskData)
		{
			Underlying = underlying;
			MaskData = maskData;
			MaskOffsetWrite = MaskOffsetRead = 0;
		}

		private void MaskBuffer(byte[] buffer, int offset, int count, ref int MaskOffset)
		{
			for (int i = 0; i < buffer.Length; i++)
				buffer[i] ^= MaskData[MaskOffset++ % 4];
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			int ret = Underlying.Read(buffer, offset, count);
			MaskBuffer(buffer, offset, ret, ref MaskOffsetRead);
			return ret;
		}

		public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
		{
			int ret = await Underlying.ReadAsync(buffer, offset, count, cancellationToken);
			MaskBuffer(buffer, offset, ret, ref MaskOffsetRead);
			return ret;
		}

		private class MyAsyncResult : IAsyncResult
		{
			public IAsyncResult Underlying { get; set; }
			public byte[] Buffer { get; set; }
			public int Offset { get; set; }

			public object AsyncState { get { return Underlying.AsyncState; } }
			public System.Threading.WaitHandle AsyncWaitHandle { get { return Underlying.AsyncWaitHandle; } }
			public bool CompletedSynchronously { get { return Underlying.CompletedSynchronously; } }
			public bool IsCompleted { get { return Underlying.IsCompleted; } }
		}

		public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
		{
			return new MyAsyncResult {
				Underlying = Underlying.BeginRead(buffer, offset, count, callback, state),
				Buffer = buffer,
				Offset = offset
			};
		}

		public override int EndRead(IAsyncResult asyncResult)
		{
			var mar = asyncResult as MyAsyncResult;
			if (mar == null)
				throw new ArgumentException();
			int read = Underlying.EndRead(mar.Underlying);
			MaskBuffer(mar.Buffer, mar.Offset, read, ref MaskOffsetRead);
			return read;
		}

		public override int ReadByte()
		{
			return Underlying.ReadByte() ^ MaskData[MaskOffsetRead++ % 4];
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			var bufferClone = new byte[count];
			Buffer.BlockCopy(buffer, offset, bufferClone, 0, count);
			MaskBuffer(bufferClone, 0, count, ref MaskOffsetWrite);
			Underlying.Write(bufferClone, 0, count);
		}

		public override Task WriteAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
		{
			var bufferClone = new byte[count];
			Buffer.BlockCopy(buffer, offset, bufferClone, 0, count);
			MaskBuffer(bufferClone, 0, count, ref MaskOffsetWrite);
			return Underlying.WriteAsync(bufferClone, 0, count, cancellationToken);
		}

		public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
		{
			var bufferClone = new byte[count];
			Buffer.BlockCopy(buffer, offset, bufferClone, 0, count);
			MaskBuffer(bufferClone, 0, count, ref MaskOffsetWrite);
			return Underlying.BeginWrite(bufferClone, 0, count, callback, state);
		}

		public override void EndWrite(IAsyncResult asyncResult)
		{
			Underlying.EndWrite(asyncResult);
		}

		public override void WriteByte(byte value)
		{
			Underlying.WriteByte((byte)(value ^ MaskData[MaskOffsetWrite++ % 4]));
		}

		public override Task FlushAsync(System.Threading.CancellationToken cancellationToken)
		{
			return Underlying.FlushAsync(cancellationToken);
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			switch (origin) {
			case SeekOrigin.Begin:
				MaskOffsetWrite = MaskOffsetRead = checked((int)(offset % 4));
				break;
			case SeekOrigin.Current:
				MaskOffsetWrite = MaskOffsetRead = checked((int)((MaskOffsetRead + offset) % 4));
				break;
			case SeekOrigin.End:
				MaskOffsetWrite = MaskOffsetRead = checked((int)((Length - offset) % 4));
				break;
			default:
				throw new NotImplementedException();
			}

			return Underlying.Seek(offset, origin);
		}

		public override void Flush()
		{
			Underlying.Flush();
		}

		public override void SetLength(long value)
		{
			Underlying.SetLength(value);
		}

		public override bool CanRead { get { return Underlying.CanRead; } }
		public override bool CanSeek { get { return Underlying.CanSeek; } }
		public override bool CanWrite { get { return Underlying.CanWrite; } }
		public override long Length { get { return Underlying.Length; } }
		public override bool CanTimeout { get { return Underlying.CanTimeout; } }
		public override int ReadTimeout {
			get { return Underlying.ReadTimeout; }
			set { Underlying.ReadTimeout = value; }
		}
		public override int WriteTimeout {
			get { return Underlying.WriteTimeout; }
			set { Underlying.WriteTimeout = value; }
		}
		public override long Position {
			get { return Underlying.Position; }
			set { Underlying.Position = value; }
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if (disposing)
				Underlying.Dispose();
		}
	}
}

