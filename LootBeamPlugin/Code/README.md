# LootBeam Mod for PowVista

This mod adds visual beam effects to rare loot items in PowVista, making them easier to spot on the ground.

## Features

- **Visual Beam Effects**: Different colored beams for different item rarities
- **Audio Cue**: Plays a sound effect when a rare item's beam appears.
- **Dynamic Rarity Detection**: Automatically detects item rarity based on name patterns
- **Persistence**: Beams remain visible as long as items are on the ground
- **Cleanup**: Beams disappear when items are picked up or despawned
- **AssetBundle Support**: Custom beam prefabs loaded from platform-specific bundles

## Rarity Colors

- **Uncommon** (Green): Mob Skateboards
- **Rare** (Orange): Mob Eggs  
- **Very Rare** (Purple): Mob Hoodies
- **Super Rare** (Rainbow): Shiny Mob Skateboards
- **Extremely Rare** (Enhanced Rainbow): Shiny Mob Hoodies

## Building the Mod

### Prerequisites

1. **Unity 2022.3.62f1** or compatible version
2. **Visual Studio** or **Rider** for building the DLL
3. **PowVista game files** for extracting dependencies

### Step 1: Build AssetBundles

1. Open the Unity project (`PowVistaSkate-plugin-dev`)
2. Ensure all beam prefabs exist in `Assets/Prefabs/Particles/LootBeam/`
3. Go to **Tools > Build LootBeam AssetBundles**
4. This creates platform-specific bundles in `Assets/Editor/Temp/`

### Step 2: Build the DLL

1. Open the DLL project in **Rider** or **Visual Studio**
2. Build the project to create `LootBeam.dll`
3. Optionally build with debug symbols for `LootBeam.pdb`

### Step 3: Install the Mod

1. Create the mod folder structure (macOS Downloads path):
   ```
   ~/Downloads/PowVistaSkateCustomPlugins/
   └── LootBeam/
       ├── LootBeam.dll
       └── AssetBundles/
           ├── Windows/
           │   ├── uncommonbeam
           │   ├── rarebeam
           │   ├── veryrarebeam
           │   ├── superrarebeam
           │   └── extremelyrarebeam
           │   └── sparkle
           └── macOS/
               ├── uncommonbeam
               ├── rarebeam
               ├── veryrarebeam
               ├── superrarebeam
               └── extremelyrarebeam
               └── sparkle
   ```

2. Copy the built DLL to `~/Downloads/PowVistaSkateCustomPlugins/LootBeam/LootBeam.dll`
3. Copy the AssetBundles from the build output folder (`AssetBundles/Windows` and `AssetBundles/macOS` at the project root) to the appropriate platform folders above.

## Dependencies

The mod requires these DLL files in the `Dependancies` folder:
- `Mirror.dll` - Networking framework
- `Scripts.dll` - PowVista game scripts (contains IMod interface)
- `Unity.TextMeshPro.dll` - Text rendering
- `UnityEngine.dll` - Main Unity engine (contains AssetBundle methods)
- `UnityEngine.AssetBundleModule.dll` - AssetBundle module
- `UnityEngine.CoreModule.dll` - Core Unity functionality
- `UnityEngine.InputLegacyModule.dll` - Input handling
- `UnityEngine.UI.dll` - UI system
- `UnityEngine.UIModule.dll` - UI module

## Troubleshooting

### Mod Not Loading
- Ensure the DLL is in the correct `Mods/LootBeam/` folder
- Check that all dependencies are available
- Verify the mod implements `IMod` interface correctly

### AssetBundles Not Loading
- Ensure AssetBundles are built for the correct platform
- Check the folder structure matches the expected layout
- Verify AssetBundles are copied to the correct platform subfolder
- Check console logs for any error messages

### Beams Not Appearing
- Check that beam prefabs are properly built into AssetBundles
- Verify the mod is loading and attaching to GameManager
- Check console logs for any error messages

## Development

### Adding New Rarities
1. Create new beam prefab in Unity
2. Add to `AssetbundleNames` array in `LootBeamAssetBundleBuilder.cs`
3. Update `LoadAssetBundles()` method in `LootBeamMod.cs`
4. Add rarity detection logic in `LootBeamPlugin.cs`

### Modifying Beam Effects
- Edit the beam prefabs in Unity
- Rebuild AssetBundles
- Update the mod DLL if needed

## License

This mod is provided as-is for PowVista players. 