using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;

namespace ActuallyWorkingWebSockets
{
	public class Synchronized<T> where T : class
	{
		public class LockAwaitable : INotifyCompletion
		{
			private Action Continuation;
			internal LockHolder Result;
			public bool IsCompleted { get; private set; }

			internal void NotifyCompletion()
			{
				IsCompleted = true;
				if (Continuation != null)
					ThreadPool.QueueUserWorkItem(_ => Continuation());
			}

			public LockHolder GetResult()
			{
				if (!IsCompleted)
					throw new InvalidOperationException();
				return Result;
			}

			public void OnCompleted(Action continuation)
			{
				if (Continuation != null)
					throw new InvalidOperationException("multiple continuations are not supported sry bout that");
				Continuation = continuation;
			}
		}

		public class LockHolder : IDisposable
		{
			private readonly Synchronized<T> Parent;
			public T LockedObject { get { return Parent.Object; } }

			internal LockHolder(Synchronized<T> parent)
			{
				Parent = parent;
			}

			public void Dispose()
			{
				Parent.FreeLock(this);
			}
		}
	
		private readonly T Object;
		private LockHolder Owner;
		private readonly ConcurrentQueue<LockAwaitable> Queue;

		public Synchronized(T t)
		{
			Object = t;
			Owner = null;
			Queue = new ConcurrentQueue<LockAwaitable>();
		}

		private void ScheduleAwaitable(LockAwaitable awaitable)
		{
			awaitable.Result = Owner = new LockHolder(this);
			awaitable.NotifyCompletion();
		}

		public async Task<LockHolder> ManualAcquire()
		{
			return await this;
		}

		public LockAwaitable GetAwaiter()
		{
			lock (this) {
				var awaitable = new LockAwaitable();
				if (Owner == null)
					ScheduleAwaitable(awaitable);
				else
					Queue.Enqueue(awaitable);
				return awaitable;
			}
		}

		public void FreeLock(LockHolder current)
		{
			lock (this) {
				if (current != Owner)
					throw new InvalidOperationException("dude wtf how could this happen");
				Owner = null;
				LockAwaitable next;
				if (Queue.TryDequeue(out next))
					// and schedule the next one
					ScheduleAwaitable(next);
			}
		}
	}
}

