using System;
using UnityEngine;

/// <summary>
/// Configuración central de cuánto ruido multiplica cada tipo de material.
/// Un solo asset compartido por todos los objetos lanzables del proyecto.
///
/// Se crea desde Unity con: Create → Echoes → Audio → Noise Material Config.
/// </summary>
[CreateAssetMenu(fileName = "NoiseMaterialConfig", menuName = "Echoes/Audio/Noise Material Config")]
public sealed class NoiseMaterialConfig : ScriptableObject
{
    [Serializable]
    public struct MaterialMultiplier
    {
        public NoiseMaterialType materialType;

        [Range(0f, 2f), Tooltip("1 = ruido normal. 0 = silencioso. 2 = el doble de ruidoso.")]
        public float multiplier;

        [Tooltip("Clip de audio que se reproduce al impactar con este material.")]
        public AudioClip impactSound;
    }

    [SerializeField]
    private MaterialMultiplier[] multipliers = new MaterialMultiplier[]
    {
        new MaterialMultiplier { materialType = NoiseMaterialType.Stone, multiplier = 1.2f },
        new MaterialMultiplier { materialType = NoiseMaterialType.Wood, multiplier = 0.9f },
        new MaterialMultiplier { materialType = NoiseMaterialType.Metal, multiplier = 1.4f },
        new MaterialMultiplier { materialType = NoiseMaterialType.Glass, multiplier = 1.1f },
        new MaterialMultiplier { materialType = NoiseMaterialType.Soft, multiplier = 0.15f },
    };

    /// <summary>
    /// Retorna el multiplicador configurado para un tipo de material.
    /// Si no está en la lista (config incompleta), retorna 1 como valor neutro.
    /// </summary>
    public float GetMultiplier(NoiseMaterialType materialType)
    {
        foreach (var entry in multipliers)
        {
            if (entry.materialType == materialType)
            {
                return entry.multiplier;
            }
        }

        Debug.LogWarning($"[NoiseMaterialConfig] No hay multiplicador configurado para {materialType}. Usando 1.");
        return 1f;
    }

    public AudioClip GetImpactSound(NoiseMaterialType materialType)
{
    foreach (var entry in multipliers)
    {
        if (entry.materialType == materialType)
        {
            return entry.impactSound;
        }
    }

    return null;
}
}
