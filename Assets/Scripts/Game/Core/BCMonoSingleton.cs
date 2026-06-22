using UnityEngine;

namespace Game.Core
{
    public abstract class BCMonoSingleton<T> : MonoBehaviour where T : BCMonoSingleton<T>
    {
        public static T Instance { get; private set; }

        protected virtual void Awake()
        {
            if(Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = (T)this;
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

        }
    }
}
