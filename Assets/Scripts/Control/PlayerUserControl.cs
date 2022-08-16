using UnityEngine;
using MinecraftClient.UI;

namespace MinecraftClient.Control
{
    [RequireComponent(typeof (PlayerController))]
    [AddComponentMenu("Unicorn/Control/Player User Control")]
    public class PlayerUserControl : MonoBehaviour
    {
        private CornClient game;
        private PlayerController player;
        private bool walkMode = false;

        private float getValue(bool pos, bool neg)
        {
            return (pos ? 1F : 0F) + (neg ? -1F : 0F);
        }

        void Start()
        {
            game = CornClient.Instance;
            player = GetComponent<PlayerController>();
        }

        void Update()
        {
            bool playerReady = player.DisableAnim || game.EnvironmentLoaded();

            if (game.IsPaused() || !playerReady) return;

            float h = getValue(Input.GetKey(KeyCode.D), Input.GetKey(KeyCode.A));
            float v = getValue(Input.GetKey(KeyCode.W), Input.GetKey(KeyCode.S));

            bool attack = Input.GetButton("Attack");
            bool up = Input.GetButton("GoUp");
            bool down = Input.GetButton("GoDown");

            // Check Walk / Run Mode
            if (Input.GetKeyDown(KeyCode.LeftControl))
            {
                walkMode = !walkMode;
                CornClient.ShowNotification(walkMode ? "Walk Mode" : "Rush Mode");
            }

            player.Tick(Time.deltaTime, h, v, walkMode, attack, up, down);
        }
    }
}