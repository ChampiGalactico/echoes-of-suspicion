using System.Collections;
using EOS.Puzzles;
using Mirror;
using UnityEngine;

namespace EOS.GuideRoom
{
    /// <summary>
    /// Bandeja-escáner de carpetas de la sala del Guía.
    ///
    /// Flujo:
    /// - Con una GuideFolderItem en el slot activo, E inserta.
    /// - El objeto real permanece oculto y se elimina del inventario.
    /// - El lector muestra una representación visual y carga el documento.
    /// - Si la carpeta contiene varias entradas, E avanza a la siguiente.
    /// - En la última entrada, E expulsa el objeto real en EjectPoint.
    ///
    /// También acepta recibos (PickableItem con DocumentData):
    /// - Se escanean igual que las carpetas.
    /// - Tras el escaneo se dispara OnReceiptScanned para que el
    ///   controller active el botón PAGAR en la terminal.
    /// - Al expulsar se dispara OnReceiptEjected.
    ///
    /// Debe estar en el mismo GameObject que NetworkIdentity,
    /// porque NetworkRatInteractor resuelve el RatInteractable
    /// directamente desde la identidad de red.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class FolderScannerDock : RatInteractable
    {
        [Header("Referencias")]

        [SerializeField]
        private Transform ejectPoint;

        [SerializeField]
        private FolderScannerVisuals visuals;

        [SerializeField]
        private GuideTerminalView terminalView;

        [Header("Escaneo")]

        [SerializeField, Min(0.1f)]
        private float scanDuration = 1.15f;

        [SyncVar]
        private uint currentFolderNetId;

        [SyncVar]
        private bool isScanning;

        [SyncVar]
        private int currentPageIndex;

        /// <summary>
        /// True when the inserted item is a PickableItem (receipt),
        /// not a GuideFolderItem (folder).
        /// </summary>
        [SyncVar]
        private bool _isReceiptMode;

        private Coroutine serverScanRoutine;

        public bool HasFolder =>
            currentFolderNetId != 0;

        public bool IsScanning =>
            isScanning;

        public bool IsReceiptMode => _isReceiptMode;

        // ── Receipt events ───────────────────────────────────
        //
        // GuideBillsTerminalController se suscribe para activar
        // el botón PAGAR en la terminal.

        /// <summary>
        /// Fired on all clients when a receipt finishes scanning.
        /// Parameter: itemId from PuzzleItemData.
        /// </summary>
        public event System.Action<string> OnReceiptScanned;

        /// <summary>
        /// Fired on all clients when a receipt is ejected.
        /// </summary>
        public event System.Action OnReceiptEjected;

        private void Awake()
        {
            RefreshInteractionPrompt();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            RefreshInteractionPrompt();

            if (HasFolder)
            {
                StartCoroutine(
                    RestoreClientStateNextFrame()
                );
            }
            else
            {
                terminalView?.ShowWaiting();
                visuals?.SetIdle();
            }
        }

        public override bool CanPreviewInteraction(
            GameObject interactor
        )
        {
            if (
                interactor == null ||
                !IsGuide(interactor)
            )
            {
                return false;
            }

            if (HasFolder)
            {
                return !isScanning;
            }

            return ResolveActiveFolderItem(
                interactor,
                useServerTable: false
            ) != null
            || ResolveActiveReceiptItem(
                interactor,
                useServerTable: false
            ) != null;
        }

        [Server]
        public override bool CanServerInteract(
            NetworkIdentity interactor
        )
        {
            if (
                interactor == null ||
                !IsGuide(interactor.gameObject)
            )
            {
                return false;
            }

            if (HasFolder)
            {
                return !isScanning;
            }

            return ResolveActiveFolderItem(
                interactor.gameObject,
                useServerTable: true
            ) != null
            || ResolveActiveReceiptItem(
                interactor.gameObject,
                useServerTable: true
            ) != null;
        }

        [Server]
        public override void ServerInteract(
            NetworkIdentity interactor
        )
        {
            if (!CanServerInteract(interactor))
            {
                return;
            }

            if (HasFolder)
            {
                // Receipts have only 1 page — always eject.
                if (_isReceiptMode)
                {
                    ServerEjectFolder();
                    return;
                }

                if (ServerTryShowNextDocument())
                {
                    return;
                }

                ServerEjectFolder();
                return;
            }

            // Try folder first, then receipt.
            GuideFolderItem folder = ResolveActiveFolderItem(
                interactor.gameObject, useServerTable: true);

            if (folder != null)
            {
                ServerInsertFolder(interactor);
                return;
            }

            PickableItem receipt = ResolveActiveReceiptItem(
                interactor.gameObject, useServerTable: true);

            if (receipt != null)
            {
                ServerInsertReceipt(interactor);
                return;
            }
        }

        [Server]
        private void ServerInsertFolder(
            NetworkIdentity interactor
        )
        {
            NetworkInventory inventory =
                interactor.GetComponent<NetworkInventory>();

            if (inventory == null)
            {
                return;
            }

            InventorySlot activeSlot =
                inventory.ActiveSlot;

            GuideFolderItem folderItem =
                ResolveFolderItem(
                    activeSlot.itemNetId,
                    useServerTable: true
                );

            if (
                folderItem == null ||
                folderItem.FolderData == null
            )
            {
                TargetReject(
                    interactor.connectionToClient,
                    "OBJETO NO RECONOCIDO"
                );

                return;
            }

            currentFolderNetId =
                activeSlot.itemNetId;

            currentPageIndex = 0;

            inventory.ServerRemoveItem(
                inventory.ActiveSlotIndex
            );

            isScanning = true;

            RefreshInteractionPrompt();

            RpcBeginScan(
                currentFolderNetId,
                scanDuration
            );

            if (serverScanRoutine != null)
            {
                StopCoroutine(
                    serverScanRoutine
                );
            }

            serverScanRoutine =
                StartCoroutine(
                    ServerFinishScanRoutine(
                        currentFolderNetId
                    )
                );
        }

        /// <summary>
        /// Inserta un recibo (PickableItem) en el escáner.
        /// Mismo flujo que carpeta pero con 1 sola página y receipt mode.
        /// </summary>
        [Server]
        private void ServerInsertReceipt(
            NetworkIdentity interactor
        )
        {
            NetworkInventory inventory =
                interactor.GetComponent<NetworkInventory>();

            if (inventory == null)
                return;

            InventorySlot activeSlot = inventory.ActiveSlot;

            PickableItem receipt = ResolveReceiptItem(
                activeSlot.itemNetId,
                useServerTable: true
            );

            if (receipt == null || receipt.DocumentData == null)
            {
                TargetReject(
                    interactor.connectionToClient,
                    "OBJETO NO RECONOCIDO"
                );
                return;
            }

            currentFolderNetId = activeSlot.itemNetId;
            currentPageIndex = 0;
            _isReceiptMode = true;

            inventory.ServerRemoveItem(
                inventory.ActiveSlotIndex
            );

            isScanning = true;
            RefreshInteractionPrompt();

            string displayName = receipt.PuzzleData != null
                ? receipt.PuzzleData.DisplayName
                : "RECIBO";

            RpcBeginReceiptScan(
                currentFolderNetId,
                scanDuration,
                displayName
            );

            if (serverScanRoutine != null)
                StopCoroutine(serverScanRoutine);

            serverScanRoutine = StartCoroutine(
                ServerFinishScanRoutine(currentFolderNetId)
            );
        }

        [Server]
        private bool ServerTryShowNextDocument()
        {
            GuideFolderItem folderItem =
                ResolveFolderItem(
                    currentFolderNetId,
                    useServerTable: true
                );

            GuideFolderData folderData =
                folderItem != null
                    ? folderItem.FolderData
                    : null;

            int documentCount =
                folderData != null
                    ? folderData.DocumentCount
                    : 0;

            if (
                documentCount <= 1 ||
                currentPageIndex >= documentCount - 1
            )
            {
                return false;
            }

            currentPageIndex++;

            RefreshInteractionPrompt();

            RpcShowDocumentPage(
                currentFolderNetId,
                currentPageIndex
            );

            return true;
        }

        [Server]
        private void ServerEjectFolder()
        {
            if (!HasFolder)
            {
                return;
            }

            bool wasReceipt = _isReceiptMode;

            uint folderNetId =
                currentFolderNetId;

            NetworkPickupItem pickup =
                ResolvePickupItem(
                    folderNetId,
                    useServerTable: true
                );

            if (pickup != null)
            {
                Vector3 worldPosition =
                    ejectPoint != null
                        ? ejectPoint.position
                        : transform.position +
                          transform.forward * 0.9f +
                          Vector3.up * 0.35f;

                pickup.Drop(worldPosition);
            }

            currentFolderNetId = 0;
            currentPageIndex = 0;
            isScanning = false;
            _isReceiptMode = false;

            RefreshInteractionPrompt();

            if (serverScanRoutine != null)
            {
                StopCoroutine(
                    serverScanRoutine
                );

                serverScanRoutine = null;
            }

            if (wasReceipt)
                RpcEjectReceipt();
            else
                RpcEjectFolder();
        }

        [Server]
        private IEnumerator ServerFinishScanRoutine(
            uint folderNetId
        )
        {
            yield return new WaitForSeconds(
                scanDuration
            );

            if (
                currentFolderNetId != folderNetId ||
                currentFolderNetId == 0
            )
            {
                serverScanRoutine = null;
                yield break;
            }

            isScanning = false;

            RefreshInteractionPrompt();

            if (_isReceiptMode)
            {
                // Resolve receipt info for the RPC.
                PickableItem receipt = ResolveReceiptItem(
                    folderNetId, useServerTable: true);

                string itemId = receipt != null && receipt.PuzzleData != null
                    ? receipt.PuzzleData.ItemId
                    : "";

                string displayName = receipt != null && receipt.PuzzleData != null
                    ? receipt.PuzzleData.DisplayName
                    : "RECIBO";

                RpcFinishReceiptScan(folderNetId, itemId, displayName);
            }
            else
            {
                RpcFinishScan(
                    folderNetId,
                    currentPageIndex
                );
            }

            serverScanRoutine = null;
        }

        [ClientRpc]
        private void RpcBeginScan(
            uint folderNetId,
            float duration
        )
        {
            GuideFolderItem folderItem =
                ResolveFolderItem(
                    folderNetId,
                    useServerTable: false
                );

            GuideFolderData folderData =
                folderItem != null
                    ? folderItem.FolderData
                    : null;

            RefreshInteractionPrompt();

            visuals?.BeginScan(
                folderData,
                duration
            );

            terminalView?.ShowLoading(
                folderData != null
                    ? folderData.DisplayName
                    : "ARCHIVO"
            );
        }

        [ClientRpc]
        private void RpcFinishScan(
            uint folderNetId,
            int pageIndex
        )
        {
            currentPageIndex = pageIndex;

            RefreshInteractionPrompt();

            GuideFolderItem folderItem =
                ResolveFolderItem(
                    folderNetId,
                    useServerTable: false
                );

            GuideFolderData folderData =
                folderItem != null
                    ? folderItem.FolderData
                    : null;

            visuals?.FinishScan();

            ShowFolderOnTerminal(
                folderData,
                pageIndex
            );
        }

        [ClientRpc]
        private void RpcShowDocumentPage(
            uint folderNetId,
            int pageIndex
        )
        {
            currentPageIndex = pageIndex;

            GuideFolderItem folderItem =
                ResolveFolderItem(
                    folderNetId,
                    useServerTable: false
                );

            GuideFolderData folderData =
                folderItem != null
                    ? folderItem.FolderData
                    : null;

            RefreshInteractionPrompt();

            ShowFolderOnTerminal(
                folderData,
                pageIndex
            );
        }

        [ClientRpc]
        private void RpcEjectFolder()
        {
            currentFolderNetId = 0;
            currentPageIndex = 0;
            isScanning = false;

            RefreshInteractionPrompt();

            visuals?.EjectFolder();
            terminalView?.ShowWaiting();
        }

        // ── Receipt RPCs ─────────────────────────────────────

        [ClientRpc]
        private void RpcBeginReceiptScan(
            uint receiptNetId,
            float duration,
            string displayName
        )
        {
            RefreshInteractionPrompt();

            // Visuals: same scan animation, null folderData is fine.
            visuals?.BeginScan(null, duration);
            terminalView?.ShowLoading(displayName);
        }

        [ClientRpc]
        private void RpcFinishReceiptScan(
            uint receiptNetId,
            string itemId,
            string displayName
        )
        {
            RefreshInteractionPrompt();
            visuals?.FinishScan();

            // Show receipt document on terminal.
            PickableItem receipt = ResolveReceiptItem(
                receiptNetId, useServerTable: false);

            if (receipt != null && receipt.DocumentData != null)
            {
                ExtractDocument(
                    receipt.DocumentData,
                    out string title,
                    out string body
                );

                terminalView?.ShowDocument(
                    displayName,
                    title,
                    body,
                    pageIndex: 0,
                    pageCount: 1
                );
            }
            else
            {
                terminalView?.ShowError("RECIBO SIN DATOS");
            }

            OnReceiptScanned?.Invoke(itemId);
        }

        [ClientRpc]
        private void RpcEjectReceipt()
        {
            currentFolderNetId = 0;
            currentPageIndex = 0;
            isScanning = false;
            _isReceiptMode = false;

            RefreshInteractionPrompt();

            visuals?.EjectFolder();
            terminalView?.ShowWaiting();

            OnReceiptEjected?.Invoke();
        }

        [TargetRpc]
        private void TargetReject(
            NetworkConnectionToClient target,
            string message
        )
        {
            visuals?.Reject();
            terminalView?.ShowError(message);
        }

        private IEnumerator RestoreClientStateNextFrame()
        {
            yield return null;

            if (_isReceiptMode)
            {
                PickableItem receipt = ResolveReceiptItem(
                    currentFolderNetId, useServerTable: false);

                string displayName = receipt != null && receipt.PuzzleData != null
                    ? receipt.PuzzleData.DisplayName
                    : "RECIBO";

                visuals?.ShowFolder(null);

                if (isScanning)
                {
                    visuals?.BeginScan(null, scanDuration);
                    terminalView?.ShowLoading(displayName);
                }
                else
                {
                    visuals?.FinishScan();

                    if (receipt != null && receipt.DocumentData != null)
                    {
                        ExtractDocument(
                            receipt.DocumentData,
                            out string title,
                            out string body
                        );

                        terminalView?.ShowDocument(
                            displayName, title, body,
                            pageIndex: 0, pageCount: 1
                        );

                        string itemId = receipt.PuzzleData != null
                            ? receipt.PuzzleData.ItemId : "";
                        OnReceiptScanned?.Invoke(itemId);
                    }
                }

                yield break;
            }

            GuideFolderItem folderItem =
                ResolveFolderItem(
                    currentFolderNetId,
                    useServerTable: false
                );

            GuideFolderData folderData =
                folderItem != null
                    ? folderItem.FolderData
                    : null;

            visuals?.ShowFolder(folderData);

            if (isScanning)
            {
                visuals?.BeginScan(
                    folderData,
                    scanDuration
                );

                terminalView?.ShowLoading(
                    folderData != null
                        ? folderData.DisplayName
                        : "ARCHIVO"
                );
            }
            else
            {
                visuals?.FinishScan();
                ShowFolderOnTerminal(
                    folderData,
                    currentPageIndex
                );
            }
        }

        private void ShowFolderOnTerminal(
            GuideFolderData folderData,
            int pageIndex
        )
        {
            if (terminalView == null)
            {
                return;
            }

            if (folderData == null)
            {
                terminalView.ShowError(
                    "DATOS DE CARPETA AUSENTES"
                );

                return;
            }

            int safePageIndex =
                Mathf.Clamp(
                    pageIndex,
                    0,
                    Mathf.Max(
                        0,
                        folderData.DocumentCount - 1
                    )
                );

            GuideFolderData.FolderDocument entry =
                folderData.GetDocument(
                    safePageIndex
                );

            if (entry.IsEmpty)
            {
                terminalView.ShowError(
                    "CARPETA VACÍA"
                );

                return;
            }

            string documentTitle;
            string documentBody;

            if (entry.HasDocument)
            {
                ExtractDocument(
                    entry.document,
                    out documentTitle,
                    out documentBody
                );
            }
            else
            {
                ExtractNote(
                    entry.note,
                    out documentTitle,
                    out documentBody
                );
            }

            terminalView.ShowDocument(
                folderData.DisplayName,
                documentTitle,
                documentBody,
                pageIndex: safePageIndex,
                pageCount:
                    Mathf.Max(
                        1,
                        folderData.DocumentCount
                    )
            );
        }

        private static void ExtractDocument(
            DocumentData document,
            out string title,
            out string body
        )
        {
            title = "DOCUMENTO";
            body = string.Empty;

            if (
                document == null ||
                document.Sections == null ||
                document.Sections.Length == 0
            )
            {
                return;
            }

            bool titleFound = false;

            System.Text.StringBuilder bodyBuilder =
                new();

            foreach (
                DocumentSection section
                in document.Sections
            )
            {
                if (
                    section == null ||
                    string.IsNullOrWhiteSpace(section.Text)
                )
                {
                    continue;
                }

                if (
                    !titleFound &&
                    section.Type == SectionType.Title
                )
                {
                    title = section.Text.Trim();
                    titleFound = true;

                    continue;
                }

                if (bodyBuilder.Length > 0)
                {
                    bodyBuilder.Append("\n\n");
                }

                bodyBuilder.Append(section.Text.Trim());
            }

            // Si no había sección de tipo Title, usa la primera
            // sección de texto como encabezado.
            if (!titleFound)
            {
                foreach (
                    DocumentSection section
                    in document.Sections
                )
                {
                    if (
                        section != null &&
                        !string.IsNullOrWhiteSpace(section.Text)
                    )
                    {
                        title = section.Text.Trim();
                        break;
                    }
                }
            }

            body = bodyBuilder.ToString();
        }

        private static void ExtractNote(
            StickyNoteData note,
            out string title,
            out string body
        )
        {
            title = "NOTA";
            body = string.Empty;

            if (note == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(note.name))
            {
                title = note.name;
            }

            body = note.NoteText ?? string.Empty;
        }

        private void RefreshInteractionPrompt()
        {
            if (!HasFolder)
            {
                interactionPrompt = "Insertar documento";
                return;
            }

            if (isScanning)
            {
                interactionPrompt = "Escaneando...";
                return;
            }

            if (_isReceiptMode)
            {
                interactionPrompt = "Retirar recibo";
                return;
            }

            GuideFolderItem folderItem =
                ResolveFolderItem(
                    currentFolderNetId,
                    useServerTable: NetworkServer.active
                );

            GuideFolderData folderData =
                folderItem != null
                    ? folderItem.FolderData
                    : null;

            bool hasNextDocument =
                folderData != null &&
                currentPageIndex <
                folderData.DocumentCount - 1;

            interactionPrompt =
                hasNextDocument
                    ? "Siguiente documento"
                    : "Retirar carpeta";
        }

        private static bool IsGuide(
            GameObject player
        )
        {
            CharacterStatsProvider stats =
                player != null
                    ? player.GetComponent<
                        CharacterStatsProvider>()
                    : null;

            return
                stats != null &&
                stats.Role == PlayerRole.Guide;
        }

        private static GuideFolderItem
            ResolveActiveFolderItem(
                GameObject player,
                bool useServerTable
            )
        {
            NetworkInventory inventory =
                player != null
                    ? player.GetComponent<
                        NetworkInventory>()
                    : null;

            if (inventory == null)
            {
                return null;
            }

            return ResolveFolderItem(
                inventory.ActiveSlot.itemNetId,
                useServerTable
            );
        }

        private static GuideFolderItem
            ResolveFolderItem(
                uint netId,
                bool useServerTable
            )
        {
            if (netId == 0)
            {
                return null;
            }

            var spawnedTable =
                useServerTable
                    ? NetworkServer.spawned
                    : NetworkClient.spawned;

            if (
                !spawnedTable.TryGetValue(
                    netId,
                    out NetworkIdentity identity
                )
            )
            {
                return null;
            }

            return identity.GetComponent<
                GuideFolderItem>();
        }

        /// <summary>
        /// Checks if the player's active slot has a PickableItem
        /// with DocumentData (i.e. a receipt that can be scanned).
        /// </summary>
        private static PickableItem
            ResolveActiveReceiptItem(
                GameObject player,
                bool useServerTable
            )
        {
            NetworkInventory inventory =
                player != null
                    ? player.GetComponent<
                        NetworkInventory>()
                    : null;

            if (inventory == null)
                return null;

            return ResolveReceiptItem(
                inventory.ActiveSlot.itemNetId,
                useServerTable
            );
        }

        /// <summary>
        /// Resolves a PickableItem with DocumentData from a netId.
        /// Returns null if the item doesn't exist or has no document.
        /// </summary>
        private static PickableItem
            ResolveReceiptItem(
                uint netId,
                bool useServerTable
            )
        {
            if (netId == 0)
                return null;

            var spawnedTable =
                useServerTable
                    ? NetworkServer.spawned
                    : NetworkClient.spawned;

            if (!spawnedTable.TryGetValue(
                    netId,
                    out NetworkIdentity identity
                ))
            {
                return null;
            }

            // Only accept if it has DocumentData and is NOT
            // a GuideFolderItem (those use the folder path).
            if (identity.GetComponent<GuideFolderItem>() != null)
                return null;

            PickableItem pickable =
                identity.GetComponent<PickableItem>();

            if (pickable == null || pickable.DocumentData == null)
                return null;

            return pickable;
        }

        private static NetworkPickupItem
            ResolvePickupItem(
                uint netId,
                bool useServerTable
            )
        {
            if (netId == 0)
            {
                return null;
            }

            var spawnedTable =
                useServerTable
                    ? NetworkServer.spawned
                    : NetworkClient.spawned;

            if (
                !spawnedTable.TryGetValue(
                    netId,
                    out NetworkIdentity identity
                )
            )
            {
                return null;
            }

            return identity.GetComponent<
                NetworkPickupItem>();
        }
    }
}
