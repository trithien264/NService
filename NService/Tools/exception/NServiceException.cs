using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NService
{
    public class NSErrorException : Exception
    {
        #region Standard constructors - do not use

        /// <summary>
        /// Create a new <see cref="PCIAppException"/>. Do not use this constructor, it
        /// does not take any of the data that makes this type useful.
        /// </summary>
        public NSErrorException()
        {

        }

        /// <summary>
        /// Create a new <see cref="PCIAppException"/>. Do not use this constructor, it
        /// does not take any of the data that makes this type useful.
        /// </summary>
        /// <param name="message">Error message, ignored.</param>
        public NSErrorException(string message) : base(message)
        {
        }

        /// <summary>
        /// Create a new <see cref="PCIAppException"/>. Do not use this constructor, it
        /// does not take any of the data that makes this type useful.
        /// </summary>
        /// <param name="message">Error message, ignored.</param>
        /// <param name="innerException">Inner exception.</param>
        public NSErrorException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        #endregion
    }

    public class NSInfoException : Exception
    {
        #region Standard constructors - do not use

        /// <summary>
        /// Create a new <see cref="PCIAppException"/>. Do not use this constructor, it
        /// does not take any of the data that makes this type useful.
        /// </summary>
        public NSInfoException()
        {

        }

        /// <summary>
        /// Create a new <see cref="PCIAppException"/>. Do not use this constructor, it
        /// does not take any of the data that makes this type useful.
        /// </summary>
        /// <param name="message">Error message, ignored.</param>
        public NSInfoException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Create a new <see cref="PCIAppException"/>. Do not use this constructor, it
        /// does not take any of the data that makes this type useful.
        /// </summary>
        /// <param name="message">Error message, ignored.</param>
        /// <param name="innerException">Inner exception.</param>
        public NSInfoException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        #endregion
    }

    [Serializable]
    public class NSNeedLoginException : Exception
    {


        #region Standard constructors - do not use

        /// <summary>
        /// Create a new <see cref="PCIAppException"/>. Do not use this constructor, it
        /// does not take any of the data that makes this type useful.
        /// </summary>
        public NSNeedLoginException()
        {

        }

        /// <summary>
        /// Create a new <see cref="PCIAppException"/>. Do not use this constructor, it
        /// does not take any of the data that makes this type useful.
        /// </summary>
        /// <param name="message">Error message, ignored.</param>
        public NSNeedLoginException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Create a new <see cref="PCIAppException"/>. Do not use this constructor, it
        /// does not take any of the data that makes this type useful.
        /// </summary>
        /// <param name="message">Error message, ignored.</param>
        /// <param name="innerException">Inner exception.</param>
        public NSNeedLoginException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        #endregion

    }

    [Serializable]
    public class NSNoPermissionException : Exception
    {


        #region Standard constructors - do not use

        /// <summary>
        /// Create a new <see cref="PCIAppException"/>. Do not use this constructor, it
        /// does not take any of the data that makes this type useful.
        /// </summary>
        public NSNoPermissionException()
        {

        }

        /// <summary>
        /// Create a new <see cref="PCIAppException"/>. Do not use this constructor, it
        /// does not take any of the data that makes this type useful.
        /// </summary>
        /// <param name="message">Error message, ignored.</param>
        public NSNoPermissionException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Create a new <see cref="PCIAppException"/>. Do not use this constructor, it
        /// does not take any of the data that makes this type useful.
        /// </summary>
        /// <param name="message">Error message, ignored.</param>
        /// <param name="innerException">Inner exception.</param>
        public NSNoPermissionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        #endregion

       
    }
}
