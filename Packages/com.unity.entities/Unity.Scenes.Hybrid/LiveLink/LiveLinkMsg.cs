using System;
using System.Diagnostics;

// #define DEBUG_LIVE_LINK

namespace Unity.Scenes
{
    public static class LiveLinkMsg
    {
        public static readonly Guid ReceiveEntityChangeSet = new Guid("34d9b47f923142ff847c0d1f8b0554d9");
        public static readonly Guid UnloadScenes = new Guid("c34a0cb23efa4fae81f9f78d755cee10");
        public static readonly Guid LoadScenes = new Guid("0d0fd642461447a59c45321269cb392d");

        public static readonly Guid ConnectLiveLink = new Guid("d58c350900c24b1e99e150338fa407b5");
        //@TODO: Generate guid properly
        public static readonly Guid SetLoadedScenes = new Guid("f58c350900c24b1e99e150338fa407b6");
        public static readonly Guid ResetGame = new Guid("16a2408ca08e48758af41c5f2919d3e4");
        
        public static readonly Guid RequestAssetBundleTargetHash = new Guid("a56c8732319341c18daae030959993f4");
        public static readonly Guid ResponseAssetBundleTargetHash = new Guid("4c8f736a115f435cb576b92a6f30bd1f");
        
        public static readonly Guid RequestAssetBundleForGUID = new Guid("e078f4ebc7f24e328615ba69bcde0d48");
        public static readonly Guid ResponseAssetBundleForGUID = new Guid("68163744fe0540468d671f081cbf25cc");

        public static bool IsDebugLogging
        {
            get
            {
                #if DEBUG_LIVE_LINK
                return true;
                #else
                return false;
                #endif
            }
        }

        
        [Conditional("DEBUG_LIVE_LINK")]
        public static void LogInfo(string msg)
        {
            Debug.Log(msg);
        }
    }
}