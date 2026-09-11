using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;

using IcedAstroGrep.Core.Logging;

namespace IcedAstroGrep.Core.EncodingDetection.Caching
{
   /// <summary>
   /// Provides the ability to cache file encodings and save/load them to disk.  
   /// Each performance setting is a separate cache file.
   /// </summary>
   /// <remarks>
   ///   IcedAstroGrep File Searching Utility. Written by Theodore L. Ward
   ///   Copyright (C) 2002 AstroComma Incorporated.
   ///   
   ///   This program is free software; you can redistribute it and/or
   ///   modify it under the terms of the GNU General Public License
   ///   as published by the Free Software Foundation; either version 2
   ///   of the License, or (at your option) any later version.
   ///   
   ///   This program is distributed in the hope that it will be useful,
   ///   but WITHOUT ANY WARRANTY; without even the implied warranty of
   ///   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
   ///   GNU General Public License for more details.
   ///   
   ///   You should have received a copy of the GNU General Public License
   ///   along with this program; if not, write to the Free Software
   ///   Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
   /// 
   ///   The author may be contacted at:
   ///   ted@astrocomma.com or curtismbeard@gmail.com
   /// </remarks>
   /// <history>
   /// [Curtis_Beard]		05/28/2015	FIX: 69, Created for speed improvements for encoding detection
   /// </history>
   public class EncodingCache
   {
      private Dictionary<string, EncodingCacheItem> cache = null;
      private LinkedList<string> lruList = null;

      /// <summary>
      /// Maps a cache key to its node in <see cref="lruList"/> so recency updates and removals stay O(1)
      /// and can never drift out of sync with the keys that actually exist in <see cref="cache"/>.
      /// </summary>
      private readonly Dictionary<string, LinkedListNode<string>> lruNodes = new Dictionary<string, LinkedListNode<string>>();

      private EncodingOptions.Performance currentPerformance = EncodingOptions.Performance.Default;
      private static readonly Lazy<EncodingCache> instance = new Lazy<EncodingCache>(() => new EncodingCache(), LazyThreadSafetyMode.ExecutionAndPublication);
      private const int capacity = 150000;

      /// <summary>
      /// Guards <see cref="cache"/>, <see cref="lruList"/> and <see cref="lruNodes"/>.
      /// The cache is process wide and is read/written from the search background thread.
      /// </summary>
      private readonly object syncRoot = new object();

      private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
      {
         IncludeFields = true
      };

      /// <summary>
      /// Retrieves the current instance of the EncodingCache.
      /// </summary>
      /// <history>
      /// [Curtis_Beard]		05/28/2015	FIX: 69, Created for speed improvements for encoding detection
      /// </history>
      public static EncodingCache Instance
      {
         get
         {
            return instance.Value;
         }
      }

      /// <summary>
      /// Creates a new instance of this class (private, use Instance property to access).
      /// </summary>
      /// <history>
      /// [Curtis_Beard]		05/28/2015	FIX: 69, Created for speed improvements for encoding detection
      /// </history>
      private EncodingCache()
      {
         cache = new Dictionary<string, EncodingCacheItem>(capacity);
         lruList = new LinkedList<string>();
      }

      /// <summary>
      /// Determines if given key is in cache.
      /// </summary>
      /// <param name="key">Key used for caching</param>
      /// <returns>true if found, false otherwise</returns>
      /// <history>
      /// [Curtis_Beard]		05/28/2015	FIX: 69, Created for speed improvements for encoding detection
      /// </history>
      public bool ContainsKey(string key)
      {
         lock (syncRoot)
         {
            return cache.ContainsKey(key);
         }
      }

      /// <summary>
      /// Gets a current item from the cache, null if not found.
      /// </summary>
      /// <param name="key">Key to item</param>
      /// <returns>EncodingCacheItem object if found, null otherwise</returns>
      /// <history>
      /// [Curtis_Beard]		05/28/2015	FIX: 69, Created for speed improvements for encoding detection
      /// </history>
      public EncodingCacheItem GetItem(string key)
      {
         lock (syncRoot)
         {
            if (cache.TryGetValue(key, out EncodingCacheItem item))
            {
               // mark as most recently used so eviction drops the real least recently used entry
               Touch(key);
               return item;
            }

            return null;
         }
      }

