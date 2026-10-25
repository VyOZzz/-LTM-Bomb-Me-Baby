using UnityEngine;

/// Builds a real map layout under the Environment root with the same BombMapView code the match
/// uses, so the Scene view and the menu background look like the game. The preview is never saved
/// into the scene; BombGame hides this root once the server sends the map of the actual round.
[ExecuteAlways]
public sealed class BombMapPreview : MonoBehaviour {
    public enum Map { Meadow, Crossroads, Crater }

    const string PreviewPrefix = "Map Preview - ";

    [SerializeField] Map map = Map.Meadow;
    readonly BombMapView view = new BombMapView();

    void OnEnable() => Rebuild();
    void OnDisable() => view.Clear();

#if UNITY_EDITOR
    void OnValidate() {
        // Objects cannot be created inside OnValidate.
        UnityEditor.EditorApplication.delayCall += () => {
            if (this != null && isActiveAndEnabled) Rebuild();
        };
    }
#endif

    void Rebuild() {
        // A domain reload drops the view's reference but not the objects it built.
        for (int i = transform.childCount - 1; i >= 0; i--) {
            GameObject child = transform.GetChild(i).gameObject;
            if (child.name.StartsWith(PreviewPrefix)) BombArt.Discard(child);
        }
        BombMaps.Layout layout = BombMaps.ById(map.ToString().ToLowerInvariant());
        GameObject built = view.Build(layout.id, layout.halfSize, layout.obstacles, layout.bushes, PreviewPrefix, transform);
        if (Application.isPlaying) return;
        foreach (Transform part in built.GetComponentsInChildren<Transform>(true))
            part.gameObject.hideFlags = HideFlags.DontSave;
    }
}
