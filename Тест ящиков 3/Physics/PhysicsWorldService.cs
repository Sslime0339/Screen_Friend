using Genbox.VelcroPhysics.Dynamics;
using Genbox.VelcroPhysics.Factories;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class PhysicsWorldService : IDisposable
{
    private const float BoxSizeMeters = 1.2f;
    private readonly World _world;
    private readonly List<Body> _boxes = new();
    private readonly List<Body> _windowColliders = new();
    private readonly float _pixelsPerMeter;
    private readonly int _screenWidth;
    private readonly int _screenHeight;
    private WindowData[] _lastWindowSnapshot = Array.Empty<WindowData>();
    private bool _disposed;

    public IReadOnlyList<Body> Boxes => _boxes;
    public IReadOnlyList<Body> WindowColliders => _windowColliders;
    public World World => _world;
    public float BoxSize => BoxSizeMeters;

    public PhysicsWorldService(int screenWidth, int screenHeight, float pixelsPerMeter)
    {
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;
        _pixelsPerMeter = pixelsPerMeter;
        _world = new World(new Vector2(0f, 19.6f));
        CreateScreenBounds();
    }

    public void SetGravity(float gravityY)
    {
        _world.Gravity = new Vector2(0f, gravityY);
    }

    public void SpawnBox(float x, float y)
    {
        var body = BodyFactory.CreateRectangle(
            _world, BoxSizeMeters, BoxSizeMeters, 2f, new Vector2(x, y));
        body.BodyType = BodyType.Dynamic;
        body.Restitution = 0.2f;
        body.Friction = 0.6f;
        _boxes.Add(body);
    }

    public void ClearBoxes()
    {
        foreach (var box in _boxes)
            _world.DestroyBody(box);

        _boxes.Clear();
    }

    public Body? FindBoxAt(float x, float y)
    {
        float half = BoxSizeMeters / 2f;
        for (int i = _boxes.Count - 1; i >= 0; i--)
        {
            Body box = _boxes[i];
            if (x >= box.Position.X - half && x <= box.Position.X + half &&
                y >= box.Position.Y - half && y <= box.Position.Y + half)
                return box;
        }

        return null;
    }

    public bool ContainsBox(Body body) => _boxes.Contains(body);

    public void SetWindowObstacles(IReadOnlyList<WindowData> windows)
    {
        if (windows.Count == _lastWindowSnapshot.Length &&
            windows.SequenceEqual(_lastWindowSnapshot))
            return;

        foreach (var collider in _windowColliders)
            _world.DestroyBody(collider);
        _windowColliders.Clear();

        foreach (var window in windows)
        {
            Body body = BodyFactory.CreateRectangle(
                _world,
                window.Width,
                window.Height,
                1f,
                new Vector2(window.CenterX, window.CenterY));

            body.BodyType = BodyType.Static;
            body.Friction = 0.4f;
            body.Restitution = 0.2f;
            _windowColliders.Add(body);
        }

        _lastWindowSnapshot = windows.ToArray();
    }

    public void Step(float dt) => _world.Step(dt);

    private void CreateScreenBounds()
    {
        float viewWidthMeters = _screenWidth / _pixelsPerMeter;
        float viewHeightMeters = _screenHeight / _pixelsPerMeter;
        const float wallThickness = 5f;
        const float groundHeightMeters = 5f;

        Body ground = BodyFactory.CreateRectangle(
            _world,
            viewWidthMeters * 1.2f,
            groundHeightMeters,
            1f,
            new Vector2(viewWidthMeters / 2f, viewHeightMeters + groundHeightMeters / 2f));
        ground.BodyType = BodyType.Static;
        ground.Friction = 0.6f;

        Body leftWall = BodyFactory.CreateRectangle(
            _world, wallThickness, viewHeightMeters * 8, 1f,
            new Vector2(-wallThickness / 2f, viewHeightMeters / 2f));
        leftWall.BodyType = BodyType.Static;
        leftWall.Friction = 0.4f;
        leftWall.Restitution = 0.3f;

        Body rightWall = BodyFactory.CreateRectangle(
            _world, wallThickness, viewHeightMeters * 8, 1f,
            new Vector2(viewWidthMeters + wallThickness / 2f, viewHeightMeters / 2f));
        rightWall.BodyType = BodyType.Static;
        rightWall.Friction = 0.4f;
        rightWall.Restitution = 0.3f;
    }

    public void Dispose()
    {
        if (_disposed) return;
        ClearBoxes();

        foreach (var collider in _windowColliders)
            _world.DestroyBody(collider);
        _windowColliders.Clear();

        _disposed = true;
    }
}
