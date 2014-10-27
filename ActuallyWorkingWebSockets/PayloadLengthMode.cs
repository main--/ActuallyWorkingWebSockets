using System;

namespace ActuallyWorkingWebSockets
{
	public enum PayloadLengthMode : int
	{
		None = -1,
		Short = 7,
		Medium = 7+16,
		Long = 7+64,
	}
}

