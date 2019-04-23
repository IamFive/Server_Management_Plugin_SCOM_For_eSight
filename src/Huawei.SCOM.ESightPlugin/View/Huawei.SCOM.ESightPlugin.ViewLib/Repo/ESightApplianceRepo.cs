using Huawei.SCOM.ESightPlugin.ViewLib.Client;
using Huawei.SCOM.ESightPlugin.ViewLib.Model;
using Huawei.SCOM.ESightPlugin.ViewLib.OM12R2;
using Huawei.SCOM.ESightPlugin.ViewLib.Utils;
using Microsoft.EnterpriseManagement.Common;
using Microsoft.EnterpriseManagement.Monitoring;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Result = Huawei.SCOM.ESightPlugin.ViewLib.Model.Result;

namespace Huawei.SCOM.ESightPlugin.ViewLib.Repo
{

    public class ESightApplianceRepo : INotifyPropertyChanged
    {
        private const string PatternDigits = @"^[a-zA-Z0-9-_\.]{1,100}$";

        #region Private Members
        private string keyword;
        ObservableCollection<ESightAppliance> items = new ObservableCollection<ESightAppliance>();
        ObservableCollection<ESightAppliance> filteredItems = new ObservableCollection<ESightAppliance>();
        #endregion //Private Members

        #region Public Members
        public ObservableCollection<ESightAppliance> Items { get => items; set => items = value; }
        public ObservableCollection<ESightAppliance> FilteredItems { get => filteredItems; set => filteredItems = value; }
        #endregion //Public Members

        #region Constructors
        public ESightApplianceRepo()
        {

        }
        #endregion //Constructor

        #region Load Appliance List
        public void LoadAll(Action<Result> callback)
        {
#if !DEBUG

            BackgroundWorker backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += delegate (object sender, DoWorkEventArgs e)
            {
                e.Result = OM12ESightApplianceRepo.All();
            };

            backgroundWorker.RunWorkerCompleted += delegate (object sender, RunWorkerCompletedEventArgs e)
            {
                Result getAllItemResult = e.Result as Result;
                if (!getAllItemResult.Success)
                {
                    callback(getAllItemResult);
                }

                List<EnterpriseManagementObject> monitoringObjects = getAllItemResult.Data as List<EnterpriseManagementObject>;
                IEnumerable<ESightAppliance> appliances = monitoringObjects.Select(obj =>
                {
                    //TODO using OM12 property binding data instead? 
                    var props = OM12Connection.GetManagementPackProperties(obj);
                    ESightAppliance appliance = new ESightAppliance
                    {
                        Host = obj[props["Host"]].Value as String,
                        Port = obj[props["Port"]].Value.ToString(),
                        AliasName = obj[props["AliasName"]].Value as String,
                        SystemId = obj[props["SystemId"]].Value as String,
                        LoginAccount = obj[props["LoginAccount"]].Value as String,
                        LoginPassword = obj[props["LoginPassword"]].Value as String,
                        CreatedOn = ((DateTime)obj[props["CreatedOn"]].Value).ToLocalTime(),
                        LastModifiedOn = ((DateTime)obj[props["LastModifiedOn"]].Value).ToLocalTime(),
                        SubscriptionAlarmStatus = Convert.ToInt32(obj[props["SubscriptionAlarmStatus"]].Value),
                        SubscriptionNeDeviceStatus = Convert.ToInt32(obj[props["SubscriptionNeDeviceStatus"]].Value),
                        LatestStatus = obj[props["LatestStatus"]].Value as string,
                    };
                    return appliance;
                });

                // update local data
                this.Items.Clear();
                appliances?.ToList().ForEach(item =>
                {
                    this.Items.Add(item);
                });
                this.Filter(null);

                callback(Result.Done(appliances));
            };
            backgroundWorker.RunWorkerAsync();

#else
            callback(Result.Done());
#endif
        }
        #endregion // Load Appliance List

        #region Filter 
        public void Filter(String keyword)
        {
            this.keyword = keyword;
            this.Filter();
        }

        private void Filter()
        {
            FilteredItems.Clear();
            foreach (var item in Items)
            {
                if (this.keyword != null && this.keyword.Trim() != "")
                {
                    if (item.Host.Contains(this.keyword) || item.AliasName.Contains(this.keyword))
                    {
                        FilteredItems.Add(item);
                    }
                }
                else
                {
                    FilteredItems.Add(item);
                }
            }

            OnPropertyChanged("FilteredItems");
        }
        #endregion //Filter 

