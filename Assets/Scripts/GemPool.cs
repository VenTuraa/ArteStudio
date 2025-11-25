using System.Collections.Generic;
using UnityEngine;

public class GemPool
{
    private Dictionary<GlobalEnums.GemType, Queue<SC_Gem>> pools;
    private Transform poolParent;
    private int maxPoolSize;

    public GemPool(Transform poolParent, int maxPoolSize = 50)
    {
        this.poolParent = poolParent;
        this.maxPoolSize = maxPoolSize;
        pools = new Dictionary<GlobalEnums.GemType, Queue<SC_Gem>>();
    }

    public SC_Gem GetGem(SC_Gem gemPrefab, Vector3 position, Transform parent)
    {
        if (!gemPrefab)
            return null;

        GlobalEnums.GemType gemType = gemPrefab.type;

        if (!pools.ContainsKey(gemType))
        {
            pools[gemType] = new Queue<SC_Gem>();
        }

        SC_Gem gem;

        if (pools[gemType].Count > 0)
        {
            gem = pools[gemType].Dequeue();
            gem.gameObject.SetActive(true);
            gem.transform.position = position;
            gem.transform.SetParent(parent);
        }
        else
        {
            gem = Object.Instantiate(gemPrefab, position, Quaternion.identity, parent);
        }

        return gem;
    }

    public void ReturnToPool(SC_Gem gem)
    {
        if (!gem || !gem.gameObject)
            return;

        GlobalEnums.GemType gemType = gem.type;

        if (!pools.ContainsKey(gemType))
        {
            pools[gemType] = new Queue<SC_Gem>();
        }

        if (pools[gemType].Count >= maxPoolSize)
        {
            Object.Destroy(gem.gameObject);
            return;
        }

        gem.gameObject.SetActive(false);
        gem.isMatch = false;
        gem.transform.SetParent(poolParent);
        
        pools[gemType].Enqueue(gem);
    }

    public void WarmPool(SC_Gem[] gemPrefabs, int initialPoolSize)
    {
        if (gemPrefabs == null)
            return;

        foreach (SC_Gem prefab in gemPrefabs)
        {
            if (!prefab)
                continue;

            GlobalEnums.GemType gemType = prefab.type;
            if (!pools.ContainsKey(gemType))
            {
                pools[gemType] = new Queue<SC_Gem>();
            }

            for (int i = 0; i < initialPoolSize; i++)
            {
                SC_Gem gem = Object.Instantiate(prefab, poolParent);
                gem.gameObject.SetActive(false);
                pools[gemType].Enqueue(gem);
            }
        }
    }
}

