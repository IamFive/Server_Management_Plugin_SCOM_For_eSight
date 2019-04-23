using System;
using System.Collections.Generic;
using System.Security;
using Microsoft.EnterpriseManagement;
using Microsoft.EnterpriseManagement.Common;
using Microsoft.EnterpriseManagement.Configuration;
using Microsoft.EnterpriseManagement.ConnectorFramework;
using Microsoft.EnterpriseManagement.Monitoring;
using Microsoft.Win32;

namespace Huawei.SCOM.ESightPlugin.ViewLib.OM12R2
{

    public static class OM12Connection
    {

        private static ManagementGroup _huaweiESightMG = null;
        private static MonitoringConnector _huaweiESightConnector = null;
        private static readonly Guid HuaweiESightGuid = new Guid("{11e62afc-aaf9-417c-8125-c31273270968}");

        public static ManagementGroup HuaweiESightMG
        {
            get
            {
                if (_huaweiESightMG != null)
                {
                    if (!_huaweiESightMG.IsConnected)
                    {
                        _huaweiESightMG.Reconnect();
                        _huaweiESightConnector.Reconnect(_huaweiESightMG);
                    }
                }
                return _huaweiESightMG;
            }
            set => _huaweiESightMG = value;
        }

        public static MonitoringConnector HuaweiESightConnector
        {
            get => _huaweiESightConnector;
            set => _huaweiESightConnector = value;
        }

        static OM12Connection()
        {
            OM12Connection.HuaweiESightMG = new ManagementGroup("localhost");
            if (!OM12Connection.CreateConnection())
            {
                OM12Connection.CreateNewConnector();
            }
        }

        private static void CreateNewConnector()
        {
            ConnectorInfo connectorInfo = new ConnectorInfo
            {
                Name = "HUAWEI ESight Connector",
                DisplayName = "HUAWEI ESight Connector",
                Description = "Connector for HUAWEI ESight Management Packs"
            };
            OM12Connection.HuaweiESightConnector = OM12Connection.HuaweiESightMG.ConnectorFramework.Setup(connectorInfo, OM12Connection.HuaweiESightGuid);
        }

        private static bool CreateConnection()
        {
            try
            {
                OM12Connection.HuaweiESightConnector = OM12Connection.HuaweiESightMG.ConnectorFramework.GetConnector(OM12Connection.HuaweiESightGuid);
                if (!OM12Connection.HuaweiESightConnector.Initialized)
                {
                    OM12Connection.HuaweiESightConnector.Initialize();
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// The get management pack class.
        /// </summary>
        /// <param name="entityClassName">
        /// The class name.
        /// </param>
        /// <returns>
        /// The <see cref="ManagementPackClass"/>.
        /// </returns>
        /// <exception cref="ApplicationException">Failed to find management pack class </exception>
        public static ManagementPackClass GetManagementPackClass(string entityClassName)
        {
            ManagementPackClassCriteria criteria = new ManagementPackClassCriteria("Name='" + entityClassName + "'");
            IList<ManagementPackClass> mpClasses = OM12Connection.HuaweiESightMG.EntityTypes.GetClasses(criteria);
            if (mpClasses.Count == 0)
            {
                throw new ApplicationException("Failed to find management pack class " + entityClassName);
            }
            return mpClasses[0];
        }

        public static IDictionary<String, ManagementPackProperty> GetManagementPackProperties(string entityClassName)
        {
            ManagementPackClass clazz = GetManagementPackClass(entityClassName);
            IList<ManagementPackProperty> props = clazz.GetProperties();
            IDictionary<String, ManagementPackProperty> keyedProperties = new Dictionary<String, ManagementPackProperty>();
            foreach (var prop in props)
            {
                keyedProperties.Add(prop.Name, prop);
            }
            return keyedProperties;
        }

        public static IDictionary<String, ManagementPackProperty> GetManagementPackProperties(EnterpriseManagementObject obj)
        {
            IList<ManagementPackProperty> props = obj.GetProperties();
            IDictionary<String, ManagementPackProperty> keyedProperties = new Dictionary<String, ManagementPackProperty>();
            foreach (var prop in props)
            {
                keyedProperties.Add(prop.Name, prop);
            }
            return keyedProperties;
        }

        public static IObjectReader<EnterpriseManagementObject> All(string entityClassName)
        {
            ManagementPackClass clazz = GetManagementPackClass(entityClassName);
            IObjectReader<EnterpriseManagementObject> items = HuaweiESightMG.EntityObjects
                .GetObjectReader<EnterpriseManagementObject>(clazz, ObjectQueryOptions.Default);
            return items;
        }

        public static IObjectReader<EnterpriseManagementObject> All(string entityClassName, ObjectQueryOptions queryOption)
        {
            ManagementPackClass clazz = GetManagementPackClass(entityClassName);
            IObjectReader<EnterpriseManagementObject> items = HuaweiESightMG.EntityObjects
                .GetObjectReader<EnterpriseManagementObject>(clazz, queryOption);
            return items;
        }

        public static IObjectReader<EnterpriseManagementObject> Query(string entityClassName, string criteria)
        {
            ManagementPackClass clazz = GetManagementPackClass(entityClassName);
            EnterpriseManagementObjectCriteria c = new EnterpriseManagementObjectCriteria(criteria, clazz);
            IObjectReader<EnterpriseManagementObject> items = HuaweiESightMG.EntityObjects
                .GetObjectReader<EnterpriseManagementObject>(c, ObjectQueryOptions.Default);
            return items;
        }

        public static IObjectReader<EnterpriseManagementObject> Query(string entityClassName, string criteria,
            ObjectQueryOptions queryOption)
        {
            ManagementPackClass clazz = GetManagementPackClass(entityClassName);
            EnterpriseManagementObjectCriteria c = new EnterpriseManagementObjectCriteria(criteria, clazz);
            IObjectReader<EnterpriseManagementObject> items = HuaweiESightMG.EntityObjects
                .GetObjectReader<EnterpriseManagementObject>(c, queryOption);
            return items;
        }


#if DEBUG
        /// <summary>
        /// ConvertToSecureString
        /// </summary>
        /// <param name="password">pd</param>
        /// <returns>SecureString</returns>
        private static SecureString ConvertToSecureString(string password)
        {
            if (password == null)
            {
                throw new ArgumentNullException("pd");
            }

            var SecuredPasswd = new SecureString();
            foreach (char c in password)
            {
                SecuredPasswd.AppendChar(c);
            }
            SecuredPasswd.MakeReadOnly();
            return SecuredPasswd;
        }
#endif
    }
}
