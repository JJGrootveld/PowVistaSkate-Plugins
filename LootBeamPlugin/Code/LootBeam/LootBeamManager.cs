using UnityEngine;
using System.Collections.Generic;
using Mirror;

namespace LootBeam
{
    public class LootBeamManager : MonoBehaviour
    {
        public static LootBeamManager? Instance;
        private Dictionary<string, GameObject> _beamPrefabs = new();
        private readonly Dictionary<int, GameObject> _activeBeams = new();
        private readonly HashSet<int> _pendingItems = new();

        [Header("Settings")]
        public float beamZOffset = 0.18f;
        public float soundVolume = 0.65f;

        private AudioSource? _audioSource;

        // ID-based categorization (authoritative lists)
        private static readonly HashSet<int> ShinySkateboardIds = new HashSet<int>
        {
            502,503,504,505,506,507,508,509,510,511,512,513,514,515,516,517,518,519,520,521,522,523,524,525,526,527,528,529,530,531,532,533,534,535,576,
            // New mobs
            606,607,608,609,610
        };

        private static readonly HashSet<int> ShinyHoodieIds = new HashSet<int>
        {
            536,537,538,539,540,541,542,543,544,545,546,547,548,549,550,551,552,553,554,555,556,557,558,559,560,561,562,563,564,565,566,567,574,
            // New mobs
            595,596,597,598,599
        };

        private static readonly HashSet<int> EggIds = new HashSet<int>
        {
            200,221,222,223,224,225,226,227,228,229,230,231,232,233,234,235,236,237,238,239,240,253,254,255,309,310,311,315,354,356,358,360,362,364,366,368,370,372,374,376,378,380,382,446,448,450,451,452,456,458,460,490,492,494,496,498,500,577,
            // New mobs
            581,583,585,587,589
        };

        private static readonly HashSet<int> HoodieIds = new HashSet<int>
        {
            10,11,12,13,14,15,16,17,18,19,20,21,28,31,32,33,34,35,36,38,41,42,43,56,63,64,67,68,72,73,75,76,94,96,97,98,99,100,106,107,110,111,112,117,184,185,186,197,198,199,245,246,247,251,306,308,335,336,337,338,339,346,347,348,349,350,351,352,353,438,439,440,441,442,443,444,445,462,463,464,465,466,467,468,469,470,471,472,
            // New mobs + related
            591,592,593,594,600,601
        };

        private static readonly HashSet<int> MobSkateboardIds = new HashSet<int>
        {
            6,7,39,57,61,62,65,66,69,71,74,81,82,83,84,85,86,87,88,89,90,91,92,93,95,101,102,103,104,105,108,109,113,114,115,116,132,133,134,135,136,137,187,188,189,190,191,192,193,194,195,242,243,244,248,249,250,252,307,318,319,320,321,322,323,324,340,341,342,343,344,345,430,431,432,433,434,435,436,437,474,475,476,477,478,479,480,481,484,575,
            // New mobs
            602,604,605,611,612
        };

        private bool _isInitialized = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Initialize(Dictionary<string, GameObject> beamPrefabs)
        {
            if (_isInitialized) return;
            _beamPrefabs = beamPrefabs;
            Debug.Log($"[LootBeamManager] Initialized with {beamPrefabs.Count} beam prefabs");

            // Create audio source child (use defaults for wide compatibility)
            var audioObj = new GameObject("LootBeamAudio");
            audioObj.transform.SetParent(transform);
            _audioSource = audioObj.AddComponent<AudioSource>();

            InvokeRepeating(nameof(CheckForItems), 1f, 0.5f);
            _isInitialized = true;
        }

        public void SetBeamPrefabs(Dictionary<string, GameObject> beamPrefabs)
        {
            if (beamPrefabs == null) return;
            _beamPrefabs = beamPrefabs;
        }

        private static bool IsTargetId(int id)
        {
            return ShinySkateboardIds.Contains(id) || ShinyHoodieIds.Contains(id) || EggIds.Contains(id) || HoodieIds.Contains(id) || MobSkateboardIds.Contains(id);
        }

