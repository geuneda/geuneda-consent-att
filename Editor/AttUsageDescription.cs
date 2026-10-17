using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Consent.Att.Editor
{
	/// <summary>
	/// iOS 추적 허용 창의 설명 문구(<c>NSUserTrackingUsageDescription</c>)입니다. 프로젝트에 하나만 둡니다(Create > Geuneda > Consent > ATT Usage Description).
	/// iOS 빌드 후처리가 기본 문구를 Info.plist 에, 언어별 문구를 <c>&lt;언어&gt;.lproj/InfoPlist.strings</c> 에 넣습니다.
	/// </summary>
	/// <remarks>에디터에서만 씁니다. 이 에셋이 없으면 다른 도구가 Info.plist 에 넣은 문구를 쓰고, 그것도 없으면 iOS 빌드가 실패합니다(문구 없이 물으면 앱이 종료됨).</remarks>
	[CreateAssetMenu(fileName = "AttUsageDescription", menuName = "Geuneda/Consent/ATT Usage Description")]
	public sealed class AttUsageDescription : ScriptableObject
	{
		private static readonly Regex LanguagePattern = new Regex("^[A-Za-z]{2,3}([-_][A-Za-z0-9]{2,8})*$");

		[SerializeField, TextArea(2, 5)]
		[Tooltip("기본 문구(Info.plist). 앱의 개발 언어로 씁니다")]
		private string _description;

		[SerializeField]
		[Tooltip("언어별 문구. 언어는 .lproj 이름(ko, ja, zh-Hans, zh-Hant 등)")]
		private List<LocalizedDescription> _localized = new List<LocalizedDescription>();

		public string Description => _description;

		public IReadOnlyList<LocalizedDescription> Localized => _localized;

		[Serializable]
		public struct LocalizedDescription
		{
			[SerializeField]
			private string _language;

			[SerializeField, TextArea(2, 5)]
			private string _description;

			public LocalizedDescription(string language, string description)
			{
				_language = language;
				_description = description;
			}

			public string Language => _language;

			public string Description => _description;
		}

		/// <summary>잘못된 설정의 이유 목록(비면 올바름)</summary>
		internal List<string> Validate()
		{
			var errors = new List<string>();

			if (string.IsNullOrWhiteSpace(_description))
			{
				errors.Add("the default description is empty");
			}

			var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (var localized in _localized)
			{
				if (string.IsNullOrEmpty(localized.Language) || !LanguagePattern.IsMatch(localized.Language))
				{
					errors.Add($"'{localized.Language}' is not a language name such as ko or zh-Hans");
				}
				else if (!languages.Add(localized.Language))
				{
					errors.Add($"language {localized.Language} appears more than once");
				}

				if (string.IsNullOrWhiteSpace(localized.Description))
				{
					errors.Add($"the description for {localized.Language} is empty");
				}
			}

			return errors;
		}

		/// <summary>프로젝트의 설정 에셋. 없으면 null, 둘 이상이면 <see cref="InvalidOperationException"/></summary>
		internal static AttUsageDescription Find()
		{
			var guids = AssetDatabase.FindAssets("t:" + nameof(AttUsageDescription));

			if (guids.Length == 0)
			{
				return null;
			}

			if (guids.Length > 1)
			{
				var paths = Array.ConvertAll(guids, AssetDatabase.GUIDToAssetPath);
				throw new InvalidOperationException($"Only one {nameof(AttUsageDescription)} asset is allowed: {string.Join(", ", paths)}");
			}

			return AssetDatabase.LoadAssetAtPath<AttUsageDescription>(AssetDatabase.GUIDToAssetPath(guids[0]));
		}

		internal void Set(string description, IEnumerable<LocalizedDescription> localized)
		{
			_description = description;
			_localized = new List<LocalizedDescription>(localized);
		}
	}
}
