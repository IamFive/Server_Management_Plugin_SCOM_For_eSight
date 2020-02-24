//**************************************************************************  
//Copyright (C) 2019 Huawei Technologies Co., Ltd. All rights reserved.
//This program is free software; you can redistribute it and/or modify
//it under the terms of the MIT license.

//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//MIT license for more detail.
//*************************************************************************  
using CommonUtil;
using Huawei.SCOM.ESightPlugin.Core;
using Huawei.SCOM.ESightPlugin.Core.Models;
using Huawei.SCOM.ESightPlugin.Models;
using Microsoft.EnterpriseManagement.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using static Huawei.SCOM.ESightPlugin.Const.ConstMgr.ESightEventeLogSource;
using System.Threading.Tasks;
using Microsoft.EnterpriseManagement.Monitoring;
using Huawei.SCOM.ESightPlugin.Models.Server;
using System.Collections.ObjectModel;
using static Huawei.SCOM.ESightPlugin.Const.ConstMgr;

namespace Huawei.SCOM.ESightPlugin.Service
{
    public partial class ESightSyncInstance
    {
        /// <summary>
        /// 
        /// </summary>
        public static EventLogEntryType[] ShouldProcessedAlarmLevels = new EventLogEntryType[] { EventLogEntryType.Error, EventLogEntryType.Warning };

        /// <summary>
        /// Gets or sets the alarm datas.
        /// </summary>
        /// <value>The alarm datas.</value>
        private Queue<AlarmData> alarmQueue { get; set; }

        /// <summary>
        /// synchronize lock object for "Alarm" Queue
        /// </summary>
        private readonly object locker = new object();

        /// <summary>
        /// processor working thread
        /// </summary>
        private Thread alarmProcessor;

        /// <summary>
        /// The new alarm received handle
        /// </summary>
        private readonly AutoResetEvent receiveAlarmEvent = new AutoResetEvent(false);


        #region 启用插入事件的任务
        /// <summary>
        ///启用插入事件的任务
        /// </summary>
        /// <returns>Task.</returns>
        private void StartAlarmEventProcessor()
        {
            if (alarmProcessor == null)
            {
                alarmProcessor = new Thread(delegate ()
                {
                    while (this.IsRunning) // TODO(turnbig) 假如Queue里面还有未处理的数据，直接丢弃？
                    {
                        logger.Polling.Info($"Current Alarm Processing Queue amount: {alarmQueue.Count}.");

                        if (alarmQueue.Count > 0 || receiveAlarmEvent.WaitOne())
                        {
                            AlarmData alarm = null;
                            lock (this.locker)
                            {
                                if (alarmQueue.Count > 0)
                                {
                                    alarm = alarmQueue.Dequeue();
                                }
                            }

                            if (alarm != null)
                            {
                                EventData eventObject = new EventData(alarm, this.ESightIp);
                                logger.Polling.Info($"[{alarm.AlarmSN}] Start processing new alarm: Cleared: {alarm.Cleared}, OptType: {alarm.OptType}, " +
                                                    $"EventType: {alarm.EventType}, EventLevel: {eventObject.LevelId.ToString()}");


                                // should we catagory alarm with server type ?
                                var deviceId = eventObject.DeviceId;
                                MonitoringDeviceObject monitoringObject = this.GetDeviceByDeveceId(deviceId);
                                if (monitoringObject == null)
                                {
                                    logger.Polling.Error($"[{alarm.AlarmSN}] No MonitoringObject({deviceId}) exists, alarm will be ignore");
                                    continue;
                                }

                                // waiting for monitoring-object ready.
                                WaitForDeviceMonitored(monitoringObject);

                                if (eventObject.Cleared)
                                {
                                    // Close SCOM alert
                                    CloseSCOMAlert(eventObject, monitoringObject);
                                }
                                else if (ShouldProcessedAlarmLevels.Contains(eventObject.LevelId))
                                {
                                    // Create New EventLog for new alarms, and generate SCOM alert through associated rule
                                    CreateNewEventLogForAlarm(eventObject);
                                }
                                else if (eventObject.OptType == 6)
                                {
                                    // Create New EventLog for new events, and generate SCOM event through associated rule
                                    CreateNewEventLogForEvent(eventObject);
                                }

                            }
                        }
                    }
                });
            }

            this.alarmProcessor.Start();
            logger.Polling.Info("Alarm processor starts successfully.");

        }

