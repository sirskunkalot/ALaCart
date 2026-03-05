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
        public const string PluginVersion = "0.0.2";

        public const string AttachTransformName = "ALaCart_AttachPointPlayer";

        // Use this class to add your own localization to the game
        // https://valheim-modding.github.io/Jotunn/tutorials/localization.html
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
                    PieceTable = "Hammer",
                    Requirements = new[]
                    {
                        new RequirementConfig("Wood", 1)
                    }
                });
                PieceManager.Instance.AddPiece(cart);

                var tf = cart.PiecePrefab.transform;
                DestroyImmediate(tf.Find("load").gameObject);
                tf.GetComponent<Rigidbody>().mass = 10;

                var attach = new GameObject(AttachTransformName);
                attach.transform.SetParent(tf, false);
                attach.transform.SetAsFirstSibling();
                attach.transform.position += Vector3.up * 0.5f;

                var container = tf.Find("Container").gameObject;
                DestroyImmediate(container.GetComponent<Container>());
                var chair = container.AddComponent<GladiatorCartComponent>();
                chair.AttachPoint = attach.transform;
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"Catched exception while creating cart: {ex}");
            }
            finally
            {
                PrefabManager.OnVanillaPrefabsAvailable -= CloneCart;
            }
        }
    }

    internal class GladiatorCartComponent : MonoBehaviour, Hoverable, Interactable
    {
        public string Name = "GladiatorAttach";

        public ZNetView NetView;

        public float UseDistance = 2f;

        private static float LastSitTime;

        public Transform AttachPoint;

        private Player AttachedPlayer;

        public void Awake()
        {
            NetView = gameObject.GetComponentInParent<ZNetView>();

            if (NetView.GetZDO() == null)
            {
                enabled = false;
            }
            else if (NetView.IsOwner())
            {
                var vagon = GetComponentInParent<Vagon>();
                float num = 10f / (float)vagon.m_bodies.Length;
                foreach (Rigidbody body in vagon.m_bodies)
                {
                    body.mass = num;
                }
            }
        }

        public void Update()
        {
            if (!AttachedPlayer)
            {
                return;
            }

            if (ZInput.GetButtonDown("Jump"))
            {
                AttachedPlayer = null;
                return;
            }

            AttachedPlayer.transform.position = AttachPoint.position;
        }

        public string GetHoverText()
        {
            if (Time.time - LastSitTime < 2f)
            {
                return "";
            }
            if (!InUseDistance(Player.m_localPlayer))
            {
                return Localization.instance.Localize("<color=grey>$piece_toofar</color>");
            }
            return Localization.instance.Localize(Name + "\n[<color=yellow><b>$KEY_Use</b></color>] $piece_use");
        }

        public string GetHoverName()
        {
            return Name;
        }

        public bool Interact(Humanoid human, bool hold, bool alt)
        {
            if (hold)
            {
                return false;
            }
            Player player = human as Player;
            if (!InUseDistance(player))
            {
                return false;
            }
            if (Time.time - LastSitTime < 2f)
            {
                return false;
            }
            if (player)
            {
                if (player.IsEncumbered())
                {
                    return false;
                }
                if (AttachedPlayer && player == AttachedPlayer)
                {
                    AttachedPlayer = null;
                    return false;
                }
                AttachedPlayer = player;
                LastSitTime = Time.time;
            }
            return false;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }

        private bool InUseDistance(Humanoid human)
        {
            return Vector3.Distance(human.transform.position, AttachPoint.position) < UseDistance;
        }
    }
}

