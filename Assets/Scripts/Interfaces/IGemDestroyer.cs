using System;
using UnityEngine;

public interface IGemDestroyer
{
    void DestroyGem(Vector2Int pos, Func<SC_Gem, bool> condition);
    void DestroyGemAt(Vector2Int pos);
}
