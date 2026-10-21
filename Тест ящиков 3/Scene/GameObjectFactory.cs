using System;

internal sealed class GameObjectFactory
{
    private readonly PhysicsWorldService _physics;

    public GameObjectFactory(PhysicsWorldService physics)
    {
        _physics = physics;
    }

    public GameObject CreateBox(float x, float y)
    {
        var body = _physics.SpawnBox(x, y);
        return new GameObject(
            Guid.NewGuid().ToString("N"),
            GameObjectKind.Box,
            body,
            _physics.BoxSize,
            _physics.BoxSize,
            new EmptyBehavior()
        );
    }
}
