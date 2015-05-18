using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CyberSaving.Caching.Net2
{
	/// <summary>Utility class for save some common facility of statistics</summary>
	/// <typeparam name="TVal">Any kind of type which have a average's concept</typeparam>
	public abstract class IStatisticsUnit<TVal>
	{
		/// <summary>Current Min values added so far</summary>
		public TVal MinValue;
		/// <summary>Current Max valeus added so far</summary>
		public TVal MaxValue;
		/// <summary>Current Average value</summary>
		public float Avg = 0.0f;
		/// <summary>Total number of value inserted</summary>
		public ulong count = 0;

		/// <summary>Reset all value of start</summary>
		public abstract void ReSet();
		/// <summary>Add new values to register</summary>
		/// <param name="val">value</param>
		public abstract void Set(TVal val);

		/// <summary>Common construct</summary>
		/// <param name="minValue">Specify lower value possible by type</param>
		public IStatisticsUnit(TVal minValue)
		{
			MaxValue = default(TVal);
			MinValue = minValue;
		}
		/// <summary>Common rappresentation of type</summary>
		/// <returns></returns>
		public override string ToString()
		{
			return string.Format("n:{0} [{1}/{2}] a:{3}", count, MinValue, MaxValue, Avg);
		}
	}

	/// <summary>Implements <c>IStatisticsUnit</c> whit long type</summary>
	public class StatisticsUnitLong : IStatisticsUnit<long>
	{
		/// <summary>Common Constructor</summary>
		public StatisticsUnitLong()
			: base(long.MaxValue) { }

		/// <summary>Reset</summary>
		/// <seealso cref="IStatisticsUnit<TVal>.ReSet()"/>
		public override void ReSet()
		{
			MaxValue = 0;
			MinValue = long.MaxValue;
			Avg = 0.0f;
			count = 0;
		}
		/// <summary>Set</summary>
		/// <seealso cref="IStatisticsUnit<TVal>.Set()"/>
		public override void Set(long val)
		{
			count++;
			if (MinValue > val)
				MinValue = val;
			if (MaxValue < val)
				MaxValue = val;
			Avg += (((float)val - Avg) / (float)count);
		}
	}

	/// <summary>Implements <c>IStatisticsUnit</c> whit int type</summary>
	public class StatisticsUnitInt : IStatisticsUnit<int>
	{

		/// <summary>Common Constructor</summary>
		public StatisticsUnitInt() : base(int.MaxValue) { }

		/// <summary>Reset</summary>
		/// <seealso cref="IStatisticsUnit<TVal>.ReSet()"/>
		public override void ReSet()
		{
			MaxValue = 0;
			MinValue = int.MaxValue;
			Avg = 0.0f;
			count = 0;
		}

		/// <summary>Set</summary>
		/// <seealso cref="IStatisticsUnit<TVal>.Set()"/>
		public override void Set(int val)
		{
			count++;
			if (MinValue > val)
				MinValue = val;
			if (MaxValue < val)
				MaxValue = val;
			Avg += (((float)val - Avg) / (float)count);
		}

		/// <summary>combines two StatisticsUnitInt between them</summary>
		/// <param name="s1"></param>
		/// <param name="s2"></param>
		/// <returns></returns>
		public static StatisticsUnitInt operator +(StatisticsUnitInt s1, StatisticsUnitInt s2)
		{
			var ret = new StatisticsUnitInt();
			ret.MaxValue = Math.Max(s1.MaxValue, s2.MaxValue);
			ret.MinValue = Math.Min(s1.MinValue, s2.MinValue);
			ret.count = s1.count + s2.count;
			ret.Avg = (float)(s1.Avg * s1.count + s2.Avg * s2.count) / (float)(s1.count + s2.count);
			return ret;
		}

		/// <summary>merge into current StatisticsUnitInt with another one</summary>
		/// <param name="s1"></param>
		public void Merge(StatisticsUnitInt s1)
		{
			this.MaxValue = Math.Max(s1.MaxValue, this.MaxValue);
			this.MinValue = Math.Min(s1.MinValue, this.MinValue);
			this.count += s1.count;
			this.Avg = (float)(s1.Avg * s1.count + this.Avg * this.count) / (float)(s1.count + this.count);
		}
	}

}