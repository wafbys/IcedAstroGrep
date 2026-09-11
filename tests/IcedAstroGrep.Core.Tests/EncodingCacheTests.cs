using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

using IcedAstroGrep.Core.EncodingDetection.Caching;

using Xunit;

namespace IcedAstroGrep.Core.Tests
{
	/// <summary>
	/// The encoding cache is shared by every search and was the source of a data structure that
	/// drifted out of sync with its own LRU list, so its contract is pinned here.
	/// </summary>
	public class EncodingCacheTests
	{
		private static EncodingCacheItem Item(long size)
		{
			return new EncodingCacheItem { CodePage = 65001, DetectorName = "test", FileSize = size };
		}

		[Fact]
		public void RemoveItem_RemovesTheEntryFromTheCache()
		{
			var cache = EncodingCache.Instance;
			string key = "remove-" + Guid.NewGuid().ToString("N");

			cache.SetItem(key, Item(1));
			Assert.True(cache.ContainsKey(key));
			Assert.NotNull(cache.GetItem(key));

			cache.RemoveItem(key);

			Assert.False(cache.ContainsKey(key));
			Assert.Null(cache.GetItem(key));
		}

		[Fact]
		public void RemovingThenReAddingAnEntryKeepsTheCacheUsable()
		{
			var cache = EncodingCache.Instance;
			string key = "readd-" + Guid.NewGuid().ToString("N");

			cache.SetItem(key, Item(1));
			cache.RemoveItem(key);
			cache.SetItem(key, Item(2));

			Assert.True(cache.ContainsKey(key));
			Assert.Equal(2, cache.GetItem(key).FileSize);
		}

		[Fact]
		public void Eviction_StaysConsistentWithTheCacheContents()
		{
			var cache = EncodingCache.Instance;

			FieldInfo capacityField = typeof(EncodingCache).GetField("capacity", BindingFlags.NonPublic | BindingFlags.Static);
			Assert.NotNull(capacityField);
			int capacity = (int)capacityField.GetValue(null);

			cache.Clear();

			string prefix = "evict-" + Guid.NewGuid().ToString("N") + "-";
			for (int i = 0; i < capacity + 50; i++)
			{
				cache.SetItem(prefix + i, Item(i));
			}

			// the oldest entries are evicted, the newest survive, and eviction never walks off the
			// end of the LRU list (which used to throw once it drifted from the dictionary)
			Assert.False(cache.ContainsKey(prefix + 0));
			Assert.True(cache.ContainsKey(prefix + (capacity + 49)));

			// removing entries and adding more must keep working
			for (int i = 0; i < 200; i++)
			{
				cache.RemoveItem(prefix + (capacity / 2 + i));
			}

			for (int i = 0; i < 200; i++)
			{
				cache.SetItem(prefix + "extra-" + i, Item(i));
			}

			Assert.True(cache.ContainsKey(prefix + "extra-199"));

			cache.Clear();
		}

		[Fact]
		public void ConcurrentUse_DoesNotThrow()
		{
			var cache = EncodingCache.Instance;
			cache.Clear();

			var errors = new List<Exception>();
			var threads = new List<Thread>();

			for (int t = 0; t < 4; t++)
			{
				var thread = new Thread(() =>
				{
					try
					{
						for (int i = 0; i < 5000; i++)
						{
							string key = "concurrent-" + (i % 250);
							cache.SetItem(key, Item(i));
							cache.GetItem(key);
							if (i % 7 == 0)
							{
								cache.RemoveItem(key);
							}

							cache.ContainsKey(key);
						}
					}
					catch (Exception ex)
					{
						lock (errors)
						{
							errors.Add(ex);
						}
					}
				});

				threads.Add(thread);
				thread.Start();
			}

			foreach (var thread in threads)
			{
				thread.Join();
			}

			Assert.Empty(errors);
			cache.Clear();
		}
	}
}
