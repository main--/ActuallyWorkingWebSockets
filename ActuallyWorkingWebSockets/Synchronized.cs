using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

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
				if (IsCompleted)
					continuation();
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

		public class ManualAcquisition
		{
			public TaskCompletionSource<LockHolder> TCS { get; set; }
			public Task<LockHolder> Task { get { return TCS.Task; } }

			public void Cancel()
			{
				if (!TCS.TrySetCanceled())
					TCS.Task.Result.Dispose();
			}
		}

		private readonly T Object;
		private LockHolder Owner;
		private readonly LinkedList<LockAwaitable> Queue;

		public Synchronized(T t)
		{
			Object = t;
			Owner = null;
			Queue = new LinkedList<LockAwaitable>();
		}

		private void ScheduleAwaitable(LockAwaitable awaitable)
		{
			awaitable.Result = Owner = new LockHolder(this);
			awaitable.NotifyCompletion();
		}

		public ManualAcquisition ManualAcquire()
		{
			var compl = new TaskCompletionSource<LockHolder>();
			var awaitable = GetAwaiter();
			awaitable.OnCompleted(() => {
				if (!compl.TrySetResult(awaitable.GetResult()))
					awaitable.GetResult().Dispose();
			});
			var parent = this;
			compl.Task.ContinueWith(task => {
				lock (parent) {
					this.Queue.Remove(awaitable);
				}
			}, TaskContinuationOptions.OnlyOnCanceled);
			return new ManualAcquisition { TCS = compl };
		}

		public LockAwaitable GetAwaiter()
		{
			lock (this) {
				var awaitable = new LockAwaitable();
				if (Owner == null)
					ScheduleAwaitable(awaitable);
				else
					Queue.AddLast(awaitable);
				return awaitable;
			}
		}

		public void FreeLock(LockHolder current)
		{
			lock (this) {
				if (current != Owner)
					throw new InvalidOperationException("dude wtf how could this happen");
				Owner = null;
				if (Queue.Count > 0) {
					var next = Queue.First.Value;
					Queue.RemoveFirst();
					ScheduleAwaitable(next);
				}
			}
		}
	}
}

