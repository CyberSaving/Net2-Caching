using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Serialization;

namespace CyberSaving.Caching.Net2 {
	/// <summary>
	/// <c>Cache</c> is the main caching mechanism to store persistently in memory a generic couple of key/value.
	/// All data will be indexes in <c>dictionary</c> and is ThreadSafe.
	/// </summary>
	/// <remarks>Threadsafe is garanteed by <see cref="ReaderWriterLockSlim"/>.</remarks>
	/// <include file='Cache.doc' path='//Cache/Class[@name="Cache"]/*'/>
	/// <typeparam name="TKey"></typeparam>
	/// <typeparam name="TValue"></typeparam>
    public class Cache<TKey, TValue> : IEnumerable<TValue>, IXmlSerializable
    {

        Dictionary<TKey, CacheItem<TKey,TValue>> _data = null;
        ReaderWriterLockSlim _lock;
        int _deadLocks = 0;
        int _heuristicCount = -1;
        readonly StatisticsUnitInt _timeoutStats = new StatisticsUnitInt();
        
		/// <summary>Maintenance algortm. If <c>null</c> use default;</summary>
        protected CacheMaintenance<TKey, TValue> _maintenance = null;

        #region properties
        /// <summary>return current timeout Stitistics</summary>
        public StatisticsUnitInt TimeoutStats { get { return _timeoutStats; } }
        /// <summary>return number of deadlock so far</summary>
        public int DeadLocks { get { return _deadLocks; } }
        
        /// <summary>return number of deadlock so far</summary>
        public int ThreadCurrentReadCount { get { return _lock.CurrentReadCount; } }
		/// <summary>return number of threads were waiting for Read</summary>
		public int ThreadWaitingReadCount { get { return _lock.WaitingReadCount; } }

		/// <summary>return number of threads were waiting for Upgrade</summary>
		public int ThreadWaitingUpgradeCount { get { return _lock.WaitingUpgradeCount; } }

		/// <summary>return number of threads were waiting for Write</summary>
		public int ThreadWaitingWriteCount { get { return _lock.WaitingWriteCount; } }

		/// <summaryCurrent maintenance algoritm</summary>
		public CacheMaintenance<TKey, TValue> Maintenance { get { return _maintenance; } }

		/// <summary>Phisical size of Dictionary (with starved and purged)</summary>
		public int Capacity { get { return (_data != null) ? _data.Count : -1; } }
		/// <summary>Number of valid items approximately</summary>
		public int HeuristicCount { get{return _heuristicCount;} }

        #endregion
        
        #region costructor
		/// <summary>Type specified with comparer algoritm and maintenance</summary>
		/// <param name="comparer">use for creating <c>Dictionary</c></param>
		/// <param name="maintenance">use to share maintenance algortms.</param>
        public Cache(IEqualityComparer<TKey> comparer, CacheMaintenance<TKey, TValue> maintenance)
        {
            _data = new Dictionary<TKey, CacheItem<TKey, TValue>>(comparer);
            _lock = new ReaderWriterLockSlim();
            _maintenance = maintenance;
        }
		/// <summary>Type specified with comparer algoritm</summary>
		/// <param name="comparer">use for creating <c>Dictionary</c></param>
        public Cache(IEqualityComparer<TKey> comparer)
        {
            _data = new Dictionary<TKey, CacheItem<TKey, TValue>>(comparer);
            _lock = new ReaderWriterLockSlim();
            _maintenance = null;
        }
		/// <summary>Default</summary>
        public Cache()
        {
            _data = new Dictionary<TKey, CacheItem<TKey, TValue>>();
            _lock = new ReaderWriterLockSlim();
            _maintenance = null;
        }



        #endregion

        #region Enumerators
		/// <summary>Get typed enumerator</summary>
		/// <returns></returns>
		/// <seealso cref="CacheEnumerator"/>
        IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator()
        {
            return new CacheEnumerator(this);
        }

