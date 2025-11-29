using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class GemPool
{
    private Dictionary<GlobalEnums.GemType, Queue<SC_Gem>> pools;
    private Transform poolParent;
    private int maxPoolSize;
    private DiContainer container;

    public GemPool(Transform poolParent, int maxPoolSize, DiContainer container)
    {
        this.poolParent = poolParent;
        this.maxPoolSize = maxPoolSize;
        this.container = container;
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
            if (container != null)
            {
                container.Inject(gem);
            }
            gem.gameObject.SetActive(true);
            gem.transform.position = position;
            gem.transform.SetParent(parent);
        }
        else
        {
            gem = Object.Instantiate(gemPrefab, position, Quaternion.identity, parent);
            if (container != null)
            {
                container.Inject(gem);
            }
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
                if (container != null)
                {
                    container.Inject(gem);
                }
                gem.gameObject.SetActive(false);
                pools[gemType].Enqueue(gem);
            }
        }
    }
    
    public class Factory : PlaceholderFactory<Transform, int, GemPool>
    {
    }
}