        private void CheckForItems()
        {
            var gameObjects = Object.FindObjectsOfType<GameObject>();
            int totalTargetItems = 0;
            int beamsCreated = 0;
            int activeBeams = 0;
            var seenIds = new HashSet<int>();

            foreach (var go in gameObjects)
            {
                if (go == null) continue;

                int idValue = GetItemNumericId(go);
                if (idValue <= 0 || !IsTargetId(idValue))
                {
                    continue;
                }

                totalTargetItems++;
                int id = go.GetInstanceID();
                seenIds.Add(id);

                if (!IsItemReady(go))
                {
                    _pendingItems.Add(id);
                    continue;
                }
                _pendingItems.Remove(id);

                if (_activeBeams.ContainsKey(id))
                {
                    activeBeams++;
                    continue;
                }

                LootRarity rarity = DetermineItemRarityById(idValue);
                GameObject? beamPrefab = GetBeamPrefabForRarity(rarity);
                if (beamPrefab == null) continue;

                Vector3 beamPosition = GetBeamPosition(go.transform.position);
                CreateBeam(beamPosition, beamPrefab, $"Item {idValue}", id);
                beamsCreated++;
            }

            // Cleanup beams whose items are gone
            var toRemove = new List<int>();
            foreach (var id in _activeBeams.Keys)
            {
                if (!seenIds.Contains(id)) toRemove.Add(id);
            }
            foreach (var id in toRemove)
            {
                if (_activeBeams.TryGetValue(id, out var beam))
                {
                    if (beam != null) Destroy(beam);
                    _activeBeams.Remove(id);
                }
            }
        }

        private bool IsItemReady(GameObject go)
        {
            if (go == null) return false;
            if (!go.activeInHierarchy) return false;
            return true;
        }

        private Vector3 GetBeamPosition(Vector3 itemPosition)
        {
            // Place slightly above the item on Z for safe rendering
            return new Vector3(itemPosition.x, itemPosition.y, itemPosition.z + beamZOffset);
        }

        private LootRarity DetermineItemRarity(string itemName)
        {
            if (itemName.Contains("Shiny") && itemName.Contains("Hoodie")) return LootRarity.ExtremelyRare;
            if (itemName.Contains("Shiny") && itemName.Contains("Skateboard")) return LootRarity.SuperRare;
            if (itemName.Contains("Hoodie")) return LootRarity.VeryRare;
            if (itemName.Contains("Egg")) return LootRarity.Rare;
            if (itemName.Contains("Skateboard")) return LootRarity.Uncommon;
            return LootRarity.Uncommon;
        }

        private LootRarity DetermineItemRarityById(int id)
        {
            if (ShinyHoodieIds.Contains(id)) return LootRarity.ExtremelyRare;
            if (ShinySkateboardIds.Contains(id)) return LootRarity.SuperRare;
            if (HoodieIds.Contains(id)) return LootRarity.VeryRare;
            if (EggIds.Contains(id)) return LootRarity.Rare;
            if (MobSkateboardIds.Contains(id)) return LootRarity.Uncommon;
            return LootRarity.Uncommon;
        }

        private GameObject? GetBeamPrefabForRarity(LootRarity rarity)
        {
            string key = rarity switch
            {
                LootRarity.Uncommon => "UncommonBeam",
                LootRarity.Rare => "RareBeam",
                LootRarity.VeryRare => "VeryRareBeam",
                LootRarity.SuperRare => "SuperRareBeam",
                LootRarity.ExtremelyRare => "ExtremelyRareBeam",
                _ => "UncommonBeam"
            };

            if (_beamPrefabs.TryGetValue(key, out var prefab) && prefab != null) return prefab;
            if (AssetbundleLoader.HasAssetbundle(key)) return AssetbundleLoader.GetAssetbundle(key);
            return null;
        }

