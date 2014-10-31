using System;

namespace ActuallyWorkingWebSockets
{
	public enum PayloadLengthMode : int
	{
		None = -1,
		Short = 7,
		Medium = 16,
		Long = 64,
	}
}

