using System.Collections.Generic;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using Newtonsoft.Json.Linq;

public class ReproductorHistorial : MonoBehaviour
{
    public GameObject puntoPrefab;
    public float velocidad = 0.05f;
    public bool reproduciendo = false;

    private List<GameObject> puntos = new List<GameObject>();
    private List<List<int>> historial = new List<List<int>>();
    private List<float> losses = new List<float>();
    private List<float> accuracies = new List<float>();
    private Color[] colores;
    private int epocaActual = 0;
    private int totalEpocas = 0;
    private bool cargado = false;
    private GUIStyle estiloHUD;

    void Start()
    {
        CargarHistorial();
        reproduciendo = true;
    }

    void OnDestroy()
    {
        Visualizador vis = FindAnyObjectByType<Visualizador>();
        if (vis != null)
        {
            vis.enabled = true;
            vis.RecargarTodo();
        }
    }

    public void CargarHistorial()
    {
        string path = Application.dataPath + "/historial_animacion.json";
        if (!File.Exists(path))
        {
            Debug.LogWarning("No existe historial_animacion.json — entrena primero.");
            return;
        }

        JObject data = JObject.Parse(File.ReadAllText(path));

        int numClases = data["num_clases"].Value<int>();
        totalEpocas = data["num_epocas"].Value<int>();

        Color[] paleta = {
            new Color(0.9f,  0.15f, 0.15f),
            new Color(0.15f, 0.85f, 0.15f),
            new Color(0.15f, 0.45f, 0.95f),
            new Color(0.95f, 0.85f, 0.10f),
            new Color(0.85f, 0.15f, 0.85f),
            new Color(0.10f, 0.85f, 0.85f),
        };
        colores = new Color[numClases];
        for (int i = 0; i < numClases; i++)
            colores[i] = paleta[i % paleta.Length];

        // Destruir puntos viejos
        foreach (var p in puntos) if (p != null) DestroyImmediate(p);
        puntos.Clear();

        // Destruir puntos del Visualizador para evitar duplicados
        Visualizador vis = FindAnyObjectByType<Visualizador>();
        if (vis != null) vis.enabled = false;

        // Crear puntos
        foreach (JObject punto in data["puntos"])
        {
            float r = punto["r"].Value<float>();
            float g = punto["g"].Value<float>();
            float b = punto["b"].Value<float>();

            GameObject go = Instantiate(puntoPrefab,
                new Vector3(r, g, b) * 5f, Quaternion.identity);
            go.transform.localScale = Vector3.one * 0.15f;
            puntos.Add(go);
        }

        // Historial de predicciones
        historial.Clear();
        losses.Clear();
        accuracies.Clear();

        foreach (JArray epoca in data["predicciones"])
            historial.Add(epoca.ToObject<List<int>>());

        foreach (var v in data["loss"]) losses.Add(v.Value<float>());
        foreach (var v in data["accuracy"]) accuracies.Add(v.Value<float>());

        epocaActual = 0;
        cargado = true;

        Debug.Log($"Historial cargado: {totalEpocas} épocas, {puntos.Count} puntos");


        CrearEjesIfNeeded();

        // Mostrar época 0
        AplicarEpoca(0);

        DecisionBoundaryNN frontera = FindAnyObjectByType<DecisionBoundaryNN>();
        if (frontera != null) frontera.Recargar();
    }

    void CrearEjesIfNeeded()
    {
        // Solo crear si no hay ejes ya en escena
        var lineRenderers = FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var lr in lineRenderers)
            if (lr.gameObject.name == "Eje") return;

        CrearEje(Vector3.zero, Vector3.right * 12f, Color.red);
        CrearEje(Vector3.zero, Vector3.up * 12f, Color.green);
        CrearEje(Vector3.zero, Vector3.forward * 12f, Color.blue);
    }

    void CrearEje(Vector3 a, Vector3 b, Color c)
    {
        GameObject obj = new GameObject("Eje");
        LineRenderer lr = obj.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = c;
        lr.endColor = c;
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
    }

    void AplicarEpoca(int epoca)
    {
        if (!cargado || epoca >= historial.Count) return;

        var preds = historial[epoca];
        for (int i = 0; i < puntos.Count && i < preds.Count; i++)
        {
            int clase = preds[i] - 1;
            Color c = (clase >= 0 && clase < colores.Length)
                ? colores[clase] : Color.black;
            puntos[i].GetComponent<Renderer>().material.color = c;
        }
        epocaActual = epoca;
    }

    void Update()
    {
        if (!cargado) return;
        string listoPath = Application.dataPath + "/listo.txt";
        if (File.Exists(listoPath))
        {
            File.Delete(listoPath);
            CargarHistorial();
            reproduciendo = true;
            return;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            reproduciendo = !reproduciendo;

        if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
            AplicarEpoca(Mathf.Min(epocaActual + 1, totalEpocas - 1));

        if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
            AplicarEpoca(Mathf.Max(epocaActual - 1, 0));

        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            AplicarEpoca(0);
            reproduciendo = true;
        }
    }

    void OnEnable()
    {
        StartCoroutine(Reproducir());
    }

    IEnumerator Reproducir()
    {
        while (true)
        {
            if (reproduciendo && cargado)
            {
                if (epocaActual < totalEpocas - 1)
                    AplicarEpoca(epocaActual + 1);
                else
                    reproduciendo = false;
            }
            yield return new WaitForSeconds(velocidad);
        }
    }

    void OnGUI()
    {
        if (!cargado) return;

        if (estiloHUD == null)
        {
            estiloHUD = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.UpperLeft
            };
            estiloHUD.normal.textColor = Color.white;
        }
        float acc = accuracies.Count > epocaActual ? accuracies[epocaActual] : 0f;
        float loss = losses.Count > epocaActual ? losses[epocaActual] : 0f;

        GUI.color = new Color(0, 0, 0, 0.6f);
        GUI.Box(new Rect(10, 10, 250, 120), "");
        GUI.color = Color.white;

        string estado = reproduciendo ? "▶ REPRODUCIENDO" : "⏸ PAUSADO";
        string texto = $"{estado}\n" +
                        $"Época:    {epocaActual + 1} / {totalEpocas}\n" +
                        $"Loss:     {loss:F4}\n" +
                        $"Accuracy: {acc:P1}\n\n" +
                        $"[Space] Play/Pausa  [R] Reiniciar\n" +
                        $"[← →] Época anterior/siguiente";

        GUI.Label(new Rect(15, 15, 240, 115), texto, estiloHUD);

        // Barra de progreso
        float progreso = totalEpocas > 0 ? (float)epocaActual / totalEpocas : 0f;
        GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        GUI.Box(new Rect(10, 138, 250, 12), "");
        GUI.color = new Color(0.3f, 0.75f, 0.4f, 0.9f);
        GUI.Box(new Rect(10, 138, 250 * progreso, 12), "");
        GUI.color = Color.white;
    }
}