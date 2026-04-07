using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class SavedModuleData
{
    public ModuleType type;
    public int upgradeLevel;
    public int hp;
    public float heat;
    public int gridX;
    public int gridY;
    public int rot90;
}

[Serializable]
public sealed class SavedGameData
{
    public int version = 1;
    public float score;
    public float scrap;
    public int ammo;
    public int assemblyPriorityMode;
    public float energyCurrent;
    public float fuelCurrent;
    public float playerPosX;
    public float playerPosY;
    public float playerRotZ;
    public List<SavedModuleData> modules = new();
}

public static class GameSaveSystem
{
    const string SaveFileName = "savegame.json";
    static readonly Color ScrapColor = new Color(0.97f, 0.63f, 0.22f, 1f);
    static readonly Color AmmoColor = new Color(0.96f, 0.32f, 0.24f, 1f);
    static bool sessionStarted;
    static bool rewardedQuitPending;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        sessionStarted = false;
        rewardedQuitPending = false;
    }

    public static bool HasStartedSession => sessionStarted;
    public static bool RewardedQuitPending => rewardedQuitPending;
    public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    public static void MarkSessionStarted()
    {
        sessionStarted = true;
        PlayerHudRuntime.ResetSessionBestScore();
    }

    public static void EndSession(bool deleteSave = true)
    {
        sessionStarted = false;
        rewardedQuitPending = false;
        if (deleteSave)
            DeleteSave();
    }

    public static void BeginRewardedQuitFlow()
    {
        rewardedQuitPending = true;
        DeleteSave();
    }

    public static bool CompleteRewardedQuitWithSave()
    {
        bool saved = SaveCurrentGame(force: true);
        rewardedQuitPending = false;
        sessionStarted = false;

        if (!saved)
            DeleteSave();

        return saved;
    }

    public static void CompleteRewardedQuitWithoutSave()
    {
        rewardedQuitPending = false;
        EndSession(deleteSave: true);
    }

    public static bool HasSaveFile()
    {
        return File.Exists(SavePath);
    }

    public static float GetSavedScore()
    {
        return TryLoad(out SavedGameData data) ? Mathf.Max(0f, data.score) : 0f;
    }

    public static void DeleteSave()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath);
    }

    public static bool SaveCurrentGame(bool force = false)
    {
        if (!force && rewardedQuitPending)
            return false;

        if (!force && !sessionStarted)
            return false;

        ShipStats playerShip = FindPlayerShip();
        if (playerShip == null)
            return false;

        SavedGameData data = CaptureCurrentGame(playerShip);
        if (data == null)
            return false;

        try
        {
            string directory = Path.GetDirectoryName(SavePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to save game: {ex.Message}");
            return false;
        }
    }

    public static bool LoadIntoCurrentScene()
    {
        if (!TryLoad(out SavedGameData data))
            return false;

        ShipStats playerShip = FindPlayerShip();
        if (playerShip == null)
            return false;

        bool loaded = ApplyToCurrentScene(playerShip, data);
        if (loaded)
        {
            sessionStarted = true;
            PlayerHudRuntime.SetSessionBestScore(data.score);
        }

        return loaded;
    }

    static bool TryLoad(out SavedGameData data)
    {
        data = null;
        if (!File.Exists(SavePath))
            return false;

        try
        {
            string json = File.ReadAllText(SavePath);
            if (string.IsNullOrWhiteSpace(json))
                return false;

            data = JsonUtility.FromJson<SavedGameData>(json);
            return data != null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to load save game: {ex.Message}");
            return false;
        }
    }

    static SavedGameData CaptureCurrentGame(ShipStats playerShip)
    {
        PlayerHudRuntime hud = PlayerHudRuntime.Instance;
        if (hud == null)
            return null;

        SavedGameData data = new SavedGameData
        {
            score = Mathf.Max(0f, playerShip.totalScore),
            scrap = hud.GetResourceValue("scrap"),
            ammo = hud.GetAmmoAmount("ammo"),
            assemblyPriorityMode = (int)playerShip.CurrentAssemblyPriorityMode,
            energyCurrent = playerShip.energyCurrent,
            fuelCurrent = playerShip.fuelCurrent,
            playerPosX = playerShip.transform.position.x,
            playerPosY = playerShip.transform.position.y,
            playerRotZ = playerShip.transform.eulerAngles.z
        };

        ModuleInstance[] modules = playerShip.GetComponentsInChildren<ModuleInstance>(true);
        for (int i = 0; i < modules.Length; i++)
        {
            ModuleInstance module = modules[i];
            if (module == null || module.data == null)
                continue;

            Vector2Int grid = Vector2Int.zero;
            int rot90 = 0;

            ModuleAttachment attachment = module.GetComponent<ModuleAttachment>();
            if (attachment != null)
            {
                grid = attachment.gridPos;
                rot90 = attachment.rot90;
            }
            else
            {
                Vector3 localPosition = playerShip.transform.InverseTransformPoint(module.transform.position);
                grid = new Vector2Int(Mathf.RoundToInt(localPosition.x), Mathf.RoundToInt(localPosition.y));
                rot90 = Mathf.RoundToInt(module.transform.localEulerAngles.z / 90f) % 4;
            }

            data.modules.Add(new SavedModuleData
            {
                type = module.data.type,
                upgradeLevel = module.upgradeLevel,
                hp = module.hp,
                heat = module.currentHeat,
                gridX = grid.x,
                gridY = grid.y,
                rot90 = rot90
            });
        }

        return data;
    }

    static bool ApplyToCurrentScene(ShipStats playerShip, SavedGameData data)
    {
        if (playerShip == null || data == null)
            return false;

        playerShip.transform.position = new Vector3(data.playerPosX, data.playerPosY, playerShip.transform.position.z);
        playerShip.transform.rotation = Quaternion.Euler(0f, 0f, data.playerRotZ);

        Rigidbody2D shipBody = playerShip.GetComponent<Rigidbody2D>();
        if (shipBody != null)
        {
            shipBody.linearVelocity = Vector2.zero;
            shipBody.angularVelocity = 0f;
        }

        ModuleInstance coreInstance = null;
        ModuleInstance[] currentModules = playerShip.GetComponentsInChildren<ModuleInstance>(true);
        for (int i = 0; i < currentModules.Length; i++)
        {
            ModuleInstance module = currentModules[i];
            if (module == null || module.data == null)
                continue;

            if (module.data.type == ModuleType.Core && coreInstance == null)
            {
                coreInstance = module;
                continue;
            }

            module.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(module.gameObject);
        }

        SavedModuleData savedCore = null;
        for (int i = 0; i < data.modules.Count; i++)
        {
            if (data.modules[i].type == ModuleType.Core)
            {
                savedCore = data.modules[i];
                break;
            }
        }

        if (coreInstance != null)
            ApplySavedModuleState(coreInstance, savedCore);

        for (int i = 0; i < data.modules.Count; i++)
        {
            SavedModuleData moduleData = data.modules[i];
            if (moduleData == null || moduleData.type == ModuleType.Core)
                continue;

            GameObject prefab = WorldSpawnDirector.GetModulePrefabByType(moduleData.type);
            if (prefab == null)
                continue;

            GameObject moduleGO = UnityEngine.Object.Instantiate(prefab, playerShip.transform.position, playerShip.transform.rotation);
            AttachModuleToShip(playerShip.transform, moduleGO.transform, new Vector2Int(moduleData.gridX, moduleData.gridY), moduleData.rot90);

            ModuleInstance instance = moduleGO.GetComponent<ModuleInstance>();
            if (instance != null)
                ApplySavedModuleState(instance, moduleData);
        }

        playerShip.Rebuild();
        playerShip.SetAssemblyPriorityMode((AssemblyPriorityMode)Mathf.Clamp(
            data.assemblyPriorityMode,
            (int)AssemblyPriorityMode.FuelFirst,
            (int)AssemblyPriorityMode.AmmoFirst));
        playerShip.energyCurrent = Mathf.Clamp(data.energyCurrent, 0f, playerShip.energyMax);
        playerShip.fuelCurrent = Mathf.Clamp(data.fuelCurrent, 0f, playerShip.fuelMax);
        playerShip.RefreshHudNow();
        PlayerHudRuntime.SetSessionBestScore(playerShip.totalScore);

        PlayerHudRuntime hud = PlayerHudRuntime.Instance;
        if (hud != null)
        {
            hud.SetResource("scrap", LocalizationManager.Get("resource.scrap", "Scrap"), data.scrap, ScrapColor);
            hud.SetAmmo("ammo", LocalizationManager.Get("resource.ammo", "Ammo"), data.ammo, AmmoColor);
        }

        return true;
    }

    static void ApplySavedModuleState(ModuleInstance instance, SavedModuleData data)
    {
        if (instance == null || instance.data == null)
            return;

        int savedUpgrade = data != null ? Mathf.Max(0, data.upgradeLevel) : 0;
        instance.upgradeLevel = savedUpgrade;
        instance.RefreshVisualState(forceReset: true);

        if (data != null)
        {
            instance.hp = Mathf.Clamp(data.hp, 0, instance.maxHp);
            instance.currentHeat = Mathf.Clamp(data.heat, 0f, instance.GetMaxHeat());
        }
    }

    static void AttachModuleToShip(Transform shipRoot, Transform moduleTf, Vector2Int grid, int rot90)
    {
        if (shipRoot == null || moduleTf == null)
            return;

        moduleTf.SetParent(shipRoot, false);
        moduleTf.localPosition = new Vector3(grid.x, grid.y, 0f);
        moduleTf.localRotation = Quaternion.Euler(0f, 0f, rot90 * 90f);
        moduleTf.localScale = Vector3.one;

        Rigidbody2D rb = moduleTf.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
            rb.Sleep();
        }

        ModuleAttachment attachment = moduleTf.GetComponent<ModuleAttachment>();
        if (attachment == null)
            attachment = moduleTf.gameObject.AddComponent<ModuleAttachment>();

        attachment.shipRoot = shipRoot;
        attachment.gridPos = grid;
        attachment.rot90 = rot90;

        IgnoreCollisionsWithShip(shipRoot, moduleTf);
    }

    static void IgnoreCollisionsWithShip(Transform shipRoot, Transform moduleTf)
    {
        Collider2D[] myColliders = moduleTf.GetComponentsInChildren<Collider2D>(true);
        Collider2D[] shipColliders = shipRoot.GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < myColliders.Length; i++)
        {
            Collider2D a = myColliders[i];
            if (a == null)
                continue;

            for (int j = 0; j < shipColliders.Length; j++)
            {
                Collider2D b = shipColliders[j];
                if (b == null || a == b)
                    continue;

                if (b.transform.IsChildOf(moduleTf))
                    continue;

                Physics2D.IgnoreCollision(a, b, true);
            }
        }
    }

    static ShipStats FindPlayerShip()
    {
        if (ShipCombatInput.ActivePlayerShip != null)
            return ShipCombatInput.ActivePlayerShip;

        ShipStats[] ships = UnityEngine.Object.FindObjectsByType<ShipStats>(FindObjectsSortMode.None);
        for (int i = 0; i < ships.Length; i++)
        {
            ShipStats ship = ships[i];
            if (ship != null && ship.isPlayerShip)
                return ship;
        }

        return null;
    }
}

[DisallowMultipleComponent]
public sealed class GameSaveRuntime : MonoBehaviour
{
    const float AutoSaveInterval = 10f;
    static GameSaveRuntime instance;
    float nextAutoSaveTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject go = new GameObject("GameSaveRuntime");
        instance = go.AddComponent<GameSaveRuntime>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        nextAutoSaveTime = Time.unscaledTime + AutoSaveInterval;
    }

    void Update()
    {
        if (!GameSaveSystem.HasStartedSession || GameRuntimeState.GameplayBlocked)
            return;

        if (Time.unscaledTime < nextAutoSaveTime)
            return;

        nextAutoSaveTime = Time.unscaledTime + AutoSaveInterval;
        GameSaveSystem.SaveCurrentGame();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && !GameSaveSystem.RewardedQuitPending)
            GameSaveSystem.SaveCurrentGame();
    }

    void OnApplicationQuit()
    {
        if (!GameSaveSystem.RewardedQuitPending)
            GameSaveSystem.SaveCurrentGame();
    }
}
