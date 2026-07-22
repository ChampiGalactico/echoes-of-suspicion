/// <summary>
/// Contrato que todo estado de criatura debe cumplir.
/// La máquina de estados solo conoce esta interfaz — no le importa el tipo concreto.
///
/// Cada estado se implementa como una clase separada (PatrolState, AlertState, etc.).
/// </summary>
public interface ICreatureState
{
    /// <summary>
    /// Se llama UNA vez cuando la criatura entra a este estado.
    /// Ideal para: configurar velocidades, elegir destino inicial, iniciar timers.
    /// </summary>
    void Enter();

    /// <summary>
    /// Se llama cada frame mientras la criatura está en este estado.
    /// Ideal para: chequear condiciones, mover, decidir si cambiar de estado.
    /// </summary>
    void Update();

    /// <summary>
    /// Se llama UNA vez cuando la criatura sale de este estado
    /// (justo antes de entrar a otro).
    /// Ideal para: limpiar timers, cancelar movimientos, resetear flags.
    /// </summary>
    void Exit();
}