using UnityEngine;

namespace ALaCart
{
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

    internal class BuffBlockTrigger : MonoBehaviour
    {
        public BuffBlockComponent BuffBlock;

        private void OnTriggerEnter(Collider other)
        {
            if (BuffBlock)
                BuffBlock.OnBuffBlockTriggerEnter(other);
        }
    }

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

        private void Awake()
        {
            _netView = GetComponent<ZNetView>();

            if (!_netView || _netView.GetZDO() == null)
            {
                enabled = false;
                return;
            }

            _netView.Register("ALaCart_RPC_BuffBlockCollected", RPC_BuffBlockCollected);
            _netView.Register("ALaCart_RPC_BuffBlockRespawn", RPC_BuffBlockRespawn);
            _netView.Register<int>("ALaCart_RPC_ApplyBuff", RPC_ApplyBuff);

            var isActive = _netView.GetZDO().GetBool(ZdoKeyIsActive, true);
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
                _netView.InvokeRPC(ZNetView.Everybody, "ALaCart_RPC_BuffBlockRespawn");
        }

        public void OnBuffBlockTriggerEnter(Collider other)
        {
            Jotunn.Logger.LogError("Entered Trigger");
            
            if (!IsActive())
                return;

            var localPlayer = Player.m_localPlayer;

            if (!localPlayer)
                return;

            var cart = other.GetComponentInParent<GladiatorCartComponent>();

            if (!cart)
                return;

            var vagon = cart.GetComponentInParent<Vagon>();

            if (!vagon)
                return;

            var puller = GetPuller(vagon);

            if (puller != localPlayer)
                return;

            var buffIndex = Random.Range(0, Buffs.Length);
            var buff = Buffs[buffIndex];

            if (buff.Target == BuffTarget.Puller || buff.Target == BuffTarget.Both)
                ApplyToPlayer(puller, buff);

            _netView.InvokeRPC(ZNetView.Everybody, "ALaCart_RPC_ApplyBuff", buffIndex);
            _netView.InvokeRPC(ZNetView.Everybody, "ALaCart_RPC_BuffBlockCollected");
        }

        private void RPC_ApplyBuff(long sender, int buffIndex)
        {
            if (buffIndex < 0 || buffIndex >= Buffs.Length)
                return;

            var localPlayer = Player.m_localPlayer;

            if (!localPlayer)
                return;

            var buff = Buffs[buffIndex];

            if (buff.Target != BuffTarget.Rider && buff.Target != BuffTarget.Both)
                return;

            var carts = FindObjectsOfType<GladiatorCartComponent>();

            foreach (var cart in carts)
            {
                if (cart.GetAttachedPlayer() == localPlayer)
                {
                    ApplyToPlayer(localPlayer, buff);
                    return;
                }
            }
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

        private void ApplyToPlayer(Player player, BuffDefinition buff)
        {
            if (!player)
                return;

            var effect = ObjectDB.instance.GetStatusEffect(buff.StatusEffect.GetStableHashCode());

            if (effect == null)
            {
                Jotunn.Logger.LogWarning($"Could not find status effect: {buff.StatusEffect}");
                return;
            }

            player.GetSEMan().AddStatusEffect(effect, true);
            player.Message(MessageHud.MessageType.Center, $"You got: {buff.Name}!");
        }

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
            if (_netView.IsOwner())
            {
                _netView.GetZDO().Set(ZdoKeyIsActive, false);
                _respawnTimer = RespawnTime;
            }

            SetVisual(false);
        }

        private void RPC_BuffBlockRespawn(long sender)
        {
            if (_netView.IsOwner())
                _netView.GetZDO().Set(ZdoKeyIsActive, true);

            SetVisual(true);
        }
    }
}