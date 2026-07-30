using UnityEngine;

namespace EOS.GuideRoom
{
    /// <summary>
    /// Datos de una carpeta física del Guía.
    ///
    /// Cada entrada reutiliza el modelo de documentos legibles ya existente
    /// en el proyecto: <see cref="DocumentData"/> (documentos con secciones)
    /// o <see cref="StickyNoteData"/> (notas simples). Una entrada puede
    /// contener uno u otro; si ambos están asignados, se prioriza el documento.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewGuideFolder",
        menuName = "EOS/Guide Room/Guide Folder Data")]
    public sealed class GuideFolderData : ScriptableObject
    {
        /// <summary>
        /// Una entrada de la carpeta. Envuelve el modelo real de readables
        /// del proyecto para no duplicar formatos de documento.
        /// </summary>
        [System.Serializable]
        public struct FolderDocument
        {
            [Tooltip("Documento con secciones. Prioritario si está asignado.")]
            public DocumentData document;

            [Tooltip("Nota simple. Se usa si no hay documento asignado.")]
            public StickyNoteData note;

            public bool HasDocument => document != null;

            public bool HasNote => note != null;

            public bool IsEmpty => document == null && note == null;
        }

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
        private FolderDocument[] documents =
            System.Array.Empty<FolderDocument>();

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

        public FolderDocument GetDocument(int index)
        {
            if (
                documents == null ||
                documents.Length == 0
            )
            {
                return default;
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
