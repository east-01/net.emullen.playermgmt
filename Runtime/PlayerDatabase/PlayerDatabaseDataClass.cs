using EMullen.PlayerMgmt;

namespace EMullen.Core.PlayerMgmt 
{
    /// <summary>
    /// The PlayerDatabaseDataClass is an extension of the PlayerDataClass that enforces a string
    ///   UID to be the first value. The UID is required to be first as it's the identifier for
    ///   the data in each table.
    /// </summary>
    public abstract class PlayerDatabaseDataClass : PlayerDataClass 
    {
        public abstract string UID { get; }
    }
}