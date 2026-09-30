using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Componyx.Common;

namespace Componyx.Data.Base
{
    /// <summary>
    /// This class serves as base class for business logic with foreign relation(s).
    /// </summary>
    public abstract class LogicRelation<T> : Logic<T>
    {
        /// <summary>
        /// Gets or sets the property name(s) of the foreign relationship object.
        /// </summary>
        protected List<string> RelationPropertyNames = new List<string>();

        /// <summary>
        /// Initializes a new instance of the Logic base class.
        /// </summary>
        public LogicRelation() : base()
        {
            if (this.RelationPropertyNames.Count == 0)
                TypeCache.GetType<T>().Properties.Where(prop => Attribute.IsDefined(prop, typeof(DataAnnotations.ForeignKeyAttribute))).ToList().ForEach(x => this.RelationPropertyNames.Add(x.Name));
        }

        /// <summary>
        /// Initializes a new instance of the Logic base class.
        /// </summary>
        /// <param name="connectionString">The Connection string.</param>
        /// <param name="providerName">The database provider name. e.g. System.Data.SqlClient (default).</param>
        public LogicRelation(string connectionString = null, string providerName = null) : this()
        {
            this.ConnectionString = connectionString;
            this.ProviderName = providerName;
        }

        /// <summary>
        /// Adds the relationship property.
        /// </summary>
        /// <typeparam name="PT">Type of the property.</typeparam>
        /// <param name="propertyExpression">Lambda expression in the form: '() => Class.Property'.</param>
        protected void AddRelationProperty<PT>(Expression<Func<PT>> propertyExpression)
        {
            this.RelationPropertyNames.Add(Common.GetPropertyName(propertyExpression));
        }

        /// <summary>
        /// Loads a list of items with the specified type for the specified foreign relationship.
        /// </summary>
        /// <param name="relation">An object of the specified type with its foreign relation key value(s) set.</param>
        /// <param name="pageIndex">The page index of the paged result set to load.</param>
        /// <returns>A list of the specified type.</returns>
        /// <returns></returns>
        public List<T> LoadSetByRelation(T relation, int? pageIndex = null)
        {
            using (DAL dal = InitDAL())
            {
                var result = LoadSetByRelation(dal, relation, pageIndex).ToList();
                this.TotalItemCount = dal.TotalItemCount ?? 0;
                return result;
            }
        }

        /// <summary>
        /// Loads a list of items with the specified type for the specified foreign relationship.
        /// </summary>
        /// <param name="relation">An object of the specified type with its foreign relation key value(s) set.</param>
        /// <param name="pageIndex">The page index of the paged result set to load.</param>
        /// <returns>A list of the specified type.</returns>
        /// <returns></returns>
        public async Task<List<T>> LoadSetByRelationAsync(T relation, int? pageIndex = null)
        {
            using (DAL dal = await InitDALAsync())
            {
                var result = await LoadSetByRelation(dal, relation, pageIndex).ToListAsync();
                this.TotalItemCount = dal.TotalItemCount ?? 0;
                return result;
            }
        }

        /// <summary>
        /// Loads a list of items with the specified type for the specified foreign relationship.
        /// </summary>
        /// <param name="dal"></param>
        /// <param name="relation"></param>
        /// <param name="pageIndex"></param>
        /// <returns></returns>
        private DAL.FluentQuery<T> LoadSetByRelation(DAL dal, T relation, int? pageIndex = null)
        {
            DAL.FluentQuery<T> query = ApplyPaging(dal, ApplyDefaultSortOrder(dal.Query<T>(LoadOneToMany, LoadOneToOne)), pageIndex);

            foreach (var name in RelationPropertyNames)
            {
                var value = TypeCache.GetType<T>().GetProperty(name).GetValue(relation);

                if (value != null)
                    query = query.Where(name).EqualTo(value);
            }

            return query;
        }
    }

}
