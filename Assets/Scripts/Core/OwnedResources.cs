using System.Collections.Generic;
using UnityEngine;

namespace PokaGoryu
{
    /// <summary>Only resources created for this owner; cached/shared materials must not be registered.</summary>
    public sealed class OwnedResources : MonoBehaviour
    {
        readonly List<Object> owned = new List<Object>();
        public static T Keep<T>(GameObject owner, T resource) where T : Object
        {
            if (resource == null) return resource;
            OwnedResources bag = owner.GetComponent<OwnedResources>();
            if (bag == null) bag = owner.AddComponent<OwnedResources>();
            if (!bag.owned.Contains(resource)) bag.owned.Add(resource);
            return resource;
        }
        void OnDestroy()
        {
            for (int i = 0; i < owned.Count; i++) if (owned[i] != null) Destroy(owned[i]);
            owned.Clear();
        }
    }
}
