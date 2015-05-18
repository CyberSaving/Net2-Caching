using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace CyberSaving.Caching.Net2 {
    /// <summary>Define indexed multiple cache in memory
	/// <para>
	///		<c>Diagnostics.BooleanSwitch = "CyberSaving.MultipleCache.Trace.Active"</c>
	///		<c>Diagnostics.TraceSwitch = "CyberSaving.MultipleCache.Trace.Level"</c>
	/// </para></summary>
	/// <remarks>[Threadsafe] It garanteed by <c>ReaderWriterLock</c></remarks>
	/// <seealso cref="Cache"/>
	/// <seealso cref="ReaderWriterLock"/>
	public sealed class MultipleCache<TValue>
    {
        #region Fields
		/// <summary>The class singleton of class <see cref="MultipleCache"/>.</summary>
        public static readonly MultipleCache<Object> Singleton = new MultipleCache<Object>();
        /// <summary>Internal cache collection</summary>
        private Dictionary<string, Cache<string, TValue>> _caches;
        /// <summary>Lock for syncronize access to the collection</summary>
        private ReaderWriterLock _lock;
		/// <summary>Cache's maintenance</summary>
        private MultipleMaintenance _maintenance = null;

        /// <summary>Enable and disable tracing.</summary>
        private bool _tracing = false;
        private System.Diagnostics.TraceSwitch _traceswitchlevel = null;
        private int _deadLocks = 0;
        private int _defaltTimeout = 5; //5 sec;
        #endregion

        MultipleCache(){
            _caches = new Dictionary<string, Cache<string, TValue>>();
            _lock = new ReaderWriterLock();
            _maintenance = new MultipleMaintenance(this);

			_tracing = new System.Diagnostics.BooleanSwitch("CyberSaving.MultipleCache.Trace.Active"
				, "CyberSaving.MultipleCache.Trace.Active", "0").Enabled;
            if(_tracing){
				_traceswitchlevel = new System.Diagnostics.TraceSwitch("CyberSaving.MultipleCache.Trace.Level"
					, "CyberSaving.MultipleCache.Trace.Level", "info");
            }

        }

        #region Enumerators
        public IEnumerable<Cache<string, TValue>> ReadAll()
        {
            _lock.AcquireReaderLock(Constants.CacheReadTimeout);
            try {
                   foreach (var item in _caches.Values)
                        yield return item;
            }
            finally
            {
                _lock.ReleaseReaderLock();
            }
            yield break;
        }
        public IEnumerable<string> ClassKeys()
        {
            _lock.AcquireReaderLock(Constants.CacheReadTimeout);
            try
            {
                foreach (string cacheitem in _caches.Keys)
                {
                    yield return cacheitem;
                }
            }
            finally
            {
                _lock.ReleaseReaderLock();
            }
            yield break;
        }
        public IEnumerable<KeyValuePair<string, Cache<string,TValue>>> Classes()
        {
            _lock.AcquireReaderLock(Constants.CacheReadTimeout);
            try
            {
                foreach (var classcache in _caches)
                {
                    yield return classcache;
                }
            }
            finally
            {
                _lock.ReleaseReaderLock();
            }
            yield break;
        }
        #endregion

        #region Properties
        /// <summary>return number of deadlock so far</summary>
        public int DeadLocks { get { return _deadLocks; } }

		/// <summary>Insert or retrieve the item specified by the key. The item is handled in the default cache</summary>
        /// <paramref name="key">key.</paramref>
        public TValue this[string key]
        {
            get { return Get(null, key); }
            set { Insert(Constants.MainClassCache, key, value, _defaltTimeout); }
        }
        
        /// <paramref name="key">key.</paramref>
        /// <paramref name="timeout">Timeout in milliseconds.</paramref>
        public TValue this[string key, int timeout]
        {
            set { Insert(Constants.MainClassCache, key, value, timeout); }
        }

		/// <summary>Insert or retrieve the item specified by the key. The item is handled in the specify cache</summary>
        /// <paramref name="classKey">Class key.</paramref>
        /// <paramref name="key">Key</paramref>
        public TValue this[string classKey, string key]
        {
            get { return Get(classKey, key); }
            set { Insert(classKey, key, value, 0); }
        }

		/// <summary>Insert or retrieve the item specified by the key. The item is handled in the specify cache</summary>
        /// <paramref name="classKey">Class key</paramref>
        /// <paramref name="key">Key</paramref>
		/// <paramref name="timeout">Timeout in milliseconds.</paramref>
        public TValue this[string classKey, string key, int timeout]
        {
            set { Insert(classKey, key, value, timeout); }
        }
        #endregion

        #region Methods
        /// <summary>Clear all caches</summary>
		/// <seealso cref="Cache.Clear()"/>
        public void ClearAll()
        {
            string classKey = "NOKEY";
            try
            {
                this._maintenance.Stop();
                _lock.AcquireWriterLock(Timeout.Infinite);
                try
                {
                    foreach (var cacheitem in _caches)
                    {
                        classKey = cacheitem.Key;
                        TraceInfo("Clearing cache {0} ", classKey);
                        var cache = cacheitem.Value;
                        cache.Clear();
                    }

                    _caches.Clear();
                    TraceInfo("Cleared!");
                }
                finally
                {
                    _lock.ReleaseWriterLock();
                }

            }
            catch (ApplicationException ae)
            {
                regLock(classKey, ae);
                TraceWarn("Deadlock while clearing classKey [{0}] ", classKey);
            }
        }

		/// <summary>Clear a cache</summary>
		/// <param name="classKey"> Class key</param>
		/// <seealso cref="Cache.Clear()"/>
        public void Clear(string classKey)
        {
            TraceDebug("Clearing classKey {0}", classKey);
            if (classKey == null)
                classKey = Constants.MainClassCache;

            Cache<string, TValue> toLookup;
            if (get(classKey, out toLookup) && toLookup!=null)
            {
                toLookup.Clear();
                TraceInfo("Cleared classKey {0}", classKey);
            }
            else
            {
                TraceInfo("Not Cleared classKey {0}", classKey);
            }
           
        }

		/// <summary>Clear default cache</summary>
		/// <seealso cref="Cache.Clear()"/>
		public void Clear()
        {
            Clear(null);
        }

        /// <summary>
		/// Removes the specified item from the default cache.
        /// </summary>
        /// <param name="key">key</param>
		/// <returns>Old value if found, otherwise <c>null</c> or <c>default</c></returns>
        public TValue Remove(string key)
        {
            return Remove(null, key);
        }
        /// <summary>
		/// Removes the specified item from the specific cache.
        /// </summary>
        /// <overloads>Remove</overloads>
        /// <param name="classKey">Class key</param>
        /// <param name="key">Key</param>
		/// <returns>Old value if found, otherwise <c>null</c> or <c>default</c></returns>
        public TValue Remove(string classKey, string key)
        {
            TraceDebug("Remove classKey [{0}] key {1}", classKey, key);
            if (classKey == null)
                classKey = Constants.MainClassCache; ;
            if (key == null)
                throw new ArgumentNullException("key");
            
            TValue retval = default(TValue);
            Cache<string, TValue> toLookup;

            if (get(classKey, out toLookup) && toLookup!=null)
            {
                retval = toLookup.Purge(key);
                TraceDebug("Removed classKey [{0}] key {1}!", classKey, key);
            }
            else
            {
                TraceDebug("no removed classKey [{0}] key {1}!", classKey, key);
            }
            
            return retval;
        }
        
        /// <summary>
        /// Get value from key class and its key
        /// </summary>
        /// <param name="classKey">Class key</param>
        /// <param name="key">Key</param>
		/// <returns>Value if found, otherwise <c>null</c> or <c>default</c></returns>
        public TValue Get(string classKey, string key)
        {
            
            if (key == null)
                throw new ArgumentNullException("key");

            if (classKey == null)
                classKey = Constants.MainClassCache;
            TValue ret;
            var _classchache = get(classKey);
            if (_classchache == null || !_classchache.Get(key, out ret))
                ret = default(TValue);
            
            return ret;
        }
        /// <summary>
		///  Put value from key class and its key.
        /// </summary>
        /// <param name="classKey">Class key</param>
        /// <param name="key">key</param>
        /// <param name="value">static value.</param>
        /// <param name="timeout">Cache timeout</param>
        public void Insert(string classKey, string key, TValue value, int timeout)
        {
            TraceDebug("Insert classKey [{0}] key:{1} timeout:{0}", classKey, key, timeout);
            if (classKey == null)
                throw new ArgumentNullException("className");
            if (key == null)
                throw new ArgumentNullException("key");

            var _classchache = get(classKey);
            if (_classchache != null)
                 _classchache.Put(key, value, timeout);
            TraceDebug("Inserted classKey [{0}] key:{1} timeout:{0}", classKey,key, timeout);
        }

        public GetSetStatus GetSet(string key, out TValue ret, Func<TValue> OnMiss)
        {
            return GetSet(null, key, 0, out ret, OnMiss);
        }
        public GetSetStatus GetSet(string key, out TValue ret, int timeout, Func<TValue> OnMiss)
        {
            return GetSet(null, key, timeout, out ret, OnMiss);
        }
        public GetSetStatus GetSet(string classKey, string key, out TValue ret, Func<TValue> OnMiss)
        {
            return GetSet(classKey, key, 0, out ret, OnMiss);
        }
        public GetSetStatus GetSet(string classKey, string key, int timeout, out TValue ret, Func<TValue> OnMiss)
        {
            if (key == null)
                throw new ArgumentNullException("key");

            if (classKey == null)
                classKey = Constants.MainClassCache;

            var _classchache = get(classKey);
            if (_classchache != null)
                return _classchache.GetSet(key, out ret, timeout, OnMiss);

            ret = default(TValue);  
            return GetSetStatus.UnCached;
        }

		public Cache<string, TValue> GetClass(string classKey)
		{
			Cache<string, TValue> retval = null;
			get(classKey, out retval);
			return retval;
		}
        Cache<string, TValue> get(string classKey)
        {
            TraceDebug("Forcing Getting classKey [{0}]", classKey);
            Cache<string, TValue> retval = null;
            try
            {
                _lock.AcquireReaderLock(Constants.CacheReadTimeout * 2);
                try
                {
                    if (!_caches.TryGetValue(classKey, out retval))
                    {
                        var cooki = _lock.UpgradeToWriterLock(Constants.CacheWriteTimeout * 2);
                        try
                        {
                            if (!_caches.TryGetValue(classKey, out retval))
                            {
                                retval = new Cache<string, TValue>(
                                    StringComparer.CurrentCultureIgnoreCase
                                    , _maintenance);

                                _caches.Add(classKey, retval);
                                TraceInfo("Forcing Added classKey [{0}]", classKey);
                            }
                            else {
                                TraceInfo("Key [{0}] already present! Skipped", classKey);
                            }
                        }
                        finally
                        {
                            _lock.DowngradeFromWriterLock(ref cooki);
                        }
                    }
                    else
                    {
                        TraceDebug("Forcing Getting classKey [{0}] already present.", classKey);
                    }
                }
                finally
                {
                    _lock.ReleaseReaderLock();
                }
            }
            catch (ApplicationException ae)
            {
                regLock(classKey, ae);
                TraceWarn("Deadlocked while getting classKey [{0}].", classKey);

            }

            TraceDebug("Forcing Getted classKey [{0}]", classKey);
            return retval;

        }
        /// <summary>Internal getter.</summary>
        /// <remarks>Acquiring read lock</remarks>
        /// <param name="classKey">class key</param>
		/// <param name="retval">output cache item if present</param>
        /// <returns>if there is a cache</returns>
		bool get(string classKey, out Cache<string, TValue> retval)
        {
            TraceDebug("Getting classKey [{0}]", classKey);
			retval = null;
            try {  
                _lock.AcquireReaderLock(Constants.CacheReadTimeout);
                try
                {
                    return _caches.TryGetValue(classKey, out retval);
                }
                finally
                {
                    _lock.ReleaseReaderLock();
                }
            } catch (ApplicationException ae) {
                TraceWarn("Deadlocked while getting classKey [{0}].", classKey);
                regLock(classKey, ae);
            } catch (Exception ex)
            {
                TraceErr("Deadlock in Getting classKey {0}", ex);
            }
			
            TraceDebug("Getted in classKey [{0}]", classKey);
            return false ;
            
        }

        void regLock(string classKey,Exception ex)
        {
            _deadLocks++;
            TraceWarn("Deadlock in Getting classKey {0}", classKey);
        }
        #endregion

        #region Trace
        /* DEBUG ------*/
        private void TraceDebugFormat(string format, params object[] args)
        {
            if (_tracing && _traceswitchlevel.Level == System.Diagnostics.TraceLevel.Verbose)
                System.Diagnostics.Trace.WriteLine(string.Format(format, args));
        }
        private void TraceDebug(string format, object arg1, object arg2, object arg3)
        {
            if (_tracing && _traceswitchlevel.Level == System.Diagnostics.TraceLevel.Verbose)
                System.Diagnostics.Trace.WriteLine(string.Format(format, arg1, arg2, arg3));
        }

        private void TraceDebug(string format, object arg1, object arg2)
        {
            if (_tracing && _traceswitchlevel.Level == System.Diagnostics.TraceLevel.Verbose)
                System.Diagnostics.Trace.WriteLine(string.Format(format, arg1, arg2));
        }
        private void TraceDebug(string format, object arg1)
        {
            if (_tracing && _traceswitchlevel.Level == System.Diagnostics.TraceLevel.Verbose)
                System.Diagnostics.Trace.WriteLine(string.Format(format, arg1));
        }
        private void TraceDebug(string message)
        {
            if (_tracing && _traceswitchlevel.Level == System.Diagnostics.TraceLevel.Verbose)
                System.Diagnostics.Trace.WriteLine(message);
        }
        /* INFO ------*/
        private void TraceInfo(string format, params object[] args)
        {
            if (_tracing && _traceswitchlevel.Level >= System.Diagnostics.TraceLevel.Info)
                System.Diagnostics.Trace.TraceInformation(format, args);
        }
        private void TraceInfo(string message)
        {
            if (_tracing && _traceswitchlevel.Level >= System.Diagnostics.TraceLevel.Info)
                System.Diagnostics.Trace.TraceInformation(message);
        }
        /* WARN ------*/
        private void TraceWarn(string format, params object[] args)
        {
            if (_tracing && _traceswitchlevel.Level >= System.Diagnostics.TraceLevel.Warning)
                System.Diagnostics.Trace.TraceWarning(format, args);
        }
        private void TraceWarn(string message)
        {
            if (_tracing && _traceswitchlevel.Level >= System.Diagnostics.TraceLevel.Warning)
                System.Diagnostics.Trace.TraceWarning(message);
        }
        /* ERROR ------*/
        private void TraceErr(string message)
        {
            if (_tracing && _traceswitchlevel.Level >= System.Diagnostics.TraceLevel.Error)
                System.Diagnostics.Trace.TraceError(message);
        }
        private void TraceErr(string message,Exception ex)
        {
            if (_tracing && _traceswitchlevel.Level >= System.Diagnostics.TraceLevel.Error)
            {
                if (string.IsNullOrEmpty(message))
                    System.Diagnostics.Trace.TraceError(string.Concat(message, ":", ex.Message, "\n", ex.StackTrace));
                else
                    System.Diagnostics.Trace.TraceError(ex.StackTrace);

            }
                
        }

        #endregion

        #region Nested Types

        private class MultipleMaintenance: CacheMaintenance<string, TValue>
        {
            
            MultipleCache<TValue> _multipleCache;
            StatisticsUnitInt _stats = new StatisticsUnitInt();
            
            public MultipleMaintenance(MultipleCache<TValue> multipleCache)
                : base(null)
            {
                _multipleCache = multipleCache;

            }

            public void Notify(CacheItem<string, TValue> cacheitem)
            {
                _multipleCache.TraceDebug("Maintenance Tick key:{0} timeout:{1}", cacheitem.Key,cacheitem.Timeout);
                if (cacheitem.Timeout > 0 && base.timer !=null)
                    base.Start(cacheitem.Timeout);
            }
            public override void Tick(object obj)
            {
                _multipleCache.TraceInfo("Maintenance Tick");
                string curerntCacheKey = "NOKEY";
                try
                {
                    _multipleCache._lock.AcquireReaderLock(Constants.CacheReadTimeout);
                    try
                    {
                        _stats.ReSet();

                        foreach (var cacheitem in _multipleCache._caches)
                        {
                            var cache = cacheitem.Value;
                            curerntCacheKey = cacheitem.Key;
                            
                            _multipleCache.TraceInfo("Purging key:{0} N=[{1} of {2}]", curerntCacheKey
                                , cache.HeuristicCount, cache.Capacity);
                            cache.Purge();
                            _multipleCache.TraceInfo("Purged key:{0} N={1}", curerntCacheKey
                                , cache.HeuristicCount);

                            if (_multipleCache._tracing 
                                && _multipleCache._traceswitchlevel.Level >= System.Diagnostics.TraceLevel.Info  )
                            {
                                _multipleCache.TraceInfo("** 5 Worst Of Hits of {0}", curerntCacheKey);
                                foreach (var citem in cache.WorstOfHits(5))
                                    _multipleCache.TraceInfo("{0}", citem);
                                _multipleCache.TraceInfo("** 5 Worst Of Ratio of {0}", curerntCacheKey);
                                foreach (var citem in cache.WorstOfRatio(5))
                                    _multipleCache.TraceInfo("{0}", citem);
                                _multipleCache.TraceInfo("** 5 Best Of Hits of {0}", curerntCacheKey);
                                foreach (var citem in cache.BestOfHits(5))
                                    _multipleCache.TraceInfo("{0}", citem);
                                _multipleCache.TraceInfo("** 5 Best Of Ratio of {0}", curerntCacheKey);
                                foreach (var citem in cache.BestOfRatio(5))
                                    _multipleCache.TraceInfo("{0}", citem);
                                _stats.Merge(cache.TimeoutStats);
                            }
                        }
                    }
                    finally
                    {
                        _multipleCache._lock.ReleaseReaderLock();
                    }
                }
                catch (ThreadAbortException)
                {
                    _multipleCache.TraceInfo("Maintenance Tick Aborted!");
                }
                catch (ApplicationException ae)
                {
                    _multipleCache.regLock(curerntCacheKey, ae);
                }
                finally
                {
                    base.Tick(obj);
                }

            }
            protected override void ResetTime()
            {
                if (_stats.MaxValue > 0)
                {
                    TimerMillisecond =
                        Math.Max(Constants.CacheMinimumPurge * 1000, (int)(_stats.Avg * 1000));
                    _multipleCache.TraceInfo("Next tick = {0} sec!", (long)(TimerMillisecond/1000));
                }
            }
        }
        #endregion
    }

}