using Componyx.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Componyx.Data
{
    /// <summary>
    /// This class facilitates database access and database to object mapping.
    /// </summary>
    public partial class DAL : IDisposable
    {
        /// <summary>
        /// Gets or sets the default database provider name used when opening a connection with the Open method that does not specify a provider name.
        /// </summary>
        public static string DefaultProviderName { get; set; } = "System.Data.SqlClient";

        private string _CLRL = "CommonLanguageRuntimeLibrary";
        private string _SqlScopeIdentity = "SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        private DbTransaction _tr = null;
        private int _trCommandCount = 0;
        private Dictionary<string, string> _map = new Dictionary<string, string>();
        private Dictionary<string, KeyData> _keyData = new Dictionary<string, KeyData>();
        private DataTable _schema;
        private Dictionary<string, Catalog> _catalogMapping = new Dictionary<string, Catalog>();
        private DbDataReader _reader = null;
        private bool _limitedResult = false;
        private List<string> _columnAliases = new List<string>();
        private bool? _tableNameValidationValue = null;
        private HashSet<string> _columnAliasPrefixes = new HashSet<string>();

        // cache objects
        private Dictionary<string, TableData> _querySchemaCache = new Dictionary<string, TableData>();
        private Dictionary<string, TableData> _typeSchemaCache = new Dictionary<string, TableData>();
        private Dictionary<string, PropInfo> _propInfoCache = new Dictionary<string, PropInfo>();
        private Dictionary<string, bool> _tableInSchemaCache = new Dictionary<string, bool>();
        private Dictionary<string, string> _tableNameCache = new Dictionary<string, string>();
        private Dictionary<string, List<ColumnData>> _schemaColumns;

        #region Enums

        /// <summary>
        /// SQL result limiter option.
        /// </summary>
        public enum SqlLimitOption
        {
            /// <summary>
            /// SQLServer only.
            /// </summary>
            Row_Number_Over = 0,
            /// <summary>
            /// Oracle only.
            /// </summary>
            RowNum = 1,
            /// <summary>
            /// MySQL, SQLLite and possible other DB engines.
            /// </summary>
            Limit = 2,
            /// <summary>
            /// PostgreSQL and possible other DB engines.
            /// </summary>
            Limit_Offset = 3
        }


        /// <summary>
        /// SQL top select option.
        /// </summary>
        public enum SqlTopLimitOption
        {
            /// <summary>
            /// SQLServer only.
            /// </summary>
            Top = 0,
            /// <summary>
            /// Oracle only.
            /// </summary>
            RowNum = 1,
            /// <summary>
            /// MySQL PostgreSQL and possible other DB engines.
            /// </summary>
            Limit = 2
        }

        /// <summary>
        /// SQL delimited identifier option.
        /// </summary>
        public enum SqlDelimitedIdentifierOption
        {
            /// <summary>
            /// Default SQL.
            /// </summary>
            DoubleQuotes = 0,
            /// <summary>
            /// MySQL.
            /// </summary>
            BackQuotes = 1,
            /// <summary>
            /// MS SQL Server.
            /// </summary>
            Brackets = 2
        }

        /// <summary>
        /// SQL column ordinal option.
        /// </summary>
        public enum SqlColumnOrdinalOption
        {
            /// <summary>
            /// MS implementation.
            /// </summary>
            ZeroBased = 0,
            /// <summary>
            /// Possible 3th party implementation.
            /// </summary>
            OneBased = 1
        }
        #endregion

        /// <summary>
        /// Gets the database provider Factory.
        /// </summary>
        public DbProviderFactory Factory { get; private set; }

        /// <summary>
        /// Gets or sets the current Connection.
        /// </summary>
        public DbConnection Connection { get; set; }

        /// <summary>
        /// Gets or sets the isolation level of the transaction.
        /// </summary>
        public IsolationLevel IsolationLevel { get; set; }

        /// <summary>
        /// Gets or sets the parameter prefix.
        /// </summary>
        public char? ParameterMarker { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether named parameters are supported by the database provider.
        /// </summary>
        public bool? NamedParameters { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the table name of the class type is validated when a column lookup is performed in the schema table.
        /// </summary>
        /// <remarks></remarks>
        public bool ColumnLookupTableNameValidation { get; set; }

        /// <summary>
        /// Gets or sets the page size used in a paging enabled query.
        /// </summary>
        public int? PageSize { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if one-to-many relations of the target type should be loaded.
        /// </summary>
        public bool LoadOneToMany { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating if one-to-one relations of the target type should be loaded.
        /// </summary>
        public bool LoadOneToOne { get; set; } = true;

        /// <summary>
        /// Gets or sets the option used for limiting the result set.
        /// </summary>
        public SqlLimitOption SqlResultLimiter { get; set; }

        /// <summary>
        /// Gets or sets the option used for selecting top results.
        /// </summary>
        public SqlTopLimitOption SqlTopLimiter { get; set; }

        /// <summary>
        /// Gets or sets the option used for delimited identifiers. Default value: DoubleQuotes.
        /// </summary>
        public SqlDelimitedIdentifierOption SqlDelimitedIdentifier { get; set; }

        /// <summary>
        /// Gets or sets the option used for retrieving columns based upon column ordinals. Default value: ZeroBased.
        /// </summary>
        public SqlColumnOrdinalOption SqlColumnOrdinal { get; set; }

        /// <summary>
        /// Gets or sets the command to retrieve the last inserted id within the current scope. 
        /// This command must be set to be able to refresh inserted entities with an auto increment key.
        /// Default value: SELECT SCOPE_IDENTITY().
        /// </summary>
        public string SqlLastIdCommand { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if 'DEFAULT VALUES' is used in the SQL INSERT command when there are no columns to insert.
        /// This command must be set to be able to save a new empty object without any column values set and no columns supporting a DBNULL-value insert.
        /// If set to false, this would result in an empty column and values list in the generated INSERT command (Most DB-providers do not support this. Set to false when using MySQL).
        /// Default value: true
        /// </summary>
        public bool UseDefaultValuesInsertCommand { get; set; } = true;

        /// <summary>
        /// Gets the total item count of the last query when paging parameters where specified.
        /// </summary>
        public int? TotalItemCount { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether an exception is thrown or if the property value is set to null when a column is missing in the query result.
        /// </summary>
        public bool IgnoreMissingColumns { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether property names that consist of parent name and key are being treated as foreign keys.
        /// </summary>
        public bool AutoDetectForeignKeys { get; set; }

        /// <summary>
        /// Gets or sets a value indicating which string comparison option is used for comparing column and table names.
        /// </summary>
        public StringComparison NameComparison { get; set; }

        /// <summary>
        /// Gets or sets the dictionary where the key represents the (hierachical) property name and the value represents the column alias name.
        /// </summary>
        public Dictionary<string, string> ColumnAliasMapping { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets the dictionary where the key represents the (hierachical) property name and the value represents the table index in the table schema.
        /// This is useful when the resultset contains multiple column sets of the same table (through joins).
        /// </summary>
        public Dictionary<string, int> TableIndexMapping { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Gets or sets relationship types that should be ignored when retrieving data.
        /// </summary>
        public HashSet<Type> IgnoreTypes { get; set; }

        /// <summary>
        /// Initializes a new instance of the DAL class and opens the database connection.
        /// </summary>
        public DAL()
        {

        }

        /// <summary>
        /// Initializes a new instance of the DAL class and opens the database connection.
        /// </summary>
        /// <param name="connection">The Connection string.</param>
        /// <param name="providerName">The database provider name. e.g. System.Data.SqlClient (default).</param>
        /// <param name="isolationLevel">The isolation level of the transaction.</param>
        /// <param name="parameterMarker">The character used to designate parameters. If omitted an attempt is made to get the parameter marker from the database schema. Defaults to @ when it is not found in the schema.</param>
        /// <param name="namedParameters">A value indicating whether named parameters are supported by the database provider. If ommited an attempt is made to define named parameter support from the database schema.</param>
        public DAL(string connection, string providerName = null, IsolationLevel isolationLevel = System.Data.IsolationLevel.ReadCommitted, char? parameterMarker = null, bool? namedParameters = null)
        {
            InitFactory(providerName, connection);
            Init(isolationLevel, parameterMarker, namedParameters);
        }

        /// <summary>
        /// Initializes a new instance of the DAL class and opens the database connection.
        /// </summary>
        /// <param name="connection">An instance of a database connection.</param>
        /// <param name="providerName">The database provider name. e.g. System.Data.SqlClient (default).</param>
        /// <param name="isolationLevel">The isolation level of the transaction.</param>
        /// <param name="parameterMarker">The character used to designate parameters. If omitted an attempt is made to get the parameter marker from the database schema. Defaults to @ when it is not found in the schema.</param>
        /// <param name="namedParameters">A value indicating whether named parameters are supported by the database provider. If ommited an attempt is made to define named parameter support from the database schema.</param>
        public DAL(DbConnection connection, string providerName = null, IsolationLevel isolationLevel = System.Data.IsolationLevel.ReadCommitted, char? parameterMarker = null, bool? namedParameters = null)
        {
            InitFactory(providerName);
            Connection = connection;
            Init(isolationLevel, parameterMarker, namedParameters);
        }

        /// <summary>
        /// Opens the database connection async.
        /// </summary>
        /// <param name="connection">The Connection string.</param>
        /// <param name="providerName">The database provider name. e.g. System.Data.SqlClient (default).</param>
        /// <param name="isolationLevel">The isolation level of the transaction.</param>
        /// <param name="parameterMarker">The character used to designate parameters. If omitted an attempt is made to get the parameter marker from the database schema. Defaults to @ when it is not found in the schema.</param>
        /// <param name="namedParameters">A value indicating whether named parameters are supported by the database provider. If ommited an attempt is made to define named parameter support from the database schema.</param>
        public async Task OpenAsync(string connection, string providerName = null, IsolationLevel isolationLevel = System.Data.IsolationLevel.ReadCommitted, char? parameterMarker = null, bool? namedParameters = null)
        {
            await InitFactory(providerName, connection).OpenAsync().ConfigureAwait(false);
            Init(isolationLevel, parameterMarker, namedParameters);
        }

        /// <summary>
        /// Opens the database connection async.
        /// </summary>
        /// <param name="connection">An instance of a database connection.</param>
        /// <param name="providerName">The database provider name. e.g. System.Data.SqlClient (default).</param>
        /// <param name="isolationLevel">The isolation level of the transaction.</param>
        /// <param name="parameterMarker">The character used to designate parameters. If omitted an attempt is made to get the parameter marker from the database schema. Defaults to @ when it is not found in the schema.</param>
        /// <param name="namedParameters">A value indicating whether named parameters are supported by the database provider. If ommited an attempt is made to define named parameter support from the database schema.</param>
        public async Task OpenAsync(DbConnection connection, string providerName = null, IsolationLevel isolationLevel = System.Data.IsolationLevel.ReadCommitted, char? parameterMarker = null, bool? namedParameters = null)
        {
            InitFactory(providerName);
            Connection = connection;
            await connection.OpenAsync().ConfigureAwait(false);
            Init(isolationLevel, parameterMarker, namedParameters);
        }

        /// <summary>
        /// Init DB Factory.
        /// </summary>
        /// <param name="providerName"></param>
        /// <param name="connection"></param>
        private DbConnection InitFactory(string providerName, string connection = null)
        {
            providerName = (!string.IsNullOrEmpty(providerName)) ? providerName : DefaultProviderName;
            Factory = DbProviderFactories.GetFactory(providerName);

            if (connection != null)
            {
                Connection = Factory.CreateConnection();
                Connection.ConnectionString = connection;
                return Connection;
            }

            return null;
        }

        /// <summary>
        /// Initializes the DAL.
        /// </summary>
        /// <param name="isolationLevel"></param>
        /// <param name="parameterMarker"></param>
        /// <param name="namedParameters"></param>
        private void Init(IsolationLevel isolationLevel, char? parameterMarker, bool? namedParameters = null)
        {
            if (Connection != null && Connection.State != ConnectionState.Open)
                Connection.Open();

            IsolationLevel = isolationLevel;
            IgnoreMissingColumns = true;
            ColumnLookupTableNameValidation = true;
            NameComparison = StringComparison.OrdinalIgnoreCase;
            ParameterMarker = parameterMarker;
            NamedParameters = namedParameters;
            PageSize = 100;

            if (ParameterMarker == null)
                ParameterMarker = GetParameterMarker();

            if (NamedParameters == null)
                NamedParameters = (GetParameterNameMaxLength() > 0);
        }

        /// <summary>
        /// 
        /// </summary>
        public void HandleError()
        {
            if (_tr != null && _tr.Connection != null)
                _tr.Rollback();

            Dispose();
        }

        /// <summary>
        /// Disposes the dal.
        /// </summary>
        public void Dispose()
        {
            CloseReader();

            if (Connection.State != ConnectionState.Closed)
                Connection.Dispose();

            if (_tr != null)
                _tr.Dispose();

            ResetSettings();
            _limitedResult = false;
            _trCommandCount = 0;
            _tr = null;
            Factory = null;
        }

        /// <summary>
        /// Adds the catalog for the specified type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="catalogName"></param>
        /// <param name="schemaName"></param>
        /// <param name="serverName"></param>
        public void AddCatalog(Type type, string catalogName, string schemaName = null, string serverName = null)
        {
            _catalogMapping[type.FullName] = new Catalog(catalogName, schemaName, serverName);
        }

        /// <summary>
        /// Adds the catalog for the specified types.
        /// </summary>
        /// <param name="types"></param>
        /// <param name="catalogName"></param>
        /// <param name="schemaName"></param>
        /// <param name="serverName"></param>
        public void AddCatalog(List<Type> types, string catalogName, string schemaName = null, string serverName = null)
        {
            foreach (Type type in types)
            {
                _catalogMapping[type.FullName] = new Catalog(catalogName, schemaName, serverName);
            }
        }

        /// <summary>
        /// Removes the catalog for the specified type.
        /// </summary>
        /// <param name="type"></param>
        public void RemoveCatalog(Type type)
        {
            _catalogMapping.Remove(type.FullName);
        }

        /// <summary>
        /// Removes the catalog for the specified types.
        /// </summary>
        /// <param name="types"></param>
        public void RemoveCatalog(List<Type> types)
        {
            foreach (Type type in types)
            {
                _catalogMapping.Remove(type.FullName);
            }
        }

        /// <summary>
        /// Removes all catalogs.
        /// </summary>
        public void ClearCatalogs()
        {
            _catalogMapping.Clear();
        }

        /// <summary>
        /// Creates a new command for the current database provider.
        /// </summary>
        /// <returns>The command object instance</returns>
        public DbCommand CreateCommand()
        {
            return Factory.CreateCommand();
        }

        /// <summary>
        /// Creates a new command for the current database provider.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="parameters"></param>
        /// <param name="timeout"></param>
        /// <param name="type"></param>
        /// <returns>The command object instance</returns>
        public DbCommand CreateCommand(string text, List<IDataParameter> parameters = null, int? timeout = null, CommandType? type = null)
        {
            DbCommand cmd = Factory.CreateCommand();
            cmd.CommandText = text;

            if (parameters != null)
            {
                foreach (IDataParameter parameter in parameters)
                {
                    cmd.Parameters.Add(parameter);
                }
            }

            if (timeout.HasValue)
                cmd.CommandTimeout = timeout.Value;

            if (type.HasValue)
                cmd.CommandType = type.Value;

            return cmd;
        }

        /// <summary>
        /// Creates a new parameter for the current database provider.
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="direction"></param>
        /// <returns>The parameter name.</returns>
        public string CreateParameter(DbCommand cmd, string name, object value, ParameterDirection? direction = null)
        {
            IDataParameter param = CreateParameter(name, value, direction);
            cmd.Parameters.Add(param);
            return (this.NamedParameters.HasValue && this.NamedParameters.Value) ? param.ParameterName : param.ParameterName[0].ToString();
        }

        /// <summary>
        /// Creates a new parameter for the current database provider.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="direction"></param>
        /// <param name="type"></param>
        /// <returns>The parameter object instance</returns>
        public IDataParameter CreateParameter(string name, object value, ParameterDirection? direction = null, DbType? type = null)
        {
            IDataParameter param = Factory.CreateParameter();
            param.ParameterName = AddParameterMarker(name);
            param.Value = DBNull.Value;

            if (value != null)
                param.Value = value;

            if (direction.HasValue)
                param.Direction = direction.Value;

            if (type.HasValue)
                param.DbType = type.Value;

            return param;
        }

        /// <summary>
        /// Adds the parameter marker to the specified parameter name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns>The parameter name with marker.</returns>
        public string AddParameterMarker(string name)
        {
            string marker = (this.ParameterMarker.HasValue) ? ParameterMarker.ToString() : "@";

            if (!name.StartsWith(marker))
                return string.Format("{0}{1}", marker, name);
            else
                return name;
        }

        /// <summary>
        /// Gets the name of an instance property from a property access lambda.
        /// </summary>
        /// <typeparam name="T">Type of the property</typeparam>
        /// <param name="propertyExpression">Lambda expression in the form: '() => Class.Property'.</param>
        /// <returns>The name of the property.</returns>
        public string GetPropertyName<T>(Expression<Func<T>> propertyExpression)
        {
            return Common.GetPropertyName(propertyExpression);
        }

        /// <summary>
        /// Gets the name of an instance property from a property access lambda.
        /// </summary>
        /// <param name="propertyExpressions">A list of Lambda expression in the form: '() => Class.Property'.</param>
        /// <returns>The name of the property.</returns>
        public string GetPropertyNames(List<Expression<Func<object>>> propertyExpressions)
        {
            List<string> names = new List<string>();

            foreach (Expression<Func<object>> lambda in propertyExpressions)
            {
                names.Add(GetPropertyName(lambda));
            }

            return string.Join(".", names);
        }


        /// <summary>
        /// Creates a new instance of the FluentQuery class to build a query with fluent syntax.
        /// </summary>
        /// <typeparam name="T">The type of the Class.</typeparam>
        /// <param name="loadOneToMany">A value indicating if one-to-many relations of the target type should be loaded.</param>
        /// <param name="loadOneToOne">A value indicating if one-to-one relations of the target type should be loaded.</param>
        /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
        public FluentQuery<T> Query<T>(bool loadOneToMany = true, bool loadOneToOne = true)
        {
            return new FluentQuery<T>(this, loadOneToMany, loadOneToOne);
        }

        /// <summary>
        /// Creates a new instance of the FluentQuery class to build a query with fluent syntax and initializes the where clause.
        /// </summary>
        /// <typeparam name="T">The type of the Class.</typeparam>
        /// <param name="propertyExpression">Lambda expression of the form: '() => Class.Property'.</param>
        /// <param name="openGroups">Count of groups to open</param>
        /// <param name="loadOneToMany">A value indicating if one-to-many relations of the target type should be loaded.</param>
        /// <param name="loadOneToOne">A value indicating if one-to-one relations of the target type should be loaded.</param>
        /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
        public FluentQuery<T> Where<T>(Expression<Func<object>> propertyExpression, int openGroups = 0, bool loadOneToMany = true, bool loadOneToOne = true)
        {
            FluentQuery<T> query = new FluentQuery<T>(this, loadOneToMany, loadOneToOne);
            return query.Where(propertyExpression);
        }

        /// <summary>
        /// Creates a new instance of the FluentQuery class to build a query with fluent syntax and initializes the where clause.
        /// </summary>
        /// <typeparam name="T">The type of the Class.</typeparam>
        /// <param name="propertyName">The property name of the root class, dot separated tree name for parent/child classes or empty if a filter is not required.</param>
        /// <param name="openGroups">Count of groups to open</param>
        /// <param name="loadOneToMany">A value indicating if one-to-many relations of the target type should be loaded.</param>
        /// <param name="loadOneToOne">A value indicating if one-to-one relations of the target type should be loaded.</param>
        /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
        public FluentQuery<T> Where<T>(string propertyName = null, int openGroups = 0, bool loadOneToMany = true, bool loadOneToOne = true)
        {
            FluentQuery<T> query = new FluentQuery<T>(this, loadOneToMany, loadOneToOne);
            return query.Where(propertyName);
        }

        /// <summary>
        /// Executes a query and returns the result as a list of T.
        /// </summary>
        /// <typeparam name="T">Type of the objects in the returned list.</typeparam>
        /// <param name="cmd">The command object.</param>
        /// <param name="pageIndex">The page index.</param>
        /// <returns>A List of T.</returns>
        public List<T> Query<T>(DbCommand cmd, int? pageIndex = null)
        {
            try
            {
                List<T> list = new List<T>();
                HashSet<object> listSet = new HashSet<object>();
                Type type = typeof(T);
                TotalItemCount = null;
                cmd.Connection = Connection;

                using (cmd)
                {
                    if (!_limitedResult)
                        PagedResult<T>(cmd, pageIndex);

                    BeginTransaction(cmd);

                    using (DbDataReader reader = cmd.ExecuteReader(CommandBehavior.KeyInfo))
                    {
                        do
                        {
                            ReadDataReader(reader, type, list, listSet);
                        } while (reader.NextResult());
                    }

                    ResetSettings();
                    CommitTransaction();

                    return list;
                }
            }
            catch
            {
                HandleError();
                throw;
            }
        }

        /// <summary>
        /// Executes a query and returns the result as a list of T.
        /// </summary>
        /// <typeparam name="T">Type of the objects in the returned list.</typeparam>
        /// <param name="cmd">The command object.</param>
        /// <param name="pageIndex">The page index.</param>
        /// <returns>A List of T.</returns>
        public async Task<List<T>> QueryAsync<T>(DbCommand cmd, int? pageIndex = null)
        {
            try
            {
                List<T> list = new List<T>();
                HashSet<object> listSet = new HashSet<object>();
                Type type = typeof(T);
                TotalItemCount = null;
                cmd.Connection = Connection;

                using (cmd)
                {
                    if (!_limitedResult)
                        PagedResult<T>(cmd, pageIndex);

                    BeginTransaction(cmd);

                    using (DbDataReader reader = await cmd.ExecuteReaderAsync(CommandBehavior.KeyInfo).ConfigureAwait(false))
                    {
                        do
                        {
                            ReadDataReader(reader, type, list, listSet);
                        } while (reader.NextResult());
                    }

                    ResetSettings();
                    CommitTransaction();

                    return list;
                }
            }
            catch
            {
                HandleError();
                throw;
            }
        }

        private void ReadDataReader<T>(DbDataReader reader, Type type, List<T> list, HashSet<object> listSet)
        {
            if (TotalItemCount == null)
            {
                if (_limitedResult && reader.Read()) // first result contains total rowcount
                {
                    TotalItemCount = reader.GetInt32(0);
                    reader.NextResult();
                }
                else
                    TotalItemCount = 0;
            }

            InitQuery(reader);

            while (reader.Read())
            {
                AddToCollection(reader, type, null, "", "", "", new Dictionary<string, string>(), list, listSet);

                if (!_limitedResult)
                    TotalItemCount++;
            }
        }

        /// <summary>
        /// Executes a query and returns the result as DbDataReader.
        /// </summary>
        /// <param name="cmd">The command object.</param>
        /// <param name="pageIndex">The page index.</param>
        /// <returns>An DbDataReader object.</returns>
        public DbDataReader QueryAsReader(DbCommand cmd, int? pageIndex = null)
        {
            try
            {
                TotalItemCount = null;
                cmd.Connection = Connection;

                using (cmd)
                {
                    if (!_limitedResult)
                        PagedResult(cmd, pageIndex);

                    CloseReader();
                    BeginTransaction(cmd);
                    _reader = cmd.ExecuteReader(CommandBehavior.KeyInfo);

                    if (_limitedResult && _reader.Read()) // first result contains total rowcount
                    {
                        TotalItemCount = _reader.GetInt32(0);
                        _reader.NextResult();
                    }

                    CommitTransaction();

                    return _reader;
                }
            }
            catch
            {
                HandleError();
                throw;
            }
        }

        /// <summary>
        /// Executes a query and returns the result as DbDataReader.
        /// </summary>
        /// <param name="cmd">The command object.</param>
        /// <param name="pageIndex">The page index.</param>
        /// <returns>An DbDataReader object.</returns>
        public async Task<DbDataReader> QueryAsReaderAsync(DbCommand cmd, int? pageIndex = null)
        {
            try
            {
                TotalItemCount = null;
                cmd.Connection = Connection;

                using (cmd)
                {
                    if (!_limitedResult)
                        PagedResult(cmd, pageIndex);

                    CloseReader();
                    BeginTransaction(cmd);
                    _reader = await cmd.ExecuteReaderAsync(CommandBehavior.KeyInfo).ConfigureAwait(false);

                    if (_limitedResult && _reader.Read()) // first result contains total rowcount
                    {
                        TotalItemCount = _reader.GetInt32(0);
                        _reader.NextResult();
                    }

                    CommitTransaction();

                    return _reader;
                }
            }
            catch
            {
                HandleError();
                throw;
            }
        }

        /// <summary>
        /// Executes a query and returns the result as a dataset.
        /// </summary>
        /// <param name="cmd">Command object.</param>
        /// <param name="pageIndex">The page index.</param>
        /// <returns></returns>
        public DataSet QueryAsDataSet(DbCommand cmd, int? pageIndex = null)
        {
            try
            {
                DataSet ds = new DataSet();
                cmd.Connection = Connection;
                int index = -1;

                TotalItemCount = null;

                using (cmd)
                {
                    if (!_limitedResult)
                        PagedResult(cmd, pageIndex);

                    BeginTransaction(cmd);

                    using (DbDataReader reader = cmd.ExecuteReader(CommandBehavior.KeyInfo))
                    {
                        do
                        {
                            FillDataSet(reader, ds, ++index);

                        } while (!reader.IsClosed);
                    }

                    CommitTransaction();
                    return ds;
                }
            }
            catch
            {
                HandleError();
                throw;
            }
        }

        /// <summary>
        /// Executes a query and returns the result as a dataset.
        /// </summary>
        /// <param name="cmd">Command object.</param>
        /// <param name="pageIndex">The page index.</param>
        /// <returns></returns>
        public async Task<DataSet> QueryAsDataSetAsync(DbCommand cmd, int? pageIndex = null)
        {
            try
            {
                DataSet ds = new DataSet();
                cmd.Connection = Connection;
                int index = -1;

                TotalItemCount = null;

                using (cmd)
                {
                    if (!_limitedResult)
                        PagedResult(cmd, pageIndex);

                    BeginTransaction(cmd);

                    using (DbDataReader reader = await cmd.ExecuteReaderAsync(CommandBehavior.KeyInfo).ConfigureAwait(false))
                    {
                        do
                        {
                            FillDataSet(reader, ds, ++index);
                        } while (!reader.IsClosed);
                    }

                    CommitTransaction();
                    return ds;
                }
            }
            catch
            {
                HandleError();
                throw;
            }
        }

        private void FillDataSet(DbDataReader reader, DataSet ds, int index)
        {
            if (_limitedResult && index == 0 && reader.Read()) // first result contains total rowcount
            {
                TotalItemCount = reader.GetInt32(0);
                reader.NextResult();
            }

            ds.Tables.Add(new DataTable());
            ds.Tables[index].Load(reader);
        }


        /// <summary>
        /// Executes a query and returns the result as T.
        /// </summary>
        /// <typeparam name="T">Type of the returned object.</typeparam>
        /// <param name="cmd">Command object.</param>
        /// <returns></returns>
        public T Scalar<T>(DbCommand cmd)
        {
            try
            {
                T result;
                cmd.Connection = Connection;

                using (cmd)
                {
                    BeginTransaction(cmd);
                    result = (T)cmd.ExecuteScalar();
                    CommitTransaction();

                    return result;
                }
            }
            catch
            {
                HandleError();
                throw;
            }
        }

        /// <summary>
        /// Executes a query and returns the result as T.
        /// </summary>
        /// <typeparam name="T">Type of the returned object.</typeparam>
        /// <param name="cmd">Command object.</param>
        /// <returns></returns>
        public async Task<T> ScalarAsync<T>(DbCommand cmd)
        {
            try
            {
                T result;
                cmd.Connection = Connection;

                using (cmd)
                {
                    BeginTransaction(cmd);
                    result = (T)await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                    CommitTransaction();

                    return result;
                }
            }
            catch
            {
                HandleError();
                throw;
            }
        }


        /// <summary>
        /// Executes a query and returns the number of rows affected.
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public int NonQuery(DbCommand cmd)
        {
            try
            {
                int result;
                cmd.Connection = Connection;

                using (cmd)
                {
                    BeginTransaction(cmd);
                    result = cmd.ExecuteNonQuery();
                    CommitTransaction();

                    return result;
                }
            }
            catch
            {
                HandleError();
                throw;
            }
        }

        /// <summary>
        /// Executes a query and returns the number of rows affected.
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public async Task<int> NonQueryAsync(DbCommand cmd)
        {
            try
            {
                int result;
                cmd.Connection = Connection;

                using (cmd)
                {
                    BeginTransaction(cmd);
                    result = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    CommitTransaction();

                    return result;
                }
            }
            catch
            {
                HandleError();
                throw;
            }
        }


        /// <summary>
        /// Gets the column metadata for the specified table.
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns>A DataTable that describes the column metadata of the specified table.</returns>
        public DataTable GetSchemaTable(string tableName)
        {
            DataTable schema = null;

            try
            {
                DbCommand cmd = Factory.CreateCommand();
                cmd.Connection = Connection;

                switch (SqlTopLimiter)
                {
                    case SqlTopLimitOption.Top:
                        cmd.CommandText = string.Format("SELECT TOP 1 * FROM {0}", tableName);
                        break;
                    case SqlTopLimitOption.RowNum:
                        cmd.CommandText = string.Format("SELECT * FROM {0} WHERE ROWNUM = 1", tableName);
                        break;
                    default:
                        cmd.CommandText = string.Format("SELECT * FROM {0} LIMIT 1", tableName);
                        break;
                }

                using (cmd)
                {
                    BeginTransaction(cmd);

                    using (DbDataReader reader = cmd.ExecuteReader(CommandBehavior.KeyInfo))
                    {
                        schema = reader.GetSchemaTable();
                    }

                    CommitTransaction();

                    return schema;
                }
            }
            catch
            {
                HandleError();
                throw;
            }
        }
        /// <summary>
        /// Limit query result by page index for flat query.
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="pageIndex"></param>
        /// <returns></returns>
        public DbCommand PagedResult(DbCommand cmd, int? pageIndex = null)
        {
            if (!PageSize.HasValue || PageSize.Value < 1 || !pageIndex.HasValue)
                return cmd;

            if (pageIndex.Value < 1)
                pageIndex = 1;

            return LimitResult(cmd, ((pageIndex.Value - 1) * PageSize.Value), PageSize.Value);
        }


        /// <summary>
        /// Limit query result by page index for query of known type. 
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="pageIndex"></param>
        /// <returns></returns>
        public DbCommand PagedResult<T>(DbCommand cmd, int? pageIndex = null)
        {
            if (!PageSize.HasValue || PageSize.Value < 1 || !pageIndex.HasValue)
                return cmd;

            if (pageIndex.Value < 1)
                pageIndex = 1;

            return LimitResult<T>(cmd, ((pageIndex.Value - 1) * PageSize.Value), PageSize.Value);
        }

        /// <summary>
        /// Limit result for flat query.
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="skip"></param>
        /// <param name="take"></param>
        /// <returns></returns>
        public DbCommand LimitResult(DbCommand cmd, int skip, int take)
        {
            Regex orderRegEx = new Regex("\\s*ORDER\\s*BY\\s*((?:[^;]*)?)(?=\\s*(?:;|$))", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            Match orderMatch = orderRegEx.Match(cmd.CommandText);
            string innerQuery = cmd.CommandText;
            string order = string.Empty, query, limit;
            string uniqueId = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Replace("/", "-").Replace("+", "_").Replace("=", "");
            string rownumAlias = Escape("R" + uniqueId);
            string innerAlias = Escape("T" + uniqueId);

            if (SqlResultLimiter == SqlLimitOption.Row_Number_Over && !orderMatch.Success)
                throw new System.Exception("An ORDER BY clause in the outer query is required to limit results with ROW_NUMBER_OVER.");

            if (SqlResultLimiter == SqlLimitOption.Row_Number_Over)
            {
                limit = string.Format("{0} > {1} AND {0} <= {2}", rownumAlias, skip, skip + take);
                order = orderMatch.Value; // order by clause
                innerQuery = innerQuery.Substring(0, orderMatch.Index); // query without order by clause
            }
            else if (SqlResultLimiter == SqlLimitOption.Limit)
                limit = string.Format("LIMIT {0}, {1}", skip, take);
            else
                limit = string.Format("LIMIT {0}, OFFSET {1}", take, skip);

            // select all columns from the subquery and include paging filter
            cmd.CommandText = string.Format("SELECT COUNT(*) FROM ({0}) T;", innerQuery);

            if (SqlResultLimiter == SqlLimitOption.Row_Number_Over)
            {
                // subquery which includes the row number in the select
                query = string.Format("SELECT *, ROW_NUMBER() OVER ({0}) {1} FROM ({2}) {3}", order, rownumAlias, innerQuery, innerAlias);
                cmd.CommandText += string.Format("SELECT * FROM ({0}) {1} WHERE {2} ORDER BY {3}", query, innerAlias, limit, rownumAlias);
            }
            else
                cmd.CommandText += string.Format("SELECT * FROM ({0}) {1} {2} {3}", innerQuery, innerAlias, order, limit);

            _limitedResult = true;
            return cmd;
        }

        /// <summary>
        /// Limit result for query of known type.
        /// </summary>
        ///<typeparam name="T"></typeparam>
        /// <param name="cmd"></param>
        /// <param name="skip"></param>
        /// <param name="take"></param>
        /// <returns></returns>
        public DbCommand LimitResult<T>(DbCommand cmd, int skip, int take)
        {
            string sql = cmd.CommandText;
            Type type = typeof(T);
            string tableName = IncludeCatalog(type, GetTableName(type));
            List<string> keyFields = GetKeyFields(typeof(T));
            string escapeStart = GetEscapeChar();
            string escapeEnd = GetEscapeChar(true);
            Regex fromRegEx = new Regex(string.Format("FROM {0}(\\sAS)?\\s{1}([\\w\\s-]*){2}", tableName, escapeStart, escapeEnd), RegexOptions.IgnoreCase | RegexOptions.Multiline);
            Match fromMatch = fromRegEx.Match(sql);
            Regex orderRegEx = new Regex("\\s*ORDER\\s*BY\\s*((?:[^;]*)?)(?=\\s*(?:;|$))", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            Match orderMatch = orderRegEx.Match(sql);
            string uniqueId = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Replace("/", "-").Replace("+", "_").Replace("=", "");
            string tempAlias = Escape("#" + uniqueId);
            string tempJoinAlias = Escape("tmp" + uniqueId);
            string joinTableAlias = Escape("T" + uniqueId);
            string rownumAlias = Escape("R" + uniqueId);
            List<string> selectFields = new List<string>();
            List<string> keyAliases = new List<string>();
            List<string> joinFields = new List<string>();
            List<string> joinTopOneFields = new List<string>();
            List<string> rawOrderFields = new List<string>();
            List<string> orderFields = new List<string>();
            List<string> orderAliases = new List<string>();
            string tableAlias, orderAlias, innerSql, rowSql, distinctSql, limitSql, newSql, orgfld, keyAlias;
            string orderSql = string.Empty;
            int index = 0;

            if (!fromMatch.Success)
                throw new System.Exception(string.Format("A table alias name for model '{0}' (table '{1}') is required in the query to limit results.", type.FullName, tableName));

            if (keyFields.Count == 0)
                throw new System.Exception(string.Format("The key fields should be specified for model '{0}' (table '{1}') to limit results.", type.FullName, tableName));

            if (SqlResultLimiter == SqlLimitOption.Row_Number_Over && !orderMatch.Success)
                throw new System.Exception("The query must end with an ORDER BY clause to limit results with ROW_NUMBER_OVER.");

            tableAlias = Escape(fromMatch.Groups[2].Value);

            foreach (string fld in keyFields)
            {
                keyAlias = Escape("K" + uniqueId + index++);
                keyAliases.Add(keyAlias); // holds the key field aliases
                selectFields.Add(string.Format("{0}.{1} {2}", tableAlias, Escape(fld), keyAlias)); // key fields to add to select statement

                if (SqlResultLimiter == SqlLimitOption.Row_Number_Over)
                {
                    joinTopOneFields.Add(string.Format("{0}={1}.{0}", keyAlias, tempJoinAlias));
                    joinFields.Add(string.Format("{0}.{1}={2}.{1}", joinTableAlias, keyAlias, tempAlias));
                }
                else
                    joinFields.Add(string.Format("{0}.{1}={2}.{3}", joinTableAlias, keyAlias, tableAlias, Escape(fld)));
            }

            sql = sql.Insert(fromMatch.Index - 1, " ," + string.Join(",", selectFields) + " "); // add key fields to select
            orderMatch = orderRegEx.Match(sql); // match again after adding key fields
            fromMatch = fromRegEx.Match(sql); // match again after adding key fields

            if (orderMatch.Success)
            {
                orderSql = sql.Substring(orderMatch.Index);
                innerSql = sql.Substring(0, orderMatch.Index).Substring(fromMatch.Index + fromMatch.Length); // inner sql after from and without order
            }
            else
                innerSql = sql.Substring(fromMatch.Index + fromMatch.Length); // inner sql after from

            if (SqlResultLimiter == SqlLimitOption.Row_Number_Over)
            {
                // a temporary table is used to speed things up, because of this, table schema data is missing and table name validation cannot be used.
                _tableNameValidationValue = this.ColumnLookupTableNameValidation;
                this.ColumnLookupTableNameValidation = false;

                rawOrderFields.AddRange(SplitOrderBy(Regex.Replace(orderSql.Trim(), "ORDER BY ", "", RegexOptions.IgnoreCase)));
                index = 0;

                foreach (string fld in rawOrderFields)
                {
                    orderAlias = Escape("O" + uniqueId + index++);
                    var trimmedField = fld.Trim();
                    var orderKeywordIndex = Math.Max(
                        trimmedField.IndexOf(" DESC", StringComparison.OrdinalIgnoreCase),
                        trimmedField.IndexOf(" ASC", StringComparison.OrdinalIgnoreCase)
                    );

                    orgfld = orderKeywordIndex > -1 ? trimmedField.Substring(0, orderKeywordIndex).Trim() : trimmedField;

                    if (fld.IndexOf(" DESC", StringComparison.OrdinalIgnoreCase) > -1)
                        orderAliases.Add(orderAlias + " DESC");
                    else
                        orderAliases.Add(orderAlias);

                    orderFields.Add(string.Format("(SELECT TOP 1 {0} FROM {1} WHERE {2} {3}) {4}", orgfld, tempAlias, string.Join(" AND ", joinTopOneFields), orderSql, orderAlias));
                }

                // create temp table
                cmd.CommandText = string.Format("SELECT * INTO {0} FROM ({1}) T {2};", tempAlias, sql.Substring(0, orderMatch.Index), sql.Substring(orderMatch.Index));
                // count total records
                cmd.CommandText += string.Format("SELECT COUNT(*) FROM (SELECT DISTINCT {0} FROM {1}) T;", string.Join(",", keyAliases), tempAlias);

                // query with limit
                // distinct primary table with order fields
                distinctSql = string.Format("SELECT DISTINCT {0}, {1} FROM {2} {3}", string.Join(",", keyAliases), string.Join(",", orderFields), tempAlias, tempJoinAlias);
                // distinct total with row number
                rowSql = string.Format("SELECT *, ROW_NUMBER() OVER (ORDER BY {0}) {1} FROM ({2}) T", string.Join(",", orderAliases), rownumAlias, distinctSql);
                // limit result
                joinFields.Add(string.Format("{0} > {1} AND {0} <= {2}", rownumAlias, skip, skip + take));

                // total sql
                newSql = string.Format("SELECT * FROM {0} JOIN ({1}) {2} ON {3} {4};", tempAlias, rowSql, joinTableAlias, string.Join(" AND ", joinFields), orderSql);
                cmd.CommandText += newSql;
                cmd.CommandText += string.Format("DROP TABLE {0};", tempAlias);
            }
            else if (SqlResultLimiter == SqlLimitOption.RowNum || SqlResultLimiter == SqlLimitOption.Limit || SqlResultLimiter == SqlLimitOption.Limit_Offset)
            {
                if (SqlResultLimiter == SqlLimitOption.RowNum)
                    limitSql = string.Format(" ROWNUM > {0} AND ROWNUM <= {1}", skip, skip + take);
                if (SqlResultLimiter == SqlLimitOption.Limit)
                    limitSql = string.Format(" LIMIT {0}, {1}", skip, take);
                else
                    limitSql = string.Format(" LIMIT {0}, OFFSET {1}", take, skip);

                distinctSql = string.Format("SELECT DISTINCT {0} FROM {1} {2} {3}", string.Join(",", selectFields), tableName, tableAlias, innerSql);
                newSql = string.Format("FROM {0} {1} JOIN ({2}{3}) {4} ON {5} ", tableName, tableAlias, distinctSql, limitSql, joinTableAlias, string.Join(" AND ", joinFields));

                // count total records
                cmd.CommandText = string.Format("SELECT COUNT(*) FROM ({0}) T;", distinctSql);
                // query with limit
                cmd.CommandText += fromRegEx.Replace(sql, newSql, 1);
            }

            _limitedResult = true;
            return cmd;
        }

        /// <summary>
        ///  Handles splitting of the ORDER BY clause, avoiding commas inside function calls like JSON_VALUE(column, property).
        /// </summary>
        /// <param name="orderByClause"></param>
        /// <returns></returns>
        private List<string> SplitOrderBy(string orderByClause)
        {
            var result = new List<string>();
            int parenCount = 0;
            string currentField = string.Empty;

            for (int i = 0; i < orderByClause.Length; i++)
            {
                char ch = orderByClause[i];

                if (ch == '(') parenCount++;
                if (ch == ')') parenCount--;

                // Split only if the comma is outside parentheses (ignoring JSON_VALUE and similar functions)
                if (ch == ',' && parenCount == 0)
                {
                    result.Add(currentField.Trim());
                    currentField = string.Empty;
                }
                else
                {
                    currentField += ch;
                }
            }

            if (!string.IsNullOrWhiteSpace(currentField))
                result.Add(currentField.Trim());

            return result;
        }

        /// <summary>
        /// Loads and fills the object with the retrieved data from the corresponding database record.
        /// </summary>
        /// <typeparam name="T">Object type</typeparam>
        /// <param name="obj"></param>
        /// <param name="loadOneToMany">A value indicating if one-to-many relations of the target type should be loaded.</param>
        /// <param name="loadOneToOne">A value indicating if one-to-one relations of the target type should be loaded.</param>
        /// <param name="sortOrder">The sort order by specifying a KeyValuePair with the property name as key and a boolean value to indicate if the sort order is ascending(false) or descending(true).</param>
        /// <returns>The loaded object.</returns>
        public T Load<T>(T obj, bool loadOneToMany = true, bool loadOneToOne = true, List<KeyValuePair<string, bool>> sortOrder = null)
        {
            var result = PrepareLoadQuery(obj, loadOneToMany, loadOneToOne, sortOrder).ToList();
            return result.FirstOrDefault();
        }

        /// <summary>
        /// Loads and fills the object with the retrieved data from the corresponding database record.
        /// </summary>
        /// <typeparam name="T">Object type</typeparam>
        /// <param name="obj"></param>
        /// <param name="loadOneToMany">A value indicating if one-to-many relations of the target type should be loaded.</param>
        /// <param name="loadOneToOne">A value indicating if one-to-one relations of the target type should be loaded.</param>
        /// <param name="sortOrder">The sort order by specifying a KeyValuePair with the property name as key and a boolean value to indicate if the sort order is ascending(false) or descending(true).</param>
        /// <returns>The loaded object.</returns>
        public async Task<T> LoadAsync<T>(T obj, bool loadOneToMany = true, bool loadOneToOne = true, List<KeyValuePair<string, bool>> sortOrder = null)
        {
            var result = await PrepareLoadQuery(obj, loadOneToMany, loadOneToOne, sortOrder).ToListAsync().ConfigureAwait(false);
            return result.FirstOrDefault();
        }

        private FluentQuery<T> PrepareLoadQuery<T>(T obj, bool loadOneToMany = true, bool loadOneToOne = true, List<KeyValuePair<string, bool>> sortOrder = null)
        {
            TableData tableData = GetSchemaTableData(typeof(T), true);
            List<PropertyInfo> properties = TypeCache.GetType(typeof(T)).Properties;
            PropertyInfo property;
            string columnName;
            FluentQuery<T> query = this.Query<T>(loadOneToMany, loadOneToOne);

            foreach (DataRow key in tableData.KeyColumns)
            {
                columnName = key.Field<string>("ColumnName");
                property = properties.Find(x => GetColumnName(x).Equals(columnName, NameComparison));
                query = query.Where(property.Name).EqualTo(property.GetValue(obj, null));
            }

            if (sortOrder != null)
            {
                foreach (KeyValuePair<string, bool> item in sortOrder)
                {
                    query = query.OrderBy(item.Key, item.Value);
                }
            }

            return query;
        }

        /// <summary>
        ///     Saves the dataset's corresponding records in the database.
        /// </summary>
        /// <param name="ds"></param>
        /// <param name="selectCmd"></param>
        public void Save(DataSet ds, DbCommand selectCmd)
        {
            try
            {
                DbDataAdapter da = Factory.CreateDataAdapter();
                DbCommandBuilder builder = Factory.CreateCommandBuilder();

                da.SelectCommand = selectCmd;
                builder.DataAdapter = da;

                da.InsertCommand = builder.GetInsertCommand();
                da.UpdateCommand = builder.GetUpdateCommand();
                da.DeleteCommand = builder.GetDeleteCommand();

                da.Update(ds);
            }
            catch
            {
                HandleError();
                throw;
            }
        }

        /// <summary>
        /// Saves a collection of objects to the database asynchronously, inserting new records and updating existing ones.
        /// </summary>
        /// <param name="items">List of objects to save.</param>
        /// <param name="deep">Save child objects.</param>
        /// <param name="refreshModel">Defines if the object model is refreshed after saving.</param>
        /// <param name="globalTransaction">Defines if a global transaction is used to store and refresh the object and it's child objects.</param>
        public void SaveSet<T>(IEnumerable items, bool deep = true, bool refreshModel = true, bool globalTransaction = true)
        {
            try
            {
                if (globalTransaction)
                    BeginTransaction();

                foreach (object obj in items)
                {
                    CommitChanges(obj, deep, refreshModel, true, null);
                }

                if (globalTransaction)
                    CommitTransaction();
            }
            catch
            {
                HandleError();
                throw;
            }
        }

        /// <summary>
        ///     Saves the object's corresponding record in the database table.
        /// </summary>
        /// <param name="obj">Object to save.</param>
        /// <param name="deep">Save child objects.</param>
        /// <param name="refreshModel">Defines if the object model is refreshed after saving.</param>
        /// <param name="globalTransaction">Defines if a global transaction is used to store and refresh the object and it's child objects.</param>
        public void Save(object obj, bool deep = true, bool refreshModel = true, bool globalTransaction = true)
        {
            try
            {
                if (globalTransaction)
                    BeginTransaction();

                CommitChanges(obj, deep, refreshModel, true, null);

                if (globalTransaction)
                    CommitTransaction();
            }
            catch
            {
                HandleError();
                throw;
            }
        }

        /// <summary>
        /// Inserts a batch of new records into the database asynchronously without checking for existing records. Faster than <see cref="SaveSetAsync{T}"/> for scenarios where records are guaranteed to be new, such as after a table truncate.
        /// Does not support storing child objects or refreshing models after saving.
        /// </summary>
        /// <typeparam name="T">The type of objects to insert.</typeparam>
        /// <param name="items">List of new objects to insert.</param>
        /// <param name="globalTransaction">Defines if a global transaction is used for all inserts.</param>
        public async Task SaveBatchAsync<T>(IEnumerable items, bool globalTransaction = true)
        {
            try
            {
                if (globalTransaction)
                    BeginTransaction();

                foreach (T obj in items)
                    await CommitChangesAsync(obj, false, false, false, null).ConfigureAwait(false);

                if (globalTransaction)
                    CommitTransaction();
            }
            catch
            {
                HandleError();
                throw;
            }
        }

        /// <summary>
        /// Saves a collection of objects to the database asynchronously, inserting new records and updating existing ones.
        /// </summary>
        /// <typeparam name="T">The type of objects to save.</typeparam>
        /// <param name="items">List of objects to save.</param>
        /// <param name="deep">Whether to also save child objects.</param>
        /// <param name="refreshModel">Whether to refresh the object model after saving, 
        /// updating properties such as generated identity values.</param>
        /// <param name="globalTransaction">Defines if a global transaction is used for all saves.</param>
        public async Task SaveSetAsync<T>(IEnumerable items, bool deep = true, bool refreshModel = true, bool globalTransaction = true)
        {
            try
            {
                if (globalTransaction)
                    BeginTransaction();

                foreach (T obj in items)
                    await CommitChangesAsync(obj, deep, refreshModel, true, null).ConfigureAwait(false);

                if (globalTransaction)
                    CommitTransaction();
            }
            catch
            {
                HandleError();
                throw;
            }
        }

        /// <summary>
        ///  Saves the object's corresponding record in the database table, inserting new records and updating existing ones.
        /// </summary>
        /// <param name="obj">Object to save.</param>
        /// <param name="deep">Save child objects.</param>
        /// <param name="refreshModel">Defines if the object model is refreshed after saving.</param>
        /// <param name="globalTransaction">Defines if a global transaction is used to store and refresh the object and it's child objects.</param>
        public async Task SaveAsync<T>(T obj, bool deep = true, bool refreshModel = true, bool globalTransaction = true)
        {
            try
            {
                if (globalTransaction)
                    BeginTransaction();

                await CommitChangesAsync(obj, deep, refreshModel, true, null).ConfigureAwait(false);

                if (globalTransaction)
                    CommitTransaction();
            }
            catch
            {
                HandleError();
                throw;
            }
        }

        /// <summary>
        ///  Deletes the object's corresponding records from the database table.
        /// </summary>
        /// <param name="list">List of objects to save.</param>
        /// <param name="deep">Delete child objects.</param>
        /// <param name="globalTransaction">Defines if a global transaction is used to remove the object and it's child objects.</param>
        public void DeleteSet<T>(IEnumerable list, bool deep = true, bool globalTransaction = true)
        {
            try
            {
                if (globalTransaction)
                    BeginTransaction();

                foreach (T obj in list)
                {
                    CommitDelete(obj, deep);
                }

                if (globalTransaction)
                    CommitTransaction();
            }
            catch
            {
                HandleError();
                throw;
            }
        }

        /// <summary>
        ///  Deletes the object's corresponding record from the database table.
        /// </summary>
        /// <param name="obj">Object to save.</param>
        /// <param name="deep">Delete child objects.</param>
        /// <param name="globalTransaction">Defines if a global transaction is used to remove the object and it's child objects.</param>
        public void Delete(object obj, bool deep = true, bool globalTransaction = true)
        {
            try
            {
                if (globalTransaction)
                    BeginTransaction();

                CommitDelete(obj, deep);

                if (globalTransaction)
                    CommitTransaction();
            }
            catch
            {
                HandleError();
                throw;
            }
        }

        /// <summary>
        ///     Deletes the object's corresponding records in the database table.
        /// </summary>
        /// <param name="list">List of objects to save.</param>
        /// <param name="deep">Delete child objects.</param>
        /// <param name="globalTransaction">Defines if a global transaction is used to store and refresh the object and it's child objects.</param>
        public async Task DeleteSetAsync<T>(IEnumerable list, bool deep = true, bool globalTransaction = true)
        {
            try
            {
                if (globalTransaction)
                    BeginTransaction();

                foreach (T obj in list)
                {
                    await CommitDeleteAsync(obj, deep).ConfigureAwait(false);
                }

                if (globalTransaction)
                    CommitTransaction();
            }
            catch
            {
                HandleError();
                throw;
            }
        }

        /// <summary>
        ///  Deletes the object's corresponding record from the database table.
        /// </summary>
        /// <param name="obj">Object to save.</param>
        /// <param name="deep">Delete child objects.</param>
        /// <param name="globalTransaction">Defines if a global transaction is used to remove the object and it's child objects.</param>
        public async Task DeleteAsync(object obj, bool deep = true, bool globalTransaction = true)
        {
            try
            {
                if (globalTransaction)
                    BeginTransaction();

                await CommitDeleteAsync(obj, deep).ConfigureAwait(false);

                if (globalTransaction)
                    CommitTransaction();
            }
            catch
            {
                HandleError();
                throw;
            }
        }

        #region "private"

        /// <summary>
        /// Begins a transaction and keeps track of new commands within the current transaction.
        /// </summary>
        private void BeginTransaction(DbCommand cmd = null)
        {
            if (_tr == null)
                _tr = Connection.BeginTransaction(IsolationLevel);

            if (_tr == null)
                return;

            if (cmd != null)
                cmd.Transaction = _tr;

            _trCommandCount++;
        }

        /// <summary>
        /// Keeps track of the commands within the current transaction and commits the transaction after the last command.
        /// </summary>
        private void CommitTransaction()
        {
            _limitedResult = false;

            if (_tr == null)
                return;

            _trCommandCount--;

            if (_trCommandCount == 0)
            {
                _tr.Commit();
                _tr = null;
            }
        }

        private void ResetSettings()
        {
            if (_tableNameValidationValue.HasValue)
            {
                this.ColumnLookupTableNameValidation = _tableNameValidationValue.Value;
                _tableNameValidationValue = null;
            }

            foreach (string treeName in _columnAliases)
            {
                this.ColumnAliasMapping.Remove(treeName);
            }

            _columnAliases = new List<string>();
        }

        /// <summary>
        /// Closes the global data reader.
        /// </summary>
        private void CloseReader()
        {
            if (_reader != null && !_reader.IsClosed)
                _reader.Close();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private char GetParameterMarker()
        {
            try
            {
                string parameterMarker = ((DbConnection)Connection).GetSchema(DbMetaDataCollectionNames.DataSourceInformation).Rows[0][DbMetaDataColumnNames.ParameterMarkerFormat].ToString();

                if (!string.IsNullOrEmpty(parameterMarker) && !parameterMarker.StartsWith("{0}"))
                    return (parameterMarker.Substring(0, 1)[0]);
                else
                    return '@';
            }
            catch
            {
                HandleError();
                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private int GetParameterNameMaxLength()
        {
            try
            {
                int? maxLength = (int?)((DbConnection)Connection).GetSchema(DbMetaDataCollectionNames.DataSourceInformation).Rows[0][DbMetaDataColumnNames.ParameterNameMaxLength];

                if (maxLength.HasValue)
                    return maxLength.Value;
                else
                    return 0;
            }
            catch
            {
                HandleError();
                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private string Escape(string name)
        {
            return string.Format("{0}{1}{2}", GetEscapeChar(), name, GetEscapeChar(true));
        }

        private string GetEscapeChar(bool end = false)
        {
            switch (SqlDelimitedIdentifier)
            {
                case SqlDelimitedIdentifierOption.BackQuotes:
                    return "`";
                case SqlDelimitedIdentifierOption.Brackets:
                    return (end) ? "]" : "[";
                default:
                    return "\"";
            }
        }

        /// <summary>
        /// Creates the table joins for the load method.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="parent"></param>
        /// <param name="loadOneToMany"></param>
        /// <param name="loadOneToOne"></param>
        /// <param name="columns"></param>
        /// <param name="tables"></param>
        /// <param name="treeName"></param>
        /// <param name="tableAliasMapping"></param>
        /// <param name="rlTypes"></param>
        /// <param name="rlTableData"></param>
        /// <param name="rlKeyInfoList"></param>
        /// <param name="rlTableAlias"></param>
        /// <param name="oneToManyRelation"></param>
        /// <param name="occurredTypes"></param>
        private void CreateJoins(Type type, Relation parent, bool loadOneToMany, bool loadOneToOne, List<string> columns, List<string> tables, string treeName, Dictionary<string, string> tableAliasMapping = null, HashSet<string> rlTypes = null, TableData rlTableData = null, List<KeyInfo> rlKeyInfoList = null, int rlTableAlias = 0, bool oneToManyRelation = true, Dictionary<string, int> occurredTypes = null)
        {
            TableData tableData = GetSchemaTableData(type, true);
            List<PropertyInfo> properties = TypeCache.GetType(type).Properties;
            PropInfo propInfo;
            Dictionary<string, Type> oneToMany = new Dictionary<string, Type>();
            Dictionary<string, Type> oneToOne = new Dictionary<string, Type>();
            DataAnnotations.ForeignKeyAttribute fk;
            List<string> join = new List<string>();
            List<KeyInfo> keyInfoList = new List<KeyInfo>();
            KeyInfo rlKeyInfo;
            string name, columnName, pkColumnName, fkColumnName, treeNameCurrent;
            string tableName = tableData.TableName;
            int tableAlias = tables.Count;
            var relation = new Relation(type, parent);
            Type rlType = (parent != null) ? parent.Type : null;
            var typeName = type.FullName;
            treeName = (string.IsNullOrEmpty(treeName)) ? type.Name : treeName;

            if (rlTypes == null)
                rlTypes = new HashSet<string>();

            // keep track of types and there table index in the result set
            if (occurredTypes == null)
                occurredTypes = new Dictionary<string, int>();

            if (occurredTypes.ContainsKey(typeName))
                occurredTypes[typeName] += 1;
            else
                occurredTypes[typeName] = 0;

            TableIndexMapping[treeName] = occurredTypes[typeName];

            foreach (PropertyInfo property in properties)
            {
                propInfo = GetPropInfo(property);
                name = property.Name;
                columnName = propInfo.ColumnName;

                // skip properties with notmapped attribute
                if (!propInfo.IsMapped || propInfo.HasIndexParameters)
                    continue;

                treeNameCurrent = string.Format("{0}.{1}", treeName, name);

                if (loadOneToMany && propInfo.IsClassList && !HasType(IgnoreTypes, propInfo.ItemType) && !rlTypes.Contains(typeName + "_" + propInfo.ItemType.FullName))
                {
                    oneToMany.Add(treeNameCurrent, propInfo.ItemType);
                }
                else if (loadOneToOne && !propInfo.IsClassList && propInfo.IsClass && !HasType(IgnoreTypes, propInfo.PropType) && (!propInfo.PropType.Equals(rlType) && !rlTypes.Contains(typeName + "_" + propInfo.PropType.FullName)))
                {
                    oneToOne.Add(treeNameCurrent, propInfo.PropType);
                }
                else if (!propInfo.IsClass && !propInfo.IsClassList)
                {
                    pkColumnName = fkColumnName = null;

                    // add unique column name
                    columns.Add(string.Format("T{0}.{1} {2}", tableAlias, Escape(columnName), Escape("T" + tableAlias + "_" + columnName)));
                    // add to column alias mapping
                    this.ColumnAliasMapping.Add(treeNameCurrent, "T" + tableAlias + "_" + columnName); // add column alias mapping
                    _columnAliases.Add(treeNameCurrent); // keep track of auto generated aliases so they can be removed after query execution

                    // add key info of current table
                    if (tableData.KeyColumns.FindIndex(x => x.Field<string>("ColumnName").Equals(columnName, NameComparison)) > -1)
                    {
                        keyInfoList.Add(new KeyInfo() { PropertyName = name, ColumnName = columnName });

                        if (!oneToManyRelation && rlType != null) // one to one relation
                        {
                            fkColumnName = FindFKColumnName(TypeCache.GetType(rlType).Properties, type, name, columnName, treeName);

                            if (!string.IsNullOrEmpty(fkColumnName))
                                join.Add(string.Format("T{0}.{1}=T{2}.{3}", tableAlias, Escape(columnName), rlTableAlias, Escape(fkColumnName)));
                        }
                    }

                    if (oneToManyRelation && rlType != null)
                    {
                        // the fk attribute holds the the primary key table and column name
                        fk = GetFKAttributes(property).Find(f => f.PKTable.Equals(GetTableName(rlType), NameComparison));

                        if (fk != null)
                        {
                            // property denoted as foreign key with the foreignkey attribute
                            pkColumnName = fk.PKColumn;
                        }
                        else if (AutoDetectForeignKeys && name.StartsWith(rlType.Name, NameComparison) && name.Length > rlType.Name.Length)
                        {
                            // property denoted as foreign key by property name (combination of parent class name and pk property name)
                            rlKeyInfo = rlKeyInfoList.Find(x => x.PropertyName.Equals(name.Substring(rlType.Name.Length), NameComparison));

                            if (rlKeyInfo != null)
                                pkColumnName = rlKeyInfo.ColumnName;
                        }

                        if (!string.IsNullOrEmpty(pkColumnName))
                            join.Add(string.Format("T{0}.{1}=T{2}.{3}", tableAlias, Escape(columnName), rlTableAlias, Escape(pkColumnName)));
                    }
                }
            }

            if (rlTableData == null)
            {
                // first table
                tables.Add(string.Format("{0} {1}", tableName, Escape("T0")));
            }
            else if (join.Count > 0)
            {
                // join relationship
                tables.Add(string.Format("LEFT JOIN {0} {1} ON {2}", tableName, Escape("T" + tableAlias), string.Join(" AND ", join)));
            }

            if (tableAliasMapping != null)
                tableAliasMapping.Add(treeName, string.Format("T{0}", tableAlias));

            foreach (string key in oneToMany.Keys)
            {
                rlTypes.Add(typeName + "_" + oneToMany[key].FullName);
                CreateJoins(oneToMany[key], relation, loadOneToMany, loadOneToOne, columns, tables, key, tableAliasMapping, new HashSet<string>(rlTypes), tableData, keyInfoList, tableAlias, true, occurredTypes);
            }

            foreach (string key in oneToOne.Keys)
            {
                rlTypes.Add(typeName + "_" + oneToOne[key].FullName);
                CreateJoins(oneToOne[key], relation, loadOneToMany, loadOneToOne, columns, tables, key, tableAliasMapping, new HashSet<string>(rlTypes), tableData, keyInfoList, tableAlias, false, occurredTypes);
            }
        }

        /// <summary>
        /// Checks if the type is in the specified types set.
        /// </summary>
        /// <param name="types"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private bool HasType(HashSet<Type> types, Type type)
        {
            if (types == null)
                return false;

            return types.Contains(type);
        }

        /// <summary>
        /// Finds the foreignkey column in the property list of the data relation.
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="type"></param>
        /// <param name="propertyName"></param>
        /// <param name="columnName"></param>
        /// <param name="treeName"></param>
        /// <returns>The foreignkey column name.</returns>
        private string FindFKColumnName(List<PropertyInfo> properties, Type type, string propertyName, string columnName, string treeName)
        {
            DataAnnotations.ForeignKeyAttribute fk;
            string name;
            string rlPropertyName = treeName.Substring(treeName.LastIndexOf(".") + 1);

            foreach (PropertyInfo property in properties)
            {
                name = property.Name;
                fk = GetFKAttributes(property).Find(f => f.PKTable.Equals(GetTableName(type), NameComparison));

                if ((fk != null && (columnName.Equals(fk.PKColumn, NameComparison) && (string.IsNullOrEmpty(fk.RelationalProperty) || string.IsNullOrEmpty(rlPropertyName) || rlPropertyName == fk.RelationalProperty)))
                    || (AutoDetectForeignKeys && name.Equals(string.Format("{0}{1}", type.Name, propertyName), NameComparison)))
                {
                    return GetColumnName(property);
                }
            }

            return null;
        }

        /// <summary>
        /// Commit changes.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="deep"></param>
        /// <param name="refreshModel"></param>
        /// <param name="parentType"></param>
        /// <param name="parentKeyInfoList"></param>
        private void CommitChanges(object obj, bool deep, bool refreshModel, bool existenceCheck = true, Type parentType = null, List<KeyInfo> parentKeyInfoList = null)
        {
            Type type = obj.GetType();
            List<PropertyInfo> properties = TypeCache.GetType(type).Properties;
            TableData tableData = GetSchemaTableData(type, true);
            DbCommand cmd = Factory.CreateCommand();
            List<KeyInfo> keyInfoList = new List<KeyInfo>();
            List<object> childs = new List<object>();
            Dictionary<string, IDataParameter> columns = CreateColumns(tableData, obj, deep, parentType, parentKeyInfoList, keyInfoList, childs);
            string where = (existenceCheck) ? CreateExistsCommand(tableData, cmd, columns) : null;
            Int64 result = 0;

            if (!string.IsNullOrEmpty(where))
                result = Convert.ToInt64(Scalar<object>(cmd));

            if (result == 0)
            {
                Insert(obj, columns, where, refreshModel);

                if (existenceCheck && deep)
                    UpdateKeyValues(obj, keyInfoList, properties);
            }
            else
                Update(obj, columns, where, refreshModel);

            foreach (object childObj in childs)
            {
                CommitChanges(childObj, deep, refreshModel, existenceCheck, type, keyInfoList);
            }
        }

        /// <summary>
        /// Commits the changes.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="deep"></param>
        /// <param name="refreshModel"></param>
        /// <param name="existenceCheck"></param>
        /// <param name="parentType"></param>
        /// <param name="parentKeyInfoList"></param>
        /// <returns></returns>
        private async Task CommitChangesAsync(object obj, bool deep, bool refreshModel, bool existenceCheck = true, Type parentType = null, List<KeyInfo> parentKeyInfoList = null)
        {
            Type type = obj.GetType();
            List<PropertyInfo> properties = TypeCache.GetType(type).Properties;
            TableData tableData = GetSchemaTableData(type, true);
            DbCommand cmd = Factory.CreateCommand();
            List<KeyInfo> keyInfoList = new List<KeyInfo>();
            List<object> childs = new List<object>();
            Dictionary<string, IDataParameter> columns = CreateColumns(tableData, obj, deep, parentType, parentKeyInfoList, keyInfoList, childs);
            string where = (existenceCheck) ? CreateExistsCommand(tableData, cmd, columns) : null;
            Int64 result = 0;

            if (!string.IsNullOrEmpty(where))
                result = Convert.ToInt64(await ScalarAsync<object>(cmd).ConfigureAwait(false));

            if (result == 0)
            {
                await InsertAsync(obj, columns, where, refreshModel).ConfigureAwait(false);

                if (existenceCheck && deep)
                    UpdateKeyValues(obj, keyInfoList, properties);
            }
            else
                await UpdateAsync(obj, columns, where, refreshModel).ConfigureAwait(false);

            foreach (object childObj in childs)
            {
                await CommitChangesAsync(childObj, deep, refreshModel, existenceCheck, type, keyInfoList).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Creates insert/update columns.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tableData"></param>
        /// <param name="obj"></param>
        /// <param name="deep"></param>
        /// <param name="parentType"></param>
        /// <param name="parentKeyInfoList"></param>
        /// <param name="keyInfoList"></param>
        /// <param name="childs"></param>
        /// <returns></returns>
        private Dictionary<string, IDataParameter> CreateColumns<T>(TableData tableData, T obj, bool deep, Type parentType, List<KeyInfo> parentKeyInfoList, List<KeyInfo> keyInfoList, List<object> childs)
        {
            Type type = obj.GetType();
            List<PropertyInfo> properties = TypeCache.GetType(type).Properties;
            string name;
            string columnName;
            List<ColumnData> columnDataList;
            ColumnData columnData;
            object value;
            Dictionary<string, IDataParameter> columns = new Dictionary<string, IDataParameter>(StringComparer.OrdinalIgnoreCase);
            DbCommand cmd = Factory.CreateCommand();
            KeyInfo parentKeyInfo;
            PropInfo propInfo;
            DataAnnotations.ForeignKeyAttribute fk;
            List<DataAnnotations.ForeignKeyAttribute> occurredFKs = new List<DataAnnotations.ForeignKeyAttribute>();

            foreach (PropertyInfo property in properties)
            {
                propInfo = GetPropInfo(property);
                // skip properties with notmapped attribute
                if (!propInfo.IsMapped || propInfo.HasIndexParameters || propInfo.IsReadOnly)
                    continue;

                name = property.Name;
                columnName = propInfo.ColumnName;
                value = property.GetValue(obj, null);
                columnData = null;

                if (value != null && deep && (propInfo.IsClassList || propInfo.IsClass))
                {
                    AddRelation(childs, value, propInfo.IsClassList);
                }
                else if (!propInfo.IsClassList && !propInfo.IsClass)
                {
                    // primitive
                    if (tableData.Columns.TryGetValue(columnName, out columnDataList))
                        columnData = columnDataList[0];

                    // add key info of current table for deep saves
                    if (tableData.KeyColumns.FindIndex(x => x.Field<string>("ColumnName").Equals(columnName, NameComparison)) > -1)
                        keyInfoList.Add(new KeyInfo() { PropertyName = name, ColumnName = columnName, Value = value });

                    if (propInfo.FKAttributes.Count > 0)
                        occurredFKs.AddRange(propInfo.FKAttributes);

                    if (parentKeyInfoList != null)
                    {
                        // the fk attribute holds the the primary key table and column name
                        fk = propInfo.FKAttributes.Find(f => f.PKTable.Equals(GetTableName(parentType), NameComparison));

                        if (fk != null)
                        {
                            // property denoted as foreign key with the foreignkey attribute
                            parentKeyInfo = parentKeyInfoList.Find(x => x.ColumnName.Equals(fk.PKColumn, NameComparison));
                            value = (parentKeyInfo != null) ? parentKeyInfo.Value : value;
                        }
                        else if (AutoDetectForeignKeys && name.StartsWith(parentType.Name, NameComparison) && name.Length > parentType.Name.Length)
                        {
                            // property denoted as foreign key by property name (combination of parent class name and pk property name)
                            parentKeyInfo = parentKeyInfoList.Find(x => x.PropertyName.Equals(name.Substring(parentType.Name.Length), NameComparison));
                            value = (parentKeyInfo != null) ? parentKeyInfo.Value : value;
                        }
                    }

                    if (columnData != null)
                    {
                        if (value != null || (columnData.AllowDBNull.HasValue && columnData.AllowDBNull.Value))
                        {
                            columns[columnName] = CreateParameter(name, value);
                        }

                    }
                }
            }

            RemoveOneToOneRelations(occurredFKs, childs);

            return columns;
        }

        private void AddRelation(List<object> items, object value, bool isClassList)
        {
            if (isClassList)
            {
                foreach (object childObj in (IList)value)
                {
                    items.Add(childObj);
                }
            }
            else
                items.Add(value);
        }

        private void RemoveOneToOneRelations(List<DataAnnotations.ForeignKeyAttribute> occurredFKs, List<object> items)
        {
            if (occurredFKs.Count > 0)
            {
                // remove relationships where the current type has the foreign key relation
                for (var index = items.Count - 1; index >= 0; index--)
                {
                    if (occurredFKs.Where(i => i.ParentType == items[index].GetType()).FirstOrDefault() != null)
                        items.RemoveAt(index);
                }
            }
        }

        /// <summary>
        /// Update key values after insert because the table could have an auto increment identity
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="keyInfoList"></param>
        /// <param name="properties"></param>
        private void UpdateKeyValues(object obj, List<KeyInfo> keyInfoList, List<PropertyInfo> properties)
        {
            foreach (KeyInfo keyInfo in keyInfoList)
            {
                keyInfo.Value = properties.Find(x => x.Name.Equals(keyInfo.PropertyName)).GetValue(obj, null);
            }
        }

        /// <summary>
        /// Inserts the object.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="columns"></param>
        /// <param name="where"></param>
        /// <param name="refreshModel"></param>
        private void Insert(object obj, Dictionary<string, IDataParameter> columns, string where, bool refreshModel)
        {
            Type type = obj.GetType();
            TableData tableData = GetSchemaTableData(type, true);
            bool hasIdentity = HasIdentity(tableData);
            string selectLastId = (!string.IsNullOrEmpty(this.SqlLastIdCommand)) ? this.SqlLastIdCommand : _SqlScopeIdentity;
            DbCommand cmd = CreateInsertCommand(type, tableData, columns);

            if (refreshModel && hasIdentity)
            {
                // add select last inserted identity to command
                cmd.CommandText += selectLastId;
                var id = Convert.ToInt64(Scalar<object>(cmd)); // execute insert and select last identity
                RefreshModel(obj, Query(obj.GetType(), CreateSelectCommand(tableData, columns, where, id)));
            }
            else
            {
                // execute insert
                NonQuery(cmd);

                if (refreshModel)
                    RefreshModel(obj, Query(obj.GetType(), CreateSelectCommand(tableData, columns, where)));
            }
        }

        /// <summary>
        /// Inserts the object.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="columns"></param>
        /// <param name="where"></param>
        /// <param name="refreshModel"></param>
        private async Task InsertAsync(object obj, Dictionary<string, IDataParameter> columns, string where, bool refreshModel)
        {
            Type type = obj.GetType();
            TableData tableData = GetSchemaTableData(type, true);
            bool hasIdentity = HasIdentity(tableData);
            string selectLastId = (!string.IsNullOrEmpty(this.SqlLastIdCommand)) ? this.SqlLastIdCommand : _SqlScopeIdentity;
            DbCommand cmd = CreateInsertCommand(type, tableData, columns);

            if (refreshModel && hasIdentity)
            {
                // add select last inserted identity to command
                cmd.CommandText += selectLastId;
                var id = Convert.ToInt64(await ScalarAsync<object>(cmd).ConfigureAwait(false)); // execute insert and select last identity
                RefreshModel(obj, await QueryAsync(obj.GetType(), CreateSelectCommand(tableData, columns, where, id)).ConfigureAwait(false));
            }
            else
            {
                // execute insert
                await NonQueryAsync(cmd).ConfigureAwait(false);

                if (refreshModel)
                    RefreshModel(obj, await QueryAsync(obj.GetType(), CreateSelectCommand(tableData, columns, where)).ConfigureAwait(false));
            }
        }

        /// <summary>
        /// Updates the object.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="columns"></param>
        /// <param name="where"></param>
        /// <param name="refreshModel"></param>
        private void Update(object obj, Dictionary<string, IDataParameter> columns, string where, bool refreshModel)
        {
            Type type = obj.GetType();
            TableData tableData = GetSchemaTableData(type, true);
            DbCommand cmd = CreateUpdateCommand(type, tableData, columns, where);

            if (string.IsNullOrWhiteSpace(cmd.CommandText))
                return;

            NonQuery(cmd);

            if (refreshModel)
                RefreshModel(obj, Query(obj.GetType(), CreateSelectCommand(tableData, columns, where)));
        }

        /// <summary>
        /// Updates the object.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="columns"></param>
        /// <param name="where"></param>
        /// <param name="refreshModel"></param>
        /// <returns></returns>
        private async Task UpdateAsync(object obj, Dictionary<string, IDataParameter> columns, string where, bool refreshModel)
        {
            Type type = obj.GetType();
            TableData tableData = GetSchemaTableData(type, true);
            DbCommand cmd = CreateUpdateCommand(type, tableData, columns, where);

            if (string.IsNullOrWhiteSpace(cmd.CommandText))
                return;

            await NonQueryAsync(cmd).ConfigureAwait(false);

            if (refreshModel)
                RefreshModel(obj, await QueryAsync(obj.GetType(), CreateSelectCommand(tableData, columns, where)).ConfigureAwait(false));
        }

        /// <summary>
        /// Refreshes the data model.
        /// </summary>
        /// <param name="original"></param>
        /// <param name="updated"></param>
        private void RefreshModel(object original, object updated)
        {
            Type type = original.GetType();
            List<PropertyInfo> properties = TypeCache.GetType(type).Properties;
            PropInfo propInfo;

            if (updated == null)
                return;

            foreach (PropertyInfo property in properties)
            {
                propInfo = GetPropInfo(property);

                // skip properties with notmapped attribute
                if (!propInfo.IsMapped || propInfo.HasIndexParameters)
                    continue;

                if (!propInfo.IsClassList && !propInfo.IsClass)
                {
                    property.SetValue(original, property.GetValue(updated, null));
                }
            }
        }

        /// <summary>
        /// Deletes the object.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="deep"></param>
        private void CommitDelete(object obj, bool deep)
        {
            Type type = obj.GetType();
            TableData tableData = GetSchemaTableData(type, true);
            List<object> childs = new List<object>();
            DbCommand cmd = CreateDeleteCommand(obj, type, tableData, deep, childs);

            foreach (object childObj in childs) // delete child objects first
            {
                CommitDelete(childObj, deep);
            }

            if (cmd.CommandText != null)
                NonQuery(cmd);
        }

        /// <summary>
        /// Deletes the object.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="deep"></param>
        private async Task CommitDeleteAsync(object obj, bool deep)
        {
            Type type = obj.GetType();
            TableData tableData = GetSchemaTableData(type, true);
            List<object> childs = new List<object>();
            DbCommand cmd = CreateDeleteCommand(obj, type, tableData, deep, childs);

            foreach (object childObj in childs) // delete child objects first
            {
                await CommitDeleteAsync(childObj, deep).ConfigureAwait(false);
            }

            if (cmd.CommandText != null)
                await NonQueryAsync(cmd).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks if the table has an identity column.
        /// </summary>
        /// <param name="tableData"></param>
        /// <returns></returns>
        private bool HasIdentity(TableData tableData)
        {
            return (tableData.KeyColumns.Find(x => x.Field<bool?>("IsAutoIncrement").HasValue && x.Field<bool?>("IsAutoIncrement").Value) != null);
        }

        /// <summary>
        /// Creates the exists command.
        /// </summary>
        /// <param name="tableData"></param>
        /// <param name="cmd"></param>
        /// <param name="columns"></param>
        /// <returns></returns>
        private string CreateExistsCommand(TableData tableData, DbCommand cmd, Dictionary<string, IDataParameter> columns)
        {
            string key;
            System.Text.StringBuilder where = new StringBuilder();

            foreach (DataRow keyColumn in tableData.KeyColumns)
            {
                key = keyColumn.Field<string>("ColumnName");

                if (columns.ContainsKey(key))
                {
                    if (where.Length > 0)
                        where.Append(" AND ");

                    where.Append(string.Format("{0}={1}", Escape(key), columns[key].ParameterName));
                    cmd.Parameters.Add(columns[key]);
                }
            }

            if (where.Length > 0)
            {
                cmd.CommandText = string.Format("SELECT COUNT(*) FROM {0} WHERE {1}", tableData.TableName, where.ToString());
                return where.ToString();
            }
            return null;
        }

        /// <summary>
        /// Creates the insert command.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="tableData"></param>
        /// <param name="columns"></param>
        /// <returns></returns>
        private DbCommand CreateInsertCommand(Type type, TableData tableData, Dictionary<string, IDataParameter> columns)
        {
            System.Text.StringBuilder insertColumns = new StringBuilder();
            System.Text.StringBuilder insertValues = new StringBuilder();
            DbCommand cmd = Factory.CreateCommand();
            DataRow keyRow;

            foreach (string key in columns.Keys)
            {
                keyRow = tableData.KeyColumns.Find(x => x.Field<string>("ColumnName").Equals(key, NameComparison));

                if (keyRow != null && keyRow.Field<bool?>("IsAutoIncrement").HasValue && keyRow.Field<bool?>("IsAutoIncrement").Value)
                {
                    continue; // skip auto-increment field
                }

                if (insertColumns.Length > 0)
                {
                    insertColumns.Append(", ");
                    insertValues.Append(", ");
                }

                insertColumns.Append(Escape(key));
                insertValues.Append(columns[key].ParameterName);
                cmd.Parameters.Add(CreateParameter(columns[key].ParameterName, columns[key].Value));
            }

            if (insertColumns.Length == 0 && UseDefaultValuesInsertCommand)
                cmd.CommandText = string.Format("INSERT INTO {0} DEFAULT VALUES;", tableData.TableName);
            else
                cmd.CommandText = string.Format("INSERT INTO {0} ({1}) VALUES ({2});", tableData.TableName, insertColumns.ToString(), insertValues.ToString());

            return cmd;
        }

        /// <summary>
        /// Creates the update command.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="tableData"></param>
        /// <param name="columns"></param>
        /// <param name="where"></param>
        /// <returns></returns>
        private DbCommand CreateUpdateCommand(Type type, TableData tableData, Dictionary<string, IDataParameter> columns, string where)
        {
            System.Text.StringBuilder update = new StringBuilder();
            DbCommand cmd = Factory.CreateCommand();

            foreach (string key in columns.Keys)
            {
                cmd.Parameters.Add(CreateParameter(columns[key].ParameterName, columns[key].Value));

                if (tableData.KeyColumns.FindIndex(x => x.Field<string>("ColumnName").Equals(key, NameComparison)) > -1)
                    continue;

                if (update.Length > 0)
                {
                    update.Append(", ");
                }

                update.Append(string.Format("{0}={1}", Escape(key), columns[key].ParameterName));
            }

            if (!string.IsNullOrWhiteSpace(update.ToString()))
                cmd.CommandText = string.Format("UPDATE {0} SET {1} WHERE {2}", tableData.TableName, update.ToString(), where);

            return cmd;
        }

        /// <summary>
        /// Creates the select command.
        /// </summary>
        /// <param name="tableData"></param>
        /// <param name="columns"></param>
        /// <param name="where"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        private DbCommand CreateSelectCommand(TableData tableData, Dictionary<string, IDataParameter> columns, string where, Int64? id = null)
        {
            DbCommand cmd = Factory.CreateCommand();
            string name;

            if (id.HasValue) // select by scope identity
            {
                cmd.CommandText = string.Format("SELECT * FROM {0} WHERE {1}={2}",
                                            tableData.TableName, Escape(tableData.KeyColumns[0].Field<string>("ColumnName")),
                                            CreateParameter(cmd, "Id", id));
            }
            else
            {
                cmd.CommandText = string.Format("SELECT * FROM {0} WHERE {1}", tableData.TableName, where);

                foreach (DataRow keyColumn in tableData.KeyColumns)
                {
                    name = keyColumn.Field<string>("ColumnName");
                    cmd.Parameters.Add(CreateParameter(columns[name].ParameterName, columns[name].Value));
                }
            }

            return cmd;
        }

        /// <summary>
        /// Creates the delete command.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="type"></param>
        /// <param name="tableData"></param>
        /// <param name="deep"></param>
        /// <param name="childs"></param>
        /// <returns></returns>
        private DbCommand CreateDeleteCommand(object obj, Type type, TableData tableData, bool deep, List<object> childs)
        {
            List<PropertyInfo> properties = TypeCache.GetType(type).Properties;
            string name;
            string columnName;
            DataRow columnData;
            object value;
            DbCommand cmd = Factory.CreateCommand();
            System.Text.StringBuilder where = new StringBuilder();
            PropInfo propInfo;
            List<DataAnnotations.ForeignKeyAttribute> occurredFKs = new List<DataAnnotations.ForeignKeyAttribute>();

            foreach (PropertyInfo property in properties)
            {
                propInfo = GetPropInfo(property);

                name = property.Name;
                columnName = GetColumnName(property);
                value = property.GetValue(obj, null);

                if (value != null && deep && (propInfo.IsClassList || propInfo.IsClass))
                {
                    AddRelation(childs, value, propInfo.IsClassList);
                }
                else if (!propInfo.IsClass && !propInfo.IsClassList)
                {
                    columnData = tableData.KeyColumns.Find(x => x.Field<string>("ColumnName").Equals(columnName, NameComparison));

                    if (propInfo.FKAttributes.Count > 0)
                        occurredFKs.AddRange(propInfo.FKAttributes);

                    if (columnData != null)
                    {
                        if (where.Length > 0)
                            where.Append(" AND ");

                        where.Append(string.Format("{0}={1}", Escape(columnData.Field<string>("ColumnName")), CreateParameter(cmd, name, value)));
                    }
                }
            }

            RemoveOneToOneRelations(occurredFKs, childs);

            if (where.Length > 0)
                cmd.CommandText = string.Format("DELETE FROM {0} WHERE {1}", tableData.TableName, where.ToString());

            return cmd;
        }

        /// <summary>
        /// Executes the query command.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private object Query(Type type, DbCommand cmd)
        {
            try
            {
                cmd.Connection = Connection;
                object obj = null;

                using (cmd)
                {
                    BeginTransaction(cmd);

                    using (DbDataReader reader = cmd.ExecuteReader(CommandBehavior.KeyInfo | CommandBehavior.SingleRow))
                    {
                        InitQuery(reader);
                        while (reader.Read())
                        {
                            obj = CreateClass(reader, type, null, type.Name, new Dictionary<string, string>());
                        }
                    }

                    CommitTransaction();

                    return obj;
                }
            }
            catch
            {
                HandleError();
                throw;
            }
        }

        /// <summary>
        /// Quer
        /// </summary>
        /// <param name="type"></param>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private async Task<object> QueryAsync(Type type, DbCommand cmd)
        {
            try
            {
                cmd.Connection = Connection;
                object obj = null;

                using (cmd)
                {
                    BeginTransaction(cmd);

                    using (DbDataReader reader = await cmd.ExecuteReaderAsync(CommandBehavior.KeyInfo | CommandBehavior.SingleRow).ConfigureAwait(false))
                    {
                        InitQuery(reader);
                        while (reader.Read())
                        {
                            obj = CreateClass(reader, type, null, type.Name, new Dictionary<string, string>());
                        }
                    }

                    CommitTransaction();

                    return obj;
                }
            }
            catch
            {
                HandleError();
                throw;
            }
        }

        /// <summary>
        /// Initializes a new query.
        /// </summary>
        private void InitQuery(DbDataReader reader)
        {
            _schema = reader.GetSchemaTable();
            _querySchemaCache = new Dictionary<string, TableData>();
            _keyData = new Dictionary<string, KeyData>();
            _tableInSchemaCache = new Dictionary<string, bool>();
            _columnAliasPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            StoreSchemaColumns(_schema);

            // build prefix set from ColumnAliasMapping for fast lookup
            foreach (string key in ColumnAliasMapping.Keys)
            {
                // add all parent paths e.g. "AccountRole.Role.Id" adds "AccountRole.Role"
                int lastDot = key.LastIndexOf('.');
                if (lastDot > 0)
                    _columnAliasPrefixes.Add(key.Substring(0, lastDot));
            }
        }

        /// <summary>
        /// Stores the columns from the schema table for quick access.
        /// </summary>
        /// <param name="schema"></param>
        private void StoreSchemaColumns(DataTable schema)
        {
            _schemaColumns = new Dictionary<string, List<ColumnData>>(StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < schema.Rows.Count; index++)
            {
                StoreColumnData(_schemaColumns, schema.Rows[index]);
            }
        }

        private void StoreColumnData(Dictionary<string, List<ColumnData>> columns, DataRow row, bool includeBaseColumn = false)
        {
            List<ColumnData> list;
            var columnData = new ColumnData();
            var columnName = row.Field<string>("ColumnName");
            var baseColumnName = row.Field<string>("BaseColumnName");

            columnData.Ordinal = row.Field<int?>("ColumnOrdinal");
            columnData.AllowDBNull = row.Field<bool?>("AllowDBNull");

            if (columnData.Ordinal.HasValue)
                columnData.Ordinal = (SqlColumnOrdinal == SqlColumnOrdinalOption.ZeroBased) ? columnData.Ordinal.Value : columnData.Ordinal.Value - 1;

            if (columnName != null)
            {
                if (!columns.TryGetValue(columnName, out list))
                    columns[columnName] = list = new List<ColumnData>();

                list.Add(columnData);
            }

            if (includeBaseColumn && baseColumnName != null && baseColumnName != columnName)
            {
                if (!columns.TryGetValue(baseColumnName, out list))
                    columns[baseColumnName] = list = new List<ColumnData>();

                list.Add(columnData);
            }
        }

        /// <summary>
        /// Adds the object to the specified list.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="type"></param>
        /// <param name="parent"></param>
        /// <param name="treeName"></param>
        /// <param name="tableName"></param>
        /// <param name="columnName"></param>
        /// <param name="rlTypes"></param>
        /// <param name="list"></param>
        /// <param name="listSet"></param>
        private void AddToCollection(DbDataReader reader, Type type, Relation parent, string treeName, string tableName, string columnName, Dictionary<string, string> rlTypes, IList list, HashSet<object> listSet)
        {
            if (!(Type.GetTypeCode(type) == TypeCode.Object && type.IsClass))
                list.Add(GetValue(reader, type, (parent != null) ? parent.Type : null, treeName, tableName, columnName, false));
            else if (TableInSchema(type)) // create class instance if the corresponding table is specified in the query
                CreateClass(reader, type, parent, (string.IsNullOrEmpty(treeName)) ? type.Name : treeName, rlTypes, list, listSet);
        }

        /// <summary>
        /// Creates a new class instance of the specified type and fills it from the data reader.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="type"></param>
        /// <param name="parent"></param>
        /// <param name="treeName"></param>
        /// <param name="rlTypes"></param>
        /// <param name="list"></param>
        /// <param name="listSet"></param>
        /// <returns></returns>
        private object CreateClass(DbDataReader reader, Type type, Relation parent, string treeName, Dictionary<string, string> rlTypes, IList list = null, HashSet<object> listSet = null)
        {
            List<PropertyInfo> properties = TypeCache.GetType(type).Properties;
            object obj;
            string name;
            PropertyInfo property;
            PropInfo propInfo;
            System.Type propType;
            KeyData k;
            TableData tableData;
            string keyData;
            string key;
            string typeName = type.FullName;
            string foundKey = null;
            string rlKey = null;
            var relation = new Relation(type, parent);

            // Note: rlTypes is a copy per branch, so sibling branches (e.g. two properties of the same type on the same parent) never see each other's entries and always work correctly.
            if (list == null && parent?.Parent?.Type == type) // check for direct parent relation (A -> B -> A)
            {
                rlKey = ((parent.Parent.Parent != null) ? parent.Parent.Parent.Type.FullName : "#") + "_" + parent.Parent.Type.FullName;
                if (rlTypes.TryGetValue(rlKey, out foundKey))
                    return _keyData[foundKey].Obj;
            }

            // circular reference solution
            rlKey = ((parent != null) ? parent.Type.FullName : "#") + "_" + type.FullName;
            if (rlTypes.TryGetValue(rlKey, out foundKey))
                return _keyData[foundKey].Obj; // relation already occurred

            tableData = GetSchemaTableData(type, false, treeName);
            keyData = GetSchemaKeyData(reader, type, treeName);

            if (string.IsNullOrEmpty(keyData))
                return null;

            key = string.Format("{0}.{1}", typeName, keyData);
            rlTypes.Add(rlKey, key);

            if (!_keyData.ContainsKey(key))
            {
                k = new KeyData();
                k.Obj = obj = Activator.CreateInstance(type); // the active object is used in save changes
                _keyData[key] = k;

                for (int index = 0; index < properties.Count; index++)
                {
                    property = properties[index];
                    name = string.Format("{0}.{1}", treeName, property.Name);
                    propInfo = GetPropInfo(property);

                    // skip indexed properties and properties with notmapped attribute which have no key in the mapping table
                    if (propInfo.HasIndexParameters || (!propInfo.IsMapped && !ColumnAliasMapping.ContainsKey(name)))
                        continue;                       

                    if (propInfo.IsList && propInfo.ItemType != null)
                    {
                        // create collection if the item type is a class and the table is specified in the query or when the item type is primitive
                        if (!propInfo.ItemIsClass || (LoadOneToMany && !HasType(IgnoreTypes, propInfo.ItemType) && TableInSchema(propInfo.ItemType)))
                        {
                            // create new list
                            k.Collections[property.Name] = (IList)Activator.CreateInstance((typeof(List<>).MakeGenericType(propInfo.ItemType)));
                            k.CollectionsInfo[property.Name] = propInfo;
                            k.CollectionSets[property.Name] = new HashSet<object>();

                            property.SetValue(obj, k.Collections[property.Name], null);
                        }
                    }
                    else if (!propInfo.IsClass)
                        property.SetValue(obj, GetValue(reader, propInfo.PropType, type, name, tableData.TableName, propInfo.ColumnName), null);
                    else if (LoadOneToOne && !HasType(IgnoreTypes, propInfo.PropType) && TableInSchema(propInfo.PropType)) // create class instance if the corresponding table is specified in the query
                    {
                        k.Classes[property.Name] = (propInfo.PropType, property);
                        property.SetValue(obj, CreateClass(reader, propInfo.PropType, relation, name, new Dictionary<string, string>(rlTypes)), null); // copy of rlTypes so cr check happens per branch path
                    }

                }

                // add the object to the specified list
                if (list != null && (listSet == null || listSet.Add(k.Obj)))
                    list.Add(_keyData[key].Obj);
            }
            else
            {
                k = _keyData[key];

                // loop through sub-classes
                foreach (var classEntry in k.Classes)
                {
                    name = string.Format("{0}.{1}", treeName, classEntry.Key);

                    if (ColumnAliasMapping.Count == 0 || _columnAliasPrefixes.Contains(name))
                    {
                        var classObj = CreateClass(reader, classEntry.Value.Type, relation, name, new Dictionary<string, string>(rlTypes));
                        if (classObj != null)
                            classEntry.Value.Property.SetValue(k.Obj, classObj);
                    }
                }

                // add to collection if reached via a collection and not already added
                if (list != null && (listSet == null || listSet.Add(k.Obj)))
                    list.Add(k.Obj);
            }

            // loop through collections in class
            foreach (string colKey in k.Collections.Keys)
            {
                name = string.Format("{0}.{1}", treeName, colKey);
                propType = k.Collections[colKey].GetType().GetGenericArguments()[0];
                propInfo = k.CollectionsInfo[colKey];

                AddToCollection(reader, propType, relation, name, tableData.TableName, propInfo.ColumnName, new Dictionary<string, string>(rlTypes), k.Collections[colKey], k.CollectionSets[colKey]);
            }

            return k.Obj; // return parent object(prevent circular references)
        }

        /// <summary>
        /// Gets the value from the reader.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="type"></param>
        /// <param name="parentType"></param>
        /// <param name="treeName"></param>
        /// <param name="tableName"></param>
        /// <param name="columnName"></param>
        /// <param name="isClass"></param>
        /// <returns></returns>
        private object GetValue(DbDataReader reader, Type type, Type parentType, string treeName, string tableName, string columnName, bool isClass = true)
        {
            // when there is a treeName we are dealing with a value on a (sub)class
            // otherwise the data reader should hold only a single column value

            if (string.IsNullOrEmpty(treeName))
                return reader[0];
            else
            {
                int? ordinal = GetOrdinal(treeName, tableName, columnName, isClass);

                if (!ordinal.HasValue)
                    return null;

                object readerValue = reader.GetValue(ordinal.Value);
                object value = (readerValue != DBNull.Value) ? reader.GetValue(ordinal.Value) : null;

                // cast numeric or string to enum
                if (value != null && type.BaseType == typeof(System.Enum))
                {
                    if (IsNumeric(reader.GetFieldType(ordinal.Value)))
                        value = Enum.ToObject(type, value);
                    else if (reader.GetFieldType(ordinal.Value) == typeof(string))
                        value = Enum.Parse(type, (string)value);
                }

                return value;
            }
        }

        /// <summary>
        /// Checks if the table is part of the current schema.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private bool TableInSchema(Type type)
        {
            bool inSchema = false;
            int index = -1;
            var tableName = GetTableName(type);

            if (_tableInSchemaCache.TryGetValue(type.FullName, out inSchema))
                return inSchema;
            else
            {
                if (!ColumnLookupTableNameValidation)
                    inSchema = true;
                else
                {
                    while (++index < _schema.Rows.Count && !inSchema)
                    {
                        inSchema = DefaultSchemaFilter(_schema.Rows[index], type, tableName);
                    }
                }

                return _tableInSchemaCache[type.FullName] = inSchema;
            }
        }

        /// <summary>
        /// Gets unique key data for the specified type.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="type"></param>
        /// <param name="treeName"></param>
        /// <returns></returns>
        private string GetSchemaKeyData(DbDataReader reader, Type type, string treeName)
        {
            int ordinal;
            object value;
            List<string> keyData = new List<string>();
            List<DataRow> rows = GetSchemaTableData(type, false, treeName).KeyColumns;

            foreach (DataRow row in rows)
            {
                ordinal = row.Field<int>("ColumnOrdinal");
                ordinal = (SqlColumnOrdinal == SqlColumnOrdinalOption.ZeroBased) ? ordinal : ordinal - 1;
                value = reader.GetValue(ordinal);

                if (value != null && value != DBNull.Value)
                    keyData.Add(value.ToString());
            }

            return String.Join("_", keyData);
        }

        private PropertyInfo GetPropertyByColumnName(Type type, List<string> columnNames)
        {
            List<PropertyInfo> properties = TypeCache.GetType(type).Properties;

            foreach (var prop in properties)
            {
                var column = GetColumnName(prop);

                if (columnNames.Contains(column))
                    return prop;
            }

            return null;
        }

        /// <summary>
        /// Gets schema table data for the specified type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="typeSchema"></param>
        /// <param name="treeName"></param>
        /// <returns></returns>
        private TableData GetSchemaTableData(Type type, bool typeSchema, string treeName = null)
        {
            string uniqueName = type.FullName;
            string tableName;
            TableData tableData = null;
            List<DataRow> rows;
            List<DataRow> keyRows;
            DataRow row;
            List<string> keyFields;
            List<string> keyProperties;
            DataTable schema;
            int index;
            string key, columnName;
            int? tableIndex = null;
            Dictionary<string, int> occurrences = null;

            if ((!typeSchema && !_querySchemaCache.TryGetValue(treeName, out tableData)) || (typeSchema && !_typeSchemaCache.TryGetValue(uniqueName, out tableData)))
            {
                tableName = IncludeCatalog(type, GetTableName(type));
                schema = (typeSchema) ? GetSchemaTable(tableName) : _schema; // get schema from database when typeSchema is true otherwise get schema from current data reader
                tableData = new TableData();
                rows = new List<DataRow>();
                keyRows = new List<DataRow>();

                // get key columns (datarows) by defined key attribute(s) on class properties
                keyFields = GetKeyFields(type);

                if (!typeSchema && ColumnLookupTableNameValidation && TableIndexMapping.Count > 0 && TableIndexMapping.ContainsKey(treeName))
                {
                    tableIndex = TableIndexMapping[treeName];
                    occurrences = new Dictionary<string, int>();
                }

                for (index = 0; index < schema.Rows.Count; index++)
                {
                    row = schema.Rows[index];

                    if (DefaultSchemaFilter(row, type, GetTableName(type)))
                    {
                        var baseColumnName = row.Field<string>("BaseColumnName");
                        var name = (string.IsNullOrEmpty(baseColumnName)) ? row.Field<string>("ColumnName") : baseColumnName;
                        var indexMatch = true;
                        var isAlias = false;

                        if (!typeSchema)
                        {
                            var alias = ColumnAliasMapping.Where(x => x.Value == name).Select(x => (KeyValuePair<string, string>?)x).FirstOrDefault();
                            isAlias = (alias.HasValue && alias.Value.Key.StartsWith(treeName + "."));

                            if (!isAlias && tableIndex.HasValue)
                            {
                                occurrences[name] = (occurrences.ContainsKey(name)) ? occurrences[name] + 1 : 0;
                                indexMatch = (occurrences[name] == tableIndex);
                            }
                        }

                        if (indexMatch)
                        {
                            rows.Add(row);
                            StoreColumnData(tableData.Columns, row, ColumnLookupTableNameValidation);
                        }
                    }
                }

                if (keyFields.Count > 0)
                {
                    if (!typeSchema && this.ColumnAliasMapping.Count > 0)
                    {
                        keyProperties = GetKeyFields(type, false);

                        for (index = 0; index < keyFields.Count; index++)
                        {
                            key = string.Format("{0}.{1}", treeName, keyProperties[index]);

                            if (this.ColumnAliasMapping.Count == 0 || !this.ColumnAliasMapping.TryGetValue(key, out columnName))
                                columnName = keyFields[index];

                            AddKeyDataRow(rows, columnName, keyRows);
                        }
                    }
                    else
                    {
                        for (index = 0; index < keyFields.Count; index++)
                        {
                            AddKeyDataRow(rows, keyFields[index], keyRows);
                        }
                    }
                }

                if (keyRows.Count == 0)
                {
                    // get key columns (datarows) by key columns
                    for (index = 0; index < rows.Count; index++)
                    {
                        row = rows[index];

                        if ((bool)(row.Field<bool?>("IsKey") ?? false))
                            keyRows.Add(row);
                    }

                    if (keyRows.Count == 0)
                        throw new System.Exception(string.Format("Key for Type '{0}' cannot be defined by either class or table schema.", type.FullName));
                }

                tableData.TableName = tableName;
                tableData.KeyColumns = keyRows;

                if (typeSchema)
                    _typeSchemaCache[uniqueName] = tableData;
                else
                    _querySchemaCache[treeName] = tableData;
            }

            return tableData;
        }

        /// <summary>
        /// Adds the key data row with the specified column name to the list of key rows.
        /// </summary>
        /// <param name="rows"></param>
        /// <param name="columnName"></param>
        /// <param name="keyRows"></param>
        private void AddKeyDataRow(List<DataRow> rows, string columnName, List<DataRow> keyRows)
        {
            DataRow keyRow = rows.Where(r => columnName.Equals(r.Field<string>("BaseColumnName"), NameComparison) || columnName.Equals(r.Field<string>("ColumnName"), NameComparison)).FirstOrDefault();

            if (keyRow != null)
                keyRows.Add(keyRow);
        }

        /// <summary>
        /// Filters the schema rows on server, catalog and table name.
        /// </summary>
        /// <param name="row"></param>
        /// <param name="type"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private bool DefaultSchemaFilter(DataRow row, Type type, string tableName)
        {
            if (!ColumnLookupTableNameValidation)
                return true;

            string baseServerName = null;
            string baseCatalogName = null;
            string baseTableName = row.Field<string>("BaseTableName");
            _catalogMapping.TryGetValue(type.FullName, out var catalog);

            if (catalog == null)
                return !string.IsNullOrEmpty(baseTableName) && baseTableName.Equals(tableName, NameComparison);

            if (row.Table.Columns.Contains("BaseServerName"))
                baseServerName = row.Field<string>("BaseServerName");

            if (row.Table.Columns.Contains("BaseCatalogName"))
                baseCatalogName = row.Field<string>("BaseCatalogName");

            // Table must match
            if (string.IsNullOrEmpty(baseTableName) || !baseTableName.Equals(tableName, NameComparison))
                return false;

            // Check catalog match if provided
            if (!string.IsNullOrEmpty(catalog.CatalogName) && !catalog.CatalogName.Equals(baseCatalogName, NameComparison))
                return false;

            // Check server match if provided
            if (!string.IsNullOrEmpty(catalog.ServerName) && !catalog.ServerName.Equals(baseServerName, NameComparison))
                return false;

            return true;
        }

        /// <summary>
        /// Gets the key fields for the specified type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="useColumnAttribute"></param>
        /// <returns></returns>
        private List<string> GetKeyFields(Type type, bool useColumnAttribute = true)
        {
            ColumnAttribute column;
            List<string> keys = new List<string>();
            List<PropertyInfo> properties = TypeCache.GetType(type).Properties
                                .Where(p => p.GetCustomAttributes(typeof(KeyAttribute), true).Length != 0)
                                .ToList();


            foreach (PropertyInfo property in properties)
            {
                if (useColumnAttribute)
                    column = (ColumnAttribute)property.GetCustomAttributes(typeof(ColumnAttribute), true).FirstOrDefault();
                else
                    column = null;

                if (column != null)
                    keys.Add(column.Name);
                else
                    keys.Add(property.Name);
            }

            return keys;
        }

        /// <summary>
        /// Retrieves the column ordinal.
        /// </summary>
        /// <param name="treeName"></param>
        /// <param name="tableName"></param>
        /// <param name="columnName"></param>
        /// <param name="isClass"></param>
        /// <returns></returns>
        private int? GetOrdinal(string treeName, string tableName, string columnName, bool isClass)
        {
            string parentTreeName = treeName.Substring(0, treeName.LastIndexOf("."));
            int? ordinal = null;
            int schemaIndex = 0;
            string columnAlias = null;
            List<ColumnData> columnDataList;

            ColumnAliasMapping.TryGetValue(treeName, out columnAlias);

            if (!isClass)
            {
                if ((!string.IsNullOrEmpty(columnAlias) && _schemaColumns.TryGetValue(columnAlias, out columnDataList)))
                    ordinal = columnDataList[0].Ordinal;
                else if (string.IsNullOrEmpty(columnAlias))
                {
                    schemaIndex = (TableIndexMapping.ContainsKey(parentTreeName)) ? TableIndexMapping[parentTreeName] : 0;

                    if (_schemaColumns.TryGetValue(columnName, out columnDataList) && columnDataList.Count > schemaIndex)
                        ordinal = columnDataList[schemaIndex].Ordinal; // get single value column
                }
            }
            if (!string.IsNullOrEmpty(columnAlias) && _querySchemaCache[parentTreeName].Columns.TryGetValue(columnAlias, out columnDataList) && (columnDataList.Count > schemaIndex))
            {
                ordinal = columnDataList[0].Ordinal; // get by alias name
            }
            else if (_querySchemaCache[parentTreeName].Columns.TryGetValue(columnName, out columnDataList) && columnDataList.Count > schemaIndex)
            {
                ordinal = columnDataList[0].Ordinal; // get by column name
            }

            if (ordinal.HasValue || this.IgnoreMissingColumns)
                return ordinal;
            else if (isClass)
                throw new System.Exception(string.Format("Column '{0}'{1} in table '{2}' was not found.", columnName, (string.IsNullOrEmpty(columnAlias)) ? "" : string.Format(" alias '{0}'", columnAlias), tableName));
            else
                throw new System.Exception(string.Format("Column '{0}'{1} was not found.", columnName, (string.IsNullOrEmpty(columnAlias)) ? "" : string.Format(" alias '{0}'", columnAlias)));
        }

        /// <summary>
        /// Prefixes the catalog name to the table name if there is a catalog mapped for the specified type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private string IncludeCatalog(Type type, string tableName)
        {
            if (_catalogMapping.TryGetValue(type.FullName, out var catalog))
            {
                string prefix = "";

                if (!string.IsNullOrEmpty(catalog.CatalogName))
                    prefix = Escape(catalog.CatalogName);

                if (!string.IsNullOrEmpty(catalog.SchemaName))
                    prefix = string.IsNullOrEmpty(prefix) ? Escape(catalog.SchemaName) : $"{prefix}.{Escape(catalog.SchemaName)}";

                if (!string.IsNullOrEmpty(catalog.ServerName))
                    prefix = $"{Escape(catalog.ServerName)}.{prefix}";

                return string.IsNullOrEmpty(prefix) ? Escape(tableName) : $"{prefix}.{Escape(tableName)}";
            }

            return Escape(tableName);
        }

        /// <summary>
        /// Gets the table name for the specified type.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private string GetTableName(Type type)
        {
            string tableName;

            if (_tableNameCache.TryGetValue(type.FullName, out tableName))
                return tableName;

            TableAttribute attr = (TableAttribute)type.GetCustomAttributes(typeof(TableAttribute), true).FirstOrDefault();

            if (attr != null)
                return _tableNameCache[type.FullName] = attr.Name;
            else
                return _tableNameCache[type.FullName] = type.Name;
        }

        /// <summary>
        /// Gets (and caches) property information.
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        private PropInfo GetPropInfo(PropertyInfo property)
        {
            string uniqueName = property.ReflectedType.FullName + "." + property.Name;
            PropInfo propInfo; bool isSystemType = false; bool itemIsSystemType = false;

            if (_propInfoCache.TryGetValue(uniqueName, out propInfo))
                return propInfo;

            _propInfoCache[uniqueName] = propInfo = new PropInfo();
            propInfo.PropType = (Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType);
            propInfo.ItemType = (propInfo.PropType.IsGenericType) ? propInfo.PropType.GetGenericArguments()[0] : null;

            if (propInfo.PropType.Module != null)
                isSystemType = (propInfo.PropType.Module.ScopeName == _CLRL);

            if (propInfo.ItemType != null && propInfo.ItemType.Module != null)
                itemIsSystemType = (propInfo.ItemType.Module.ScopeName == _CLRL);

            propInfo.IsClass = (!isSystemType && Type.GetTypeCode(propInfo.PropType) == TypeCode.Object && propInfo.PropType.IsClass);
            propInfo.ItemIsClass = (!itemIsSystemType && Type.GetTypeCode(propInfo.ItemType) == TypeCode.Object && propInfo.ItemType.IsClass);
            propInfo.IsList = (typeof(IEnumerable).IsAssignableFrom(propInfo.PropType));
            propInfo.IsClassList = (propInfo.IsList && propInfo.ItemIsClass);
            propInfo.IsMapped = IsMapped(property);
            propInfo.IsReadOnly = IsReadOnly(property);
            propInfo.ColumnName = GetColumnName(property);
            propInfo.FKAttributes = GetFKAttributes(property);
            propInfo.HasIndexParameters = (property.GetIndexParameters().Count() > 0);

            if (propInfo.IsMapped && (propInfo.IsClass || propInfo.IsClassList))
                propInfo.IsMapped = (propInfo.IsClass) ? IsMapped(propInfo.PropType) : IsMapped(propInfo.ItemType);

            return propInfo;
        }

        /// <summary>
        /// Gets the column name for the specified property.
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        private string GetColumnName(PropertyInfo property)
        {
            ColumnAttribute attr = (ColumnAttribute)property.GetCustomAttributes(typeof(ColumnAttribute), true).FirstOrDefault();

            if (attr != null)
                return attr.Name;
            else
                return property.Name;
        }

        /// <summary>
        /// Gets a list of ForeignKey attributes for the specified property.
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        private List<DataAnnotations.ForeignKeyAttribute> GetFKAttributes(PropertyInfo property)
        {
            List<DataAnnotations.ForeignKeyAttribute> attributes = property.GetCustomAttributes(typeof(DataAnnotations.ForeignKeyAttribute), true).Cast<DataAnnotations.ForeignKeyAttribute>().ToList();

            foreach (DataAnnotations.ForeignKeyAttribute attr in attributes)
            {
                if (attr != null && string.IsNullOrEmpty(attr.PKTable))
                {
                    attr.PKTable = GetTableName(attr.ParentType);

                    if (string.IsNullOrEmpty(attr.PKColumn))
                        attr.PKColumn = GetColumnName(TypeCache.GetType(attr.ParentType).GetProperty(attr.PKProperty));
                }
            }

            return attributes;
        }

        /// <summary>
        /// Checks if the property is mapped.
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        private bool IsMapped(PropertyInfo property)
        {
            return ((NotMappedAttribute)property.GetCustomAttributes(typeof(NotMappedAttribute), true).FirstOrDefault() == null);
        }

        /// <summary>
        /// Checks if the property is DB read-only.
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        private bool IsReadOnly(PropertyInfo property)
        {
            return ((DataAnnotations.ReadOnlyAttribute)property.GetCustomAttributes(typeof(DataAnnotations.ReadOnlyAttribute), true).FirstOrDefault() != null);
        }

        /// <summary>
        /// Checks if the type is mapped.
        /// </summary>
        /// <param name="type"></param>
        /// <returns>A value indicating if the type is mapped.</returns>
        private bool IsMapped(System.Type type)
        {
            return ((NotMappedAttribute)type.GetCustomAttributes(typeof(NotMappedAttribute), true).FirstOrDefault() == null);
        }

        /// <summary>
        /// Checks if property type is numeric
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsNumeric(System.Type type)
        {
            if (type == typeof(Int16)
                || type == typeof(Int32)
                || type == typeof(Int64)
                || type == typeof(UInt16)
                || type == typeof(UInt32)
                || type == typeof(UInt64)
                || type == typeof(float)
                || type == typeof(double)
                || type == typeof(decimal))
                return true;
            else
                return false;
        }

        #endregion
    }
}
