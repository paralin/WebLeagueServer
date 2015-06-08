using System;

namespace WLNetwork
{
	/// <summary>
	/// WebLeague match result
	/// </summary>
	public enum EMatchResult
	{
		/// <summary>
		/// Never count this match result
		/// </summary>
		DontCount = 0,

		/// <summary>
		/// Unknown match result (to be counted later)
		/// </summary>
		Unknown = 1,

		/// <summary>
		/// Radiant victory
		/// </summary>
		RadVictory = 2,

		/// <summary>
		/// Dire victory
		/// </summary>
		DireVictory = 3,
	}
}

