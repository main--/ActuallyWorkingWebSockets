using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ActuallyWorkingWebSockets
{
	public static class WebSocketProtocol
	{
		[ThreadStatic]
		private static System.Security.Cryptography.RandomNumberGenerator CSPRNG;

		#region byte1 flags
		private static readonly byte FLAG1_FIN = 0x80;
		//private static readonly byte FLAG1_RSV1 = 0x40;
		//private static readonly byte FLAG1_RSV2 = 0x20;
		//private static readonly byte FLAG1_RSV3 = 0x10;
		#endregion

		#region byte2 flags
		private static readonly byte FLAG2_MASK = 0x80;
		#endregion

		private static void BuildFrameHeader(FrameOpcode opcode, int appDataLength, bool masking, out byte[] maskData, out byte[] buffer, out int appDataOffset, bool bareHeader)
		{
			var requiredLengthBits = MyMath.Log2(appDataLength);

			var plm = PayloadLengthMode.None;
			foreach (PayloadLengthMode mode in Enum.GetValues(typeof(PayloadLengthMode))) {
				if (((int)mode) >= requiredLengthBits) {
					plm = mode;
					break;
				}
			}

			// TODO: implement extensions
			int bufferLength = 2 + ((((int)plm) - 7) / 8) + (masking ? 4 : 0) + (bareHeader ? 0 : appDataLength);
			buffer = new byte[bufferLength];
			buffer[0] = (byte)((byte)opcode | FLAG1_FIN);

			switch (plm) {
			case PayloadLengthMode.Short:
				buffer[1] = (byte)appDataLength;
				appDataOffset = 2;
				break;
			case PayloadLengthMode.Medium:
				buffer[1] = 126;
				var shortLength = BitConverter.GetBytes(checked((UInt16)appDataLength));
				Array.Copy(shortLength, 0, buffer, 2, shortLength.Length);
				appDataOffset = 4;
				break;
			case PayloadLengthMode.Long:
				var longLength = BitConverter.GetBytes(checked((UInt64)appDataLength));
				Array.Copy(longLength, 0, buffer, 2, longLength.Length);
				appDataOffset = 10;
				break;
			default:
				throw new InvalidOperationException("Either the string is impossibly large (HI FUTURE! :D) or the selection loop (or the switch) has failed.");
			}

			if (masking) {
				maskData = new byte[4];
				if (CSPRNG == null)
					CSPRNG = System.Security.Cryptography.RandomNumberGenerator.Create();
				CSPRNG.GetBytes(maskData);
				buffer[1] |= FLAG2_MASK;
				Array.Copy(maskData, 0, buffer, appDataOffset, maskData.Length);
				appDataOffset += 4;
			} else
				maskData = null;
		}

		private static async Task SendFixedFrame(Synchronized<Stream> sStream, FrameOpcode opcode, int appDataLength, bool masking, Action<byte[], int> dataWriter)
		{
			int appDataOffset;
			byte[] buffer, maskData;
			BuildFrameHeader(opcode, appDataLength, masking, out maskData, out buffer, out appDataOffset, bareHeader: false);

			dataWriter(buffer, appDataOffset);

			if (masking)
				for (int i = 0; i < appDataLength; i++)
					buffer[i + appDataOffset] ^= maskData[i % 4];

			using (var streamHolder = await sStream)
				await streamHolder.LockedObject.WriteAsync(buffer, 0, buffer.Length);
		}

		public static Task SendTextFrame(Synchronized<Stream> stream, string message, bool masking)
		{
			return SendFixedFrame(stream, FrameOpcode.Text, Encoding.UTF8.GetByteCount(message), masking, (buffer, appDataOffset) => {
				Encoding.UTF8.GetBytes(message, 0, message.Length, buffer, appDataOffset);
			});
		}

		public static Task SendByteArrayFrame(Synchronized<Stream> stream, byte[] data, bool masking)
		{
			return SendFixedFrame(stream, FrameOpcode.Binary, data.Length, masking, (buffer, appDataOffset) => {
				Buffer.BlockCopy(data, 0, buffer, appDataOffset, data.Length);
			});
		}

		public static Task SendControlFrame(Synchronized<Stream> stream, ControlFrame frame)
		{
			return SendFixedFrame(stream, frame.FrameType.ToControlFrameOpcode(),
				frame.Payload.Length, false, (buffer, appDataOffset) => {
					Buffer.BlockCopy(frame.Payload, 0, buffer, appDataOffset, frame.Payload.Length);
				});
		}

		public static async Task SendStream(Synchronized<Stream> sSocketStream, Stream dataStream, bool masking)
		{
			using (var socketStreamHolder = await sSocketStream) {
				var socketStream = socketStreamHolder.LockedObject;

				int appDataOffset;
				byte[] header, maskData;
				BuildFrameHeader(FrameOpcode.Binary, checked((int)dataStream.Length), masking, out maskData, out header, out appDataOffset, bareHeader: true);

				await socketStream.WriteAsync(header, 0, header.Length);

				if (masking)
					socketStream = new MaskingStream(socketStream, maskData);

				await dataStream.CopyToAsync(socketStream);
			}
		}


		public struct FrameHeader
		{
			public FrameOpcode Opcode { get; set; }
			public bool GroupIsComplete { get; set; }
			public bool IsMasked { get; set; }
			public int PayloadLength { get; set; }
			public byte[] MaskData { get; set; }
		}

		public static async Task<FrameHeader> ReadFrameHeader(Stream stream)
		{
			var firstByte = await stream.ReadByteAsync();
			var opcode = (FrameOpcode)(firstByte & 0x0F);
			System.Diagnostics.Debug.WriteLine(opcode, "opcode");

			var flags = firstByte & 0xF0;
			var complete = (flags == FLAG1_FIN);
			if ((flags & ~FLAG1_FIN) != 0)
				throw new InvalidDataException("extensions not supported");

			System.Diagnostics.Debug.WriteLine(complete, "complete");
			var secondByte = await stream.ReadByteAsync();
			var isMasked = (secondByte & FLAG2_MASK) != 0;
			int payloadLength = secondByte & ~FLAG2_MASK;
			System.Diagnostics.Debug.WriteLine(payloadLength, "payloadLength pre");
			// need to reverse them cause endianness or something
			if (payloadLength == 126)
				payloadLength = BitConverter.ToInt16(Util.ReverseArray(await stream.ReadAllBytesAsync(2)), 0);
			else if (payloadLength == 127)
				payloadLength = BitConverter.ToInt32(Util.ReverseArray(await stream.ReadAllBytesAsync(4)), 0);
			System.Diagnostics.Debug.WriteLine(payloadLength, "payloadLength post");

			var maskData = isMasked ? await stream.ReadAllBytesAsync(4) : null;

			return new FrameHeader { Opcode = opcode, GroupIsComplete = complete, IsMasked = isMasked, PayloadLength = payloadLength, MaskData = maskData };
		}

		public static async Task<object> ReadFrameGroup(Synchronized<Stream> stream, Func<ControlFrame, Task> controlFrameHandler)
		{
			using (var streamHolder = await stream)
				return await ReadFrameGroupLockAcquired(streamHolder, controlFrameHandler);
		}

		public static async Task<object> ReadFrameGroupLockAcquired(Synchronized<Stream>.LockHolder streamHolder, Func<ControlFrame, Task> controlFrameHandler, bool loop = true)
		{
			var stream = streamHolder.LockedObject;
			var textFragments = new List<byte[]>(1);
			do {
				var header = await ReadFrameHeader(stream);
				switch (header.Opcode) {
				case FrameOpcode.Binary:
					if (textFragments.Count > 0)
						throw new InvalidDataException();
					return new WebSocketInputStream(header, streamHolder);
				case FrameOpcode.Continuation:
					// we don't have to expect that the client breaks the protocol
					// if this continues binary, we already returned the stream
				case FrameOpcode.Text:
					var buffer = await (header.IsMasked ? new MaskingStream(stream,
						             header.MaskData) : stream).ReadAllBytesAsync(header.PayloadLength);

					textFragments.Add(buffer);
					if (header.GroupIsComplete) {
						if (textFragments.Count == 1)
							return Encoding.UTF8.GetString(buffer);

						var finalBuffer = new byte[textFragments.Sum(array => array.Length)];
						int offset = 0;
						foreach (var part in textFragments) {
							Buffer.BlockCopy(part, 0, finalBuffer, offset, part.Length);
							offset += part.Length;
						}

						return Encoding.UTF8.GetString(finalBuffer);
					}

					break;
				case FrameOpcode.Ping:
				case FrameOpcode.Pong:
				case FrameOpcode.Close:
					await HandleControlFrame(header, stream, controlFrameHandler);
					break;
				default:
					throw new InvalidDataException("unknown opcode");
				}
			} while (loop);
			return null;
		}

		public static async Task HandleControlFrame(FrameHeader header, Stream stream, Func<ControlFrame, Task> handler)
		{
			var payload = await (header.IsMasked ? new MaskingStream(stream,
				header.MaskData) : stream).ReadAllBytesAsync(header.PayloadLength);
			if (!header.GroupIsComplete)
				throw new InvalidDataException("RFC states that control frames must not be fragmented");
			await handler(new ControlFrame { FrameType = header.Opcode.ToControlFrameType(), Payload = payload });
		}
	}
}
