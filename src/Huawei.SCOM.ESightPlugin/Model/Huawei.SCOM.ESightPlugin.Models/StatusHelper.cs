//**************************************************************************  
//Copyright (C) 2019 Huawei Technologies Co., Ltd. All rights reserved.
//This program is free software; you can redistribute it and/or modify
//it under the terms of the MIT license.

//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//MIT license for more detail.
//*************************************************************************  
// --------------------------------------------------------------------------------------------------------------------
// <copyright file="StatusHelper.cs" company="">
//   
// </copyright>
// <summary>
//   The status helper.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;

namespace Huawei.SCOM.ESightPlugin.Models
{
    /// <summary>
    /// The status helper.
    /// </summary>
    public class StatusHelper
    {

        /// <summary>
        /// 0：正常
        /// -1：离线
        /// -2：未知
        /// 其他：故障
        /// </summary>
        /// <param name="status">The status.</param>
        /// <returns>0 if health, -2 if error, -3 if no update required.</returns>
        public static string GroupComponentHealthStatus(string status)
        {
            switch (status)
            {
                case "0":
                    return "0";
                
                case "-1":
                case "-2": 
                    return "-3"; // 返回-3 标识健康状态本次不做更新

                default:
                    return "-2";
            }
        }

        /// <summary>
        /// 0：正常
        /// -2/5 ：未知
        /// 其他：故障
        /// </summary>
        /// <param name="status">The status.</param>
        /// <returns>0 if health, -2 if error, -3 if no update required.</returns>
        public static string GroupMezzHealthStatus(string status)
        {
            switch (status)
            {
                case "0":
                    return "0";

                case "5":
                case "-2":
                    return "-3"; // 返回-3 标识健康状态本次不做更新

                default:
                    return "-2";
            }
        }

        public static string ConvertComponentHealthStatusToScomHealthStatus(string groupedHealthStatus)
        {
            switch (groupedHealthStatus)
            {
                case "0":
                    return "OK";

                case "-2":
                    return "Critical";

                case "-3":   // should not be used
                    return "Not changed";

                default:
                    throw new ArgumentException($"Do not know how to convert health status {groupedHealthStatus} to SCOM health status.");
            }
        }


        /// <summary>
        /// convert Device status to SCOM health status
        /// 0：正常
        /// -1：离线
        /// -2：未知
        /// 其他：故障
        /// </summary>
        /// <param name="status">The status.</param>
        /// <returns>0 if health, -2 if error, -3 if no update required.</returns>

        public static string ConvertDeviceStatusToScomHealthStatus(string status)
        {
            switch (status)
            {
                case "0":
                    return "OK";

                case "-1":
                    return "Warning";

                case "-2":
                    return "Critical";

                default:
                    return "Critical";
            }
        }

        /// <summary>
        /// The get present state. 
        /// CPU、Memory、Disk、电源、风扇、Board六种部件的在位状态,按照0展示为不在位、-2和2位未知其余为在位 ，未知显示为： Unkown 
        /// </summary>
        /// <param name="presentState">
        /// The present state.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        public static string GetPresentState(string presentState)
        {
            if (presentState == null)
            {
                return "Present";
            }
            if (presentState == "0")
            {
                return "Absent";
            }
            if (presentState == "-2" || presentState == "2")
            {
                return "Unkown";
            }
            return "Present";
        }
    }
}