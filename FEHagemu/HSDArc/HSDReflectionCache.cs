using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace FEHagemu.HSDArchive
{
    public readonly struct FieldMeta
    {
        public readonly FieldInfo Field;
        public readonly HSDFieldAttribute Attr;

        public FieldMeta(FieldInfo field, HSDFieldAttribute attr)
        {
            Field = field;
            Attr = attr;
        }
    }

    public static class HSDReflectionCache
    {
        private static readonly ConcurrentDictionary<Type, FieldMeta[]> _cache = new();

        public static FieldMeta[] GetFieldMetas(Type type)
        {
            return _cache.GetOrAdd(type, static t =>
            {
                var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
                var list = new List<FieldMeta>(fields.Length);
                foreach (var field in fields)
                {
                    var attr = field.GetCustomAttribute<HSDFieldAttribute>();
                    if (attr != null)
                        list.Add(new FieldMeta(field, attr));
                }
                return list.ToArray();
            });
        }
    }
}
