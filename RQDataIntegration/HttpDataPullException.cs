using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RQDataIntegration
{
    internal class HttpDataPullException : Exception
    {
        internal HttpDataPullException() : base()
        {
            
        }
        
        internal HttpDataPullException(string message) : base(message)
        {
            
        }

        internal HttpDataPullException(string message, Exception innerException)
        {
            
        }
    }
}
