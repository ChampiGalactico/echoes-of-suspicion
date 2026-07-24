/// <summary>
/// Implementada por los estados de criatura que tienen un jugador target
/// específico (Alert, Chase, Search, Attack). Permite que CreatureController
/// notifique quién es el target al disparar el evento de cambio de estado,
/// sin necesidad de que quien escucha haga polling.
/// </summary>
public interface ITargetedState
{
    uint TargetPlayerNetId { get; }
}