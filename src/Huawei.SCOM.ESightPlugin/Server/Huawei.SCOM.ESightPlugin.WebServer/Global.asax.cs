using Huawei.SCOM.ESightPlugin.WebServer.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Web;
using System.Web.Security;
using System.Web.SessionState;
using Huawei.SCOM.ESightPlugin.LogUtil;

namespace Huawei.SCOM.ESightPlugin.WebServer
{
    using Huawei.SCOM.ESightPlugin.RESTeSightLib.Helper;
    using Huawei.SCOM.ESightPlugin.WebServer.Scheduler;
    using System.Web.Caching;
    using System.Web.Hosting;

    public class Global : System.Web.HttpApplication
    {
        private const string ApplianceOperationConsumerTriggerItem = "TriggerApplianceOperationConsumer";

        protected void Application_Start(object sender, EventArgs e)
        {
            NotifyClient.Instance.Init();

            // use http cache strategy to trigger consumer task
            TriggerApplianceOpeationConsumerTask(60);
        }

        private void TriggerApplianceOpeationConsumerTask(int expireInSeconds)
        {
            var OnCacheRemove = new CacheItemRemovedCallback(OnCacheItemRemoved);
            HttpRuntime.Cache.Insert(ApplianceOperationConsumerTriggerItem, expireInSeconds, null,
                DateTime.Now.AddSeconds(expireInSeconds), Cache.NoSlidingExpiration,
                CacheItemPriority.NotRemovable, OnCacheRemove);
        }

        public void OnCacheItemRemoved(string itemName, object v, CacheItemRemovedReason r)
        {
            if (itemName.Equals(ApplianceOperationConsumerTriggerItem))
            {
                try
                {
                    ESightApplianceOperationConsumer.Instance.Consume();
                }
                finally
                {
                    // re-trigger next consume task
                    TriggerApplianceOpeationConsumerTask(60);
                }
            }
        }

        protected void Session_Start(object sender, EventArgs e)
        {

        }

        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            RedirectNotificationURL();
        }

        protected void Application_AuthenticateRequest(object sender, EventArgs e)
        {

        }

        protected void Application_Error(object sender, EventArgs e)
        {

        }

        protected void Session_End(object sender, EventArgs e)
        {

        }

        protected void Application_End(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// 重定向URL，转义subscribeID
        /// </summary>
        protected void RedirectNotificationURL()
        {
            try
            {
                var requestUrl = System.Web.HttpContext.Current.Request.Url.ToString();
                if (requestUrl.Contains("AlarmNotification/"))
                {
                    var newUrl = "/AlarmNotification.ashx?subscribeID=" + requestUrl.Substring(requestUrl.LastIndexOf('/') + 1);
                    HttpContext.Current.Server.TransferRequest(newUrl, true);
                }
                else if (requestUrl.Contains("NeDeviceNotification/"))
                {
                    var newUrl = "/NeDeviceNotification.ashx?subscribeID=" + requestUrl.Substring(requestUrl.LastIndexOf('/') + 1);
                    HttpContext.Current.Server.TransferRequest(newUrl, true);
                }
                else if (requestUrl.Contains("SystemKeepAlive/"))
                {
                    var newUrl = "/SystemKeepAlive.ashx?subscribeID=" + requestUrl.Substring(requestUrl.LastIndexOf('/') + 1);
                    HttpContext.Current.Server.TransferRequest(newUrl, true);
                }
            }
            catch (Exception ex)
            {
                HWLogger.NotifyRecv.Error($"RedirectNotificationURL ERROR:", ex);
            }

        }
    }
}