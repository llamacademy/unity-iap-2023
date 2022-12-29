using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;

public class StoreIconProvider
{
    public static Dictionary<string, Texture2D> Icons
    {
        get;
        private set;
    } = new();
    private static int TargetIconCount;
    public delegate void LoadCompleteAction();
    public static event LoadCompleteAction OnLoadComplete;

    public static void Initialize(ProductCollection Products)
    {
        if (Icons.Count == 0)
        {
            Debug.Log($"Loading store icons for {Products.all.Length} products.");
            TargetIconCount = Products.all.Length;
            foreach (Product product in Products.all)
            {
                Debug.Log($"Loading store icon at path StoreIcons/{product.definition.id}");
                ResourceRequest operation = Resources.LoadAsync<Texture2D>($"StoreIcons/{product.definition.id}");
                operation.completed += HandleLoadIcon;
            }
        }
        else
        {
            Debug.LogError("StoreIconProvider has already been initialized!");
        }
    }

    public static Texture2D GetIcon(string Id)
    {
        if (Icons.Count == 0)
        {
            Debug.LogError("Called StoreIconProvider.GetIcon before initializing!" +
                "This is not a supported operation!");
            throw new InvalidOperationException("StoreIconProvider.GetIcon() cannot be called before calling" +
                "StoreIconProivder.Initialize()");
        }
        else
        {
            Icons.TryGetValue(Id, out Texture2D icon);
            return icon;
        }
    }

    private static void HandleLoadIcon(AsyncOperation Operation)
    {
        ResourceRequest request = Operation as ResourceRequest;
        if (request.asset != null)
        {
            Debug.Log($"Successfully loaded {request.asset.name}");
            Icons.Add(request.asset.name, request.asset as Texture2D);

            if (Icons.Count == TargetIconCount)
            {
                OnLoadComplete?.Invoke();
            }
        }
        else
        {
            // Subtract from total because something failed to load
            TargetIconCount--;
        }
    }
}
