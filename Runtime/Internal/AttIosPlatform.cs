#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using System.Threading;
using AOT;
using Cysharp.Threading.Tasks;
using Geuneda.Core;

// ReSharper disable once CheckNamespace

namespace Geuneda.Consent.Att
{
	/// <summary>
	/// iOS ATT 플러그인(<c>Plugins/iOS/GeunedaAtt.mm</c>)입니다. 요청마다 GCHandle 을 넘기고, 네이티브가 요청마다 정확히 한 번 부르는 콜백에서만 핸들을 풉니다.
	/// 콜백은 메인 스레드에서 오고, 이어지는 코드는 다음 처리에서 실행합니다(네이티브 콜백 안의 예외는 앱을 끝냄).
	/// </summary>
	internal sealed class AttIosPlatform : IAttPlatform
	{
		// 네이티브 콜백의 상태 값: 0~3 은 답, -1 ATT 없음, -2 창을 띄우기 전에 취소
		private const int CancelledBeforePrompt = -2;

		private delegate void CompletionCallback(IntPtr context, int status);

		// 네이티브가 들고 있을 함수 포인터. 수거되지 않게 둔다(바뀌지 않는 값)
		private static readonly CompletionCallback Completed = OnCompleted;

		private static readonly ModuleLogger Logger = new ModuleLogger("Geuneda.Consent.Att");

		[DllImport("__Internal")]
		private static extern int GeunedaAtt_Status();

		[DllImport("__Internal")]
		private static extern void GeunedaAtt_Request(IntPtr context, CompletionCallback callback);

		[DllImport("__Internal")]
		private static extern void GeunedaAtt_Cancel(IntPtr context);

		public int ReadStatus()
		{
			return GeunedaAtt_Status();
		}

		public async UniTask<bool> RequestAsync(CancellationToken cancellationToken)
		{
			var request = new Request();
			request.Context = GCHandle.ToIntPtr(GCHandle.Alloc(request));
			GeunedaAtt_Request(request.Context, Completed);

			// 취소는 아무 스레드에서나 올 수 있어 메인 스레드로 넘긴다. 창을 띄우기 전이면 네이티브가 취소로 콜백한다
			using (cancellationToken.Register(() => MainThreadDispatcher.Post(request.Cancel)))
			{
				return await request.Completion.Task;
			}
		}

		[MonoPInvokeCallback(typeof(CompletionCallback))]
		private static void OnCompleted(IntPtr context, int status)
		{
			try
			{
				var handle = GCHandle.FromIntPtr(context);
				var request = (Request) handle.Target;
				request.Context = IntPtr.Zero;
				handle.Free();
				MainThreadDispatcher.Post(() => request.Completion.TrySetResult(status != CancelledBeforePrompt));
			}
			catch (Exception exception)
			{
				Logger.Error(exception, "ATT callback failed");
			}
		}

		private sealed class Request
		{
			public readonly UniTaskCompletionSource<bool> Completion = new UniTaskCompletionSource<bool>();
			// 네이티브 콜백이 오면 0 으로 바꾼다(그 뒤 같은 값을 다음 요청이 받을 수 있다)
			public IntPtr Context;

			public void Cancel()
			{
				// 이미 끝난 요청의 늦은 취소는 다음 요청을 건드리지 않는다
				if (Context == IntPtr.Zero)
				{
					return;
				}

				// 창을 띄우기 전이면 네이티브가 취소로 콜백해 핸들을 푼다. 창이 떠 있으면 답이 올 때 풀고, 여기서는 기다리기만 그만둔다
				GeunedaAtt_Cancel(Context);
				Completion.TrySetResult(false);
			}
		}
	}
}
#endif
