using System;

public class LevelData
{
    public string[] Rows { get; }

    public int Width { get; }
    public int Height => Rows.Length;

    public LevelData(params string[] rows)
    {
        Rows = rows;

        if (rows == null || rows.Length == 0)
        {
            Width = 0;
            return;
        }

        Width = GetRowTokens(0).Length;

        for (int i = 1; i < rows.Length; i++)
        {
            if (GetRowTokens(i).Length != Width)
            {
                throw new Exception(
                    "LevelData: Tüm satırlar aynı genişlikte olmalı."
                );
            }
        }
    }

    public string[] GetRowTokens(int rowIndex)
    {
        return Rows[rowIndex].Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries
        );
    }
}