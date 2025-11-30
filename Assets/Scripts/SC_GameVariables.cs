using UnityEngine;

public class SC_GameVariables : MonoBehaviour
{
    public GameObject bgTilePrefabs;
    public SC_Gem bomb;
    public SC_Gem[] gems;
    public int dropHeight = 0;
    public float gemSpeed;
    public float cascadeDelay = 0.05f;
    public float bombExplosionDelay = 1f;
    public float scoreSpeed = 5;
    
    [HideInInspector]
    public int rowsSize = 7;
    [HideInInspector]
    public int colsSize = 7;
}
