using System.Collections.Generic;

public interface IMatchHandlerService
{
    void DestroyMatches();
    List<SC_Gem> FilterValidCascadeMatches(List<SC_Gem> allMatches);
    void ProcessCascadeMatches(List<SC_Gem> validCascadeMatches);
    void SetBombHandler(IBombHandler bombHandler);
    void SetCascadeService(ICascadeService cascadeService);
}
