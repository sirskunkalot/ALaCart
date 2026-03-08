using UnityEngine;

namespace ALaCart
{
    internal enum BuffTarget
    {
        Puller,
        Rider,
        Both
    }

    internal class BuffDefinition
    {
        public string Name;
        public string StatusEffect;
        public BuffTarget Target;

        public BuffDefinition(string name, string statusEffect, BuffTarget target)
        {
            Name = name;
            StatusEffect = statusEffect;
            Target = target;
        }
    }

    internal class BuffBlockComponent : MonoBehaviour
    {
        public GameObject Visual;
        public float RespawnTime = 10f;

        private const string ZdoKeyIsActive = "ALaCart_BuffBlockActive";

        private ZNetView _netView;
        private float _respawnTimer;

        private static readonly BuffDefinition[] Buffs =
        {
            new BuffDefinition("Speed Boost", "SE_Wind", BuffTarget.Puller),
            new BuffDefinition("Stamina Regen", "SE_Rested", BuffTarget.Puller),

            new BuffDefinition("Shield", "SE_Shield", BuffTarget.Rider),
            new BuffDefinition("Health Regen", "SE_Potion_healthmedium", BuffTarget.Rider),
        };

        // --- Lifecycle ---

        private void Awake()
        {
            _netView = GetComponent<ZNetView>();

            if (!_netView || _netView.GetZDO() == null)
            {
                ALaCart.DebugLog("BuffBlock Awake - no ZNetView or ZDO, disabling");
                enabled = false;
                return;
            }

            ALaCart.DebugLog($"BuffBlock Awake - ZDO: {_netView.GetZDO().m_uid}, Owner: {_netView.IsOwner()}");

            _netView.Register("ALaCart_RPC_BuffBlockCollected", RPC_BuffBlockCollected);
            _netView.Register("ALaCart_RPC_BuffBlockRespawn", RPC_BuffBlockRespawn);
            _netView.Register<int, ZDOID>("ALaCart_RPC_ApplyBuff", RPC_ApplyBuff);

            var isActive = _netView.GetZDO().GetBool(ZdoKeyIsActive, true);
            ALaCart.DebugLog($"BuffBlock Awake - IsActive: {isActive}");
            SetVisual(isActive);
        }

        private void Update()
        {
            if (!_netView.IsOwner())
                return;

            if (IsActive())
                return;

            _respawnTimer -= Time.deltaTime;

            if (_respawnTimer <= 0f)
            {
                ALaCart.DebugLog("BuffBlock - Respawn timer expired, sending respawn RPC");
                _netView.InvokeRPC(ZNetView.Everybody, "ALaCart_RPC_BuffBlockRespawn");
            }
        }

        // --- Collection ---

        public void OnBuffBlockTriggerEnter(Collider other)
        {
            ALaCart.DebugLog($"BuffBlock trigger entered by: {other.name} (parent: {other.transform.root.name})");

            if (!IsActive())
            {
                ALaCart.DebugLog("BuffBlock - Not active, ignoring trigger");
                return;
            }

            var localPlayer = Player.m_localPlayer;

            if (!localPlayer)
            {
                ALaCart.DebugLog("BuffBlock - No local player");
                return;
            }

            var cart = other.GetComponentInParent<GladiatorCartComponent>();

            if (!cart)
            {
                ALaCart.DebugLog("BuffBlock - No cart found on collider");
                return;
            }

            var vagon = cart.GetComponentInParent<Vagon>();

            if (!vagon)
            {
                ALaCart.DebugLog("BuffBlock - No vagon found on cart");
                return;
            }

            var puller = GetPuller(vagon);

            ALaCart.DebugLog($"BuffBlock - Puller: {puller?.GetPlayerName() ?? "none"}, LocalPlayer: {localPlayer.GetPlayerName()}");

            if (puller != localPlayer)
            {
                ALaCart.DebugLog("BuffBlock - Local player is not the puller, ignoring");
                return;
            }

            var cartNetView = cart.GetComponentInParent<ZNetView>();

            if (!cartNetView)
            {
                ALaCart.DebugLog("BuffBlock - No ZNetView on cart");
                return;
            }

            var cartId = cartNetView.GetZDO().m_uid;
            var buffIndex = Random.Range(0, Buffs.Length);

            ALaCart.DebugLog($"BuffBlock - Collecting! Buff: {Buffs[buffIndex].Name} (index: {buffIndex}), CartId: {cartId}, Target: {Buffs[buffIndex].Target}");

            _netView.InvokeRPC(ZNetView.Everybody, "ALaCart_RPC_ApplyBuff", buffIndex, cartId);
            _netView.InvokeRPC(ZNetView.Everybody, "ALaCart_RPC_BuffBlockCollected");
        }

        private Player GetPuller(Vagon vagon)
        {
            foreach (var player in Player.GetAllPlayers())
            {
                if (vagon.IsAttached(player))
                    return player;
            }

            return null;
        }

        // --- Buff Application ---