      /// <summary>
      /// Removes an EncodingCacheItem from the cache for the given key.
      /// </summary>
      /// <param name="key">Unique key</param>
      /// <history>
      /// [LinkNet]           05/25/2017	ADD: 97, Remove cache item for case when file encoding may have changed 
      /// </history>
      public void RemoveItem(string key)
      {
         lock (syncRoot)
         {
            // remove from the dictionary as well, otherwise the entry would be written back to disk
            // on the next Save and the LRU bookkeeping would drift away from the actual contents
            if (cache.Remove(key))
            {
               RemoveNode(key);
            }
         }
      }

      /// <summary>
      /// Sets an EncodingCacheItem in the cache for the given key.
      /// </summary>
      /// <param name="key">Unique key</param>
      /// <param name="item">EncodingCacheItem to add</param>
      /// <history>
      /// [Curtis_Beard]		05/28/2015	FIX: 69, Created for speed improvements for encoding detection
      /// </history>
      public void SetItem(string key, EncodingCacheItem item)
      {
         lock (syncRoot)
         {
            if (cache.ContainsKey(key))
            {
               // update item incase of change
               cache[key] = item;
            }
            else
            {
               cache.Add(key, item);

               // over capacity so remove the least recently used entries; driven off cache.Count so the
               // eviction can never walk off the end of an out of sync LRU list
               while (cache.Count > capacity && lruList.First != null)
               {
                  string leastUsedKey = lruList.First.Value;
                  lruList.RemoveFirst();
                  lruNodes.Remove(leastUsedKey);
                  cache.Remove(leastUsedKey);
               }
            }

            // move the key to the top of the LRU list
            Touch(key);
         }
      }

      /// <summary>
      /// Moves the given key to the most recently used position of the LRU list.
      /// The caller must hold <see cref="syncRoot"/>.
      /// </summary>
      /// <param name="key">Unique key</param>
      private void Touch(string key)
      {
         RemoveNode(key);
         lruNodes[key] = lruList.AddLast(key);
      }

      /// <summary>
      /// Removes the given key from the LRU list, leaving the dictionary untouched.
      /// The caller must hold <see cref="syncRoot"/>.
      /// </summary>
      /// <param name="key">Unique key</param>
      private void RemoveNode(string key)
      {
         if (lruNodes.TryGetValue(key, out LinkedListNode<string> node))
         {
            lruList.Remove(node);
            lruNodes.Remove(key);
         }
      }

      /// <summary>
      /// Empties the in-memory cache. The caller must hold <see cref="syncRoot"/>.
      /// </summary>
      private void ClearInternal()
      {
         cache.Clear();
         lruList.Clear();
         lruNodes.Clear();
      }

      /// <summary>
      /// Saves the current cache to disk with the given performanc setting.
      /// </summary>
      /// <param name="performanceSetting">Performance setting that should match the cache contents.</param>
      /// <history>
      /// [Curtis_Beard]		05/28/2015	FIX: 69, Created for speed improvements for encoding detection
      /// </history>
      public void Save(EncodingOptions.Performance performanceSetting)
      {
         string path = GetFilePath(performanceSetting);

         // take a snapshot under the lock so the (slow) disk write does not block a concurrent search
         Dictionary<string, EncodingCacheItem> snapshot;
         lock (syncRoot)
         {
            snapshot = new Dictionary<string, EncodingCacheItem>(cache);
         }

         LogClient.Instance.Logger.Info("Saving encoding cache [{0} items] to disk at {1}", snapshot.Count, path);

         try
         {
            FileInfo fInfo = new FileInfo(path);

            if (!fInfo.Directory.Exists)
            {
               fInfo.Directory.Create();
            }

            using (FileStream fs = new FileStream(path, FileMode.Create))
            {
               using (var deflate = new DeflateStream(fs, CompressionMode.Compress))
               {
                  JsonSerializer.Serialize(deflate, snapshot, JsonOptions);
               }
            }
         }
         catch (Exception ex)
         {
            LogClient.Instance.Logger.Error("Saving generic error: {0}", LogClient.GetAllExceptions(ex));
         }
      }

