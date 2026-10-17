using System.Threading;
using Cysharp.Threading.Tasks;
using Geuneda.Core;

// ReSharper disable once CheckNamespace

namespace Geuneda.Consent.Att
{
	/// <summary>
	/// iOS 앱 추적 투명성(ATT) 공급자입니다. iOS 14 이상 기기에서 아직 묻지 않았으면 허용을 묻고 답을 기다립니다.
	/// 그 밖의 플랫폼(Android, 에디터, iOS 14 미만)은 <see cref="TrackingStatus.NotSupported"/> 이고 묻지 않습니다.
	/// </summary>
	/// <remarks>
	/// <para>메인 스레드에서 씁니다. 앱이 활성 상태가 아니면 활성이 될 때 묻습니다(비활성 상태에서 물으면 시스템이 창 없이 답함).</para>
	/// <para>
	/// 창이 뜬 뒤 취소하면 기다리기만 그만두고 <see cref="ResultCodes.Cancelled"/> 입니다(시스템 창은 닫을 수 없고, 사용자의 답은 기기에 저장되어 <see cref="Status"/> 로 보임).
	/// 물었는데도 <see cref="TrackingStatus.NotDetermined"/> 면 시스템이 창을 띄우지 않은 것이라 경고를 남깁니다(다음 실행에서 다시 물음).
	/// </para>
	/// <para>iOS 빌드에는 추적 설명 문구가 있어야 합니다(없이 물으면 앱이 종료됨). README 의 '추적 설명 문구'를 봅니다.</para>
	/// </remarks>
	public sealed class AttTrackingAuthorization : ITrackingAuthorization
	{
		private static readonly ModuleLogger Logger = new ModuleLogger("Geuneda.Consent.Att");

		private readonly IAttPlatform _platform;
		private bool _busy;

		public AttTrackingAuthorization() : this(CreatePlatform())
		{
		}

		internal AttTrackingAuthorization(IAttPlatform platform)
		{
			_platform = platform;
		}

		public TrackingStatus Status => AttStatuses.From(_platform.ReadStatus());

		public async UniTask<Result> RequestAsync(CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return Result.Fail(ResultCodes.Cancelled);
			}

			if (_busy)
			{
				return Result.Fail(ResultCodes.InProgress, "A tracking authorization request is in progress");
			}

			if (Status != TrackingStatus.NotDetermined)
			{
				return Result.Ok();
			}

			_busy = true;

			try
			{
				if (!await _platform.RequestAsync(cancellationToken))
				{
					return Result.Fail(ResultCodes.Cancelled);
				}
			}
			finally
			{
				_busy = false;
			}

			var status = Status;

			if (status == TrackingStatus.NotDetermined)
			{
				Logger.Warning("ATT request finished without an answer; the system did not show the prompt");
			}
			else
			{
				Logger.Info($"ATT answered: {status}");
			}

			return Result.Ok();
		}

		private static IAttPlatform CreatePlatform()
		{
#if UNITY_IOS && !UNITY_EDITOR
			return new AttIosPlatform();
#else
			return new AttUnsupportedPlatform();
#endif
		}
	}
}
