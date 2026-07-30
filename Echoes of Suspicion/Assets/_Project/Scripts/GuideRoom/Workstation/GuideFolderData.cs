using UnityEngine;

namespace EOS.GuideRoom
{
    /// <summary>
    /// Datos de una carpeta física del Guía.
    ///
    /// Cada entrada reutiliza ReadableData, el formato de documentos
    /// ya utilizado por los papeles y notas del proyecto.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewGuideFolder",
        menuName = "EOS/Guide Room/Guide Folder Data")]
    public sealed class GuideFolderData : ScriptableObject
    {
        [Header("Identidad")]

        [SerializeField]
        private string folderId = "folder.sample";

        [SerializeField]
        private string displayName = "CARPETA SIN NOMBRE";

        [SerializeField]
        private Color folderColor =
            new(0.25f, 0.36f, 0.22f, 1f);

        [Header("Contenido")]

        [SerializeField]
        private ReadableData[] documents =
            System.Array.Empty<ReadableData>();

        public string FolderId => folderId;

        public string DisplayName =>
            string.IsNullOrWhiteSpace(displayName)
                ? name
                : displayName;

        public Color FolderColor => folderColor;

        public int DocumentCount =>
            documents != null
                ? documents.Length
                : 0;

        public ReadableData GetDocument(int index)
        {
            if (
                documents == null ||
                documents.Length == 0
            )
            {
                return null;
            }

            int safeIndex =
                Mathf.Clamp(
                    index,
                    0,
                    documents.Length - 1
                );

            return documents[safeIndex];
        }
    }
}