        private void RPC_ApplyBuff(long sender, int buffIndex, ZDOID cartId)
        {
            ALaCart.DebugLog($"BuffBlock RPC_ApplyBuff - sender: {sender}, buffIndex: {buffIndex}, cartId: {cartId}");

            if (buffIndex < 0 || buffIndex >= Buffs.Length)
            {
                Jotunn.Logger.LogWarning($"BuffBlock RPC_ApplyBuff - Invalid buff index: {buffIndex}");
                return;
            }

            var localPlayer = Player.m_localPlayer;

            if (!localPlayer)
            {
                ALaCart.DebugLog("BuffBlock RPC_ApplyBuff - No local player");
                return;
            }

            var cartObject = ZNetScene.instance.FindInstance(cartId);

            if (!cartObject)
            {
                ALaCart.DebugLog($"BuffBlock RPC_ApplyBuff - Cart not found for ZDOID: {cartId}");
                return;
            }

            var cart = cartObject.GetComponentInChildren<GladiatorCartComponent>();

            if (!cart)
            {
                ALaCart.DebugLog("BuffBlock RPC_ApplyBuff - No GladiatorCartComponent on cart");
                return;
            }

            var vagon = cart.GetComponentInParent<Vagon>();

            if (!vagon)
            {
                ALaCart.DebugLog("BuffBlock RPC_ApplyBuff - No Vagon on cart");
                return;
            }

            var buff = Buffs[buffIndex];
            var isPuller = vagon.IsAttached(localPlayer);
            var isRider = cart.GetAttachedPlayer() == localPlayer;

            ALaCart.DebugLog($"BuffBlock RPC_ApplyBuff - Player: {localPlayer.GetPlayerName()}, IsPuller: {isPuller}, IsRider: {isRider}, BuffTarget: {buff.Target}");

            switch (buff.Target)
            {
                case BuffTarget.Puller:
                    if (isPuller)
                        ApplyToPlayer(localPlayer, buff);
                    else
                        ALaCart.DebugLog("BuffBlock RPC_ApplyBuff - Puller buff but not puller, skipping");
                    break;

                case BuffTarget.Rider:
                    if (isRider)
                        ApplyToPlayer(localPlayer, buff);
                    else
                        ALaCart.DebugLog("BuffBlock RPC_ApplyBuff - Rider buff but not rider, skipping");
                    break;

                case BuffTarget.Both:
                    if (isPuller || isRider)
                        ApplyToPlayer(localPlayer, buff);
                    else
                        ALaCart.DebugLog("BuffBlock RPC_ApplyBuff - Both buff but neither puller nor rider, skipping");
                    break;
            }
        }

        private void ApplyToPlayer(Player player, BuffDefinition buff)
        {
            if (!player)
                return;

            var effect = ObjectDB.instance.GetStatusEffect(buff.StatusEffect.GetStableHashCode());

            if (effect == null)
            {
                Jotunn.Logger.LogWarning($"BuffBlock ApplyToPlayer - Could not find status effect: {buff.StatusEffect}");
                return;
            }

            ALaCart.DebugLog($"BuffBlock ApplyToPlayer - Applying {buff.Name} ({buff.StatusEffect}) to {player.GetPlayerName()}");

            player.GetSEMan().AddStatusEffect(effect, true);
            player.Message(MessageHud.MessageType.Center, $"You got: {buff.Name}!");
        }

        // --- Visual State ---

        private bool IsActive()
        {
            var zdo = _netView.GetZDO();
            if (zdo == null)
                return false;

            return zdo.GetBool(ZdoKeyIsActive, true);
        }

        private void SetVisual(bool active)
        {
            if (Visual)
                Visual.SetActive(active);
        }

        private void RPC_BuffBlockCollected(long sender)
        {
            ALaCart.DebugLog($"BuffBlock RPC_BuffBlockCollected - sender: {sender}, IsOwner: {_netView.IsOwner()}");

            if (_netView.IsOwner())
            {
                _netView.GetZDO().Set(ZdoKeyIsActive, false);
                _respawnTimer = RespawnTime;
            }

            SetVisual(false);
        }

        private void RPC_BuffBlockRespawn(long sender)
        {
            ALaCart.DebugLog($"BuffBlock RPC_BuffBlockRespawn - sender: {sender}, IsOwner: {_netView.IsOwner()}");

            if (_netView.IsOwner())
                _netView.GetZDO().Set(ZdoKeyIsActive, true);

            SetVisual(true);
        }
    }

    internal class BuffBlockTrigger : MonoBehaviour
    {
        public BuffBlockComponent BuffBlock;

        private void OnTriggerEnter(Collider other)
        {
            if (BuffBlock)
                BuffBlock.OnBuffBlockTriggerEnter(other);
        }
    }

    internal class BuffBlockSpin : MonoBehaviour
    {
        public float RotationSpeed = 50f;
        public float BobSpeed = 2f;
        public float BobHeight = 0.15f;

        private Vector3 _startPosition;

        private void Start()
        {
            _startPosition = transform.localPosition;
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, RotationSpeed * Time.deltaTime);
            var bob = Mathf.Sin(Time.time * BobSpeed) * BobHeight;
            transform.localPosition = _startPosition + Vector3.up * bob;
        }
    }
}