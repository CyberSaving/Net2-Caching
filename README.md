# Net2 Cache Library
Caching system for DotNet for CLR 2


The cache library provides a simple interface for cache in-memory solutions for DotNet (CLR 2.0). 

Cache features
-------------------------

1. ThreadSafe 
2. Use Generics
2. Keep statistic of write, read and miss-
3. Multi domain cache.
4. Expiring algorithm by asynchronous thread
5. Tracing activities

Planned features (TODO)
---------------

1. Sliding Expiring (like Asp.net cahce)
2. Full XML Serializable.
3. Call back on Cleaning 
4. Add to NuGet

Quick example
-------------

The following is a quick example of how to use CSaving Cache.
```csharp

	[STAThread]
	static void Main(string[] args)
	{
		object retval;
		Cache<string, object> _caches =  new Cache<string, object>(StringComparer.CurrentCultureIgnoreCase);
		
		//Insert values into the cache
		_caches.Put("itemaA","valueA");
		_caches.Put("itemaB","valueB", 2 ); //for 2 seconds
		
		//Retrieve values from the cache
		_caches.Get("itemaA",out retval); //true if found
		
		//Retrieve values and add if missed
		_caches.GetSet("itemaA",out retval, () => OnMissDoSomething()); 
		_caches.GetSet("itemaB",out retval, 2, () => OnMissDoSomething());  //for 2 seconds
		
		switch (_caches.GetSet("itemaA",out retval, () => OnMissDoSomething())){
			case Added: 		//Created in a Cache
				break;
			case Unchanged:  	//already in a Cache
				break;
			case Updated: 		//Updated after starved
				break;
			case UnCached: 		//Impossible to Cache (es Timeout deadlock)
				break;
		}
		
		//Clear all collection
		_caches.Clear();
		
		//Remove expired items and their statistics
		_caches.Trim()
	}
