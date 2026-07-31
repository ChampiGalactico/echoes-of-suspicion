using System.Collections;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EOS.GuideRoom
{
    /// <summary>
    /// Fax receptor en la sala del Guía.
    ///
    /// Después de que se completa el puzzle 2, muestra "PAGOS PENDIENTES"
    /// en su pantalla con una luz verde parpadeante y un sonido.
    /// Cuando el Guía interactúa (E), envía CmdStartBillsPuzzle al server.
    ///
    /// Modelo procedimental: caja tipo fax con pantalla (WorldSpace Canvas),
    /// luz indicadora verde y bandeja.
    ///
    /// SETUP:
    /// 1. Colocar en la sala del Guía cerca del escritorio.
    /// 2. Asignar font (Audiowide_SDF).
    /// 3. Opcionalmente asignar arrivalClip (sonido de fax recibido).
    /// 4. NetworkIdentity requerido (es RatInteractable).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class GuideFaxReceiver : RatInteractable
    {
        [Header("UI")]

        [SerializeField, Tooltip("Fuente para el texto de la pantalla (Audiowide_SDF).")]
        private TMP_FontAsset font;

        [Header("Audio")]

        [SerializeField, Tooltip("Sonido al recibir el fax de pagos pendientes.")]
        private AudioClip arrivalClip;

        [SerializeField, Range(0f, 1f)]
        private float arrivalVolume = 0.7f;

        [Header("Colors")]

        [SerializeField]
        private Color screenTextColor = new Color(0.22f, 1f, 0.32f, 1f);

        [SerializeField]
        private Color indicatorColor = new Color(0.1f, 0.9f, 0.3f, 1f);

        [SerializeField]
        private Color bodyColor = new Color(0.25f, 0.25f, 0.28f, 1f);

        [Header("Paper Animation")]

        [SerializeField, Min(0.5f)]
        private float paperAnimDuration = 2f;

        [SerializeField]
        private AudioClip faxPrintSound;

        // ── State ────────────────────────────────────────────

        /// <summary>
        /// True when "PAGOS PENDIENTES" should be shown and the Guide
        /// can interact to start the bills puzzle.
        /// </summary>
        [SyncVar(hook = nameof(OnActivatedChanged))]
        private bool _activated;

        [SyncVar]
        private bool _puzzleStarted;

        /// <summary>Punto donde aparecen los recibos. Público para que el coordinator lo use.</summary>
        public Transform ReceiptSpawnPoint => _receiptSpawnPoint != null
            ? _receiptSpawnPoint
            : transform;

        // ── Procedural references ────────────────────────────

        private TMP_Text _screenText;
        private Light _indicatorLight;
        private Renderer _bulbRenderer;
        private AudioSource _audioSource;
        private Canvas _screenCanvas;
        private Coroutine _blinkRoutine;
        private Transform _paperVisual;
        private Transform _paperStartPoint;
        private Transform _paperEndPoint;
        private Transform _receiptSpawnPoint;

        // ── Lifecycle ────────────────────────────────────────

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();

            _audioSource.spatialBlend = 1f;
            _audioSource.playOnAwake = false;
            _audioSource.maxDistance = 12f;

            // Buscar hijos existentes antes de crear nuevos.
            FindExistingReferences();

            if (_screenText == null)
                BuildProceduralModel();

            if (_paperVisual != null)
                _paperVisual.gameObject.SetActive(false);

            SetScreenIdle();

            interactionPrompt = "Fax";
        }

        private void OnEnable()
        {
            // Escuchar eventos del coordinator para actualizar la pantalla.
            EOS.Puzzles.BillsPuzzleCoordinator.OnGuideNextBillAnnounced += HandleNextBillAnnounced;
            EOS.Puzzles.BillsPuzzleCoordinator.OnGuideReceiptArrived += HandleReceiptArrived;
            EOS.Puzzles.BillsPuzzleCoordinator.OnClientBillPaid += HandleBillPaid;
            EOS.Puzzles.BillsPuzzleCoordinator.OnClientTimeout += HandleTimeout;
        }

        private void OnDisable()
        {
            EOS.Puzzles.BillsPuzzleCoordinator.OnGuideNextBillAnnounced -= HandleNextBillAnnounced;
            EOS.Puzzles.BillsPuzzleCoordinator.OnGuideReceiptArrived -= HandleReceiptArrived;
            EOS.Puzzles.BillsPuzzleCoordinator.OnClientBillPaid -= HandleBillPaid;
            EOS.Puzzles.BillsPuzzleCoordinator.OnClientTimeout -= HandleTimeout;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (_activated && !_puzzleStarted)
                ShowPendingState();
            else
                SetScreenIdle();
        }

        // ── Server API ───────────────────────────────────────

        /// <summary>
        /// Called by the progression system (e.g. DemoProgressionManager)
        /// when puzzle 2 is solved. Activates the fax receiver for the Guide.
        /// </summary>
        [Server]
        public void Activate()
        {
            if (_activated) return;

            _activated = true;
            RpcShowArrival();
        }

        /// <summary>
        /// Called after the bills puzzle starts so the fax no longer shows
        /// the pending message.
        /// </summary>
        [Server]
        public void MarkPuzzleStarted()
        {
            _puzzleStarted = true;
            RpcClearScreen();
        }

        // ── Interaction ──────────────────────────────────────

        public override bool CanPreviewInteraction(GameObject interactor)
        {
            if (!_activated || _puzzleStarted) return false;
            return interactor != null && IsGuide(interactor);
        }

        public override string GetInteractionPrompt(GameObject interactor)
        {
            if (_activated && !_puzzleStarted)
                return "Iniciar pagos pendientes";

            return "Fax (sin mensajes)";
        }

        [Server]
        public override bool CanServerInteract(NetworkIdentity interactor)
        {
            if (!_activated || _puzzleStarted) return false;
            return interactor != null && IsGuide(interactor.gameObject);
        }

        [Server]
        public override void ServerInteract(NetworkIdentity interactor)
        {
            if (!CanServerInteract(interactor)) return;

            MarkPuzzleStarted();

            // The actual StartBillsPuzzle call goes through the Guide's
            // PlayerHealth Command so it originates from the correct client.
            // We fire a TargetRpc to tell the Guide client to send the Command.
            TargetTriggerStartCommand(interactor.connectionToClient);
        }

        [TargetRpc]
        private void TargetTriggerStartCommand(NetworkConnectionToClient target)
        {
            var localPlayer = NetworkClient.localPlayer;
            if (localPlayer == null) return;

            var health = localPlayer.GetComponent<PlayerHealth>();
            if (health != null)
                health.CmdStartBillsPuzzle();
        }

        // ── Client RPCs ──────────────────────────────────────

        [ClientRpc]
        private void RpcShowArrival()
        {
            ShowPendingState();

            if (arrivalClip != null && _audioSource != null)
                _audioSource.PlayOneShot(arrivalClip, arrivalVolume);
        }

        [ClientRpc]
        private void RpcClearScreen()
        {
            SetScreenIdle();
        }

        // ── SyncVar hook ─────────────────────────────────────

        private void OnActivatedChanged(bool oldVal, bool newVal)
        {
            if (newVal && !_puzzleStarted)
                ShowPendingState();
            else
                SetScreenIdle();
        }

        // ── Coordinator event handlers ────────────────────────

        private void HandleNextBillAnnounced(
            int billIndex, int totalBills, string billName, string instructions,
            float timeLimit)
        {
            if (!_puzzleStarted) return;

            // Show which receipt the Guide needs to ask for.
            ShowScreenText($"NECESITA:\n{billName.ToUpper()}");
            StartBlinking();
        }

        private void HandleReceiptArrived(string itemId, string displayName, string paymentCode)
        {
            if (!_puzzleStarted) return;

            // Receipt arrived — update screen and play print animation.
            ShowScreenText($"RECIBIDO:\n{displayName.ToUpper()}");

            if (arrivalClip != null && _audioSource != null)
                _audioSource.PlayOneShot(arrivalClip, arrivalVolume);

            if (faxPrintSound != null && _audioSource != null)
                _audioSource.PlayOneShot(faxPrintSound, arrivalVolume);

            StartCoroutine(PlayPaperArrivalAnimation());
        }

        private void HandleBillPaid(int billIndex, string billName)
        {
            // Next bill will be announced via HandleNextBillAnnounced.
        }

        private void HandleTimeout()
        {
            ShowScreenText("TIEMPO\nAGOTADO");
        }

        // ── Screen states ────────────────────────────────────

        private void ShowScreenText(string text)
        {
            if (_screenText == null) return;
            _screenText.text = text;
            _screenText.color = screenTextColor;
            _screenText.gameObject.SetActive(true);
        }

        private void StartBlinking()
        {
            if (_indicatorLight != null)
            {
                _indicatorLight.color = indicatorColor;
                _indicatorLight.enabled = true;
            }

            if (_blinkRoutine != null)
                StopCoroutine(_blinkRoutine);
            _blinkRoutine = StartCoroutine(BlinkRoutine());
        }

        private void ShowPendingState()
        {
            if (_screenText != null)
            {
                _screenText.text = "PAGOS\nPENDIENTES";
                _screenText.color = screenTextColor;
                _screenText.gameObject.SetActive(true);
            }

            if (_indicatorLight != null)
            {
                _indicatorLight.color = indicatorColor;
                _indicatorLight.enabled = true;
            }

            if (_bulbRenderer != null)
            {
                _bulbRenderer.material.color = indicatorColor;
                _bulbRenderer.material.SetColor("_EmissionColor", indicatorColor * 2f);
            }

            if (_blinkRoutine != null)
                StopCoroutine(_blinkRoutine);
            _blinkRoutine = StartCoroutine(BlinkRoutine());
        }

        private void SetScreenIdle()
        {
            if (_screenText != null)
            {
                _screenText.text = "SIN MENSAJES";
                _screenText.color = new Color(
                    screenTextColor.r, screenTextColor.g, screenTextColor.b, 0.3f);
                _screenText.gameObject.SetActive(true);
            }

            if (_indicatorLight != null)
                _indicatorLight.enabled = false;

            if (_blinkRoutine != null)
            {
                StopCoroutine(_blinkRoutine);
                _blinkRoutine = null;
            }
        }

        private IEnumerator BlinkRoutine()
        {
            while (true)
            {
                if (_indicatorLight != null)
                    _indicatorLight.intensity = 1.5f;
                yield return new WaitForSeconds(0.5f);

                if (_indicatorLight != null)
                    _indicatorLight.intensity = 0.3f;
                yield return new WaitForSeconds(0.5f);
            }
        }

        // ── Paper Animation ──────────────────────────────

        private IEnumerator PlayPaperArrivalAnimation()
        {
            if (_paperVisual == null || _paperStartPoint == null || _paperEndPoint == null)
                yield break;

            // Show paper at start position (inside the machine).
            _paperVisual.gameObject.SetActive(true);
            _paperVisual.position = _paperStartPoint.position;
            _paperVisual.rotation = _paperStartPoint.rotation;

            float elapsed = 0f;

            while (elapsed < paperAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / paperAnimDuration);
                float smooth = Mathf.SmoothStep(0f, 1f, t);

                _paperVisual.position = Vector3.Lerp(
                    _paperStartPoint.position, _paperEndPoint.position, smooth);
                _paperVisual.rotation = Quaternion.Slerp(
                    _paperStartPoint.rotation, _paperEndPoint.rotation, smooth);

                // Blink indicator light during animation.
                if (_indicatorLight != null)
                    _indicatorLight.intensity = Mathf.Lerp(0.3f, 1.5f,
                        Mathf.Sin(elapsed * 8f) * 0.5f + 0.5f);

                yield return null;
            }

            // Snap to final position.
            _paperVisual.position = _paperEndPoint.position;
            _paperVisual.rotation = _paperEndPoint.rotation;

            // Keep paper visible — the Guide needs to see it arrived.
        }

        // ── Helpers ──────────────────────────────────────────

        private static bool IsGuide(GameObject player)
        {
            var stats = player != null
                ? player.GetComponent<CharacterStatsProvider>()
                : null;
            return stats != null && stats.Role == PlayerRole.Guide;
        }

        // ── Procedural Model ─────────────────────────────────

        /// <summary>
        /// Busca los hijos procedimentales ya existentes y cachea
        /// las referencias. Así en Play no se recrean duplicados
        /// y se respetan rotaciones/posiciones manuales.
        /// </summary>
        private void FindExistingReferences()
        {
            Transform screen = transform.Find("FaxReceiverScreen");
            if (screen != null)
            {
                _screenCanvas = screen.GetComponent<Canvas>();
                _screenText = screen.GetComponentInChildren<TMP_Text>();
            }

            Transform indicator = transform.Find("FaxReceiverIndicator");
            if (indicator != null)
            {
                _indicatorLight = indicator.GetComponent<Light>();
                Transform bulb = indicator.Find("Bulb");
                if (bulb != null)
                    _bulbRenderer = bulb.GetComponent<Renderer>();
            }

            _paperVisual = transform.Find("FaxReceiverPaper");
            _paperStartPoint = transform.Find("FaxReceiverPaperStart");
            _paperEndPoint = transform.Find("FaxReceiverPaperEnd");
            _receiptSpawnPoint = transform.Find("FaxReceiverReceiptSpawn");
        }

        [ContextMenu("Generate Fax Receiver Model")]
        private void EditorGenerateModel()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name is "FaxReceiverBody" or "FaxReceiverTray"
                    or "FaxReceiverScreen" or "FaxReceiverIndicator"
                    or "FaxReceiverPaper" or "FaxReceiverPaperStart"
                    or "FaxReceiverPaperEnd" or "FaxReceiverReceiptSpawn")
                {
                    SafeDestroy(child.gameObject);
                }
            }

            _screenText = null;
            _indicatorLight = null;
            _bulbRenderer = null;
            _screenCanvas = null;
            _paperVisual = null;
            _paperStartPoint = null;
            _paperEndPoint = null;
            _receiptSpawnPoint = null;

            BuildProceduralModel();
        }

        private void Reset()
        {
            BuildProceduralModel();
        }

        private void BuildProceduralModel()
        {
            if (_screenText != null) return; // Already built.

            // ── Body (main box) ──
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "FaxReceiverBody";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(0.5f, 0.25f, 0.35f);

            var bodyRenderer = body.GetComponent<Renderer>();
            if (bodyRenderer != null)
            {
                bodyRenderer.material = new Material(
                    Shader.Find("Universal Render Pipeline/Lit"));
                bodyRenderer.material.color = bodyColor;
            }

            var bodyCol = body.GetComponent<Collider>();
            if (bodyCol != null) SafeDestroy(bodyCol);

            // ── Tray (back raised section) ──
            GameObject tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tray.name = "FaxReceiverTray";
            tray.transform.SetParent(transform, false);
            tray.transform.localPosition = new Vector3(0f, 0.15f, -0.1f);
            tray.transform.localScale = new Vector3(0.4f, 0.04f, 0.15f);
            tray.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);

            var trayRenderer = tray.GetComponent<Renderer>();
            if (trayRenderer != null)
            {
                trayRenderer.material = new Material(
                    Shader.Find("Universal Render Pipeline/Lit"));
                trayRenderer.material.color = new Color(
                    bodyColor.r * 0.8f, bodyColor.g * 0.8f, bodyColor.b * 0.8f);
            }

            var trayCol = tray.GetComponent<Collider>();
            if (trayCol != null) SafeDestroy(trayCol);

            // ── Screen (WorldSpace Canvas on the front face) ──
            GameObject screenObj = new GameObject("FaxReceiverScreen");
            screenObj.transform.SetParent(transform, false);
            screenObj.transform.localPosition = new Vector3(0f, 0.06f, 0.176f);
            screenObj.transform.localRotation = Quaternion.identity;

            _screenCanvas = screenObj.AddComponent<Canvas>();
            _screenCanvas.renderMode = RenderMode.WorldSpace;

            var canvasRt = screenObj.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(300f, 150f);
            canvasRt.localScale = Vector3.one * 0.001f; // 0.3m x 0.15m

            // Screen background (dark).
            var screenBg = screenObj.AddComponent<Image>();
            screenBg.color = new Color(0.01f, 0.04f, 0.02f, 0.95f);
            screenBg.raycastTarget = false;

            // Text.
            GameObject textObj = new GameObject("ScreenText");
            textObj.transform.SetParent(screenObj.transform, false);

            var textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(10f, 10f);
            textRt.offsetMax = new Vector2(-10f, -10f);

            _screenText = textObj.AddComponent<TextMeshProUGUI>();
            _screenText.fontSize = 36f;
            _screenText.alignment = TextAlignmentOptions.Center;
            _screenText.color = screenTextColor;
            _screenText.textWrappingMode = TextWrappingModes.Normal;
            _screenText.text = "SIN MENSAJES";

            if (font != null)
                _screenText.font = font;

            // ── Indicator light ──
            GameObject lightObj = new GameObject("FaxReceiverIndicator");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = new Vector3(0.2f, 0.14f, 0.14f);

            _indicatorLight = lightObj.AddComponent<Light>();
            _indicatorLight.type = LightType.Point;
            _indicatorLight.range = 0.6f;
            _indicatorLight.intensity = 1f;
            _indicatorLight.color = indicatorColor;
            _indicatorLight.enabled = false;

            GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = "Bulb";
            bulb.transform.SetParent(lightObj.transform, false);
            bulb.transform.localScale = Vector3.one * 0.03f;

            _bulbRenderer = bulb.GetComponent<Renderer>();
            if (_bulbRenderer != null)
            {
                _bulbRenderer.material = new Material(
                    Shader.Find("Universal Render Pipeline/Lit"));
                _bulbRenderer.material.color = indicatorColor;
                _bulbRenderer.material.EnableKeyword("_EMISSION");
                _bulbRenderer.material.SetColor("_EmissionColor", indicatorColor * 2f);
            }

            var bulbCol = bulb.GetComponent<Collider>();
            if (bulbCol != null) SafeDestroy(bulbCol);

            // ── Paper animation objects ──

            // Paper visual — thin quad that slides out of the tray.
            GameObject paper = GameObject.CreatePrimitive(PrimitiveType.Quad);
            paper.name = "FaxReceiverPaper";
            paper.transform.SetParent(transform, false);
            paper.transform.localPosition = new Vector3(0f, 0.18f, -0.08f);
            paper.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            paper.transform.localScale = new Vector3(0.18f, 0.25f, 1f);

            var paperRenderer = paper.GetComponent<Renderer>();
            if (paperRenderer != null)
            {
                paperRenderer.material = new Material(
                    Shader.Find("Universal Render Pipeline/Lit"));
                paperRenderer.material.color = new Color(0.92f, 0.9f, 0.85f); // Off-white paper
            }

            var paperCol = paper.GetComponent<Collider>();
            if (paperCol != null) SafeDestroy(paperCol);

            paper.SetActive(false);
            _paperVisual = paper.transform;

            // Start point — inside the machine body.
            GameObject startPt = new GameObject("FaxReceiverPaperStart");
            startPt.transform.SetParent(transform, false);
            startPt.transform.localPosition = new Vector3(0f, 0.14f, 0.05f);
            startPt.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _paperStartPoint = startPt.transform;

            // End point — on the tray, outside the machine.
            GameObject endPt = new GameObject("FaxReceiverPaperEnd");
            endPt.transform.SetParent(transform, false);
            endPt.transform.localPosition = new Vector3(0f, 0.18f, -0.18f);
            endPt.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
            _paperEndPoint = endPt.transform;

            // Receipt spawn point — where the actual PickableItem is teleported.
            GameObject spawnPt = new GameObject("FaxReceiverReceiptSpawn");
            spawnPt.transform.SetParent(transform, false);
            spawnPt.transform.localPosition = new Vector3(0f, 0.22f, -0.2f);
            _receiptSpawnPoint = spawnPt.transform;

            // ── Main collider on parent ──
            if (GetComponent<Collider>() == null)
            {
                BoxCollider mainCol = gameObject.AddComponent<BoxCollider>();
                mainCol.center = new Vector3(0f, 0.06f, 0f);
                mainCol.size = new Vector3(0.55f, 0.35f, 0.4f);
            }
        }

        private static void SafeDestroy(Object obj)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(obj);
            else
#endif
                Destroy(obj);
        }
    }
}
