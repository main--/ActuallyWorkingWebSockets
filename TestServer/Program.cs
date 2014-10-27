using System;
using ActuallyWorkingWebSockets;

namespace TestServer
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			var server = new WebSocketServer(new System.Net.IPEndPoint(
				System.Net.IPAddress.Any, 31337)) { ClientHandler = async session => {
					var textIn = await session.ReceiveTextMessage();
					Console.WriteLine ("got text in: {0}", textIn);
					await session.SendTextMessage("response: " + textIn);
					var inStream = await session.ReceiveBinaryMessage();
					Console.WriteLine (await new System.IO.StreamReader(inStream).ReadToEndAsync());
					await session.SendTextMessage("ack");
				} };
			var serverTask = server.RunAsync();
			Console.ReadKey(true);
			server.RequestShutdown();
			serverTask.Wait();
		}
	}
}
