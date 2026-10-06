using System.Collections.Generic;
using UnityEngine;

// Visualiza paso a paso cómo Generator2D construye la mazmorra:
// salas -> triangulación de Delaunay -> árbol de expansión mínima -> ciclos extra -> pasillos A*.
// Todo se dibuja en una única malla plana con colores por vértice (un draw call),
// reconstruida cada frame mientras se anima la aparición de los elementos del paso actual.
public class DungeonAlgorithmView : MonoBehaviour
{
    public enum Stage
    {
        None,
        Rooms,
        Delaunay,
        MinimumSpanningTree,
        ExtraEdges,
        Hallways
    }

    public const int StageCount = 5;

    [SerializeField]
    Generator2D generator;
    [SerializeField]
    DungeonDemoCamera demoCamera;
    [SerializeField]
    [Tooltip("Segundos que tarda en aparecer todo lo nuevo de cada paso")]
    float revealDuration = 1.5f;

    static readonly Color BackgroundColor = new Color(0.08f, 0.09f, 0.12f, 1f);
    static readonly Color RoomColor = new Color(0.8f, 0.32f, 0.3f, 0.9f);
    static readonly Color SpawnRoomColor = new Color(0.6f, 0.35f, 0.85f, 0.95f);
    static readonly Color NodeColor = new Color(1f, 1f, 1f, 1f);
    static readonly Color DelaunayColor = new Color(0.85f, 0.85f, 0.9f, 0.9f);
    static readonly Color MstColor = new Color(0.45f, 0.9f, 0.35f, 1f);
    static readonly Color ExtraColor = new Color(0.3f, 0.75f, 1f, 1f);
    static readonly Color HallwayColor = new Color(0.95f, 0.65f, 0.2f, 0.95f);
    static readonly Color DoorColor = new Color(0.35f, 0.95f, 0.5f, 1f);

    public Stage CurrentStage { get; private set; } = Stage.None;
    public bool IsActive { get => CurrentStage != Stage.None; }

    Mesh mesh;
    MeshRenderer meshRenderer;
    Generator2D.GenerationTrace shownTrace;
    float stageStartTime;

    readonly List<Vector3> vertices = new List<Vector3>();
    readonly List<Color> colors = new List<Color>();
    readonly List<int> triangles = new List<int>();

    public static string StageTitle(Stage stage)
    {
        switch (stage)
        {
            case Stage.Rooms: return "1/5  Salas";
            case Stage.Delaunay: return "2/5  Triangulación de Delaunay";
            case Stage.MinimumSpanningTree: return "3/5  Árbol de expansión mínima";
            case Stage.ExtraEdges: return "4/5  Ciclos extra";
            case Stage.Hallways: return "5/5  Pasillos con A*";
            default: return "";
        }
    }

    public static string StageDescription(Stage stage)
    {
        switch (stage)
        {
            case Stage.Rooms:
                return "Se colocan salas de tamaño aleatorio en posiciones aleatorias y se descartan las que se solapan. La morada es la sala de inicio.";
            case Stage.Delaunay:
                return "Se unen los centros de las salas en triángulos sin aristas cruzadas. Son todas las conexiones razonables entre salas vecinas.";
            case Stage.MinimumSpanningTree:
                return "Con el algoritmo de Prim se eligen las aristas más cortas que conectan todas las salas sin ciclos. Así todo es alcanzable.";
            case Stage.ExtraEdges:
                return "Se recupera un 12,5 % de las aristas descartadas (en azul) para crear rutas alternativas y que no sea un camino lineal.";
            case Stage.Hallways:
                return "Cada arista se convierte en un pasillo con A* sobre el grid. Pasar por pasillos existentes cuesta menos, así que tienden a compartirse. En verde, las puertas.";
            default:
                return "";
        }
    }

    void Awake()
    {
        if (generator == null)
            generator = FindObjectOfType<Generator2D>();
        if (demoCamera == null)
            demoCamera = FindObjectOfType<DungeonDemoCamera>();

        // En la raíz de la escena: Generator2D.ClearMap() borra los hijos del generador
        GameObject viewObject = new GameObject("Algorithm View");

        mesh = new Mesh();
        mesh.name = "Algorithm View";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.MarkDynamic();
        viewObject.AddComponent<MeshFilter>().sharedMesh = mesh;

        meshRenderer = viewObject.AddComponent<MeshRenderer>();
        // Sprites/Default: sin iluminación, usa el color de vértice y está en "Always Included Shaders"
        meshRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.enabled = false;
    }

    public void Show(Stage stage)
    {
        if (generator == null || generator.Trace == null)
            return;

        if (stage == Stage.None)
        {
            Hide();
            return;
        }

        if (!IsActive && demoCamera != null)
        {
            demoCamera.SetMode(DungeonDemoCamera.Mode.Overview);
            demoCamera.LookFromAbove();
        }

        CurrentStage = stage;
        stageStartTime = Time.unscaledTime;
        shownTrace = generator.Trace;
        generator.SetMapVisible(false);
        meshRenderer.enabled = true;
    }

    public void Hide()
    {
        CurrentStage = Stage.None;
        meshRenderer.enabled = false;
        if (generator != null)
            generator.SetMapVisible(true);
    }

