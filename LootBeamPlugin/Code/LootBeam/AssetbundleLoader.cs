using System;
using System.IO;
using UnityEngine;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace LootBeam
{
    public class AssetbundleLoader
    {
        public static AudioClip? LootBeamSound { get; private set; }
        private static string[] AssetbundleNames = { "uncommonbeam", "rarebeam", "veryrarebeam", "superrarebeam", "extremelyrarebeam", "sparkle" };
        
        private static readonly Dictionary<string, GameObject> Assetbundles = new(System.StringComparer.OrdinalIgnoreCase);
        
        public static void LoadAssetbundles(Action onComplete)
        {
            // Start loading without noisy logs
            int attempted = 0;
            int processed = 0;

            void Done()
            {
                if (processed >= attempted)
                {
                    Debug.Log($"[AssetbundleLoader] Finished loading AssetBundles. Loaded {Assetbundles.Count} bundles.");
                    onComplete();
                }
            }

            foreach (var name in AssetbundleNames)
            {
                if (!Assetbundles.TryAdd(name, null!)) continue;

                var platformName = Application.platform switch
                {
                    RuntimePlatform.OSXPlayer => "macOS",
                    RuntimePlatform.WindowsPlayer => "Windows",
                    RuntimePlatform.OSXEditor => "macOS",
                    RuntimePlatform.WindowsEditor => "Windows",
                    _ => "Windows"
                };
                // Candidate search paths
                string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string nameLower = name.ToLower();
                // Resolve current plugin DLL location to infer the player's chosen mods folder
                string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string? modsRootFromDll = null;
                try { modsRootFromDll = Path.GetDirectoryName(assemblyLocation); } catch { modsRootFromDll = null; }

                var candidates = new List<string>
                {
                    // 1) Next to the running DLL (supports arbitrary user-selected Mods folder)
                    modsRootFromDll == null ? null : Path.Combine(modsRootFromDll, "LootBeam", "AssetBundles", platformName, nameLower),

                    // User Downloads (two possible root folder names)
                    Path.Combine(userHome, "Downloads", "PowVistaSkateCustomPlugins", "LootBeam", "AssetBundles", platformName, nameLower),
                    Path.Combine(userHome, "Downloads", "PowVistaCustomPlugins", "LootBeam", "AssetBundles", platformName, nameLower),

                    // Relative to game folder (two possible root folder names and a generic Mods fallback)
                    Path.Combine(Directory.GetParent(Application.dataPath).FullName, "PowVistaSkateCustomPlugins", "LootBeam", "AssetBundles", platformName, nameLower),
                    Path.Combine(Directory.GetParent(Application.dataPath).FullName, "PowVistaCustomPlugins", "LootBeam", "AssetBundles", platformName, nameLower),
                    Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Mods", "LootBeam", "AssetBundles", platformName, nameLower)
                };

                string path = null;
                foreach (var candidate in candidates)
                {
                    if (string.IsNullOrEmpty(candidate)) continue;
                    // Silent probe; leave only errors
                    if (File.Exists(candidate)) { path = candidate; break; }
                }

                if (string.IsNullOrEmpty(path))
                {
                    var checkedList = string.Join(" | ", candidates.FindAll(c => !string.IsNullOrEmpty(c)));
                    Debug.LogError($"[AssetbundleLoader] AssetBundle '{nameLower}' not found. Checked: {checkedList}");
                    // count as processed even if missing
                    processed++;
                    Done();
                    continue;
                }

                // Proceed to load from resolved path
                
                if (!File.Exists(path))
                {
                    Debug.LogError($"[AssetbundleLoader] AssetBundle file not found: {path}");
                    Debug.LogError($"[AssetbundleLoader] Please ensure AssetBundles are built and placed in the correct location!");
                    processed++;
                    Done();
                    continue;
                }

                attempted++;
                try
                {
                    var bundleRequest = AssetBundle.LoadFromFileAsync(path);
                    bundleRequest.completed += _ =>
                    {
                        try
                        {
                            var bundle = bundleRequest.assetBundle;
                            if (bundle != null)
                            {
                                if (name == "sparkle")
                                {
                                    // Older profiles may not have LoadAllAssets<T>. Enumerate names and try load per name.
                                    AudioClip loadedClip = null;
                                    var assetNames = bundle.GetAllAssetNames();
                                    for (int i = 0; i < assetNames.Length; i++)
                                    {
                                        try
                                        {
                                            var clip = bundle.LoadAsset<AudioClip>(assetNames[i]);
                                            if (clip != null)
                                            {
                                                loadedClip = clip;
                                                break;
                                            }
                                        }
                                        catch { }
                                    }
                                    if (loadedClip != null)
                                    {
                                        LootBeamSound = loadedClip;
                                        Debug.Log($"[AssetbundleLoader] Successfully loaded sound: {LootBeamSound.name}");
                                    }
                                    else
                                    {
                                        Debug.LogError($"[AssetbundleLoader] Failed to load any AudioClip from bundle: {name}");
                                    }
                                }
                                else
                                {
                                    GameObject rootObject = null;
                                    try { rootObject = bundle.LoadAsset<GameObject>(nameLower); } catch { }
                                    if (rootObject == null)
                                    {
                                        var assetNames = bundle.GetAllAssetNames();
                                        // Keep compact logs
                                        if (assetNames.Length > 0)
                                        {
                                            rootObject = bundle.LoadAsset<GameObject>(assetNames[0]);
                                        }
                                    }
                                    if (rootObject != null)
                                    {
                                        Assetbundles[name] = rootObject;
                                        // Loaded
                                    }
                                    else
                                    {
                                        Debug.LogError($"[AssetbundleLoader] Failed to load GameObject from AssetBundle {name}");
                                    }
                                }
                                bundle.Unload(false);
                            }
                            else
                            {
                                Debug.LogError($"[AssetbundleLoader] Failed to load AssetBundle: {path}");
                            }
                        }
                        catch (Exception e)
                        {
                    Debug.LogError($"[AssetbundleLoader] Exception completing AssetBundle {name}: {e.Message}");
                        }
                        finally
                        {
                            processed++;
                Done();
                        }
                    };
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AssetbundleLoader] Exception scheduling AssetBundle {name}: {e.Message}");
                    processed++;
                    Done();
                }
            }
            if (attempted == 0)
            {
                Done();
            }
        }
        
        public static GameObject GetAssetbundle(string name) 
        {
            if (Assetbundles.TryGetValue(name, out GameObject asset))
            {
                return asset;
            }
            
            Debug.LogError($"[AssetbundleLoader] AssetBundle '{name}' not found in loaded bundles!");
            Debug.LogError($"[AssetbundleLoader] Available bundles: {string.Join(", ", Assetbundles.Keys)}");
            return null!;
        }
        
        public static Dictionary<string, GameObject> GetAllAssetbundles() 
        {
            Debug.Log($"[AssetbundleLoader] Returning {Assetbundles.Count} loaded AssetBundles");
            return new Dictionary<string, GameObject>(Assetbundles);
        }
        
        public static bool HasAssetbundle(string name)
        {
            return Assetbundles.ContainsKey(name);
        }
    }
} 