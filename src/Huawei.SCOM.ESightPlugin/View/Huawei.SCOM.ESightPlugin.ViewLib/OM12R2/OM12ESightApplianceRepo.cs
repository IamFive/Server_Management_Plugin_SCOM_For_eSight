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
using static Huawei.SCOM.ESightPlugin.ViewLib.Model.Constants;
using Result = Huawei.SCOM.ESightPlugin.ViewLib.Model.Result;

namespace Huawei.SCOM.ESightPlugin.ViewLib.OM12R2
{
    public static class OM12ESightApplianceRepo
    {
        public static ManagementPackClass GetMPClass()
        {
            return OM12Connection.GetManagementPackClass(ESightAppliance.EntityClassName);
        }

        public static Result All()
        {
            try
            {
                IObjectReader<EnterpriseManagementObject> objects = OM12Connection.All(ESightAppliance.EntityClassName);
                return Result.Done(objects.ToList());
            }
            catch (Exception e)
            {
                return Result.Failed(100, $"Internal error caused by {e.Message}", e);
            }
        }

        public static Result FindByHost(string host)
        {
            try
            {
                IObjectReader<EnterpriseManagementObject> items = OM12Connection.Query(ESightAppliance.EntityClassName, $"Host='{host}'");
                return Result.Done(items.ToList());
            }
            catch (Exception e)
            {
                return Result.Failed(100, $"Internal error caused by {e.Message}", e);
            }
        }

        public static Result Add(ESightAppliance appliance)
        {
            try
            {
                Result findByHostResult = FindByHost(appliance.Host);
                if (!findByHostResult.Success)
                {
                    return findByHostResult;
                }
                else
                {
                    List<EnterpriseManagementObject> filteredList
                        = findByHostResult.Data as List<EnterpriseManagementObject>;
                    if (filteredList.Count > 0)
                    {
                        return Result.Failed(101, $"ESight {appliance.Host} already exsits.");
                    }
                }

                IncrementalDiscoveryData incrementalDiscoveryData = new IncrementalDiscoveryData();

                // add appliance record
                ManagementPackClass MPClass = GetMPClass();
                CreatableEnterpriseManagementObject EMOAppliance =
                    new CreatableEnterpriseManagementObject(OM12Connection.HuaweiESightMG, MPClass);
                IDictionary<string, ManagementPackProperty> props =
                    OM12Connection.GetManagementPackProperties(EMOAppliance);
                EMOAppliance[props["Host"]].Value = appliance.Host;
                EMOAppliance[props["Port"]].Value = appliance.Port;
                EMOAppliance[props["AliasName"]].Value = appliance.AliasName;
                EMOAppliance[props["SystemId"]].Value = appliance.SystemId;
                EMOAppliance[props["LoginAccount"]].Value = appliance.LoginAccount;
                EMOAppliance[props["LoginPassword"]].Value = RijndaelManagedCrypto.Instance
                    .EncryptForCS(appliance.LoginPassword);
                EMOAppliance[props["LastModifiedOn"]].Value = DateTime.UtcNow;
                EMOAppliance[props["CreatedOn"]].Value = DateTime.UtcNow;

                EMOAppliance[props["OpenID"]].Value = Guid.NewGuid().ToString("D");
                EMOAppliance[props["SubscribeID"]].Value = Guid.NewGuid().ToString("D");
                EMOAppliance[props["SubKeepAliveStatus"]].Value = 0;
                EMOAppliance[props["SubscriptionAlarmStatus"]].Value = 0;
                EMOAppliance[props["SubscriptionNeDeviceStatus"]].Value = 0;

                EMOAppliance[props["SubKeepAliveError"]].Value = string.Empty;
                EMOAppliance[props["SubscripeAlarmError"]].Value = string.Empty;
                EMOAppliance[props["SubscripeNeDeviceError"]].Value = string.Empty;
                EMOAppliance[props["LatestConnectInfo"]].Value = string.Empty;

                EMOAppliance[props["LatestStatus"]].Value = Constants.ESightConnectionStatus.NONE;

                ManagementPackClass baseEntity = OM12Connection.GetManagementPackClass("System.Entity");
                EMOAppliance[baseEntity, "DisplayName"].Value = appliance.Host;
                incrementalDiscoveryData.Add(EMOAppliance);

                // add appliance-operation record
                ESightApplianceOperation operation = new ESightApplianceOperation()
                {
                    Id = Guid.NewGuid().ToString("D"),
                    Host = appliance.Host,
                    OperationType = Convert.ToInt32(ESightApplianceOperationType.Add),
                    IsSystemIdChanged = false,
                    CreatedOn = DateTime.UtcNow,
                };
                CreatableEnterpriseManagementObject EMOApplianceOperation =
                    OM12ESightApplianceOperationRepo.GetManagementObject(operation);
                incrementalDiscoveryData.Add(EMOApplianceOperation);

                // commit transiaction
                incrementalDiscoveryData.Commit(OM12Connection.HuaweiESightMG);

                return Result.Done();
            }
            catch (Exception e)
            {
                return Result.Failed(100, $"Internal error caused by {e.Message}", e);
            }
        }