    // Después del último paso se vuelve al mapa final
    public void Next()
    {
        if (CurrentStage == Stage.Hallways)
            Hide();
        else
            Show(CurrentStage + 1);
    }

    public void Previous()
    {
        if (CurrentStage > Stage.Rooms)
            Show(CurrentStage - 1);
    }

    void LateUpdate()
    {
        if (!IsActive)
            return;

        // Se ha generado un mapa nuevo mientras se visualizaba: ocultarlo y repetir el paso
        if (generator.Trace != shownTrace)
            Show(CurrentStage);

        float t = revealDuration > 0f ? Mathf.Clamp01((Time.unscaledTime - stageStartTime) / revealDuration) : 1f;
        Build(shownTrace, CurrentStage, t);
        Apply();
    }

    void Build(Generator2D.GenerationTrace trace, Stage stage, float t)
    {
        vertices.Clear();
        colors.Clear();
        triangles.Clear();

        Vector2Int size = generator.GridSize;
        AddRect(new Vector2(-0.5f, -0.5f), new Vector2(size.x - 0.5f, size.y - 0.5f), BackgroundColor);

        // Pasillos (debajo de las salas)
        if (stage >= Stage.Hallways)
        {
            int paths = Reveal(trace.HallwayPaths.Count, stage == Stage.Hallways, t);
            for (int i = 0; i < paths; i++)
            {
                foreach (Vector2Int cell in trace.HallwayPaths[i])
                    AddCell(cell, 0.42f, HallwayColor);
            }
        }

        // Salas
        int rooms = Reveal(trace.Rooms.Count, stage == Stage.Rooms, t);
        for (int i = 0; i < rooms; i++)
        {
            RectInt room = trace.Rooms[i];
            Color color = room.Equals(trace.SpawnRoom) ? SpawnRoomColor : RoomColor;
            AddRect(new Vector2(room.xMin - 0.5f, room.yMin - 0.5f), new Vector2(room.xMax - 0.5f, room.yMax - 0.5f), color);
        }

        if (stage >= Stage.Hallways)
        {
            int doors = Reveal(trace.Doors.Count, stage == Stage.Hallways, t);
            for (int i = 0; i < doors; i++)
                AddCell(trace.Doors[i], 0.3f, DoorColor);
        }

        if (stage < Stage.Delaunay)
            return;

        // Grafo: Delaunay se atenúa cuando ya se ha elegido el árbol
        Color delaunayColor = stage == Stage.Delaunay ? DelaunayColor : Faded(DelaunayColor, 0.15f);
        AddEdges(trace.DelaunayEdges, Reveal(trace.DelaunayEdges.Count, stage == Stage.Delaunay, t), 0.12f, delaunayColor);

        float graphAlpha = stage == Stage.Hallways ? 0.35f : 1f;

        if (stage >= Stage.MinimumSpanningTree)
            AddEdges(trace.MstEdges, Reveal(trace.MstEdges.Count, stage == Stage.MinimumSpanningTree, t), 0.25f, Faded(MstColor, graphAlpha));

        if (stage >= Stage.ExtraEdges)
            AddEdges(trace.ExtraEdges, Reveal(trace.ExtraEdges.Count, stage == Stage.ExtraEdges, t), 0.25f, Faded(ExtraColor, graphAlpha));

        foreach (RectInt room in trace.Rooms)
        {
            Vector2 center = (Vector2)room.position + (Vector2)room.size / 2f - new Vector2(0.5f, 0.5f);
            AddCell(center, 0.3f, Faded(NodeColor, graphAlpha));
        }
    }

    // Cuántos elementos mostrar: todos si el paso ya pasó, una parte si es el paso actual
    static int Reveal(int count, bool isCurrentStage, float t)
    {
        return isCurrentStage ? Mathf.CeilToInt(count * t) : count;
    }

    static Color Faded(Color color, float alpha)
    {
        color.a *= alpha;
        return color;
    }

    void AddEdges(List<Vector2[]> edges, int count, float width, Color color)
    {
        for (int i = 0; i < count; i++)
            AddLine(edges[i][0], edges[i][1], width, color);
    }

    void AddCell(Vector2 center, float halfSize, Color color)
    {
        Vector2 half = new Vector2(halfSize, halfSize);
        AddRect(center - half, center + half, color);
    }

    void AddRect(Vector2 min, Vector2 max, Color color)
    {
        AddQuad(min, new Vector2(min.x, max.y), max, new Vector2(max.x, min.y), color);
    }

    void AddLine(Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 direction = b - a;
        if (direction.sqrMagnitude < 1e-6f)
            return;

        Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
        AddQuad(a - normal, a + normal, b + normal, b - normal, color);
    }

    void AddQuad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int start = vertices.Count;
        vertices.Add(generator.GridToWorld(a, 0.02f));
        vertices.Add(generator.GridToWorld(b, 0.02f));
        vertices.Add(generator.GridToWorld(c, 0.02f));
        vertices.Add(generator.GridToWorld(d, 0.02f));
        colors.Add(color);
        colors.Add(color);
        colors.Add(color);
        colors.Add(color);

        // Cull Off en Sprites/Default, así que el sentido de los triángulos da igual.
        // Sin ZWrite: se pintan en el orden en que se añaden.
        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
        triangles.Add(start);
        triangles.Add(start + 2);
        triangles.Add(start + 3);
    }

    void Apply()
    {
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
    }
}
