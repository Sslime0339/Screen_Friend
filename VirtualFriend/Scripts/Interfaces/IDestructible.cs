namespace VirtualFriend.Scripts.Interfaces
{
    internal interface IDestructible
    {
        float MaxHealth { get; }
        float CurrentHealth { get; }
        void TakeDamage(float amount);
        void DestroyObject();
    }
}
