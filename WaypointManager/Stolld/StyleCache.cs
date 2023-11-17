
using UnityEngine;

namespace UI
{
    /// <summary>
    /// The unity default style can only be accessed inside of an OnGUI method
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class StyleCache : MonoBehaviour
    {
        public static UISkinDef Skin { get; set; }

        void OnGUI()
        {
            if (Skin == null)
            {
                Skin = StyleConverter.Convert(GUI.skin);
                Destroy(this);
            }
        }
    }
}
