using System.Collections;
using UnityEngine;

// Panel de la demo web: semilla, regenerar y cambio de modo de cámara.
// Se usa IMGUI para no depender de Canvas/EventSystem en la escena.
public class DungeonDemoUI : MonoBehaviour
{
    [SerializeField]
    Generator2D generator;
    [SerializeField]
    DungeonDemoCamera demoCamera;
    [SerializeField]
    DungeonAlgorithmView algorithmView;
    [SerializeField]
    string title = "Dungeon Generator";

    const int PanelWidth = 300;

    static Rect panelRect;
    static float uiScale = 1f;

    // true mientras el campo de semilla tiene el foco (para no mover la cámara al escribir)
    public static bool IsTyping { get; private set; }

    string seedText;
    bool generating;
    bool showHelp = true;
    float copiedUntil;
    GUIStyle wrappedLabel;

    // position en coordenadas de pantalla de Input (origen abajo a la izquierda)
    public static bool IsPointerOverUI(Vector2 position)
    {
        Vector2 guiPosition = new Vector2(position.x, Screen.height - position.y) / uiScale;
        return panelRect.Contains(guiPosition);
    }

    void Awake()
    {
        if (generator == null)
            generator = FindObjectOfType<Generator2D>();
        if (demoCamera == null)
            demoCamera = FindObjectOfType<DungeonDemoCamera>();
        if (algorithmView == null)
            algorithmView = FindObjectOfType<DungeonAlgorithmView>();

        // ?seed=1234 en la URL: se aplica en Awake, antes de que Generator2D genere en Start
        if (generator != null && DungeonDemoWeb.TryGetSeedFromUrl(out int urlSeed))
            generator.Seed = urlSeed;
    }

    void Start()
    {
        if (generator != null)
        {
            seedText = generator.Seed.ToString();
            DungeonDemoWeb.UpdateUrl(generator.Seed);
        }
    }

    void Update()
    {
        if (generator == null || generating || IsTyping)
            return;

        if (Input.GetKeyDown(KeyCode.R))
            GenerateRandom();

        bool viewingAlgorithm = algorithmView != null && algorithmView.IsActive;

        if (Input.GetKeyDown(KeyCode.Tab) && demoCamera != null && !viewingAlgorithm)
            demoCamera.ToggleMode();

        if (algorithmView != null)
        {
            if (Input.GetKeyDown(KeyCode.V))
            {
                if (viewingAlgorithm)
                    algorithmView.Hide();
                else
                    algorithmView.Show(DungeonAlgorithmView.Stage.Rooms);
            }
            else if (viewingAlgorithm && Input.GetKeyDown(KeyCode.Space))
                algorithmView.Next();
            else if (viewingAlgorithm && Input.GetKeyDown(KeyCode.Backspace))
                algorithmView.Previous();
        }

        if (Input.GetKeyDown(KeyCode.H))
            showHelp = !showHelp;
    }

    void GenerateRandom()
    {
        int seed = Random.Range(0, 1000000);
        seedText = seed.ToString();
        StartCoroutine(Regenerate(seed));
    }

    IEnumerator Regenerate(int seed)
    {
        generating = true;

        // Un frame para que se vea "Generando..." antes del parón
        yield return null;

        generator.ClearMap();

        // Destroy es diferido: esperar a que el mapa antiguo desaparezca antes del bake del NavMesh
        yield return null;

        // Liberar las mallas combinadas por el static batching del mapa anterior
        yield return Resources.UnloadUnusedAssets();

        generator.Generate(seed);
        DungeonDemoWeb.UpdateUrl(seed);

        if (demoCamera != null)
        {
            demoCamera.ResetView();
            if (algorithmView != null && algorithmView.IsActive)
                demoCamera.LookFromAbove();
        }

        generating = false;
    }

    void OnGUI()
    {
        if (generator == null)
            return;

        uiScale = Mathf.Clamp(Screen.height / 720f, 1f, 2f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));

        bool viewingAlgorithm = algorithmView != null && algorithmView.IsActive;

