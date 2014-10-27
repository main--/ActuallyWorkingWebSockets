using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;

namespace ActuallyWorkingWebSockets
{
	public class WebSocketServer
	{
		private readonly Socket Listener;
		public Func<WebSocketSession, Task> ClientHandler { get; set; }

		public WebSocketServer(IPEndPoint listenEP)
		{
			Listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			Listener.Bind(listenEP);
		}

		public async Task RunAsync()
		{
			Listener.Listen(8);
			var activeTasks = new List<Task>();
			activeTasks.Add(Listener.AcceptAsyncNew());

			while (activeTasks.Count > 0) {
				Debug.WriteLine("loop");
				var finished = await Task.WhenAny(activeTasks);
				activeTasks.Remove(finished);
				Debug.WriteLine("finished one");
				var acceptTask = finished as Task<Socket>;
				if (acceptTask != null) {
					Debug.WriteLine("got client");
					try {
						var client = acceptTask.Result;
						activeTasks.Add(HandleClientAsnyc(client));

						// and listen for another one
						activeTasks.Add(Listener.AcceptAsyncNew());
					} catch (AggregateException e) {
						if (!(e.InnerExceptions.Single() is InvalidOperationException))
							throw;
						// else disposed, just ignore and shutdown
					}
				} else {
					// client finished, maybe there was an exception?
					try {
						finished.Wait();
					} catch (AggregateException e) {
						Trace.WriteLine(e);
					}
					Debug.WriteLine("client rekt");
				}
			}
		}

		public void RequestShutdown()
		{
			Listener.Close();
		}

		#region mandatory request headers
		private static readonly string Host = "Host";
		private static readonly string Upgrade = "Upgrade";
		private static readonly string Connection = "Connection";
		private static readonly string SecWebSocketKey = "Sec-WebSocket-Key";
		private static readonly string SecWebSocketVersion = "Sec-WebSocket-Version";
		#endregion

		#region optional request headers
		private static readonly string Origin = "Origin";
		private static readonly string SecWebSocketProtocol = "Sec-WebSocket-Protocol";
		private static readonly string SecWebSocketExtensions = "Sec-WebSocket-Extensions";
		#endregion

		private static readonly string[] KnownHeaders = new string[] { Host, Upgrade, Connection,
			SecWebSocketKey, SecWebSocketVersion, Origin, SecWebSocketProtocol, SecWebSocketExtensions };

		private async Task HandleClientAsnyc(Socket client)
		{
			Debug.WriteLine("handling");
			using (var netStream = new NetworkStream(client, ownsSocket: true)) {
				Debug.WriteLine("made netstream");
				var headers = new Dictionary<string, string>();
				using (var reader = new StreamReader(netStream, Encoding.ASCII, false, bufferSize: 1024, leaveOpen: true)) {
					string status = await reader.ReadLineAsync();
					Trace.Assert(status == "GET / HTTP/1.1", "request status check failed", "Actual request status was: '" + status + "'");

					string line;
					do {
						line = await reader.ReadLineAsync();
						foreach (var header in KnownHeaders)
							if (line.StartsWith(header + ":", true, System.Globalization.CultureInfo.InvariantCulture))
								headers.Add(header, line.Substring(header.Length + 2));
					} while (line != String.Empty);

					Debug.WriteLine("found end of headers");

					if ((headers[Connection].ToLowerInvariant() != "upgrade")
					    || (headers[Upgrade].ToLowerInvariant() != "websocket"))
						return; // invalid headers, fuck it.

					// TODO: maybe care about origin
				}

				// we no longer need the streamreader. we're gonna switch protocols soon :D

				// calculate key
				// yes, the RFC defines a hardcoded value
				// and wow, this is surprisingly easy in C#
				var acceptKey = Convert.ToBase64String(System.Security.Cryptography.SHA1.Create().ComputeHash(
					Encoding.ASCII.GetBytes(headers[SecWebSocketKey] + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));

				using (var writer = new StreamWriter(netStream, Encoding.ASCII, bufferSize: 1024, leaveOpen: true) { NewLine = "\r\n" }) {
					await writer.WriteLineAsync("HTTP/1.1 101 Switching Protocols");
					await writer.WriteLineAsync("Upgrade: websocket");
					await writer.WriteLineAsync("Connection: Upgrade");
					await writer.WriteLineAsync("Sec-WebSocket-Accept: " + acceptKey);
					await writer.WriteLineAsync();
					await writer.FlushAsync();
				}

				// we no longer need the streamwriter, SWITCHING PROTOCOLS NOW
				// ---------------------------------------------------------------

				using (var session = new WebSocketSession(netStream))
					if (ClientHandler != null)
						await ClientHandler(session);

				// We sent a Close message. Spec says that we should wait for their CloseACK now.
				// But maybe we read a frame and it's something else. What should we do then?
				// What if they just keep sending data? DDoS much?
				// So we'll just close the connection.
			}
		}
	}
}