        #region Test Appliance 
        internal void Test(ESightAppliance appliance, Action<Result> callback)
        {
            Result validateResult = Validate(appliance);
            if (!validateResult.Success)
            {
                callback(validateResult);
                return;
            }

            BackgroundWorker backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += delegate (object sender, DoWorkEventArgs e)
            {
                if (string.IsNullOrEmpty(appliance.LoginPassword))
                {
                    Result findByHostResult = OM12ESightApplianceRepo.FindByHost(appliance.Host);
                    if (!findByHostResult.Success)
                    {
                        e.Result = findByHostResult;
                        return;
                    }
                    else
                    {
                        List<EnterpriseManagementObject> filteredList
                            = findByHostResult.Data as List<EnterpriseManagementObject>;
                        if (filteredList.Count != 1)
                        {
                            e.Result = Result.Failed(101, $"ESight {appliance.Host} has been deleted.");
                            return;
                        }

                        EnterpriseManagementObject managementObject = filteredList[0];
                        var props = OM12Connection.GetManagementPackProperties(managementObject);
                        appliance.LoginPassword = RijndaelManagedCrypto.Instance
                            .DecryptFromCS(managementObject[props["LoginPassword"]].Value as string);
                    }
                }

                using (var Client = new ESightClient(appliance))
                {
                    e.Result = Client.TestCredential();
                }
            };

            backgroundWorker.RunWorkerCompleted += delegate (object sender, RunWorkerCompletedEventArgs e)
            {
                callback(e.Result as Result);
            };
            backgroundWorker.RunWorkerAsync();
        }

        #endregion //Test Appliance 

        #region Add Appliance 
        internal void Add(ESightAppliance appliance, Action<Result> callback)
        {
            Result validateResult = Validate(appliance);
            if (!validateResult.Success)
            {
                callback(validateResult);
                return;
            }


            BackgroundWorker backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += delegate (object sender, DoWorkEventArgs e)
            {
                using (var Client = new ESightClient(appliance))
                {
                    Result result = Client.TestCredential();
                    if (!result.Success)
                    {
                        e.Result = result;
                    }
                    else
                    {
                        try
                        {
#if !DEBUG
                            e.Result = OM12ESightApplianceRepo.Add(appliance);
#else
                            e.Result = Result.Done();
#endif
                        }
                        catch (ApplicationException ex)
                        {
                            e.Result = Result.Failed(101, "Failed to add esight appliance to OM database.", ex);
                        }
                    }
                }
            };

            backgroundWorker.RunWorkerCompleted += delegate (object sender, RunWorkerCompletedEventArgs e)
            {
                Result result = e.Result as Result;
                if (result.Success)
                {
                    this.LoadAll(callback);
                }

                // callback handler of add esight dialog window
                callback(result);
            };
            backgroundWorker.RunWorkerAsync();
        }

        #endregion //Add Appliance 

        #region Update Appliance 
        internal void Update(ESightAppliance appliance, Action<Result> callback)
        {
            Result validateResult = Validate(appliance);
            if (!validateResult.Success)
            {
                callback(validateResult);
                return;
            }


            BackgroundWorker backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += delegate (object sender, DoWorkEventArgs e)
            {
                if (string.IsNullOrEmpty(appliance.LoginPassword))
                {
                    Result findByHostResult = OM12ESightApplianceRepo.FindByHost(appliance.Host);
                    if (!findByHostResult.Success)
                    {
                        e.Result = findByHostResult;
                        return;
                    }
                    else
                    {
                        List<EnterpriseManagementObject> filteredList
                            = findByHostResult.Data as List<EnterpriseManagementObject>;
                        if (filteredList.Count != 1)
                        {
                            e.Result = Result.Failed(101, $"ESight {appliance.Host} has been deleted.");
                            return;
                        }

                        EnterpriseManagementObject managementObject = filteredList[0];
                        var props = OM12Connection.GetManagementPackProperties(managementObject);
                        appliance.LoginPassword = RijndaelManagedCrypto.Instance
                            .DecryptFromCS(managementObject[props["LoginPassword"]].Value as string);
                    }
                }

                using (var Client = new ESightClient(appliance))
                {
                    Result result = Client.TestCredential();
                    if (!result.Success)
                    {
                        e.Result = result;
                    }
                    else
                    {
                        try
                        {
#if !DEBUG
                            e.Result = OM12ESightApplianceRepo.Update(appliance);
#else
                            e.Result = Result.Done();
#endif
                        }
                        catch (ApplicationException ex)
                        {
                            e.Result = Result.Failed(101, "Failed to update esight appliance to OM database.", ex);
                        }
                    }
                }
            };

            backgroundWorker.RunWorkerCompleted += delegate (object sender, RunWorkerCompletedEventArgs e)
            {
                Result result = e.Result as Result;
                if (result.Success)
                {
                    //this.Items.Remove(this.Items.Single(i => i.Equals(appliance)));
                    //this.Items.Insert(0, appliance);
                    //this.Filter();
                    this.LoadAll(callback);
                }

                // callback handler of add esight dialog window
                callback(result);
            };
            backgroundWorker.RunWorkerAsync();
        }

