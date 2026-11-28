using System;
using System.Collections.Generic;
using System.Text;

namespace VirtualFriend.Entities
{
    internal class ObjectStorage
    {
        private readonly List<GameObject> _objects = new();
        public IReadOnlyList<GameObject> Objects => _objects;

        public void Add(GameObject obj)
        {
            if (!_objects.Contains(obj))
                _objects.Add(obj);
        }

        public void Remove(GameObject obj)
        {
            _objects.Remove(obj);
        }

        public void SortByDepth()
        {
            _objects.Sort((a, b) =>
            {
                int layerCompare = a.Layer.CompareTo(b.Layer);
                return layerCompare != 0 ? layerCompare : a.ZIndex.CompareTo(b.ZIndex);
            });
        }

        public void Clear()
        {
            _objects.Clear();
        }
    }

}
