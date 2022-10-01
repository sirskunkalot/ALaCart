// ALaCart
// a Valheim mod skeleton using Jötunn
// 
// File:    ALaCart.cs
// Project: ALaCart

using BepInEx;
using Jotunn.Entities;
using Jotunn.Managers;

namespace ALaCart
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    //[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class ALaCart : BaseUnityPlugin
    {
        public const string PluginGUID = "com.jotunn.ALaCart";
        public const string PluginName = "ALaCart";
        public const string PluginVersion = "0.0.1";
        
        // Use this class to add your own localization to the game
        // https://valheim-modding.github.io/Jotunn/tutorials/localization.html
        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();

        private void Awake()
        {
            // Jotunn comes with its own Logger class to provide a consistent Log style for all mods using it
            Jotunn.Logger.LogInfo("ALaCart has landed");
            
            // To learn more about Jotunn's features, go to
            // https://valheim-modding.github.io/Jotunn/tutorials/overview.html
        }
    }
}

