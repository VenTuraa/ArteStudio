
public class GameStateService : IGameStateService
{
    public GlobalEnums.GameState CurrentState { get; private set; } = GlobalEnums.GameState.move;

    public void SetState(GlobalEnums.GameState state)
    {
        CurrentState = state;
    }
}
