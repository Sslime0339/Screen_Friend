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

    public void Update(float deltaTime)
    {
        // Snapshot позволяет поведению создавать/удалять объекты без изменения
        // коллекции, по которой прямо сейчас идёт итерация.
        foreach (var gameObject in _objects.ToArray())
        {
            if (_objects.Contains(gameObject))
                gameObject.Update(this, deltaTime);
        }
    }

    public bool Remove(GameObject gameObject)
    {
        if (!_objects.Contains(gameObject))
            return false;

        gameObject.NotifyRemoved(this);
        _physics.RemoveBox(gameObject.Body);
        _objects.Remove(gameObject);
        return true;
    }

    public GameObject? FindAt(float x, float y)
    {
        var body = _physics.FindBoxAt(x, y);
        return body == null ? null : _objects.LastOrDefault(item => item.Body == body);
    }

    public void Clear()
    {
        foreach (var gameObject in _objects.ToArray())
            gameObject.NotifyRemoved(this);

        _physics.ClearBoxes();
        _objects.Clear();
    }

    public void RemoveObjectsWithoutBodies()
    {
        var liveBodies = _physics.Boxes.ToHashSet();
        _objects.RemoveAll(item => !liveBodies.Contains(item.Body));
    }
}
