using UnityEngine;

[DisallowMultipleComponent]
public class ShipEngineVfx : MonoBehaviour
{
    const string ExhaustAnchorObjectName = "ExhaustAnchor";
    const string ExhaustObjectName = "ExhaustVfx";
    const string ExhaustCoreObjectName = "ExhaustCore";
    const float ExhaustHoldDuration = 0.12f;

    static Material sharedExhaustMaterial;
    static Sprite sharedExhaustSprite;

    [SerializeField] Transform exhaustAnchor;
    [SerializeField] ParticleSystem exhaustParticleSystem;
    [SerializeField] SpriteRenderer exhaustCoreRenderer;
    [SerializeField] Vector3 exhaustLocalOffset = new Vector3(0f, -2.2f, 0f);
    [SerializeField] float minEmissionRate = 24f;
    [SerializeField] float maxEmissionRate = 90f;
    [SerializeField] float minVelocity = 1.8f;
    [SerializeField] float maxVelocity = 4.2f;
    [SerializeField] float minStartSize = 0.12f;
    [SerializeField] float maxStartSize = 0.22f;
    [SerializeField] float startLifetime = 0.18f;
    [SerializeField, HideInInspector] bool anchorOffsetInitialized;

    bool exhaustActive;
    float exhaustActiveUntil;

    void Awake()
    {
        EnsureExhaustParticleSystem();
        DisableImmediate();
    }

    void OnValidate()
    {
        EnsureExhaustAnchor();
        RefreshExhaustAnchors();
        SyncRendererSorting();
    }

    void LateUpdate()
    {
        if (exhaustActive && Time.unscaledTime > exhaustActiveUntil)
            DisableImmediate();
    }

    void OnDisable()
    {
        DisableImmediate();
    }

    public void SetExhaustActive(bool active, float intensity = 1f)
    {
        EnsureExhaustParticleSystem();

        if (!active || intensity <= 0f)
        {
            DisableImmediate();
            return;
        }

        float normalizedIntensity = Mathf.Clamp01(intensity);

        var main = exhaustParticleSystem.main;
        main.startLifetime = startLifetime;
        main.startSize = Mathf.Lerp(minStartSize, maxStartSize, normalizedIntensity);

        var emission = exhaustParticleSystem.emission;
        emission.rateOverTime = Mathf.Lerp(minEmissionRate, maxEmissionRate, normalizedIntensity);

        var velocity = exhaustParticleSystem.velocityOverLifetime;
        velocity.y = new ParticleSystem.MinMaxCurve(-Mathf.Lerp(minVelocity, maxVelocity, normalizedIntensity));

        UpdateCoreRenderer(normalizedIntensity);

        if (!exhaustParticleSystem.isPlaying)
            exhaustParticleSystem.Play(true);

        exhaustActive = true;
        exhaustActiveUntil = Time.unscaledTime + ExhaustHoldDuration;
    }

    public static void RefreshForDirection(ModuleInstance[] modules, Vector2 desiredDirection, float directionThreshold, float intensity)
    {
        if (modules == null || modules.Length == 0)
            return;

        Vector2 desiredDir = desiredDirection.sqrMagnitude > 0.0001f
            ? desiredDirection.normalized
            : Vector2.up;

        float clampedIntensity = Mathf.Clamp01(intensity);
        if (clampedIntensity <= 0f)
            return;

        for (int i = 0; i < modules.Length; i++)
        {
            ModuleInstance module = modules[i];
            if (module == null || module.data == null || module.data.type != ModuleType.Engine)
                continue;

            if (module.GetThrust() <= 0f)
                continue;

            if (Vector2.Dot(module.transform.up, desiredDir) < directionThreshold)
                continue;

            ShipEngineVfx vfx = module.GetComponent<ShipEngineVfx>();
            if (vfx == null)
                vfx = module.gameObject.AddComponent<ShipEngineVfx>();

            vfx.SetExhaustActive(true, clampedIntensity);
        }
    }

    void EnsureExhaustParticleSystem()
    {
        EnsureExhaustAnchor();

        if (exhaustParticleSystem == null)
        {
            Transform existing = transform.Find(ExhaustObjectName);
            if (existing != null)
                exhaustParticleSystem = existing.GetComponent<ParticleSystem>();
        }

        if (exhaustParticleSystem == null)
            exhaustParticleSystem = CreateExhaustParticleSystem();

        if (exhaustCoreRenderer == null)
            exhaustCoreRenderer = CreateOrFindCoreRenderer();

        RefreshExhaustAnchors();
        SyncRendererSorting();
    }

    void EnsureExhaustAnchor()
    {
        if (exhaustAnchor == null)
        {
            Transform existing = transform.Find(ExhaustAnchorObjectName);
            if (existing != null)
            {
                exhaustAnchor = existing;
                if (!anchorOffsetInitialized)
                {
                    exhaustLocalOffset = existing.localPosition;
                    anchorOffsetInitialized = true;
                }
            }
        }

        if (exhaustAnchor == null)
        {
            var anchorObject = new GameObject(ExhaustAnchorObjectName);
            anchorObject.transform.SetParent(transform, false);
            anchorObject.transform.localPosition = exhaustLocalOffset;
            anchorObject.transform.localRotation = Quaternion.identity;
            anchorObject.transform.localScale = Vector3.one;
            exhaustAnchor = anchorObject.transform;
            anchorOffsetInitialized = true;
        }
    }

