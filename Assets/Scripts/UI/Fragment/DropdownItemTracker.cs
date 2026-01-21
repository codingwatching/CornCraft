using UnityEngine;
using UnityEngine.UI;
using CraftSharp;

namespace CraftSharp.UI
{
    public class DropdownItemTracker : MonoBehaviour
    {
        public string LoginName = string.Empty;
        public UserProfileType ProfileType = UserProfileType.Offline;
        public Button RemoveButton;
    }
}