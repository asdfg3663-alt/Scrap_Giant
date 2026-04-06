using System;
using UnityEngine;

public static class ShipThrustUtility
{
    public struct DirectionalThrustResult
    {
        public Vector2 force;
        public float torque;
        public float requestedThrust;
        public float appliedThrust;
        public bool hasActiveEngines;
    }

    public static Vector2 ComputeModuleCenterOfMass(ModuleInstance[] modules, Vector2 fallbackCenterOfMass, float minModuleMass)
    {
        if (modules == null || modules.Length == 0)
            return fallbackCenterOfMass;

        float totalModuleMass = 0f;
        Vector2 weightedSum = Vector2.zero;

        for (int i = 0; i < modules.Length; i++)
        {
            ModuleInstance module = modules[i];
            if (module == null || module.data == null)
                continue;

            float moduleMass = Mathf.Max(0f, module.GetMass());
            if (moduleMass < minModuleMass)
                continue;

            totalModuleMass += moduleMass;
            weightedSum += (Vector2)module.transform.position * moduleMass;
        }

        if (totalModuleMass <= 0.0001f)
            return fallbackCenterOfMass;

        return weightedSum / totalModuleMass;
    }

    public static bool TryComputeDirectionalThrustCenter(
        ModuleInstance[] modules,
        Vector2 desiredDirection,
        float directionThreshold,
        out Vector2 thrustCenter,
        out float totalDirectionalThrust)
    {
        thrustCenter = Vector2.zero;
        totalDirectionalThrust = 0f;

        if (modules == null || modules.Length == 0)
            return false;

        Vector2 desiredDir = desiredDirection.sqrMagnitude > 0.0001f
            ? desiredDirection.normalized
            : Vector2.up;

        Vector2 weightedSum = Vector2.zero;
        for (int i = 0; i < modules.Length; i++)
        {
            ModuleInstance module = modules[i];
            if (module == null || module.data == null)
                continue;

            float moduleThrust = module.GetThrust();
            if (moduleThrust <= 0f)
                continue;

            Vector2 engineDir = module.transform.up;
            if (Vector2.Dot(engineDir, desiredDir) < directionThreshold)
                continue;

            weightedSum += (Vector2)module.transform.position * moduleThrust;
            totalDirectionalThrust += moduleThrust;
        }

        if (totalDirectionalThrust <= 0.0001f)
            return false;

        thrustCenter = weightedSum / totalDirectionalThrust;
        return true;
    }

    public static float ComputeModuleBoundsRadius(ModuleInstance[] modules, Vector2 center, float fallbackRadius = 1f)
    {
        if (modules == null || modules.Length == 0)
            return Mathf.Max(0.5f, fallbackRadius);

        float radius = 0f;
        for (int i = 0; i < modules.Length; i++)
        {
            ModuleInstance module = modules[i];
            if (module == null || module.data == null)
                continue;

            float distance = Vector2.Distance(center, module.transform.position) + 0.5f;
            if (distance > radius)
                radius = distance;
        }

        return Mathf.Max(0.5f, radius, fallbackRadius);
    }

    public static DirectionalThrustResult BuildDirectionalThrust(
        ModuleInstance[] modules,
        Vector2 centerOfMass,
        Vector2 desiredDirection,
        float throttle,
        float directionThreshold,
        Func<float, float> effectiveThrustResolver,
        bool refreshEngineVfx = true,
        float maxGimbalDegrees = 0f)
    {
        DirectionalThrustResult result = default;
        if (modules == null || modules.Length == 0)
            return result;

        float throttleAmount = Mathf.Clamp01(Mathf.Abs(throttle));
        if (throttleAmount <= 0f)
            return result;

        Vector2 desiredDir = desiredDirection.sqrMagnitude > 0.0001f
            ? desiredDirection.normalized
            : Vector2.up;

        for (int i = 0; i < modules.Length; i++)
        {
            ModuleInstance module = modules[i];
            if (module == null || module.data == null)
                continue;

            float moduleThrust = module.GetThrust();
            if (moduleThrust <= 0f)
                continue;

            if (Vector2.Dot(module.transform.up, desiredDir) < directionThreshold)
                continue;

            result.requestedThrust += moduleThrust * throttleAmount;
        }

        if (result.requestedThrust <= 0f)
            return result;

        float appliedTotalThrust = effectiveThrustResolver != null
            ? Mathf.Max(0f, effectiveThrustResolver(result.requestedThrust))
            : result.requestedThrust;

        if (appliedTotalThrust <= 0f)
            return result;

        float appliedThrustScale = appliedTotalThrust / result.requestedThrust;
        result.hasActiveEngines = true;

        if (refreshEngineVfx)
            ShipEngineVfx.RefreshForDirection(modules, desiredDir, directionThreshold, throttleAmount * appliedThrustScale);

        for (int i = 0; i < modules.Length; i++)
        {
            ModuleInstance module = modules[i];
            if (module == null || module.data == null)
                continue;

            float moduleThrust = module.GetThrust();
            if (moduleThrust <= 0f)
                continue;

            Vector2 engineDir = module.transform.up;
            if (Vector2.Dot(engineDir, desiredDir) < directionThreshold)
                continue;

            if (maxGimbalDegrees > 0f)
            {
                float maxDegreesDelta = Mathf.Max(0f, maxGimbalDegrees);
                float signedAngle = Vector2.SignedAngle(engineDir, desiredDir);
                float clampedAngle = Mathf.Clamp(signedAngle, -maxDegreesDelta, maxDegreesDelta);
                engineDir = (Quaternion.Euler(0f, 0f, clampedAngle) * engineDir).normalized;
            }

            float appliedThrust = moduleThrust * throttleAmount * appliedThrustScale;
            if (appliedThrust <= 0f)
                continue;

            Vector2 force = engineDir * appliedThrust;
            Vector2 offset = (Vector2)module.transform.position - centerOfMass;

            result.force += force;
            result.torque += offset.x * force.y - offset.y * force.x;
            result.appliedThrust += appliedThrust;
        }

        return result;
    }
}