      /// <summary>
      /// Loads a cache from disk for the given performance setting.
      /// </summary>
      /// <param name="performanceSetting">Desired performance setting cache to load</param>
      /// <history>
      /// [Curtis_Beard]		05/28/2015	FIX: 69, Created for speed improvements for encoding detection
      /// [Curtis_Beard]		07/27/2015	FIX: 80, seek to beginning of stream
      /// </history>
      public void Load(EncodingOptions.Performance performanceSetting)
      {
         lock (syncRoot)
         {
            if (cache != null && cache.Count > 0 && performanceSetting == currentPerformance)
            {
               LogClient.Instance.Logger.Info("Encoding cache already loaded [{0} items]", cache.Count);
               return;
            }

            string path = GetFilePath(performanceSetting);
            LogClient.Instance.Logger.Info("Loading encoding cache from disk at {0}", path);

            try
            {
               ClearInternal();

               if (File.Exists(path))
               {
                  using (FileStream fs = new FileStream(path, FileMode.Open))
                  {
                     using (var deflate = new DeflateStream(fs, CompressionMode.Decompress))
                     {
                        var loaded = JsonSerializer.Deserialize<Dictionary<string, EncodingCacheItem>>(deflate, JsonOptions);
                        if (loaded != null)
                        {
                           cache = loaded;
                        }

                        foreach (var key in cache.Keys)
                        {
                           lruNodes[key] = lruList.AddLast(key);
                        }

                        currentPerformance = performanceSetting;

                        LogClient.Instance.Logger.Info("Encoding cache loaded successfully with {0} items", cache.Count);
                     }
                  }
               }
            }
            catch (Exception ex)
            {
               LogClient.Instance.Logger.Error("Loading generic error: {0}", LogClient.GetAllExceptions(ex));
            }
         }
      }

      /// <summary>
      /// Clears the current cache.
      /// </summary>
      /// <param name="deletePhysical">determines if all physical cache files should be deleted</param>
      /// <history>
      /// [Curtis_Beard]		05/28/2015	FIX: 69, Created for speed improvements for encoding detection
      /// </history>
      public void Clear(bool deletePhysical = false)
      {
         lock (syncRoot)
         {
            ClearInternal();
         }

         if (deletePhysical)
         {
            LogClient.Instance.Logger.Info("Deleting the physical encoding cache files");

            // delete all cache files by going through all values of enumeration
            Array values = Enum.GetValues(typeof(EncodingOptions.Performance));
            foreach (var val in values)
            {
               FileInfo fInfo = null;
               try
               {
                  fInfo = new FileInfo(GetFilePath((EncodingOptions.Performance)val));
                  if (fInfo.Exists)
                  {
                     fInfo.Delete();
                  }
               }
               catch (Exception ex)
               {
                  LogClient.Instance.Logger.Error("Error deleting cache file {0}: {1}", fInfo != null ? fInfo.FullName : "[unknown]", LogClient.GetAllExceptions(ex));
               }
            }
         }
      }

      /// <summary>
      /// Gets the file path for the given performance setting.
      /// </summary>
      /// <param name="performanceSetting">Desired performance setting</param>
      /// <returns></returns>
      /// <history>
      /// [Curtis_Beard]		05/28/2015	FIX: 69, Created for speed improvements for encoding detection
      /// </history>
      private static string GetFilePath(EncodingOptions.Performance performanceSetting)
      {
         return Path.Combine(ApplicationPaths.CacheDirectory, string.Format("encodings_{0}.cache", performanceSetting));
      }
   }
}
