#if UNITY_IOS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Consent.Att.Editor
{
	/// <summary>
	/// iOS 빌드의 Xcode 프로젝트에 추적 설명 문구를 넣고 AppTrackingTransparency 를 UnityFramework 에 약하게 링크합니다.
	/// 다른 SDK 의 후처리(MAX 등) 뒤에 실행해 설정 에셋의 문구가 남게 합니다. iOS 빌드 대상일 때만 컴파일합니다(UnityEditor.iOS.Xcode).
	/// </summary>
	internal sealed class AttPostprocessBuild : IPostprocessBuildWithReport
	{
		public const string UsageKey = "NSUserTrackingUsageDescription";

		private const string LocalizationsKey = "CFBundleLocalizations";
		private const string DevelopmentRegionKey = "CFBundleDevelopmentRegion";

		public int callbackOrder => 1000;

		public void OnPostprocessBuild(BuildReport report)
		{
			if (report.summary.platform != BuildTarget.iOS)
			{
				return;
			}

			var outputPath = report.summary.outputPath;
			var settings = FindSettings();
			var languages = WriteInfoPlist(Path.Combine(outputPath, "Info.plist"), settings);
			UpdateProject(outputPath, settings, languages);
		}

		private static AttUsageDescription FindSettings()
		{
			AttUsageDescription settings;

			try
			{
				settings = AttUsageDescription.Find();
			}
			catch (InvalidOperationException exception)
			{
				throw new BuildFailedException(exception.Message);
			}

			if (settings == null)
			{
				return null;
			}

			var errors = settings.Validate();

			if (errors.Count > 0)
			{
				throw new BuildFailedException($"{AssetDatabase.GetAssetPath(settings)}: {string.Join("; ", errors)}");
			}

			return settings;
		}

		// 기본 문구와 언어 목록을 쓴다. 설정 에셋이 없으면 다른 도구가 넣은 문구가 있어야 한다
		private static List<string> WriteInfoPlist(string plistPath, AttUsageDescription settings)
		{
			var plist = new PlistDocument();
			plist.ReadFromFile(plistPath);
			var languages = new List<string>();

			if (settings == null)
			{
				if (!plist.root.values.TryGetValue(UsageKey, out var existing) || string.IsNullOrWhiteSpace(existing.AsString()))
				{
					throw new BuildFailedException(
						$"Geuneda.Consent.Att needs {UsageKey} in Info.plist (asking for tracking without it terminates the app). " +
						"Create one with Assets > Create > Geuneda > Consent > ATT Usage Description");
				}

				return languages;
			}

			plist.root.SetString(UsageKey, settings.Description);

			foreach (var localized in settings.Localized)
			{
				languages.Add(localized.Language);
			}

			if (languages.Count > 0)
			{
				// CFBundleLocalizations 는 문자열 배열이어야 한다(App Store 검증). 다른 도구가 넣은 언어는 남긴다
				var array = plist.root.values.TryGetValue(LocalizationsKey, out var value) && value is PlistElementArray existingArray ? existingArray : null;

				if (array == null)
				{
					array = plist.root.CreateArray(LocalizationsKey);
					var developmentRegion = plist.root.values.TryGetValue(DevelopmentRegionKey, out var region) ? region.AsString() : null;
					array.AddString(string.IsNullOrEmpty(developmentRegion) ? "en" : developmentRegion);
				}

				foreach (var language in languages)
				{
					if (!array.values.Exists(element => element is PlistElementString text && string.Equals(text.value, language, StringComparison.OrdinalIgnoreCase)))
					{
						array.AddString(language);
					}
				}
			}

			plist.WriteToFile(plistPath);
			return languages;
		}

		private static void UpdateProject(string outputPath, AttUsageDescription settings, List<string> languages)
		{
			var projectPath = PBXProject.GetPBXProjectPath(outputPath);
			var project = new PBXProject();
			project.ReadFromFile(projectPath);

			// 플러그인(.mm)은 UnityFramework 에 들어간다. iOS 13 기기에서도 실행되게 약하게 링크한다
			project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "AppTrackingTransparency.framework", true);

			if (settings != null)
			{
				var mainTarget = project.GetUnityMainTargetGuid();

				foreach (var localized in settings.Localized)
				{
					// .lproj 폴더 안에 두어야 Xcode 가 그 언어의 리소스로 번들에 복사한다
					var relativePath = $"{localized.Language}.lproj/InfoPlist.strings";
					var absolutePath = Path.Combine(outputPath, relativePath);
					Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
					var existing = File.Exists(absolutePath) ? File.ReadAllText(absolutePath) : null;
					string merged;

					try
					{
						merged = InfoPlistStrings.Set(existing, UsageKey, localized.Description);
					}
					catch (FormatException exception)
					{
						throw new BuildFailedException($"{absolutePath} is not a readable .strings file: {exception.Message}");
					}

					File.WriteAllText(absolutePath, merged, new UTF8Encoding(false));

					if (string.IsNullOrEmpty(project.FindFileGuidByProjectPath(relativePath)))
					{
						project.AddFileToBuild(mainTarget, project.AddFile(relativePath, relativePath, PBXSourceTree.Source));
					}
				}
			}

			project.WriteToFile(projectPath);
			Debug.Log($"[Geuneda.Consent.Att] Added {UsageKey} ({(settings == null ? "from another tool" : "default + " + languages.Count + " languages")}) and weak-linked AppTrackingTransparency");
		}
	}
}
#endif
