using System;

namespace ActuallyWorkingWebSockets
{
	public class ControlFrame
	{
		public enum Type { Close, Ping, Pong }

		public Type FrameType { get; set; }
		public byte[] Payload { get; set; }

		public ControlFrame()
		{
			Payload = new byte[0];
		}
	}
}

