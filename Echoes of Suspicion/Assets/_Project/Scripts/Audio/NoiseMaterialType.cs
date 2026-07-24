/// <summary>
/// Tipos de material para objetos que generan ruido al impactar.
/// Determina qué tan fuerte suena un objeto respecto a otro con la misma velocidad.
/// </summary>
public enum NoiseMaterialType
{
    Stone,
    Wood,
    Metal,
    Glass,
    Soft // Almohadas, peluches, tela — casi no hacen ruido.
}