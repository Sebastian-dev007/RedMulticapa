using System.Collections.Generic;
using System.IO;
using System.Globalization;
using UnityEngine;
using System.Collections;

public class Visualizador : MonoBehaviour
{
    public GameObject puntoPrefab;
    private List<GameObject> puntos = new List<GameObject>();
    private List<int> clasesReales = new List<int>();
    private Color[] coloresClase;
    private int numClases = 3;
    private RedNeuronalUnity red;
    private bool esperandoRecarga = false;
    private bool recargando = false;
    private int epocaActual = 0;
    private int epocasTotal = 0;
    private float lossActual = 0f;
    private float accActual = 0f;
    private bool entrenandoAhora = false;
    private GUIStyle estiloHUD;
    public bool mostrarHUD = true;

    void Start()
    {
        if (FindAnyObjectByType<ReproductorHistorial>() != null)
        {
            Debug.Log("ReproductorHistorial activo — Visualizador en espera.");
            return;
        }

        CargarModelo();
        GenerarColores();
        CargarDatos();
        ClasificarConRed();
        CrearEjes();
    }

    void GenerarColores()
    {
        Color[] paleta = new Color[]
        {
            new Color(0.9f,  0.15f, 0.15f),
            new Color(0.15f, 0.85f, 0.15f),
            new Color(0.15f, 0.45f, 0.95f),
            new Color(0.95f, 0.85f, 0.10f),
            new Color(0.85f, 0.15f, 0.85f),
            new Color(0.10f, 0.85f, 0.85f),
        };
        coloresClase = new Color[numClases];
        for (int i = 0; i < numClases; i++)
            coloresClase[i] = paleta[i % paleta.Length];
    }

    void CargarDatos()
    {
        string path = Application.dataPath + "/datos.csv";
        if (!File.Exists(path)) { Debug.LogError("No se encontró datos.csv"); return; }

        foreach (string linea in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(linea) || linea.StartsWith("r")) continue;

            string[] v = linea.Split(',');
            float r = float.Parse(v[0], CultureInfo.InvariantCulture);
            float g = float.Parse(v[1], CultureInfo.InvariantCulture);
            float b = float.Parse(v[2], CultureInfo.InvariantCulture);
            int clase = int.Parse(v[3]);

            GameObject punto = Instantiate(puntoPrefab,
                new Vector3(r, g, b) * 5f, Quaternion.identity);
            punto.transform.localScale = Vector3.one * 0.15f;

            puntos.Add(punto);
            clasesReales.Add(clase);
        }
        Debug.Log($"✅ Puntos cargados: {puntos.Count}");
    }

    void CargarModelo()
    {
        string path = Application.dataPath + "/modelo.json";
        red = new RedNeuronalUnity();
        if (red.CargarDesdeJSON(path))
        {
            numClases = red.outputSize;
            Debug.Log("✅ Modelo cargado correctamente");
        }
        else
        {
            Debug.LogError("❌ Error cargando modelo");
            red = null;
        }
    }

    void ClasificarConRed()
    {
        if (red == null || !red.cargado)
        {
            Debug.LogError("❌ La red no está cargada");
            return;
        }

        for (int i = 0; i < puntos.Count; i++)
        {
            Vector3 pos = puntos[i].transform.position / 5f;
            int pred = red.Predecir(new float[] { pos.x, pos.y, pos.z });

            Color color = (pred - 1 >= 0 && pred - 1 < coloresClase.Length)
                ? coloresClase[pred - 1]
                : Color.black;

            puntos[i].GetComponent<Renderer>().material.color = color;
        }
    }

    public void CrearEjes()
    {
        foreach (var eje in FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (eje.gameObject.name == "Eje") return;

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

    void Update()
    {
        if (FindAnyObjectByType<ReproductorHistorial>() != null) return;

        if (Time.frameCount % 30 != 0) return;
        string path = Application.dataPath + "/listo.txt";
        if (!esperandoRecarga && File.Exists(path))
        {
            esperandoRecarga = true;
            StartCoroutine(EsperarYRecargar(path));
        }
    }

    IEnumerator EsperarYRecargar(string path)
    {
        yield return new WaitForSeconds(0.3f);
        if (File.Exists(path)) File.Delete(path);

        CargarProgreso();
        CargarModelo();
        GenerarColores();

        if (puntos.Count == 0)
            CargarDatos();

        ClasificarConRed();

        DecisionBoundaryNN frontera = FindAnyObjectByType<DecisionBoundaryNN>();
        if (frontera != null)
            frontera.Recargar();

        esperandoRecarga = false;
    }

    public void RecargarTodo()
    {
        foreach (var p in puntos) Destroy(p);
        puntos.Clear();
        clasesReales.Clear();

        CargarModelo();
        GenerarColores();
        CargarDatos();
        ClasificarConRed();

        DecisionBoundaryNN frontera = FindAnyObjectByType<DecisionBoundaryNN>();
        if (frontera != null)
            frontera.Recargar();
        else
            Debug.LogWarning("No se encontró DecisionBoundaryNN en la escena");

        Debug.Log("Recargado completo");
    }

    public void RecargarDesdeEditor()
    {
        if (recargando) return;
        recargando = true;
        StartCoroutine(RecargarConDelay());
    }

    IEnumerator RecargarConDelay()
    {
        foreach (var p in puntos)
            if (p != null) DestroyImmediate(p);
        puntos.Clear();
        clasesReales.Clear();

        yield return new WaitForEndOfFrame();

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
        yield return new WaitForEndOfFrame();
#endif

        CargarModelo();
        GenerarColores();
        CargarDatos();
        ClasificarConRed();

        DecisionBoundaryNN frontera = FindAnyObjectByType<DecisionBoundaryNN>();
        if (frontera != null)
            frontera.Recargar();

        Debug.Log("Recargado completo desde editor");
        recargando = false;
    }

    void CargarProgreso()
    {
        string path = Application.dataPath + "/progreso.json";
        if (!File.Exists(path)) return;

        try
        {
            var data = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
            epocaActual = (int)data["epoca_actual"];
            epocasTotal = (int)data["epocas_total"];
            lossActual = (float)data["loss_actual"];
            accActual = (float)data["acc_actual"];
            entrenandoAhora = (bool)data["entrenando"];
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Error leyendo progreso.json: {ex.Message}");
        }
    }

    void OnGUI()
    {
        if (!mostrarHUD) return;
        if (!entrenandoAhora && epocaActual == 0) return;

        if (estiloHUD == null)
        {
            estiloHUD = new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(10, 10, 8, 8)
            };
            estiloHUD.normal.textColor = Color.white;
        }

        float progreso = epocasTotal > 0 ? (float)epocaActual / epocasTotal : 0f;
        string estado = entrenandoAhora ? "ENTRENANDO" : "COMPLETADO";
        string texto = $"{estado}\nÉpoca: {epocaActual} / {epocasTotal}\n" +
                         $"Loss:  {lossActual:F4}\nAcc:   {accActual:P1}";

        GUI.color = new Color(0, 0, 0, 0.6f);
        GUI.Box(new Rect(10, 10, 220, 100), "");
        GUI.color = Color.white;
        GUI.Label(new Rect(10, 10, 220, 100), texto, estiloHUD);

        GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        GUI.Box(new Rect(10, 118, 220, 14), "");
        GUI.color = entrenandoAhora
            ? new Color(0.3f, 0.75f, 0.4f, 0.9f)
            : new Color(0.15f, 0.45f, 0.95f, 0.9f);
        GUI.Box(new Rect(10, 118, 220 * progreso, 14), "");
        GUI.color = Color.white;
    }
}