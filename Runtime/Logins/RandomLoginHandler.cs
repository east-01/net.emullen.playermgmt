using System.Threading.Tasks;
using EMullen.Core;

namespace EMullen.PlayerMgmt 
{
    public class RandomLoginHandler : LoginHandler
    {
        public override string Status => "Ready";

        protected override async Task LoginStart(LocalPlayer localPlayer, bool registering)
        {
            string uid = IdentifierData.GenerateUID();
            BLog.Log($"Logging in local player id {localPlayer.Input.playerIndex} as {uid}", "LoginHandler", 0);
            await LoginEnd(localPlayer, uid, false);
        }
        /// <summary>
        /// The player can never be logging in for the random login handler, UIDs generated
        ///   randomly and instantly.
        /// </summary>
        /// <returns>false</returns>
        public override bool IsLoggingIn(LocalPlayer localPlayer) => false;
    }
}