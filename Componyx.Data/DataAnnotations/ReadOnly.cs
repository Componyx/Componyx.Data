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

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ReadOnlyAttribute : Attribute
    {
    }
}
