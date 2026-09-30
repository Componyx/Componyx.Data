using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Componyx.Data
{
    public partial class DAL
    {
        private class KeyData
        {
            public object Obj { get; set; }
            public Dictionary<string, IList> Collections { get; set; }
            public Dictionary<string, PropInfo> CollectionsInfo { get; set; }
            public Dictionary<string, (Type Type, PropertyInfo Property)> Classes { get; set; }

            public Dictionary<string, HashSet<object>> CollectionSets { get; set; }

            public KeyData()
            {
                Collections = new Dictionary<string, IList>();
                CollectionsInfo = new Dictionary<string, PropInfo>();
                Classes = new Dictionary<string, (Type Type, PropertyInfo Property)>();
                CollectionSets = new Dictionary<string, HashSet<object>>();
            }
        }

        private class PropInfo
        {
            public bool IsClass { get; set; }
            public bool IsClassList { get; set; }
            public bool IsList { get; set; }
            public System.Type PropType { get; set; }
            public System.Type ItemType { get; set; }
            public bool ItemIsClass { get; set; }
            public bool IsMapped { get; set; }
            public bool IsReadOnly { get; set; }
            public string ColumnName { get; set; }
            public List<DataAnnotations.ForeignKeyAttribute> FKAttributes { get; set; }
            public bool HasIndexParameters { get; set; }
        }

        private class TableData
        {
            public string TableName { get; set; }
            public List<DataRow> KeyColumns { get; set; }
            public Dictionary<string, List<ColumnData>> Columns { get; set; }

            public TableData()
            {
                KeyColumns = new List<DataRow>();
                Columns = new Dictionary<string, List<ColumnData>>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private class ColumnData
        {
            public bool? AllowDBNull { get; set; }
            public int? Ordinal { get; set; }
        }

        private class KeyInfo
        {
            public string PropertyName { get; set; }
            public string ColumnName { get; set; }
            public object Value { get; set; }
        }

        private class Catalog
        {
            public string CatalogName { get; set; }
            public string SchemaName { get; set; }
            public string ServerName { get; set; }

            public Catalog(string catalogName, string schemaName, string serverName)
            {
                this.CatalogName = catalogName;
                this.SchemaName = schemaName;
                this.ServerName = serverName;
            }
        }

        private class Relation
        {
            public Relation Parent { get; set; }
            public Type Type { get; set; }

            public Relation(Type type, Relation parent = null)
            {
                this.Type = type;
                this.Parent = parent;
            }
        }
    }
}
