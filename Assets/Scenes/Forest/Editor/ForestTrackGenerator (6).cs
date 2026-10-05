// Forest Track Generator v5 (editor-only)
// Put this file in Assets/Editor/, then open Tools > Forest Track Generator and click Generate Scene.
//
// Builds a 1.3 km x 1.3 km dense forest scene:
//  - short start road into a fork plaza with three signed trails, all ending at one shared finish plaza
//  - EASY: gentle dirt trail past two lakes, flowers, lookouts with benches and docks, lakeside fences
//  - HARD: wide rocky trail (room for AI racers) with switchbacks over two forested mountains;
//          it follows the ground down each descent and climbs back up instead of riding on a raised ridge
//  - SPRINT: wide gravel trail with long straights
//  - rocks and fallen logs along every trail's edges, always kept off the riding surface

using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class ForestTrackGenerator : EditorWindow
{
    [SerializeField] int seed = 1234;
    [SerializeField] int treeCount = 9000;
    [SerializeField] GameObject[] treePrefabs = new GameObject[0];
    [SerializeField] GameObject[] rockPrefabs = new GameObject[0];
    [SerializeField] GameObject waterPrefab;
    [SerializeField] string scenePath = "Assets/Scenes/Forest/Forest.unity";
    [SerializeField] float wallHeight = 3f;
    [SerializeField] float wallGap = 0.5f;

    const string GenFolder = "Assets/Scenes/Forest/Generated";
    const float S = 0.65f;          // layout scale: trail coordinates below are multiplied by this
    const float AmpScale = 0.6f;    // mountain height scale
    const int Res = 1025;
    const float SizeXZ = 2000f * S;
    const float SizeY = 220f;
    static readonly float Cell = SizeXZ / (Res - 1);
    const float RimWidth = 100f;
    const float TreeLine = 150f;
    const float BareRockHeight = 150f;
    const float HardFillSpread = 2.2f; // how far the hard trail's embankments blend into the ground

    static readonly Vector2 FinishCenter = new Vector2(1000f, 1780f) * S;
    const float FinishRadius = 22f;
    const int StartId = 0, EasyId = 1, HardId = 2, SprintId = 3;

    struct Mountain
    {
        public Vector2 c; public float sigma, amp;
        public Mountain(float x, float z, float s, float a) { c = new Vector2(x, z) * S; sigma = s * S; amp = a * AmpScale; }
    }

    class Lake
    {
        public Vector2 c; public float r, level;
        public Lake(float x, float z, float radius) { c = new Vector2(x, z) * S; r = radius * S; }
    }

    class TrackPath
    {
        public string name;
        public Vector2[] ctrl;
        public float halfWidth, falloff, maxUp, maxDown, roughness, treeClear;
        public bool climbOnly; // never goes downhill between the fork and the finish
        public int smoothRadius, layer;
        public Vector3[] pts;
    }

    class Lookout { public Vector3 center, dir; public Lake lake; public int index; }

    static readonly Mountain[] Mountains =
    {
        new Mountain(1010, 760, 110, 95),   // hard trail, first climb
        new Mountain(1000, 1370, 110, 105), // hard trail, second climb
        new Mountain(1000, 450, 70, 35),    // hills before the first climb
        new Mountain(700, 1150, 110, 110),
        new Mountain(1450, 1100, 110, 120),
        new Mountain(1400, 470, 90, 80),
        new Mountain(180, 620, 110, 120),
        new Mountain(300, 1600, 130, 160),
        new Mountain(1780, 1820, 120, 150),
        new Mountain(1300, 1450, 100, 90)
    };

    System.Random rng;
    float ox, oz;
    float[,] baseH, edgeBuf, targetBuf, falloffBuf;
    int[,] idBuf;
    Lake[] lakes;
    TrackPath startPath;
    TrackPath[] branches;
    TrackPath[] allPaths;
    List<Lookout> lookouts;
    float forkH, finishH;

    Terrain terrain;
    TerrainData td;
    List<GameObject> treeList;
    int[] treeProto;
    List<TreeInstance> instances;
    Transform looseTrees;

    [MenuItem("Tools/Forest Track Generator")]
    static void Open() => GetWindow<ForestTrackGenerator>("Forest Track");

    void OnGUI()
    {
        var so = new SerializedObject(this);
        EditorGUILayout.LabelField("Forest Track (3 paths, shared finish)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("seed"));
        EditorGUILayout.PropertyField(so.FindProperty("treeCount"));
        EditorGUILayout.PropertyField(so.FindProperty("treePrefabs"), true);
        EditorGUILayout.PropertyField(so.FindProperty("rockPrefabs"), true);
        EditorGUILayout.PropertyField(so.FindProperty("waterPrefab"));
        EditorGUILayout.PropertyField(so.FindProperty("scenePath"));
        so.ApplyModifiedProperties();
        EditorGUILayout.HelpBox("Leave the prefab lists empty to use built-in low-poly trees and rocks. " +
                                "Flowers, bushes, logs, fences, docks and benches are always generated.", MessageType.Info);
        if (GUILayout.Button("Generate Scene", GUILayout.Height(32))) Generate();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Track Walls", EditorStyles.boldLabel);
        so.Update();
        EditorGUILayout.PropertyField(so.FindProperty("wallHeight"));
        EditorGUILayout.PropertyField(so.FindProperty("wallGap"), new GUIContent("Gap From Trail Edge"));
        so.ApplyModifiedProperties();
        EditorGUILayout.HelpBox("Adds invisible box colliders along both edges of every trail in the open scene. " +
                                "Use the same seed the scene was generated with. Running it again replaces the old walls.", MessageType.None);
        if (GUILayout.Button("Add Track Walls to Open Scene", GUILayout.Height(28))) AddTrackWalls();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Update Existing Scene", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Reshapes the trails in the open scene using the current settings, repaints the ground, " +
                                "puts trees, rocks, logs, signs and fences back on the ground, and rebuilds the track walls. " +
                                "Your own objects (like the bike) are not touched.", MessageType.None);
        if (GUILayout.Button("Update Open Scene", GUILayout.Height(28))) UpdateOpenScene();
    }

    // ================================================================== main

    void Generate()
    {
        if (!EditorUtility.DisplayDialog("Generate Scene",
                $"This builds a brand-new scene at {scenePath} and overwrites the files in {GenFolder}.\n\n" +
                "Any scene already using those files (like your current track) will break.\n\n" +
                "To change a scene you already have, cancel and use 'Update Open Scene' instead.",
                "Generate anyway", "Cancel")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        try
        {
            rng = new System.Random(seed);
            ox = rng.Next(0, 5000);
            oz = rng.Next(0, 5000);
            lakes = MakeLakes();
            // start from an empty folder: replacing meshes/materials in place leaves the tree prefabs pointing at deleted copies
            if (AssetDatabase.IsValidFolder(GenFolder)) AssetDatabase.DeleteAsset(GenFolder);
            EnsureFolder(GenFolder);
            EnsureFolder(Path.GetDirectoryName(scenePath).Replace('\\', '/'));

            Progress("Shaping terrain", 0.05f);
            BuildBaseHeights();
            Progress("Laying out trails", 0.2f);
            BuildPaths();
            BuildLookouts();
            float[,] heights = CarveHeights();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Progress("Creating terrain", 0.3f);
            td = new TerrainData();
            td.heightmapResolution = Res;
            td.alphamapResolution = Res - 1;
            td.baseMapResolution = 1024;
            td.size = new Vector3(SizeXZ, SizeY, SizeXZ);
            td.SetHeights(0, 0, heights);
            AssetDatabase.CreateAsset(td, GenFolder + "/ForestTerrain.asset");

            Progress("Painting ground", 0.4f);
            PaintTerrain();

            var terrainGO = Terrain.CreateTerrainGameObject(td);
            terrainGO.name = "Terrain";
            terrain = terrainGO.GetComponent<Terrain>();
            ConfigureTerrain();

            Progress("Planting forest", 0.55f);
            PlantVegetation();

            Progress("Placing rocks and logs", 0.75f);
            PlaceRocksAndLogs();

            Progress("Adding lakes, signs, lookouts", 0.85f);
            PlaceWater();
            PlaceMarkers();
            PlaceLookoutProps();

            SetupLighting(startPath.pts[3], Quaternion.LookRotation(Tangent(startPath, 3)));

            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Forest track generated: " + scenePath);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    static Lake[] MakeLakes() => new[]
    {
        new Lake(560, 560, 60),   // easy trail lake 1
        new Lake(258, 1062, 55),  // easy trail lake 2
        new Lake(975, 1045, 45),  // valley between the hard trail's mountains
        new Lake(1600, 760, 65),  // beside the sprint trail
        new Lake(840, 440, 40)    // near the fork
    };

    // ================================================================== update existing scene

    // puts every generated object directly on the current ground (no matter how far off it was)
    void SnapObjectsToGround()
    {
        void SetY(Transform c, float y) { var p = c.position; p.y = y; c.position = p; }
        void Each(string group, System.Action<Transform> fn)
        {
            var g = GameObject.Find(group);
            if (!g) return;
            var kids = new List<Transform>();
            foreach (Transform c in g.transform) kids.Add(c);
            if (kids.Count == 0) return;
            Undo.RecordObjects(kids.ToArray(), "Snap To Ground");
            foreach (var c in kids) fn(c);
        }

        Each("Rocks", c => SetY(c, GroundY(c.position) - 0.25f * c.localScale.y));            // slightly sunk in, like when generated
        Each("Fallen Logs", c => SetY(c, GroundY(c.position) + c.localScale.x * 0.5f - 0.1f)); // resting on the ground
        Each("Track Markers", c => SetY(c, GroundY(c.position)));                             // signs, arches, benches, spawn
        Each("Forest (prefab trees)", c => SetY(c, GroundY(c.position)));
        Each("Lookouts", c => { if (c.name == "Bench") SetY(c, GroundY(c.position)); });     // docks stay at the water
        RebuildFences();
    }

    // fences are made of posts and rails between them, so rebuild them on the current ground
    void RebuildFences()
    {
        var old = GameObject.Find("Fences");
        var wood = AssetDatabase.LoadAssetAtPath<Material>(GenFolder + "/DockWood.mat");
        if (old)
        {
            if (!wood)
            {
                var r = old.GetComponentInChildren<MeshRenderer>();
                if (r) wood = r.sharedMaterial;
            }
            Undo.DestroyObjectImmediate(old);
        }
        if (!wood) return;

        var fences = new GameObject("Fences").transform;
        Undo.RegisterCreatedObjectUndo(fences.gameObject, "Rebuild Fences");
        var easy = allPaths[EasyId];
        foreach (var lo in lookouts) BuildFence(easy, lo.index - 35, lo.index + 35, lo.index, lo.dir, fences, wood);
    }

    void UpdateOpenScene()
    {
        var t = Object.FindFirstObjectByType<Terrain>();
        if (t == null)
        {
            EditorUtility.DisplayDialog("Update Open Scene", "Open the generated forest scene first.", "OK");
            return;
        }
        bool hadWalls = GameObject.Find("Track Walls") != null;
        try
        {
            Progress("Rebuilding trail layout", 0.1f);
            rng = new System.Random(seed);
            ox = rng.Next(0, 5000);
            oz = rng.Next(0, 5000);
            lakes = MakeLakes();
            BuildBaseHeights();
            BuildPaths();
            BuildLookouts();
            var heights = CarveHeights();
            terrain = t;
            td = t.terrainData;

            Undo.RegisterCompleteObjectUndo(td, "Update Open Scene");

            Progress("Reshaping terrain", 0.4f);
            td.SetHeights(0, 0, heights);

            Progress("Repainting ground", 0.6f);
            if (td.terrainLayers != null && td.terrainLayers.Length == 4) PaintAlphamaps();

            Progress("Putting objects back on the ground", 0.8f);
            // terrain trees, flowers and bushes store their own height, so snap them to the new ground
            var trees = td.treeInstances;
            for (int i = 0; i < trees.Length; i++)
            {
                var ti = trees[i];
                ti.position.y = td.GetInterpolatedHeight(ti.position.x, ti.position.z) / td.size.y;
                trees[i] = ti;
            }
            td.SetTreeInstances(trees, false);

            SnapObjectsToGround();

            t.Flush();
            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        if (hadWalls) AddTrackWalls();
        Debug.Log("Open scene updated.");
    }

    // ================================================================== track walls

    void AddTrackWalls()
    {
        var t = Object.FindFirstObjectByType<Terrain>();
        if (t == null)
        {
            EditorUtility.DisplayDialog("Track Walls", "Open the generated forest scene first.", "OK");
            return;
        }
        try
        {
            // rebuild the same trail layout the scene was generated with (deterministic from the seed)
            Progress("Rebuilding trail layout", 0.2f);
            rng = new System.Random(seed);
            ox = rng.Next(0, 5000);
            oz = rng.Next(0, 5000);
            lakes = MakeLakes();
            BuildBaseHeights();
            BuildPaths();
            BuildLookouts();
            terrain = t;

            // open areas the walls must not cross: start clearing, fork plaza, finish plaza, lake lookouts
            var clearings = new List<Vector3>(); // x, z = centre, y = radius
            clearings.Add(new Vector3(startPath.pts[0].x, 8f, startPath.pts[0].z));
            var forkPt = startPath.pts[startPath.pts.Length - 1];
            clearings.Add(new Vector3(forkPt.x, 14f, forkPt.z));
            clearings.Add(new Vector3(FinishCenter.x, FinishRadius, FinishCenter.y));
            foreach (var lo in lookouts) clearings.Add(new Vector3(lo.center.x, 6f, lo.center.z));

            var old = GameObject.Find("Track Walls");
            if (old) Undo.DestroyObjectImmediate(old);
            var parent = new GameObject("Track Walls");
            Undo.RegisterCreatedObjectUndo(parent, "Add Track Walls");
            parent.isStatic = true;

            Progress("Placing walls", 0.6f);
            int count = 0;
            const int seg = 4; // metres of trail per collider
            for (int pi = 0; pi < allPaths.Length; pi++)
            {
                var p = allPaths[pi];
                var group = new GameObject(p.name + " Walls").transform;
                group.SetParent(parent.transform);
                group.gameObject.isStatic = true;
                float off = p.halfWidth + wallGap;

                for (int i = 0; i < p.pts.Length - 1; i += seg)
                {
                    int j = Mathf.Min(i + seg, p.pts.Length - 1);
                    Vector3 ra = Vector3.Cross(Vector3.up, Tangent(p, i));
                    Vector3 rb = Vector3.Cross(Vector3.up, Tangent(p, j));
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 a = p.pts[i] + ra * side * off, b = p.pts[j] + rb * side * off;
                        Vector3 mid = (a + b) * 0.5f;

                        // leave openings only where a wall would cross another trail, the fork, the finish plaza or a lookout
                        if (WallBlocked(a, pi, i, clearings) || WallBlocked(b, pi, j, clearings) ||
                            WallBlocked(mid, pi, (i + j) / 2, clearings)) continue;

                        a.y = GroundY(a);
                        b.y = GroundY(b);
                        Vector3 d = b - a;
                        if (d.sqrMagnitude < 0.01f) continue;

                        var w = new GameObject("Wall");
                        w.transform.SetParent(group, false);
                        w.transform.SetPositionAndRotation((a + b) * 0.5f + Vector3.up * wallHeight * 0.5f, Quaternion.LookRotation(d));
                        var bc = w.AddComponent<BoxCollider>();
                        bc.size = new Vector3(0.3f, wallHeight, d.magnitude + 0.4f); // slight overlap so there are no gaps between segments
                        w.isStatic = true;
                        count++;
                    }
                }
            }
            EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
            Debug.Log($"Added {count} track wall colliders.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    float GroundY(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;

    // exact check: is this wall point on a clearing, another trail, or a far-away section of its own trail (switchbacks)?
    bool WallBlocked(Vector3 q, int pathIdx, int idx, List<Vector3> clearings)
    {
        foreach (var c in clearings)
        {
            float dx = q.x - c.x, dz = q.z - c.z;
            float r = c.y + 0.3f;
            if (dx * dx + dz * dz < r * r) return true;
        }
        for (int pi = 0; pi < allPaths.Length; pi++)
        {
            var p = allPaths[pi];
            float lim = p.halfWidth + 0.3f, lim2 = lim * lim;
            for (int k = 0; k < p.pts.Length; k++)
            {
                if (pi == pathIdx && Mathf.Abs(k - idx) < 40) continue; // its own trail right here doesn't count
                float dx = q.x - p.pts[k].x, dz = q.z - p.pts[k].z;
                if (dx * dx + dz * dz < lim2) return true;
            }
        }
        return false;
    }

    static void Progress(string msg, float p) => EditorUtility.DisplayProgressBar("Forest Track", msg, p);
    float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    // ================================================================== terrain shape

    float Fbm(float x, float z, int octaves)
    {
        float sum = 0f, amp = 0.5f, freq = 1f;
        for (int i = 0; i < octaves; i++)
        {
            sum += amp * (Mathf.PerlinNoise(ox + x * freq, oz + z * freq) - 0.5f) * 2f;
            amp *= 0.5f;
            freq *= 2f;
        }
        return sum;
    }

    float BaseRaw(float x, float z)
    {
        float h = 30f + Fbm(x * 0.0035f, z * 0.0035f, 4) * 12f;
        h += Fbm(x * 0.04f + 100f, z * 0.04f + 100f, 2) * 1.5f;

        foreach (var m in Mountains)
        {
            float dx = x - m.c.x, dz = z - m.c.y;
            float k = Mathf.Exp(-(dx * dx + dz * dz) / (2f * m.sigma * m.sigma));
            if (k > 0.001f) h += k * m.amp * (1f + Fbm(x * 0.02f + m.c.x * 0.01f, z * 0.02f + m.c.y * 0.01f, 3) * 0.3f);
        }

        // forested hills around the map edge so you never see the end of the world
        float edge = Mathf.Min(Mathf.Min(x, z), Mathf.Min(SizeXZ - x, SizeXZ - z));
        if (edge < RimWidth)
        {
            float t = 1f - edge / RimWidth;
            h += t * t * 70f * (0.7f + 0.6f * Mathf.PerlinNoise(ox + x * 0.008f, oz + z * 0.008f));
        }
        return h;
    }

    void BuildBaseHeights()
    {
        foreach (var l in lakes) l.level = BaseRaw(l.c.x, l.c.y) - 1.5f;

        baseH = new float[Res, Res];
        for (int z = 0; z < Res; z++)
        for (int x = 0; x < Res; x++)
        {
            float wx = x * Cell, wz = z * Cell;
            float h = BaseRaw(wx, wz);
            foreach (var l in lakes)
            {
                float dl = Vector2.Distance(new Vector2(wx, wz), l.c);
                if (dl >= l.r + 20f) continue;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(l.r * 0.7f, l.r + 5f, dl));
                float rim = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(l.r + 5f, l.r + 20f, dl));
                float outside = Mathf.Lerp(Mathf.Max(h, l.level + 0.6f), h, rim);
                h = Mathf.Lerp(l.level - 2.5f, outside, t);
            }
            baseH[z, x] = h;
        }
    }

    // ================================================================== trails

    void BuildPaths()
    {
        startPath = new TrackPath
        {
            name = "Start", ctrl = V(1000, 170, 1000, 210, 1000, 250),
            halfWidth = 5f, falloff = 10f, maxUp = 0.03f, maxDown = 0.03f, smoothRadius = 12, layer = 1, treeClear = 6f
        };
        var easy = new TrackPath
        {
            name = "Easy",
            ctrl = V(1000, 250, 930, 270, 820, 295, 700, 330, 600, 370, 530, 430, 490, 490, 472, 560, 488, 640,
                     450, 730, 390, 820, 360, 920, 343, 1000, 338, 1060, 352, 1140, 400, 1250, 470, 1360,
                     560, 1460, 680, 1560, 810, 1650, 920, 1725, 1000, 1780),
            halfWidth = 4.5f, falloff = 9f, maxUp = 0.045f, maxDown = 0.045f, smoothRadius = 18, layer = 1, treeClear = 7f
        };
        var hard = new TrackPath
        {
            name = "Hard",
            ctrl = V(1000, 250, 1035, 330, 975, 395, 1045, 460, 965, 530, 1040, 600, 975, 670, 1030, 740,
                     990, 820, 1060, 900, 1110, 990, 1090, 1090, 1030, 1170, 960, 1240, 1040, 1300, 965, 1365,
                     1035, 1430, 975, 1500, 1020, 1580, 990, 1680, 1000, 1780),
            halfWidth = 4.25f, falloff = 10f, maxUp = 0.15f, maxDown = 0.15f, smoothRadius = 15, layer = 2, climbOnly = true,
            roughness = 0f, treeClear = 5.5f
        };
        var sprint = new TrackPath
        {
            name = "Sprint",
            ctrl = V(1000, 250, 1090, 262, 1300, 270, 1500, 280, 1640, 330, 1705, 430, 1722, 600, 1725, 800,
                     1722, 1000, 1712, 1200, 1680, 1380, 1600, 1520, 1470, 1615, 1320, 1685, 1160, 1735, 1000, 1780),
            halfWidth = 5f, falloff = 12f, maxUp = 0.035f, maxDown = 0.035f, smoothRadius = 25, layer = 3, treeClear = 6f
        };

        // the start road and fork plaza are completely flat
        ComputePathHeights(startPath, null, null);
        float avg = 0f;
        foreach (var pt in startPath.pts) avg += pt.y;
        forkH = avg / startPath.pts.Length;
        for (int i = 0; i < startPath.pts.Length; i++) startPath.pts[i].y = forkH;

        // the finish sits above the fork (so the hard trail only ever climbs), but no higher
        // than every trail can reach comfortably within its grade limit
        float s = 0f; int c = 0;
        for (int dz = -15; dz <= 15; dz += 5)
        for (int dx = -15; dx <= 15; dx += 5) { s += SampleGrid(baseH, FinishCenter.x + dx, FinishCenter.y + dz); c++; }
        float cap = float.MaxValue;
        foreach (var b in new[] { easy, hard, sprint }) cap = Mathf.Min(cap, 0.6f * b.maxUp * SplineLength(b.ctrl));
        finishH = Mathf.Clamp(s / c, forkH + Mathf.Min(10f, cap), forkH + cap);

        branches = new[] { easy, hard, sprint };
        foreach (var b in branches) ComputePathHeights(b, forkH, finishH);
        allPaths = new[] { startPath, easy, hard, sprint };
    }

    static float SplineLength(Vector2[] ctrl)
    {
        var pts = SampleSpline(ctrl, 1f);
        float len = 0f;
        for (int i = 1; i < pts.Count; i++) len += Vector2.Distance(pts[i], pts[i - 1]);
        return len;
    }

    static Vector2[] V(params float[] a)
    {
        var r = new Vector2[a.Length / 2];
        for (int i = 0; i < r.Length; i++) r[i] = new Vector2(a[2 * i], a[2 * i + 1]) * S;
        return r;
    }

    void ComputePathHeights(TrackPath p, float? pinStart, float? pinEnd)
    {
        var xz = SampleSpline(p.ctrl, 1f);
        int n = xz.Count;
        var ds = new float[n];
        for (int i = 1; i < n; i++) ds[i] = Vector2.Distance(xz[i], xz[i - 1]);

        var raw = new float[n];
        for (int i = 0; i < n; i++) raw[i] = SampleGrid(baseH, xz[i].x, xz[i].y);

        var h = new float[n];
        for (int i = 0; i < n; i++)
        {
            int a = Mathf.Max(0, i - p.smoothRadius), b = Mathf.Min(n - 1, i + p.smoothRadius);
            float sum = 0f;
            for (int k = a; k <= b; k++) sum += raw[k];
            h[i] = sum / (b - a + 1);
        }

        // limit climbs to maxUp and descents to maxDown (in the direction of travel)
        int iters = pinEnd.HasValue ? 4 : 1;
        if (p.climbOnly && pinStart.HasValue && pinEnd.HasValue)
        {
            // never goes down: follows the hills up, stays level over dips, cuts through tops above the finish height
            float top = pinEnd.Value;
            for (int it = 0; it < 4; it++)
            {
                h[0] = pinStart.Value;
                for (int i = 1; i < n; i++)
                    h[i] = Mathf.Clamp(h[i], h[i - 1], Mathf.Min(h[i - 1] + p.maxUp * ds[i], top));
                h[n - 1] = top;
                for (int i = n - 2; i >= 0; i--)
                    h[i] = Mathf.Clamp(h[i], h[i + 1] - p.maxUp * ds[i + 1], h[i + 1]);
            }
            iters = 0;
        }
        for (int it = 0; it < iters; it++)
        {
            if (pinStart.HasValue) h[0] = pinStart.Value;
            for (int i = 1; i < n; i++)
                h[i] = Mathf.Clamp(h[i], h[i - 1] - p.maxDown * ds[i], h[i - 1] + p.maxUp * ds[i]);
            if (pinEnd.HasValue)
            {
                h[n - 1] = pinEnd.Value;
                for (int i = n - 2; i >= 0; i--)
                    h[i] = Mathf.Clamp(h[i], h[i + 1] - p.maxUp * ds[i + 1], h[i + 1] + p.maxDown * ds[i + 1]);
            }
        }
        if (pinStart.HasValue) h[0] = pinStart.Value;

        // round off the sharp corners the grade limits leave at crests and dips.
        // both ends stay fixed, and averaging never makes a slope steeper.
        int ps = Mathf.Max(8, p.smoothRadius);
        var tmp = new float[n];
        for (int pass = 0; pass < 5; pass++)
        {
            for (int i = 0; i < n; i++)
            {
                int r = Mathf.Min(ps, Mathf.Min(i, n - 1 - i));
                float sum = 0f;
                for (int k = i - r; k <= i + r; k++) sum += h[k];
                tmp[i] = sum / (2 * r + 1);
            }
            System.Array.Copy(tmp, h, n);
        }

        p.pts = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float bump = (p.roughness > 0f && i > 25 && i < n - 35)
                ? (Mathf.PerlinNoise(ox + i * 0.12f, oz + 7f) - 0.5f) * 2f * p.roughness
                : 0f;
            p.pts[i] = new Vector3(xz[i].x, h[i] + bump, xz[i].y);
        }
    }

    void BuildLookouts()
    {
        lookouts = new List<Lookout>();
        var easy = allPaths[EasyId];
        foreach (var lake in new[] { lakes[0], lakes[1] })
        {
            int best = 0; float bestD = float.MaxValue;
            for (int i = 0; i < easy.pts.Length; i++)
            {
                float d = Vector2.Distance(new Vector2(easy.pts[i].x, easy.pts[i].z), lake.c);
                if (d < bestD) { bestD = d; best = i; }
            }
            var p = easy.pts[best];
            var dir = new Vector3(lake.c.x - p.x, 0f, lake.c.y - p.z).normalized;
            var center = p + dir * (easy.halfWidth + 6f);
            center.y = p.y;
            lookouts.Add(new Lookout { center = center, dir = dir, lake = lake, index = best });
        }
    }

    static List<Vector2> SampleSpline(Vector2[] c, float step)
    {
        var result = new List<Vector2>();
        for (int i = 0; i < c.Length - 1; i++)
        {
            Vector2 p0 = c[Mathf.Max(i - 1, 0)], p1 = c[i], p2 = c[i + 1], p3 = c[Mathf.Min(i + 2, c.Length - 1)];
            int n = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(p1, p2) / step));
            for (int s = 0; s < n; s++) result.Add(CatmullRom(p0, p1, p2, p3, s / (float)n));
        }
        result.Add(c[c.Length - 1]);
        return result;
    }

    static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    static float SampleGrid(float[,] g, float x, float z)
    {
        float fx = Mathf.Clamp(x / Cell, 0f, Res - 1.001f), fz = Mathf.Clamp(z / Cell, 0f, Res - 1.001f);
        int x0 = (int)fx, z0 = (int)fz;
        float tx = fx - x0, tz = fz - z0;
        float a = Mathf.Lerp(g[z0, x0], g[z0, x0 + 1], tx);
        float b = Mathf.Lerp(g[z0 + 1, x0], g[z0 + 1, x0 + 1], tx);
        return Mathf.Lerp(a, b, tz);
    }

    static Vector3 Tangent(TrackPath p, int i)
    {
        int a = Mathf.Clamp(i - 2, 0, p.pts.Length - 1), b = Mathf.Clamp(i + 2, 0, p.pts.Length - 1);
        var d = p.pts[b] - p.pts[a];
        d.y = 0f;
        return d.normalized;
    }

    // ================================================================== carving

    float[,] CarveHeights()
    {
        edgeBuf = new float[Res, Res];
        targetBuf = new float[Res, Res];
        falloffBuf = new float[Res, Res];
        idBuf = new int[Res, Res];
        for (int z = 0; z < Res; z++)
        for (int x = 0; x < Res; x++) { edgeBuf[z, x] = float.MaxValue; idBuf[z, x] = -1; }

        for (int id = 0; id < allPaths.Length; id++)
        {
            float reach = allPaths[id].falloff * (id == HardId ? HardFillSpread : 1f);
            var pts = allPaths[id].pts;
            for (int i = 0; i < pts.Length - 1; i++)
                StampSegment(pts[i], pts[i + 1], allPaths[id].halfWidth, allPaths[id].falloff, reach, id);
        }

        var s0 = startPath.pts[0];
        Stamp(s0.x, s0.z, s0.y, 8f, 10f, 10f, StartId);
        var fork = startPath.pts[startPath.pts.Length - 1];
        Stamp(fork.x, fork.z, forkH, 14f, 10f, 10f, StartId);
        Stamp(FinishCenter.x, FinishCenter.y, finishH, FinishRadius, 12f, 12f, StartId);
        foreach (var lo in lookouts) Stamp(lo.center.x, lo.center.z, lo.center.y, 6f, 8f, 8f, EasyId);

        var result = new float[Res, Res];
        for (int z = 0; z < Res; z++)
        for (int x = 0; x < Res; x++)
        {
            float h = baseH[z, x];
            float e = edgeBuf[z, x];
            if (e <= 0f) h = targetBuf[z, x];
            else if (idBuf[z, x] >= 0)
            {
                // spread the hard trail's banks out wide (above or below the ground)
                // so it reads as a natural hillside, not a raised ridge or a trench
                float f = falloffBuf[z, x];
                if (idBuf[z, x] == HardId) f *= HardFillSpread;
                if (e < f) h = Mathf.Lerp(targetBuf[z, x], h, Mathf.SmoothStep(0f, 1f, e / f));
            }
            result[z, x] = Mathf.Clamp01(h / SizeY);
        }
        return result;
    }

    // every ground cell takes the trail height at its exact closest point on the centre line,
    // so the riding surface is one smooth slope instead of small steps between sample points
    void StampSegment(Vector3 a, Vector3 b, float halfW, float falloff, float reach, int id)
    {
        float r = halfW + reach;
        int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - r) / Cell)), x1 = Mathf.Min(Res - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + r) / Cell));
        int z0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.z, b.z) - r) / Cell)), z1 = Mathf.Min(Res - 1, Mathf.CeilToInt((Mathf.Max(a.z, b.z) + r) / Cell));
        float abx = b.x - a.x, abz = b.z - a.z;
        float len2 = Mathf.Max(1e-6f, abx * abx + abz * abz);
        for (int iz = z0; iz <= z1; iz++)
        for (int ix = x0; ix <= x1; ix++)
        {
            float px = ix * Cell - a.x, pz = iz * Cell - a.z;
            float t = Mathf.Clamp01((px * abx + pz * abz) / len2);
            float dx = px - abx * t, dz = pz - abz * t;
            float e = Mathf.Sqrt(dx * dx + dz * dz) - halfW;
            if (e > reach || e >= edgeBuf[iz, ix]) continue;
            edgeBuf[iz, ix] = e;
            targetBuf[iz, ix] = Mathf.Lerp(a.y, b.y, t);
            falloffBuf[iz, ix] = falloff;
            idBuf[iz, ix] = id;
        }
    }

    void Stamp(float x, float z, float h, float halfW, float falloff, float reach, int id)
    {
        float r = halfW + reach;
        int x0 = Mathf.Max(0, Mathf.FloorToInt((x - r) / Cell)), x1 = Mathf.Min(Res - 1, Mathf.CeilToInt((x + r) / Cell));
        int z0 = Mathf.Max(0, Mathf.FloorToInt((z - r) / Cell)), z1 = Mathf.Min(Res - 1, Mathf.CeilToInt((z + r) / Cell));
        for (int iz = z0; iz <= z1; iz++)
        for (int ix = x0; ix <= x1; ix++)
        {
            float dx = ix * Cell - x, dz = iz * Cell - z;
            float e = Mathf.Sqrt(dx * dx + dz * dz) - halfW;
            if (e > reach || e >= edgeBuf[iz, ix]) continue;
            edgeBuf[iz, ix] = e;
            targetBuf[iz, ix] = h;
            falloffBuf[iz, ix] = falloff;
            idBuf[iz, ix] = id;
        }
    }

    float EdgeAt(float wx, float wz)
    {
        int hx = Mathf.Clamp(Mathf.RoundToInt(wx / Cell), 0, Res - 1);
        int hz = Mathf.Clamp(Mathf.RoundToInt(wz / Cell), 0, Res - 1);
        return edgeBuf[hz, hx];
    }

    // ================================================================== painting

    void PaintTerrain()
    {
        // 4 layers keeps the terrain to a single render pass (important on Quest)
        var grass  = MakeLayer("Grass",  MakeGroundTexture("GrassTex",  new Color(0.24f, 0.38f, 0.16f), new Color(0.33f, 0.47f, 0.20f), 1), 6f);
        var dirt   = MakeLayer("Dirt",   MakeGroundTexture("DirtTex",   new Color(0.40f, 0.30f, 0.20f), new Color(0.52f, 0.40f, 0.27f), 2), 5f);
        var rock   = MakeLayer("Rock",   MakeGroundTexture("RockTex",   new Color(0.40f, 0.39f, 0.37f), new Color(0.56f, 0.55f, 0.51f), 3), 8f);
        var gravel = MakeLayer("Gravel", MakeGroundTexture("GravelTex", new Color(0.63f, 0.58f, 0.49f), new Color(0.74f, 0.70f, 0.62f), 4), 4f);
        td.terrainLayers = new[] { grass, dirt, rock, gravel };
        PaintAlphamaps();
    }

    void PaintAlphamaps()
    {
        int ar = td.alphamapResolution;
        var maps = new float[ar, ar, 4];
        for (int az = 0; az < ar; az++)
        for (int ax = 0; ax < ar; ax++)
        {
            float u = (ax + 0.5f) / ar, v = (az + 0.5f) / ar;
            int hx = Mathf.Min(Res - 1, Mathf.RoundToInt(u * (Res - 1)));
            int hz = Mathf.Min(Res - 1, Mathf.RoundToInt(v * (Res - 1)));
            float wx = u * SizeXZ, wz = v * SizeXZ;

            float e = edgeBuf[hz, hx];
            float pathW = e <= 0f ? 1f : (e < 1.5f ? 1f - e / 1.5f : 0f);
            float height = td.GetInterpolatedHeight(u, v);
            float rockW = Mathf.Max(Mathf.InverseLerp(28f, 42f, td.GetSteepness(u, v)),
                                    Mathf.InverseLerp(BareRockHeight, BareRockHeight + 15f, height));
            // leaf litter / bare forest floor patches
            float dirtW = Mathf.Clamp01((Mathf.PerlinNoise(ox + u * 70f, oz + v * 70f) - 0.55f) * 3.5f) * (1f - rockW);
            foreach (var l in lakes)
                if (Vector2.Distance(new Vector2(wx, wz), l.c) < l.r + 7f) dirtW = 1f - rockW;
            float grassW = Mathf.Max(0f, 1f - rockW - dirtW);

            float k = 1f - pathW;
            maps[az, ax, 0] = grassW * k;
            maps[az, ax, 1] = dirtW * k;
            maps[az, ax, 2] = rockW * k;
            if (pathW > 0f) maps[az, ax, allPaths[idBuf[hz, hx]].layer] += pathW;
        }
        td.SetAlphamaps(0, 0, maps);
    }

    void ConfigureTerrain()
    {
        terrain.heightmapPixelError = 8f;
        terrain.basemapDistance = 200f;
        terrain.treeDistance = 280f;
        terrain.treeBillboardDistance = 280f;
        terrain.treeMaximumFullLODCount = 10000;
        terrain.drawInstanced = true;
        var sh = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if (sh != null)
        {
            var m = new Material(sh);
            AssetDatabase.CreateAsset(m, GenFolder + "/TerrainMat.mat");
            terrain.materialTemplate = m;
        }
    }

    // ================================================================== vegetation

    bool Free(float wx, float wz, bool useTreeClear, float pathGap, float lakeGap)
    {
        if (wx < 2f || wz < 2f || wx > SizeXZ - 2f || wz > SizeXZ - 2f) return false;
        int hx = Mathf.Clamp(Mathf.RoundToInt(wx / Cell), 0, Res - 1);
        int hz = Mathf.Clamp(Mathf.RoundToInt(wz / Cell), 0, Res - 1);
        int id = idBuf[hz, hx];
        if (id >= 0)
        {
            float gap = useTreeClear ? allPaths[id].treeClear : pathGap;
            if (edgeBuf[hz, hx] < gap) return false;
        }
        var p = new Vector2(wx, wz);
        foreach (var l in lakes) if (Vector2.Distance(p, l.c) < l.r + lakeGap) return false;
        return true;
    }

    bool TreeGround(float wx, float wz)
    {
        float u = wx / SizeXZ, v = wz / SizeXZ;
        return td.GetSteepness(u, v) < 42f && td.GetInterpolatedHeight(u, v) < TreeLine;
    }

    void AddInst(int proto, float wx, float wz, float s)
    {
        float u = wx / SizeXZ, v = wz / SizeXZ;
        instances.Add(new TreeInstance
        {
            position = new Vector3(u, td.GetInterpolatedHeight(u, v) / SizeY, v),
            prototypeIndex = proto,
            widthScale = s,
            heightScale = s * Rand(0.9f, 1.2f),
            rotation = Rand(0f, Mathf.PI * 2f),
            color = Color.white,
            lightmapColor = Color.white
        });
    }

    void PlaceTree(int treeIdx, float wx, float wz, float s)
    {
        if (treeProto[treeIdx] >= 0) { AddInst(treeProto[treeIdx], wx, wz, s); return; }
        if (!looseTrees) looseTrees = new GameObject("Forest (prefab trees)").transform;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(treeList[treeIdx]);
        go.transform.SetParent(looseTrees);
        go.transform.position = new Vector3(wx, terrain.SampleHeight(new Vector3(wx, 0f, wz)), wz);
        go.transform.rotation = Quaternion.Euler(0f, Rand(0f, 360f), 0f);
        go.transform.localScale *= s;
        go.isStatic = true;
    }

    void PlantVegetation()
    {
        treeList = new List<GameObject>();
        if (treePrefabs != null) foreach (var p in treePrefabs) if (p) treeList.Add(p);
        bool customTrees = treeList.Count > 0;
        if (!customTrees) treeList.AddRange(BuildDefaultTrees()); // 0 PineDark, 1 PineLight, 2 Round, 3 Birch
        var decor = BuildDecor();                                   // 0 bush, 1-3 flowers

        var protos = new List<TreePrototype>();
        treeProto = new int[treeList.Count];
        for (int i = 0; i < treeList.Count; i++)
        {
            if (IsTerrainTreeCompatible(treeList[i]))
            {
                treeProto[i] = protos.Count;
                protos.Add(new TreePrototype { prefab = treeList[i] });
            }
            else treeProto[i] = -1;
        }
        int bushProto = protos.Count;
        protos.Add(new TreePrototype { prefab = decor[0] });
        var flowerProto = new int[3];
        for (int k = 0; k < 3; k++) { flowerProto[k] = protos.Count; protos.Add(new TreePrototype { prefab = decor[k + 1] }); }
        td.treePrototypes = protos.ToArray();

        instances = new List<TreeInstance>();
        int[] allTrees = new int[treeList.Count];
        for (int i = 0; i < allTrees.Length; i++) allTrees[i] = i;
        int[] easyLining = customTrees ? allTrees : new[] { 2, 3 };

        // 1. dense main forest with only small clearings
        int placed = 0, attempts = 0;
        while (placed < treeCount && attempts++ < treeCount * 20)
        {
            float wx = Rand(0f, SizeXZ), wz = Rand(0f, SizeXZ);
            if (!Free(wx, wz, true, 0f, 12f) || !TreeGround(wx, wz)) continue;
            if (Mathf.PerlinNoise(ox * 0.5f + wx * 0.012f, oz * 0.5f + wz * 0.012f) < 0.24f) continue;
            PlaceTree(rng.Next(treeList.Count), wx, wz, Rand(0.85f, 1.6f));
            placed++;
        }

        // 2. trees close along every trail so you ride through the forest, not past it
        LineTrail(allPaths[EasyId], easyLining, 7, 7f, 11f);
        LineTrail(allPaths[HardId], allTrees, 6, 5.5f, 9f);
        LineTrail(allPaths[SprintId], allTrees, 7, 6f, 10f);
        LineTrail(startPath, allTrees, 6, 6f, 9f);

        // 3. easy trail: bushes and flowers along it
        var easy = allPaths[EasyId];
        for (int i = 0; i < easy.pts.Length; i++)
        {
            Vector3 right = Vector3.Cross(Vector3.up, Tangent(easy, i));
            if (i % 5 == 0)
            {
                var p = easy.pts[i] + right * (rng.NextDouble() < 0.5 ? -1f : 1f) * (easy.halfWidth + Rand(3.5f, 8f));
                if (Free(p.x, p.z, false, 3f, 4f)) AddInst(bushProto, p.x, p.z, Rand(0.7f, 1.3f));
            }
            if (i % 2 == 0)
            {
                for (int k = 0; k < 2; k++)
                {
                    var p = easy.pts[i] + right * (rng.NextDouble() < 0.5 ? -1f : 1f) * (easy.halfWidth + Rand(1.2f, 7f));
                    if (Free(p.x, p.z, false, 1f, 3f)) AddInst(flowerProto[rng.Next(3)], p.x, p.z, Rand(0.8f, 1.4f));
                }
            }
        }

        // 4. flowers around lake shores
        foreach (var l in lakes)
        {
            for (int k = 0; k < 80; k++)
            {
                float a = Rand(0f, Mathf.PI * 2f), r = l.r + Rand(7f, 18f);
                float wx = l.c.x + Mathf.Cos(a) * r, wz = l.c.y + Mathf.Sin(a) * r;
                if (Free(wx, wz, false, 1.2f, 6f)) AddInst(flowerProto[rng.Next(3)], wx, wz, Rand(0.8f, 1.4f));
            }
        }

        // 5. flowers in the small clearings
        placed = 0; attempts = 0;
        while (placed < 1000 && attempts++ < 40000)
        {
            float wx = Rand(0f, SizeXZ), wz = Rand(0f, SizeXZ);
            if (Mathf.PerlinNoise(ox * 0.5f + wx * 0.012f, oz * 0.5f + wz * 0.012f) >= 0.24f) continue;
            if (!Free(wx, wz, false, 1.5f, 8f) || !TreeGround(wx, wz)) continue;
            AddInst(flowerProto[rng.Next(3)], wx, wz, Rand(0.8f, 1.5f));
            placed++;
        }

        // 6. thick undergrowth
        placed = 0; attempts = 0;
        while (placed < 3000 && attempts++ < 40000)
        {
            float wx = Rand(0f, SizeXZ), wz = Rand(0f, SizeXZ);
            if (!Free(wx, wz, false, 2.5f, 8f) || !TreeGround(wx, wz)) continue;
            AddInst(bushProto, wx, wz, Rand(0.6f, 1.5f));
            placed++;
        }

        td.SetTreeInstances(instances.ToArray(), true);
        terrain.Flush();
    }

    void LineTrail(TrackPath p, int[] choices, int every, float minOff, float maxOff)
    {
        for (int i = 10; i < p.pts.Length - 30; i += every)
        {
            Vector3 right = Vector3.Cross(Vector3.up, Tangent(p, i));
            for (int side = -1; side <= 1; side += 2)
            {
                if (rng.NextDouble() > 0.7) continue;
                var q = p.pts[i] + right * side * (p.halfWidth + Rand(minOff, maxOff));
                if (Free(q.x, q.z, true, 0f, 10f) && TreeGround(q.x, q.z))
                    PlaceTree(choices[rng.Next(choices.Length)], q.x, q.z, Rand(0.9f, 1.5f));
            }
        }
    }

    static bool IsTerrainTreeCompatible(GameObject p) =>
        p.GetComponent<LODGroup>() != null || (p.GetComponent<MeshFilter>() != null && p.GetComponent<MeshRenderer>() != null);

    GameObject[] BuildDefaultTrees()
    {
        var bark = MakeMat("Bark", new Color(0.36f, 0.25f, 0.17f));
        var birchBark = MakeMat("BirchBark", new Color(0.88f, 0.87f, 0.82f));
        var pineDark = MakeMat("PineDark", new Color(0.12f, 0.30f, 0.16f));
        var pineLight = MakeMat("PineLight", new Color(0.20f, 0.40f, 0.19f));
        var leafy = MakeMat("Leafy", new Color(0.32f, 0.50f, 0.20f));
        var birchLeaf = MakeMat("BirchLeaf", new Color(0.48f, 0.63f, 0.26f));

        var pine = new MeshBuilder(2);
        pine.Prism(0, Vector3.zero, 2.4f, 0.28f, 6);
        for (int k = 0; k < 4; k++) pine.Cone(1, new Vector3(0f, 1.4f + k * 2f, 0f), 3.2f, 2.4f * (1f - k * 0.2f), 8);
        var pineMesh = SaveMesh(pine.ToMesh(), "PineMesh");

        var round = new MeshBuilder(2);
        round.Prism(0, Vector3.zero, 3.6f, 0.3f, 6);
        round.Ico(1, new Vector3(0f, 4.8f, 0f), 2.5f, 0.9f, 0.18f, rng);
        round.Ico(1, new Vector3(0.8f, 4.0f, 0.4f), 1.5f, 0.9f, 0.2f, rng);
        var roundMesh = SaveMesh(round.ToMesh(), "RoundTreeMesh");

        var birch = new MeshBuilder(2);
        birch.Prism(0, Vector3.zero, 4.8f, 0.17f, 6);
        birch.Ico(1, new Vector3(0f, 5.6f, 0f), 1.7f, 1.3f, 0.2f, rng);
        birch.Ico(1, new Vector3(0.5f, 4.6f, 0.2f), 1.1f, 1.1f, 0.2f, rng);
        var birchMesh = SaveMesh(birch.ToMesh(), "BirchMesh");

        return new[]
        {
            MakePrefab("Pine_Dark", pineMesh, new[] { bark, pineDark }, true),
            MakePrefab("Pine_Light", pineMesh, new[] { bark, pineLight }, true),
            MakePrefab("RoundTree", roundMesh, new[] { bark, leafy }, true),
            MakePrefab("Birch", birchMesh, new[] { birchBark, birchLeaf }, true)
        };
    }

    GameObject[] BuildDecor()
    {
        var bushMat = MakeMat("BushGreen", new Color(0.18f, 0.38f, 0.16f));
        var stemMat = MakeMat("Stem", new Color(0.25f, 0.50f, 0.20f));
        var yellow = MakeMat("PetalYellow", new Color(0.98f, 0.82f, 0.20f));
        var purple = MakeMat("PetalPurple", new Color(0.62f, 0.38f, 0.85f));
        var white = MakeMat("PetalWhite", new Color(0.95f, 0.95f, 0.92f));

        var bush = new MeshBuilder(1);
        bush.Ico(0, new Vector3(0f, 0.45f, 0f), 0.6f, 0.8f, 0.2f, rng);
        bush.Ico(0, new Vector3(0.45f, 0.35f, 0.2f), 0.45f, 0.8f, 0.2f, rng);
        bush.Ico(0, new Vector3(-0.4f, 0.35f, -0.15f), 0.5f, 0.8f, 0.2f, rng);
        var bushMesh = SaveMesh(bush.ToMesh(), "BushMesh");

        var fl = new MeshBuilder(2);
        for (int f = 0; f < 5; f++)
        {
            var p = new Vector3(Rand(-0.35f, 0.35f), 0f, Rand(-0.35f, 0.35f));
            float h = Rand(0.3f, 0.55f);
            fl.Prism(0, p, h, 0.02f, 3);
            fl.Cone(1, p + Vector3.up * (h - 0.03f), 0.08f, 0.09f, 5);
        }
        var flowerMesh = SaveMesh(fl.ToMesh(), "FlowerMesh");

        return new[]
        {
            MakePrefab("Bush", bushMesh, new[] { bushMat }, false),
            MakePrefab("Flowers_Yellow", flowerMesh, new[] { stemMat, yellow }, false),
            MakePrefab("Flowers_Purple", flowerMesh, new[] { stemMat, purple }, false),
            MakePrefab("Flowers_White", flowerMesh, new[] { stemMat, white }, false)
        };
    }

    // ================================================================== rocks and logs

    List<GameObject> rockList;
    float[] rockRadius;
    Transform rocksParent, logsParent;
    Material logMat;

    void PlaceRocksAndLogs()
    {
        rockList = new List<GameObject>();
        if (rockPrefabs != null) foreach (var p in rockPrefabs) if (p) rockList.Add(p);
        if (rockList.Count == 0)
        {
            var rockMat = MakeMat("RockMat", new Color(0.46f, 0.45f, 0.43f));
            for (int i = 0; i < 3; i++)
            {
                var mb = new MeshBuilder(1);
                mb.Ico(0, Vector3.zero, 1f, 0.65f, 0.28f, rng);
                rockList.Add(MakePrefab("Rock_" + i, SaveMesh(mb.ToMesh(), "RockMesh_" + i), new[] { rockMat }, false));
            }
        }
        // horizontal footprint of each rock prefab at scale 1, so they can be kept off the trail
        rockRadius = new float[rockList.Count];
        for (int i = 0; i < rockList.Count; i++)
        {
            float r = 1.3f;
            var rends = rockList[i].GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                foreach (var rend in rends) b.Encapsulate(rend.bounds);
                r = Mathf.Max(b.extents.x, b.extents.z) + Mathf.Max(Mathf.Abs(b.center.x), Mathf.Abs(b.center.z));
            }
            rockRadius[i] = Mathf.Max(0.2f, r);
        }

        rocksParent = new GameObject("Rocks").transform;
        logsParent = new GameObject("Fallen Logs").transform;
        logMat = MakeMat("LogBark", new Color(0.33f, 0.24f, 0.17f));

        // along every trail's shoulders (more on the hard trail)
        DecorateTrail(allPaths[HardId], 3, 0.75f, 18, 1.8f);
        DecorateTrail(allPaths[EasyId], 6, 0.5f, 30, 1.2f);
        DecorateTrail(allPaths[SprintId], 7, 0.5f, 35, 1.4f);

        // scattered through the forest
        int placed = 0, attempts = 0;
        while (placed < 500 && attempts++ < 10000)
        {
            float wx = Rand(0f, SizeXZ), wz = Rand(0f, SizeXZ);
            if (!Free(wx, wz, false, 3f, 2f)) continue;
            TryRock(wx, wz, Rand(0.7f, 2.6f), 1f);
            placed++;
        }
        placed = 0; attempts = 0;
        while (placed < 120 && attempts++ < 6000)
        {
            float wx = Rand(0f, SizeXZ), wz = Rand(0f, SizeXZ);
            if (!Free(wx, wz, false, 5f, 8f) || !TreeGround(wx, wz)) continue;
            TryLog(wx, wz, Rand(0f, 360f), Rand(0.45f, 0.8f), Rand(4f, 8f), 2f);
            placed++;
        }

        // rocks along lake shores
        foreach (var l in lakes)
        {
            for (int k = 0; k < 12; k++)
            {
                float a = Rand(0f, Mathf.PI * 2f), r = l.r + Rand(0f, 8f);
                TryRock(l.c.x + Mathf.Cos(a) * r, l.c.y + Mathf.Sin(a) * r, Rand(0.8f, 2f), 1f);
            }
        }
    }

    void DecorateTrail(TrackPath p, int rockEvery, float rockChance, int logEvery, float maxRock)
    {
        for (int i = 25; i < p.pts.Length - 50; i++)
        {
            Vector3 t = Tangent(p, i), right = Vector3.Cross(Vector3.up, t);
            if (i % rockEvery == 0 && rng.NextDouble() < rockChance)
            {
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                float s = Rand(0.4f, maxRock);
                var q = p.pts[i] + right * side * (p.halfWidth + 0.8f + s * 1.4f + Rand(0f, 2.5f));
                if (TryRock(q.x, q.z, s, 0.6f) && rng.NextDouble() < 0.4)
                {
                    // small cluster, pushed further from the trail
                    for (int k = 0; k < 2; k++)
                    {
                        var q2 = q + right * side * Rand(0.8f, 2f) + t * Rand(-1.5f, 1.5f);
                        TryRock(q2.x, q2.z, s * Rand(0.4f, 0.7f), 0.6f);
                    }
                }
            }
            if (i % logEvery == logEvery / 2)
            {
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                float d = Rand(0.4f, 0.7f), len = Rand(3f, 6f);
                var q = p.pts[i] + right * side * (p.halfWidth + d * 0.5f + 1.2f + Rand(0f, 2f));
                float yaw = Quaternion.LookRotation(t).eulerAngles.y + Rand(-8f, 8f); // lies parallel to the trail
                TryLog(q.x, q.z, yaw, d, len, 0.8f);
            }
        }
    }

    // places a rock only if its whole footprint stays at least `margin` metres off every trail surface
    bool TryRock(float x, float z, float s, float margin)
    {
        int k = rng.Next(rockList.Count);
        float footprint = rockRadius[k] * s * 1.3f;
        if (EdgeAt(x, z) < footprint + margin) return false;
        if (x < 2f || z < 2f || x > SizeXZ - 2f || z > SizeXZ - 2f) return false;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(rockList[k]);
        go.transform.SetParent(rocksParent);
        float y = terrain.SampleHeight(new Vector3(x, 0f, z));
        go.transform.position = new Vector3(x, y - 0.25f * s, z);
        go.transform.rotation = Quaternion.Euler(Rand(-10f, 10f), Rand(0f, 360f), Rand(-10f, 10f));
        go.transform.localScale = Vector3.Scale(go.transform.localScale, new Vector3(s * Rand(0.8f, 1.3f), s, s * Rand(0.8f, 1.3f)));
        go.isStatic = true;
        return true;
    }

    // places a log only if its middle and both ends stay off every trail surface
    bool TryLog(float x, float z, float yaw, float d, float len, float margin)
    {
        var axis = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        var mid = new Vector3(x, 0f, z);
        foreach (var q in new[] { mid, mid + axis * len * 0.5f, mid - axis * len * 0.5f })
            if (EdgeAt(q.x, q.z) < d * 0.5f + margin) return false;

        float y = terrain.SampleHeight(mid) + d * 0.5f - 0.1f;
        Prim(PrimitiveType.Cylinder, logsParent, new Vector3(x, y, z), new Vector3(d, len * 0.5f, d), logMat,
             Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, 0f));
        return true;
    }

    // ================================================================== water

    void PlaceWater()
    {
        var parent = new GameObject("Lakes").transform;
        Material mat = null;
        Mesh disc = null;
        if (!waterPrefab)
        {
            mat = MakeMat("LakeWater", new Color(0.16f, 0.36f, 0.40f), 0.9f);
            var mb = new MeshBuilder(1);
            mb.Disc(0, Vector3.zero, 1f, 48);
            disc = SaveMesh(mb.ToMesh(), "LakeDisc");
        }
        for (int i = 0; i < lakes.Length; i++)
        {
            var l = lakes[i];
            var pos = new Vector3(l.c.x, l.level, l.c.y);
            if (waterPrefab)
            {
                var w = (GameObject)PrefabUtility.InstantiatePrefab(waterPrefab);
                w.name = "Lake_" + i;
                w.transform.SetParent(parent);
                w.transform.position = pos;
                continue;
            }
            var lake = new GameObject("Lake_" + i);
            lake.transform.SetParent(parent);
            lake.transform.position = pos;
            lake.transform.localScale = new Vector3(l.r + 5f, 1f, l.r + 5f);
            lake.AddComponent<MeshFilter>().sharedMesh = disc;
            lake.AddComponent<MeshRenderer>().sharedMaterial = mat;
            lake.isStatic = true;
        }
    }

    // ================================================================== signs, arches, summits

    void PlaceMarkers()
    {
        var parent = new GameObject("Track Markers").transform;
        var wood = MakeMat("Wood", new Color(0.45f, 0.32f, 0.20f));
        var dark = MakeMat("ArchDark", new Color(0.15f, 0.15f, 0.17f));
        var stone = MakeMat("SummitSign", new Color(0.35f, 0.33f, 0.30f));

        var spawn = new GameObject("PlayerSpawn");
        spawn.transform.SetParent(parent);
        spawn.transform.SetPositionAndRotation(startPath.pts[3], Quaternion.LookRotation(Tangent(startPath, 3)));

        MakeArch("START", startPath, 15, dark, parent);

        string[] titles = { "EASY", "HARD", "SPRINT" };
        string[] subs = { "Scenic lake trail", "Mountain climb", "Speed run" };
        Color[] cols = { new Color(0.20f, 0.55f, 0.25f), new Color(0.70f, 0.18f, 0.15f), new Color(0.15f, 0.35f, 0.70f) };
        Vector3 forkCenter = startPath.pts[startPath.pts.Length - 1];

        for (int b = 0; b < branches.Length; b++)
        {
            var p = branches[b];
            var boardMat = MakeMat("Sign_" + titles[b], cols[b]);

            int si = Mathf.Min(20, p.pts.Length - 1);
            Vector3 right = Vector3.Cross(Vector3.up, Tangent(p, si));
            Vector3 pos = p.pts[si] + right * (p.halfWidth + 1.5f);
            pos.y = terrain.SampleHeight(pos);
            Vector3 facing = pos - forkCenter;
            facing.y = 0f;
            MakeSign(titles[b], subs[b], pos, facing.normalized, wood, boardMat, parent);

            MakeArch("FINISH", p, p.pts.Length - 40, boardMat, parent);
        }

        var hard = allPaths[HardId];
        int half = hard.pts.Length / 2;
        int[] peaks = { ArgMaxHeight(hard, 30, half), ArgMaxHeight(hard, half, hard.pts.Length - 60) };
        for (int k = 0; k < peaks.Length; k++)
        {
            int i = peaks[k];
            Vector3 t = Tangent(hard, i), right = Vector3.Cross(Vector3.up, t);
            Vector3 pos = hard.pts[i] + right * (hard.halfWidth + 2f);
            pos.y = terrain.SampleHeight(pos);
            MakeSign("SUMMIT", $"{k + 1} of 2", pos, t, wood, stone, parent);
        }

        foreach (float deg in new[] { 50f, 90f, 130f, 170f })
        {
            float a = deg * Mathf.Deg2Rad;
            var dirOut = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var pos = new Vector3(FinishCenter.x, 0f, FinishCenter.y) + dirOut * (FinishRadius - 4f);
            pos.y = terrain.SampleHeight(pos);
            MakeBench(pos, -dirOut, wood, parent);
        }
    }

    static int ArgMaxHeight(TrackPath p, int from, int to)
    {
        int best = from;
        for (int i = from; i < to && i < p.pts.Length; i++) if (p.pts[i].y > p.pts[best].y) best = i;
        return best;
    }

    void MakeArch(string label, TrackPath p, int i, Material mat, Transform parent)
    {
        i = Mathf.Clamp(i, 0, p.pts.Length - 1);
        var root = new GameObject(label + "_" + p.name).transform;
        root.SetParent(parent);
        Vector3 pos = p.pts[i];
        pos.y = terrain.SampleHeight(pos);
        root.SetPositionAndRotation(pos, Quaternion.LookRotation(Tangent(p, i)));

        float w = p.halfWidth + 0.6f;
        Box(root, new Vector3(-w, 2f, 0f), new Vector3(0.35f, 5f, 0.35f), mat);
        Box(root, new Vector3(w, 2f, 0f), new Vector3(0.35f, 5f, 0.35f), mat);
        Box(root, new Vector3(0f, 4.3f, 0f), new Vector3(2f * w + 0.35f, 0.9f, 0.25f), mat);
        Label(label, root, new Vector3(0f, 4.3f, -0.14f), 6f);
    }

    void MakeSign(string title, string sub, Vector3 pos, Vector3 facing, Material wood, Material board, Transform parent)
    {
        var root = new GameObject("Sign_" + title).transform;
        root.SetParent(parent);
        root.SetPositionAndRotation(pos, Quaternion.LookRotation(facing));
        Box(root, new Vector3(0f, 1.2f, 0f), new Vector3(0.18f, 2.6f, 0.18f), wood);
        Box(root, new Vector3(0f, 2.5f, -0.1f), new Vector3(3f, 1.5f, 0.1f), board);
        Label($"{title}\n<size=50%>{sub}</size>", root, new Vector3(0f, 2.5f, -0.17f), 5f);
    }

    void MakeBench(Vector3 pos, Vector3 facing, Material wood, Transform parent)
    {
        var root = new GameObject("Bench").transform;
        root.SetParent(parent);
        root.SetPositionAndRotation(pos, Quaternion.LookRotation(facing));
        Box(root, new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.08f, 0.45f), wood);
        Box(root, new Vector3(0f, 0.8f, -0.2f), new Vector3(1.6f, 0.4f, 0.06f), wood);
        for (int sx = -1; sx <= 1; sx += 2)
        for (int sz = -1; sz <= 1; sz += 2)
            Box(root, new Vector3(sx * 0.7f, 0.22f, sz * 0.17f), new Vector3(0.08f, 0.45f, 0.08f), wood);
    }

    // ================================================================== lookouts, docks, fences

    void PlaceLookoutProps()
    {
        var parent = new GameObject("Lookouts").transform;
        var fences = new GameObject("Fences").transform;
        var wood = MakeMat("DockWood", new Color(0.52f, 0.38f, 0.24f));
        var easy = allPaths[EasyId];

        foreach (var lo in lookouts)
        {
            Vector3 dir = lo.dir, right = Vector3.Cross(Vector3.up, dir);
            Vector3 c = lo.center;
            c.y = terrain.SampleHeight(c);

            MakeBench(c + right * 2.6f - dir * 1f, dir, wood, parent);
            MakeBench(c - right * 2.6f - dir * 1f, dir, wood, parent);

            float deckY = lo.lake.level + 0.5f;
            Vector3 p0 = c + dir * 4f;
            float drop = p0.y - deckY;
            Vector3 p1 = p0 + dir * Mathf.Max(2f, drop * 3f);
            p1.y = deckY;
            if (drop > 0.2f)
                Prim(PrimitiveType.Cube, parent, (p0 + p1) * 0.5f, new Vector3(2.2f, 0.12f, Vector3.Distance(p0, p1)), wood,
                     Quaternion.LookRotation(p1 - p0));

            float deckLen = Mathf.Max(6f, Vector2.Distance(new Vector2(p1.x, p1.z), lo.lake.c) - lo.lake.r * 0.6f);
            Vector3 p2 = p1 + dir * deckLen;
            Prim(PrimitiveType.Cube, parent, (p1 + p2) * 0.5f, new Vector3(2.2f, 0.15f, deckLen), wood, Quaternion.LookRotation(dir));
            for (float d = 0f; d <= deckLen; d += 3f)
                for (int s = -1; s <= 1; s += 2)
                    Box(parent, p1 + dir * d + right * s * 1f + Vector3.down * 1.2f, new Vector3(0.15f, 2.5f, 0.15f), wood);

            BuildFence(easy, lo.index - 35, lo.index + 35, lo.index, dir, fences, wood);
        }
    }

    void BuildFence(TrackPath p, int from, int to, int skipCenter, Vector3 lakeDir, Transform parent, Material wood)
    {
        Vector3? prev = null;
        for (int i = from; i <= to; i += 3)
        {
            if (i < 0 || i >= p.pts.Length || Mathf.Abs(i - skipCenter) < 9) { prev = null; continue; }
            Vector3 right = Vector3.Cross(Vector3.up, Tangent(p, i));
            float side = Vector3.Dot(right, lakeDir) >= 0f ? 1f : -1f;
            Vector3 post = p.pts[i] + right * side * (p.halfWidth + 1.2f);
            post.y = terrain.SampleHeight(post);
            Box(parent, post + Vector3.up * 0.55f, new Vector3(0.12f, 1.1f, 0.12f), wood);
            if (prev.HasValue)
            {
                Rail(prev.Value, post, 0.5f, parent, wood);
                Rail(prev.Value, post, 0.95f, parent, wood);
            }
            prev = post;
        }
    }

    static void Rail(Vector3 a, Vector3 b, float h, Transform parent, Material wood)
    {
        Vector3 a2 = a + Vector3.up * h, b2 = b + Vector3.up * h;
        Prim(PrimitiveType.Cube, parent, (a2 + b2) * 0.5f, new Vector3(0.06f, 0.08f, Vector3.Distance(a2, b2)), wood,
             Quaternion.LookRotation(b2 - a2));
    }

    // ================================================================== lighting

    void SetupLighting(Vector3 spawnPos, Quaternion spawnRot)
    {
        var light = Object.FindFirstObjectByType<Light>();
        if (light)
        {
            light.transform.rotation = Quaternion.Euler(40f, -40f, 0f);
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.shadows = LightShadows.Soft;
        }

        var skyShader = Shader.Find("Skybox/Procedural");
        if (skyShader != null)
        {
            var sky = new Material(skyShader);
            sky.SetFloat("_SunSize", 0.04f);
            sky.SetFloat("_AtmosphereThickness", 1.1f);
            sky.SetColor("_SkyTint", new Color(0.55f, 0.62f, 0.75f));
            sky.SetColor("_GroundColor", new Color(0.30f, 0.36f, 0.30f));
            sky.SetFloat("_Exposure", 1.2f);
            AssetDatabase.CreateAsset(sky, GenFolder + "/Sky.mat");
            RenderSettings.skybox = sky;
            if (light) RenderSettings.sun = light;
        }

        // closer, green-grey fog makes the forest feel enclosed
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 60f;
        RenderSettings.fogEndDistance = 300f;
        RenderSettings.fogColor = new Color(0.58f, 0.66f, 0.62f);
        DynamicGI.UpdateEnvironment();

        var cam = Object.FindFirstObjectByType<Camera>();
        if (cam)
        {
            cam.transform.SetPositionAndRotation(spawnPos + Vector3.up * 1.6f, spawnRot);
            cam.farClipPlane = 400f;
        }
    }

    // ================================================================== primitives and labels

    static void Box(Transform parent, Vector3 localPos, Vector3 scale, Material mat) =>
        Prim(PrimitiveType.Cube, parent, localPos, scale, mat, Quaternion.identity);

    static void Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat, Quaternion localRot)
    {
        var go = GameObject.CreatePrimitive(type);
        DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        go.isStatic = true;
    }

    static void Label(string text, Transform parent, Vector3 localPos, float size)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = new Vector2(8f, 2f);
    }

    // ================================================================== asset helpers

    static void EnsureFolder(string path)
    {
        if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static Material MakeMat(string name, Color c, float smoothness = 0.1f)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh) { name = name, enableInstancing = true };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        AssetDatabase.CreateAsset(m, $"{GenFolder}/{name}.mat");
        return m;
    }

    static Mesh SaveMesh(Mesh mesh, string name)
    {
        AssetDatabase.CreateAsset(mesh, $"{GenFolder}/{name}.asset");
        return mesh;
    }

    static GameObject MakePrefab(string name, Mesh mesh, Material[] mats, bool trunkCollider)
    {
        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = mats;
        if (trunkCollider)
        {
            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1.5f, 0f);
            col.height = 3f;
            col.radius = 0.35f;
        }
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{GenFolder}/{name}.prefab");
        DestroyImmediate(go);
        return prefab;
    }

    static TerrainLayer MakeLayer(string name, Texture2D tex, float tile)
    {
        var tl = new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(tile, tile), smoothness = 0f, metallic = 0f };
        AssetDatabase.CreateAsset(tl, $"{GenFolder}/{name}.terrainlayer");
        return tl;
    }

    static Texture2D MakeGroundTexture(string name, Color a, Color b, int s)
    {
        const int T = 256;
        var tex = new Texture2D(T, T, TextureFormat.RGBA32, true);
        var px = new Color[T * T];
        for (int y = 0; y < T; y++)
        for (int x = 0; x < T; x++)
        {
            float n = TileNoise(x, y, T, 8, s) * 0.5f + TileNoise(x, y, T, 32, s + 7) * 0.3f + TileNoise(x, y, T, 128, s + 13) * 0.2f;
            px[y * T + x] = Color.Lerp(a, b, n);
        }
        tex.SetPixels(px);
        tex.Apply();
        string path = $"{GenFolder}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static float TileNoise(int x, int y, int size, int cells, int s)
    {
        float fx = x / (float)size * cells, fy = y / (float)size * cells;
        int x0 = (int)fx, y0 = (int)fy;
        float tx = fx - x0, ty = fy - y0;
        tx = tx * tx * (3f - 2f * tx);
        ty = ty * ty * (3f - 2f * ty);
        int x1 = (x0 + 1) % cells, y1 = (y0 + 1) % cells;
        x0 %= cells; y0 %= cells;
        float r0 = Mathf.Lerp(Hash(x0, y0, s), Hash(x1, y0, s), tx);
        float r1 = Mathf.Lerp(Hash(x0, y1, s), Hash(x1, y1, s), tx);
        return Mathf.Lerp(r0, r1, ty);
    }

    static float Hash(int x, int y, int s)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + s * 982451653;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x7fffffff) / (float)int.MaxValue;
        }
    }

    // ================================================================== low-poly mesh builder

    class MeshBuilder
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<int>[] subs;

        public MeshBuilder(int subCount)
        {
            subs = new List<int>[subCount];
            for (int i = 0; i < subCount; i++) subs[i] = new List<int>();
        }

        static Vector3 Dir(float ang) => new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));

        public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) { var t = b; b = c; c = t; }
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            subs[sub].Add(i); subs[sub].Add(i + 1); subs[sub].Add(i + 2);
        }

        public void Prism(int sub, Vector3 bottom, float height, float radius, int sides)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = bottom + Dir(a0) * radius, p1 = bottom + Dir(a1) * radius;
                Vector3 q0 = p0 + Vector3.up * height, q1 = p1 + Vector3.up * height;
                Vector3 o = Dir((a0 + a1) * 0.5f);
                Tri(sub, p0, q0, q1, o);
                Tri(sub, p0, q1, p1, o);
            }
        }

        public void Cone(int sub, Vector3 bottom, float height, float radius, int sides)
        {
            Vector3 apex = bottom + Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = bottom + Dir(a0) * radius, p1 = bottom + Dir(a1) * radius;
                Tri(sub, p0, apex, p1, Dir((a0 + a1) * 0.5f) + Vector3.up * 0.5f);
                Tri(sub, bottom, p1, p0, Vector3.down);
            }
        }

        public void Disc(int sub, Vector3 center, float radius, int sides)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Tri(sub, center, center + Dir(a0) * radius, center + Dir(a1) * radius, Vector3.up);
            }
        }

        public void Ico(int sub, Vector3 center, float radius, float squashY, float jitter, System.Random rng)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < v.Length; i++)
            {
                float k = radius * (1f + ((float)rng.NextDouble() * 2f - 1f) * jitter);
                var p = v[i].normalized * k;
                p.y *= squashY;
                v[i] = center + p;
            }
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            for (int i = 0; i < f.Length; i += 3)
            {
                Vector3 a = v[f[i]], b = v[f[i + 1]], c = v[f[i + 2]];
                Tri(sub, a, b, c, (a + b + c) / 3f - center);
            }
        }

        public Mesh ToMesh()
        {
            var m = new Mesh();
            m.SetVertices(verts);
            m.subMeshCount = subs.Length;
            for (int i = 0; i < subs.Length; i++) m.SetTriangles(subs[i], i);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
