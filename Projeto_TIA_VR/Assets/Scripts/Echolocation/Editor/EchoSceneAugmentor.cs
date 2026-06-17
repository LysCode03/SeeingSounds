using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

/// <summary>
/// Tools for dropping the echolocation system into an EXISTING scene (e.g. BasicScene)
/// without hand-editing the scene file.
///
///   Tools > Echolocation > Add System To Open Scene
///       Adds an EchoRevealManager (if missing) and a movement-driven footstep
///       emitter parented to the main/VR camera.
///
///   Tools > Echolocation > Convert Selected To Echo Reveal
///       Swaps the materials on the selected objects (and their children) to the
///       EchoReveal shader, preserving each material's albedo map/colour. This is
///       what makes the real room geometry react to sound. Undoable.
///
///   Tools > Echolocation > Set Active Camera Background Black
///       Convenience: makes the camera clear to solid black for the dark-room look.
/// </summary>
public static class EchoSceneAugmentor
{
    private const string MaterialFolder = "Assets/EchoLocation";
    private const string ConvertedFolder = MaterialFolder + "/Converted";

    [MenuItem("Tools/Echolocation/Add System To Open Scene")]
    public static void AddSystemToOpenScene()
    {
        // Manager
        var manager = Object.FindObjectOfType<EchoRevealManager>();
        if (manager == null)
        {
            var go = new GameObject("EchoRevealManager");
            Undo.RegisterCreatedObjectUndo(go, "Add EchoRevealManager");
            manager = go.AddComponent<EchoRevealManager>();
            Debug.Log("[Echo] Added EchoRevealManager to the scene.");
        }
        else
        {
            Debug.Log("[Echo] EchoRevealManager already present.");
        }

        // Camera (VR rig camera is tagged MainCamera)
        Camera cam = Camera.main;
        if (cam == null) cam = Object.FindObjectOfType<Camera>();
        if (cam == null)
        {
            EditorUtility.DisplayDialog("Echolocation",
                "Added the manager, but found no Camera in the scene to attach footsteps to. " +
                "Add the footstep emitter to your player/head manually.",
                "OK");
            MarkDirty();
            return;
        }

        // Footsteps: small, tight wave around the player, automatic every 1s.
        // (Idempotent: finds an existing "Footsteps" child and re-applies the settings.)
        var steps = GetOrAddEmitter(cam.transform, "Footsteps");
        steps.mode = EchoSoundEmitter.Mode.OneShot;
        steps.autoRepeat = true;
        steps.onlyWhenMoving = false; // always on the timer, unaffected by small movements
        steps.frequency = 1f;         // every 1 second
        steps.speed = 6f;
        steps.maxRadius = 3f;         // small, hugging the player
        steps.fade = 2f;
        steps.intensity = 1f;

        // Big wave on E: a deliberate, long-range ping.
        var big = GetOrAddEmitter(cam.transform, "BigWave (E)");
        big.mode = EchoSoundEmitter.Mode.OneShot;
        big.autoRepeat = false;
        big.triggerOnKey = true;      // default key is E
        big.speed = 12f;
        big.maxRadius = 20f;          // long range
        big.fade = 4f;
        big.intensity = 1.5f;

        Debug.Log("[Echo] Configured Footsteps (small, 1 Hz, automatic) and BigWave (E) under '" + cam.name + "'.");

        MarkDirty();
        EditorUtility.DisplayDialog("Echolocation",
            "System added to the open scene.\n\n" +
            "Player now has: small automatic footsteps (every 1s) + a large wave on the E key.\n\n" +
            "Next: select the room geometry you want to be revealable and run\n" +
            "Tools > Echolocation > Convert Selected To Echo Reveal.\n\n" +
            "(Don't convert the player hands, controllers or UI.)",
            "OK");
    }

