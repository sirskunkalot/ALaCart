using BepInEx;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System;
using UnityEngine;

namespace ALaCart
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class ALaCart : BaseUnityPlugin
    {
        public const string PluginGUID = "de.sirskunkalot.ALaCart";
        public const string PluginName = "ALaCart";
        public const string PluginVersion = "0.0.3";

        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();

        private void Awake()
        {
            PrefabManager.OnVanillaPrefabsAvailable += CloneCart;
        }

        private void CloneCart()
        {
            try
            {
                var cart = new CustomPiece("GladiatorCart", "Cart", new PieceConfig
                {
                    Name = "GladiatorCart",
                    Description = "Mountable cart. Feel like a gladiator.",
                    PieceTable = PieceTables.Hammer,
                    Category = PieceCategories.Misc,
                    Requirements = new[]
                    {
                        new RequirementConfig("Wood", 1)
                    }
                });

                cart.Piece.m_craftingStation = null;
                cart.Piece.m_canBeRemoved = true;
                PieceManager.Instance.AddPiece(cart);

                var tf = cart.PiecePrefab.transform;
                DestroyImmediate(tf.Find("load").gameObject);

                var attach = new GameObject("AttachPointPlayer");
                attach.transform.SetParent(tf, false);
                attach.transform.SetAsFirstSibling();
                attach.transform.localPosition = Vector3.up * 0.5f;

                var container = tf.Find("Container").gameObject;
                DestroyImmediate(container.GetComponent<Container>());

                var chair = container.AddComponent<GladiatorCartComponent>();
                chair.AttachPoint = attach.transform;
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"Caught exception while creating cart: {ex}");
            }
            finally
            {
                PrefabManager.OnVanillaPrefabsAvailable -= CloneCart;
            }
        }
    }
}