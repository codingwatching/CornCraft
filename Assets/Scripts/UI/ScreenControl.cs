using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CraftSharp.UI
{
    public class ScreenControl : MonoBehaviour
    {
        private BaseCornClient client;
        private readonly Stack<BaseScreen> screenStack = new();

        [SerializeField] private ChatScreen m_ChatScreen;
        [SerializeField] private InventoryScreen m_InventoryScreen;
        [SerializeField] private DeathScreen m_DeathScreen;
        [SerializeField] private SignEditorScreen m_SignEditorScreen;
        [SerializeField] private HUDScreen m_HUDScreen;
        [SerializeField] private PacketScreen m_PacketScreen;
        [SerializeField] private LoadingScreen m_LoadingScreen;
        [SerializeField] private PauseScreen m_PauseScreen;

        private readonly Dictionary<Type, BaseScreen> screenRegistry = new();

        /// <summary>
        /// Should be called on client start
        /// </summary>
        public void SetClient(BaseCornClient curClient)
        {
            client = curClient;

            // Initialize screens
            screenRegistry.Add(typeof (ChatScreen),       m_ChatScreen);
            screenRegistry.Add(typeof (InventoryScreen),  m_InventoryScreen);
            screenRegistry.Add(typeof (DeathScreen),      m_DeathScreen);
            screenRegistry.Add(typeof (SignEditorScreen), m_SignEditorScreen);
            screenRegistry.Add(typeof (HUDScreen),        m_HUDScreen);
            screenRegistry.Add(typeof (PacketScreen),     m_PacketScreen);
            screenRegistry.Add(typeof (LoadingScreen),    m_LoadingScreen);
            screenRegistry.Add(typeof (PauseScreen),      m_PauseScreen);

            // Push HUD Screen on start, before pushing Loading Screen
            PushScreen<HUDScreen>();
        }

        public T PushScreen<T>() where T : BaseScreen
        {
            if (screenRegistry.ContainsKey(typeof (T)))
            {
                var screen = screenRegistry[typeof (T)];
                screen.EnsureInitialized();

                // Deactivate previous top screen if present
                if (screenStack.Count > 0)
                {
                    screenStack.Peek().IsActive = false;
                }
                
                // Push and activate new top screen
                screenStack.Push(screen);
                screen.IsActive = true;

                // Move this screen to the top
                screen.transform.SetAsLastSibling();

                UpdateScreenStates();

                return (T) screen;
            }
            Debug.LogWarning($"Screen type [{typeof (T)}] is not registered!");

            return null;
        }

        public void SetScreenData<T>(Action<T> callback) where T : BaseScreen
        {
            if (screenRegistry.ContainsKey(typeof (T)))
            {
                var screen = screenRegistry[typeof (T)];
                screen.EnsureInitialized();

                callback.Invoke((T) screen);
            }
            else
            {
                Debug.LogWarning($"Screen type [{typeof (T)}] is not registered!");
            }
        }

        public void TryPopScreen()
        {
            if (screenStack.Count <= 0)
                Debug.LogError("Trying to pop an already empty screen stack!");

            // Deactivate and pop previous top screen
            BaseScreen screen2Pop = screenStack.Peek();

            screen2Pop.IsActive = false;
            screenStack.Pop();

            // Push and activate new top screen
            if (screenStack.Count > 0)
                screenStack.Peek().IsActive = true;

            UpdateScreenStates();
        }

        private void UpdateScreenStates()
        {
            // Get States
            bool releaseCursor = screenStack.Count > 0 && screenStack.Peek().ReleaseCursor();
            bool pauseControllerInput = screenStack.Aggregate(false,
                (current, w) => current || w.ShouldPauseControllerInput());

            //Debug.Log($"In window stack: {string.Join(' ', screenStack)}");

            // Update States
            if (client!.ControllerInputPaused != pauseControllerInput)
            {
                client.SetControllerInputPaused(pauseControllerInput);
            }

            Cursor.lockState = releaseCursor ? CursorLockMode.None : CursorLockMode.Locked;
        }

        public void SetLoadingScreen(bool loading)
        {
            if (loading)
            {
                if (screenStack.Count > 0 && screenStack.Peek() is LoadingScreen)
                {
                    // Do nothing...
                }
                else
                {
                    PushScreen<LoadingScreen>();
                }
            }
            else
            {
                if (screenStack.Count > 0 && screenStack.Peek() is LoadingScreen)
                {
                    TryPopScreen();
                }
            }
        }

        private void Update()
        {
            if (screenStack.Count > 0)
            {
                var topScreen = screenStack.Peek();
                
                // Update screen at stack top
                topScreen.UpdateScreen();
            }
        }
    }
}
