// 
// System.Web.Caching
//
// Author:
//   Patrik Torstensson (Patrik.Torstensson@labs2.com)
//
// (C) Copyright Patrik Torstensson, 2001
//
namespace System.Web.Caching
{
	/// <summary>
	/// Class responsible for representing a cache entry.
	/// </summary>
	public class CacheEntry
	{
		/// <summary>
		/// Defines the status of the current cache entry
		/// </summary>
		public enum Flags
		{
			Removed	= 0,
			Public	= 1
		}

		private CacheItemPriority		_enumPriority;
        
		private long	_longHits;

		private byte	_byteExpiresBucket;
		private int		_intExpiresIndex;

		private	long	_ticksExpires;	
		private long	_ticksSlidingExpiration;

		private	string	_strKey;
		private	object	_objItem;

		private long	_longMinHits;

		private Flags	_enumFlags;
		
		private CacheDependency		_objDependency;
		private Cache				_objCache;

		/// <summary>
		/// Constructs a new cache entry
		/// </summary>
		/// <param name="strKey">The cache key used to reference the item.</param>
		/// <param name="objItem">The item to be added to the cache.</param>
		/// <param name="objDependency">The file or cache key dependencies for the item. When any dependency changes, the object becomes invalid and is removed from the cache. If there are no dependencies, this paramter contains a null reference.</param>
		/// <param name="dtExpires">The time at which the added object expires and is removed from the cache. </param>
		/// <param name="tsSpan">The interval between the time the added object was last accessed and when that object expires. If this value is the equivalent of 20 minutes, the object expires and is removed from the cache 20 minutes after it is last accessed.</param>
		/// <param name="longMinHits">Used to detect and control if the item should be flushed due to under usage</param>
		/// <param name="boolPublic">Defines if the item is public or not</param>
		/// <param name="enumPriority">The relative cost of the object, as expressed by the CacheItemPriority enumeration. The cache uses this value when it evicts objects; objects with a lower cost are removed from the cache before objects with a higher cost.</param>
		public CacheEntry(	Cache objManager, string strKey, object objItem, CacheDependency objDependency, CacheItemRemovedCallback eventRemove, 
							System.DateTime dtExpires, System.TimeSpan tsSpan, long longMinHits, bool boolPublic, CacheItemPriority enumPriority )
		{
			if (boolPublic)
			{
				SetFlag(Flags.Public);
			}

			_strKey = strKey;
			_objItem = objItem;
			_objCache = objManager;
			
			_onRemoved += eventRemove;

			_enumPriority = enumPriority;

			_ticksExpires = dtExpires.Ticks;

			_ticksSlidingExpiration = tsSpan.Ticks;

			// If we have a sliding expiration it overrides the absolute expiration (MS behavior)
			if (tsSpan.Ticks != System.TimeSpan.Zero.Ticks)
			{
				_ticksExpires = System.DateTime.Now.AddTicks(_ticksSlidingExpiration).Ticks;
			}
			
			_objDependency = objDependency;
			if (_objDependency != null)
			{
				if (_objDependency.IsDisposed)
				{
					throw new System.ObjectDisposedException("System.Web.CacheDependency");
				}

				// Add the entry to the cache dependency handler (we support multiple entries per handler)
				_objDependency.Changed += new CacheDependency.CacheDependencyCallback(OnChanged); 
			}

			_longMinHits = longMinHits;
		}

		private event CacheItemRemovedCallback _onRemoved;

		public void OnChanged(CacheDependency objDependency)
		{
			_objCache.Remove(_strKey, CacheItemRemovedReason.DependencyChanged);
		}

		/// <summary>
		/// Cleans up the cache entry, removes the cache dependency and calls the remove delegate.
		/// </summary>
		/// <param name="enumReason">The reason why the cache entry are going to be removed</param>
		public void Close(CacheItemRemovedReason enumReason)
		{	
			lock(this)
			{
				// Check if the item already is removed
				if (TestFlag(Flags.Removed))
				{
					return;
				}

				SetFlag(Flags.Removed);

				if (_onRemoved != null)
				{
					// Call the delegate to tell that we are now removing the entry
					try 
					{
						_onRemoved(_strKey, _objItem, enumReason);		
					}
					catch (System.Exception objException)
					{
						System.Diagnostics.Debug.Fail("System.Web.CacheEntry.Close() Exception when calling remove delegate", "Message: " + objException.Message + " Stack: " + objException.StackTrace + " Source:" + objException.Source);
					}
				}

				// If we have a dependency, remove the entry
				if (_objDependency != null)
				{
					_objDependency.Changed -= new CacheDependency.CacheDependencyCallback(OnChanged);
					if (!_objDependency.HasEvents)
					{
						_objDependency.Dispose();
					}
				}
			}
		}

