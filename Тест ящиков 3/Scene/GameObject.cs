using Genbox.VelcroPhysics.Dynamics;

internal enum GameObjectKind
{
    Box
}

/// <summary>
/// Игровой объект сцены. На этом этапе физическое состояние хранится в Body,
/// а GameObject содержит идентичность и параметры отображения.
/// </summary>
internal sealed class GameObject
{
    public string Id { get; }
    public GameObjectKind Kind { get; }
    public Body Body { get; }
    public float Width { get; }
    public float Height { get; }

    public GameObject(string id, GameObjectKind kind, Body body, float width, float height)
    {
        Id = id;
        Kind = kind;
        Body = body;
        Width = width;
        Height = height;
    }
}
