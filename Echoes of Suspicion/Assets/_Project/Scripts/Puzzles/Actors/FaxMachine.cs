using System.Collections;
using Mirror;
using UnityEngine;

namespace EOS.Puzzles
{
    /// <summary>
    /// Fax machine interactable for the Runner in the Bills puzzle.
    ///
    /// Flow:
    /// 1. Runner approaches with a receipt in active inventory slot.
    /// 2. Prompt shows "Enviar [receipt name]".
    /// 3. On interact: receipt is removed from inventory, paper model slides
    ///    into the machine, fax sound plays, noise is generated.
    /// 4. After sendDuration, the server raises OnReceiptSent so
    ///    BillsPuzzleCoordinator can notify the Guide.
    ///
    /// If no FaxMachineModel child exists at Awake, a procedural model is
    /// built from primitives (box body + paper slot + indicator light).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class FaxMachine : RatInteractable
    {
        [Header("Send Settings")]

        [SerializeField, Min(0.5f)]
        private float sendDuration = 2.5f;

        [SerializeField]
        private AudioClip faxSendSound;

        [SerializeField]
        private AudioClip faxCompleteSound;

        [Header("Paper Animation")]

        [SerializeField, Tooltip("Transform where the paper starts (above the slot). " +
                                 "Auto-created if null.")]
        private Transform paperStartPoint;

        [SerializeField, Tooltip("Transform where the paper ends (inside the machine). " +
                                 "Auto-created if null.")]
        private Transform paperEndPoint;

        [SerializeField, Tooltip("Paper visual used for the send animation. " +
                                 "Auto-created if null.")]
        private Transform paperVisual;

        [Header("Indicator Light")]

        [SerializeField, Tooltip("Light that blinks during send. Auto-created if null.")]
        private Light indicatorLight;

        [SerializeField]
        private Color idleColor = new Color(0.2f, 0.8f, 0.2f);

        [SerializeField]
        private Color sendingColor = new Color(1f, 0.6f, 0f);

        [Header("Noise")]

        [SerializeField]
        private NoiseLevel sendNoiseLevel = NoiseLevel.Medium;

        // ── State ────────────────────────────────────────────

        [SyncVar]
        private bool _isSending;

        private AudioSource _audioSource;
        private Coroutine _sendRoutine;

        // Cached receipt id from last send (server only).
        private string _lastSentReceiptId;
        private ReceiptData _lastSentReceiptData;

        /// <summary>
        /// Fired on the server when a receipt finishes sending.
        /// BillsPuzzleCoordinator subscribes to this.
        /// </summary>
        public event System.Action<string, ReceiptData> OnReceiptSent;

        // ── Lifecycle ────────────────────────────────────────

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();

            _audioSource.spatialBlend = 1f;
            _audioSource.playOnAwake = false;
            _audioSource.maxDistance = 15f;

            if (paperVisual == null || paperStartPoint == null || paperEndPoint == null)
                BuildProceduralModel();

            if (paperVisual != null)
                paperVisual.gameObject.SetActive(false);

            RefreshPrompt(null);
        }

        // ── Interaction ──────────────────────────────────────

        public override bool CanPreviewInteraction(GameObject interactor)
        {
            if (_isSending) return false;
            return FindActiveReceipt(interactor) != null;
        }

        public override string GetInteractionPrompt(GameObject interactor)
        {
            if (_isSending) return "Enviando...";

            var receipt = FindActiveReceipt(interactor);
            if (receipt != null)
                return $"Enviar {receipt.ReceiptDisplayName}";

            return "Fax (necesitas un recibo)";
        }

        [Server]
        public override bool CanServerInteract(NetworkIdentity interactor)
        {
            if (_isSending || interactor == null) return false;
            return FindActiveReceiptServer(interactor) != null;
        }

