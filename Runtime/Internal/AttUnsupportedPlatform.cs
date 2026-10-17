using System.Threading;
using Cysharp.Threading.Tasks;

// ReSharper disable once CheckNamespace

namespace Geuneda.Consent.Att
{
	/// <summary>ATT 가 없는 플랫폼(Android, 에디터, 그 밖)입니다.</summary>
	internal sealed class AttUnsupportedPlatform : IAttPlatform
	{
		public int ReadStatus()
		{
			return AttStatuses.NotSupported;
		}

		public UniTask<bool> RequestAsync(CancellationToken cancellationToken)
		{
			return UniTask.FromResult(true);
		}
	}
}
