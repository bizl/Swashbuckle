﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Web.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Swashbuckle.Swagger
{
    public class SchemaRegistry
    {
        public static readonly Dictionary<Type, Func<Schema>> PrimitiveMappings = new Dictionary<Type, Func<Schema>>()
            {
                {typeof (Int16), () => new Schema {type = "integer", format = "int32"}},
                {typeof (UInt16), () => new Schema {type = "integer", format = "int32"}},
                {typeof (Int32), () => new Schema {type = "integer", format = "int32"}},
                {typeof (UInt32), () => new Schema {type = "integer", format = "int32"}},
                {typeof (Int64), () => new Schema {type = "integer", format = "int64"}},
                {typeof (UInt64), () => new Schema {type = "integer", format = "int64"}},
                {typeof (Single), () => new Schema {type = "number", format = "float"}},
                {typeof (Double), () => new Schema {type = "number", format = "double"}},
                {typeof (Decimal), () => new Schema {type = "number", format = "double"}},
                {typeof (String), () => new Schema {type = "string"}},
                {typeof (Char), () => new Schema {type = "string"}},
                {typeof (Byte), () => new Schema {type = "string", format = "byte"}},
                {typeof (SByte), () => new Schema {type = "string", format = "byte"}},
                {typeof (Guid), () => new Schema {type = "string"}},
                {typeof (Boolean), () => new Schema {type = "boolean"}},
                {typeof (DateTime), () => new Schema {type = "string", format = "date-time"}},
                {typeof (DateTimeOffset), () => new Schema {type = "string", format = "date-time"}}
            };

        private static readonly IEnumerable<Type> HttpTypes = new[]
            {
                typeof(HttpRequestMessage),
                typeof(HttpResponseMessage),
                typeof(IHttpActionResult)
            };

        private readonly IContractResolver _contractResolver;
        private readonly IDictionary<Type, Func<Schema>> _customSchemaMappings;
        private readonly IEnumerable<ISchemaFilter> _schemaFilters;

        public SchemaRegistry(
            IContractResolver contractResolver,
            IDictionary<Type, Func<Schema>> customSchemaMappings,
            IEnumerable<ISchemaFilter> schemaFilters)
        {
            _contractResolver = contractResolver;
            _customSchemaMappings = customSchemaMappings;
            _schemaFilters = schemaFilters;

            Definitions = new Dictionary<string, Schema>(StringComparer.OrdinalIgnoreCase);
        }

        public IDictionary<string, Schema> Definitions { get; private set; }

        public Schema FindOrRegister(Type type)
        {
            var referencedTypes = new Queue<KeyValuePair<string, Type>>();
            var rootSchema = CreateSchema(type, true, referencedTypes);

            while (referencedTypes.Any())
            {
                var next = referencedTypes.Dequeue();
                if (Definitions.ContainsKey(next.Key)) continue;

                Definitions.Add(next.Key, CreateSchema(next.Value, false, referencedTypes));
            }

            // Need to fully qualify any ref to a schema that's contained in Definitions
            // TODO: There has to be a better way - this will do for now though!
            if (rootSchema.@ref != null)
                rootSchema.@ref = "#/definitions/" + rootSchema.@ref;
            if (rootSchema.items != null && rootSchema.items.@ref != null)
                rootSchema.items.@ref = "#/definitions/" + rootSchema.items.@ref;

            return rootSchema;
        }

        private Schema CreateSchema(
            Type type,
            bool refIfComplex,
            Queue<KeyValuePair<string, Type>> referencedTypes)
        {
            if (_customSchemaMappings.ContainsKey(type))
                return _customSchemaMappings[type]();

            if (PrimitiveMappings.ContainsKey(type))
                return PrimitiveMappings[type]();

            Type innerType;
            if (type.IsNullable(out innerType))
                return CreateSchema(innerType, refIfComplex, referencedTypes);

            if (type.IsEnum)
                return new Schema { type = "string", @enum = type.GetEnumNames() };

            // Non-primitive - utilize the Json contract resolver
            var contract = _contractResolver.ResolveContract(type);

            if (contract is JsonArrayContract)
                return CreateArraySchema((JsonArrayContract)contract, referencedTypes);

            if (contract is JsonDictionaryContract)
                return CreateDictionarySchema((JsonDictionaryContract)contract, referencedTypes);

            if (contract is JsonObjectContract && !HttpTypes.Contains(type))
            {
                return refIfComplex
                    ? CreateRefSchema(type, referencedTypes)
                    : CreateComplexSchema((JsonObjectContract)contract, referencedTypes);
            }

            // Falback, describe anything else as an object
            return CreateSchema(typeof(object), refIfComplex, referencedTypes);
        }

        private Schema CreateRefSchema(Type type, Queue<KeyValuePair<string, Type>> referencedTypes)
        {
            var id = type.FriendlyId();
            referencedTypes.Enqueue(new KeyValuePair<string, Type>(id, type));
            return new Schema { @ref = id };
        }

        private Schema CreateArraySchema(JsonArrayContract contract, Queue<KeyValuePair<string, Type>> referencedTypes)
        {
            var items = (contract.UnderlyingType == contract.CollectionItemType)
                ? CreateRefSchema(contract.CollectionItemType, referencedTypes) //prevents infinite loop
                : CreateSchema(contract.CollectionItemType, true, referencedTypes);

            return new Schema
                {
                    type = "array",
                    items = items
                };
        }

        private Schema CreateDictionarySchema(JsonDictionaryContract contract, Queue<KeyValuePair<string, Type>> referencedTypes)
        {
            var additionalProperties = (contract.UnderlyingType == contract.DictionaryValueType)
                ? CreateRefSchema(contract.DictionaryValueType, referencedTypes) //prevents infinite loop
                : CreateSchema(contract.DictionaryValueType, true, referencedTypes);

            return new Schema
                {
                    type = "object",
                    additionalProperties = additionalProperties
                };
        }

        private Schema CreateComplexSchema(JsonObjectContract contract, Queue<KeyValuePair<string, Type>> referencedTypes)
        {
            var properties = contract.Properties.Where(p => !p.Ignored).ToDictionary(
                prop => prop.PropertyName,
                prop => CreateSchema(prop.PropertyType, true, referencedTypes)
                    .WithValidationProperties(prop));

            var required = contract.Properties.Where(prop => prop.IsRequired())
                .Select(propInfo => propInfo.PropertyName)
                .ToList();

            var schema = new Schema
            {
                required = required.Any() ? required : null, // required can be null but not empty
                properties = properties,
                type = "object"
            };

            foreach (var filter in _schemaFilters)
            {
                filter.Apply(schema, this, contract.UnderlyingType);
            }

            return schema;
        }
    }
}