using System.IO;
using System.Threading.Tasks;
using EMullen.Core;
using EMullen.Core.PlayerMgmt;
using UnityEngine;

namespace EMullen.PlayerMgmt 
{
    /// <summary>
    /// The file system login handler is responsible for getting the players username and logging
    ///   in with it as an input.
    /// </summary>
    public class FSLoginHandler : UserInputLoginHandler
    {

        [Header("Must match corresponding FileSystemPlayerDatabase's name")]
        [SerializeField]
        private string fsDatabaseName = "default";

        public override string Status => "Ready";

        protected override Task LoginStart(LocalPlayer localPlayer, bool registering)
        {
            return Task.CompletedTask;
        }

        public override async Task AcceptInput(LocalPlayer from, LoginInput input)
        {
            string uid = input.input[0];

            BLog.Highlight($"Uid: {uid}");

            if(input.registering) {
                string basePath = FSPlayerDatabase.GetPath(fsDatabaseName);
                string playersPath = FSPlayerDatabase.GetPlayersPath(basePath, uid);
                if(File.Exists(playersPath)) {
                    throw new System.Exception("Cannot register player, they already exist.");
                }
            }

            await LoginEnd(from, input.input[0]);
        }
    }
}