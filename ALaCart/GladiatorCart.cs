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
                enabled = false;
                return;
            }

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
                        zdo.Set(ZdoKeyAttachedPlayer, ZDOID.None);
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
                Detach();
                return;
            }

            if (ZInput.GetButtonDown("Jump") || _attachedPlayer.IsDead())
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

        // --- Attach / Detach ---

        private void Attach(Player player)
        {
            _attachedPlayer = player;

            if (_netView && _netView.GetZDO() != null)
                _netView.InvokeRPC("ALaCart_RPC_Attach", player.GetZDOID());
        }

        private void Detach()
        {
            _attachedPlayer = null;

            if (_netView && _netView.GetZDO() != null)
                _netView.InvokeRPC("ALaCart_RPC_Detach");
        }

        private void RPC_Attach(long sender, ZDOID playerId)
        {
            var zdo = _netView.GetZDO();
            if (zdo == null)
                return;

            if (_netView.IsOwner())
                zdo.Set(ZdoKeyAttachedPlayer, playerId);
        }

        private void RPC_Detach(long sender)
        {
            var zdo = _netView.GetZDO();
            if (zdo == null)
                return;

            if (_netView.IsOwner())
                zdo.Set(ZdoKeyAttachedPlayer, ZDOID.None);
        }

        // --- State ---

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
                Detach();
                _lastSitTime = Time.time;
                return true;
            }

            if (IsInUse())
                return false;

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