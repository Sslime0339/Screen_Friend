internal sealed class EmptyBehavior : IGameObjectBehavior
{
    public void Update(GameObject gameObject, Scene scene, float deltaTime) { }
    public void OnRemoved(GameObject gameObject, Scene scene) { }
}
