using CommonUtil;
using Huawei.SCOM.ESightPlugin.LogUtil;
using Huawei.SCOM.ESightPlugin.RESTeSightLib.Helper;
using Huawei.SCOM.ESightPlugin.RESTeSightLib;
using Huawei.SCOM.ESightPlugin.ViewLib.Model;
using Huawei.SCOM.ESightPlugin.ViewLib.OM12R2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using static Huawei.SCOM.ESightPlugin.ViewLib.Model.Constants;
using Result = Huawei.SCOM.ESightPlugin.ViewLib.Model.Result;
using Huawei.SCOM.ESightPlugin.Models;
using Huawei.SCOM.ESightPlugin.WebServer.Helper;
using Huawei.SCOM.ESightPlugin.ViewLib.Utils;
using Microsoft.EnterpriseManagement.Common;
using Microsoft.EnterpriseManagement.ConnectorFramework;

namespace Huawei.SCOM.ESightPlugin.WebServer.Scheduler
{
    public class ESightApplianceOperationConsumer
    {

        private static readonly object SyncObject = new object();
        private static ESightApplianceOperationConsumer _instance = null;

        public static ESightApplianceOperationConsumer Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (SyncObject)
                    {
                        if (_instance == null)
                        {
                            _instance = new ESightApplianceOperationConsumer();
                        }
                    }
                }
                return _instance;
            }
        }



        public void Consume()
        {
            HWLogger.UI.Info($"Start to consume appliance operation event");
            IObjectReader<EnterpriseManagementObject> objects =
                   OM12Connection.All(ESightApplianceOperation.EntityClassName);

            // it should be in order by default, Reorder it just in case.
            List<EnterpriseManagementObject> sortedApplianceOperationList
                                         = objects.OrderBy(item => item.LastModified).ToList();
            HWLogger.UI.Info($"Detect {sortedApplianceOperationList.Count} consume events");
            sortedApplianceOperationList.ForEach(obj =>
            {
                var props = OM12Connection.GetManagementPackProperties(obj);
                var Host = obj[props["Host"]].Value as string;
                var OperationType = (ESightApplianceOperationType)obj[props["OperationType"]].Value;
                var IsSystemIdChanged = Convert.ToBoolean(obj[props["IsSystemIdChanged"]].Value);
                var OldSystemId = obj[props["OldSystemId"]].Value as string;
                var CreatedOn = Convert.ToDateTime(obj[props["CreatedOn"]].Value);

                try
                {
                    switch (OperationType)
                    {
                        case ESightApplianceOperationType.Add:
                            HandleAddESightOperation(Host, CreatedOn);
                            break;
                        case ESightApplianceOperationType.Update:
                            HandleUpdateESightOperation(Host, IsSystemIdChanged, OldSystemId, CreatedOn);
                            break;
                        case ESightApplianceOperationType.Delete:
                            HandleDeleteESightOperation(Host, CreatedOn);
                            break;
                    }

                    // remove event.
                    HWLogger.UI.Info($"Delete processed appliance-operation record now.");
                    IncrementalDiscoveryData incrementalDiscoveryData = new IncrementalDiscoveryData();
                    incrementalDiscoveryData.Remove(obj);
                    incrementalDiscoveryData.Commit(OM12Connection.HuaweiESightMG);
                    HWLogger.UI.Info($"Delete processed appliance-operation record succeed.");
                }
                catch
                {
                    // handled, ignore here
                }
            });
        }

        private static void HandleDeleteESightOperation(string Host, DateTime CreatedOn)
        {
            try
            {
                HWLogger.UI.Info($"Handle delete eSight '{Host}' event created on {CreatedOn.ToLocalTime()}.");
                HWESightHost eSight = ESightDal.Instance.GetEntityByHostIp(Host);
                if (eSight == null)
                {
                    throw new Exception($"Can not find eSight: {Host}");
                }

                // 取消订阅（不再修改eSight的订阅状态）
                UnSubscribeESight(Host, eSight.SystemID, true);

                // 告诉服务（删除scom中的服务器）
                Task.Run(() =>
                {
                    var message = new TcpMessage<string>(Host, TcpMessageType.DeleteESight, "delete a ESight");
                    NotifyClient.Instance.SendMsg(message);
                });
            }
            catch (Exception e)
            {
                HWLogger.UI.Error("Failed to handle delete eSight event", e);
                throw;
            }
        }

        private static void HandleUpdateESightOperation(string Host, bool IsSystemIdChanged, string OldSystemId, DateTime CreatedOn)
        {
            try
            {
                HWLogger.UI.Info($"Handle update eSight '{Host}' event created on {CreatedOn.ToLocalTime()}.");
                HWESightHost model = ESightDal.Instance.GetEntityByHostIp(Host);
                var json = JsonUtil.SerializeObject(model);
                HWLogger.UI.Info("Updated eSight properties: [{0}]", json);

                ESightEngine.Instance.SaveSession(model);
                if (IsSystemIdChanged)
                {
                    // 修改了systemId，在session保存成功后重新订阅
                    UnSubscribeESight(Host, OldSystemId);
                }

                // 告诉服务
                Task.Run(() =>
                {
                    var message = new TcpMessage<string>(Host, TcpMessageType.SyncESight, "Update ESight");
                    NotifyClient.Instance.SendMsg(message);
                });

                HWLogger.UI.Info("Update eSight event process succeed.");
            }
            catch (Exception e)
            {
                HWLogger.UI.Error("Failed to handle update eSight event", e);
                throw;
            }
        }

        private static void HandleAddESightOperation(string Host, DateTime CreatedOn)
        {
            try
            {
                HWLogger.UI.Info($"Handle add eSight '{Host}' event created on {CreatedOn.ToLocalTime()}");
                HWESightHost model = ESightDal.Instance.GetEntityByHostIp(Host);
                var json = JsonUtil.SerializeObject(model);
                HWLogger.UI.Info("New created eSight properties: [{0}]", model);
                ESightEngine.Instance.SaveSession(model);
                // 告诉服务
                Task.Run(() =>
                {
                    var message = new TcpMessage<string>(model.HostIP, TcpMessageType.SyncESight, "add new ESight");
                    NotifyClient.Instance.SendMsg(message);
                });
                HWLogger.UI.Info("Add eSight event process succeed.");
            }
            catch (Exception e)
            {
                HWLogger.UI.Error("Failed to handle add eSight event", e);
                throw;
            }
        }

        /// <summary>
        /// 取消订阅eSight.(需要用旧的systemId取消订阅)
        /// </summary>
        /// <param name="hostIp">The host ip.</param>
        /// <param name="oldSystemId">The system identifier.</param>
        /// <param name="isDeleteSession">The is delete session.</param>
        private static void UnSubscribeESight(string hostIp, string oldSystemId, bool isDeleteSession = false)
        {
            Task.Run(() =>
            {
                try
                {
                    // 取消订阅
                    var iEsSession = ESightEngine.Instance.FindEsSession(hostIp);

                    var resut = iEsSession.UnSubscribeKeepAlive(oldSystemId);
                    HWLogger.UI.Info($"UnSubscribeKeepAlive.eSight:{hostIp} result:{JsonUtil.SerializeObject(resut)}");
                    resut = iEsSession.UnSubscribeAlarm(oldSystemId);
                    HWLogger.UI.Info($"UnSubscribeAlarm. eSight:{hostIp} result:{JsonUtil.SerializeObject(resut)}");
                    resut = iEsSession.UnSubscribeNeDevice(oldSystemId);
                    HWLogger.UI.Info($"UnSubscribeNeDevice. eSight:{hostIp} result:{JsonUtil.SerializeObject(resut)}");
                }
                catch (Exception ex)
                {
                    HWLogger.UI.Error("UnSubscribeWhenUpdateSystemId Error", ex);
                }
                finally
                {
                    if (isDeleteSession)
                    {
                        // 从缓存中删除
                        ESightEngine.Instance.RemoveEsSession(hostIp);
                    }
                }
            });
        }
    }
}