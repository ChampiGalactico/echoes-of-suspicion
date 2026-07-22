using Mirror;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public sealed class NetworkPlayerColor : NetworkBehaviour
{
    [SerializeField]
    private Renderer bodyRenderer;

    // Colores disponibles para asignar a los jugadores.
    private static readonly Color[] AvailableColors = new[]
    {
        Color.red,
        Color.blue,
        Color.green,
        Color.yellow,
        Color.magenta,
        Color.cyan
    };

    // Variable sincronizada por red. Cuando el servidor la cambie,
    // se llama al método ApplyColor en todos los clientes.
    [SyncVar(hook = nameof(ApplyColor))]
    private Color assignedColor = Color.white;

    // Se ejecuta solo en el servidor cuando el jugador se conecta.
    public override void OnStartServer()
    {
        base.OnStartServer();

        // Elige un color según cuántos jugadores hay ya conectados.
        int playerIndex = NetworkServer.connections.Count - 1;
        int colorIndex = playerIndex % AvailableColors.Length;

        assignedColor = AvailableColors[colorIndex];
    }

    // Se llama automáticamente cuando assignedColor cambia
    // (tanto en el servidor como en los clientes).
    private void ApplyColor(Color oldColor, Color newColor)
    {
        if (bodyRenderer == null)
        {
            return;
        }

        // Crea una instancia del material para no modificar el original.
        bodyRenderer.material.color = newColor;
    }

    // Al inicializar en el cliente, aplica el color inicial.
    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyColor(Color.white, assignedColor);
    }
}