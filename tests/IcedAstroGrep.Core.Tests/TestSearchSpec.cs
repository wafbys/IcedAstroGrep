using System.Collections.Generic;
using IcedAstroGrep.Core;
using IcedAstroGrep.Core.EncodingDetection;

namespace IcedAstroGrep.Core.Tests
{
	internal sealed class TestSearchSpec : ISearchSpec
	{
		public string[] StartDirectories { get; set; }
		public string[] StartFilePaths { get; set; }
		public bool SearchInSubfolders { get; set; }
		public bool UseRegularExpressions { get; set; }
		public bool UseCaseSensitivity { get; set; }
		public bool UseWholeWordMatching { get; set; }
		public bool UseNegation { get; set; }
		public int ContextLines { get; set; }
		public string SearchText { get; set; }
		public bool ReturnOnlyFileNames { get; set; }
		public List<FileEncoding> FileEncodings { get; set; }
		public EncodingOptions EncodingDetectionOptions { get; set; } = new EncodingOptions(false) { UseEncodingCache = false };
		public string FileFilter { get; set; } = "*.txt";
		public int LongLineCharCount { get; set; }
		public int BeforeAfterCharCount { get; set; }
		public List<FilterItem> FilterItems { get; set; }
	}
}
