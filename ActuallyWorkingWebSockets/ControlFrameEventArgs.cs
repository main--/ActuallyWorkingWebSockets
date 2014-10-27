using System;

namespace ActuallyWorkingWebSockets
{
	public class ControlFrameEventArgs : EventArgs
	{
		public ControlFrame ControlFrame { get; private set; }
		public bool SuppressAutoResponse { get; set; }

		public ControlFrameEventArgs(ControlFrame frame)
		{
			ControlFrame = frame;
			SuppressAutoResponse = false;
		}
	}
}

