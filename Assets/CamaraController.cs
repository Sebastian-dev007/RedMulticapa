using UnityEngine;
using UnityEngine.InputSystem;

public class CamaraController : MonoBehaviour
{
    [Header("Objetivo")]
    public Vector3 objetivo = new Vector3(5f, 5f, 5f);

    [Header("Orbita")]
    public float sensibilidadRaton = 3f;
    public float distancia = 15f;
    public float distanciaMin = 3f;
    public float distanciaMax = 30f;

    [Header("Zoom")]
    public float sensibilidadZoom = 2f;

    private float anguloX = 20f;
    private float anguloY = 45f;

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        // ✅ ORBITAR — clic derecho + arrastrar
        if (mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            anguloY += delta.x * sensibilidadRaton * Time.deltaTime * 10f;
            anguloX -= delta.y * sensibilidadRaton * Time.deltaTime * 10f;
            anguloX = Mathf.Clamp(anguloX, -80f, 80f);
        }

        // ✅ ZOOM — rueda del ratón
        float scroll = mouse.scroll.ReadValue().y;
        distancia -= scroll * sensibilidadZoom * 0.01f;
        distancia = Mathf.Clamp(distancia, distanciaMin, distanciaMax);

        // ✅ MOVER OBJETIVO — clic medio + arrastrar
        if (mouse.middleButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            objetivo -= transform.right * delta.x * 0.05f;
            objetivo -= transform.up * delta.y * 0.05f;
        }

        // Calcular posición de la cámara
        Quaternion rotacion = Quaternion.Euler(anguloX, anguloY, 0);
        transform.position = objetivo + rotacion * new Vector3(0, 0, -distancia);
        transform.LookAt(objetivo);
    }
}