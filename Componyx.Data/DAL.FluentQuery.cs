using Componyx.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Componyx.Data
{
    public partial class DAL
    {

        /// <summary>
        /// This class facilitates constructing queries using Fluent Syntax.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public class FluentQuery<T>
        {
            DAL _dal;
            StringBuilder _select = null;
            StringBuilder _where = null;
            StringBuilder _groupBy = null;
            StringBuilder _having = null;
            StringBuilder _orderBy = null;
            List<string> _columns = null;
            List<string> _distinctColumns = null;
            List<string> _tables = null;
            Dictionary<string, string> _tableAliasMapping = null;
            DbCommand _cmd = null;
            int _skip = 0, _take = 0;
            bool _distinct;
            string _lastPropertyName, _lastColumnName, _lastColumnNameAlias, _tableAlias;
            Dictionary<string, List<int>> _joinOn = null;

            /// <summary>
            /// Gets the underlaying command.
            /// </summary>
            public DbCommand Command
            {
                get
                {
                    return _cmd;
                }
            }


            /// <summary>
            /// Initializes a new instance of the FluentQuery<typeparamref name="T"/> class.
            /// </summary>
            /// <param name="dal"></param>
            /// <param name="loadOneToMany">A value indicating if one-to-many relations of the target type should be loaded.</param>
            /// <param name="loadOneToOne">A value indicating if one-to-one relations of the target type should be loaded.</param>
            public FluentQuery(DAL dal, bool loadOneToMany = true, bool loadOneToOne = true)
            {
                _dal = dal;
                _select = new StringBuilder();
                _where = new StringBuilder();
                _groupBy = new StringBuilder();
                _having = new StringBuilder();
                _orderBy = new StringBuilder();
                _columns = new List<string>();
                _distinctColumns = new List<string>();
                _tables = new List<string>();
                _tableAliasMapping = new Dictionary<string, string>();
                _cmd = _dal.CreateCommand();
                _skip = _take = 0;
                _joinOn = new Dictionary<string, List<int>>();

                _dal.LoadOneToMany = loadOneToMany;
                _dal.LoadOneToOne = loadOneToOne;
                _dal.CreateJoins(typeof(T), null, loadOneToMany, loadOneToOne, _columns, _tables, null, _tableAliasMapping);
            }

            /// <summary>
            /// Appends a filter to the SQL JOIN ON statement for the corresponding table join.
            /// </summary>
            /// <param name="propertyExpressions">A list of Lambda expressions: '() => Class.Property'.</param>
            /// <param name="whereClause">A SQL WHERE clause.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> JoinOn(List<Expression<Func<object>>> propertyExpressions, string whereClause = null)
            {
                return JoinOn(_dal.GetPropertyNames(propertyExpressions), whereClause);
            }

            /// <summary>
            /// Appends a filter to the SQL JOIN ON statement for the corresponding table join.
            /// </summary>
            /// <param name="propertyExpression">A lambda expressions: '() => Class.Property'.</param>
            /// <param name="whereClause">A SQL WHERE clause.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> JoinOn<PT>(Expression<Func<PT>> propertyExpression, string whereClause = null)
            {
                return JoinOn(_dal.GetPropertyName(propertyExpression), whereClause);
            }

            /// <summary>
            /// Appends a filter to the SQL JOIN ON statement for the corresponding table join.
            /// </summary>
            /// <param name="propertyName">The dot separated tree name for a property of a parent/child classes.</param>
            /// <param name="whereClause">A SQL WHERE clause.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> JoinOn(string propertyName, string whereClause = null)
            {
                string columnName = GetColumnName(propertyName);

                CloseGroups(); // close previous groups
                UpdateJoinOn(true); // end possible join

                _tableAlias = columnName.Substring(0, columnName.IndexOf("."));
                UpdateJoinOn(); // start new join
                OpenGroups(1); // always group a new joinon clause so all and/or statements belong to this group until joinon/where is called again

                _where.Append(string.Format("{0}", columnName));

                if (!string.IsNullOrEmpty(whereClause))
                    _where.Append(whereClause);

                return this;
            }

            /// <summary>
            /// Creates a SQL WHERE statement.
            /// </summary>
            /// <param name="propertyExpressions">A list of Lambda expressions: '() => Class.Property'.</param>
            /// <param name="addOperator">A value indicating if the AND operator is added to the SQL Where clause if the text does not start with an AND/OR operator.</param>
            /// <param name="handleGroups">A value indicating if groups (parentheses) are closed and/or opened.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> Where(List<Expression<Func<object>>> propertyExpressions, bool addOperator = true, bool handleGroups = true)
            {
                return Where(_dal.GetPropertyNames(propertyExpressions), addOperator, handleGroups);
            }

            /// <summary>
            /// Creates a SQL WHERE statement.
            /// </summary>
            /// <param name="propertyExpression">A lambda expression: '() => Class.Property'.</param>
            /// <param name="addOperator">A value indicating if the AND operator is added to the SQL Where clause if the text does not start with an AND/OR operator.</param>
            /// <param name="handleGroups">A value indicating if groups (parentheses) are closed and/or opened.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> Where<PT>(Expression<Func<PT>> propertyExpression, bool addOperator = true, bool handleGroups = true)
            {
                return Where(_dal.GetPropertyName(propertyExpression), addOperator, handleGroups);
            }

            /// <summary>
            /// Creates a SQL WHERE statement.
            /// </summary>
            /// <param name="propertyName">The property name of the root class or dot separated tree name for a property of a parent/child classes.</param>
            /// <param name="addOperator">A value indicating if the AND operator is added to the SQL Where clause if the text does not start with an AND/OR operator.</param>
            /// <param name="handleGroups">A value indicating if groups (parentheses) are closed and/or opened.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> Where(string propertyName = null, bool addOperator = true, bool handleGroups = true)
            {
                string columnName = (!string.IsNullOrEmpty(propertyName)) ? GetColumnName(propertyName) : null;

                if (handleGroups)
                    CloseGroups(); // close previous groups

                UpdateJoinOn(true); // end possible join

                if (addOperator && _where.Length > 0)
                    _where.Append(" AND ");

                if (handleGroups)
                    OpenGroups(1); // always group a new where clause so all and/or statements belong to this group until joinon/where is called again

                if (!string.IsNullOrEmpty(propertyName))
                    _where.Append(string.Format("{0}", columnName));

                return this;
            }

            /// <summary>
            /// Creates a SQL AND condition in the current query.
            /// </summary>
            /// <param name="propertyExpressions">A list of lambda expressions: '() => Class.Property'.</param>
            /// <param name="openGroups">A value indicating the amount of groups (parentheses) to open.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> And(List<Expression<Func<object>>> propertyExpressions, int openGroups = 0)
            {
                return And(_dal.GetPropertyNames(propertyExpressions), openGroups);
            }

            /// <summary>
            /// Creates a SQL AND condition in the current query.
            /// </summary>
            /// <param name="propertyExpression">A lambda expression of the form: '() => Class.Property'.</param>
            /// <param name="openGroups">A value indicating the amount of groups (parentheses) to open.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> And<PT>(Expression<Func<PT>> propertyExpression, int openGroups = 0)
            {
                return And(_dal.GetPropertyName(propertyExpression), openGroups);
            }

            /// <summary>
            /// Creates a SQL AND condition in the current query.
            /// </summary>
            /// <param name="propertyName">The property name of the root class, dot separated tree name for parent/child classes or empty to use previous property.</param>
            /// <param name="openGroups">A value indicating the amount of groups (parentheses) to open.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> And(string propertyName = null, int openGroups = 0)
            {
                if (!_where.ToString().EndsWith("("))
                    _where.Append(" AND ");

                OpenGroups(openGroups);
                _where.Append(string.Format("{0}", GetColumnName(propertyName)));
                return this;
            }

            /// <summary>
            /// Creates a SQL OR condition in the current query.
            /// </summary>
            /// <param name="propertyExpressions">A list of lambda expressions: '() => Class.Property'.</param>
            /// <param name="openGroups">A value indicating the amount of groups (parentheses) to open.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> Or(List<Expression<Func<object>>> propertyExpressions, int openGroups = 0)
            {
                return Or(_dal.GetPropertyNames(propertyExpressions), openGroups);
            }

            /// <summary>
            /// Creates a SQL OR condition in the current query.
            /// </summary>
            /// <param name="propertyExpression">A lambda expression of the form: '() => Class.Property'.</param>
            /// <param name="openGroups">A value indicating the amount of groups (parentheses) to open.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> Or<PT>(Expression<Func<PT>> propertyExpression, int openGroups = 0)
            {
                return Or(_dal.GetPropertyName(propertyExpression), openGroups);
            }

            /// <summary>
            /// Creates a SQL OR condition in the current query.
            /// </summary>
            /// <param name="propertyName">The property name of the root class, dot separated tree name for parent/child classes or empty to use previous property.</param>
            /// <param name="openGroups">A value indicating the amount of groups (parentheses) to open.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> Or(string propertyName = null, int openGroups = 0)
            {
                if (!_where.ToString().EndsWith("("))
                    _where.Append(" OR ");

                OpenGroups(openGroups);
                _where.Append(string.Format("{0}", GetColumnName(propertyName)));
                return this;
            }

            /// <summary>
            /// Creates a SQL GROUP BY statement in the current query.
            /// </summary>
            /// <param name="propertyExpressions">A list of lambda expressions: '() => Class.Property'.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> GroupBy(List<Expression<Func<object>>> propertyExpressions)
            {
                return GroupBy(_dal.GetPropertyNames(propertyExpressions));
            }

            /// <summary>
            /// Creates a SQL GROUP BY statement in the current query.
            /// </summary>
            /// <param name="propertyExpression">A lambda expression of the form: '() => Class.Property'.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> GroupBy<PT>(Expression<Func<PT>> propertyExpression)
            {
                return GroupBy(_dal.GetPropertyName(propertyExpression));
            }

            /// <summary>
            /// Creates a SQL GROUP BY statement in the current query.
            /// </summary>
            /// <param name="propertyName">The property name, dot separated tree name for parent/child classes or a single dot for previous property name.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> GroupBy(string propertyName)
            {
                if (_groupBy.Length == 0)
                    _groupBy.Append(" GROUP BY ");
                else
                    _groupBy.Append(",");

                _groupBy.Append(string.Format("{0}", GetColumnName(propertyName)));
                return this;
            }

            /// <summary>
            /// Creates a SQL ORDER BY statement in the current query.
            /// </summary>
            /// <param name="propertyExpressions">A list of lambda expressions: '() => Class.Property'.</param>
            /// <param name="descending">A value indicating if the order should be descending.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> OrderBy(List<Expression<Func<object>>> propertyExpressions, bool descending = false)
            {
                return OrderBy(_dal.GetPropertyNames(propertyExpressions), descending);
            }

            /// <summary>
            /// Creates a SQL ORDER BY statement in the current query.
            /// </summary>
            /// <param name="propertyExpression">A lambda expression: '() => Class.Property'.</param>
            /// <param name="descending">A value indicating if the order should be descending.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> OrderBy<PT>(Expression<Func<PT>> propertyExpression, bool descending = false)
            {
                return OrderBy(_dal.GetPropertyName(propertyExpression), descending);
            }

            /// <summary>
            /// Creates a SQL ORDER BY statement in the current query.
            /// </summary>
            /// <param name="propertyName">The property name, dot separated tree name for parent/child classes or a single dot for previous property name.</param>
            /// <param name="descending">A value indicating if the order should be descending.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> OrderBy(string propertyName, bool descending = false)
            {
                if (_orderBy.Length == 0)
                    _orderBy.Append(" ORDER BY ");
                else
                    _orderBy.Append(",");

                _orderBy.Append(string.Format("{0}{1}", GetColumnName(propertyName, true), (descending) ? " DESC" : ""));
                return this;
            }

            /// <summary>
            /// Appends the current SELECT clause within the query with the specified SQL text.
            /// </summary>
            /// <param name="text">The SQL text. Use default string format placeholders for property names.</param>
            /// <param name="propertyNames">A list of property names to replace through string format within the specified SQL text.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> SqlSelect(string text, List<string> propertyNames = null)
            {
                int index = 0;

                if (!text.StartsWith(","))
                    text += ",";

                if (propertyNames != null)
                {
                    object[] columns = new object[propertyNames.Count];
                    foreach (string name in propertyNames)
                    {
                        columns[index++] = GetColumnName(name);
                    }

                    _select.Append(string.Format(text, columns));
                }
                else
                    _select.Append(text);

                return this;
            }

            /// <summary>
            /// Appends the current WHERE or JOIN clause within the query with the specified SQL text.
            /// </summary>
            /// <param name="text">The SQL text. Use default string format placeholders for property names.</param>
            /// <param name="propertyNames">A list of property names to replace through string format within the specified SQL text.</param>
            /// <param name="addOperator">A value indicating if the AND operator is added to the SQL Where clause if the text does not start with an AND/OR operator.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> SqlWhere(string text, List<string> propertyNames = null, bool addOperator = true)
            {
                int index = 0;

                if (addOperator && _where.Length > 0 && (!text.StartsWith(" AND", StringComparison.OrdinalIgnoreCase) && !text.StartsWith(" OR", StringComparison.OrdinalIgnoreCase)))
                    _where.Append(" AND ");

                if (propertyNames != null)
                {
                    object[] columns = new object[propertyNames.Count];
                    foreach (string name in propertyNames)
                    {
                        columns[index++] = GetColumnName(name);
                    }

                    _where.Append(string.Format(text, columns));
                }
                else
                    _where.Append(text);

                return this;
            }

            /// <summary>
            /// Creates the HAVING clause within the query with the specified SQL text.
            /// </summary>
            /// <param name="text">The SQL text. Use default string format placeholders for property names.</param>
            /// <param name="propertyNames">A list of property names to replace through string format within the specified SQL text.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> SqlHaving(string text, List<string> propertyNames = null)
            {
                int index = 0;

                if (propertyNames != null)
                {
                    object[] columns = new object[propertyNames.Count];
                    foreach (string name in propertyNames)
                    {
                        columns[index++] = GetColumnName(name);
                    }

                    _having.Append(string.Format(text, columns));
                }
                else
                    _having.Append(text);

                return this;
            }


            /// <summary>
            /// Appends the current ORDER BY clause within the query with the specified SQL text.
            /// </summary>
            /// <param name="text">The SQL text. Use default string format placeholders for property names.</param>
            /// <param name="propertyNames">A list of property names to replace through string format within the specified SQL text.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> SqlOrderBy(string text, List<string> propertyNames = null)
            {
                int index = 0;

                if (_orderBy.Length == 0)
                    _orderBy.Append(" ORDER BY ");
                else
                    _orderBy.Append(",");

                if (propertyNames != null)
                {
                    object[] columns = new object[propertyNames.Count];
                    foreach (string name in propertyNames)
                    {
                        columns[index++] = GetColumnName(name);
                    }

                    _orderBy.Append(string.Format(text, columns));
                }
                else
                    _orderBy.Append(text);

                return this;
            }

            /// <summary>
            /// Adds the SQL DISTINCT keyword to return only unique (different) records.
            /// </summary>
            /// <param name="propertyExpressions">A list of lambda expressions: '() => Class.Property'.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> Distinct(List<Expression<Func<object>>> propertyExpressions)
            {
                return Distinct(_dal.GetPropertyNames(propertyExpressions));
            }

            /// <summary>
            /// Adds the SQL DISTINCT keyword to return only unique (different) records.
            /// </summary>
            /// <param name="propertyExpression">A lambda expression: '() => Class.Property'.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> Distinct(Expression<Func<object>> propertyExpression)
            {
                return Distinct(_dal.GetPropertyName(propertyExpression));
            }

            /// <summary>
            /// Adds the SQL DISTINCT keyword to return only unique (different) records.
            /// </summary>
            /// <param name="propertyName">The property name, dot separated tree name for parent/child classes or a single dot for previous property name.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> Distinct(string propertyName)
            {
                _distinctColumns.Add(GetColumnName(propertyName));
                return Distinct();
            }

            /// <summary>
            /// Adds the SQL DISTINCT keyword to return only unique (different) records.
            /// </summary>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> Distinct()
            {
                _distinct = true;
                return this;
            }

            /// <summary>
            /// Skips a specified number of records in the result set.
            /// </summary>
            /// <param name="count"></param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> Skip(int count)
            {
                _skip = count;
                return this;
            }

            /// <summary>
            /// Takes a specified number of records from the result set.
            /// </summary>
            /// <param name="count"></param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> Take(int count)
            {
                _take = count;
                return this;
            }

            /// <summary>
            /// Takes the specified page from the result set.
            /// </summary>
            /// <param name="pageIndex"></param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> TakePage(int pageIndex)
            {
                if (_dal.PageSize.HasValue)
                {
                    _skip = ((pageIndex - 1) * _dal.PageSize.Value);
                    _take = _dal.PageSize.Value;
                }

                return this;
            }

            /// <summary>
            /// Executes the generated query and returns the result as a list of T.
            /// </summary>
            /// <returns></returns>
            public List<T> ToList()
            {
                Prepare();
                return _dal.Query<T>(_cmd);
            }

            /// <summary>
            /// Executes the generated query and returns the result as a list of T.
            /// </summary>
            /// <returns></returns>
            public async Task<List<T>> ToListAsync()
            {
                Prepare();
                return await _dal.QueryAsync<T>(_cmd).ConfigureAwait(false);
            }

            /// <summary>
            /// Executes the generated query and returns the result as dataset.
            /// </summary>
            /// <returns></returns>
            public DataSet ToDataSet()
            {
                Prepare();
                return _dal.QueryAsDataSet(_cmd);
            }

            /// <summary>
            /// Executes the generated query and returns the result as dataset.
            /// </summary>
            /// <returns></returns>
            public async Task<DataSet> ToDataSetAsync()
            {
                Prepare();
                return await _dal.QueryAsDataSetAsync(_cmd).ConfigureAwait(false);
            }

            private void Prepare()
            {
                CloseGroups();
                UpdateJoinOn(true);

                if (_where.Length == 0)
                    _where.Append("1=1");

                _cmd.CommandText = string.Format("SELECT{0}{1}{2} FROM {3} WHERE {4} {5} {6} {7}",
                                    (_distinct) ? " DISTINCT " : " ",
                                    (_distinctColumns.Count > 0) ? string.Join(",", _distinctColumns) : string.Join(",", _columns),
                                    _select.ToString(), string.Join(" ", _tables),
                                    _where.ToString(), _groupBy.ToString(), _having.ToString(), _orderBy.ToString());

                if (_take > 0)
                    _dal.LimitResult<T>(_cmd, _skip, _take);
            }

            /// <summary>
            /// Creates an equal to condition in the current query.
            /// </summary>
            /// <param name="value">The value to compare.</param>
            /// <param name="closeGroups">A value indicating the amount of groups (parentheses) to close.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> EqualTo(object value, int closeGroups = 0)
            {
                if (value == null)
                    value = DBNull.Value;

                _where.Append(string.Format(" {0}{1}", EqualityOperator(value, "="), (value != DBNull.Value) ? " " + _dal.CreateParameter(_cmd, "P" + _cmd.Parameters.Count, value) : ""));
                CloseGroups(closeGroups);
                return this;
            }

            /// <summary>
            /// Creates a not equal to condition in the current query.
            /// </summary>
            /// <param name="value">The value to compare.</param>
            /// <param name="closeGroups">A value indicating the amount of groups (parentheses) to close.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> NotEqualTo(object value, int closeGroups = 0)
            {
                if (value == null)
                    value = DBNull.Value;

                _where.Append(string.Format(" {0}{1}", EqualityOperator(value, "<>"), (value != DBNull.Value) ? " " + _dal.CreateParameter(_cmd, "P" + _cmd.Parameters.Count, value) : ""));
                CloseGroups(closeGroups);
                return this;
            }

            /// <summary>
            /// Creates a greater than condition in the current query.
            /// </summary>
            /// <param name="value">The value to compare.</param>
            /// <param name="closeGroups">A value indicating the amount of groups (parentheses) to close.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> GreaterThan(object value, int closeGroups = 0)
            {
                _where.Append(string.Format(" > {0}", _dal.CreateParameter(_cmd, "P" + _cmd.Parameters.Count, value)));
                CloseGroups(closeGroups);
                return this;
            }

            /// <summary>
            /// Creates a greater than or equal to condition in the current query.
            /// </summary>
            /// <param name="value">The value to compare.</param>
            /// <param name="closeGroups">A value indicating the amount of groups (parentheses) to close.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> GreaterThanOrEqualTo(object value, int closeGroups = 0)
            {
                _where.Append(string.Format(" >= {0}", _dal.CreateParameter(_cmd, "P" + _cmd.Parameters.Count, value)));
                CloseGroups(closeGroups);
                return this;
            }

            /// <summary>
            /// Creates a less than condition in the current query.
            /// </summary>
            /// <param name="value">The value to compare.</param>
            /// <param name="closeGroups">A value indicating the amount of groups (parentheses) to close.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> LessThan(object value, int closeGroups = 0)
            {
                _where.Append(string.Format(" < {0}", _dal.CreateParameter(_cmd, "P" + _cmd.Parameters.Count, value)));
                CloseGroups(closeGroups);
                return this;
            }

            /// <summary>
            /// Creates a less than or equal to condition in the current query.
            /// </summary>
            /// <param name="value">The value to compare.</param>
            /// <param name="closeGroups">A value indicating the amount of groups (parentheses) to close.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> LessThanOrEqualTo(object value, int closeGroups = 0)
            {
                _where.Append(string.Format(" <= {0}", _dal.CreateParameter(_cmd, "P" + _cmd.Parameters.Count, value)));
                CloseGroups(closeGroups);
                return this;
            }

            /// <summary>
            /// Creates a starts with condition in the current query.
            /// </summary>
            /// <param name="value">The value to compare.</param>
            /// <param name="closeGroups">A value indicating the amount of groups (parentheses) to close.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> StartsWith(object value, int closeGroups = 0)
            {
                return StartsWith(value, false, closeGroups);
            }

            /// <summary>
            /// Creates a not starts with condition in the current query.
            /// </summary>
            /// <param name="value">The value to compare.</param>
            /// <param name="closeGroups">A value indicating the amount of groups (parentheses) to close.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> NotStartsWith(object value, int closeGroups = 0)
            {
                return StartsWith(value, true, closeGroups);
            }

            /// <summary>
            /// Creates an ends with condition in the current query.
            /// </summary>
            /// <param name="value">The value to compare.</param>
            /// <param name="closeGroups">A value indicating the amount of groups (parentheses) to close.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> EndsWith(object value, int closeGroups = 0)
            {
                return EndsWith(value, false, closeGroups);
            }

            /// <summary>
            /// Creates a not ends with condition in the current query.
            /// </summary>
            /// <param name="value">The value to compare.</param>
            /// <param name="closeGroups">A value indicating the amount of groups (parentheses) to close.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> NotEndsWith(object value, int closeGroups = 0)
            {
                return EndsWith(value, true, closeGroups);
            }

            /// <summary>
            /// Creates a contains condition in the current query.
            /// </summary>
            /// <param name="value">The value to compare.</param>
            /// <param name="closeGroups">A value indicating the amount of groups (parentheses) to close.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> Contains(object value, int closeGroups = 0)
            {
                return Contains(value, false, closeGroups);
            }

            /// <summary>
            /// Creates a contains condition in the current query.
            /// </summary>
            /// <param name="value">The value to compare.</param>
            /// <param name="closeGroups">A value indicating the amount of groups (parentheses) to close.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> NotContains(object value, int closeGroups = 0)
            {
                return Contains(value, true, closeGroups);
            }

            /// <summary>
            /// Creates a between condition in the current query.
            /// </summary>
            /// <param name="rangeStart">The start value to compare.</param>
            /// <param name="rangeEnd">The end value to compare.</param>
            /// <param name="closeGroups">A value indicating the amount of groups (parentheses) to close.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> Between(object rangeStart, object rangeEnd, int closeGroups = 0)
            {
                return Between(rangeStart, rangeEnd, false, closeGroups);
            }

            /// <summary>
            /// Creates a not between condition in the current query.
            /// </summary>
            /// <param name="rangeStart">The start value to compare.</param>
            /// <param name="rangeEnd">The end value to compare.</param>
            /// <param name="closeGroups">A value indicating the amount of groups (parentheses) to close.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> NotBetween(object rangeStart, object rangeEnd, int closeGroups = 0)
            {
                return Between(rangeStart, rangeEnd, true, closeGroups);
            }

            /// <summary>
            /// Creates an in condition in the current query.
            /// </summary>
            /// <param name="values">The value to compare.</param>
            /// <param name="closeGroups">A value indicating the amount of groups (parentheses) to close.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> In<E>(List<E> values, int closeGroups = 0)
            {
                return In(values, false, closeGroups);
            }

            /// <summary>
            /// Creates a not in condition in the current query.
            /// </summary>
            /// <param name="values">The value to compare.</param>
            /// <param name="closeGroups">A value indicating the amount of groups (parentheses) to close.</param>
            /// <returns>A FluentQuery<typeparamref name="T"/> class.</returns>
            public FluentQuery<T> NotIn<E>(List<E> values, int closeGroups = 0)
            {
                return In(values, true, closeGroups);
            }

            private FluentQuery<T> StartsWith(object value, bool not, int closeGroups = 0)
            {
                _where.Append(string.Format(" {0}LIKE {1}", (not) ? "NOT " : "", _dal.CreateParameter(_cmd, "P" + _cmd.Parameters.Count, value.ToString() + "%")));
                CloseGroups(closeGroups);
                return this;
            }

            private FluentQuery<T> EndsWith(object value, bool not, int closeGroups = 0)
            {
                _where.Append(string.Format(" {0}LIKE {1}", (not) ? "NOT " : "", _dal.CreateParameter(_cmd, "P" + _cmd.Parameters.Count, "%" + value.ToString())));
                CloseGroups(closeGroups);
                return this;
            }

            private FluentQuery<T> Contains(object value, bool not, int closeGroups = 0)
            {
                _where.Append(string.Format(" {0}LIKE {1}", (not) ? "NOT " : "", _dal.CreateParameter(_cmd, "P" + _cmd.Parameters.Count, "%" + value.ToString() + "%")));
                CloseGroups(closeGroups);
                return this;
            }

            private FluentQuery<T> Between(object rangeStart, object rangeEnd, bool not, int closeGroups = 0)
            {
                _where.Append(string.Format(" {0}BETWEEN {1} AND {2}", (not) ? "NOT " : "", _dal.CreateParameter(_cmd, "P" + _cmd.Parameters.Count, rangeStart), _dal.CreateParameter(_cmd, "P" + _cmd.Parameters.Count, rangeEnd)));
                CloseGroups(closeGroups);
                return this;
            }

            private FluentQuery<T> In<E>(List<E> values, bool not, int closeGroups = 0)
            {
                List<string> parameters = new List<string>();

                foreach (E value in values)
                {
                    parameters.Add(_dal.CreateParameter(_cmd, "P" + _cmd.Parameters.Count, value));
                }

                _where.Append(string.Format(" {0}IN ({1})", (not) ? "NOT " : "", string.Join(",", parameters)));
                CloseGroups(closeGroups);
                return this;
            }

            private string EqualityOperator(object value, string defaultOperator)
            {
                if (value.Equals(DBNull.Value))
                    return (defaultOperator == "=") ? "IS NULL" : "IS NOT NULL";

                return defaultOperator;
            }

            private string GetColumnName(string propertyName, bool alias = false)
            {
                if (string.IsNullOrEmpty(propertyName) || propertyName.Equals(_lastPropertyName, StringComparison.OrdinalIgnoreCase))
                    return (alias) ? _lastColumnNameAlias : _lastColumnName;

                string[] names = propertyName.Split('.');
                Type type = typeof(T);
                PropertyInfo property;
                PropInfo propInfo;
                int index = 0;
                string treeName = null;
                string tableAlias;

                _lastPropertyName = propertyName;

                try
                {
                    foreach (string name in names)
                    {
                        property = TypeCache.GetType(type).GetProperty(name);
                        propInfo = _dal.GetPropInfo(property);

                        if (index < names.Length - 1)
                        {
                            if (propInfo.IsClassList)
                                type = propInfo.ItemType;
                            else if (propInfo.IsClass)
                                type = propInfo.PropType;
                            else
                                throw new System.Exception(string.Format("Property type mismatch, property should either be class or class collection."));

                            treeName = (string.IsNullOrEmpty(treeName)) ? name : string.Format("{0}.{1}", treeName, name);
                            index++;
                        }
                        else
                        {
                            tableAlias = "T0";

                            if (!string.IsNullOrEmpty(treeName) && _tableAliasMapping.ContainsKey(typeof(T).Name + "." + treeName))
                                tableAlias = _tableAliasMapping[typeof(T).Name + "." + treeName];

                            _lastColumnNameAlias = _dal.Escape(tableAlias + "_" + propInfo.ColumnName);
                            _lastColumnName = string.Format("{0}.{1}", tableAlias, _dal.Escape(propInfo.ColumnName));

                            if (alias)
                                return _lastColumnNameAlias;
                            else
                                return _lastColumnName;
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new System.Exception(string.Format("Unable to get the column name for property with tree name '{0}'.", propertyName), ex);
                }

                return null;
            }

            private void OpenGroups(int groupCount)
            {
                for (int i = 0; i < groupCount; i++)
                {
                    _where.Append("(");
                }
            }

            private void CloseGroups(int groupCount)
            {
                for (int i = 0; i < groupCount; i++)
                {
                    _where.Append(")");
                }
            }

            private void CloseGroups()
            {
                int openCount = _where.ToString().Split('(').Length - 1;
                int closeCount = _where.ToString().Split(')').Length - 1;
                int groupCount = (openCount - closeCount);

                if (groupCount > 0 && _where.ToString().EndsWith("("))
                    _where.Append("1=1"); // where without follow up

                CloseGroups(groupCount);
            }

            private void UpdateJoinOn(bool end = false)
            {
                if (_tableAlias == null)
                    return;

                List<int> joinOn;
                int index;
                string on;

                if (!_joinOn.TryGetValue(_tableAlias, out joinOn))
                {
                    joinOn = _joinOn[_tableAlias] = new List<int>() { 0, 0 };
                }

                if (!end)
                    joinOn[0] = _where.Length;
                else
                {
                    joinOn[1] = _where.Length;
                    index = int.Parse(_tableAlias.Substring(1));
                    _tableAlias = null;

                    // append where part to correct table join                    
                    on = _where.ToString().Substring(joinOn[0], joinOn[1] - joinOn[0]);
                    _where.Remove(joinOn[0], joinOn[1] - joinOn[0]);
                    _tables[index] += " AND " + on;
                }
            }
        }
    }
}
