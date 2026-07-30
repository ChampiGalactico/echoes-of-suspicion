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
    /// 2. Prompt shows "Enviar [DisplayName]".
    /// 3. On interact: receipt removed from inventory, tag checked.
    ///    - Tag matches current round → send animation, OnReceiptSent fires,
    ///      receipt destroyed.
    ///    - Tag mismatch → reject animation (red light), receipt drops
    ///      on the floor near the fax for the Runner to pick up.
    ///
    /// BillsPuzzleCoordinator calls SetAcceptedTag() each round. While no
    /// tag is set the fax is inactive (no prompt, no interaction).
    ///
    /// Item filter uses receiptTagPrefix ("Receipt") for broad prompt
    /// visibility — any item with ItemTag starting with "Receipt" shows
    /// the insert prompt. The exact tag match happens server-side.
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

        [Header("Reject Settings")]

        [SerializeField, Min(0.5f)]
        private float rejectDuration = 1.5f;

        [SerializeField]
        private AudioClip faxRejectSound;

        [SerializeField]
        private Color rejectColor = new Color(1f, 0.1f, 0.1f);

        [SerializeField, Tooltip("Where the receipt drops after rejection. " +
                                 "Falls back to fax front if unassigned.")]
        private Transform rejectDropPoint;

        [Header("Item Filter")]

        [SerializeField, Tooltip("Tag suffix for receipt items (e.g. 'Receipt'). " +
                                 "Items with ItemTag containing this can be inserted.")]
        private string receiptTagFilter = "Receipt";

        [Header("Paper Animation")]

        [SerializeField, Tooltip("Transform where the paper starts (above the slot). " +
                                 "Auto-created if null.")]
        private Transform paperStartPoint;

        [SerializeField, Tooltip("Transform where the paper ends (inside the machine). " +
                                 "Auto-created if null.")]
        private Transform paperEndPoint;

        [SerializeField, Tooltip("Paper visual used for animations. " +
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

        [SerializeField]
        private NoiseLevel rejectNoiseLevel = NoiseLevel.Low;

        // ── State ────────────────────────────────────────────

        [SyncVar]
        private bool _isSending;

        /// <summary>
        /// The exact tag accepted this round (e.g. "ReceiptElectric").
        /// Set by the coordinator. Synced so clients hide the prompt
        /// when the fax is inactive (tag is null/empty).
        /// </summary>
        [SyncVar]
        private string _currentAcceptedTag;

        private AudioSource _audioSource;
        private Coroutine _activeRoutine;

        // Cached from last interaction (server only).
        private string _lastSentItemId;
        private PuzzleItemData _lastSentPuzzleData;
        private DocumentData _lastSentDocumentData;

        // ── Events ───────────────────────────────────────────

        /// <summary>
        /// Fired on server when a receipt is successfully sent.
        /// Parameters: itemId, puzzleData, documentData (may be null).
        /// BillsPuzzleCoordinator subscribes to this.
        /// </summary>
        public event System.Action<string, PuzzleItemData, DocumentData> OnReceiptSent;

        /// <summary>
        /// Fired on server when the fax rejects a receipt (wrong tag).
        /// Parameter: rejected itemId.
        /// </summary>
        public event System.Action<string> OnReceiptRejectedByFax;

        // ── Public API ───────────────────────────────────────

        /// <summary>
        /// Set the exact tag accepted for the current round.
        /// Called by BillsPuzzleCoordinator each round.
        /// Pass null/empty to deactivate the fax.
        /// </summary>
        [Server]
        public void SetAcceptedTag(string tag)
        {
            _currentAcceptedTag = tag;
        }

        // ── Lifecycle ────────────────────────────────────────

        private void Reset()
        {
            if (paperVisual == null && paperStartPoint == null && paperEndPoint == null)
                BuildProceduralModel();
        }

        [ContextMenu("Generate Fax Model")]
        private void EditorGenerateModel()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name is "FaxBody" or "FaxTray" or "FaxPaper"
                    or "PaperStart" or "PaperEnd" or "FaxIndicator")
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            paperVisual = null;
            paperStartPoint = null;
            paperEndPoint = null;
            indicatorLight = null;

            BuildProceduralModel();
        }

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

            interactionPrompt = "Fax";
        }

        // ── Interaction ──────────────────────────────────────

        public override bool CanPreviewInteraction(GameObject interactor)
        {
            if (_isSending) return false;
            if (string.IsNullOrEmpty(_currentAcceptedTag)) return false;
            return FindActiveReceipt(interactor) != null;
        }

        public override string GetInteractionPrompt(GameObject interactor)
        {
            if (_isSending) return "Enviando...";

            var pickable = FindActiveReceipt(interactor);
            if (pickable != null)
                return $"Enviar {pickable.PuzzleData.DisplayName}";

            return "Fax (necesitas un recibo)";
        }

        [Server]
        public override bool CanServerInteract(NetworkIdentity interactor)
        {
            if (_isSending || interactor == null) return false;
            if (string.IsNullOrEmpty(_currentAcceptedTag)) return false;
            return FindActiveReceiptServer(interactor) != null;
        }

        [Server]
        public override void ServerInteract(NetworkIdentity interactor)
        {
            if (!CanServerInteract(interactor)) return;

            var inventory = interactor.GetComponent<NetworkInventory>();
            if (inventory == null) return;

            InventorySlot slot = inventory.ActiveSlot;
            if (slot.IsEmpty || slot.itemNetId == 0) return;

            if (!NetworkServer.spawned.TryGetValue(slot.itemNetId, out NetworkIdentity itemIdentity))
                return;

            PickableItem pickable = itemIdentity.GetComponent<PickableItem>();
            if (pickable == null || pickable.PuzzleData == null) return;

            // Cache info before removing from inventory.
            _lastSentItemId = pickable.PuzzleData.ItemId;
            _lastSentPuzzleData = pickable.PuzzleData;
            _lastSentDocumentData = pickable.DocumentData;

            // Remove from inventory.
            inventory.ServerRemoveItem(inventory.ActiveSlotIndex);

            _isSending = true;

            if (_activeRoutine != null)
                StopCoroutine(_activeRoutine);

            // Exact tag match → send. Mismatch → reject with red light.
            if (pickable.PuzzleData.ItemTag == _currentAcceptedTag)
                _activeRoutine = StartCoroutine(ServerSendSequence(itemIdentity));
            else
                _activeRoutine = StartCoroutine(ServerRejectSequence(itemIdentity));
        }

        // ── Send sequence (tag matched) ──────────────────────

        [Server]
        private IEnumerator ServerSendSequence(NetworkIdentity receiptObject)
        {
            RpcPlaySendAnimation(sendDuration);
            PuzzleEvents.RaiseNoiseGenerated(transform.position, sendNoiseLevel);

            yield return new WaitForSeconds(sendDuration);

            _isSending = false;
            RpcPlaySendComplete();

            // Destroy the receipt — it's been "faxed".
            if (receiptObject != null)
                NetworkServer.Destroy(receiptObject.gameObject);

            OnReceiptSent?.Invoke(_lastSentItemId, _lastSentPuzzleData, _lastSentDocumentData);
            ClearCachedData();
            _activeRoutine = null;
        }

        // ── Reject sequence (wrong tag) ──────────────────────

        [Server]
        private IEnumerator ServerRejectSequence(NetworkIdentity receiptObject)
        {
            RpcPlayRejectAnimation(rejectDuration);
            PuzzleEvents.RaiseNoiseGenerated(transform.position, rejectNoiseLevel);

            yield return new WaitForSeconds(rejectDuration);

            _isSending = false;
            RpcPlayRejectComplete();

            // Drop receipt near fax so Runner can pick it up again.
            if (receiptObject != null)
            {
                Vector3 dropPos = rejectDropPoint != null
                    ? rejectDropPoint.position
                    : transform.position + transform.forward * 0.5f + Vector3.up * 0.3f;

                receiptObject.transform.position = dropPos;
                receiptObject.transform.rotation = Quaternion.identity;

                var pickup = receiptObject.GetComponent<NetworkPickupItem>();
                if (pickup != null)
                    pickup.SetVisibility(true);
            }

            OnReceiptRejectedByFax?.Invoke(_lastSentItemId);
            ClearCachedData();
            _activeRoutine = null;
        }

        private void ClearCachedData()
        {
            _lastSentItemId = null;
            _lastSentPuzzleData = null;
            _lastSentDocumentData = null;
        }

        // ── Client RPCs (send) ───────────────────────────────

        [ClientRpc]
        private void RpcPlaySendAnimation(float duration)
        {
            StartCoroutine(ClientSendAnimation(duration));

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

        // ── Client RPCs (reject) ─────────────────────────────

        [ClientRpc]
        private void RpcPlayRejectAnimation(float duration)
        {
            StartCoroutine(ClientRejectAnimation(duration));

            if (faxRejectSound != null)
                _audioSource.PlayOneShot(faxRejectSound);

            if (indicatorLight != null)
                indicatorLight.color = rejectColor;
        }

        [ClientRpc]
        private void RpcPlayRejectComplete()
        {
            if (indicatorLight != null)
            {
                indicatorLight.color = idleColor;
                indicatorLight.intensity = 1f;
            }

            if (paperVisual != null)
                paperVisual.gameObject.SetActive(false);
        }

        // ── Client animations ────────────────────────────────

        private IEnumerator ClientSendAnimation(float duration)
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

        private IEnumerator ClientRejectAnimation(float duration)
        {
            if (paperVisual == null || paperStartPoint == null || paperEndPoint == null)
                yield break;

            paperVisual.gameObject.SetActive(true);
            paperVisual.position = paperStartPoint.position;
            paperVisual.rotation = paperStartPoint.rotation;

            float half = duration * 0.5f;
            Vector3 midPos = Vector3.Lerp(
                paperStartPoint.position,
                paperEndPoint.position,
                0.5f);
            Quaternion midRot = Quaternion.Slerp(
                paperStartPoint.rotation,
                paperEndPoint.rotation,
                0.5f);

            // Paper goes halfway in.
            float elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / half);

                paperVisual.position = Vector3.Lerp(
                    paperStartPoint.position, midPos, t);
                paperVisual.rotation = Quaternion.Slerp(
                    paperStartPoint.rotation, midRot, t);

                if (indicatorLight != null)
                {
                    float blink = Mathf.Sin(elapsed * 12f) > 0f ? 1f : 0.3f;
                    indicatorLight.intensity = blink;
                }

                yield return null;
            }

            // Paper comes back out.
            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / half);

                paperVisual.position = Vector3.Lerp(midPos,
                    paperStartPoint.position, t);
                paperVisual.rotation = Quaternion.Slerp(midRot,
                    paperStartPoint.rotation, t);

                if (indicatorLight != null)
                {
                    float blink = Mathf.Sin(elapsed * 12f) > 0f ? 1f : 0.3f;
                    indicatorLight.intensity = blink;
                }

                yield return null;
            }

            paperVisual.gameObject.SetActive(false);

            if (indicatorLight != null)
                indicatorLight.intensity = 1f;
        }

        // ── Helpers ──────────────────────────────────────────

        /// <summary>
        /// Client-side: find PickableItem with receipt tag prefix
        /// in active inventory slot.
        /// </summary>
        private PickableItem FindActiveReceipt(GameObject interactor)
        {
            if (interactor == null) return null;

            var inventory = interactor.GetComponent<NetworkInventory>();
            if (inventory == null) return null;

            InventorySlot slot = inventory.ActiveSlot;
            if (slot.IsEmpty || slot.itemNetId == 0) return null;

            if (!NetworkClient.spawned.TryGetValue(slot.itemNetId, out NetworkIdentity identity))
                return null;

            var pickable = identity.GetComponent<PickableItem>();
            if (pickable == null || pickable.PuzzleData == null) return null;

            string tag = pickable.PuzzleData.ItemTag;
            if (string.IsNullOrEmpty(tag) || !tag.Contains(receiptTagFilter))
                return null;

            return pickable;
        }

        /// <summary>
        /// Server-side: find PickableItem with receipt tag prefix
        /// in active inventory slot.
        /// </summary>
        [Server]
        private PickableItem FindActiveReceiptServer(NetworkIdentity interactor)
        {
            var inventory = interactor.GetComponent<NetworkInventory>();
            if (inventory == null) return null;

            InventorySlot slot = inventory.ActiveSlot;
            if (slot.IsEmpty || slot.itemNetId == 0) return null;

            if (!NetworkServer.spawned.TryGetValue(slot.itemNetId, out NetworkIdentity identity))
                return null;

            var pickable = identity.GetComponent<PickableItem>();
            if (pickable == null || pickable.PuzzleData == null) return null;

            string tag = pickable.PuzzleData.ItemTag;
            if (string.IsNullOrEmpty(tag) || !tag.Contains(receiptTagFilter))
                return null;

            return pickable;
        }

        // ── Procedural Model ─────────────────────────────────

        private static void SafeDestroy(Object obj)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(obj);
            else
#endif
                Destroy(obj);
        }

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

            var bodyCol = body.GetComponent<Collider>();
            if (bodyCol != null) SafeDestroy(bodyCol);

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
            if (trayCol != null) SafeDestroy(trayCol);

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
            if (paperCol != null) SafeDestroy(paperCol);

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
            if (bulbCol != null) SafeDestroy(bulbCol);

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
