using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Huawei.SCOM.ESightPlugin.ViewLib.Model
{
    public class Result
    {
        private const int DEFAULT_FAILIRE_CODE = 100;

        /// <summary>
        /// whether action succeed
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// failed code when action failed
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// failed message when action failed
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// failed message when action failed
        /// </summary>
        public Exception Cause { get; set; }

        public string FullMessage
        {
            get
            {
                if (Success)
                {
                    return Message;
                }
                else
                {
                    return $"{Code}: {Message}";
                }
            }
        }

        /// <summary>
        /// Action result if provided
        /// </summary>
        public Object Data { get; set; }

        public static Result Done(Object data)
        {
            Result result = new Result
            {
                Success = true,
                Message = String.Empty,
                Data = data
            };
            return result;
        }

        public static Result Done(string message, Object data)
        {
            Result result = new Result
            {
                Success = true,
                Message = message,
                Data = data,
            };
            return result;
        }

        public static Result Done()
        {
            Result result = new Result
            {
                Success = true,
                Message = String.Empty,
            };
            return result;
        }

        public static Result Failed(String message)
        {
            return Result.Failed(DEFAULT_FAILIRE_CODE, message);
        }

        public static Result Failed(int code, String message)
        {
            Result result = new Result
            {
                Success = false,
                Message = message,
                Code = code
            };
            return result;
        }

        public static Result Failed(int code, String message, Exception cause)
        {
            Result result = new Result
            {
                Success = false,
                Message = message,
                Code = code,
                Cause = cause,
            };
            return result;
        }

        public static Result Failed(int code, String message, Object data)
        {
            Result result = new Result
            {
                Success = false,
                Message = message,
                Code = code,
                Data = data
            };
            return result;
        }

    }
}