        #endregion //Update Appliance 

        #region Delete Appliance 
        public void Delete(ESightAppliance appliance, Action<Result> callback)
        {
            BackgroundWorker backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += delegate (object sender, DoWorkEventArgs e)
            {
                try
                {
#if !DEBUG
                    e.Result = OM12ESightApplianceRepo.Delete(appliance);
#else
                    e.Result = Result.Done();
#endif
                }
                catch (ApplicationException ex)
                {
                    e.Result = Result.Failed(102, "Failed to delete esight appliance from OM database.", ex);
                }
            };

            backgroundWorker.RunWorkerCompleted += delegate (object sender, RunWorkerCompletedEventArgs e)
            {
                Result result = e.Result as Result;
                if (result.Success)
                {
                    if (Items.Contains(appliance))
                    {
                        items.Remove(appliance);
                    }

                    this.Filter();
                }

                // callback handler of add esight dialog window
                callback(result);
            };
            backgroundWorker.RunWorkerAsync();
        }
        #endregion //Delete Appliance 

        #region Validate Appliance 
        public Result Validate(ESightAppliance appliance)
        {

            // host 
            if (string.IsNullOrEmpty(appliance.Host))
            {
                return Result.Failed(1000, "Host should not be null or empty.");
            }


            bool isValidIPAddr = IPAddress.TryParse(appliance.Host, out IPAddress Address);
            bool isValidDomain = Uri.CheckHostName(appliance.Host) != UriHostNameType.Unknown;
            if (!isValidIPAddr && !isValidDomain)
            {
                return Result.Failed(1000, "Host should be a valid IP adress or domain name.");
            }
            if (isValidIPAddr)
            {
                if (Address.AddressFamily.Equals(AddressFamily.InterNetwork))
                {
                    if (!Regex.IsMatch(appliance.Host, "^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$"))
                    {
                        return Result.Failed(1000, "Host should be a valid IP adress or domain name.");
                    }
                }
            }


            // port
            if (string.IsNullOrEmpty(appliance.Port))
            {
                return Result.Failed(1001, "Port should not be null or empty.");
            }

            bool isNumeric = int.TryParse(appliance.Port, out int PortAsInt);
            if (isNumeric)
            {
                if (PortAsInt < 1 || PortAsInt > 65535)
                {
                    return Result.Failed(1001, "Port should be a digits between 1 and 65535.");
                }

            }
            else
            {
                return Result.Failed(1001, "Port should be a digits between 1 and 65535.");
            }

            // System Id
            if (string.IsNullOrEmpty(appliance.SystemId))
            {
                return Result.Failed(1001, "SystemId should not be null or empty.");
            }

            if (!Regex.IsMatch(appliance.SystemId, PatternDigits))
            {
                return Result.Failed(1001, "SystemId should contains 1 to 100 characters, which can include letters, digits, hyphens (-), underscores (_), and periods(.).");
            }


            if (appliance.UpdateCredential)
            {
                // Login Account
                if (string.IsNullOrEmpty(appliance.LoginAccount))
                {
                    return Result.Failed(1001, "Account should not be null or empty.");
                }

                if (!Regex.IsMatch(appliance.LoginAccount, PatternDigits))
                {
                    return Result.Failed(1001, "Account should contains 1 to 100 characters, which can include letters, digits, hyphens (-), underscores (_), and periods(.).");
                }


                // Login Password
                if (string.IsNullOrEmpty(appliance.LoginPassword))
                {
                    return Result.Failed(1001, "Password should not be null or empty.");
                }
            }


            return Result.Done();
        }
        #endregion //Validate Credential 

        #region NotifyPropertyChanged 
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion //NotifyPropertyChanged 
    }


}
