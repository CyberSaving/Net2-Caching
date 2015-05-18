using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CyberSaving.Caching.Net2 {
	/// <summary>The simplest interface that helps an element to be cached</summary>
	/// <typeparam name="TKey">Type of string. Often string</typeparam>
	/// <typeparam name="TValue">Type of string. Any type</typeparam>
    interface ICacheItem<TKey, TValue>
    {
         TValue Value { get; set; }
         TKey Key { get; set; }

    }

}