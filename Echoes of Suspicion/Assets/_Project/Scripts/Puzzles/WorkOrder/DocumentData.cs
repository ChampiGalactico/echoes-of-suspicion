using UnityEngine;

namespace EOS.Puzzles.WorkOrder
{
    public enum WorkOrderDocumentType
    {
        DiagnosticNote,
        ManualFolder
    }

    public enum VehicleSystemType
    {
        Motor,
        Brakes,
        Cooling,
        Electrical,
        Battery,
        Transmission
    }

    public enum UrgencyLevel
    {
        Green,
        Yellow,
        Red
    }

    public enum WorkOrderToolType
    {
        None,
        AdjustableWrench,
        Screwdriver,
        Hammer,
        Pliers,
        LugWrench
    }

    [CreateAssetMenu(
        fileName = "DocumentData_",
        menuName = "EOS/Puzzles/Work Order/Document Data"
    )]
    public sealed class DocumentData : ScriptableObject
    {
        [Header("Identificación")]
        [SerializeField]
        private string documentId;

        [SerializeField]
        private WorkOrderDocumentType documentType;

        [Header("Sistema del vehículo")]
        [SerializeField]
        private VehicleSystemType vehicleSystem;

        [SerializeField]
        private WorkOrderToolType associatedTool =
            WorkOrderToolType.None;

        [Header("Contenido visible")]
        [SerializeField]
        private string displayTitle;

        [SerializeField]
        [TextArea(3, 8)]
        private string greenBodyText;

        [SerializeField]
        [TextArea(3, 8)]
        private string yellowBodyText;

        [SerializeField]
        [TextArea(3, 8)]
        private string redBodyText;

        public string DocumentId => documentId;

        public WorkOrderDocumentType DocumentType =>
            documentType;

        public VehicleSystemType VehicleSystem =>
            vehicleSystem;

        public WorkOrderToolType AssociatedTool =>
            associatedTool;

        public string DisplayTitle =>
            displayTitle;

        public string GetBodyText(UrgencyLevel urgency)
        {
            return urgency switch
            {
                UrgencyLevel.Green => greenBodyText,
                UrgencyLevel.Yellow => yellowBodyText,
                UrgencyLevel.Red => redBodyText,
                _ => string.Empty
            };
        }
    }
}