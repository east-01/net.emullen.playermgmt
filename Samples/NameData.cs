using UnityEngine;

namespace EMullen.PlayerMgmt.Samples 
{
    public class NameData : PlayerDataClass
    {
        public string Name = "PlayerName";

        public NameData() {}
        public override string ToString() => Name;
    }
}