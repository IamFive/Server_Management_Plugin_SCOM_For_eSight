﻿using Huawei.SCOM.ESightPlugin.ViewLib.Model;
using Huawei.SCOM.ESightPlugin.ViewLib.Repo;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Huawei.SCOM.ESightPlugin.ViewLib
{
    /// <summary>
    /// EditESightDialog.xaml 的交互逻辑
    /// </summary>
    public partial class EditESightDialog : Window, INotifyPropertyChanged
    {
        private ESightApplianceRepo ESightApplianceRepo { get; set; }

        private bool _updateCredentialChecked = false;
        private ESightAppliance _item = new ESightAppliance();
        private Result _actionResult = Result.Done();

        public bool UpdateCredentialChecked
        {
            get
            {
                return this._updateCredentialChecked;
            }
            set
            {
                if (value != this._updateCredentialChecked)
                {
                    this._updateCredentialChecked = value;
                    this.OnPropertyChanged("UpdateCredentialChecked");
                }
            }
        }

        public ESightAppliance Item
        {
            get => _item;
            set
            {
                if (_item != value)
                {
                    _item = value;
                    this.OnPropertyChanged("ActionResult");
                }
            }
        }
        public Result ActionResult
        {
            get => _actionResult;

            set
            {

                if (_actionResult != value)
                {
                    _actionResult = value;
                    this.OnPropertyChanged("ActionResult");
                }
            }
        }


        public EditESightDialog(ESightApplianceRepo repo)
        {
            InitializeComponent();
            this.ESightApplianceRepo = repo;
            this.DataContext = this;
            this.ShowInTaskbar = false;
        }

        public void SetItem(ESightAppliance item)
        {
            this.Item = item;
            // using binding instead?
            txtHost.Text = item.Host;
            txtAlias.Text = item.AliasName;
            txtPort.Text = item.Port;
            txtSystemId.Text = item.SystemId;
            txtAccount.Text = item.LoginAccount;
            // txtPassword.Password = item.LoginPd;
        }


        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                this.DragMove();
            }
            catch { }
        }

        private void OnCancelBtnClicked(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OnSaveBtnClicked(object sender, RoutedEventArgs e)
        {
            this.ActionResult = Result.Done();
            ESightAppliance credential = new ESightAppliance
            {
                Host = txtHost.Text,
                Port = txtPort.Text,
                SystemId = txtSystemId.Text,
                AliasName = txtAlias.Text,
                LoginAccount = txtAccount.Text,
                LoginPassword = txtPassword.Password,
                UpdateCredential = UpdateCredentialChecked,
            };

            ESightApplianceRepo.Update(credential, UpdateEsightCallback);
        }

        void UpdateEsightCallback(Result result)
        {
            this.ActionResult = result;
            if (result.Success)
            {
                this.Close();
            }
        }

        private void OnTestBtnClicked(object sender, RoutedEventArgs e)
        {
            this.ActionResult = Result.Done();
            ESightAppliance eSight = new ESightAppliance
            {
                Host = txtHost.Text,
                Port = txtPort.Text,
                SystemId = txtSystemId.Text,
                AliasName = txtAlias.Text,
                LoginAccount = txtAccount.Text,
                LoginPassword = txtPassword.Password,
                UpdateCredential = UpdateCredentialChecked,
            };

            ESightApplianceRepo.Test(eSight, TestEsightCallback);
        }

        void TestEsightCallback(Result result)
        {
            this.ActionResult = result;
        }

        private void OnCloseBtnClicked(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        #region NotifyPropertyChanged 
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion //NotifyPropertyChanged 
    }
}
