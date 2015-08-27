using WLNetwork.Matches.Enums;

namespace WLNetwork.Matches.Args
{
	/// <summary>
	/// Change a match result.
	/// </summary>
	public class ChangeResultArgs
	{
		/// <summary>
		/// Match result ID
		/// </summary>
		public ulong Id { get; set; }

		/// <summary>
		/// New match result
		/// </summary>
		/// <value>The result.</value>
		public EMatchResult Result { get; set; }
	}
}

