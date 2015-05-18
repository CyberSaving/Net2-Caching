using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CyberSaving.Caching.Net2 {
	/// <summary>Set of constants often used by cache's mechanism.</summary>
    public class Constants
    {
		/// <summary>Maximum time to wait on the semaphore for write to the cache.</summary>
		/// <value>5000</value>
        public const int CacheWriteTimeout = 5000;
		/// <summary>Maximum time to wait on the semaphore for read to the cache..</summary>
		/// <value>5000</value>
		public const int CacheReadTimeout = 5000;
		/// <summary>Minimum waiting time before launching the cache controll process.(sec)</summary>
		/// <value>10</value>
		public const int CacheMinimumPurge = 10;
		/// <summary>Name of default Cache</summary>
		/// <value>_0</value>
		public static readonly string MainClassCache = "_0";
    }

	/// <summary>Describe type of resualt in a insert or update action</summary>
	/// <remarks><c>(_var & GetSetStatus.Added)>0 </c> => item was created</remarks>
    public enum GetSetStatus : short
    {
        /// <summary>When a item is created in a Cache, usually in first time</summary>
		/// <value>1</value>
        Added = 1,

        /// <summary>When a item are already in a Cache</summary>
		/// <value>2</value>
		Unchanged = 2,
        
        /// <summary>When a item are update becouse the item is Starved in a Cache, usually after cleaning</summary>
		/// <value>3</value>
		Updated = 3,

        /// <summary>When is impossible to add into the cache</summary>
		/// <value>4</value>
		UnCached = 4
    };
}
