using Zenject;

public class GameConfigService : IGameConfig
{
    public int BoardWidth { get; }
    public int BoardHeight { get; }
    public int PoolSize { get; }

    [Inject]
    public GameConfigService(SC_GameVariables gameVariables)
    {
        BoardWidth = gameVariables.rowsSize;
        BoardHeight = gameVariables.colsSize;
        PoolSize = 50;
    }
}


