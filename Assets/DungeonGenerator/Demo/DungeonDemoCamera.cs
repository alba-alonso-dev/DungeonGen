using UnityEngine;
using UnityEngine.AI;

// Cámara para la demo web:
//  - Vista general: orbitar (arrastrar), desplazar (WASD / botón central), zoom (rueda / pellizco).
//  - Explorar: primera persona sobre el NavMesh (WASD + arrastrar para mirar).
// Los prefabs del mapa no tienen colliders, así que el NavMesh hace de "suelo y paredes".
[RequireComponent(typeof(Camera))]
public class DungeonDemoCamera : MonoBehaviour
{
    public enum Mode
    {
        Overview,
        Explore
    }

    [SerializeField]
    Generator2D generator;

    [Header("Vista general")]
    [SerializeField]
    float orbitSpeed = 4f;
    [SerializeField]
    float panSpeed = 1f;
    [SerializeField]
    float zoomSpeed = 0.15f;
    [SerializeField]
    float minPitch = 15f;
    [SerializeField]
    float maxPitch = 89f;

    [Header("Explorar")]
    [SerializeField]
    float walkSpeed = 5f;
    [SerializeField]
    float runMultiplier = 2f;
    [SerializeField]
    float lookSpeed = 3f;
    [SerializeField]
    float eyeHeight = 1.7f;
    [SerializeField]
    float torchRange = 12f;

    [Header("Iluminación")]
    [SerializeField]
    bool createLightIfMissing = true;

    public Mode CurrentMode { get; private set; } = Mode.Overview;

    // Orbit
    Vector3 pivot;
    float distance;
    float minDistance;
    float maxDistance;
    float yaw;
    float pitch = 60f;

    // Explore
    Vector3 feetPosition;
    float exploreYaw;
    float explorePitch;
    Light torch;

    bool dragging;
    float lastPinchDistance;

    void Awake()
    {
        if (generator == null)
            generator = FindObjectOfType<Generator2D>();

        bool sceneHasLight = FindObjectOfType<Light>() != null;

        // Luz de "antorcha" que acompaña a la cámara al explorar
        GameObject torchObject = new GameObject("Torch");
        torchObject.transform.SetParent(transform, false);
        torch = torchObject.AddComponent<Light>();
        torch.type = LightType.Point;
        torch.range = torchRange;
        torch.intensity = 1.5f;
        torch.color = new Color(1f, 0.85f, 0.6f);
        torch.shadows = LightShadows.None;
        torch.enabled = false;

        if (createLightIfMissing && !sceneHasLight)
        {
            GameObject sun = new GameObject("Directional Light");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.8f;
            light.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }

    void Start()
    {
        ResetView();
    }

    // Recoloca la cámara para el mapa actual (llamar tras generar un mapa nuevo)
    public void ResetView()
    {
        if (generator == null)
            return;

        Vector3 mapSize = generator.MapSize;
        float extent = Mathf.Max(mapSize.x, mapSize.z);

        pivot = generator.MapCenter;
        minDistance = extent * 0.1f;
        maxDistance = extent * 2.5f;
        distance = extent * 1.1f;
        yaw = 0f;
        pitch = 60f;

        if (CurrentMode == Mode.Explore && EnterExplore())
            return;

        SetMode(Mode.Overview);
    }

    public void SetMode(Mode mode)
    {
        if (mode == Mode.Explore && !EnterExplore())
            return;

        CurrentMode = mode;
        torch.enabled = mode == Mode.Explore;

        if (mode == Mode.Overview)
            ApplyOrbit();
    }

    public void ToggleMode()
    {
        SetMode(CurrentMode == Mode.Overview ? Mode.Explore : Mode.Overview);
    }

    bool EnterExplore()
    {
        if (generator == null || !generator.IsGenerated)
            return false;

        if (!NavMesh.SamplePosition(generator.SpawnPosition, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            Debug.LogWarning("No NavMesh near spawn position, cannot explore.");
            return false;
        }

        feetPosition = hit.position;
        exploreYaw = 0f;
        explorePitch = 0f;
        ApplyExplore();
        return true;
    }

    void Update()
    {
        if (generator == null || DungeonDemoUI.IsTyping)
            return;

        UpdateDragState();

        if (CurrentMode == Mode.Overview)
            UpdateOverview();
        else
            UpdateExplore();
    }

    void UpdateDragState()
    {
        // Solo se arrastra si el clic empieza fuera del panel de UI
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
            dragging = !DungeonDemoUI.IsPointerOverUI(Input.mousePosition);

        if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1) && !Input.GetMouseButton(2))
            dragging = false;
    }

