using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// One-click builders for echolocation test scenes. Creates a dark room out of
/// primitives, applies the EchoReveal material to everything, drops in a manager,
/// a constant "radio" sound and a movement-driven "footsteps" emitter.
///
///   Tools > Echolocation > Build Test Scene (VR)   -> real XR rig with thumbstick locomotion
///   Tools > Echolocation > Build Test Scene (Flat) -> desktop fly camera, no headset needed
/// </summary>
public static class EchoTestSceneBuilder
{
    private const string MaterialFolder = "Assets/EchoLocation";
    private const string MaterialPath   = MaterialFolder + "/EchoRevealMaterial.mat";
    private const string ScenesFolder   = "Assets/Scenes";
    private const string VrScenePath    = ScenesFolder + "/EchoTest_VR.unity";
    private const string FlatScenePath  = ScenesFolder + "/EchoTest_Flat.unity";

    // Ready-made XR rig (continuous move + turn already wired) shipped with the XRI Starter Assets.
    private const string XrRigPrefabPath =
        "Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

    [MenuItem("Tools/Echolocation/Build Test Scene (VR)")]
    public static void BuildVR() => BuildScene(true);

    [MenuItem("Tools/Echolocation/Build Test Scene (Flat)")]
    public static void BuildFlat() => BuildScene(false);

    private static void BuildScene(bool vr)
    {
        Shader shader = Shader.Find("Custom/EchoReveal");
        if (shader == null)
        {
            EditorUtility.DisplayDialog("Echolocation",
                "Shader 'Custom/EchoReveal' not found.\n\nMake sure EchoReveal.shader imported with no errors, then try again.",
                "OK");
            return;
        }

        string scenePath = vr ? VrScenePath : FlatScenePath;
        if (!EditorUtility.DisplayDialog("Echolocation",
                "This will create a NEW scene at " + scenePath + " and switch to it.\nSave any unsaved changes first.\n\nContinue?",
                "Build it", "Cancel"))
            return;

        Material mat = EnsureMaterial(shader);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- Dark environment ---
        RenderSettings.ambientMode  = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.skybox       = null;
        RenderSettings.fog          = false;

        // --- Manager ---
        new GameObject("EchoRevealManager").AddComponent<EchoRevealManager>();

        BuildRoom(mat);

        // --- Player / camera ---
        GameObject camGo = vr ? SetupVrRig() : SetupFlatCamera();
        if (camGo == null) return; // VR rig missing; SetupVrRig already warned

        camGo.AddComponent<EchoClickToPing>(); // handy in the editor / XR Device Simulator

        // --- Footsteps: pulse every 1.5s, ONLY while the player is moving, from the camera ---
        var steps = new GameObject("Footsteps");
        steps.transform.SetParent(camGo.transform);
        steps.transform.localPosition = Vector3.zero;
        var stepsEmit = steps.AddComponent<EchoSoundEmitter>();
        stepsEmit.mode = EchoSoundEmitter.Mode.OneShot;
        stepsEmit.autoRepeat = true;
        stepsEmit.onlyWhenMoving = true;
        stepsEmit.repeatInterval = 1.5f;
        stepsEmit.speed = 7f;
        stepsEmit.maxRadius = 9f;
        stepsEmit.fade = 3f;
        stepsEmit.intensity = 1f;

        // --- Constant sound (a radio humming in the corner) ---
        var radio = new GameObject("ConstantSound_Radio");
        radio.transform.position = new Vector3(-3.5f, 1f, 3.5f);
        var radioEmit = radio.AddComponent<EchoSoundEmitter>();
        radioEmit.mode = EchoSoundEmitter.Mode.Constant;
        radioEmit.maxRadius = 4f;
        radioEmit.intensity = 0.9f;

        // --- Save ---
        if (!AssetDatabase.IsValidFolder(ScenesFolder))
            AssetDatabase.CreateFolder("Assets", "Scenes");
        EditorSceneManager.SaveScene(scene, scenePath);

        string controls = vr
            ? "Put on the headset and press Play.\nLeft thumbstick = move, right thumbstick = turn.\nFootsteps pulse from you only while you move."
            : "Press Play.\nRight-drag = look, WASD = move, Q/E = down/up, Shift = faster.\nLeft-click = ping a spot, Space = ping ahead.\nFootsteps pulse only while you move.";

        EditorUtility.DisplayDialog("Echolocation",
            "Test scene created at " + scenePath + "\n\n" + controls +
            "\n\nThe radio glows steadily in the corner.",
            "Nice");
    }

    private static GameObject SetupFlatCamera()
    {
        var camGo = new GameObject("TestCamera");
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGo.transform.position = new Vector3(0f, 1.6f, -4.5f);
        camGo.tag = "MainCamera";
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<EchoFreeLook>();
        return camGo;
    }

    private static GameObject SetupVrRig()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrRigPrefabPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Echolocation",
                "Couldn't find the XR rig prefab at:\n" + XrRigPrefabPath +
                "\n\nImport the XR Interaction Toolkit 'Starter Assets' sample, or use 'Build Test Scene (Flat)' instead.",
                "OK");
            return null;
        }

        var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        rig.transform.position = new Vector3(0f, 0f, -4f);

        Camera cam = rig.GetComponentInChildren<Camera>();
        if (cam == null)
        {
            EditorUtility.DisplayDialog("Echolocation",
                "Instantiated the XR rig but found no Camera inside it. Use 'Build Test Scene (Flat)' instead.",
                "OK");
            return null;
        }

        // Keep the dark-room look: the rig camera should clear to black.
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        return cam.gameObject;
    }

    private static void BuildRoom(Material mat)
    {
        var room = new GameObject("Room").transform;
        const float w = 10f, d = 10f, h = 3f;
        CreateBox("Floor",   new Vector3(0, 0, 0),          new Vector3(w, 0.1f, d), mat, room);
        CreateBox("Ceiling", new Vector3(0, h, 0),          new Vector3(w, 0.1f, d), mat, room);
        CreateBox("Wall_N",  new Vector3(0, h / 2, d / 2),  new Vector3(w, h, 0.1f), mat, room);
        CreateBox("Wall_S",  new Vector3(0, h / 2, -d / 2), new Vector3(w, h, 0.1f), mat, room);
        CreateBox("Wall_E",  new Vector3(w / 2, h / 2, 0),  new Vector3(0.1f, h, d), mat, room);
        CreateBox("Wall_W",  new Vector3(-w / 2, h / 2, 0), new Vector3(0.1f, h, d), mat, room);

        // Obstacles so reveals have something to wrap around.
        CreateBox("Crate1", new Vector3(-2f, 0.5f, 1f),     new Vector3(1f, 1f, 1f),       mat, room);
        CreateBox("Crate2", new Vector3(2.5f, 0.75f, -2f),  new Vector3(1.5f, 1.5f, 1.5f), mat, room);
        CreatePrimitive("Pillar", PrimitiveType.Cylinder, new Vector3(1f, 1f, 3f), new Vector3(0.6f, 1f, 0.6f), mat, room);
    }

    private static Material EnsureMaterial(Shader shader)
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets", "EchoLocation");

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(shader) { name = "EchoRevealMaterial" };
            AssetDatabase.CreateAsset(mat, MaterialPath);
            AssetDatabase.SaveAssets();
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }
        return mat;
    }

    private static GameObject CreateBox(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
        => CreatePrimitive(name, PrimitiveType.Cube, pos, scale, mat, parent);

    private static GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.localScale = scale;

        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off; // unlit, no real-time lights
        mr.receiveShadows = false;
        return go;
    }
}
