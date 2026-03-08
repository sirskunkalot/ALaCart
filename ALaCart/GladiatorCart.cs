using UnityEngine;

namespace ALaCart
{
    internal class GladiatorCartComponent : MonoBehaviour, Hoverable, Interactable
    {
        public string Name = "GladiatorAttach";
        public float UseDistance = 2f;
        public Transform AttachPoint;

        private const string ZdoKeyAttachedPlayer = "ALaCart_AttachedPlayer";

        private ZNetView _netView;
        private float _lastSitTime;
        private Player _attachedPlayer;

        // --- Lifecycle ---

        public void Awake()
        {
            _netView = gameObject.GetComponentInParent<ZNetView>();

            if (_netView.GetZDO() == null)
            {
                ALaCart.DebugLog("Cart Awake - no ZDO, disabling");
                enabled = false;
                return;
            }

            ALaCart.DebugLog($"Cart Awake - ZDO: {_netView.GetZDO().m_uid}, Owner: {_netView.IsOwner()}");

            _netView.Register<ZDOID>("ALaCart_RPC_Attach", RPC_Attach);
            _netView.Register("ALaCart_RPC_Detach", RPC_Detach);

            if (_netView.IsOwner())
            {
                var zdo = _netView.GetZDO();
                var attachedId = zdo.GetZDOID(ZdoKeyAttachedPlayer);

                if (attachedId != ZDOID.None)
                {
                    var playerObject = ZNetScene.instance.FindInstance(attachedId);

                    if (!playerObject)
                    {
                        ALaCart.DebugLog($"Cart Awake - Clearing stale attachment: {attachedId}");
                        zdo.Set(ZdoKeyAttachedPlayer, ZDOID.None);
                    }
                }

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
                ALaCart.DebugLog("Cart Update - AttachPoint lost, detaching");
                Detach();
                return;
            }

            if (ZInput.GetButtonDown("Jump") || _attachedPlayer.IsDead())
            {
                ALaCart.DebugLog($"Cart Update - Detaching (Jump: {ZInput.GetButtonDown("Jump")}, Dead: {_attachedPlayer.IsDead()})");
                Detach();
                return;
            }

            _attachedPlayer.transform.position = AttachPoint.position;
        }

        private void OnDestroy()
        {
            ALaCart.DebugLog("Cart OnDestroy");
            Detach();
        }

        // --- Attach / Detach ---

        private void Attach(Player player)
        {
            ALaCart.DebugLog($"Cart Attach - Player: {player.GetPlayerName()}, ZDOID: {player.GetZDOID()}");
            _attachedPlayer = player;

            if (_netView && _netView.GetZDO() != null)
                _netView.InvokeRPC("ALaCart_RPC_Attach", player.GetZDOID());
        }

        private void Detach()
        {
            ALaCart.DebugLog($"Cart Detach - Player: {_attachedPlayer?.GetPlayerName() ?? "none"}");
            _attachedPlayer = null;

            if (_netView && _netView.GetZDO() != null)
                _netView.InvokeRPC("ALaCart_RPC_Detach");
        }

        private void RPC_Attach(long sender, ZDOID playerId)
        {
            ALaCart.DebugLog($"Cart RPC_Attach - sender: {sender}, playerId: {playerId}, IsOwner: {_netView.IsOwner()}");

            var zdo = _netView.GetZDO();
            if (zdo == null)
                return;

            if (_netView.IsOwner())
                zdo.Set(ZdoKeyAttachedPlayer, playerId);
        }

        private void RPC_Detach(long sender)
        {
            ALaCart.DebugLog($"Cart RPC_Detach - sender: {sender}, IsOwner: {_netView.IsOwner()}");

            var zdo = _netView.GetZDO();
            if (zdo == null)
                return;

            if (_netView.IsOwner())
                zdo.Set(ZdoKeyAttachedPlayer, ZDOID.None);
        }

        // --- State ---

        public Player GetAttachedPlayer()
        {
            return _attachedPlayer;
        }

        public bool IsLocalPlayerAttached()
        {
            return _attachedPlayer && _attachedPlayer == Player.m_localPlayer;
        }

        private bool IsInUse()
        {
            var zdo = _netView.GetZDO();
            if (zdo == null)
                return false;

            return zdo.GetZDOID(ZdoKeyAttachedPlayer) != ZDOID.None;
        }

        // --- Interaction ---

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

            if (_attachedPlayer && player == _attachedPlayer)
            {
                ALaCart.DebugLog($"Cart Interact - Detaching player: {player.GetPlayerName()}");
                Detach();
                _lastSitTime = Time.time;
                return true;
            }

            if (IsInUse())
            {
                ALaCart.DebugLog("Cart Interact - Already in use");
                return false;
            }

            ALaCart.DebugLog($"Cart Interact - Attaching player: {player.GetPlayerName()}");
            Attach(player);
            _lastSitTime = Time.time;
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }

        // --- Hover ---

        public string GetHoverText()
        {
            if (Time.time - _lastSitTime < 2f)
                return "";

            var localPlayer = Player.m_localPlayer;

            if (!localPlayer)
                return "";

            if (!InUseDistance(localPlayer))
                return Localization.instance.Localize("<color=grey>$piece_toofar</color>");

            if (!_attachedPlayer && IsInUse())
                return Localization.instance.Localize("<color=grey>In use</color>");

            return Localization.instance.Localize(Name + "\n[<color=yellow><b>$KEY_Use</b></color>] $piece_use");
        }

        public string GetHoverName()
        {
            return Name;
        }

        // --- Utility ---

        private bool InUseDistance(Humanoid human)
        {
            if (!human || !AttachPoint)
                return false;

            return Vector3.Distance(human.transform.position, AttachPoint.position) < UseDistance;
        }
    }
}