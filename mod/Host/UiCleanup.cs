#if HAS_CITIES
using System.Collections.Generic;
using ColossalFramework.UI;

namespace FinanceOverhaul
{
    /// <summary>
    /// Removes mod-made UI left by an older assembly (hot reload) without looping on
    /// UIComponent.Find: Find matches a name cached at creation, so renaming a component
    /// does not hide it from Find, and "find, rename, destroy, repeat" never ends (it froze
    /// the game). Snapshot the children, detach each match, then destroy it.
    /// </summary>
    internal static class UiCleanup
    {
        public static int RemoveChildrenWhere(
            UIComponent parent,
            System.Predicate<UIComponent> match
        )
        {
            if (parent == null)
            {
                return 0;
            }

            var doomed = new List<UIComponent>();
            foreach (UIComponent child in parent.components)
            {
                if (child != null && match(child))
                {
                    doomed.Add(child);
                }
            }

            foreach (UIComponent c in doomed)
            {
                parent.RemoveUIComponent(c);
                UnityEngine.Object.Destroy(c.gameObject);
            }

            return doomed.Count;
        }

        public static int RemoveChildrenNamed(UIComponent parent, params string[] names)
        {
            if (parent == null)
            {
                return 0;
            }

            var doomed = new List<UIComponent>();
            foreach (UIComponent child in parent.components)
            {
                if (child == null)
                {
                    continue;
                }

                for (int i = 0; i < names.Length; i++)
                {
                    if (child.cachedName == names[i] || child.name == names[i])
                    {
                        doomed.Add(child);
                        break;
                    }
                }
            }

            foreach (UIComponent c in doomed)
            {
                parent.RemoveUIComponent(c);
                UnityEngine.Object.Destroy(c.gameObject);
            }

            return doomed.Count;
        }
    }
}
#endif
