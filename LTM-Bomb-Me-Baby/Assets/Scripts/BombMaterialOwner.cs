using System.Collections.Generic;
using UnityEngine;

/// Releases only explicitly tagged, per-object materials. Imported assets and shared caches
/// are never tagged. Destroying a renderer alone does not release its runtime materials.
public sealed class BombMaterialOwner : MonoBehaviour {
    public const string Prefix = "Bomb runtime: ";
    readonly HashSet<Material> owned = new HashSet<Material>();

    public static void Capture(GameObject root) {
        var owner = root.GetComponent<BombMaterialOwner>();
        if (owner == null) owner = root.AddComponent<BombMaterialOwner>();
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
                if (material != null && material.name.StartsWith(Prefix)) owner.owned.Add(material);
    }

    void OnDestroy() {
        foreach (Material material in owned) if (material != null) Destroy(material);
        owned.Clear();
    }
}
