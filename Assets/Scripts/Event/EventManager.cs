using System;
using System.Collections.Generic;
using UnityEngine;

namespace CraftSharp.Event
{
    public interface IEventListener
    {
        void RebindEventListeners();
    }

    // Singleton Event Manager
    public class EventManager
    {
        private static EventManager instance;

        public static EventManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new EventManager();
                }
                return instance;
            }
        }
        
        private Dictionary<Type, IEventRegistrations> eventTable = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            instance = new EventManager();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RebindRetainedListeners()
        {
            foreach (var behaviour in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (behaviour.gameObject.scene.IsValid() && behaviour.gameObject.scene.isLoaded &&
                    behaviour is IEventListener listener)
                {
                    listener.RebindEventListeners();
                }
            }
        }

        public void Register<T>(Action<T> callback)
        {
            var t = typeof (T);

            if (!eventTable.ContainsKey(t))
            {
                var registrations = new EventRegistrations<T>();
                registrations.actions += callback;
                eventTable.Add(t, registrations);
            }
            else
            {
                var registrations = eventTable[t] as EventRegistrations<T>;
                registrations.actions -= callback;
                registrations.actions += callback;
            }
        }

        public void Unregister<T>(Action<T> callback)
        {
            var t = typeof (T);

            if (eventTable.ContainsKey(t) && eventTable[t] != null)
            {
                var registrations = eventTable[t] as EventRegistrations<T>;
                if (registrations.actions != null)
                {
                    registrations.actions -= callback;
                }
            }
        }

        public void Broadcast<T>(T message)
        {
            Type t = typeof (T);

            if (eventTable.ContainsKey(t) && eventTable[t] != null)
            {
                var registrations = eventTable[t] as EventRegistrations<T>;

                registrations.actions?.Invoke(message);
            }
        }

        public void BroadcastOnUnityThread<T>(T message)
        {
            Type t = typeof (T);

            if (eventTable.ContainsKey(t) && eventTable[t] != null)
            {
                var registrations = eventTable[t] as EventRegistrations<T>;

                Loom.QueueOnMainThread(() => registrations.actions?.Invoke(message));
            }
        }
    }

}