        public static Result Update(ESightAppliance appliance)
        {
            try
            {
                Result findByHostResult = FindByHost(appliance.Host);
                if (!findByHostResult.Success)
                {
                    return findByHostResult;
                }
                else
                {
                    List<EnterpriseManagementObject> filteredList
                        = findByHostResult.Data as List<EnterpriseManagementObject>;
                    if (filteredList.Count != 1)
                    {
                        return Result.Failed(101, $"ESight {appliance.Host} has been deleted.");
                    }
                }


                IncrementalDiscoveryData incrementalDiscoveryData = new IncrementalDiscoveryData();

                // update appliance record
                ManagementPackClass MPClass = GetMPClass();
                List<EnterpriseManagementObject> objects = findByHostResult.Data as List<EnterpriseManagementObject>;
                EnterpriseManagementObject managementObject = objects[0];
                var props = OM12Connection.GetManagementPackProperties(managementObject);
                managementObject[props["Port"]].Value = appliance.Port;
                managementObject[props["AliasName"]].Value = appliance.AliasName;

                // update system id if neccessary
                string currentSystemId = managementObject[props["SystemId"]].Value as string;
                bool IsSystemIdChanged = currentSystemId != appliance.SystemId;
                if (IsSystemIdChanged)
                {
                    managementObject[props["SystemId"]].Value = appliance.SystemId;
                    managementObject[props["SubscribeID"]].Value = Guid.NewGuid().ToString("D");
                    managementObject[props["SubKeepAliveStatus"]].Value = 0;
                    managementObject[props["SubscriptionAlarmStatus"]].Value = 0;
                    managementObject[props["SubscriptionNeDeviceStatus"]].Value = 0;
                    managementObject[props["SubKeepAliveError"]].Value = string.Empty;
                    managementObject[props["SubscripeAlarmError"]].Value = string.Empty;
                    managementObject[props["SubscripeNeDeviceError"]].Value = string.Empty;
                }

                // update credential if neccessary
                if (appliance.UpdateCredential)
                {
                    managementObject[props["LoginAccount"]].Value = appliance.LoginAccount;
                    managementObject[props["LoginPassword"]].Value = RijndaelManagedCrypto.Instance
                        .EncryptForCS(appliance.LoginPassword);
                }
                managementObject[props["LastModifiedOn"]].Value = DateTime.UtcNow;
                incrementalDiscoveryData.Add(managementObject);

                // add appliance-operation record
                ESightApplianceOperation operation = new ESightApplianceOperation()
                {
                    Id = Guid.NewGuid().ToString("D"),
                    Host = appliance.Host,
                    OperationType = Convert.ToInt32(ESightApplianceOperationType.Update),
                    IsSystemIdChanged = IsSystemIdChanged,
                    OldSystemId = currentSystemId,
                    CreatedOn = DateTime.UtcNow,
                };
                CreatableEnterpriseManagementObject EMOApplianceOperation =
                    OM12ESightApplianceOperationRepo.GetManagementObject(operation);
                incrementalDiscoveryData.Add(EMOApplianceOperation);

                // submit transiaction
                incrementalDiscoveryData.Overwrite(OM12Connection.HuaweiESightMG);

                return Result.Done();
            }
            catch (Exception e)
            {
                return Result.Failed(100, $"Internal error caused by {e.Message}", e);
            }
        }

        public static Result Delete(ESightAppliance appliance)
        {
            IncrementalDiscoveryData incrementalDiscoveryData = new IncrementalDiscoveryData();
            try
            {
                bool found = false;
                Result getAllItemsResult = All();
                if (!getAllItemsResult.Success)
                {
                    return getAllItemsResult;
                }

                List<EnterpriseManagementObject> objects = getAllItemsResult.Data as List<EnterpriseManagementObject>;
                foreach (EnterpriseManagementObject monitoringObject in objects)
                {
                    if (monitoringObject.DisplayName == appliance.Host)
                    {
                        found = true;
                        //EnterpriseManagementObject @object = OM12Connection.HuaweiESightMG.EntityObjects.GetObject<EnterpriseManagementObject>(
                        //    monitoringObject.Id, ObjectQueryOptions.Default);
                        incrementalDiscoveryData.Remove(monitoringObject);
                    }
                }

                if (!found)
                {
                    return Result.Failed(104, $"{appliance.Host} does not exists, delete failed.");
                }

                // add appliance-operation record
                ESightApplianceOperation operation = new ESightApplianceOperation()
                {
                    Id = Guid.NewGuid().ToString("D"),
                    Host = appliance.Host,
                    OperationType = Convert.ToInt32(ESightApplianceOperationType.Delete),
                    IsSystemIdChanged = false,
                    CreatedOn = DateTime.UtcNow,
                };
                CreatableEnterpriseManagementObject EMOApplianceOperation =
                    OM12ESightApplianceOperationRepo.GetManagementObject(operation);
                incrementalDiscoveryData.Add(EMOApplianceOperation);

                incrementalDiscoveryData.Commit(OM12Connection.HuaweiESightMG);
                return Result.Done();
            }
            catch (Exception e)
            {
                return Result.Failed(100, $"Internal error caused by {e.Message}", e);
            }
        }
    }
}
