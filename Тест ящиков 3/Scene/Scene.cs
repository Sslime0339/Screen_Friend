using System.Collections.Generic;
using System.Linq;

internal sealed class Scene
{
    private readonly GameObjectFactory _factory;
    private readonly PhysicsWorldService _physics;
    private readonly List<GameObject> _objects = new();

    public IReadOnlyList<GameObject> Objects => _objects;

    public Scene(GameObjectFactory factory, PhysicsWorldService physics)
    {
        _factory = factory;
        _physics = physics;
    }

    public GameObject CreateBox(float x, float y)
    {
        var gameObject = _factory.CreateBox(x, y);
        _objects.Add(gameObject);
        return gameObject;
    }

    public void Clear()
    {
        _physics.ClearBoxes();
        _objects.Clear();
    }

    /// <summary>
    /// Удаляет из списка объекты, тела которых были удалены физическим миром.
    /// Нужен как страховка при будущих сценариях удаления отдельных объектов.
    /// </summary>
    public void RemoveObjectsWithoutBodies()
    {
        var liveBodies = _physics.Boxes.ToHashSet();
        _objects.RemoveAll(item => !liveBodies.Contains(item.Body));
    }
}
