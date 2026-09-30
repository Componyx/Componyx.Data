using Componyx.Common.Json;

namespace Componyx.Data.Base
{
    /// <summary>
    /// This class serves as base class for data contracts with a JSON data property.
    /// </summary>
    public class JsonData
    {
        /// <summary>
        /// Gets or sets the data of the object.
        /// </summary>
        public string Data { get; set; }

        /// <summary>
        /// Sets the JSON data property by serializing the specified object.
        /// </summary>
        /// <typeparam name="T">The data type.</typeparam>
        /// <param name="data">The data object.</param>
        /// <param name="serializer">The serializer to use. When omitted, <see cref="JsonDataSettings.Serializer"/> is used.</param>
        public void SetData<T>(T data, IJsonSerializer serializer = null)
        {
            this.Data = (serializer ?? JsonDataSettings.Serializer).Serialize(data);
        }

        /// <summary>
        /// Gets the object of the specified type by deserializing the JSON data.
        /// </summary>
        /// <typeparam name="T">The data type.</typeparam>
        /// <param name="serializer">The serializer to use. When omitted, <see cref="JsonDataSettings.Serializer"/> is used.</param>
        /// <returns>The data object of the specified data type.</returns>
        public T GetData<T>(IJsonSerializer serializer = null)
        {
            if (!string.IsNullOrEmpty(this.Data))
                return (serializer ?? JsonDataSettings.Serializer).Deserialize<T>(this.Data);

            return default;
        }
    }
}