using Huawei.SCOM.ESightPlugin.ViewLib.Model;
using Huawei.SCOM.ESightPlugin.ViewLib.Utils;
using Microsoft.EnterpriseManagement.Common;
using Microsoft.EnterpriseManagement.Configuration;
using Microsoft.EnterpriseManagement.ConnectorFramework;
using Microsoft.EnterpriseManagement.Monitoring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Result = Huawei.SCOM.ESightPlugin.ViewLib.Model.Result;

namespace Huawei.SCOM.ESightPlugin.ViewLib.OM12R2
{
    public static class OM12ESightApplianceOperationRepo
    {
        public static ManagementPackClass GetMPClass()
        {
            return OM12Connection.GetManagementPackClass(ESightApplianceOperation.EntityClassName);
        }

        public static Result All()
        {
            try
            {
                IObjectReader<EnterpriseManagementObject> objects =
                    OM12Connection.All(ESightApplianceOperation.EntityClassName);
                return Result.Done(objects.ToList());
            }
            catch (Exception e)
            {
                return Result.Failed(100, $"Internal error caused by {e.Message}", e);
            }
        }

        public static CreatableEnterpriseManagementObject GetManagementObject(ESightApplianceOperation operation)
        {
            ManagementPackClass MPClass = GetMPClass();
            CreatableEnterpriseManagementObject EMOAppliance =
                new CreatableEnterpriseManagementObject(OM12Connection.HuaweiESightMG, MPClass);
            var props = OM12Connection.GetManagementPackProperties(EMOAppliance);
            EMOAppliance[props["Id"]].Value = operation.Id;
            EMOAppliance[props["Host"]].Value = operation.Host;
            EMOAppliance[props["OperationType"]].Value = operation.OperationType;
            EMOAppliance[props["IsSystemIdChanged"]].Value = operation.IsSystemIdChanged;
            EMOAppliance[props["CreatedOn"]].Value = DateTime.Now;

            ManagementPackClass baseEntity = OM12Connection.GetManagementPackClass("System.Entity");
            EMOAppliance[baseEntity, "DisplayName"].Value = operation.Host;
            return EMOAppliance;
        }

        public static Result Add(ESightApplianceOperation operation)
        {
            try
            {
                CreatableEnterpriseManagementObject EMOApplianceOperation = GetManagementObject(operation);
                EMOApplianceOperation.Commit();
                return Result.Done();
            }
            catch (Exception e)
            {
                return Result.Failed(100, $"Internal error caused by {e.Message}", e);
            }
        }

    }
}
