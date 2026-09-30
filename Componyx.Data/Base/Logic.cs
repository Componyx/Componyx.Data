using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Componyx.Data.Base
{
    /// <summary>
    /// This class serves as base class for business logic.
    /// </summary>
    public abstract class Logic<T>
    {
        /// <summary>
        /// Add or remove event handlers for the event which fires when the Data Access Layer initializes.
        /// </summary>
        public event EventHandler InitializeDAL;

        /// <summary>
        /// Gets or sets the Connection string.
        /// </summary>
        public string ConnectionString;

        /// <summary>
        /// Gets or sets the Connection provider name.
        /// </summary>
        public string ProviderName;

        /// <summary>
        /// Gets or sets the default sort order by specifying a KeyValuePair with the property name as key and a boolean value to indicate if the sort order is ascending(false) or descending(true).
        /// </summary>
        public List<KeyValuePair<string, bool>> DefaultSortOrder;

        /// <summary>
        /// Gets or sets the isolation level of the transaction.
        /// </summary>
        public IsolationLevel? IsolationLevel;

        /// <summary>
        /// Gets the total item count of the last query when paging parameters where specified.
        /// </summary>
        public int? TotalItemCount { get; protected set; }

        /// <summary>
        /// Gets or sets the page size used in a paging enabled query.
        /// </summary>
        public int? PageSize { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if one-to-many relations of the target type should be loaded.
        /// </summary>
        public bool LoadOneToMany { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if one-to-one relations of the target type should be loaded.
        /// </summary>
        public bool LoadOneToOne { get; set; }

        /// <summary>
        /// Gets or sets a value indicating which relations of the target type should be ignored
        /// </summary>
        public HashSet<Type> IgnoreTypes { get; set; }

        /// <summary>
        /// Initializes a new instance of the Logic base class.
        /// </summary>
        public Logic()
        {
            LoadOneToMany = LoadOneToOne = true;
            Init();
        }

        /// <summary>
        /// Initializes a new instance of the Logic base class.
        /// </summary>
        /// <param name="connectionString">The Connection string.</param>
        /// <param name="providerName">The database provider name. e.g. System.Data.SqlClient (default).</param>
        public Logic(string connectionString = null, string providerName = null) : this()
        {
            this.ConnectionString = connectionString;
            this.ProviderName = providerName;
        }

        /// <summary>
        /// Sets the default sort order.
        /// </summary>
        /// <param name="sortOrder">A list of key/value pairs with a Lambda expression in the form: '() => Class.Property' as key and a value indicating if the sorting must be ascending(false) or descending(true).</param>
        public void SetDefaultSortOrder(List<KeyValuePair<Expression<Func<object>>, bool>> sortOrder)
        {
            this.DefaultSortOrder = new List<KeyValuePair<string, bool>>();

            foreach (var kvp in sortOrder)
            {
                this.DefaultSortOrder.Add(new KeyValuePair<string, bool>(Common.GetPropertyName(kvp.Key), kvp.Value));
            }
        }

        /// <summary>
        /// Sets the default sort order.
        /// </summary>
        /// <param name="sortOrder">A list of key/value pairs with a Lambda expression in the form: '() => Class.Property' as key and a value indicating if the sorting must be ascending(false) or descending(true).</param>
        public void SetDefaultSortOrder(List<KeyValuePair<List<Expression<Func<object>>>, bool>> sortOrder)
        {
            this.DefaultSortOrder = new List<KeyValuePair<string, bool>>();

            foreach (var kvp in sortOrder)
            {
                this.DefaultSortOrder.Add(new KeyValuePair<string, bool>(Common.GetPropertyNames<List<Expression<Func<object>>>>(kvp.Key), kvp.Value));
            }
        }

        /// <summary>
        /// Adds the default sort order.
        /// </summary>
        /// <param name="sortOrder">A key/value pair with a Lambda expression in the form: '() => Class.Property' as key and a value indicating if the sorting must be ascending(false) or descending(true).</param>
        public void AddDefaultSortOrder(KeyValuePair<Expression<Func<object>>, bool> sortOrder)
        {
            if (this.DefaultSortOrder == null)
                this.DefaultSortOrder = new List<KeyValuePair<string, bool>>();

            this.DefaultSortOrder.Add(new KeyValuePair<string, bool>(Common.GetPropertyName(sortOrder.Key), sortOrder.Value));
        }

        /// <summary>
        /// Adds the default sort order.
        /// </summary>
        /// <param name="sortOrder">A key/value pair with a Lambda expression in the form: '() => Class.Property' as key and a value indicating if the sorting must be ascending(false) or descending(true).</param>
        public void AddDefaultSortOrder(KeyValuePair<List<Expression<Func<object>>>, bool> sortOrder)
        {
            if (this.DefaultSortOrder == null)
                this.DefaultSortOrder = new List<KeyValuePair<string, bool>>();


            this.DefaultSortOrder.Add(new KeyValuePair<string, bool>(Common.GetPropertyNames<List<Expression<Func<object>>>>(sortOrder.Key), sortOrder.Value));
        }


        /// <summary>
        /// Loads a list of items with the specified type.
        /// </summary>
        /// <returns>A list with items of the specified type.</returns>
        public List<T> LoadSet()
        {
            using (DAL dal = InitDAL())
            {
                return ApplyDefaultSortOrder(dal.Query<T>(LoadOneToMany, LoadOneToOne)).ToList();
            }
        }

        /// <summary>
        /// Loads a list of items with the specified type.
        /// </summary>
        /// <returns>A list with items of the specified type.</returns>
        public async Task<List<T>> LoadSetAsync()
        {
            using (DAL dal = await InitDALAsync())
            {
                return await ApplyDefaultSortOrder(dal.Query<T>(LoadOneToMany, LoadOneToOne)).ToListAsync();
            }
        }

        /// <summary>
        /// Loads a list of items with the specified type.
        /// </summary>
        /// <param name="pageIndex">The page index of the paged result set to load.</param>
        /// <returns>A list of the specified type.</returns>
        public List<T> LoadSet(int? pageIndex = null)
        {
            using (DAL dal = InitDAL())
            {
                DAL.FluentQuery<T> query = ApplyPaging(dal, ApplyDefaultSortOrder(dal.Query<T>(LoadOneToMany, LoadOneToOne)), pageIndex);

                var result = query.ToList();
                this.TotalItemCount = dal.TotalItemCount ?? 0;
                return result;
            }
        }

        /// <summary>
        /// Loads a list of items with the specified type.
        /// </summary>
        /// <param name="pageIndex">The page index of the paged result set to load.</param>
        /// <returns>A list of the specified type.</returns>
        public async Task<List<T>> LoadSetAsync(int? pageIndex = null)
        {
            using (DAL dal = await InitDALAsync())
            {
                DAL.FluentQuery<T> query = ApplyPaging(dal, ApplyDefaultSortOrder(dal.Query<T>(LoadOneToMany, LoadOneToOne)), pageIndex);

                var result = await query.ToListAsync();
                this.TotalItemCount = dal.TotalItemCount ?? 0;
                return result;
            }
        }

        /// <summary>
        /// Loads a single item of the specified type.
        /// </summary>
        /// <param name="obj">The object to load.</param>
        /// <returns>A single item of the specified type.</returns>
        public T Load(T obj)
        {
            using (DAL dal = InitDAL())
            {
                return dal.Load(obj, LoadOneToMany, LoadOneToOne, DefaultSortOrder);
            }
        }

        /// <summary>
        /// Loads a single item of the specified type.
        /// </summary>
        /// <param name="obj">The object to load.</param>
        /// <returns>A single item of the specified type.</returns>
        public async Task<T> LoadAsync(T obj)
        {
            using (DAL dal = await InitDALAsync())
            {
                return await dal.LoadAsync(obj, LoadOneToMany, LoadOneToOne, DefaultSortOrder);
            }
        }

        /// <summary>
        /// Stores the items in the database.
        /// </summary>
        /// <param name="list">A list of objects to store.</param>
        /// <param name="deep">Defines if related objects are stored.</param>
        /// <param name="refreshModel">Defines if the model should be reloaded from the database.</param>
        public void SaveSet(IEnumerable<T> list, bool deep = true, bool refreshModel = true)
        {
            using (DAL dal = InitDAL())
            {
                dal.SaveSet<T>(list, deep, refreshModel);
            }
        }

        /// <summary>
        /// Stores the items in the database.
        /// </summary>
        /// <param name="list">A list of objects to store.</param>
        /// <param name="deep">Defines if related objects are stored.</param>
        /// <param name="refreshModel">Defines if the model should be reloaded from the database.</param>
        public async Task SaveSetAsync(IEnumerable<T> list, bool deep = true, bool refreshModel = true)
        {
            using (DAL dal = await InitDALAsync())
            {
                await dal.SaveSetAsync<T>(list, deep, refreshModel);
            }
        }

        /// <summary>
        /// Stores the item in the database.
        /// </summary>
        /// <param name="obj">The object to store.</param>
        /// <param name="deep">Defines if related objects are stored.</param>
        /// <param name="refreshModel">Defines if the model should be reloaded from the database.</param>
        public void Save(T obj, bool deep = true, bool refreshModel = true)
        {
            using (DAL dal = InitDAL())
            {
                dal.Save(obj, deep, refreshModel);
            }
        }

        /// <summary>
        /// Stores the item in the database.
        /// </summary>
        /// <param name="obj">The object to store.</param>
        /// <param name="deep">Defines if related objects are stored.</param>
        /// <param name="refreshModel">Defines if the model should be reloaded from the database.</param>
        public async Task SaveAsync(T obj, bool deep = true, bool refreshModel = true)
        {
            using (DAL dal = await InitDALAsync())
            {
                await dal.SaveAsync(obj, deep, refreshModel);
            }
        }

        /// <summary>
        /// Removes the items in the database.
        /// </summary>
        /// <param name="list">A list of objects to delete.</param>
        /// <param name="deep">Defines if related objects are removed.</param>
        public void DeleteSet(IEnumerable<T> list, bool deep = true)
        {
            using (DAL dal = InitDAL())
            {
                dal.DeleteSet<T>(list, deep);
            }
        }

        /// <summary>
        /// Removes the items in the database.
        /// </summary>
        /// <param name="list">A list of objects to delete.</param>
        /// <param name="deep">Defines if related objects are removed.</param>
        public async Task DeleteSetAsync(IEnumerable<T> list, bool deep = true)
        {
            using (DAL dal = await InitDALAsync())
            {
                await dal.DeleteSetAsync<T>(list, deep);
            }
        }

        /// <summary>
        /// Removes the item in the database.
        /// </summary>
        /// <param name="obj">The object to delete.</param>
        /// <param name="deep">Defines if related objects are removed.</param>
        public void Delete(T obj, bool deep = true)
        {
            using (DAL dal = InitDAL())
            {
                dal.Delete(obj, deep);
            }
        }

        /// <summary>
        /// Removes the item in the database.
        /// </summary>
        /// <param name="obj">The object to delete.</param>
        /// <param name="deep">Defines if related objects are removed.</param>
        public async Task DeleteAsync(T obj, bool deep = true)
        {
            using (DAL dal = await InitDALAsync())
            {
                await dal.DeleteAsync(obj, deep);
            }
        }

        /// <summary>
        /// Applies the default sort order to the query.
        /// </summary>
        /// <param name="query">The FluentQuery of type T.</param>
        /// <returns>The FluentQuery of type T.</returns>
        protected DAL.FluentQuery<T> ApplyDefaultSortOrder(DAL.FluentQuery<T> query)
        {
            if (DefaultSortOrder != null)
            {
                foreach (KeyValuePair<string, bool> item in DefaultSortOrder)
                {
                    query = query.OrderBy(item.Key, item.Value);
                }
            }

            return query;
        }

        /// <summary>
        ///  Initializes the class. This method can be overridden in the derived class.
        /// </summary>
        protected abstract void Init();

        /// <summary>
        /// Initializes the Data Access Layer. This method can be overridden in the derived class.
        /// </summary>
        /// <param name="dal"></param>
        /// <returns></returns>
        protected virtual DAL InitDAL(DAL dal = null)
        {
            if (dal == null)
            {
                dal = new DAL(ConnectionString, ProviderName);

                if (IsolationLevel.HasValue && IsolationLevel > 0)
                    dal.IsolationLevel = IsolationLevel.Value;

                dal.IgnoreTypes = IgnoreTypes;
            }

            if (InitializeDAL != null)
                InitializeDAL(dal, EventArgs.Empty);

            return dal;
        }

        /// <summary>
        /// Initializes the Data Access Layer. This method can be overridden in the derived class.
        /// </summary>
        /// <param name="dal"></param>
        /// <returns></returns>
        protected virtual async Task<DAL> InitDALAsync(DAL dal = null)
        {
            if (dal == null)
            {
                dal = new DAL();
                await dal.OpenAsync(this.ConnectionString, this.ProviderName);

                if (IsolationLevel.HasValue && IsolationLevel > 0)
                    dal.IsolationLevel = IsolationLevel.Value;

                dal.IgnoreTypes = IgnoreTypes;
            }

            if (InitializeDAL != null)
                InitializeDAL(dal, EventArgs.Empty);

            return dal;
        }

        /// <summary>
        /// Apply paging to the query.
        /// </summary>
        /// <param name="dal"></param>
        /// <param name="query"></param>
        /// <param name="pageIndex"></param>
        /// <returns></returns>
        protected DAL.FluentQuery<T> ApplyPaging(DAL dal, DAL.FluentQuery<T> query, int? pageIndex = null)
        {
            if (PageSize != null && PageSize.Value >= 1)
            {
                if (pageIndex == null || pageIndex.Value < 1)
                    pageIndex = 1;

                dal.PageSize = PageSize;
                query = query.TakePage(pageIndex.Value);
            }

            return query;
        }
    }
}
