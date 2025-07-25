using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

using CraftSharp.Event;

namespace CraftSharp.UI
{
    [RequireComponent(typeof (CanvasGroup))]
    public class ChatScreen : BaseScreen
    {
        private const int MAX_CHAT_MESSAGES = 100;
        private const string COMMAND_PREFIX = "/";
        private bool isActive;

        public override bool IsActive
        {
            set {
                isActive = value;
                screenGroup.alpha = value ? 1F : 0F;
                screenGroup.blocksRaycasts = value;
                screenGroup.interactable   = value;
                // Focus chat input on enter chat screen
                if (value)
                {
                    chatInput.text = string.Empty;
                    chatInput.ActivateInputField();
                }
            }

            get => isActive;
        }

        // UI controls and objects
        [SerializeField] private AutoCompletedInputField chatInput;
        [SerializeField] private RectTransform chatContentPanel;
        [SerializeField] private GameObject chatMessagePrefab;
        [SerializeField] private RectTransform cursorTextPanel;
        [SerializeField] private TMP_Text cursorText;
        
        private readonly Queue<TMP_Text> chatMessages = new();
        private CanvasGroup screenGroup;
        private RectTransform screenRect;

        // Chat message data
        private readonly List<string> sentChatHistory = new();
        private int chatIndex;
        private string chatBuffer = string.Empty;

        public override bool ReleaseCursor()
        {
            return true;
        }

        public override bool ShouldPauseControllerInput()
        {
            return true;
        }

        public void InputCommandPrefix()
        {
            chatInput.ClearCompletionOptions();
            
            chatInput.SetTextWithoutNotify(COMMAND_PREFIX);
            chatInput.caretPosition = COMMAND_PREFIX.Length;

            // Workaround for IMEs. See https://github.com/DevBobcorn/CornCraft/issues/25
            StartCoroutine(AutoCompletedInputField.SimpleWait(null, () =>
            {
                // Wait till next frame
                chatInput.SetTextWithoutNotify(COMMAND_PREFIX);
                chatInput.caretPosition = COMMAND_PREFIX.Length;
            }));
        }

        private int lastTextUpdateFrameCount = -1;

        private void OnChatInputTextChange(string chatInputText)
        {
            // Workaround for TMP's pasting problem. See https://discussions.unity.com/t/tmp-inputfield-insert-is-very-badly-optimized/1513475
            if (Time.frameCount == lastTextUpdateFrameCount)
            {
                return;
            }
            lastTextUpdateFrameCount = Time.frameCount;
            
            if (chatInputText.StartsWith(COMMAND_PREFIX) && chatInputText.Length > COMMAND_PREFIX.Length)
            {
                string requestText;
                if (chatInput.caretPosition > 0 && chatInput.caretPosition < chatInputText.Length)
                    requestText = chatInputText[..chatInput.caretPosition];
                else
                    requestText = chatInputText;

                // Request command auto complete
                var client = CornApp.CurrentClient;
                if (!client) return;

                client.SendAutoCompleteRequest(requestText);

                //Debug.Log($"Requesting auto completion: [{requestText}]");
            }
            else
            {
                chatInput.ClearCompletionOptions();
            }
        }

        private void SendChatMessage()
        {
            if (chatInput.text.Trim() == string.Empty)
                return;
            
            string chat = chatInput.text;
            // Send if client exists...
            var client = CornApp.CurrentClient;
            if (client)
            {
                client.TrySendChat(chat);
            }
            
            chatInput.SetTextWithoutNotify(string.Empty);
            chatInput.ClearCompletionOptions();

            StartCoroutine(AutoCompletedInputField.SimpleWait(null, () =>
            {
                // Wait till next frame
                chatInput.ActivateInputField();
            }));

            // Remove the chat text from previous history if present
            if (sentChatHistory.Contains(chat))
                sentChatHistory.Remove(chat);

            sentChatHistory.Add(chat);
            chatIndex = sentChatHistory.Count;
        }

        private void PrevChatMessage()
        {
            if (sentChatHistory.Count > 0 && chatIndex - 1 >= 0)
            {
                if (chatIndex == sentChatHistory.Count)
                {   // Store to buffer...
                    chatBuffer = chatInput.text;
                }
                chatIndex--;

                // Don't notify before we set the caret position
                chatInput.SetTextWithoutNotify(sentChatHistory[chatIndex]);
                chatInput.ClearCompletionOptions();

                chatInput.caretPosition = sentChatHistory[chatIndex].Length;
                StartCoroutine(AutoCompletedInputField.SimpleWait(null, () =>
                {
                    // Wait till next frame
                    chatInput.caretPosition = sentChatHistory[chatIndex].Length;
                }));
            }
        }

        private void NextChatMessage()
        {
            if (sentChatHistory.Count > 0 && chatIndex < sentChatHistory.Count)
            {
                chatIndex++;
                if (chatIndex == sentChatHistory.Count)
                {
                    // Restore buffer... Don't notify before we set the caret position
                    chatInput.SetTextWithoutNotify(chatBuffer);
                    chatInput.ClearCompletionOptions();
                    
                    chatInput.caretPosition = chatBuffer.Length;
                    StartCoroutine(AutoCompletedInputField.SimpleWait(null, () =>
                    {
                        // Wait till next frame
                        chatInput.caretPosition = chatBuffer.Length;
                    }));

                    // Refresh auto-completion options
                    OnChatInputTextChange(chatBuffer);
                }
                else
                {
                    // Don't notify before we set the caret position
                    chatInput.SetTextWithoutNotify(sentChatHistory[chatIndex]);
                    chatInput.ClearCompletionOptions();
                    
                    chatInput.caretPosition = sentChatHistory[chatIndex].Length;
                    StartCoroutine(AutoCompletedInputField.SimpleWait(null, () =>
                    {
                        // Wait till next frame
                        chatInput.caretPosition = sentChatHistory[chatIndex].Length;
                    }));
                }
            }
        }