    Vector2 LookDelta()
    {
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Moved && !DungeonDemoUI.IsPointerOverUI(touch.position))
                return touch.deltaPosition * 0.1f;
            return Vector2.zero;
        }

        if (dragging && (Input.GetMouseButton(0) || Input.GetMouseButton(1)))
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

        return Vector2.zero;
    }

    Vector2 MoveInput()
    {
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    }

    void UpdateOverview()
    {
        Vector2 look = LookDelta();
        yaw += look.x * orbitSpeed;
        pitch = Mathf.Clamp(pitch - look.y * orbitSpeed, minPitch, maxPitch);

        // Desplazamiento en el plano horizontal relativo a la orientación de la cámara
        Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
        Vector2 move = MoveInput();
        Vector3 pan = yawRotation * new Vector3(move.x, 0f, move.y) * (panSpeed * distance * Time.deltaTime);

        if (dragging && Input.GetMouseButton(2))
            pan -= yawRotation * new Vector3(Input.GetAxis("Mouse X"), 0f, Input.GetAxis("Mouse Y")) * (panSpeed * distance * 0.05f);

        Vector3 mapSize = generator.MapSize;
        Vector3 center = generator.MapCenter;
        pivot += pan;
        pivot.x = Mathf.Clamp(pivot.x, center.x - mapSize.x, center.x + mapSize.x);
        pivot.z = Mathf.Clamp(pivot.z, center.z - mapSize.z, center.z + mapSize.z);

        // Zoom: rueda del ratón o pellizco
        float zoom = 0f;
        if (!DungeonDemoUI.IsPointerOverUI(Input.mousePosition))
            zoom = Mathf.Clamp(Input.mouseScrollDelta.y, -3f, 3f);

        if (Input.touchCount == 2)
        {
            float pinch = Vector2.Distance(Input.GetTouch(0).position, Input.GetTouch(1).position);
            if (Input.GetTouch(1).phase != TouchPhase.Began && lastPinchDistance > 0f)
                zoom = (pinch - lastPinchDistance) * 0.02f;
            lastPinchDistance = pinch;
        }
        else
        {
            lastPinchDistance = 0f;
        }

        distance = Mathf.Clamp(distance * (1f - zoom * zoomSpeed), minDistance, maxDistance);

        ApplyOrbit();
    }

    void ApplyOrbit()
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.rotation = rotation;
        transform.position = pivot - rotation * Vector3.forward * distance;
    }

    void UpdateExplore()
    {
        Vector2 look = LookDelta();
        exploreYaw += look.x * lookSpeed;
        explorePitch = Mathf.Clamp(explorePitch - look.y * lookSpeed, -80f, 80f);

        Vector2 move = MoveInput();
        if (move.sqrMagnitude > 1f)
            move.Normalize();

        float speed = walkSpeed * (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? runMultiplier : 1f);
        Vector3 delta = Quaternion.Euler(0f, exploreYaw, 0f) * new Vector3(move.x, 0f, move.y) * (speed * Time.deltaTime);

        if (delta.sqrMagnitude > 0f)
        {
            // Intentar el movimiento completo y, si choca, deslizar por cada eje
            if (!TryMove(delta) && !TryMove(new Vector3(delta.x, 0f, 0f)))
                TryMove(new Vector3(0f, 0f, delta.z));
        }

        ApplyExplore();
    }

    bool TryMove(Vector3 delta)
    {
        if (delta.sqrMagnitude < 1e-8f)
            return false;

        Vector3 target = feetPosition + delta;
        if (NavMesh.Raycast(feetPosition, target, out NavMeshHit _, NavMesh.AllAreas))
            return false;

        if (!NavMesh.SamplePosition(target, out NavMeshHit hit, 1f, NavMesh.AllAreas))
            return false;

        feetPosition = hit.position;
        return true;
    }

    void ApplyExplore()
    {
        transform.rotation = Quaternion.Euler(explorePitch, exploreYaw, 0f);
        transform.position = feetPosition + Vector3.up * eyeHeight;
    }
}
