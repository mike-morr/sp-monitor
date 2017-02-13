using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPMonitor
{

    [Serializable]
    public class CreateListFailedException : Exception
    {
        public CreateListFailedException() { }
        public CreateListFailedException(string message) : base(message) { }
        public CreateListFailedException(string message, Exception inner) : base(message, inner) { }
        protected CreateListFailedException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }


    [Serializable]
    public class CreateListItemFailedException : Exception
    {
        public CreateListItemFailedException() { }
        public CreateListItemFailedException(string message) : base(message) { }
        public CreateListItemFailedException(string message, Exception inner) : base(message, inner) { }
        protected CreateListItemFailedException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}

