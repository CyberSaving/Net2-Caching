using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace CyberSaving.Caching.Net2 {
    #region CacheItem class implementation
    /// <summary>Define a entry Key,Value in a Cache.  <seealso cref="Cache"/></summary>
    public class CacheItem<TKey, TValue> : IXmlSerializable
    {

        #region Fields
        /// <summary>Item's key</summary>
        protected TKey _key;
		/// <summary>Item's value</summary>
        protected TValue _value;
        
        /// <summary>Creation Timestamp (it will assign when a item is create in a cache)</summary>
        protected DateTime _creationTimestamp;
        /// <summary>Last write access</summary>
        protected DateTime _valueTimestamp;
        /// <summary>Last access time (readwWrite), it will assign when a item was read or writed.</summary>		
        protected DateTime _lastAccessTimestamp;
        /// <summary>Time to live, <see cref="CacheMode"/></summary>
        protected int _timeout;
        /// <summary>Number of read hits.</summary>
        protected int _rHits;
		/// <summary>Number of write hits.</summary>
        protected int _wHits;
		/// <summary>Number of starved event.</summary>
        protected int _sHits;
        /// <summary>When the item will purged</summary>
        protected bool _isvalid;

        #endregion

        #region Construction
        /// <summary>Base Costructor.</summary>
        public CacheItem() { }
        /// <overloads>Specialize constructor with all initializes object references kept in cache</overloads>
		/// <summary>Costructor</summary>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param> 
        /// <param name="timeout">Time to live in second</param>
        public CacheItem(TKey key, TValue value, int timeout)
        {
            _value = value;
            _key = key;
            _rHits = _wHits = 0;
            _creationTimestamp = DateTime.Now;
            _lastAccessTimestamp = _creationTimestamp;
            _valueTimestamp = _creationTimestamp;
            _timeout = timeout;
            _isvalid = true;
        }

		/// <overloads>Specialize constructor with all initializes object references kept in cache.</overloads>
		/// <summary>Costructor.</summary>
		/// <param name="key">Key</param>
		/// <param name="value">Value</param> 
        public CacheItem(TKey key, TValue value)
            : this(key, value, 0)
        {
        }

		/// <summary>Copy costructor</summary>
        public CacheItem(CacheItem<TKey, TValue> ci)
        {
            _creationTimestamp = ci._creationTimestamp;
            _valueTimestamp = ci._valueTimestamp;
            _lastAccessTimestamp = ci._lastAccessTimestamp;
            _rHits = ci._rHits;
            _wHits = ci._wHits;
            _timeout = ci._timeout;
            _key = ci._key;
            _value = ci._value;
            _isvalid = ci._isvalid;
        }

        #endregion

        #region Properties
        /// <summary>Get or Set cached value.</summary>
        public virtual TValue Value
        {
            get
            {
                return _value;
            }
            set
            {
                this._isvalid = true;
                this._value = value ;
            }
        }
		/// <summary>Number of times the item was not found</summary>
        public int SHits
        {
            get { return _sHits; }
        }

		/// <summary>Number of times the item was read</summary>
        public int RHits
        {
            get { return _rHits; }
        }
		/// <summary>Number of times the item was written</summary>				
        public int WHits
        {
            get { return _wHits; }
        }
		/// <summary>Number of times the item was read or written</summary>			
        public int Hits
        {
            get { return _rHits + _wHits; }
        }

		/// <summary>Athomic add read count</summary>
        /// <remarks>update last acccess date</remarks>
        /// <returns>last updated</returns>
        /// <see cref="LastAccessTimestamp"/>
        public int AddRHit()
        {
            System.Threading.Interlocked.Increment(ref _rHits);
            _lastAccessTimestamp = DateTime.Now;
            return _rHits;
        }
		/// <summary>Athomic add write count</summary>
		/// <remarks>update last acccess date and original Timestamp</remarks>
		/// <returns>last updated</returns>
        /// <see cref="LastAccessTimestamp"/>
		/// <see cref="ValueTimestamp"/>
        public int AddWHit()
        {
            System.Threading.Interlocked.Increment(ref _wHits);
            _valueTimestamp = _lastAccessTimestamp = DateTime.Now;
            return _wHits;
        }

		/// <summary>Athomic add starved count</summary>
		/// <returns>last updated</returns>
		/// <see cref="LastAccessTimestamp"/>
		public int AddStHit()
        {
            System.Threading.Interlocked.Increment(ref _sHits);
            return _sHits;
        }

        /// <summary>Timeout (sec).</summary>	
        public int Timeout
        {
            get { return _timeout; }
            set { _timeout = value; }
        }

        /// <summary>Timestamp when was created.</summary>	
        public DateTime CreationTimestamp
        {
            get { return _creationTimestamp; }
        }
		/// <summary>Timestamp of last read or write</summary>	
        public DateTime LastAccessTimestamp
        {
            get { return _lastAccessTimestamp; }
            protected set { _lastAccessTimestamp = value; }
        }

		/// <summary>Timestamp of value was set</summary>	
        public DateTime ValueTimestamp
        {
            get { return _valueTimestamp; }
        }
        /// <summary>The key, <see cref="CacheItem"/></summary>
        public TKey Key
        {
            get { return _key; }
            set { _key = value; }
        }

        

        /// <summary>The item is considered starved when timeout period has expired</summary>
        virtual public bool IsStarved
        {
            get
            {
                if (!_isvalid) 
                    return true;
                if (_timeout == 0)
                    return false;
                DateTime dt = _valueTimestamp;
                bool isstarved = (dt.AddSeconds((double)_timeout)).CompareTo(System.DateTime.Now) < 0;
                return isstarved;
            }
        }
        /// <summary>Define if a item is still alive or it's purged</summary>
        /// <seealso cref="Purge()"/>
        public bool IsAlive
        {
            get
            {
                return _isvalid;
            }
        }
        #endregion

        #region Methods
        /// <summary>Remove items object</summary>
        public void Purge()
        {
            _value = default(TValue);
            _isvalid = false;
        }

        /// <summary>Rappresent all properties of <b>CacheItem</b>.</summary>
        /// <returns>Format Key = {0}, Value = {1}, Created = {2}, LastAccess = {3} (es. "Key = {uno}, Value = {1000}, Created = {22/10/2020 19:19:00}, LastAccess = {22/10/2020 21:19:00}").</returns>
        public override string ToString()
        {
            return String.Format("[{0}]=>'{1}', tout = {3}, tsValue = {2}, tsLast = {4}, hits[r:{5}|w:{6}|s:{7}]", Key, Value
                , _valueTimestamp
                , _timeout
                , LastAccessTimestamp
                , this._rHits, this._wHits, this._sHits
                );
        }
        
        
        private string ConvertAllToString(object what)
        {
            string retval = null; long lenght = 0;
            ConvertAllToString(what, out retval, out lenght);
            return retval;
        }
        private bool ConvertAllToString(object what, out string retstring, out long lenght){
            lenght = 0;
            retstring = null;
            if( what ==null) 
                return false;

            if (what is string)
            {
                retstring = (string)what;
                return false;
            }
                

            var theType = what.GetType();
            if (what is IFormattable)
            {
                retstring =((IFormattable)what).ToString(null, System.Globalization.CultureInfo.InvariantCulture);
                lenght = retstring.Length;
                return false;
            }
            else if (theType.IsSerializable )
            {
                // Serialize to a base 64 string
                byte[] bytes;
                long length = 0;
                using (var ws = new System.IO.MemoryStream()) { 
                    var sf = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    sf.Serialize(ws, what);
                    length = ws.Length;
                    bytes = ws.GetBuffer();
                    retstring = Convert.ToBase64String(bytes, 0, bytes.Length, Base64FormattingOptions.None);
                }
                lenght = bytes.LongLength;
                return true ;
            }
            else
            {
                retstring = what.ToString();
                lenght = retstring.Length;
            }
            return false;
        }

        private TValue convertValueFromBase64(string bufferStr, long lenght)
        {
            return convertValueFromBase64(bufferStr, lenght);
        }
        private TValue convertValueFromBase64(byte[] buffer, long lenght)
        {
            TValue retval = default(TValue);
            var sf = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            using (var ws = new System.IO.MemoryStream(buffer))
            {
                 retval =  (TValue)sf.Deserialize(ws);
            }
            return retval;
        }
        #endregion

        #region IXmlSerializable Impl
        System.Xml.Schema.XmlSchema IXmlSerializable.GetSchema(){ return null; }

        void IXmlSerializable.ReadXml(System.Xml.XmlReader reader)
        {
            reader.MoveToContent();
            string bf = reader.GetAttribute("Key");
            if (bf == null) throw new Exception("Missed key");
            _key = (TKey)Convert.ChangeType(bf,typeof(TKey));

            bf = reader.GetAttribute("Timeout");
            if (bf != null)
                _timeout = Convert.ToInt32(bf);
            if (reader.Read())
                reader.ReadStartElement("Hits");
            bf = reader.GetAttribute("r");
            _rHits = Convert.ToInt32(bf);
            bf = reader.GetAttribute("w");
            _wHits = Convert.ToInt32(bf);
            bf = reader.GetAttribute("s");
            _sHits = Convert.ToInt32(bf);
            
            if (reader.Read())
                reader.ReadStartElement("Dates");
            bf = reader.GetAttribute("cr");
            _creationTimestamp = DateTime.Parse(bf, System.Globalization.CultureInfo.InvariantCulture);
            bf = reader.GetAttribute("va");
            _valueTimestamp = DateTime.Parse(bf, System.Globalization.CultureInfo.InvariantCulture);
            bf = reader.GetAttribute("la");
            _lastAccessTimestamp = DateTime.Parse(bf, System.Globalization.CultureInfo.InvariantCulture);

            if (reader.Read()) { 
                reader.ReadStartElement("Value");
                bf = reader.GetAttribute("lenght");
                if (bf != null){
                    _value = convertValueFromBase64(reader.ReadElementContentAsString(),long.Parse(bf));
                } else
                    _value = (TValue)Convert.ChangeType(reader.ReadElementContentAsString(),typeof(TValue));

                reader.ReadEndElement();
            }


        }

        void IXmlSerializable.WriteXml(System.Xml.XmlWriter writer)
        {
            writer.WriteAttributeString("Key", ConvertAllToString(this._key));
            if (_timeout >0)
                writer.WriteAttributeString("Timeout", _timeout.ToString());
            
            writer.WriteStartElement("Hits");
                writer.WriteAttributeString("r",_rHits.ToString());
                writer.WriteAttributeString("w",_wHits.ToString());
                writer.WriteAttributeString("s",_sHits.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("Dates");
                writer.WriteAttributeString("cr",_creationTimestamp.ToString(System.Globalization.CultureInfo.InvariantCulture));
                writer.WriteAttributeString("va", _valueTimestamp.ToString(System.Globalization.CultureInfo.InvariantCulture));
                writer.WriteAttributeString("la", _lastAccessTimestamp.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteEndElement();
            
            writer.WriteStartElement("Value");
            writer.WriteAttributeString("type",(_value ==null) ? "" : _value.GetType().FullName);

            string converted;long lenght;
            if (ConvertAllToString(_value, out converted, out lenght))
            {
                if (converted != null)
                    writer.WriteAttributeString("lenght", lenght.ToString());
            }
            if (converted!=null)
                writer.WriteValue(converted);
            writer.WriteEndElement();
        }
        #endregion
    }
    #endregion

	/// <summary>Implement sliding timeout expiration
	/// <para>
	///  [Origin] [Timeout]  [Last access]  [sliding expiration] 
	///		|    ->   |    ->    |        ->       [starved]
	/// </para>
	/// </summary>
	/// <typeparam name="TKey"></typeparam>
	/// <typeparam name="TValue"></typeparam>
    public class CacheItemSlide<TKey, TValue> : CacheItem<TKey, TValue> 
    {
        /// <summary>Time of sliding sliding</summary>
        int _slidingExpirationSecond = 0;

		/// <summary>Second of sliding expiration</summary>
        public int SlidingExpiration
        {
            get { return _slidingExpirationSecond; }
            set { _slidingExpirationSecond = value; }
        }

		/// <summary>Costructor</summary>
		/// <param name="key"></param>
		/// <param name="value"></param>
		/// <param name="timeout"></param>
		/// <param name="slidingExpiration"></param>
        public CacheItemSlide(TKey key, TValue value, int timeout, int slidingExpiration)
            : base(key, value, timeout)
        {
            _slidingExpirationSecond = slidingExpiration;
        }

		/// <summary>Costructor</summary>
		/// <param name="from"></param>
        public CacheItemSlide(CacheItemSlide<TKey, TValue> from)
            : base(from)
        {
            _slidingExpirationSecond = from._slidingExpirationSecond;
        }

		/// <summary>sliding starved algoritm</summary>
        public override bool IsStarved
        {
            get
            {
                bool isAlreadyStarved = base.IsStarved;
                if (_slidingExpirationSecond==0)
                    return isAlreadyStarved;
                
                //Stared but not purged (isAlive)
                if (isAlreadyStarved && this.IsAlive){
                    return
                        this._lastAccessTimestamp
                        .AddSeconds((double)_slidingExpirationSecond)
                        .CompareTo(DateTime.Now) < 0;
                }
                return false;

            }
        }
    }

	/// <summary>Abstract class for implement CacheItem comparetor</summary>
	/// <typeparam name="TKey"></typeparam>
	/// <typeparam name="TValue"></typeparam>
    public abstract class CacheItemComparer<TKey, TValue> : IComparer<CacheItem<TKey, TValue>>
    {
        public static readonly CacheItemComparer<TKey, TValue> ByHits =
            new StringComparerByTotalHits();
        public static readonly CacheItemComparer<TKey, TValue> ByRatio =
            new StringComparerByTotalRatio();
        private class StringComparerByTotalHits : CacheItemComparer<TKey, TValue>
        {
            public override int Compare(CacheItem<TKey, TValue> x, CacheItem<TKey, TValue> y)
            {
                if (x.Hits != y.Hits)
                    return x.Hits.CompareTo(y.Hits);
                if (x.SHits != y.SHits)
                    return x.SHits.CompareTo(y.SHits);
                return x.ValueTimestamp.CompareTo(y.ValueTimestamp);
            }
        }

        private class StringComparerByTotalRatio : CacheItemComparer<TKey, TValue>
        {
            public override int Compare(CacheItem<TKey, TValue> x, CacheItem<TKey, TValue> y)
            {
                float xR = (x.Hits == 0) ? 0 : ((float)x.Hits - x.SHits) / (float)x.Hits;
                float yR = (y.Hits == 0) ? 0 : ((float)y.Hits - y.SHits) / (float)y.Hits;
                if (xR != yR)
                    return xR.CompareTo(yR);
                if (x.Hits != y.Hits)
                    return x.Hits.CompareTo(y.Hits);
                return x.ValueTimestamp.CompareTo(y.ValueTimestamp);
            }
        }

		/// <summary>Method to override for define Compare algoritm
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
        public abstract int Compare(CacheItem<TKey, TValue> x, CacheItem<TKey, TValue> y);
    }

}