using System.Collections;
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
    /// - E nuevamente expulsa el objeto real en EjectPoint.
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

        private Coroutine serverScanRoutine;

        public bool HasFolder =>
            currentFolderNetId != 0;

        public bool IsScanning =>
            isScanning;

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
                ServerEjectFolder();
                return;
            }

            ServerInsertFolder(interactor);
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

        [Server]
        private void ServerEjectFolder()
        {
            if (!HasFolder)
            {
                return;
            }

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
            isScanning = false;

            RefreshInteractionPrompt();

            if (serverScanRoutine != null)
            {
                StopCoroutine(
                    serverScanRoutine
                );

                serverScanRoutine = null;
            }

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

            RpcFinishScan(folderNetId);

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
            uint folderNetId
        )
        {
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

            ShowFolderOnTerminal(folderData);
        }

        [ClientRpc]
        private void RpcEjectFolder()
        {
            RefreshInteractionPrompt();

            visuals?.EjectFolder();
            terminalView?.ShowWaiting();
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
                ShowFolderOnTerminal(folderData);
            }
        }

        private void ShowFolderOnTerminal(
            GuideFolderData folderData
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

            ReadableData document =
                folderData.GetDocument(0);

            if (document == null)
            {
                terminalView.ShowError(
                    "CARPETA VACÍA"
                );

                return;
            }

            string documentTitle;
            string documentBody;

            if (
                document.Type ==
                ReadableType.StickyNote
            )
            {
                documentTitle =
                    string.IsNullOrWhiteSpace(
                        document.Title
                    )
                        ? "NOTA"
                        : document.Title;

                documentBody =
                    document.NoteText ?? string.Empty;
            }
            else
            {
                documentTitle =
                    string.IsNullOrWhiteSpace(
                        document.Title
                    )
                        ? "DOCUMENTO"
                        : document.Title;

                documentBody =
                    document.Content ?? string.Empty;
            }

            terminalView.ShowDocument(
                folderData.DisplayName,
                documentTitle,
                documentBody,
                pageIndex: 0,
                pageCount:
                    Mathf.Max(
                        1,
                        folderData.DocumentCount
                    )
            );
        }

        private void RefreshInteractionPrompt()
        {
            interactionPrompt =
                HasFolder
                    ? (
                        isScanning
                            ? "Escaneando..."
                            : "Retirar carpeta"
                    )
                    : "Insertar carpeta";
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