        private void CreateNewEventLogForEvent(EventData eventObject)
        {
            var alarm = eventObject.AlarmData;
            logger.Polling.Info($"[{alarm.AlarmSN}] Persist event to window EventLog now.");

            // Create new event log instance
            EventInstance instance = new EventInstance(int.Parse(eventObject.EventId), 0, eventObject.LevelId);

            object[] values = new object[] {
                eventObject.AlarmData.AlarmName,  // channel
                eventObject.DeviceId,             // SCOM monitor object DN
                eventObject.AlarmSn.ToString(),
            };

            EventLog.WriteEvent(EVENT_SOURCE, instance, values);
            this.logger.Polling.Info($"[{alarm.AlarmSN}] Persist event to window EventLog successfully.");
        }


        /// <summary>
        /// Create New EventLog according to the esight alarm
        /// </summary>
        /// <param name="eventObject"></param>
        private void CreateNewEventLogForAlarm(EventData eventObject)
        {
            var alarm = eventObject.AlarmData;
            logger.Polling.Info($"[{alarm.AlarmSN}] Persist alarm to window EventLog now.");

            /** 
                we do not care about whether device's health status is "not monitoring",
                we can just insert alarm to window EventLog even it may be droped.
             */

            // Create new event log instance
            EventInstance instance = new EventInstance(int.Parse(eventObject.EventId), 0, eventObject.LevelId);
            // EventInstance instance = new EventInstance(eventObject.AlarmSn, 0, eventObject.LevelId);
            object[] values = new object[] {
                eventObject.Description,
                eventObject.AlarmData.AlarmName,  // channel
                eventObject.DeviceId,             // SCOM monitor object DN
                eventObject.AlarmData.NeDN,
                eventObject.AlarmData.NeType,
                eventObject.AlarmData.AlarmId,
                eventObject.AlarmSn.ToString(),
                eventObject.Priority,
                eventObject.Severity,
                TimeHelper.StampToDateTime(eventObject.AlarmData.EventTime.ToString()).ToString(),  // 10
                eventObject.AlarmData.ObjectInstance,
                eventObject.AlarmData.ProposedRepairActions,
                eventObject.AlarmData.PerceivedSeverity,
                this.Session.ESight.SubscribeID,            // 14
                eventObject.AlarmData.ProbableCauseStr,
            };

            /**
            <Custom1>$Data/Params/Param[2]$</Custom1>
            <Custom2>$Data/Params/Param[3]$</Custom2>
            <Custom3>$Data/Params/Param[4]$</Custom3>
            <Custom4>$Data/Params/Param[5]$</Custom4>
            <Custom5>$Data/Params/Param[6]$</Custom5>
            <Custom6>$Data/Params/Param[7]$</Custom6>
            <Custom7>$Data/Params/Param[10]$</Custom7>
            <Custom8>$Data/Params/Param[11]$</Custom8>
            <Custom9>$Data/Params/Param[13]$</Custom9>
            <Custom10>$Data/Params/Param[14]$</Custom10>*/

            EventLog.WriteEvent(EVENT_SOURCE, instance, values);
            this.logger.Polling.Info($"[{alarm.AlarmSN}] Persist alarm to window EventLog successfully.");
        }
        #endregion


        /// <summary>
        /// Close SCOM Alert associated with the Alarm
        /// </summary>
        /// <param name="eventObject"></param>
        /// <param name="monitoringObject"></param>
        private void CloseSCOMAlert(EventData eventObject, MonitoringDeviceObject monitoringObject)
        {
            var alarm = eventObject.AlarmData;
            logger.Polling.Info($"[{alarm.AlarmSN}] ESight alarm is cleared, close SCOM alert now.");

            /**
            <Suppression>
              <SuppressionValue>$Data/Params/Param[3]$</SuppressionValue>
              <SuppressionValue>$Data/Params/Param[6]$</SuppressionValue>
              <SuppressionValue>$Data/Params/Param[11]$</SuppressionValue>
            </Suppression>
            */
            // We will identify the alert using suppression rule.
            // Already closed alerts should be ignored.
            var criteria = $"ResolutionState != '255' and CustomField5 = '{eventObject.AlarmData.AlarmId}' " +
                            $"and CustomField8 = '{eventObject.AlarmData.ObjectInstance}'";
            ReadOnlyCollection<MonitoringAlert> alerts = monitoringObject.Device.GetMonitoringAlerts(new MonitoringAlertCriteria(criteria));
            if (alerts.Count == 0)
            {
                logger.Polling.Warn($"[{alarm.AlarmSN}] No un-closed SCOM alert is associated with current alarm.");
            }
            else
            {
                // It should be 1 in normal sutiation.
                logger.Polling.Info($"[{alarm.AlarmSN}] Associated SCOM alerts count is {alerts.Count}.");
            }

            foreach (MonitoringAlert alert in alerts)
            {
                alert.ResolutionState = BladeConnector.Instance.CloseState.ResolutionState;
                var reason = AlarmClearType.GetClearReason(eventObject.AlarmData.ClearedType);
                alert.Update($"Close by ESight: {reason}.");
                logger.Polling.Info($"[{alarm.AlarmSN}] Close SCOM alert successfully.");
            }
        }


