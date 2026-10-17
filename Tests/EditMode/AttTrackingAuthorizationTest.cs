using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using Geuneda.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// ReSharper disable once CheckNamespace

namespace Geuneda.Consent.Att.Tests
{
	public sealed class AttTrackingAuthorizationTest
	{
		private sealed class FakePlatform : IAttPlatform
		{
			public int Status;
			public int AnswerStatus = 3;
			public int Requests;
			public UniTaskCompletionSource<bool> Hold;
			public CancellationToken LastToken;

			public int ReadStatus()
			{
				return Status;
			}

			public UniTask<bool> RequestAsync(CancellationToken cancellationToken)
			{
				Requests++;
				LastToken = cancellationToken;

				if (Hold != null)
				{
					return Hold.Task;
				}

				Status = AnswerStatus;
				return UniTask.FromResult(true);
			}
		}

		private sealed class Pending
		{
			public bool Done;
			public Result Value;

			public static Pending Of(UniTask<Result> task)
			{
				var pending = new Pending();
				Run(task, pending).Forget();
				return pending;
			}

			private static async UniTaskVoid Run(UniTask<Result> task, Pending pending)
			{
				pending.Value = await task;
				pending.Done = true;
			}
		}

		[TestCase(-1, TrackingStatus.NotSupported)]
		[TestCase(0, TrackingStatus.NotDetermined)]
		[TestCase(1, TrackingStatus.Restricted)]
		[TestCase(2, TrackingStatus.Denied)]
		[TestCase(3, TrackingStatus.Authorized)]
		[TestCase(4, TrackingStatus.Denied)]
		public void Status_MapsTheNativeValue(int native, TrackingStatus expected)
		{
			Assert.That(new AttTrackingAuthorization(new FakePlatform { Status = native }).Status, Is.EqualTo(expected));
		}

		[TestCase(-1)]
		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		public void AlreadyAnswered_OrNotSupported_DoesNotAsk(int native)
		{
			var platform = new FakePlatform { Status = native };

			var result = Pending.Of(new AttTrackingAuthorization(platform).RequestAsync(default));

			Assert.That(result.Done, Is.True);
			Assert.That(result.Value.IsSuccess, Is.True);
			Assert.That(platform.Requests, Is.EqualTo(0));
		}

		[Test]
		public void NotDetermined_Asks_AndTheAnswerIsTheStatus()
		{
			var platform = new FakePlatform { Status = 0, AnswerStatus = 2 };
			var att = new AttTrackingAuthorization(platform);

			var result = Pending.Of(att.RequestAsync(default));

			Assert.That(result.Value.IsSuccess, Is.True);
			Assert.That(platform.Requests, Is.EqualTo(1));
			Assert.That(att.Status, Is.EqualTo(TrackingStatus.Denied));
		}

		[Test]
		public void PromptNotShown_IsSuccess_WithAWarning()
		{
			var platform = new FakePlatform { Status = 0, AnswerStatus = 0 };
			LogAssert.Expect(LogType.Warning, new Regex("did not show the prompt"));

			var result = Pending.Of(new AttTrackingAuthorization(platform).RequestAsync(default));

			Assert.That(result.Value.IsSuccess, Is.True);
		}

		[Test]
		public void CancelledBeforeThePrompt_ReturnsCancelled_AndAllowsTheNextRequest()
		{
			var platform = new FakePlatform { Status = 0, Hold = new UniTaskCompletionSource<bool>() };
			var att = new AttTrackingAuthorization(platform);
			var first = Pending.Of(att.RequestAsync(default));

			var overlapping = Pending.Of(att.RequestAsync(default));
			Assert.That(overlapping.Value.Code, Is.EqualTo(ResultCodes.InProgress));

			platform.Hold.TrySetResult(false);
			Assert.That(first.Done, Is.True);
			Assert.That(first.Value.Code, Is.EqualTo(ResultCodes.Cancelled));

			platform.Hold = null;
			Assert.That(Pending.Of(att.RequestAsync(default)).Value.IsSuccess, Is.True);
			Assert.That(platform.Requests, Is.EqualTo(2));
		}

		[Test]
		public void AlreadyCancelledToken_ReturnsCancelled_WithoutAsking()
		{
			var platform = new FakePlatform { Status = 0 };

			var result = Pending.Of(new AttTrackingAuthorization(platform).RequestAsync(new CancellationToken(true)));

			Assert.That(result.Value.Code, Is.EqualTo(ResultCodes.Cancelled));
			Assert.That(platform.Requests, Is.EqualTo(0));
		}

		[Test]
		public void PublicConstructor_InTheEditor_IsNotSupported()
		{
			var att = new AttTrackingAuthorization();

			Assert.That(att.Status, Is.EqualTo(TrackingStatus.NotSupported));
			Assert.That(Pending.Of(att.RequestAsync(default)).Value.IsSuccess, Is.True);
		}
	}
}
