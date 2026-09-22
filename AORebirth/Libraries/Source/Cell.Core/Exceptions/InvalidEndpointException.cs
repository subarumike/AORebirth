using System;
using System.Net;
using System.Runtime.Serialization;
#if !AOREBIRTH_LINUX
using System.Security.Permissions;
#endif

namespace Cell.Core.Exceptions
{
    [Serializable]
	public class InvalidEndpointException : Exception
	{
		private IPEndPoint _endpoint;

		public InvalidEndpointException(IPEndPoint ep)
		{
			_endpoint = ep;
		}

		public InvalidEndpointException(IPEndPoint ep, string message)
			: base(message)
		{ 
			_endpoint = ep;
		}

		public IPEndPoint Endpoint
		{
			get
			{
				return _endpoint;
			}
		}

#if !AOREBIRTH_LINUX
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException("info");

            base.GetObjectData(info, context);
        }
#endif
	}
}