        private void CreateBeam(Vector3 position, GameObject beamPrefab, string itemName, int itemId)
        {
            try
            {
                GameObject beamInstance = Instantiate(beamPrefab, position, Quaternion.identity);
                // Ensure upright in 2D per game coordinates
                beamInstance.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

                // Play sparkle sound from AssetBundle using local AudioSource
                if (_audioSource != null && AssetbundleLoader.LootBeamSound != null)
                {
                    try
                    {
                        var clip = AssetbundleLoader.LootBeamSound;
                        _audioSource.Stop();
                        _audioSource.clip = clip;
                        _audioSource.volume = soundVolume;
                        _audioSource.loop = false;
                        _audioSource.Play();
                    }
                    catch {}
                }
                
                var beamEffect = beamInstance.GetComponent<LootBeamEffect>();
                if (beamEffect == null) beamEffect = beamInstance.AddComponent<LootBeamEffect>();
                beamEffect.InitializeBeam();

                // Ensure it renders on top in 2D
                var renderer = beamInstance.GetComponent<Renderer>();
                if (renderer != null)
                {
                    try { renderer.sortingOrder = 5000; } catch { }
                }
                var spriteRenderer = beamInstance.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null) spriteRenderer.sortingOrder = 5000;
                
                _activeBeams[itemId] = beamInstance;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LootBeamManager] Failed to create beam for {itemName}: {e.Message}");
            }
        }

        private string GetReadableItemName(GameObject go)
        {
            // Prefer a GlobalName component if available
            Component globalName = null;
            var allComponents = go.GetComponents<Component>();
            foreach (var c in allComponents)
            {
                if (c != null && c.GetType().Name == "GlobalName")
                {
                    globalName = c;
                    break;
                }
            }
            if (globalName != null)
            {
                var type = globalName.GetType();
                // Try any non-empty string properties first
                foreach (var prop in type.GetProperties())
                {
                    if (prop.PropertyType == typeof(string) && prop.CanRead)
                    {
                        try
                        {
                            var value = prop.GetValue(globalName) as string;
                            if (!string.IsNullOrEmpty(value))
                            {
                                return value;
                            }
                        }
                        catch {}
                    }
                }
                // Then try fields
                foreach (var field in type.GetFields())
                {
                    if (field.FieldType == typeof(string))
                    {
                        try
                        {
                            var value = field.GetValue(globalName) as string;
                            if (!string.IsNullOrEmpty(value))
                            {
                                return value;
                            }
                        }
                        catch {}
                    }
                }
            }
            // Try ItemID component for any readable string
            foreach (var c in allComponents)
            {
                if (c == null) continue;
                if (c.GetType().Name == "ItemID")
                {
                    var type = c.GetType();
                    foreach (var prop in type.GetProperties())
                    {
                        if (prop.PropertyType == typeof(string) && prop.CanRead)
                        {
                            try
                            {
                                var value = prop.GetValue(c) as string;
                                if (!string.IsNullOrEmpty(value))
                                {
                                    return value;
                                }
                            }
                            catch {}
                        }
                    }
                    foreach (var field in type.GetFields())
                    {
                        if (field.FieldType == typeof(string))
                        {
                            try
                            {
                                var value = field.GetValue(c) as string;
                                if (!string.IsNullOrEmpty(value))
                                {
                                    return value;
                                }
                            }
                            catch {}
                        }
                    }
                }
            }
            // Try common visual component names
            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null) { var sname = sr.sprite.name; if (!string.IsNullOrEmpty(sname)) return sname; }
            var mf = go.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) { var mname = mf.sharedMesh.name; if (!string.IsNullOrEmpty(mname)) return mname; }
            var mr = go.GetComponentInChildren<MeshRenderer>();
            if (mr != null)
            {
                if (mr.sharedMaterial != null)
                {
                    var matName = mr.sharedMaterial.name;
                    if (!string.IsNullOrEmpty(matName)) return matName;
                }
                var mats = mr.sharedMaterials;
                if (mats != null)
                {
                    foreach (var mat in mats)
                    {
                        if (mat == null) continue;
                        var n = mat.name;
                        if (!string.IsNullOrEmpty(n)) return n;
                    }
                }
            }

            // Fallback to GameObject name cleanup
            var name = go.name;
            if (string.IsNullOrEmpty(name)) return "";
            // Strip common suffixes
            name = name.Replace("(Clone)", string.Empty);
            return name;
        }

        private int GetItemNumericId(GameObject go)
        {
            var comps = go.GetComponents<Component>();
            foreach (var c in comps)
            {
                if (c == null) continue;
                var t = c.GetType();
                if (t.Name != "ItemID") continue;

                // Try common property names first
                foreach (var prop in t.GetProperties())
                {
                    if (prop.PropertyType == typeof(int) && prop.CanRead)
                    {
                        try
                        {
                            var val = (int)prop.GetValue(c);
                            if (val > 0) return val;
                        }
                        catch { }
                    }
                }
                // Try common field names
                foreach (var field in t.GetFields())
                {
                    if (field.FieldType == typeof(int))
                    {
                        try
                        {
                            var val = (int)field.GetValue(c);
                            if (val > 0) return val;
                        }
                        catch { }
                    }
                }
            }
            return -1;
        }

        private void OnDestroy()
        {
            foreach (var beam in _activeBeams.Values)
            {
                if (beam != null) Destroy(beam);
            }
            _activeBeams.Clear();
        }
    }

    public enum LootRarity
    {
        Uncommon,
        Rare,
        VeryRare,
        SuperRare,
        ExtremelyRare
    }
} 