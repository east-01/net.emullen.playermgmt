using System;
using System.Runtime.Serialization;
using EMullen.PlayerMgmt;

namespace EMullen.Core.PlayerMgmt 
{
    /// <summary>
    /// An exception relating to all database errors, holds the culprit WebRequestException
    ///   and additional message from the database server.
    /// </summary>
    [Serializable]
    public class DatabaseException : Exception
    {
        public UnityWebRequestException WebException => InnerException != null ? (UnityWebRequestException) InnerException : null;

        public DatabaseException() : base() {}
        public DatabaseException(string message) : base(message) {}
        public DatabaseException(string message, Exception innerException) : base(message, innerException) {}
        protected DatabaseException(SerializationInfo info, StreamingContext context) : base(info, context) {}
    }
}