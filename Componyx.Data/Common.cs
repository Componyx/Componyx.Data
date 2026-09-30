using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Componyx.Data
{
    internal class Common
    {
        /// <summary>
        /// Gets the name of an instance property from a property access lambda.
        /// </summary>
        /// <typeparam name="T">Type of the property.</typeparam>
        /// <param name="propertyExpressions">A list of Lambda expression in the form: '() => Class.Property'.</param>
        /// <returns>The name of the property.</returns>
        internal static string GetPropertyNames<T>(List<Expression<Func<object>>> propertyExpressions)
        {
            List<string> names = new List<string>();

            foreach (Expression<Func<object>> lambda in propertyExpressions)
            {
                names.Add(GetPropertyName(lambda));
            }

            return string.Join(".", names);
        }


        /// <summary>
        /// Gets the name of an instance property from a property access lambda.
        /// </summary>
        /// <typeparam name="T">Type of the property.</typeparam>
        /// <param name="propertyExpression">Lambda expression in the form: '() => Class.Property'.</param>
        /// <returns>The name of the property.</returns>
        internal static string GetPropertyName<T>(Expression<Func<T>> propertyExpression)
        {
            MemberExpression me;
            switch (propertyExpression.Body.NodeType)
            {
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                    var ue = propertyExpression.Body as UnaryExpression;
                    me = ((ue != null) ? ue.Operand : null) as MemberExpression;
                    break;
                default:
                    me = propertyExpression.Body as MemberExpression;
                    break;
            }

            if (me == null)
            {
                throw new ArgumentException("You must pass a lambda expression: '() => Class.Property'.");
            }

            return me.Member.Name;
        }
    }
}
