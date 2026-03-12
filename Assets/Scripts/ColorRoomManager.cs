using UnityEngine;

public class ColorRoomManager : MonoBehaviour
{
    [Header("Références Faders")]
    public FaderController faderPiano;
    public FaderController faderGuitare;

    [Header("Lumières de la pièce")]
    public Light[] roomLights;

    [Header("Couleurs")]
    public Color colorPiano = Color.blue;
    public Color colorGuitare = Color.red;

    [Header("Intensité")]
    public float minIntensity = 0.5f;
    public float maxIntensity = 2f;

    void Update()
    {
        float piano = faderPiano.value;
        float guitare = faderGuitare.value;

        // Couleur = blend entre rouge et bleu selon les valeurs
        Color roomColor = Color.black;

        if (piano + guitare > 0f)
        {
            float totalVolume = piano + guitare;
            roomColor = (colorPiano * piano + colorGuitare * guitare) / totalVolume;
        }

        // Intensité basée sur le volume total
        float intensity = Mathf.Lerp(minIntensity, maxIntensity, (piano + guitare) / 2f);

        // On applique à toutes les lumières
        foreach (Light light in roomLights)
        {
            light.color = roomColor;
            light.intensity = intensity;
        }
    }
}