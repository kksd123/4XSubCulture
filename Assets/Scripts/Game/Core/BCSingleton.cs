using Unity.Entities;
using UnityEngine.InputSystem.Utilities;

namespace Game.Core
{
    public interface IBCSingleton : IComponentData
    { }

    public static class BCSingleton<T> where T : unmanaged, IBCSingleton
    {
        public static Entity Ensure(EntityManager em, in T initial = default)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if(!query.IsEmptyIgnoreFilter)
            {
                return query.GetSingletonEntity();
            }

            var entity = em.CreateEntity(typeof(T));
            em.SetComponentData(entity, initial);
            return entity;
        }

        public static bool TryGet(EntityManager em, out T value)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if(query.IsEmptyIgnoreFilter)
            {
                value = default;
                return false;
            }

            value = query.GetSingleton<T>();
            return true;
        }

        public static T Get(EntityManager em)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.GetSingleton<T>();
        }

        public static void Set(EntityManager em, T value)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            query.SetSingleton<T>(value);
        }
    }
}
