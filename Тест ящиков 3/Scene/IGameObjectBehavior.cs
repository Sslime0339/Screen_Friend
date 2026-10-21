internal interface IGameObjectBehavior
{
    void Update(GameObject gameObject, Scene scene, float deltaTime);
    void OnRemoved(GameObject gameObject, Scene scene);
}
