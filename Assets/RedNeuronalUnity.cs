using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Newtonsoft.Json.Linq;

public class RedNeuronalUnity
{
    private List<float[,]> Ws = new List<float[,]>();
    private List<float[]> Bs = new List<float[]>();
    private int numCapasOcultas = 0;
    public int outputSize = 3;
    public bool cargado = false;

    public bool CargarDesdeJSON(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"No existe: {path}");
            return false;
        }
        JObject data;
        try { data = JObject.Parse(File.ReadAllText(path)); }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error leyendo JSON: {ex.Message}");
            return false;
        }

        if (data["output_size"] == null)
        {
            Debug.LogError("modelo.json no tiene 'output_size'");
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
                Debug.LogError("modelo.json no tiene capas. Reentrena.");
                return false;
            }
            capasArr = lista.ToArray();
        }
        numCapasOcultas = capasArr.Length;
        int totalCapas = numCapasOcultas + 1;

        Ws.Clear(); Bs.Clear();
        for (int i = 1; i <= totalCapas; i++)
        {
            if (data[$"W{i}"] == null || data[$"b{i}"] == null)
            {
                Debug.LogError($"Falta W{i} o b{i}. Reentrena el modelo.");
                return false;
            }
            Ws.Add(LeerMatriz(data[$"W{i}"]));
            Bs.Add(LeerVector(data[$"b{i}"]));
        }

        cargado = true;
        Debug.Log($"RedNeuronalUnity: {numCapasOcultas} capas ocultas, salidas={outputSize}");
        return true;
    }

    public float[] Forward(float[] x)
    {
        float[] a = x;
        for (int i = 0; i < Ws.Count; i++)
        {
            a = CapaLineal(a, Ws[i], Bs[i]);
            a = (i < numCapasOcultas) ? ReLU(a) : Softmax(a);
        }
        return a;
    }

    public int Predecir(float[] x)
    {
        float[] salida = Forward(x);
        int maxIdx = 0;
        for (int j = 1; j < salida.Length; j++)
            if (salida[j] > salida[maxIdx]) maxIdx = j;
        return maxIdx + 1;
    }

    float[] ReLU(float[] z)
    {
        float[] a = new float[z.Length];
        for (int j = 0; j < z.Length; j++) a[j] = Mathf.Max(0f, z[j]);
        return a;
    }

    float[] Softmax(float[] z)
    {
        float max = z[0];
        for (int j = 1; j < z.Length; j++) if (z[j] > max) max = z[j];
        float sum = 0f;
        float[] s = new float[z.Length];
        for (int j = 0; j < z.Length; j++) { s[j] = Mathf.Exp(z[j] - max); sum += s[j]; }
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