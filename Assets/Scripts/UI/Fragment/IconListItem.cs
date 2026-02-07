using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CraftSharp.UI
{
    public class IconListItem : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text descriptionLeft;
        [SerializeField] private TMP_Text descriptionRight;
        [SerializeField] private Button rootButton;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = Color.cyan;

        public void SetIcon(Sprite sprite)
        {
            if (iconImage != null)
                iconImage.sprite = sprite;
        }

        public void SetDescriptions(string left, string right)
        {
            if (descriptionLeft != null && left != null)
                descriptionLeft.text = left;

            if (descriptionRight != null && right != null)
                descriptionRight.text = right;
        }

        public void AddClickListener(UnityAction action)
        {
            if (rootButton != null && action != null)
                rootButton.onClick.AddListener(action);
        }

        public void ClearClickListeners()
        {
            if (rootButton != null)
                rootButton.onClick.RemoveAllListeners();
        }

        public void SetSelected(bool selected)
        {
            if (rootButton != null)
            {
                rootButton.image.color = selected ? selectedColor : normalColor;
            }
        }

        private void Start()
        {
            // Ensure the item starts in normal state
            SetSelected(false);
        }
    }
}