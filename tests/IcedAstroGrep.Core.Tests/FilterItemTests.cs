using System;
using System.Collections.Generic;
using System.Linq;

using Xunit;

namespace IcedAstroGrep.Core.Tests
{
	public class FilterItemTests
	{
		private static FilterItem CreateItem(string value, string sizeOption = "")
		{
			return new FilterItem(new FilterType(FilterType.Categories.File, FilterType.SubCategories.Name), value, FilterType.ValueOptions.None, false, sizeOption, true);
		}

		/// <summary>
		/// Values that used to corrupt the serialized form: the field separator '|', the list
		/// separator '<' and backslashes (which must survive untouched, see the legacy tests).
		/// </summary>
		[Theory]
		[InlineData("a|b")]
		[InlineData("a<b")]
		[InlineData("regex(a|b)<x>")]
		[InlineData("^(foo|bar)$")]
		[InlineData("C:\\Windows\\")]
		[InlineData("\\\\server\\share")]
		[InlineData("a\\|b")]
		[InlineData("a\\\\|b")]
		[InlineData("a\\<b")]
		[InlineData("trailing\\")]
		[InlineData("")]
		public void Value_SurvivesRoundTrip(string value)
		{
			var parsed = FilterItem.FromString(CreateItem(value).ToString());

			Assert.Equal(value, parsed.Value);
		}

		[Fact]
		public void SizeOption_SurvivesRoundTrip()
		{
			var parsed = FilterItem.FromString(CreateItem("412", "K|B").ToString());

			Assert.Equal("412", parsed.Value);
			Assert.Equal("K|B", parsed.ValueSizeOption);
		}

		[Fact]
		public void AllFields_SurviveRoundTrip()
		{
			var original = new FilterItem(new FilterType(FilterType.Categories.Directory, FilterType.SubCategories.Path), "a|b", FilterType.ValueOptions.Equals, true, "MB", false);

			var parsed = FilterItem.FromString(original.ToString());

			Assert.Equal(original.FilterType.ToString(), parsed.FilterType.ToString());
			Assert.Equal(original.Value, parsed.Value);
			Assert.Equal(original.ValueOption, parsed.ValueOption);
			Assert.Equal(original.ValueIgnoreCase, parsed.ValueIgnoreCase);
			Assert.Equal(original.ValueSizeOption, parsed.ValueSizeOption);
			Assert.Equal(original.Enabled, parsed.Enabled);
		}

		[Theory]
		[InlineData("a|b")]
		[InlineData("a<b")]
		[InlineData("\\\\host\\share\\dir")]
		public void List_SurvivesRoundTrip(string value)
		{
			var list = new List<FilterItem> { CreateItem("*.log"), CreateItem(value), CreateItem("C:\\temp") };

			var parsed = FilterItem.ConvertStringToFilterItems(FilterItem.ConvertFilterItemsToString(list));

			Assert.Equal(3, parsed.Count);
			Assert.Equal("*.log", parsed[0].Value);
			Assert.Equal(value, parsed[1].Value);
			Assert.Equal("C:\\temp", parsed[2].Value);
		}

		[Fact]
		public void NewItems_AreMarkedWithTheFormatPrefix()
		{
			string serialized = CreateItem("a|b<c").ToString();

			Assert.StartsWith("~v2~", serialized);

			// the literal '<' must always carry its escape character, otherwise the list split
			// would cut the item in half
			int index = serialized.IndexOf('<');
			while (index >= 0)
			{
				Assert.True(index > 0 && serialized[index - 1] == '\\', "unescaped list separator in: " + serialized);
				index = serialized.IndexOf('<', index + 1);
			}
		}

		[Fact]
		public void LegacyItem_WithoutPrefix_StillParses()
		{
			// format written before escaping existed
			var parsed = FilterItem.FromString("File^Extension|*.txt|None|False||True");

			Assert.Equal("*.txt", parsed.Value);
			Assert.Equal(string.Empty, parsed.ValueSizeOption);
			Assert.True(parsed.Enabled);
		}

		[Fact]
		public void LegacyValue_WithDoubleBackslash_IsNotUnescaped()
		{
			var parsed = FilterItem.FromString("File^Path|\\\\server\\share|None|False||True");

			Assert.Equal("\\\\server\\share", parsed.Value);
		}

		[Fact]
		public void LegacyList_StillSplitsOnEverySeparator()
		{
			var parsed = FilterItem.ConvertStringToFilterItems("File^Extension|*.txt|None|False||True<File^Extension|*.log|None|False||True");

			Assert.Equal(2, parsed.Count);
			Assert.Equal("*.log", parsed[1].Value);
		}

		[Fact]
		public void ExclusionLogDetails_StillSplitIntoTwoParts()
		{
			// frmMain stores exclusion log details as "<FilterItem>~~<filterValue>"
			string details = string.Format("{0}~~{1}", CreateItem("a|b").ToString(), "filtered-value");

			var parts = details.Split(new[] { "~~" }, StringSplitOptions.None);

			Assert.Equal(2, parts.Length);
			Assert.Equal("a|b", FilterItem.FromString(parts[0]).Value);
		}

		[Fact]
		public void EmptyInputs_ProduceNoItems()
		{
			Assert.Equal(string.Empty, FilterItem.ConvertFilterItemsToString(null));
			Assert.Equal(string.Empty, FilterItem.ConvertFilterItemsToString(new List<FilterItem>()));
			Assert.Empty(FilterItem.ConvertStringToFilterItems(string.Empty));
		}

		[Fact]
		public void AnUnreadableSizeValueNeverMatchesInsteadOfThrowing()
		{
			using (var folder = new TempFolder())
			{
				string path = folder.Write("size.txt", "needle");

				// a size exclusion whose value is not a number used to throw FormatException, once per
				// file, for the whole search
				var item = new FilterItem(new FilterType(FilterType.Categories.File, FilterType.SubCategories.Size), "not-a-number", FilterType.ValueOptions.GreaterThan, false, true);

				Assert.False(item.ShouldExcludeFile(new System.IO.FileInfo(path), out _));
			}
		}

		[Fact]
		public void BinaryDetectionLooksPastTheFirstKilobyte()
		{
			using (var folder = new TempFolder())
			{
				// a kilobyte of text, then two NUL pairs at about 4 KB: the buffer used to be 1 KB while
				// the comment promised 10 KB, so this file was reported as text
				var content = new List<byte>(System.Text.Encoding.ASCII.GetBytes(new string('a', 4096)));
				content.AddRange(new byte[] { 0, 0, (byte)'b', (byte)'c', 0, 0 });

				string path = System.IO.Path.Combine(folder.Path, "binary.bin");
				System.IO.File.WriteAllBytes(path, content.ToArray());

				Assert.True(FilterItem.IsBinaryFile(new System.IO.FileInfo(path)));
			}
		}

		[Fact]
		public void PlainTextIsNotReportedAsBinary()
		{
			using (var folder = new TempFolder())
			{
				string path = folder.Write("text.txt", new string('a', 8192));

				Assert.False(FilterItem.IsBinaryFile(new System.IO.FileInfo(path)));
			}
		}
	}
}
