using Geuneda.Consent.Att.Editor;
using NUnit.Framework;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Geuneda.Consent.Att.Tests
{
	public sealed class AttUsageDescriptionTest
	{
		private static AttUsageDescription Create(string description, params AttUsageDescription.LocalizedDescription[] localized)
		{
			var settings = ScriptableObject.CreateInstance<AttUsageDescription>();
			settings.Set(description, localized);
			return settings;
		}

		[Test]
		public void Validate_AcceptsADefaultAndLanguages()
		{
			var settings = Create("Used for personalized ads",
				new AttUsageDescription.LocalizedDescription("ko", "맞춤 광고에 씁니다"),
				new AttUsageDescription.LocalizedDescription("zh-Hant", "用於個人化廣告"));

			Assert.That(settings.Validate(), Is.Empty);
			Object.DestroyImmediate(settings);
		}

		[Test]
		public void Validate_ReportsEmptyTextsBadAndDuplicateLanguages()
		{
			var settings = Create(" ",
				new AttUsageDescription.LocalizedDescription("ko", "a"),
				new AttUsageDescription.LocalizedDescription("KO", "b"),
				new AttUsageDescription.LocalizedDescription("ko/../x", "c"),
				new AttUsageDescription.LocalizedDescription("ja", ""));

			var errors = settings.Validate();

			Assert.That(errors, Has.Count.EqualTo(4), string.Join("\n", errors));
			Assert.That(errors[0], Does.Contain("default description"));
			Assert.That(errors[1], Does.Contain("more than once"));
			Assert.That(errors[2], Does.Contain("not a language name"));
			Assert.That(errors[3], Does.Contain("ja is empty"));
			Object.DestroyImmediate(settings);
		}

		[Test]
		public void InfoPlistStrings_AddsReplacesAndKeepsOtherKeys()
		{
			const string key = "NSUserTrackingUsageDescription";

			Assert.That(InfoPlistStrings.Set(null, key, "a"), Is.EqualTo("\"NSUserTrackingUsageDescription\" = \"a\";\n"));

			var other = "\"CFBundleDisplayName\" = \"게임\";";
			Assert.That(InfoPlistStrings.Set(other, key, "a"), Is.EqualTo(other + "\n\"NSUserTrackingUsageDescription\" = \"a\";\n"));

			var existing = "\"CFBundleDisplayName\" = \"게임\";\n  \"NSUserTrackingUsageDescription\" = \"old\";\n\"Other\" = \"x\";\n";
			Assert.That(InfoPlistStrings.Set(existing, key, "new $1 \"q\""),
				Is.EqualTo("\"CFBundleDisplayName\" = \"게임\";\n  \"NSUserTrackingUsageDescription\" = \"new $1 \\\"q\\\"\";\n\"Other\" = \"x\";\n"));
		}

		[Test]
		public void InfoPlistStrings_ReplacesOnlyTheEntry_OnASharedLine()
		{
			const string key = "NSUserTrackingUsageDescription";
			var existing = "\"NSUserTrackingUsageDescription\" = \"old; still old\"; \"CFBundleDisplayName\" = \"게임\";\n";

			Assert.That(InfoPlistStrings.Set(existing, key, "new"),
				Is.EqualTo("\"NSUserTrackingUsageDescription\" = \"new\"; \"CFBundleDisplayName\" = \"게임\";\n"));
		}

		[Test]
		public void InfoPlistStrings_HandlesCrlfCommentsEscapesAndDuplicates()
		{
			const string key = "NSUserTrackingUsageDescription";
			var existing = "/* \"NSUserTrackingUsageDescription\" = \"in a comment\"; */\r\n"
			               + "\"Quote\" = \"a \\\"q\\\" b\"; // trailing\r\n"
			               + "\"NSUserTrackingUsageDescription\" = \"old\";\r\n"
			               + "NSUserTrackingUsageDescription = \"duplicate\";\r\n";

			var merged = InfoPlistStrings.Set(existing, key, "new");

			Assert.That(merged, Is.EqualTo("/* \"NSUserTrackingUsageDescription\" = \"in a comment\"; */\r\n"
			                               + "\"Quote\" = \"a \\\"q\\\" b\"; // trailing\r\n"
			                               + "\"NSUserTrackingUsageDescription\" = \"new\";\r\n"
			                               + "\r\n"));
			Assert.That(InfoPlistStrings.Set("\"A\" = \"b\";\r\n", key, "x"), Is.EqualTo("\"A\" = \"b\";\r\n\"NSUserTrackingUsageDescription\" = \"x\";\r\n"));
		}

		[Test]
		public void InfoPlistStrings_TreatsUnicodeAndOctalEscapedKeysAsTheSameKey()
		{
			const string key = "NSUserTrackingUsageDescription";
			var existing = "\"NSUserTrackingUsageDescription\" = \"old A\";\n"
			               + "\"\\U004ESUserTrackingUsageDescription\" = \"old B\";\n"
			               + "\"\\116SUserTrackingUsageDescription\" = \"old C\";\n"
			               + "\"Title\" = \"\\U00e9t\\351\";\n";

			var merged = InfoPlistStrings.Set(existing, key, "new");

			Assert.That(merged, Is.EqualTo("\"NSUserTrackingUsageDescription\" = \"new\";\n\n\n\"Title\" = \"\\U00e9t\\351\";\n"),
				"only one entry for the key remains and other entries keep their original text");
		}

		[TestCase("\"A\" = \"b\"")]
		[TestCase("\"\\U12\" = \"b\";")]
		[TestCase("\"\\UZZZZ\" = \"b\";")]
		[TestCase("\"A\" \"b\";")]
		[TestCase("\"A\" = \"b;")]
		[TestCase("/* open")]
		[TestCase("bplist00")]
		public void InfoPlistStrings_RejectsUnreadableText(string existing)
		{
			Assert.Throws<System.FormatException>(() => InfoPlistStrings.Set(existing, "NSUserTrackingUsageDescription", "x"));
		}

		[Test]
		public void InfoPlistStrings_EscapesBackslashesQuotesAndNewlines()
		{
			Assert.That(InfoPlistStrings.Escape("a\\b\"c\r\nd\ne"), Is.EqualTo("a\\\\b\\\"c\\nd\\ne"));
		}
	}
}