        /// <summary>
        /// Waiting for device object monitored.
        /// </summary>
        /// <param name="obj"></param>
        public void WaitForDeviceMonitored(MonitoringDeviceObject obj)
        {
            // When an object is first created, it's status is "not monitoring". 
            // The status will changed when Monitor run with a configed interval.
            MonitoringObject device = obj.Device;
            if (device.StateLastModified == null || device.HealthState == HealthState.Uninitialized)
            {
                this.logger.Polling.Info($"MonitoringObject({obj.DeviceId}) is not monitoring.");
                this.logger.Polling.Info($"     Device added time is: {device.TimeAdded}.");
                this.logger.Polling.Info($"     Device last modified time is: {device.LastModified}.");
                this.logger.Polling.Info($"     Device state last modified time is: {device.StateLastModified}.");

                // Do not know why RecalculateMonitoringState will stop Service, So, we just wait the monitor run automate.
                // obj.Device.RecalculateMonitoringState();
                var stateLastModified = device.StateLastModified.HasValue ? device.StateLastModified.Value : device.LastModified;
                TimeSpan stateNotChangedTimeLong = DateTime.UtcNow - stateLastModified;
                // the interval of monitor for our object is 5 minutes. So we will wait 5m.
                TimeSpan expectTimeLong = TimeSpan.FromMinutes(5);
                if (stateNotChangedTimeLong <= expectTimeLong)
                {
                    Thread.Sleep(expectTimeLong - stateNotChangedTimeLong);
                }

                obj.Reload();
                logger.Polling.Info($"Current health state for device `{obj.DeviceId}` is {obj.Device.HealthState}");
            }

            if (!device.IsAvailable)
            {
                this.logger.Polling.Info($"MonitoringObject({obj.DeviceId}) is not avaiable.");
                this.logger.Polling.Info($"     Device added time is: {device.TimeAdded}.");
                this.logger.Polling.Info($"     Device last modified time is: {device.LastModified}.");
                this.logger.Polling.Info($"     Device availability last modified time is: {device.AvailabilityLastModified}.");

                var availableLastModified = device.AvailabilityLastModified.HasValue ? device.AvailabilityLastModified.Value : device.LastModified;
                TimeSpan availableNotChangedTimeLong = DateTime.UtcNow - availableLastModified;
                // the interval of monitor for our object is 5 minutes. So we will wait 5m.
                TimeSpan expectTimeLong = TimeSpan.FromMinutes(5);
                if (availableNotChangedTimeLong <= expectTimeLong)
                {
                    Thread.Sleep(expectTimeLong - availableNotChangedTimeLong);
                }

                obj.Reload();
                logger.Polling.Info($"Current health state for device `{obj.DeviceId}` is {obj.Device.HealthState}");
            }
        }

