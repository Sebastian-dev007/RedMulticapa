using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Newtonsoft.Json.Linq;

public class DecisionBoundaryNN : MonoBehaviour
{
    public int resolucion = 20;

    private List<float[,]> Ws = new List<float[,]>();
    private List<float[]> bs = new List<float[]>();
    private int numCapasOcultas = 0;
    private int outputSize = 3;
    private Color[] coloresClase;
    private List<GameObject> voxels = new List<GameObject>();

    void Start() { if (CargarModelo()) DibujarFrontera(); }

    public void Recargar()
    {
        foreach (var v in voxels) Destroy(v);
        voxels.Clear();
        if (CargarModelo()) DibujarFrontera();
    }

    bool CargarModelo()
    {
        string path = Application.dataPath + "/modelo.json";
        if (!File.Exists(path)) { Debug.LogError("No existe modelo.json"); return false; }

        JObject data;
        try
        {
            data = JObject.Parse(File.ReadAllText(path));
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error leyendo modelo.json: {ex.Message}");
            return false;
        }

        // ✅ Verificar campo obligatorio
        if (data["output_size"] == null)
        {
            Debug.LogError("modelo.json no tiene 'output_size'. Reentrena el modelo.");
            return false;
        }

        outputSize = data["output_size"].Value<int>();

        int[] capasArr;
        if (data["capas_ocultas"] != null)
        {
            capasArr = data["capas_ocultas"].ToObject<int[]>();
        }
        else
        {
            var lista = new List<int>();
            string[] claves = { "hidden_size","hidden_size2","hidden_size3",
                            "hidden_size4","hidden_size5","hidden_size6" };
            foreach (var clave in claves)
                if (data[clave] != null)
                    lista.Add(data[clave].Value<int>());

            if (lista.Count == 0)
            {
                Debug.LogError("modelo.json no tiene información de capas. Reentrena.");
                return false;
            }
            capasArr = lista.ToArray();
        }

        numCapasOcultas = capasArr.Length;
        int totalCapas = numCapasOcultas + 1;

        Ws.Clear(); bs.Clear();
        for (int i = 1; i <= totalCapas; i++)
        {
            string claveW = $"W{i}";
            string claveB = $"b{i}";
            if (data[claveW] == null || data[claveB] == null)
            {
                Debug.LogError($"Falta {claveW} o {claveB} en modelo.json. Reentrena.");
                return false;
            }
            Ws.Add(LeerMatriz(data[claveW]));
            bs.Add(LeerVector(data[claveB]));
        }

        Color[] paleta = new Color[] {
        new Color(0.9f, 0.15f, 0.15f),
        new Color(0.15f, 0.85f, 0.15f),
        new Color(0.15f, 0.45f, 0.95f),
        new Color(0.95f, 0.85f, 0.10f),
        new Color(0.85f, 0.15f, 0.85f),
        new Color(0.10f, 0.85f, 0.85f),
    };
        coloresClase = new Color[outputSize];
        for (int i = 0; i < outputSize; i++)
            coloresClase[i] = paleta[i % paleta.Length];

        Debug.Log($"Modelo cargado: {numCapasOcultas} capas ocultas, salidas={outputSize}");
        return true;
    }

    void DibujarFrontera()
    {
        float paso = 1f / resolucion;
        int[] conteo = new int[outputSize];

        for (int xi = 0; xi < resolucion; xi++)
            for (int yi = 0; yi < resolucion; yi++)
                for (int zi = 0; zi < resolucion; zi++)
                    conteo[Predecir(xi * paso, yi * paso, zi * paso) - 1]++;

        for (int i = 0; i < outputSize; i++)
            Debug.Log($"Clase {i + 1}: {conteo[i]} puntos");

        for (int xi = 0; xi < resolucion; xi++)
            for (int yi = 0; yi < resolucion; yi++)
                for (int zi = 0; zi < resolucion; zi++)
                {
                    float px = xi * paso, py = yi * paso, pz = zi * paso;
                    int clase = Predecir(px, py, pz);

                    bool frontera =
                        (xi < resolucion - 1 && Predecir(px + paso, py, pz) != clase) ||
                        (yi < resolucion - 1 && Predecir(px, py + paso, pz) != clase) ||
                        (zi < resolucion - 1 && Predecir(px, py, pz + paso) != clase);

                    if (!frontera) continue;

                    Vector3 pos = new Vector3(px, py, pz) * 5f;
                    GameObject v = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    v.transform.position = pos;
                    v.transform.localScale = Vector3.one * (paso * 5f * 0.5f);
                    v.GetComponent<Renderer>().material.color = coloresClase[clase - 1];
                    Destroy(v.GetComponent<Collider>());
                    voxels.Add(v);
                }

        Debug.Log($"Frontera total: {voxels.Count} puntos");
    }

    float[] ReLU(float[] z)
    {
        float[] a = new float[z.Length];
        for (int j = 0; j < z.Length; j++) a[j] = Mathf.Max(0f, z[j]);
        return a;
    }

    float[] Softmax(float[] z)
    {
        float maxZ = z[0];
        for (int j = 1; j < z.Length; j++) if (z[j] > maxZ) maxZ = z[j];
        float sum = 0f;
        float[] s = new float[z.Length];
        for (int j = 0; j < z.Length; j++) { s[j] = Mathf.Exp(z[j] - maxZ); sum += s[j]; }
        for (int j = 0; j < z.Length; j++) s[j] /= sum;
        return s;
    }

    float[] CapaLineal(float[] entrada, float[,] W, float[] b)
    {
        int cols = W.GetLength(1);
        float[] z = new float[cols];
        for (int j = 0; j < cols; j++)
        {
            z[j] = b[j];
            for (int i = 0; i < entrada.Length; i++)
                z[j] += entrada[i] * W[i, j];
        }
        return z;
    }

    int Predecir(float r, float g, float b)
    {
        float[] a = { r, g, b };

        for (int i = 0; i < Ws.Count; i++)
        {
            float[] z = CapaLineal(a, Ws[i], bs[i]);
            // ReLU en ocultas, Softmax en salida
            a = (i < numCapasOcultas) ? ReLU(z) : Softmax(z);
        }

        int maxIdx = 0;
        for (int j = 1; j < a.Length; j++)
            if (a[j] > a[maxIdx]) maxIdx = j;

        return maxIdx + 1;
    }

    float[,] LeerMatriz(JToken t)
    {
        var arr = t.ToObject<float[][]>();
        int f = arr.Length, c = arr[0].Length;
        float[,] m = new float[f, c];
        for (int i = 0; i < f; i++)
            for (int j = 0; j < c; j++) m[i, j] = arr[i][j];
        return m;
    }

    float[] LeerVector(JToken t) => t.ToObject<float[]>();
}