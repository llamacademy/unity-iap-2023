using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.UI;

public class DemoStorePage : MonoBehaviour, IStoreListener
{
    [SerializeField]
    private UIProduct UIProductPrefab;
    [SerializeField]
    private HorizontalLayoutGroup ContentPanel;
    [SerializeField]
    private GameObject LoadingOverlay;

    private Action OnPurchaseCompleted;
    private IStoreController StoreController;
    private IExtensionProvider ExtensionProvider;

    private async void Awake()
    {
        InitializationOptions options = new InitializationOptions()
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            .SetEnvironmentName("test");
#else
            .SetEnvironmentName("production");
#endif
        await UnityServices.InitializeAsync(options);
        ResourceRequest operation = Resources.LoadAsync<TextAsset>("IAPProductCatalog");
        operation.completed += HandleIAPCatalogLoaded;
    }

    private void HandleIAPCatalogLoaded(AsyncOperation Operation)
    {
        ResourceRequest request = Operation as ResourceRequest;

        Debug.Log($"Loaded Asset: {request.asset}");
        ProductCatalog catalog = JsonUtility.FromJson<ProductCatalog>((request.asset as TextAsset).text);
        Debug.Log($"Loaded catalog with {catalog.allProducts.Count} items");

        StandardPurchasingModule.Instance().useFakeStoreUIMode = FakeStoreUIMode.StandardUser;
        StandardPurchasingModule.Instance().useFakeStoreAlways = true;

#if UNITY_ANDROID
        ConfigurationBuilder builder = ConfigurationBuilder.Instance(
            StandardPurchasingModule.Instance(AppStore.GooglePlay)
        );
#elif UNITY_IOS
        ConfigurationBuilder builder = ConfigurationBuilder.Instance(
            StandardPurchasingModule.Instance(AppStore.AppleAppStore)
        );
#else
        ConfigurationBuilder builder = ConfigurationBuilder.Instance(
            StandardPurchasingModule.Instance(AppStore.NotSpecified)
        );
#endif
        foreach (ProductCatalogItem item in catalog.allProducts)
        {
            builder.AddProduct(item.id, item.type);
        }

        Debug.Log($"Initializing Unity IAP with {builder.products.Count} products");
        UnityPurchasing.Initialize(this, builder);
    }

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        StoreController = controller;
        ExtensionProvider = extensions;
        Debug.Log($"Successfully Initialized Unity IAP. Store Controller has {StoreController.products.all.Length} products");
        StoreIconProvider.Initialize(StoreController.products);
        StoreIconProvider.OnLoadComplete += HandleAllIconsLoaded;
    }

    private void HandleAllIconsLoaded()
    {
        StartCoroutine(CreateUI());
    }

    private IEnumerator CreateUI()
    {
        List<Product> sortedProducts = StoreController.products.all
            .TakeWhile(item => !item.definition.id.Contains("sale"))
            .OrderBy(item => item.metadata.localizedPrice)
            .ToList();

        foreach (Product product in sortedProducts)
        {
            UIProduct uiProduct = Instantiate(UIProductPrefab);
            uiProduct.OnPurchase += HandlePurchase;
            uiProduct.Setup(product);
            uiProduct.transform.SetParent(ContentPanel.transform, false);
            yield return null;
        }

        HorizontalLayoutGroup group = ContentPanel.GetComponent<HorizontalLayoutGroup>();
        float spacing = group.spacing;
        float horizontalPadding = group.padding.left + group.padding.right;
        float itemSize = ContentPanel.transform
            .GetChild(0)
            .GetComponent<RectTransform>()
            .sizeDelta.x;

        RectTransform contentPanelRectTransform = ContentPanel.GetComponent<RectTransform>();
        contentPanelRectTransform.sizeDelta = new(
            horizontalPadding + (spacing + itemSize) * sortedProducts.Count,
            contentPanelRectTransform.sizeDelta.y
        );
    }

    private void HandlePurchase(Product Product, Action OnPurchaseCompleted)
    {
        LoadingOverlay.SetActive(true);
        this.OnPurchaseCompleted = OnPurchaseCompleted;
        StoreController.InitiatePurchase(Product);
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        Debug.LogError($"Error initializing IAP because of {error}." +
            $"\r\nShow a message to the player depending on the error.");
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        Debug.Log($"Failed to purchase {product.definition.id} because {failureReason}");
        OnPurchaseCompleted?.Invoke();
        OnPurchaseCompleted = null;
        LoadingOverlay.SetActive(false);
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs purchaseEvent)
    {
        Debug.Log($"Successfully purchased {purchaseEvent.purchasedProduct.definition.id}");
        OnPurchaseCompleted?.Invoke();
        OnPurchaseCompleted = null;
        LoadingOverlay.SetActive(false);

        // do something, like give the player their currency, unlock the item,
        // update some metrics or analytics, etc...

        return PurchaseProcessingResult.Complete;
    }
}
