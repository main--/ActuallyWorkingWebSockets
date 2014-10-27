using System;

namespace ActuallyWorkingWebSockets
{
	public static class MyMath
	{
		// quick and dirty log2
		// there are faster ways and there are nicer ways
		// but it really doesn't matter
		public static int Log2(int i)
		{
			if (i < 0)
				throw new ArgumentOutOfRangeException("i");

			int log = 0;
			for (; i != 0; i >>= 1)
				log++;
			return log;
		}
	}
}

