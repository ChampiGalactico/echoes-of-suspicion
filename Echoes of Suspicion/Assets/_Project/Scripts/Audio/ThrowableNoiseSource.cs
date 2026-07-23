using Mirror;
using UnityEngine;

/// <summary>
/// Genera un evento de ruido cuando el objeto impacta algo con suficiente fuerza.
/// La intensidad depende de la velocidad de impacto Y del tipo de material
/// (una piedra suena fuerte, una almohada casi nada, con la misma velocidad).
///
/// Requiere Rigidbody. Solo procesa el impacto en el servidor.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(AudioSource))]
public sealed class ThrowableNoiseSource : NetworkBehaviour
{
    [Header("Material")]
    [SerializeField, Tooltip("Tipo de material de este objeto. Determina cuánto multiplica el ruido base.")]
    private NoiseMaterialType materialType = NoiseMaterialType.Stone;

    [SerializeField, Tooltip("Asset compartido con los multiplicadores por tipo de material.")]
    private NoiseMaterialConfig materialConfig;

    [Header("Noise on Impact")]
    [SerializeField, Tooltip("Velocidad de impacto mínima para generar ruido.")]
    private float minImpactSpeed = 1.5f;

    [SerializeField, Tooltip("Velocidad de impacto que genera el ruido base máximo (intensidad 1, antes del multiplicador de material).")]
    private float maxImpactSpeed = 8f;

    [SerializeField, Tooltip("Tiempo mínimo entre impactos que generan ruido (evita spam si rebota varias veces).")]

    private AudioSource audioSource;
    private float impactCooldown = 0.3f;

    private float lastImpactTime;

    private void OnCollisionEnter(Collision collision)
    {
        if (!isServer)
        {
            return;
        }

        if (Time.time - lastImpactTime < impactCooldown)
        {
            return;
        }

        float speed = collision.relativeVelocity.magnitude;
        if (speed < minImpactSpeed)
        {
            return;
        }

        lastImpactTime = Time.time;

        float baseIntensity = Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, speed);
        float materialMultiplier = materialConfig != null
            ? materialConfig.GetMultiplier(materialType)
            : 1f;

        float intensity = Mathf.Clamp01(baseIntensity * materialMultiplier);
        Vector3 impactPosition = collision.GetContact(0).point;

        NoiseEvent noiseEvent = new NoiseEvent(impactPosition, intensity, NoiseSource.ObjectImpact, netId);
        NoiseEventBus.Publish(noiseEvent);

        RpcNotifyImpactNoise(impactPosition, intensity);
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.spatialBlend = 1f; // 1 = sonido 3D completo (se atenúa con distancia)
        audioSource.playOnAwake = false;
    }

    [ClientRpc]
    private void RpcNotifyImpactNoise(Vector3 position, float intensity)
    {
        PlayImpactSound(); // esto sí debe sonar en todos, incluido el host

        if (isServer)
        {
            return;
        }

        NoiseEvent noiseEvent = new NoiseEvent(position, intensity, NoiseSource.ObjectImpact, netId);
        NoiseEventBus.Publish(noiseEvent);
    }

    private void PlayImpactSound()
    {
        if (materialConfig == null)
        {
            return;
        }

        AudioClip clip = materialConfig.GetImpactSound(materialType);
        if (clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}