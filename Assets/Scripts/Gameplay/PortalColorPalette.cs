using UnityEngine;

public static class PortalColorPalette
{
    public static Color GetColor(int id)
    {
        switch (id % 6)
        {
            case 0:
                return new Color32(230, 190, 255, 255);  // Purple

            case 1:
                return new Color32(45, 190, 225, 255);   // Cyan

            case 2:
                return new Color32(238, 139, 48, 255);   // Orange

            case 3:
                return new Color32(226, 74, 151, 255);   // Pink

            case 4:
                return new Color32(103, 194, 79, 255);   // Green

            default:
                return new Color32(226, 73, 73, 255);    // Red
        }
    }
}