using UnityEngine;
using UnityEditor;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using Newtonsoft.Json;

public class InterfazParametros : EditorWindow
{
    string pythonPath = "python";
    string projectPath = @"C:\Users\Usuario\Downloads\Cristian\Inteligencia artificial\Perceptron_V2";

    int tipoIndex = 0;
    int numClases = 3;
    int n = 150;
    float sigma = 12f;
    float separacion = 200f;
    int seed = 42;

    float tasa = 0.05f;
    int epocas = 400;
    int batchSize = 16;

    List<int> capasOcultas = new List<int> { 8, 6 };

    string logOutput = "";
    bool entrenando = false;
    Vector2 scrollLog;
    Vector2 scrollMain;
    Process procesoActivo;

    Texture2D graficaTextura;
    bool mostrarGrafica = false;
    Vector2 scrollGrafica;

    static readonly string[] TIPOS = { "nubes", "circulo", "xor", "lineal" };

    [MenuItem("Red Neuronal/Interfaz de Parámetros")]
    public static void Abrir()
    {
        var w = GetWindow<InterfazParametros>("Red Neuronal");
        w.minSize = new Vector2(460, 680);
    }

    void OnGUI()
    {
        scrollMain = EditorGUILayout.BeginScrollView(scrollMain);
        DibujarTitulo();
        DibujarRutas();
        DibujarDatos();
        DibujarEntrenamiento();
        DibujarArquitectura();
        DibujarBotones();
        DibujarGrafica();
        DibujarLog();
        EditorGUILayout.EndScrollView();
    }

    void DibujarTitulo()
    {
        EditorGUILayout.Space(10);
        var s = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("Red Neuronal — Configurador", s);
        EditorGUILayout.Space(6);
    }

    void DibujarRutas()
    {
        Seccion("Rutas");
        pythonPath = EditorGUILayout.TextField("Python", pythonPath);
        projectPath = EditorGUILayout.TextField("Carpeta proyecto", projectPath);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Buscar carpeta", GUILayout.Width(120)))
        {
            string ruta = EditorUtility.OpenFolderPanel("Carpeta Python", projectPath, "");
            if (!string.IsNullOrEmpty(ruta)) projectPath = ruta;
        }
        EditorGUILayout.EndHorizontal();
    }

    void DibujarDatos()
    {
        Seccion("Generación de datos");
        tipoIndex = EditorGUILayout.Popup("Tipo de datos", tipoIndex, TIPOS);
        string tipo = TIPOS[tipoIndex];
        bool solo2 = tipo == "circulo" || tipo == "xor" || tipo == "lineal";

        if (solo2)
        {
            numClases = 2;
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.IntField("Núm. clases", 2);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.HelpBox($"'{tipo}' solo soporta 2 clases.", MessageType.Info);
        }
        else
        {
            numClases = EditorGUILayout.IntSlider("Núm. clases", numClases, 2, 6);
        }

        n = EditorGUILayout.IntSlider("Muestras (n)", n, 50, 1000);
        sigma = EditorGUILayout.Slider("Sigma", sigma, 1f, 50f);
        separacion = EditorGUILayout.Slider("Separación", separacion, 50f, 254f);
        seed = EditorGUILayout.IntField("Semilla", seed);
    }

    void DibujarEntrenamiento()
    {
        Seccion("Entrenamiento");
        tasa = EditorGUILayout.Slider("Tasa aprendizaje", tasa, 0.001f, 0.5f);
        epocas = EditorGUILayout.IntSlider("Épocas", epocas, 50, 2000);
        batchSize = EditorGUILayout.IntSlider("Batch size", batchSize, 4, 128);
    }

