using Componyx.Common.Json;
using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Componyx.Data.Base
{
    /// <summary>
    /// Holds the settings used by <see cref="JsonData"/>.
    /// </summary>
    public static class JsonDataSettings
    {
        private static IJsonSerializer _serializer = new DefaultSerializer();

        /// <summary>
        /// Gets or sets the serializer used by <see cref="JsonData"/> when no serializer is passed to <see cref="JsonData.SetData{T}"/> or <see cref="JsonData.GetData{T}"/>. Set this once at application startup. Defaults to a System.Text.Json based serializer.
        /// </summary>
        public static IJsonSerializer Serializer
        {
            get => _serializer;
            set => _serializer = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// The default serializer. Uses separate options for writing and reading, because System.Text.Json does not allow reference handling (needed when writing) together with populating existing objects (needed when reading).
        /// </summary>
        private sealed class DefaultSerializer : IJsonSerializer
        {
            private readonly IJsonSerializer _writer;
            private readonly IJsonSerializer _reader;

            public DefaultSerializer()
            {
                var writeOptions = CreateBaseOptions();
                writeOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles; // like Newtonsoft's ReferenceLoopHandling.Ignore

                var readOptions = CreateBaseOptions();
                readOptions.PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate; // Newtonsoft fills get-only collections and existing objects

                _writer = new SystemTextJsonSerializer(writeOptions);
                _reader = new SystemTextJsonSerializer(readOptions);
            }

            public string Serialize<T>(T data)
            {
                return _writer.Serialize(data);
            }

            public T Deserialize<T>(string json)
            {
                return _reader.Deserialize<T>(json);
            }

            private static JsonSerializerOptions CreateBaseOptions()
            {
                return new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    IncludeFields = true, // Newtonsoft serializes public fields
                    NumberHandling = JsonNumberHandling.AllowReadingFromString, // Newtonsoft reads "123" into numeric properties
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // no \uXXXX for non-ASCII or HTML characters, like Newtonsoft
                };
            }
        }
    }
}