        float height = 205f;
        if (viewingAlgorithm)
            height += 160f;
        else if (showHelp)
            height += 100f;

        panelRect = new Rect(10f, 10f, PanelWidth, height);

        GUILayout.BeginArea(panelRect, GUI.skin.box);
        GUILayout.Label(title, GUI.skin.box);

        GUI.enabled = !generating;

        GUILayout.BeginHorizontal();
        GUILayout.Label("Semilla", GUILayout.Width(55f));
        GUI.SetNextControlName("SeedField");
        seedText = GUILayout.TextField(seedText ?? "", 9);
        GUILayout.EndHorizontal();

        bool submit = Event.current.type == EventType.KeyDown
            && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
            && GUI.GetNameOfFocusedControl() == "SeedField";

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Generar") || submit)
        {
            if (int.TryParse(seedText, out int seed))
                StartCoroutine(Regenerate(seed));
            GUI.FocusControl(null);
        }
        if (GUILayout.Button("Aleatoria (R)"))
        {
            GenerateRandom();
            GUI.FocusControl(null);
        }
        GUILayout.EndHorizontal();

        if (demoCamera != null)
        {
            bool explore = demoCamera.CurrentMode == DungeonDemoCamera.Mode.Explore;
            GUI.enabled = !generating && !viewingAlgorithm;
            if (GUILayout.Button(explore ? "Vista general (Tab)" : "Explorar (Tab)"))
            {
                demoCamera.ToggleMode();
                GUI.FocusControl(null);
            }
            GUI.enabled = !generating;
        }

        if (algorithmView != null)
        {
            if (GUILayout.Button(viewingAlgorithm ? "Ver mazmorra (V)" : "Ver algoritmo paso a paso (V)"))
            {
                if (viewingAlgorithm)
                    algorithmView.Hide();
                else
                    algorithmView.Show(DungeonAlgorithmView.Stage.Rooms);
                GUI.FocusControl(null);
            }
        }

        if (GUILayout.Button(Time.unscaledTime < copiedUntil ? "¡Enlace copiado!" : "Copiar enlace"))
        {
            DungeonDemoWeb.CopyToClipboard(DungeonDemoWeb.GetShareUrl(generator.Seed));
            copiedUntil = Time.unscaledTime + 2f;
            GUI.FocusControl(null);
        }

        GUI.enabled = true;

        if (generating)
        {
            GUILayout.Label("Generando...");
        }
        else if (viewingAlgorithm)
        {
            DungeonAlgorithmView.Stage stage = algorithmView.CurrentStage;
            GUILayout.Label(DungeonAlgorithmView.StageTitle(stage), GUI.skin.box);

            if (wrappedLabel == null)
                wrappedLabel = new GUIStyle(GUI.skin.label) { wordWrap = true };
            GUILayout.Label(DungeonAlgorithmView.StageDescription(stage), wrappedLabel);

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUI.enabled = stage > DungeonAlgorithmView.Stage.Rooms;
            if (GUILayout.Button("< Anterior"))
                algorithmView.Previous();
            GUI.enabled = true;
            if (GUILayout.Button(stage == DungeonAlgorithmView.Stage.Hallways ? "Resultado >" : "Siguiente >"))
                algorithmView.Next();
            GUILayout.EndHorizontal();
            GUILayout.Label("Espacio / Retroceso: avanzar / volver");
        }
        else if (showHelp)
        {
            bool explore = demoCamera != null && demoCamera.CurrentMode == DungeonDemoCamera.Mode.Explore;
            GUILayout.Label(explore
                ? "WASD / flechas: andar\nShift: correr\nArrastrar: mirar"
                : "Arrastrar: orbitar\nWASD / botón central: desplazar\nRueda / pellizco: zoom");
            GUILayout.Label("V: algoritmo paso a paso   H: ocultar ayuda");
        }

        GUILayout.EndArea();

        IsTyping = GUI.GetNameOfFocusedControl() == "SeedField";
    }
}