        #nullable enable

        private Action<ChatMessageEvent>? chatMessageCallback;
        private Action<AutoCompletionEvent>? autoCompleteCallback;

        #nullable disable

        private void HandleTextClickAction(string clickAction, string clickValue)
        {
            Debug.Log($"Click on action [{clickAction}: {clickValue}]");

            switch (clickAction)
            {
                case "open_url":
                    Protocol.Microsoft.OpenBrowser(clickValue);
                    break;
                case "open_file":
                    
                    break;
                case "run_command":
                    // Not supported for chat message. Only works with root component on a sign
                    break;
                case "suggest_command":
                    // Don't notify before we set the caret position
                    chatInput.SetTextWithoutNotify(clickValue);
                    chatInput.caretPosition = clickValue.Length;
                    chatInput.ClearCompletionOptions();
                    
                    OnChatInputTextChange(clickValue);
                    break;
                case "change_page":
                    // Not supported for chat message. Only works within book screen
                    break;
                case "copy_to_clipboard":
                    GUIUtility.systemCopyBuffer = clickValue;
                    CornApp.Notify(Translations.Get("login.link_copied"), Notification.Type.Success);
                    break;
            }
        }
        
        private void UpdateCursorText(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                cursorTextPanel.gameObject.SetActive(false);
            }
            else
            {
                cursorText.text = str;
                cursorTextPanel.gameObject.SetActive(true);
                
                LayoutRebuilder.ForceRebuildLayoutImmediate(cursorTextPanel);
            }
        }

        protected override void Initialize()
        {
            // Initialize controls and add listeners
            screenGroup = GetComponent<CanvasGroup>();
            screenRect = GetComponent<RectTransform>();

            if (chatMessages.Count > 0)
            {
                foreach (var chatMessage in chatMessages)
                {
                    Destroy(chatMessage.gameObject);
                }
                chatMessages.Clear();
            }

            chatInput.onValueChanged.AddListener(OnChatInputTextChange);
            
            // Hide cursor text
            cursorTextPanel.gameObject.SetActive(false);

            // Register callbacks
            chatMessageCallback = e =>
            {
                var styledMessage = TMPConverter.MC2TMP(e.Message);
                var chatMessageObj = Instantiate(chatMessagePrefab, chatContentPanel);
                
                var chatMessage = chatMessageObj.GetComponent<TMP_Text>();
                chatMessage.text = styledMessage;

                if (e.Actions is not null && e.Actions.Length > 0)
                {
                    var game = CornApp.CurrentClient;
                    if (!game) return;
                    
                    var chatMessageInteractable = chatMessageObj.AddComponent<ChatMessageInteractable>();
                    chatMessageInteractable.SetupInteractable(chatMessage, e.Actions,
                        HandleTextClickAction, UpdateCursorText, game.UICamera);
                }
                
                chatMessages.Enqueue(chatMessage);

                while (chatMessages.Count > MAX_CHAT_MESSAGES)
                {
                    // Dequeue and destroy
                    Destroy(chatMessages.Dequeue().gameObject);
                }
            };

            autoCompleteCallback = e =>
            {
                if (e.Options.Length > 0)
                {   // Show at most 20 options
                    var completionOptions = e.Options;
                    var completionStart = e.Start;
                    var completionLength = e.Length;
                    var completionSelectedIndex = 0; // Select first option

                    //Debug.Log($"Received completions: s{completionStart} l{completionLength} [{string.Join(", ", completionOptions)}]");

                    chatInput.SetCompletionOptions(completionOptions, completionSelectedIndex, completionStart, completionLength);
                }
                else // No option available
                {
                    chatInput.ClearCompletionOptions();
                }
            };

            chatInput.m_OnUpArrowKeyNotConsumedByCompletionSelection.AddListener(PrevChatMessage);
            chatInput.m_OnDownArrowKeyNotConsumedByCompletionSelection.AddListener(NextChatMessage);

            EventManager.Instance.Register(chatMessageCallback);
            EventManager.Instance.Register(autoCompleteCallback);
        }

        private void OnDestroy()
        {
            if (chatMessageCallback is not null)
                EventManager.Instance.Unregister(chatMessageCallback);
            
            if (autoCompleteCallback is not null)
                EventManager.Instance.Unregister(autoCompleteCallback);

        }

        public override void UpdateScreen()
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                var client = CornApp.CurrentClient;
                if (client)
                {
                    client.ScreenControl.TryPopScreen();
                }
                return;
            }
            
            var game = CornApp.CurrentClient;
            if (!game) return;

            // Update cursor text position
            var mousePos = Mouse.current.position.value;
            
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                screenRect, mousePos, game.UICamera, out Vector2 newPos);
            
            var tipPos = new Vector2(
                Mathf.Min(screenRect.rect.width / 2 - cursorTextPanel.rect.width, newPos.x),
                Mathf.Max(cursorTextPanel.rect.height - screenRect.rect.height / 2, newPos.y) );
            
            tipPos = transform.TransformPoint(tipPos);

            // Don't modify z coordinate
            cursorTextPanel.position = new Vector3(tipPos.x, tipPos.y, cursorTextPanel.position.z);

            if (chatInput.IsActive())
            {
                if (Keyboard.current.enterKey.wasPressedThisFrame ||
                    Keyboard.current.numpadEnterKey.wasPressedThisFrame)
                {
                    SendChatMessage();
                }

                if (Keyboard.current.tabKey.wasPressedThisFrame)
                {
                    chatInput.PerformCompletion(OnChatInputTextChange);
                }
            }
        }
    }
}
