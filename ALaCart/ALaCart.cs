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

        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();

        private void Awake()
        {
            PrefabManager.OnVanillaPrefabsAvailable += CloneCart;
            PrefabManager.Instance.CreateEmptyPrefab();
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

                var attach = new GameObject(AttachTransformName);
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

    internal class GladiatorCartComponent : MonoBehaviour, Hoverable, Interactable
    {
        public string Name = "GladiatorAttach";
        public ZNetView NetView;
        public float UseDistance = 2f;
        public Transform AttachPoint;

        private float _lastSitTime;
        private Player _attachedPlayer;

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
                var num = 10f / vagon.m_bodies.Length;

                foreach (var body in vagon.m_bodies)
                {
                    body.mass = num;
                }
            }
        }

        public void Update()
        {
            if (!_attachedPlayer)
                return;

            if (!AttachPoint)
            {
                Detach();
                return;
            }

            if (ZInput.GetButtonDown("Jump"))
            {
                Detach();
                return;
            }

            _attachedPlayer.transform.position = AttachPoint.position;
        }

        private void OnDestroy()
        {
            Detach();
        }

        private void Detach()
        {
            _attachedPlayer = null;
        }

        public string GetHoverText()
        {
            if (Time.time - _lastSitTime < 2f)
                return "";

            var localPlayer = Player.m_localPlayer;

            if (!localPlayer)
                return "";

            if (!InUseDistance(localPlayer))
                return Localization.instance.Localize("<color=grey>$piece_toofar</color>");

            return Localization.instance.Localize(Name + "\n[<color=yellow><b>$KEY_Use</b></color>] $piece_use");
        }

        public string GetHoverName()
        {
            return Name;
        }

        public bool Interact(Humanoid human, bool hold, bool alt)
        {
            if (hold)
                return false;

            var player = human as Player;

            if (!player)
                return false;

            if (!AttachPoint)
                return false;

            if (!InUseDistance(player))
                return false;

            if (Time.time - _lastSitTime < 2f)
                return false;

            if (player.IsEncumbered())
                return false;

            if (_attachedPlayer && player == _attachedPlayer)
            {
                Detach();
                return true;
            }

            _attachedPlayer = player;
            _lastSitTime = Time.time;
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }

        private bool InUseDistance(Humanoid human)
        {
            if (!human || !AttachPoint)
                return false;

            return Vector3.Distance(human.transform.position, AttachPoint.position) < UseDistance;
        }
    }
}