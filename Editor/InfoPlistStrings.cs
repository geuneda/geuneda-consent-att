using System;
using System.Text;

// ReSharper disable once CheckNamespace

namespace Geuneda.Consent.Att.Editor
{
	/// <summary>
	/// <c>InfoPlist.strings</c> 텍스트에서 키 하나의 항목만 넣거나 바꿉니다. 주석, 따옴표 문자열의 이스케이프, 한 줄의 여러 항목, CRLF 를 구분하고
	/// 다른 항목과 주석은 그대로 둡니다. 같은 키가 여러 번 있으면 첫 자리에 하나만 남깁니다.
	/// </summary>
	internal static class InfoPlistStrings
	{
		/// <exception cref="FormatException">.strings 형식으로 읽을 수 없음(그대로 두면 키가 겹치거나 다른 항목이 깨지므로 빌드를 멈춘다)</exception>
		public static string Set(string existing, string key, string value)
		{
			var entry = $"\"{Escape(key)}\" = \"{Escape(value)}\";";

			if (string.IsNullOrEmpty(existing))
			{
				return entry + "\n";
			}

			var output = new StringBuilder(existing.Length + entry.Length + 1);
			var copiedUntil = 0;
			var replaced = false;
			var position = 0;

			while (true)
			{
				SkipTrivia(existing, ref position);

				if (position >= existing.Length)
				{
					break;
				}

				var start = position;
				var parsedKey = ReadString(existing, ref position);
				SkipTrivia(existing, ref position);
				Expect(existing, ref position, '=');
				SkipTrivia(existing, ref position);
				ReadString(existing, ref position);
				SkipTrivia(existing, ref position);
				Expect(existing, ref position, ';');

				if (parsedKey != key)
				{
					continue;
				}

				// 이 항목 앞까지 그대로 두고, 첫 항목은 새 값으로 바꾸고 나머지 같은 키는 지운다
				output.Append(existing, copiedUntil, start - copiedUntil);

				if (!replaced)
				{
					output.Append(entry);
					replaced = true;
				}

				copiedUntil = position;
			}

			output.Append(existing, copiedUntil, existing.Length - copiedUntil);

			if (replaced)
			{
				return output.ToString();
			}

			var text = output.ToString();
			var newLine = text.Contains("\r\n") ? "\r\n" : "\n";
			return text.EndsWith("\n") ? text + entry + newLine : text + newLine + entry + newLine;
		}

		/// <summary>.strings 따옴표 문자열 이스케이프(역슬래시, 따옴표, 줄바꿈)</summary>
		public static string Escape(string value)
		{
			return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\n");
		}

		// 공백, 줄바꿈, /* */ 와 // 주석을 건너뛴다
		private static void SkipTrivia(string text, ref int position)
		{
			while (position < text.Length)
			{
				if (char.IsWhiteSpace(text[position]) || text[position] == '\uFEFF')
				{
					position++;
				}
				else if (At(text, position, "/*"))
				{
					var end = text.IndexOf("*/", position + 2, StringComparison.Ordinal);
					position = end < 0 ? throw new FormatException("Unterminated comment in InfoPlist.strings") : end + 2;
				}
				else if (At(text, position, "//"))
				{
					var end = text.IndexOf('\n', position + 2);
					position = end < 0 ? text.Length : end + 1;
				}
				else
				{
					return;
				}
			}
		}

		// 따옴표 문자열(이스케이프를 풀어 돌려줌) 또는 따옴표 없는 단어
		private static string ReadString(string text, ref int position)
		{
			if (position >= text.Length)
			{
				throw new FormatException("Unexpected end of InfoPlist.strings");
			}

			var value = new StringBuilder();

			if (text[position] != '"')
			{
				while (position < text.Length && (char.IsLetterOrDigit(text[position]) || "_.$:/-".IndexOf(text[position]) >= 0))
				{
					value.Append(text[position++]);
				}

				return value.Length > 0 ? value.ToString() : throw new FormatException($"Unexpected '{text[position]}' in InfoPlist.strings");
			}

			position++;

			while (position < text.Length)
			{
				var c = text[position++];

				if (c == '"')
				{
					return value.ToString();
				}

				if (c == '\\' && position < text.Length)
				{
					value.Append(ReadEscape(text, ref position));
					continue;
				}

				value.Append(c);
			}

			throw new FormatException("Unterminated string in InfoPlist.strings");
		}

		// 역슬래시 뒤를 푼다(OpenStep 규칙): \U 와 16진수 4자리, 8진수 1~3자리, 제어 문자, 그 밖은 그 문자 자체.
		// 같은 키를 다른 표기로 쓴 항목도 같은 키로 보기 위해서다
		private static char ReadEscape(string text, ref int position)
		{
			var escaped = text[position++];

			switch (escaped)
			{
				case 'n':
					return '\n';
				case 't':
					return '\t';
				case 'r':
					return '\r';
				case 'a':
					return '\a';
				case 'b':
					return '\b';
				case 'f':
					return '\f';
				case 'v':
					return '\v';
				case 'U':
				case 'u':
					if (position + 4 > text.Length || !int.TryParse(text.Substring(position, 4), System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out var code))
					{
						throw new FormatException($"Invalid \\U escape in InfoPlist.strings at {position}");
					}

					position += 4;
					return (char) code;
			}

			if (escaped < '0' || escaped > '7')
			{
				return escaped;
			}

			var octal = escaped - '0';

			for (var digits = 1; digits < 3 && position < text.Length && text[position] >= '0' && text[position] <= '7'; digits++)
			{
				octal = octal * 8 + (text[position++] - '0');
			}

			return (char) octal;
		}

		private static void Expect(string text, ref int position, char expected)
		{
			if (position >= text.Length || text[position] != expected)
			{
				throw new FormatException($"Expected '{expected}' in InfoPlist.strings at {position}");
			}

			position++;
		}

		private static bool At(string text, int position, string token)
		{
			return string.CompareOrdinal(text, position, token, 0, token.Length) == 0;
		}
	}
}