    void DibujarArquitectura()
    {
        Seccion("Capas ocultas");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Total: {capasOcultas.Count} capa(s)", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+ Capa", GUILayout.Width(65)) && capasOcultas.Count < 6)
            capasOcultas.Add(8);
        EditorGUI.BeginDisabledGroup(capasOcultas.Count <= 1);
        if (GUILayout.Button("- Capa", GUILayout.Width(65)))
            capasOcultas.RemoveAt(capasOcultas.Count - 1);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);

        for (int i = 0; i < capasOcultas.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"  Capa {i + 1}", GUILayout.Width(55));
            capasOcultas[i] = EditorGUILayout.IntSlider(capasOcultas[i], 1, 64);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(4);
        string resumen = "Entrada(3)";
        foreach (var c in capasOcultas) resumen += $" → ({c})";
        resumen += $" → Salida({numClases})";
        EditorGUILayout.HelpBox(resumen, MessageType.None);
    }

    // ── Agrega esta bandera al inicio de la clase ──
    bool pendienteRecarga = false;

    void DibujarBotones()
    {
        EditorGUILayout.Space(8);
        EditorGUI.BeginDisabledGroup(entrenando);

        if (GUILayout.Button("Guardar config.json", GUILayout.Height(30)))
            GuardarConfig();

        EditorGUILayout.Space(4);
        var colorAntes = GUI.backgroundColor;
        GUI.backgroundColor = entrenando
            ? new Color(0.8f, 0.4f, 0.2f)
            : new Color(0.3f, 0.75f, 0.4f);

        if (GUILayout.Button(
            entrenando ? "Entrenando... (espera)" : "▶  Entrenar y enviar a Unity",
            GUILayout.Height(40)))
            LanzarEntrenamiento();

        GUI.backgroundColor = colorAntes;
        EditorGUI.EndDisabledGroup();

        if (entrenando)
        {
            EditorGUILayout.Space(2);
            if (GUILayout.Button("Detener entrenamiento", GUILayout.Height(28)))
                Detener();
        }

        // ✅ Botón de recarga SEPARADO — solo ejecuta una vez al hacer click
        EditorGUILayout.Space(4);
        EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying || entrenando);
        if (GUILayout.Button("↺  Recargar visualización", GUILayout.Height(28)))
            EjecutarRecarga();
        EditorGUI.EndDisabledGroup();

        if (!EditorApplication.isPlaying)
            EditorGUILayout.HelpBox("Inicia Play Mode para ver cambios.", MessageType.Info);

        EditorGUILayout.Space(6);
    }

    void EjecutarRecarga()
    {
        if (pendienteRecarga) return;
        pendienteRecarga = true;

        Visualizador vis = UnityEngine.Object.FindAnyObjectByType<Visualizador>();
        if (vis != null)
        {
            vis.RecargarDesdeEditor();  // usa corrutina
            Log("✅ Recarga iniciada.");
        }
        else
            Log("⚠ No se encontró Visualizador — ¿Unity está en Play Mode?");

        pendienteRecarga = false;
    }

    void DibujarGrafica()
    {
        Seccion("Gráfica de entrenamiento");
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Cargar gráfica", GUILayout.Width(120)))
            CargarGrafica();

        mostrarGrafica = EditorGUILayout.ToggleLeft("Mostrar", mostrarGrafica);

        if (graficaTextura != null)
        {
            GUIStyle estado = new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = new Color(0.3f, 0.75f, 0.4f) } };
            EditorGUILayout.LabelField("✔ Cargada", estado);
        }
        EditorGUILayout.EndHorizontal();

        if (!mostrarGrafica || graficaTextura == null) return;

        EditorGUILayout.Space(4);
        float ancho = EditorGUIUtility.currentViewWidth - 24f;
        float proporcion = (float)graficaTextura.height / graficaTextura.width;
        float alto = ancho * proporcion;

        scrollGrafica = EditorGUILayout.BeginScrollView(
            scrollGrafica, GUILayout.Height(Mathf.Min(alto, 280f)));
        Rect rect = GUILayoutUtility.GetRect(ancho, alto);
        GUI.DrawTexture(rect, graficaTextura, ScaleMode.ScaleToFit);
        EditorGUILayout.EndScrollView();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Abrir en explorador", GUILayout.Width(140)))
        {
            string ruta = Path.Combine(projectPath, "analisis_red_neuronal.png");
            if (File.Exists(ruta))
            {
                string rutaWin = ruta.Replace('/', '\\');
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{rutaWin}\"");
            }
        }
        if (GUILayout.Button("Recargar", GUILayout.Width(80)))
            CargarGrafica();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);
    }

    void CargarGrafica()
    {
        string ruta = Path.Combine(projectPath, "analisis_red_neuronal.png");
        if (!File.Exists(ruta))
        {
            Log("⚠ No se encontró analisis_red_neuronal.png — entrena primero.");
            return;
        }

        byte[] bytes = File.ReadAllBytes(ruta);
        graficaTextura = new Texture2D(2, 2);

        if (graficaTextura.LoadImage(bytes))
        {
            mostrarGrafica = true;
            Log($"✅ Gráfica cargada ({graficaTextura.width}×{graficaTextura.height}px)");
        }
        else
        {
            Log("❌ Error cargando la imagen.");
            graficaTextura = null;
        }
        Repaint();
    }

    void DibujarLog()
    {
        if (string.IsNullOrEmpty(logOutput)) return;
        Seccion("Salida");
        scrollLog = EditorGUILayout.BeginScrollView(scrollLog, GUILayout.Height(150));
        EditorGUILayout.TextArea(logOutput, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
        if (GUILayout.Button("Limpiar log", GUILayout.Width(90)))
            logOutput = "";
    }

    void GuardarConfig()
    {
        var config = new Dictionary<string, object>
        {
            ["tipo_datos"] = TIPOS[tipoIndex],
            ["num_clases"] = numClases,
            ["n"] = n,
            ["sigma"] = sigma,
            ["separacion"] = separacion,
            ["seed"] = seed,
            ["tasa"] = tasa,
            ["epocas"] = epocas,
            ["batch_size"] = batchSize,
        };

        string[] claves = { "hidden_size","hidden_size2","hidden_size3",
                            "hidden_size4","hidden_size5","hidden_size6" };
        for (int i = 0; i < capasOcultas.Count; i++)
            config[claves[i]] = capasOcultas[i];

        string ruta = Path.Combine(projectPath, "config.json");
        try
        {
            File.WriteAllText(ruta, JsonConvert.SerializeObject(config, Formatting.Indented));
            Log($"✅ config.json guardado en {ruta}");
        }
        catch (System.Exception ex) { Log($"❌ Error: {ex.Message}"); }
    }

    void LanzarEntrenamiento()
    {
        logOutput = "";
        GuardarConfig();

        if (!File.Exists(Path.Combine(projectPath, "main.py")))
        {
            Log($"❌ No se encontró main.py en:\n{projectPath}");
            return;
        }

        string python = BuscarPython();
        if (python == null)
        {
            Log("❌ No se encontró Python. Pon la ruta completa en 'Python'.");
            Log(@"Ejemplo: C:\Users\Usuario\AppData\Local\Programs\Python\Python313\python.exe");
            return;
        }

        entrenando = true;
        logOutput = "";
        Log($"▶ Python: {python}");
        Log($"▶ Proyecto: {projectPath}\n");

        var psi = new ProcessStartInfo
        {
            FileName = python,
            Arguments = "\"main.py\"",
            WorkingDirectory = projectPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        procesoActivo = new Process { StartInfo = psi, EnableRaisingEvents = true };
        procesoActivo.OutputDataReceived += (s, e) => { if (e.Data != null) Log(e.Data); };
        procesoActivo.ErrorDataReceived += (s, e) => { if (e.Data != null) Log("⚠ " + e.Data); };
        procesoActivo.Exited += (s, e) =>
        {
            int cod = procesoActivo.ExitCode;
            entrenando = false;
            Log(cod == 0 ? "\n✅ Python terminó." : $"\n❌ Error (código {cod})");

            EditorApplication.delayCall += () =>
            {
                AssetDatabase.Refresh();
                CargarGrafica();

                if (EditorApplication.isPlaying)
                {
                    Visualizador vis = UnityEngine.Object.FindAnyObjectByType<Visualizador>();
                    if (vis != null)
                    {
                        vis.RecargarDesdeEditor();
                        Log("✅ Recarga automática iniciada.");
                    }
                }
                else
                    Log("⚠ Presiona Play y luego '↺ Recargar'.");

                Repaint();
            };
        };

        try
        {
            procesoActivo.Start();
            procesoActivo.BeginOutputReadLine();
            procesoActivo.BeginErrorReadLine();
        }
        catch (System.Exception ex)
        {
            entrenando = false;
            Log($"❌ No se pudo lanzar Python:\n{ex.Message}");
        }
    }

    string BuscarPython()
    {
        if (PythonFunciona(pythonPath)) return pythonPath;

        string usuario = System.Environment.GetFolderPath(
            System.Environment.SpecialFolder.UserProfile);

        string[] candidatos = {
            Path.Combine(usuario, @"AppData\Local\Programs\Python\Python313\python.exe"),
            Path.Combine(usuario, @"AppData\Local\Programs\Python\Python312\python.exe"),
            Path.Combine(usuario, @"AppData\Local\Programs\Python\Python311\python.exe"),
            @"C:\Python313\python.exe",
            @"C:\Python312\python.exe",
            "python3", "python"
        };

        foreach (var c in candidatos)
            if (PythonFunciona(c)) { Log($"Python encontrado: {c}"); return c; }

        return null;
    }

    bool PythonFunciona(string ruta)
    {
        try
        {
            var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ruta,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            p.Start();
            p.WaitForExit(2000);
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    void Detener()
    {
        try { procesoActivo?.Kill(); entrenando = false; Log("⏹ Detenido."); }
        catch { }
    }

    void Log(string msg)
    {
        logOutput += msg + "\n";
        var lineas = logOutput.Split('\n');
        if (lineas.Length > 80)
            logOutput = string.Join("\n", lineas, lineas.Length - 80, 80);
        Repaint();
    }

    void Seccion(string label)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        Rect r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.35f));
        EditorGUILayout.Space(2);
    }

    void OnDestroy() => Detener();
}