		/// <summary>
		/// Tests a specific flag is set or not.
		/// </summary>
		/// <param name="oFlag">Flag to test agains</param>
		/// <returns>Returns true if the flag is set.</returns>
		public bool TestFlag(Flags oFlag)
		{
			lock(this) 
			{
				if ((_enumFlags & oFlag) != 0)
				{
					return true;
				} 

				return false;
			}
		}

		/// <summary>
		/// Sets a specific flag.
		/// </summary>
		/// <param name="oFlag">Flag to set.</param>
		public void SetFlag(Flags oFlag)
		{
			lock (this)
			{
				_enumFlags |= oFlag;
			}
		}

		/// <summary>
		/// Returns true if the object has minimum hit usage flushing enabled.
		/// </summary>
		public bool HasUsage
		{
			get { 
				if (_longMinHits == System.Int64.MaxValue) 
				{
					return false;
				}

				return true;
			}
		}

		/// <summary>
		/// Returns true if the entry has absolute expiration.
		/// </summary>
		public bool HasAbsoluteExpiration
		{
			get 
			{ 
				if (_ticksExpires == System.DateTime.MaxValue.Ticks) 
				{
					return false;
				}

				return true;
			}
		}

		/// <summary>
		/// Returns true if the entry has sliding expiration enabled.
		/// </summary>
		public bool HasSlidingExpiration
		{
			get 
			{ 
				if (_ticksSlidingExpiration == System.TimeSpan.Zero.Ticks) 
				{
					return false;
				}

				return true;
			}
		}
		
		/// <summary>
		/// Gets and sets the current expires bucket the entry is active in.
		/// </summary>
		public byte ExpiresBucket
		{
			get 
			{ 
				lock (this) 
				{
					return _byteExpiresBucket; 
				}
			}
			set 
			{ 
				lock (this)
				{
					_byteExpiresBucket = ExpiresBucket; 
				}
			}
		}

		/// <summary>
		/// Gets and sets the current index in the expires bucket of the current cache entry.
		/// </summary>
		public int ExpiresIndex
		{
			get 
			{ 
				lock (this)
				{
					return _intExpiresIndex; 
				}
			}
			
			set 
			{ 
				lock (this)
				{
					_intExpiresIndex = ExpiresIndex; 
				}
			}
		}
        
		/// <summary>
		/// Gets and sets the expiration of the cache entry.
		/// </summary>
		public long Expires
		{
			get 
			{ 
				lock (this)
				{
					return _ticksExpires; 
				}
			}
			set 
			{ 
				lock (this)
				{
					_ticksExpires = Expires; 
				}
			}
		}

		/// <summary>
		/// Gets the sliding expiration value. The return value is in ticks (since 0/0-01 in 100nanosec)
		/// </summary>
		public long SlidingExpiration
		{
			get 
			{ 
				return _ticksSlidingExpiration; 
			}
		}

		/// <summary>
		/// Returns the current cached item.
		/// </summary>
		public object Item
		{
			get
			{
				return _objItem; 
			}
		}

		/// <summary>
		/// Returns the current cache identifier.
		/// </summary>
		public string Key
		{
			get 
			{ 
				return _strKey; 
			}
		}

		/// <summary>
		/// Gets and sets the current number of hits on the cache entry.
		/// </summary>
		public long Hits
		{
			// todo: Could be optimized by using interlocked methods..
			get 
			{
				lock (this)
				{
					return _longHits; 
				}
			}
			set 
			{ 
				lock (this)
				{
					_longHits = Hits; 
				}
			}
		}

		/// <summary>
		/// Returns minimum hits for the usage flushing rutine.
		/// </summary>
		public long MinimumHits
		{
			get 
			{ 
				return _longMinHits; 
			}
		}

		/// <summary>
		/// Returns the priority of the cache entry.
		/// </summary>
		public CacheItemPriority Priority
		{
			get 
			{ 
				return _enumPriority; 
			}
		}
	}
}