    ParticleSystem CreateExhaustParticleSystem()
    {
        GameObject exhaustObject = new GameObject(ExhaustObjectName);
        exhaustObject.transform.SetParent(exhaustAnchor != null ? exhaustAnchor : transform, false);
        exhaustObject.transform.localPosition = Vector3.zero;
        exhaustObject.transform.localRotation = Quaternion.identity;
        exhaustObject.transform.localScale = Vector3.one;

        ParticleSystem particleSystem = exhaustObject.AddComponent<ParticleSystem>();
        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particleSystem.main;
        main.playOnAwake = false;
        main.loop = true;
        main.startLifetime = startLifetime;
        main.startSpeed = 0f;
        main.startSize = minStartSize;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 128;
        main.gravityModifier = 0f;

        var emission = particleSystem.emission;
        emission.rateOverTime = 0f;

        var shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.18f, 0.04f, 0f);

        var velocity = particleSystem.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(0f);
        velocity.y = new ParticleSystem.MinMaxCurve(-minVelocity);
        velocity.z = new ParticleSystem.MinMaxCurve(0f);

        var colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient colorGradient = new Gradient();
        colorGradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.97f, 0.86f), 0f),
                new GradientColorKey(new Color(1f, 0.55f, 0.18f), 0.35f),
                new GradientColorKey(new Color(0.8f, 0.18f, 0.08f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.75f, 0.3f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(colorGradient);

        var sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.95f),
            new Keyframe(0.45f, 0.7f),
            new Keyframe(1f, 0f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var noise = particleSystem.noise;
        noise.enabled = true;
        noise.strength = 0.18f;
        noise.frequency = 0.7f;
        noise.scrollSpeed = 0.5f;

        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.lengthScale = 1.05f;
        renderer.velocityScale = 0.35f;
        EnsureRendererMaterial(renderer);

        return particleSystem;
    }

    SpriteRenderer CreateOrFindCoreRenderer()
    {
        Transform existing = transform.Find(ExhaustCoreObjectName);
        if (existing != null)
        {
            SpriteRenderer existingRenderer = existing.GetComponent<SpriteRenderer>();
            if (existingRenderer != null)
                return existingRenderer;
        }

        GameObject coreObject = new GameObject(ExhaustCoreObjectName);
        coreObject.transform.SetParent(exhaustAnchor != null ? exhaustAnchor : transform, false);
        coreObject.transform.localPosition = Vector3.zero;
        coreObject.transform.localRotation = Quaternion.identity;
        coreObject.transform.localScale = Vector3.one;

        SpriteRenderer spriteRenderer = coreObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetOrCreateExhaustSprite();
        spriteRenderer.color = new Color(1f, 0.6f, 0.15f, 0f);
        spriteRenderer.enabled = false;
        return spriteRenderer;
    }

    void SyncRendererSorting()
    {
        if (exhaustParticleSystem == null)
            return;

        ParticleSystemRenderer particleRenderer = exhaustParticleSystem.GetComponent<ParticleSystemRenderer>();
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (particleRenderer == null || spriteRenderer == null)
            return;

        EnsureRendererMaterial(particleRenderer);
        particleRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        particleRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;

        if (exhaustCoreRenderer != null)
        {
            exhaustCoreRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            exhaustCoreRenderer.sortingOrder = spriteRenderer.sortingOrder + 2;
        }
    }

    Sprite GetOrCreateExhaustSprite()
    {
        if (sharedExhaustSprite != null)
            return sharedExhaustSprite;

        Texture2D texture = Texture2D.whiteTexture;
        sharedExhaustSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.92f),
            texture.width);
        sharedExhaustSprite.name = "Runtime_EngineExhaust_Sprite";
        return sharedExhaustSprite;
    }

    void EnsureRendererMaterial(ParticleSystemRenderer particleRenderer)
    {
        if (particleRenderer == null)
            return;

        if (sharedExhaustMaterial == null)
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                Shader.Find("Particles/Standard Unlit") ??
                Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Unlit/Texture");

            if (shader != null)
            {
                sharedExhaustMaterial = new Material(shader)
                {
                    name = "Runtime_EngineExhaust_Mat"
                };
            }
        }

        if (sharedExhaustMaterial != null)
            particleRenderer.sharedMaterial = sharedExhaustMaterial;
    }

    void UpdateCoreRenderer(float intensity)
    {
        if (exhaustCoreRenderer == null)
            return;

        float clampedIntensity = Mathf.Clamp01(intensity);
        RefreshExhaustAnchors();
        exhaustCoreRenderer.transform.localScale = new Vector3(
            Mathf.Lerp(0.1f, 0.16f, clampedIntensity),
            Mathf.Lerp(0.2f, 0.5f, clampedIntensity),
            1f);
        exhaustCoreRenderer.color = new Color(1f, 0.72f, 0.22f, Mathf.Lerp(0.35f, 0.7f, clampedIntensity));
        exhaustCoreRenderer.enabled = true;
    }

    void RefreshExhaustAnchors()
    {
        if (exhaustAnchor != null)
        {
            exhaustAnchor.localRotation = Quaternion.identity;
            exhaustAnchor.localPosition = exhaustLocalOffset;
        }

        if (exhaustParticleSystem != null)
            exhaustParticleSystem.transform.localRotation = Quaternion.identity;

        if (exhaustCoreRenderer != null)
            exhaustCoreRenderer.transform.localRotation = Quaternion.identity;
    }

    void DisableImmediate()
    {
        if (exhaustParticleSystem == null)
        {
            exhaustActive = false;
            return;
        }

        var emission = exhaustParticleSystem.emission;
        emission.rateOverTime = 0f;

        if (exhaustParticleSystem.isPlaying)
            exhaustParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (exhaustCoreRenderer != null)
            exhaustCoreRenderer.enabled = false;

        exhaustActive = false;
        exhaustActiveUntil = 0f;
    }
}
