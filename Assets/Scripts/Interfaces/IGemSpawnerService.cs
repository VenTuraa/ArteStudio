using UnityEngine;

public interface IGemSpawnerService
{
    void InitializeBoard(int width, int height);
    SC_Gem SpawnGem(Vector2Int position, SC_Gem prefab);
    SC_Gem SpawnGemAtPosition(int column, int spawnY, SC_Gem gemPrefab);
    SC_Gem GetSafeGemForPosition(int column, int targetY, System.Collections.Generic.List<GemDropInfo> existingDropQueue, System.Collections.Generic.List<GemDropInfo> newGemsQueue);
    void SetGameCoordinator(IGameCoordinator coordinator);
}

public struct GemDropInfo
{
    public SC_Gem gem;
    public int sourceY;
    public int targetY;
}
