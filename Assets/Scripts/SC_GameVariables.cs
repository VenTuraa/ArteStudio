using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SC_GameVariables : MonoBehaviour
{
    public GameObject bgTilePrefabs;
    public SC_Gem bomb;
    public SC_Gem[] gems;
    public float bonusAmount = 0.5f;
    public float bombChance = 2f;
    public int dropHeight = 0;
    public float gemSpeed;
    public float cascadeDelay = 0.05f; // Delay between each gem drop in cascade
    public float bombNeighborDestroyDelay = 0.3f; // Delay before destroying neighbor pieces
    public float bombDestroyDelay = 0.5f; // Delay before destroying the bomb itself
    public float scoreSpeed = 5;
    
    [HideInInspector]
    public int rowsSize = 7;
    [HideInInspector]
    public int colsSize = 7;
}