        /// <summary>
        /// Synchronizes open alarms from esight.
        /// </summary>
        public void SyncESightOpenAlarms()
        {
            // TODO(turnbig) 是否需要过滤 EventType 2， 是否需要过滤级别
            logger.Polling.Info($"Start sync open alarms for eSight `{ESightIp}`.");

            // We need to get all open alerts here, because we need to compare and close them later.
            // Get all not closed SCOM alerts
            var criteria = $"ResolutionState != '255' and CustomField10 = '{Session.ESight.SubscribeID}'";
            var alerts = MGroup.Instance.OperationalData.GetMonitoringAlerts(new MonitoringAlertCriteria(criteria), null);
            OnPollingInfo($"Unclosed SCOM alerts count of esight(`{ESightIp}`) is: {alerts.Count}");

            int totalPages = 1;
            int currentPage = 1;

            var allOpenAlarms = new List<AlarmHistory>();
            while (currentPage <= totalPages)
            {
                try
                {
                    var result = this.Session.GetOpenAlarms(currentPage);
                    totalPages = result.TotalPage;

                    // TODO(turnbig) should we only process device alarms? what about the notification messages?
                    var deviceAlarms = result.Data.Where(x => x.EventType == 2).ToList();
                    allOpenAlarms.AddRange(deviceAlarms);
                    OnPollingInfo($"Succeed fetching esight open alarms. Pagination: page {currentPage}, count:{result.Data.Count}, device alarms count:{deviceAlarms.Count}.");

                    deviceAlarms.ForEach(item =>
                    {
                        // we does not care about whether the alert exists or not indeed.
                        // if we insert same alert, it will just increase repeat count.
                        // But we still keep the old logics here. :)
                        var alarm = new AlarmData(item);
                        var alert = alerts.FirstOrDefault(GetScomAlertSuppressionPredicator(alarm));

                        bool shouldProcess = false;
                        if (alert != null)
                        {
                            // only alarm warning level will be modified
                            shouldProcess = Convert.ToInt32(alert.CustomField9 ?? "0") != alarm.PerceivedSeverity;
                        }
                        else
                        {
                            lock (this.locker)
                            {
                                // find data from current alarm processing queue
                                shouldProcess = !alarmQueue.Any(_alarm =>
                                {
                                    return _alarm.NeDN.Equals(alarm.NeDN) && _alarm.AlarmId.Equals(alarm.AlarmId)
                                        && _alarm.ObjectInstance.Equals(alarm.ObjectInstance)
                                        && Convert.ToInt32(alert.CustomField9 ?? "0") == alarm.PerceivedSeverity;
                                });
                            }
                        }

                        if (shouldProcess)
                        {
                            OnPollingInfo($"[{alarm.AlarmSN}] Current alarm is not processed, will submit to processing queue now.");
                            // Submit new alarm to update the alert
                            SubmitNewAlarm(alarm);
                        }
                        else
                        {
                            OnPollingInfo($"[{alarm.AlarmSN}] Current alarm exists, ignore.");
                        }

                    });
                }
                catch (Exception ex)
                {
                    OnPollingError($"Failed to sync opened alarms from eSight: {this.ESightIp}. Page number: {currentPage}.", ex);
                }

                currentPage++;
            }

            // TODO(turnbig)
            // 插入历史告警完成后调用订阅接口
            this.Subscribe();


            // find all alerts that is still open in SCOM but not in esight ope
            alerts.ToList().ForEach(alert =>
            {
                bool exists = allOpenAlarms.Any(alarm =>
                {
                    var data = new AlarmData(alarm);
                    return GetScomAlertSuppressionPredicator(data).Invoke(alert);
                });

                if (!exists)
                {
                    alert.ResolutionState = BladeConnector.Instance.CloseState.ResolutionState;
                    alert.Update($"Closed by sync open alarms interval task.");
                    logger.Polling.Info($"[{alert.CustomField6}] Succeed closing SCOM alert when syncing open alarm task.");
                }
            });
        }

        private static Func<MonitoringAlert, bool> GetScomAlertSuppressionPredicator(AlarmData alarm)
        {
            return _alert =>
            {
                return _alert.CustomField3.Equals(alarm.NeDN) && _alert.CustomField5.Equals(alarm.AlarmId.ToString())
                            && _alert.CustomField8.Equals(alarm.ObjectInstance);
            };
        }

        /// <summary>
        /// 对比告警
        /// </summary>
        /// <param name="preData">The pre data.</param>
        /// <param name="nowData">The now data.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        private bool CompareAlarmData(AlarmData preData, AlarmData nowData)
        {
            if (preData.AlarmName != nowData.AlarmName) { return false; }
            if (preData.AlarmId != nowData.AlarmId) { return false; }
            //if (preData.ArrivedTime != nowData.ArrivedTime) { return false; }
            //if (preData.MoDN != nowData.MoDN) { return false; }
            //if (preData.MoName != nowData.MoName) { return false; }
            //if (preData.NeDN != nowData.NeDN) { return false; }
            //if (preData.NeName != nowData.NeName) { return false; }
            //if (preData.NeType != nowData.NeType) { return false; }
            if (preData.PerceivedSeverity != nowData.PerceivedSeverity) { return false; }
            if (preData.ProbableCause != nowData.ProbableCause) { return false; }
            return true;
        }

        /// <summary>
        /// Submit alarm to processing queue
        /// </summary>
        /// <param name="data">The data.</param>
        public void SubmitNewAlarm(AlarmData data)
        {
            /**
             * SCOM will only process alarms with event-type "2"(Device related).
             */
            if (data.EventType == 2)
            {
                logger.Polling.Info($"[{data.AlarmSN}] Submit new eSight alarm to processing queue.");
                lock (locker)
                {
                    alarmQueue.Enqueue(data);
                }

                this.receiveAlarmEvent.Set();
            }
            else
            {
                logger.Polling.Info($"[{data.AlarmSN}] Alarm is ignored, event type is: {data.EventType}.");
            }
        }
    }
}