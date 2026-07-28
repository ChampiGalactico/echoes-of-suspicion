using Mirror;
using UnityEngine;

namespace EOS.Puzzles
{
    /// <summary>
    /// Feedback visual y sonoro para cada slot de reparación del carro.
    /// Se coloca junto a SlotActor + SlotActorInteractable en cada caja
    /// invisible sobre el capó.
    ///
    /// Conectar desde el Inspector:
    ///   LeafPuzzle.OnPuzzleSolved  -> HandleSuccess()
    ///   LeafPuzzle.OnPuzzleFailed  -> HandleFailure()
    ///   LeafPuzzle.OnPuzzleReset   -> HandleReset()
    /// </summary>
    public class CarRepairFeedback : NetworkBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private SlotActor _slot;
        [SerializeField] private Collider _interactionCollider;

        [Header("Éxito")]
        [SerializeField] private AudioClip _successSound;
        [SerializeField] private GameObject _successVFX;

        [Header("Fallo")]
        [SerializeField] private AudioClip _failSound;
        [SerializeField] private AudioClip _explosionSound;
        [SerializeField] private GameObject _failVFX;
        [SerializeField] private Light _redLight;
        [SerializeField] private float _redLightDuration = 1.5f;

        [Header("Daño al Runner")]
        [SerializeField] private float _runnerDamage = 10f;

        private AudioSource _audioSource;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();

            _audioSource.spatialBlend = 1f;

            if (_redLight != null)
                _redLight.enabled = false;
        }

        // ─── Wired to LeafPuzzle.OnPuzzleSolved (fires on all clients via RPC) ───

        public void HandleSuccess()
        {
            PlayEffect(_successVFX);
            PlaySound(_successSound);

            if (_interactionCollider != null)
                _interactionCollider.enabled = false;

            if (isServer)
                ServerHandleSuccess();
        }

        [Server]
        private void ServerHandleSuccess()
        {
            // El slot queda con el item correcto; no limpiar.
            // Nada más que hacer server-side por ahora.
        }

        // ─── Wired to LeafPuzzle.OnPuzzleFailed (fires on all clients via RPC) ───

        public void HandleFailure()
        {
            PlayEffect(_failVFX);
            PlaySound(_failSound);

            if (_explosionSound != null)
            {
                // El sonido de explosión se reproduce con un pequeño delay
                // para que primero suene el error y luego el golpe.
                float delay = _failSound != null ? _failSound.length * 0.6f : 0f;
                _audioSource.clip = _explosionSound;
                _audioSource.PlayDelayed(delay);
            }

            if (_redLight != null)
            {
                _redLight.enabled = true;
                Invoke(nameof(TurnOffRedLight), _redLightDuration);
            }

            if (isServer)
                ServerHandleFailure();
        }

        [Server]
        private void ServerHandleFailure()
        {
            // Daño al Runner.
            var runnerProvider = PlayerUtils.FindPlayerByRole(PlayerRole.Runner);
            if (runnerProvider != null)
            {
                var health = runnerProvider.GetComponent<PlayerHealth>();
                if (health != null)
                    health.TakeDamage(_runnerDamage);
            }

            // Devolver el item al mundo y limpiar el slot.
            // Se hace con un pequeño delay para que el feedback visual
            // se alcance a ver antes de que el item desaparezca del slot.
            Invoke(nameof(ServerClearSlot), 1f);
        }

        [Server]
        private void ServerClearSlot()
        {
            if (_slot == null || !_slot.HasItem) return;

            PickableItem item = _slot.Clear();
            if (item == null) return;

            var pickup = item.GetComponent<NetworkPickupItem>();
            if (pickup != null)
                pickup.ReturnToOrigin();
        }

        // ─── Wired to LeafPuzzle.OnPuzzleReset (fires on all clients via RPC) ───

        public void HandleReset()
        {
            if (_interactionCollider != null)
                _interactionCollider.enabled = true;

            TurnOffRedLight();
        }

        // ─── Helpers ───

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
                _audioSource.PlayOneShot(clip);
        }

        private void PlayEffect(GameObject vfxPrefab)
        {
            if (vfxPrefab == null) return;

            var instance = Instantiate(vfxPrefab, transform.position, Quaternion.identity);
            Destroy(instance, 4f);
        }

        private void TurnOffRedLight()
        {
            if (_redLight != null)
                _redLight.enabled = false;
        }
    }
}
