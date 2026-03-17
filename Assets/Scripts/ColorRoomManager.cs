using UnityEngine;

public class ColorRoomManager : MonoBehaviour
{
    [Header("Références de faders")]
    public FaderController faderPiano;
    public FaderController faderGuitare;
    public FaderController faderBass;

    [Header("Toutes les lights là dedans")]
    public Light[] roomLights;

    [Header("Couleurs")]
    public Color colorViolons = Color.blue;
    public Color colorGuitare = Color.red;
    public Color colorBass = Color.green;

    [Header("Intensité des lights")]
    public float minIntensity = 0.5f;
    public float maxIntensity = 2f;

    void Update()
    {
        float piano = faderPiano.value;
        float guitare = faderGuitare.value;
        float bass = faderBass.value;

        Color roomColor = Color.white;

        if (piano + guitare + bass > 0f)
        {
            float totalVolume = piano + guitare + bass;
            roomColor = (colorViolons * piano + colorGuitare * guitare + colorBass * bass) / totalVolume;
        }

        float intensity = Mathf.Lerp(minIntensity, maxIntensity, (piano + guitare + bass) / 2f);

        foreach (Light light in roomLights)
        {
            light.color = roomColor;
            light.intensity = intensity;
        }
    }
}