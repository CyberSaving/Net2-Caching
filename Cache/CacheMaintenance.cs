using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace CyberSaving.Caching.Net2 {
    
	/// <summary>Disposable class that manage Maintenance algoritm</summary>
	/// <typeparam name="TKey"></typeparam>
	/// <typeparam name="TValue"></typeparam>
	interface ICacheMaintenance<TKey, TValue> : IDisposable
    {
        DateTime ExecutionLast { get ; }
        long ExecutionCount { get ;}

        void Start(int dueTime);
        void Stop();
        void Notify(CacheItem<TKey, TValue> item);
    }

	/// <summary>Maintenance algortim that use <c>Timer</c> for check cached items</summary>
	/// <remarks><see cref="Constants.CacheMinimumPurge"/> is minimum time for the timer's period. </remarks>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
	public class CacheMaintenance<TKey, TValue> : ICacheMaintenance<TKey, TValue>
    {

        Timer _timer = null;
        int _timerMillisecond = Constants.CacheMinimumPurge  * 1000;
        Cache<TKey, TValue> _cache = null;
        DateTime _exelast = DateTime.MinValue;
        long _execount = 0;
 
        public  CacheMaintenance(Cache<TKey,TValue> cache)
        {
            _cache = cache;
        }

        #region properties
        protected Cache<TKey, TValue> CurrentItem { get { return _cache; } }
        protected Timer timer { get { return _timer; } }

        public DateTime ExecutionLast { get { return _exelast; } }
        public long ExecutionCount { get { return _execount; } }
        public bool IsRunning { get { return _timer !=null; } }
        #endregion

        public virtual void Tick(object obj){
            _exelast = DateTime.Now;
            _execount++;
            if (_cache!=null)
                try
                {
                    _cache.Purge();
                }
                catch{ }

            //restart Ticketing
            ResetTime();
        }
        public void Start(int dueTime)
        {
            if (_timer != null)
                _timer.Dispose();

            _timer = new Timer(new TimerCallback(this.Tick), null, dueTime, Timeout.Infinite);
        }

        public void Stop()
        {
            if (_timer != null)
               _timer.Dispose();
            _timer = null;
        }

        void ICacheMaintenance<TKey, TValue>.Notify(CacheItem<TKey, TValue> cacheitem){
            //do nothing
        }
        protected virtual void ResetTime()
        {
            if (_cache.TimeoutStats.MaxValue > 0)
            {
                TimerMillisecond =
                    Math.Max(1000 * Constants.CacheMinimumPurge, (int)(_cache.TimeoutStats.Avg * 1000));
            }
        }

        public int TimerMillisecond
        {
            get { return _timerMillisecond;  }
            protected set
            {
                _timerMillisecond = value;
                _timer.Change(_timerMillisecond, Timeout.Infinite);
            }
        }

        void IDisposable.Dispose()
        {
            if (_timer != null)
                _timer.Dispose();
            _cache = null;
        }

    }

}
