using UnityEngine;

public static class ConnectionColorPalette
{
    public static Color GetColor(int id)
    {
        switch (id % 6)
        {
            case 0:
                // Koyu Mavi
                return new Color(0.05f, 0.25f, 0.9f);

            case 1:
                // Mor
                return new Color(0.65f, 0.1f, 0.9f);

            case 2:
                // Kırmızı
                return new Color(0.9f, 0.05f, 0.05f);

            case 3:
                // Koyu Yeşil
                return new Color(0.05f, 0.55f, 0.15f);

            case 4:
                // Pembe
                return new Color(0.95f, 0.1f, 0.55f);

            default:
                // Turuncu
                return new Color(1f, 0.35f, 0.05f);
        }
    }
}