		/// <summary>Get generic enumerator</summary>
		/// <returns></returns>
		/// <seealso cref="CacheEnumerator"/>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return new CacheEnumerator(this);
        }
        /// <summary>Get all internal items inside a read lock</summary>
        /// <remarks>[threadsafe]. If locked, deadlock will be increment</remarks>
        /// <returns>IEnumerable</returns>
        public IEnumerable<CacheItem<TKey, TValue>> ReadAll()
        {
            if (_lock.TryEnterReadLock(Constants.CacheReadTimeout))
                try
                {
                    foreach (var item in _data.Values)
                        yield return item;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            else
            {
                Interlocked.Increment(ref _deadLocks);
            }
            yield break;
        }
        #endregion
        
		/// <summary>Get a iten inside the cache</summary>
		/// <remarks>[threadsafe]. If locked, deadlock will be increment</remarks>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns><c>true</c> if the element was found</returns>
        public bool Get(TKey key, out TValue value)
        {
            value = default(TValue);
            if (_lock.TryEnterReadLock(Constants.CacheReadTimeout))
                try
                {
                    CacheItem<TKey, TValue> result;
                    if (_data.TryGetValue(key, out result))
                    {
                        if (result.IsStarved)
                        {
                            result.AddStHit();
                        }
                        else
                        {
                            result.AddRHit();
                            value = (TValue)result.Value;
                            return true;
                        }
                    }
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            else
            {
                Interlocked.Increment(ref _deadLocks);
            }
            return false;
        }

		/// <summary>Looking for the key inside the cahce. If not found OnMiss will be called and save into the cache</summary>
		/// <remarks>Item will not expire.
		/// [threadsafe]. If locked, deadlock will be increment
		/// </remarks>
		/// <param name="key">key for indexed item</param>
		/// <param name="value">returned value old value if found, otherwise new one</param>
		/// <param name="OnMiss">delegate will call on miss</param>
		/// <returns>Get-Set Status action. <see cref="GetSetStatus"/></returns>
		/// <seealso cref="GetSet(TKey,TValue,int,Func<TValue>)" />
        public GetSetStatus GetSet(TKey key, out TValue value, Func<TValue> OnMiss)
        {
            return GetSet(key, out value, 0, OnMiss);
        }

		/// <summary>>Looking for the key inside the cahce. If not found OnMiss will be called and save into the cache</summary>
		/// <remarks>Item will expire in <paramref name="secondsTimeout"/> seconds.
		/// [threadsafe]. If locked, deadlock will be increment
		/// </remarks>
		/// <param name="key">Key for indexed item</param>
		/// <param name="value">returned value old value if found, otherwise new one</param>
		/// <param name="secondsTimeout">Time to live in second</param>
		/// <param name="OnMiss">delegate will call on miss</param>
		/// <returns>Get-Set Status action. <see cref="GetSetStatus"/></returns>
        public GetSetStatus GetSet(TKey key, out TValue value, int secondsTimeout, Func<TValue> OnMiss)
        {
            GetSetStatus retval  = GetSetStatus.Unchanged;
            value = default(TValue);
            if(_lock.TryEnterUpgradeableReadLock(Constants.CacheReadTimeout))
                try
                {
                    CacheItem<TKey, TValue> result;
                    if (_data.TryGetValue(key, out result))
                    {
                        if (result.IsStarved)
                        {
                            result.AddStHit();
                            //making value
                            value = OnMiss();
                            updateAndInitCacheItem(result, value);
                            _heuristicCount++;
                            retval = GetSetStatus.Updated;
                        }
                        else
                        {
                            result.AddRHit();
                            value = (TValue)result.Value;
                            //Unchanged
                            retval = GetSetStatus.Unchanged;
                        }
                    }
                    else
                    {
                        //making value
                        value = OnMiss();
                        result = addAndInitCacheItem(key, value, secondsTimeout);
                        retval = GetSetStatus.Added;
                    }
                }
                finally
                {
                    _lock.ExitUpgradeableReadLock();
                }
            else
            {
                Interlocked.Increment(ref _deadLocks);
                //making value
                value = OnMiss();
                retval = GetSetStatus.UnCached;
            }
            return retval;
        }
		/// <summary>Add a item into the cache. No expire</summary>
		/// <param name="key">key</param>
		/// <param name="value">value</param>
		/// <returns><c>false</c> if deadlock.</returns>
        public bool Put(TKey key, TValue value)
        {
            return Put( key,  value,0);
        }
		/// <summary>Add a item into the cache. Expire in </summary>
		/// <param name="key">key</param>
		/// <param name="value">value</param>
		/// <param name="secondsTimeout">Time to live in second</param>
		/// <returns><c>false</c> if deadlock.</returns>
        public bool Put(TKey key, TValue value, int secondsTimeout)
        {
            if(_lock.TryEnterUpgradeableReadLock(Constants.CacheReadTimeout))
                try
                {
                    CacheItem<TKey, TValue> result;
                    if (_data.TryGetValue(key, out result))
                    {
                        updateAndInitCacheItem(result, value);
                    }
                    else
                    {
                        result = addAndInitCacheItem(key, value, secondsTimeout);
                    }
                    return true;
                }
                finally
                {
                    _lock.ExitUpgradeableReadLock();
                }
            else
            {
                Interlocked.Increment(ref _deadLocks);
                return false;
            }
        }

		/// <summary>Mark a item inside the cache as delated</summary>
		/// <remarks>increment heuristic Count</remarks>
		/// <param name="key">Key</param>
		/// <returns>Old value if found, otherwise <c>null</c> or <c>default</c></returns>
        public TValue Purge(TKey key)
        {
            TValue retval;
            CacheItem<TKey,TValue> retcitem;
            if (_lock.TryEnterReadLock(Constants.CacheReadTimeout))
            {
                try {
                    if (_data.TryGetValue(key, out retcitem))
                    {
                        lock (retcitem)
                        {
                            retval = retcitem.Value;
                            retcitem.Purge();
                        }
                        _heuristicCount--;
                    }
                    else
                    {
                        retval = default(TValue);
                    }
                }finally{
                    _lock.ExitReadLock();
                }
            }
            else { Interlocked.Increment(ref _deadLocks); retval = default(TValue); }
            
            return retval;
        }

		/// <summary>Remove all data and all statistic</summary>
        public void Clear()
        {
             _lock.EnterWriteLock();
            try
            {
				//reset timer's statistic 
                _timeoutStats.ReSet();
                _data.Clear();
                _heuristicCount = 0;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>Purge all cached items that are starved, include all statistics</summary>
        public void Trim()
        {
            forAllStarved(true,(v) => _data.Remove(v.Key));
        }
		/// <summary>Purge all cached items that are starved</summary>
        public void Purge()
        {
            forAllStarved(false, (v) => { lock (v) { v.Purge(); } });
        }
        /// <summary>Force Count all items that aren't starved</summary>
        /// <returns>-1 if deadlock occur</returns>
        public int Count()
        {
            if (_lock.TryEnterReadLock(Constants.CacheReadTimeout))
                try
                {
                    int newHeuristicCount = 0;    
                    foreach (var item in _data.Values)
                        if (!item.IsStarved)
                            newHeuristicCount++;
                    _heuristicCount = newHeuristicCount;
                    return newHeuristicCount;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            else
            {
                Interlocked.Increment(ref _deadLocks);
            }
            return -1;
        }

		/// <summary>Retrieve worst <paramref name="top"/> elements by Hits</summary>
        /// <param name="top"></param>
        /// <returns></returns>
        public IList<CacheItem<TKey,TValue>> WorstOfHits(int top){
            return Search(top,true,CacheItemComparer<TKey,TValue>.ByHits);
        }
		/// <summary>Retrieve best <paramref name="top"/> elements by Hits</summary>
		/// <param name="top"></param>
		/// <returns></returns>
        public IList<CacheItem<TKey, TValue>> BestOfHits(int top)
        {
            return Search(top, false, CacheItemComparer<TKey, TValue>.ByHits);
        }
		/// <summary>Retrieve worst <paramref name="top"/> elements by ratio</summary>
		/// <param name="top"></param>
		/// <returns></returns>
        public IList<CacheItem<TKey, TValue>> WorstOfRatio(int top)
        {
            return Search(top, true, CacheItemComparer<TKey, TValue>.ByRatio);
        }
		/// <summary>Retrieve best <paramref name="top"/> elements by ratio</summary>
		/// <param name="top"></param>
		/// <returns></returns>
        public IList<CacheItem<TKey, TValue>> BestOfRatio(int top)
        {
            return Search(top, false, CacheItemComparer<TKey, TValue>.ByRatio);
        }
        
		/// <summary>Dump all properties of cache</summary>
		/// <returns></returns>
        public override string ToString()
        {
            int _h = this.HeuristicCount;
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Count [H:{0}/C:{1}]", _h, this.Capacity);
            var stats = this.TimeoutStats;
            sb.AppendFormat("\tStats [c:{0} avg:{1}]\tDeadLock {2}", stats.count, stats.Avg,_deadLocks);
            sb.AppendFormat("\tth [Now:{0} wait R:{1} U:{2} W:{3}]", this.ThreadCurrentReadCount
                , this.ThreadWaitingReadCount
                , this.ThreadWaitingUpgradeCount
                , this.ThreadWaitingWriteCount
                );
            sb.AppendFormat("\tMnt: Every {2} msec [last:{1} c:{0}]", this.Maintenance.ExecutionLast
                , this.Maintenance.ExecutionCount
                , this.Maintenance.TimerMillisecond
                );
            return sb.ToString();
        }

		/// <summary>Make an ordering persisted list by comparator</summary>
		/// <param name="top"></param>
		/// <param name="asc"></param>
		/// <param name="comparator"></param>
		/// <returns></returns>
        protected IList<CacheItem<TKey, TValue>> Search(int top, bool asc, IComparer<CacheItem<TKey, TValue>> comparator)
        {

            List<CacheItem<TKey, TValue>> _sl
            = new List<CacheItem<TKey, TValue>>(top + 1);

            if (_lock.TryEnterReadLock(Constants.CacheReadTimeout))
                try
                {
                    foreach (var item in _data.Values)
                    {
                        if (_sl.Count < top){
                            _sl.Add(item);
                            if (_sl.Count == top)
                                _sl.Sort(comparator);
                        }
                        else if (asc && comparator.Compare(_sl[_sl.Count - 1], item) > 0)
                        {
                            int idx = _sl.FindIndex((v) => comparator.Compare(v, item) >= 0);
                            if (idx < 0) idx = 0;
                            _sl.Insert(idx,item);
                            _sl.RemoveAt(_sl.Count - 1);
                        }
                        else if (!asc && comparator.Compare(_sl[0], item) < 0)
                        {
                            int idx = _sl.FindIndex((v) => comparator.Compare(v, item) > 0);
                            if (idx < 0) idx = _sl.Count - 1;
                            _sl.Insert(idx, item);
                            _sl.RemoveAt(0);
                        }
                    }
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            else
            {
                Interlocked.Increment(ref _deadLocks);
            }
            if(!asc) _sl.Reverse();
            return _sl;

        }
        protected void forAllStarved(bool lockWrite,Action<CacheItem<TKey, TValue>> action)
        {
            if (
                lockWrite ? _lock.TryEnterUpgradeableReadLock(Constants.CacheReadTimeout)
                : _lock.TryEnterReadLock(Constants.CacheReadTimeout)
                )
            {
				//reset timer's statistic 
                int newHeuristicCount = 0;
                _timeoutStats.ReSet();
                try
                {
                    foreach (var citem in _data.Values)
                    {
                        if (citem.IsStarved)
                        {
                            if(lockWrite){
                                _lock.EnterWriteLock();
                                try
                                {
                                    action(citem);
                                }
                                finally
                                {
                                    _lock.ExitWriteLock();
                                }
                            } else {
                                action(citem);
                            }
                        }
                        else
                        {
                            newHeuristicCount++;
                            if(citem.Timeout>0)
                                _timeoutStats.Set(citem.Timeout); 
                        }
                    }
                    _heuristicCount = newHeuristicCount;
                }
                finally
                {
                    if(lockWrite) _lock.ExitUpgradeableReadLock();
                    else _lock.ExitReadLock();
                }

            }
            else
            {
                Interlocked.Increment(ref _deadLocks);
            }
        }

        protected CacheItem<TKey, TValue> addAndInitCacheItem(TKey key, TValue value, int secondsTimeout)
        {
            var result = new CacheItem<TKey, TValue>(key, value, secondsTimeout);
            _lock.EnterWriteLock();
            try
            {
                _data.Add(key, result);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
            
            if (secondsTimeout > 0)
            {
                _timeoutStats.Set(secondsTimeout);
                doMaintenance(secondsTimeout);
            }
            _heuristicCount++;
            return result;
        }
        protected  void updateAndInitCacheItem(CacheItem<TKey, TValue> citem, TValue value){
            lock (citem)
            {
                citem.Value = value;
                citem.AddWHit();
            }
        }

        protected void doMaintenance(int dueTime){
            if (_maintenance == null)
                _maintenance = new CacheMaintenance<TKey, TValue>(this);
            
            if(!_maintenance.IsRunning)
                _maintenance.Start(dueTime);
        }
        /// <summary>
		/// Implements the interface ICacheEnumerator to support enumeration of the items in the cache, <see cref="System.Collections.IEnumerator"/>
		/// </summary>
        private class CacheEnumerator : IEnumerator<TValue>, IDisposable
        {
            private bool isDisposed = false;
            private bool _locked = false;

            private Dictionary<TKey, CacheItem<TKey, TValue>>.ValueCollection.Enumerator _enumerator;
			/// <summary>Current cache item.</summary>
            private Cache<TKey, TValue> _ch;

            public CacheEnumerator(Cache<TKey, TValue> _ch)
            {
                this._enumerator = _ch._data.Values.GetEnumerator();
                this._ch = _ch;
                this._locked = _ch._lock.TryEnterReadLock(Constants.CacheReadTimeout);
            }

            TValue IEnumerator<TValue>.Current
            {
                get { return _enumerator.Current.Value; }
            }

            object System.Collections.IEnumerator.Current
            {
                get { return _enumerator.Current.Value; }
            }

            bool System.Collections.IEnumerator.MoveNext()
            {
                bool retval;
                do
                {
                    retval = this._enumerator.MoveNext();
                } while (retval && this._enumerator.Current.IsStarved );

                if (retval == false)
                    innerDispose();
                
                return retval;
            }

            void System.Collections.IEnumerator.Reset()
            {
                this._enumerator.Dispose();
                this._enumerator = _ch._data.Values.GetEnumerator();
            }
          
            protected void innerDispose(){
                if (!isDisposed)
                {
                    if (_locked) { _ch._lock.ExitReadLock(); _locked = false; };
                    this._enumerator.Dispose();
                    isDisposed = true;
                }
            }
            /// <summary>
			/// Implements the dispose method for release any locks on the cache .
            /// </summary>
            void IDisposable.Dispose()
            {
                innerDispose();
            }

        }

        #region IXmlSerializable
        System.Xml.Schema.XmlSchema IXmlSerializable.GetSchema(){return null;}
		/// <summary>Not yet implements, sorry!</summary>
		/// <param name="reader"></param>
        void IXmlSerializable.ReadXml(System.Xml.XmlReader reader)
        {
            throw new NotImplementedException();
        }

		/// <summary>Custom xml format</summary>
		/// <param name="writer"></param>
        void IXmlSerializable.WriteXml(System.Xml.XmlWriter writer)
        {
            writer.WriteAttributeString("deadLocks", _deadLocks.ToString());
            foreach (CacheItem<TKey,TValue> item in _data.Values)
            {
                writer.WriteStartElement("item");
                writer.WriteAttributeString("typeof", item.GetType().Name);
                ((IXmlSerializable)item).WriteXml(writer);
                writer.WriteEndElement();
            }
        }
        #endregion
    }
}
