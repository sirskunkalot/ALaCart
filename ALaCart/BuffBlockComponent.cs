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

    internal class BuffBlockComponent : MonoBehaviour
    {
        public GameObject Visual;
        public float RespawnTime = 10f;

        private const string ZdoKeyIsActive = "ALaCart_BuffBlockActive";

        private ZNetView _netView;
        private float _respawnTimer;

        private static readonly string[] BuffNames =
        {
            "SE_Rested",
            "SE_Wind",
            "SE_Shield",
            "SE_Potion_healthmedium"
        };

        private void Awake()
        {
            _netView = GetComponent<ZNetView>();

            if (_netView == null || _netView.GetZDO() == null)
            {
                enabled = false;
                return;
            }

            _netView.Register("ALaCart_RPC_BuffBlockCollected", RPC_BuffBlockCollected);
            _netView.Register("ALaCart_RPC_BuffBlockRespawn", RPC_BuffBlockRespawn);

            var isActive = _netView.GetZDO().GetBool(ZdoKeyIsActive, true);
            SetVisual(isActive);
        }

        private void Update()
        {
            if (!_netView)
                return;
            
            if (!_netView.IsOwner())
                return;

            if (IsActive())
                return;

            _respawnTimer -= Time.deltaTime;

            if (_respawnTimer <= 0f)
                _netView.InvokeRPC(ZNetView.Everybody, "ALaCart_RPC_BuffBlockRespawn");
        }

        private void OnTriggerEnter(Collider other)
        {
            Jotunn.Logger.LogInfo($"Entered trigger {other}");
            if (!IsActive())
                return;

            var localPlayer = Player.m_localPlayer;

            if (!localPlayer)
                return;

            var player = other.GetComponentInParent<Player>();
            if (player != localPlayer)
                return;

            ApplyRandomBuff(localPlayer);
            _netView.InvokeRPC(ZNetView.Everybody, "ALaCart_RPC_BuffBlockCollected");
        }

        private void ApplyRandomBuff(Player player)
        {
            var buffName = BuffNames[UnityEngine.Random.Range(0, BuffNames.Length)];
            var effect = ObjectDB.instance.GetStatusEffect(buffName.GetStableHashCode());

            if (effect != null)
            {
                player.GetSEMan().AddStatusEffect(effect, true);
                player.Message(MessageHud.MessageType.Center, $"You got: {effect.m_name}!");
            }
            else
            {
                Jotunn.Logger.LogWarning($"Could not find status effect: {buffName}");
            }
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