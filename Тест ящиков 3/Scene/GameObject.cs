using Genbox.VelcroPhysics.Dynamics;

internal enum GameObjectKind
{
    Box
}

/// <summary>
/// Игровой объект сцены. Физическое состояние хранится в Body,
/// поведение объекта отделено через IGameObjectBehavior.
/// </summary>
internal sealed class GameObject
{
    public string Id { get; }
    public GameObjectKind Kind { get; }
    public Body Body { get; }
    public float Width { get; }
    public float Height { get; }
    public IGameObjectBehavior Behavior { get; }

    public GameObject(
        string id,
        GameObjectKind kind,
        Body body,
        float width,
        float height,
        IGameObjectBehavior behavior)
    {
        Id = id;
        Kind = kind;
        Body = body;
        Width = width;
        Height = height;
        Behavior = behavior;
    }

    public void Update(Scene scene, float deltaTime) =>
        Behavior.Update(this, scene, deltaTime);

    public void NotifyRemoved(Scene scene) =>
        Behavior.OnRemoved(this, scene);
}
