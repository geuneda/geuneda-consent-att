using System.Threading;
using Cysharp.Threading.Tasks;

// ReSharper disable once CheckNamespace

namespace Geuneda.Consent.Att
{
	/// <summary>ATT 네이티브 호출입니다. 테스트에서 바꿔 끼웁니다.</summary>
	internal interface IAttPlatform
	{
		/// <summary>네이티브 상태 값(<see cref="AttStatuses"/>: -1 ATT 없음, 0~3 <c>ATTrackingManagerAuthorizationStatus</c>)</summary>
		int ReadStatus();

		/// <summary>허용을 묻고 답이 오면 true, 창을 띄우기 전에 취소되면 false 로 끝납니다. 창이 뜬 뒤의 취소는 기다리기만 그만둡니다(시스템 창은 닫을 수 없음)</summary>
		UniTask<bool> RequestAsync(CancellationToken cancellationToken);
	}
}
