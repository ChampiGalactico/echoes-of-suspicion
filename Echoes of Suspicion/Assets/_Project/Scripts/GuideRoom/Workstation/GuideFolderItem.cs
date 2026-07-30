using UnityEngine;

namespace EOS.GuideRoom
{
    /// <summary>
    /// Identifica un NetworkPickupItem como carpeta compatible
    /// con la terminal del Guía.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GuideFolderItem : MonoBehaviour
    {
        [SerializeField]
        private GuideFolderData folderData;

        public GuideFolderData FolderData =>
            folderData;
    }
}
