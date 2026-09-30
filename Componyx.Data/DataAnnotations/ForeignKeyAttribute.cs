using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Componyx.Data.DataAnnotations
{
    /// <summary>
    /// This class facilitates custom attribute functionality for DAL mapped objects.
    /// </summary>

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class ForeignKeyAttribute : Attribute
    {
        #region Properties

        /// <summary>
        /// Gets or sets the parent type.
        /// </summary>
        public Type ParentType { get; set; }

        /// <summary>
        /// Gets or sets primary key property.
        /// </summary>
        public string PKProperty { get; set; }

        /// <summary>
        /// Gets or sets primary key table.
        /// </summary>
        public string PKTable { get; set; }

        /// <summary>
        /// Gets or sets primary key column.
        /// </summary>
        public string PKColumn { get; set; }

        /// <summary>
        /// Gets or sets relation class property.
        /// </summary>
        public string RelationalProperty { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Denotes a property used as a foreign key in a relationship.
        /// </summary>
        /// <param name="type">The relationship type.</param>
        /// <param name="pkProperty">The name of the primary key property.</param>
        /// <param name="relationalProperty">The name of the reference property on the current type which maps to the parent type.</param>
        public ForeignKeyAttribute(Type type, string pkProperty, string relationalProperty = null)
        {
            this.ParentType = type;
            this.PKProperty = pkProperty;
            this.RelationalProperty = relationalProperty;
        }

        /// <summary>
        /// Denotes a property used as a foreign key in a relationship.
        /// </summary>
        /// <param name="pkTable">The name of the primary key table in the relationship.</param>
        /// <param name="pkColumn">The name of the primary key column in the relationship.</param>
        /// <param name="relationalProperty">The name of the reference property on the current type which maps to the parent type.</param>
        public ForeignKeyAttribute(string pkTable, string pkColumn, string relationalProperty = null)
        {
            this.PKTable = pkTable;
            this.PKColumn = pkColumn;
            this.RelationalProperty = relationalProperty;
        }

        #endregion
    }
}