    private static GameObject NewChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Add " + name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        return go;
    }

    /// <summary>Find a named child emitter under the parent, or create one. Settings are recorded for Undo.</summary>
    private static EchoSoundEmitter GetOrAddEmitter(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        GameObject go = t != null ? t.gameObject : NewChild(parent, name);
        var emit = go.GetComponent<EchoSoundEmitter>();
        if (emit == null) emit = go.AddComponent<EchoSoundEmitter>();
        Undo.RecordObject(emit, "Configure " + name);
        return emit;
    }

    [MenuItem("Tools/Echolocation/Convert Selected To Echo Reveal")]
    public static void ConvertSelectedToEchoReveal()
    {
        Shader shader = Shader.Find("Custom/EchoReveal");
        if (shader == null)
        {
            EditorUtility.DisplayDialog("Echolocation", "Shader 'Custom/EchoReveal' not found.", "OK");
            return;
        }

        GameObject[] selection = Selection.gameObjects;
        if (selection == null || selection.Length == 0)
        {
            EditorUtility.DisplayDialog("Echolocation",
                "Select the environment objects (walls, floor, furniture) you want to be revealable first.",
                "OK");
            return;
        }

        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets", "EchoLocation");
        if (!AssetDatabase.IsValidFolder(ConvertedFolder))
            AssetDatabase.CreateFolder(MaterialFolder, "Converted");

        // One EchoReveal material per source material, reused across renderers.
        var cache = new Dictionary<Material, Material>();
        int rendererCount = 0;

        var renderers = new List<MeshRenderer>();
        foreach (var go in selection)
            renderers.AddRange(go.GetComponentsInChildren<MeshRenderer>(true));

        foreach (var mr in renderers)
        {
            Material[] src = mr.sharedMaterials;
            var dst = new Material[src.Length];
            for (int i = 0; i < src.Length; i++)
                dst[i] = src[i] == null ? null : GetOrCreateEchoMaterial(src[i], shader, cache);

            Undo.RecordObject(mr, "Convert To Echo Reveal");
            mr.sharedMaterials = dst;
            rendererCount++;
        }

        AssetDatabase.SaveAssets();
        MarkDirty();
        EditorUtility.DisplayDialog("Echolocation",
            "Converted " + rendererCount + " renderer(s) using " + cache.Count + " new EchoReveal material(s).\n" +
            "Materials saved in " + ConvertedFolder + ".\n\n" +
            "Use Edit > Undo to revert the renderer changes if needed.",
            "OK");
    }

    [MenuItem("Tools/Echolocation/Set Active Camera Background Black")]
    public static void SetCameraBlack()
    {
        Camera cam = Camera.main;
        if (cam == null) cam = Object.FindObjectOfType<Camera>();
        if (cam == null) { EditorUtility.DisplayDialog("Echolocation", "No camera found.", "OK"); return; }

        Undo.RecordObject(cam, "Set Camera Black");
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        MarkDirty();
        Debug.Log("[Echo] Set '" + cam.name + "' to clear to solid black.");
    }

    private static Material GetOrCreateEchoMaterial(Material source, Shader shader, Dictionary<Material, Material> cache)
    {
        if (cache.TryGetValue(source, out Material existing))
            return existing;

        var mat = new Material(shader) { name = "Echo_" + source.name };

        // Preserve albedo so revealed surfaces show their real look (URP Lit or legacy names).
        Texture baseMap = null;
        if (source.HasProperty("_BaseMap")) baseMap = source.GetTexture("_BaseMap");
        else if (source.HasProperty("_MainTex")) baseMap = source.GetTexture("_MainTex");
        if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);

        Color baseColor = Color.white;
        if (source.HasProperty("_BaseColor")) baseColor = source.GetColor("_BaseColor");
        else if (source.HasProperty("_Color")) baseColor = source.GetColor("_Color");
        baseColor.a = 1f;
        mat.SetColor("_BaseColor", baseColor);

        string path = AssetDatabase.GenerateUniqueAssetPath(ConvertedFolder + "/Echo_" + source.name + ".mat");
        AssetDatabase.CreateAsset(mat, path);

        cache[source] = mat;
        return mat;
    }

    private static void MarkDirty()
    {
        Scene s = SceneManager.GetActiveScene();
        if (s.IsValid()) EditorSceneManager.MarkSceneDirty(s);
    }
}
