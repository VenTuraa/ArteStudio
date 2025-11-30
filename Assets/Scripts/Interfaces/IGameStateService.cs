
public interface IGameStateService
{
    GlobalEnums.GameState CurrentState { get; }
    void SetState(GlobalEnums.GameState state);
}
