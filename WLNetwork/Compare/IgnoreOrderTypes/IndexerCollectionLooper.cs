using System;
using System.Collections;
using System.Reflection;

namespace KellermanSoftware.CompareNetObjects.IgnoreOrderTypes
{
    internal class IndexerCollectionLooper : IEnumerable
    {
        private readonly int _cnt;
        private readonly object _indexer;
        private readonly PropertyInfo _info;

        public IndexerCollectionLooper(object obj, PropertyInfo info, int cnt)
        {
            _indexer = obj;
            if (info == null)
                throw new ArgumentNullException("info");

            _info = info;
            _cnt = cnt;
        }

        public IEnumerator GetEnumerator()
        {
            for (var i = 0; i < _cnt; i++)
            {
                object value = _info.GetValue(_indexer, new object[] {i});
                yield return value;
            }
        }
    }
}