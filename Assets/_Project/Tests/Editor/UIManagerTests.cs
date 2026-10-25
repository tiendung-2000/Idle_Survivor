using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>UI giả lập cho test: đếm số lần gọi vòng đời và lưu dữ liệu nhận được.</summary>
public class TestInventoryUI : BaseUI<string>
{
    public int CreateCount, OpenCount, CloseCount;
    public string LastData;

    protected override void OnCreate() => CreateCount++;
    protected override void OnOpenWithData(string data) { OpenCount++; LastData = data; }
    protected override void OnClose() => CloseCount++;
}

public class TestMenuUI : BaseUI
{
    public int OpenCount;
    protected override void OnOpen(object data) => OpenCount++;
}

public class TestUnregisteredUI : BaseUI { }

/// <summary>
/// Kiểm thử chấp nhận A–H theo Brief. Chạy tại Window > General > Test Runner > EditMode.
/// Ở Edit Mode, Unity không gọi OnEnable/OnDisable và không chạy coroutine, nên hiệu ứng và trường hợp
/// "bị tắt từ bên ngoài" được kiểm tra riêng ở Play Mode.
/// </summary>
public class UIManagerTests
{
    private GameObject root;
    private RectTransform layer;
    private UIManager manager;
    private UIPrefabRegistry registry;
    private GameObject inventoryPrefab;
    private GameObject menuPrefab;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("UITestRoot", typeof(RectTransform));
        layer = new GameObject("PopupLayer", typeof(RectTransform)).GetComponent<RectTransform>();
        layer.SetParent(root.transform, false);
        manager = root.AddComponent<UIManager>();

        registry = ScriptableObject.CreateInstance<UIPrefabRegistry>();
        inventoryPrefab = new GameObject("TestInventoryUI", typeof(RectTransform), typeof(TestInventoryUI));
        menuPrefab = new GameObject("TestMenuUI", typeof(RectTransform), typeof(TestMenuUI));
        registry.Register(inventoryPrefab.GetComponent<TestInventoryUI>(), UILayer.Popup);
        registry.Register(menuPrefab.GetComponent<TestMenuUI>(), UILayer.Popup);

