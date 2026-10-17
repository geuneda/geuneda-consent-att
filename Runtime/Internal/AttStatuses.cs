// ReSharper disable once CheckNamespace

namespace Geuneda.Consent.Att
{
	/// <summary>네이티브 상태 값을 <see cref="TrackingStatus"/> 로 바꿉니다. 값은 <c>Plugins/iOS/GeunedaAtt.mm</c> 과 같습니다.</summary>
	internal static class AttStatuses
	{
		public const int NotSupported = -1;

		public static TrackingStatus From(int status)
		{
			switch (status)
			{
				case NotSupported:
					return TrackingStatus.NotSupported;
				case 0:
					return TrackingStatus.NotDetermined;
				case 1:
					return TrackingStatus.Restricted;
				case 2:
					return TrackingStatus.Denied;
				case 3:
					return TrackingStatus.Authorized;
				default:
					// 나중에 생길 값은 추적하지 않는 쪽으로 본다
					return TrackingStatus.Denied;
			}
		}
	}
}