        [Server]
        public override void ServerInteract(NetworkIdentity interactor)
        {
            if (!CanServerInteract(interactor)) return;

            var inventory = interactor.GetComponent<NetworkInventory>();
            if (inventory == null) return;

            // Find the receipt in the active slot.
            InventorySlot slot = inventory.ActiveSlot;
            if (slot.IsEmpty || slot.itemNetId == 0) return;

            if (!NetworkServer.spawned.TryGetValue(slot.itemNetId, out NetworkIdentity itemIdentity))
                return;

            ReceiptData receipt = itemIdentity.GetComponent<ReceiptData>();
            if (receipt == null) return;

            // Cache receipt info before removing from inventory.
            _lastSentReceiptId = receipt.ReceiptId;
            _lastSentReceiptData = receipt;

            // Remove from inventory (the world object stays hidden).
            inventory.ServerRemoveItem(inventory.ActiveSlotIndex);

            // Start the send sequence.
            _isSending = true;

            if (_sendRoutine != null)
                StopCoroutine(_sendRoutine);

            _sendRoutine = StartCoroutine(ServerSendSequence(itemIdentity));
        }

        [Server]
        private IEnumerator ServerSendSequence(NetworkIdentity receiptObject)
        {
            // Notify all clients to play the animation.
            RpcPlaySendAnimation(sendDuration);

            // Generate noise — the fax is loud.
            PuzzleEvents.RaiseNoiseGenerated(transform.position, sendNoiseLevel);

            yield return new WaitForSeconds(sendDuration);

            // Send complete.
            _isSending = false;

            RpcPlaySendComplete();

            // Destroy the receipt world object — it's been "faxed".
            if (receiptObject != null)
                NetworkServer.Destroy(receiptObject.gameObject);

            // Notify the coordinator.
            OnReceiptSent?.Invoke(_lastSentReceiptId, _lastSentReceiptData);
            _lastSentReceiptId = null;
            _lastSentReceiptData = null;

            _sendRoutine = null;
        }

        // ── Client RPCs ──────────────────────────────────────

        [ClientRpc]
        private void RpcPlaySendAnimation(float duration)
        {
            StartCoroutine(ClientPaperAnimation(duration));

            if (faxSendSound != null)
                _audioSource.PlayOneShot(faxSendSound);

            if (indicatorLight != null)
                indicatorLight.color = sendingColor;
        }

        [ClientRpc]
        private void RpcPlaySendComplete()
        {
            if (faxCompleteSound != null)
                _audioSource.PlayOneShot(faxCompleteSound);

            if (indicatorLight != null)
                indicatorLight.color = idleColor;

            if (paperVisual != null)
                paperVisual.gameObject.SetActive(false);
        }

        private IEnumerator ClientPaperAnimation(float duration)
        {
            if (paperVisual == null || paperStartPoint == null || paperEndPoint == null)
                yield break;

            paperVisual.gameObject.SetActive(true);
            paperVisual.position = paperStartPoint.position;
            paperVisual.rotation = paperStartPoint.rotation;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

                paperVisual.position = Vector3.Lerp(
                    paperStartPoint.position,
                    paperEndPoint.position,
                    t);

                paperVisual.rotation = Quaternion.Slerp(
                    paperStartPoint.rotation,
                    paperEndPoint.rotation,
                    t);

                // Blink indicator light.
                if (indicatorLight != null)
                {
                    float blink = Mathf.Sin(elapsed * 8f) > 0f ? 1f : 0.3f;
                    indicatorLight.intensity = blink;
                }

                yield return null;
            }

            paperVisual.gameObject.SetActive(false);