        manager.Configure(registry, layer, layer, layer, layer);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(inventoryPrefab);
        Object.DestroyImmediate(menuPrefab);
        Object.DestroyImmediate(registry);
    }

    private int CountInstances<T>() where T : Component => layer.GetComponentsInChildren<T>(true).Length;

    [Test]
    public void A_FirstOpen_InstantiatesExactlyOnce()
    {
        var ui = manager.Open<TestInventoryUI>();

        Assert.IsNotNull(ui);
        Assert.AreEqual(1, CountInstances<TestInventoryUI>());
        Assert.AreSame(layer, ui.transform.parent, "UI phải nằm dưới lớp đã đăng ký");
        Assert.IsTrue(ui.gameObject.activeSelf);
        Assert.IsTrue(manager.IsCreated<TestInventoryUI>());
        Assert.IsTrue(manager.IsOpen<TestInventoryUI>());
        Assert.AreEqual(1, ui.CreateCount);
        Assert.AreEqual(1, ui.OpenCount);
    }

    [Test]
    public void B_Close_DeactivatesAndKeepsInstance()
    {
        var ui = manager.Open<TestInventoryUI>();
        manager.Close<TestInventoryUI>();

        Assert.IsTrue(ui != null, "Instance không được bị Destroy");
        Assert.IsFalse(ui.gameObject.activeSelf);
        Assert.AreEqual(1, CountInstances<TestInventoryUI>());
        Assert.IsTrue(manager.IsCreated<TestInventoryUI>(), "Đã tạo nhưng đang ẩn");
        Assert.IsFalse(manager.IsOpen<TestInventoryUI>());
        Assert.AreSame(ui, manager.Get<TestInventoryUI>());
        Assert.AreEqual(1, ui.CloseCount);
    }

    [Test]
    public void C_Reopen_ReusesInstance_OnCreateOnce_OnOpenAgain()
    {
        var first = manager.Open<TestInventoryUI>();
        manager.Close<TestInventoryUI>();
        var second = manager.Open<TestInventoryUI>();

        Assert.AreSame(first, second);
        Assert.IsTrue(second.gameObject.activeSelf);
        Assert.AreEqual(1, CountInstances<TestInventoryUI>());
        Assert.AreEqual(1, second.CreateCount, "OnCreate chỉ chạy một lần");
        Assert.AreEqual(2, second.OpenCount, "OnOpen chạy mỗi lần mở");
    }

    [Test]
    public void D_RepeatedOpen_NoDuplicates()
    {
        for (int i = 0; i < 5; i++) manager.Open<TestInventoryUI>();

        Assert.AreEqual(1, CountInstances<TestInventoryUI>());
        Assert.AreEqual(1, manager.CreatedCount);
        Assert.AreEqual(1, manager.Get<TestInventoryUI>().CreateCount);
    }

    [Test]
    public void E_CloseBeforeOpen_And_RepeatedClose_AreSafe()
    {
        Assert.DoesNotThrow(() =>
        {
            manager.Close<TestInventoryUI>();
            manager.Close<TestMenuUI>();
            manager.CloseAll();
        });
        Assert.IsFalse(manager.IsCreated<TestInventoryUI>());
        Assert.AreEqual(0, CountInstances<TestInventoryUI>(), "Close không được tạo instance");

        var ui = manager.Open<TestInventoryUI>();
        manager.Close<TestInventoryUI>();
        manager.Close<TestInventoryUI>();
        Assert.AreEqual(1, ui.CloseCount, "Đóng lặp lại không chạy OnClose lần nữa");
    }

    [Test]
    public void F_MultipleTypes_CloseAll_ThenReopen()
    {
        var inventory = manager.Open<TestInventoryUI>();
        var menu = manager.Open<TestMenuUI>();
        Assert.IsTrue(manager.IsOpen<TestInventoryUI>() && manager.IsOpen<TestMenuUI>());

        manager.CloseAll();
        Assert.IsFalse(inventory.gameObject.activeSelf || menu.gameObject.activeSelf);
        Assert.IsTrue(manager.IsCreated<TestInventoryUI>() && manager.IsCreated<TestMenuUI>());

        Assert.AreSame(inventory, manager.Open<TestInventoryUI>());
        Assert.AreSame(menu, manager.Open<TestMenuUI>());
        Assert.AreEqual(2, manager.CreatedCount);

        manager.Toggle<TestMenuUI>();
        Assert.IsFalse(manager.IsOpen<TestMenuUI>(), "Toggle khi đang mở → đóng");
        manager.Toggle<TestMenuUI>();
        Assert.IsTrue(manager.IsOpen<TestMenuUI>(), "Toggle khi đang đóng → mở");
    }

    [Test]
    public void G_DataRefreshedOnEveryOpen()
    {
        var ui = manager.Open<TestInventoryUI>("A");
        Assert.AreEqual("A", ui.LastData);

        manager.Close<TestInventoryUI>();
        manager.Open<TestInventoryUI>("B");
        Assert.AreEqual("B", ui.LastData);

        manager.Open<TestInventoryUI>("C");   // Open khi đang mở: làm mới dữ liệu
        Assert.AreEqual("C", ui.LastData);

        manager.Close<TestInventoryUI>();
        manager.Open<TestInventoryUI>();
        Assert.IsNull(ui.LastData, "Mở không truyền dữ liệu thì không giữ dữ liệu cũ");

        LogAssert.Expect(LogType.Warning, new Regex("không khớp"));
        manager.Open<TestInventoryUI>(123);
        Assert.IsNull(ui.LastData, "Sai kiểu dữ liệu thì dùng giá trị mặc định");
    }

    [Test]
    public void H_MissingPrefab_FailsSafely()
    {
        LogAssert.Expect(LogType.Error, new Regex("TestUnregisteredUI"));
        var ui = manager.Open<TestUnregisteredUI>();

        Assert.IsNull(ui);
        Assert.IsFalse(manager.IsCreated<TestUnregisteredUI>());
        Assert.AreEqual(0, layer.childCount, "Không được tạo object lỗi");
    }

    [Test]
    public void Registry_RejectsDuplicateRegistration()
    {
        Assert.IsFalse(registry.Register(inventoryPrefab.GetComponent<TestInventoryUI>(), UILayer.Overlay));
        Assert.AreEqual(2, registry.Entries.Count);
    }

    [Test]
    public void Release_DestroysAndNextOpenCreatesFresh()
    {
        var first = manager.Open<TestInventoryUI>();
        manager.Release<TestInventoryUI>();

        Assert.IsFalse(manager.IsCreated<TestInventoryUI>());
        Assert.IsTrue(first == null, "Release hủy hẳn instance");

        var second = manager.Open<TestInventoryUI>();
        Assert.AreEqual(1, second.CreateCount);
        Assert.AreEqual(1, CountInstances<TestInventoryUI>());
    }
}
