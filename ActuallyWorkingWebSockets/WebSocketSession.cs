using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ActuallyWorkingWebSockets
{
	public class WebSocketSession : IDisposable
	{
		private readonly Synchronized<Stream> OutputStream;
		private readonly Synchronized<Stream> InputStream;

		/// <summary>
		/// Gets or sets a value indicating whether this <see cref="ActuallyWorkingWebSockets.WebSocketSession"/>
		/// is masking *outgoing* data.
		/// </summary>
		/// <value><c>true</c> if masking; otherwise, <c>false</c>.</value>
		public bool Masking { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether this <see cref="ActuallyWorkingWebSockets.WebSocketSession"/>
		/// automatically sends pong frames in response to ping frames.
		/// </summary>
		/// <value><c>true</c> if auto pong is enabled; otherwise, <c>false</c>.</value>
		public bool AutoPong { get; set; }

		public event EventHandler<ControlFrameEventArgs> OnControlFrame;

		public WebSocketSession(Stream stream)
		{
			OutputStream = new Synchronized<Stream>(stream);
			InputStream = new Synchronized<Stream>(stream);
		}

		public Task SendTextMessage(string message)
		{
			return WebSocketProtocol.SendTextFrame(OutputStream, message, Masking);
		}

		public Task SendStream(Stream stream)
		{
			return WebSocketProtocol.SendStream(OutputStream, stream, Masking);
		}

		public Task Ping()
		{
			return Ping(CancellationToken.None);
		}

		public async Task Ping(CancellationToken token)
		{
			var payload = Guid.NewGuid().ToByteArray();
			var pongSource = new TaskCompletionSource<object>();
			token.Register(() => pongSource.TrySetCanceled());

			EventHandler<ControlFrameEventArgs> completionTrigger = (sender, e) =>  {
				if ((e.ControlFrame.FrameType == ControlFrame.Type.Pong)
					&& payload.SequenceEqual(e.ControlFrame.Payload))
					pongSource.TrySetResult(null);
			};

			OnControlFrame += completionTrigger;

			await WebSocketProtocol.SendControlFrame(OutputStream, new ControlFrame {
				Payload = payload,
				FrameType = ControlFrame.Type.Ping,
			});

			// either wait until they read it for us
			// or aquire the readlock and read it on our own
			var readLockAcquire = InputStream.ManualAcquire();
			var pongTask = pongSource.Task;
			if (pongTask == await Task.WhenAny(readLockAcquire.Task, pongTask)) {
				System.Diagnostics.Debug.WriteLine("nice, they read it for us");
				readLockAcquire.Cancel();
			} else {
				System.Diagnostics.Debug.WriteLine("welp, our work");
				using (var readLock = await readLockAcquire.Task)
					await WebSocketProtocol.ReadFrameGroupLockAcquired(readLock, HandleControlFrame, false);

				// pls tell me that this was the frame we needed
				if (!pongTask.IsCompleted)
					throw new InvalidDataException("we pinged and they sent something else. welp.");
			}

			OnControlFrame -= completionTrigger;
		}

		public async Task<string> ReceiveTextMessage()
		{
			var read = await WebSocketProtocol.ReadFrameGroup(InputStream, HandleControlFrame) as string;
			if (read == null)
				throw new InvalidOperationException("wanted to read string but got something else");
			return read;
		}

		public async Task<Stream> ReceiveBinaryMessage()
		{
			var read = await WebSocketProtocol.ReadFrameGroup(InputStream, HandleControlFrame) as Stream;
			if (read == null)
				throw new InvalidOperationException("wanted to read stream but got something else");
			return read;
		}

		private async Task HandleControlFrame(ControlFrame frame)
		{
			var eventargs = new ControlFrameEventArgs(frame);
			var controlFrameEvent = OnControlFrame;
			if (controlFrameEvent != null)
				await Task.Factory.FromAsync(controlFrameEvent.BeginInvoke,
					controlFrameEvent.EndInvoke, this, eventargs, null);

			if (!eventargs.SuppressAutoResponse && AutoPong && (frame.FrameType == ControlFrame.Type.Ping))
				await WebSocketProtocol.SendControlFrame(OutputStream,
					new ControlFrame { FrameType = ControlFrame.Type.Pong, Payload = frame.Payload });
		}

		void IDisposable.Dispose()
		{
			WebSocketProtocol.SendControlFrame(OutputStream, new ControlFrame {
				FrameType = ControlFrame.Type.Close,
			}).Wait();
		}
	}
}

