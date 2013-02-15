﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

namespace System.Data.Entity.ModelConfiguration.Configuration
{
    using System.Collections.Generic;
    using System.Data.Entity.Core.Mapping;
    using System.Data.Entity.Core.Metadata.Edm;
    using System.Data.Entity.ModelConfiguration.Edm;
    using System.Data.Entity.ModelConfiguration.Utilities;
    using System.Data.Entity.Resources;
    using System.Data.Entity.Utilities;
    using System.Linq;
    using System.Reflection;

    internal class ModificationFunctionConfiguration
    {
        private readonly Dictionary<PropertyPath, Tuple<string, string>> _parameterNames
            = new Dictionary<PropertyPath, Tuple<string, string>>();

        private readonly Dictionary<PropertyInfo, string> _resultBindings
            = new Dictionary<PropertyInfo, string>();

        private string _name;
        private string _rowsAffectedParameter;

        public ModificationFunctionConfiguration()
        {
        }

        private ModificationFunctionConfiguration(ModificationFunctionConfiguration source)
        {
            DebugCheck.NotNull(source);

            _name = source._name;
            _rowsAffectedParameter = source._rowsAffectedParameter;

            source._parameterNames.Each(
                c => _parameterNames.Add(c.Key, Tuple.Create(c.Value.Item1, c.Value.Item2)));

            source._resultBindings.Each(
                r => _resultBindings.Add(r.Key, r.Value));
        }

        public virtual ModificationFunctionConfiguration Clone()
        {
            return new ModificationFunctionConfiguration(this);
        }

        public void HasName(string name)
        {
            DebugCheck.NotEmpty(name);

            _name = name;
        }

        public string Name
        {
            get { return _name; }
        }

        public void RowsAffectedParameter(string name)
        {
            DebugCheck.NotEmpty(name);

            _rowsAffectedParameter = name;
        }

        public string RowsAffectedParameterName
        {
            get { return _rowsAffectedParameter; }
        }

        public Dictionary<PropertyPath, Tuple<string, string>> ParameterNames
        {
            get { return _parameterNames; }
        }

        public Dictionary<PropertyInfo, string> ResultBindings
        {
            get { return _resultBindings; }
        }

        public void Parameter(
            PropertyPath propertyPath, string parameterName, string originalValueParameterName = null)
        {
            DebugCheck.NotNull(propertyPath);
            DebugCheck.NotEmpty(parameterName);

            _parameterNames[propertyPath] = Tuple.Create(parameterName, originalValueParameterName);
        }

        public void Result(PropertyPath propertyPath, string columnName)
        {
            DebugCheck.NotNull(propertyPath);
            DebugCheck.NotEmpty(columnName);

            _resultBindings[propertyPath.Single()] = columnName;
        }

        public virtual void Configure(StorageModificationFunctionMapping modificationFunctionMapping)
        {
            DebugCheck.NotNull(modificationFunctionMapping);

            ConfigureName(modificationFunctionMapping);
            ConfigureRowsAffectedParameter(modificationFunctionMapping);
            ConfigureParameters(modificationFunctionMapping);
            ConfigureResultBindings(modificationFunctionMapping);
        }

        private void ConfigureName(StorageModificationFunctionMapping modificationFunctionMapping)
        {
            DebugCheck.NotNull(modificationFunctionMapping);

            if (!string.IsNullOrWhiteSpace(_name))
            {
                modificationFunctionMapping.Function.Name = _name;
            }
        }

        private void ConfigureRowsAffectedParameter(StorageModificationFunctionMapping modificationFunctionMapping)
        {
            DebugCheck.NotNull(modificationFunctionMapping);

            if (!string.IsNullOrWhiteSpace(_rowsAffectedParameter))
            {
                if (modificationFunctionMapping.RowsAffectedParameter == null)
                {
                    throw Error.NoRowsAffectedParameter(modificationFunctionMapping.Function.Name);
                }

                modificationFunctionMapping.RowsAffectedParameter.Name = _rowsAffectedParameter;
            }
        }

        private void ConfigureParameters(StorageModificationFunctionMapping modificationFunctionMapping)
        {
            foreach (var keyValue in _parameterNames)
            {
                var propertyPath = keyValue.Key;
                var parameterName = keyValue.Value.Item1;
                var originalValueParameterName = keyValue.Value.Item2;

                var parameterBindings
                    = modificationFunctionMapping
                        .ParameterBindings
                        .Where(
                            pb => propertyPath.Equals(
                                new PropertyPath(
                                      pb.MemberPath.Members
                                        .OfType<EdmProperty>()
                                        .Select(m => m.GetClrPropertyInfo()))))
                        .ToList();

                if (parameterBindings.Count == 1)
                {
                    var parameterBinding = parameterBindings.Single();

                    if (!string.IsNullOrWhiteSpace(originalValueParameterName))
                    {
                        if (parameterBinding.IsCurrent)
                        {
                            throw Error.ModificationFunctionParameterNotFoundOriginal(
                                propertyPath,
                                modificationFunctionMapping.Function.Name);
                        }
                    }

                    parameterBinding.Parameter.Name = parameterName;
                }
                else if (parameterBindings.Count == 2)
                {
                    parameterBindings.Single(pb => pb.IsCurrent).Parameter.Name = parameterName;

                    if (!string.IsNullOrWhiteSpace(originalValueParameterName))
                    {
                        parameterBindings.Single(pb => !pb.IsCurrent).Parameter.Name = originalValueParameterName;
                    }
                }
                else
                {
                    throw Error.ModificationFunctionParameterNotFound(
                        propertyPath,
                        modificationFunctionMapping.Function.Name);
                }
            }
        }

        private void ConfigureResultBindings(StorageModificationFunctionMapping modificationFunctionMapping)
        {
            DebugCheck.NotNull(modificationFunctionMapping);

            foreach (var keyValue in _resultBindings)
            {
                var propertyInfo = keyValue.Key;
                var columnName = keyValue.Value;

                var resultBinding
                    = (modificationFunctionMapping
                           .ResultBindings ?? Enumerable.Empty<StorageModificationFunctionResultBinding>())
                        .SingleOrDefault(rb => propertyInfo.IsSameAs(rb.Property.GetClrPropertyInfo()));

                if (resultBinding == null)
                {
                    throw Error.ResultBindingNotFound(
                        propertyInfo.Name,
                        modificationFunctionMapping.Function.Name);
                }

                resultBinding.ColumnName = columnName;
            }
        }

        public bool IsCompatibleWith(ModificationFunctionConfiguration other)
        {
            DebugCheck.NotNull(other);

            if ((_name != null)
                && (other._name != null)
                && !string.Equals(_name, other._name, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !_parameterNames
                        .Join(
                            other._parameterNames,
                            kv1 => kv1.Key,
                            kv2 => kv2.Key,
                            (kv1, kv2) => !Equals(kv1.Value, kv2.Value))
                        .Any(j => j);
        }
    }
}