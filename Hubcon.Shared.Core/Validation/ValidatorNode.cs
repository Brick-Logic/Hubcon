using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Hubcon.Validation
{
    public sealed class ValidatorNode<T>
    {
        private readonly PropertyEntry[] _entries;

        private ValidatorNode(PropertyEntry[] entries) => _entries = entries;

        public bool TryValidate(T instance, List<ValidationResult> results, string? pathPrefix = null)
        {
            if (_entries.Length == 0) return true;
            
            int before = results.Count;

            foreach (ref readonly var entry in _entries.AsSpan())
                entry.Validate(instance, results, pathPrefix);

            return results.Count == before;
        }

        private readonly struct PropertyEntry
        {
            private readonly string _memberName;
            private readonly Func<T, object?> _getter;
            private readonly ValidationAttribute[] _attributes;
            private readonly Action<object, List<ValidationResult>, string?>? _childValidate;

            public PropertyEntry(string memberName,
                Func<T, object?> getter,
                ValidationAttribute[] attributes,
                Action<object, List<ValidationResult>, string?>? childValidate)
            {
                _memberName = memberName;
                _getter = getter;
                _attributes = attributes;
                _childValidate = childValidate;
            }

            internal void Validate(T owner, List<ValidationResult> results, string? pathPrefix)
            {
                var value = _getter(owner);
                
                var fullPath = pathPrefix is null
                    ? _memberName
                    : string.Concat(pathPrefix, ".", _memberName);

                if (_attributes.Length > 0)
                {
                    var ctx = new ValidationContext(owner) { MemberName = _memberName };

                    int mark = results.Count;

                    Validator.TryValidateValue(value!, ctx, results, _attributes);
                    
                    for (int i = mark; i < results.Count; i++)
                    {
                        var r = results[i];
                        results[i] = new ValidationResult(r.ErrorMessage, new[] { fullPath });
                    }
                }
                
                if (_childValidate is not null && value is not null)
                    _childValidate(value, results, fullPath);
            }
        }

        public static Builder Create() => new();

        public sealed class Builder
        {
            private readonly List<PropertyEntry> _entries = new();

            public Builder Leaf<TValue>(
                string memberName,
                Func<T, TValue> getter,
                params ValidationAttribute[] attributes)
            {
                var finalAttributes = attributes.Length > 0
                    ? attributes
                    : typeof(T)
                        .GetProperty(memberName)?
                        .GetCustomAttributes(typeof(ValidationAttribute), true)
                        .Select(x => (ValidationAttribute)x)
                        .ToArray();

                _entries.Add(new PropertyEntry(
                    memberName,
                    o => getter(o),
                    finalAttributes, null));
                return this;
            }

            public Builder Branch<TValue>(
                string memberName,
                Func<T, TValue> getter,
                ValidatorNode<TValue> child,
                params ValidationAttribute[] attributes)
            {
                var finalAttributes = attributes.Length > 0
                    ? attributes
                    : typeof(T)
                        .GetProperty(memberName)!
                        .GetCustomAttributes(typeof(ValidationAttribute), true)
                        .Select(x => (ValidationAttribute)x).ToArray();

                _entries.Add(new PropertyEntry(
                    memberName,
                    o => getter(o),
                    finalAttributes,
                    (obj, res, prefix) => child.TryValidate((TValue?)obj!, res, prefix)));

                return this;
            }

            public Builder Collection<TItem>(
                string memberName,
                Func<T, IEnumerable<TItem>?> getter,
                ValidatorNode<TItem> itemNode,
                params ValidationAttribute[] attributes)
            {
                var finalAttributes = attributes.Length > 0
                    ? attributes
                    : typeof(T)
                        .GetProperty(memberName)!
                        .GetCustomAttributes(typeof(ValidationAttribute), true)
                        .Select(x => (ValidationAttribute)x).ToArray();

                _entries.Add(new PropertyEntry(
                    memberName,
                    o => getter(o),
                    finalAttributes,
                    (obj, res, prefix) =>
                    {
                        if (obj is not IEnumerable<TItem> col) return;
                        int idx = 0;
                        foreach (var item in col)
                        {
                            if (item is not null)
                                itemNode.TryValidate(item, res, $"{prefix}[{idx}]");
                            idx++;
                        }
                    }));
                return this;
            }

            public ValidatorNode<T> Build() => new(_entries.ToArray());
        }
    }
}