            if (indicatorLight != null)
                indicatorLight.intensity = 1f;
        }

        // ── Helpers ──────────────────────────────────────────

        /// <summary>Client-side receipt lookup (for prompt display).</summary>
        private ReceiptData FindActiveReceipt(GameObject interactor)
        {
            if (interactor == null) return null;

            var inventory = interactor.GetComponent<NetworkInventory>();
            if (inventory == null) return null;

            InventorySlot slot = inventory.ActiveSlot;
            if (slot.IsEmpty || slot.itemNetId == 0) return null;

            if (!NetworkClient.spawned.TryGetValue(slot.itemNetId, out NetworkIdentity identity))
                return null;

            return identity.GetComponent<ReceiptData>();
        }

        /// <summary>Server-side receipt lookup.</summary>
        [Server]
        private ReceiptData FindActiveReceiptServer(NetworkIdentity interactor)
        {
            var inventory = interactor.GetComponent<NetworkInventory>();
            if (inventory == null) return null;

            InventorySlot slot = inventory.ActiveSlot;
            if (slot.IsEmpty || slot.itemNetId == 0) return null;

            if (!NetworkServer.spawned.TryGetValue(slot.itemNetId, out NetworkIdentity identity))
                return null;

            return identity.GetComponent<ReceiptData>();
        }

        private void RefreshPrompt(ReceiptData receipt)
        {
            interactionPrompt = receipt != null
                ? $"Enviar {receipt.ReceiptDisplayName}"
                : "Fax";
        }

        // ── Procedural Model ─────────────────────────────────
        //
        // Builds a basic fax machine from Unity primitives if no
        // model is assigned. Good enough for programmer art / MVP.

        private void BuildProceduralModel()
        {
            // ── Body (main box) ──
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "FaxBody";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(0.5f, 0.2f, 0.35f);

            var bodyRenderer = body.GetComponent<Renderer>();
            if (bodyRenderer != null)
            {
                bodyRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                bodyRenderer.material.color = new Color(0.25f, 0.25f, 0.28f);
            }

            // Remove collider from child — the parent should have its own.
            var bodyCol = body.GetComponent<Collider>();
            if (bodyCol != null) Destroy(bodyCol);

            // ── Paper tray (back raised section) ──
            GameObject tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tray.name = "FaxTray";
            tray.transform.SetParent(transform, false);
            tray.transform.localPosition = new Vector3(0f, 0.12f, -0.1f);
            tray.transform.localScale = new Vector3(0.4f, 0.04f, 0.15f);
            tray.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);

            var trayRenderer = tray.GetComponent<Renderer>();
            if (trayRenderer != null)
            {
                trayRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                trayRenderer.material.color = new Color(0.2f, 0.2f, 0.22f);
            }

            var trayCol = tray.GetComponent<Collider>();
            if (trayCol != null) Destroy(trayCol);

            // ── Paper visual (thin white quad) ──
            GameObject paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
            paper.name = "FaxPaper";
            paper.transform.SetParent(transform, false);
            paper.transform.localScale = new Vector3(0.3f, 0.005f, 0.4f);

            var paperRenderer = paper.GetComponent<Renderer>();
            if (paperRenderer != null)
            {
                paperRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                paperRenderer.material.color = new Color(0.95f, 0.93f, 0.88f);
            }

            var paperCol = paper.GetComponent<Collider>();
            if (paperCol != null) Destroy(paperCol);

            paperVisual = paper.transform;
            paper.SetActive(false);

            // ── Start / End points for animation ──
            GameObject start = new GameObject("PaperStart");
            start.transform.SetParent(transform, false);
            start.transform.localPosition = new Vector3(0f, 0.18f, -0.18f);
            start.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            paperStartPoint = start.transform;

            GameObject end = new GameObject("PaperEnd");
            end.transform.SetParent(transform, false);
            end.transform.localPosition = new Vector3(0f, 0.1f, 0.08f);
            paperEndPoint = end.transform;

            // ── Indicator light ──
            GameObject lightObj = new GameObject("FaxIndicator");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = new Vector3(0.18f, 0.11f, 0.12f);

            indicatorLight = lightObj.AddComponent<Light>();
            indicatorLight.type = LightType.Point;
            indicatorLight.range = 0.5f;
            indicatorLight.intensity = 1f;
            indicatorLight.color = idleColor;

            // Small sphere to visualize the light.
            GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = "Bulb";
            bulb.transform.SetParent(lightObj.transform, false);
            bulb.transform.localScale = Vector3.one * 0.03f;

            var bulbRenderer = bulb.GetComponent<Renderer>();
            if (bulbRenderer != null)
            {
                bulbRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                bulbRenderer.material.color = idleColor;
                bulbRenderer.material.EnableKeyword("_EMISSION");
                bulbRenderer.material.SetColor("_EmissionColor", idleColor * 2f);
            }

            var bulbCol = bulb.GetComponent<Collider>();
            if (bulbCol != null) Destroy(bulbCol);

            // ── Main collider on parent ──
            if (GetComponent<Collider>() == null)
            {
                BoxCollider mainCol = gameObject.AddComponent<BoxCollider>();
                mainCol.center = new Vector3(0f, 0.05f, 0f);
                mainCol.size = new Vector3(0.55f, 0.3f, 0.4f);
            }
        }
    }
}
