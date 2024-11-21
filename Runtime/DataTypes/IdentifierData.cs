using System;

namespace EMullen.PlayerMgmt 
{
    /// <summary>
    /// This IdentifierData is used by the PlayerManagement to keep track of the PlayerData.
    /// The uid WILL NOT CHANGE once instantiated, this ensures you can safely reference the
    ///   same PlayerData each time.
    /// </summary>
    public class IdentifierData : PlayerDataClass
    {
        public string uid;
        public int? localPlayerIndex;

        public IdentifierData() {}

        public IdentifierData(string uid, int? localPlayerIndex = null) 
        {
            this.uid = uid;
            this.localPlayerIndex = localPlayerIndex;
        }

        public IdentifierData(int? localPlayerIndex = null) 
        {
            this.uid = GenerateUID();
            this.localPlayerIndex = localPlayerIndex;
        }

        public override string ToString() => $"uid: {uid} idx: {localPlayerIndex}";

        public static string GenerateUID() => Guid.NewGuid().ToString();
    }

    public static class IdentifierPlayerDataExtensions 
    {
        public static string GetUID(this PlayerData playerData) 
        {
            return playerData.GetData<IdentifierData>().uid;
        }
    }
}